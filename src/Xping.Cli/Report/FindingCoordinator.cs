/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Cli.Report.Scoring;

namespace Xping.Cli.Report;

/// <summary>
/// The findings a report is built from, and what happened while producing them.
/// </summary>
/// <param name="Findings">The surviving findings, most severe first.</param>
/// <param name="FailedProviders">Names of providers that threw.</param>
/// <param name="ExcludedLowEvidence">Candidates dropped for resting on too little data.</param>
/// <param name="ExcludedNotSignificant">
/// Candidates dropped because their kind's comparison, charged for every fingerprint it was run on,
/// no longer said anything. Published rather than absorbed: a report that has quietly discarded
/// eleven candidates and one that had nothing to discard are the same empty block, and a reader who
/// cannot tell them apart learns the wrong thing from silence.
/// </param>
internal sealed record AnalysisResult(
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<string> FailedProviders,
    int ExcludedLowEvidence,
    int ExcludedNotSignificant)
{
    /// <summary>Gets an empty result.</summary>
    public static AnalysisResult Empty { get; } = new([], [], 0, 0);
}

/// <summary>
/// Runs every provider and assembles their output into one ranked set.
/// </summary>
/// <remarks>
/// <para>
/// This is where the rules that must not vary by kind live: the reporting floor, the evidence bands,
/// the multiplicity correction, the impact formula and the sort. Providers observe; the coordinator
/// judges. Pushing any of this into providers is how six independently written metrics end up with
/// three severity models.
/// </para>
/// <para>
/// The multiplicity correction in particular can only live here. A provider by contract sees one
/// kind and cannot see how many fingerprints the other providers asked their question of, nor —
/// more to the point — can it judge its own p-values against the number of times it produced one.
/// It reports the size of the family it tested and this decides what that family may claim.
/// </para>
/// </remarks>
internal sealed class FindingCoordinator(IEnumerable<IFindingProvider> providers)
{
    /// <summary>
    /// Runs the enabled providers over one window.
    /// </summary>
    /// <param name="context">The window, sessions and shared indexes.</param>
    /// <param name="kinds">Kinds to restrict to, or <see langword="null"/> for all of them.</param>
    /// <param name="warnings">Receives one line per provider failure.</param>
    /// <returns>The ranked findings and what went wrong producing them.</returns>
    public AnalysisResult Run(
        AnalysisContext context, IReadOnlySet<FindingKind>? kinds, TextWriter warnings)
    {
        var failed = new List<string>();

        // Collected rather than emitted, because nothing here can be judged one candidate at a
        // time. Whether a p-value is worth reporting depends on how many fingerprints the kind was
        // tested on and on what the other survivors read, neither of which is known until every
        // provider has run.
        var surviving = new List<(FindingCandidate Candidate, int Sessions)>();
        var tested = new Dictionary<FindingKind, int>();

        int lowEvidence = 0;

        // Ordered by name so that provider execution — and therefore the order equally-ranked
        // findings were produced in — does not depend on registration or reflection order.
        foreach (IFindingProvider provider in providers.OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (kinds != null && !provider.Kinds.Any(kinds.Contains))
                continue;

            ProviderReport report;

            try
            {
                report = provider.Analyze(context);
            }
            // Deliberately broad. A provider is a self-contained metric, and the contract is that one
            // of them failing costs its own findings and nothing else. Narrowing this to anticipated
            // exception types would mean the first unanticipated one takes down `xping report`.
            catch (Exception ex)
            {
                failed.Add(provider.Name);
                warnings.WriteLine($"warning: provider '{provider.Name}' failed: {ex.Message}");
                continue;
            }

            // Filtered by the same kind set as the candidates are, so that `--kind` narrows a family
            // and its members together and cannot change what survives within a kind it kept.
            foreach ((FindingKind kind, int count) in report.HypothesesTested)
            {
                if (kinds == null || kinds.Contains(kind))
                    tested[kind] = tested.GetValueOrDefault(kind) + count;
            }

            foreach (FindingCandidate candidate in report.Candidates)
            {
                if (kinds != null && !kinds.Contains(candidate.Kind))
                    continue;

                int sessions = EvidenceLevelResolver.CountSessions(candidate.Subject, context.Tests);

                if (!EvidenceLevelResolver.MeetsReportingFloor(sessions, context.Window.SessionCount))
                {
                    lowEvidence++;
                    continue;
                }

                surviving.Add((candidate, sessions));
            }
        }

        Dictionary<FindingKind, double?> cutoffs = Cutoffs(surviving, tested);

        var findings = new List<Finding>();
        int notSignificant = 0;

        foreach ((FindingCandidate collected, int sessions) in surviving)
        {
            FindingCandidate? candidate = Reported(collected, cutoffs);

            if (candidate == null)
            {
                notSignificant++;
                continue;
            }

            double impact = ImpactScorer.Score(FindingCandidateInputs.From(candidate), context.Tests);

            findings.Add(new Finding(
                FindingId.Compute(candidate.Kind, candidate.Subject.SortKey),
                candidate.Kind,
                candidate.Cap(ImpactScorer.Band(impact)),
                EvidenceLevelResolver.Resolve(sessions),
                candidate.Subject,
                candidate.Evidence,
                candidate.DrillDownCommand,
                impact));
        }

        findings.Sort(FindingOrder.Instance);
        failed.Sort(StringComparer.Ordinal);

        return new AnalysisResult(findings, failed, lowEvidence, notSignificant);
    }

    /// <summary>
    /// Resolves, per kind, the largest p-value that kind's family admits.
    /// </summary>
    /// <param name="surviving">Candidates that cleared the reporting floor.</param>
    /// <param name="tested">Fingerprints each kind formed its hypothesis test on.</param>
    /// <returns>A cutoff per tested kind; the value is null where the kind admits nothing.</returns>
    /// <remarks>
    /// <para>
    /// Per kind rather than over everything at once, because the kinds are not one family. They ask
    /// unrelated questions of the same fingerprints — is this slower, does it fail in the evening,
    /// has it stopped running — and pooling them would let a suite with one clear duration
    /// regression pay for a clock finding that had nothing to do with it.
    /// </para>
    /// <para>
    /// The reporting floor has already run, so a kind's p-values here are those of its candidates
    /// that cleared it, while the denominator remains everything the kind was tested on. That
    /// direction is the safe one: dropping p-values can only lower the ranks of those that remain,
    /// and a lower rank is a stricter bar. The reverse — correcting against a family narrowed to
    /// what survived — is what makes a correction weaker than the data warrants.
    /// </para>
    /// </remarks>
    private static Dictionary<FindingKind, double?> Cutoffs(
        IReadOnlyList<(FindingCandidate Candidate, int Sessions)> surviving,
        IReadOnlyDictionary<FindingKind, int> tested)
    {
        var byKind = new Dictionary<FindingKind, List<double>>();

        foreach ((FindingCandidate candidate, _) in surviving)
        {
            if (candidate.PValue is not { } p)
                continue;

            if (!byKind.TryGetValue(candidate.Kind, out List<double>? values))
                byKind[candidate.Kind] = values = [];

            values.Add(p);
        }

        var cutoffs = new Dictionary<FindingKind, double?>();

        foreach ((FindingKind kind, List<double> values) in byKind)
        {
            cutoffs[kind] = BenjaminiHochberg.Cutoff(
                values, tested.GetValueOrDefault(kind), LocalAnalysisConstants.FalseDiscoveryRate);
        }

        return cutoffs;
    }

    /// <summary>
    /// Resolves what to report about one subject, or nothing where the pass silenced it.
    /// </summary>
    /// <param name="collected">The candidate a provider offered.</param>
    /// <param name="cutoffs">The cutoff resolved for each tested kind.</param>
    /// <returns>The candidate to report, or <see langword="null"/>.</returns>
    /// <remarks>
    /// A candidate that does not clear its bar hands over to its alternative where it named one.
    /// The alternative is a weaker claim about the same subject that the provider suppressed only
    /// because the stronger one held, and it is resolved here rather than there because whether the
    /// stronger one holds is not known until this pass has run. Without the handover a provider's
    /// suppression would outlive the finding that justified it.
    /// </remarks>
    private static FindingCandidate? Reported(
        FindingCandidate collected, Dictionary<FindingKind, double?> cutoffs)
    {
        if (Survives(collected, cutoffs))
            return collected;

        // One step and no chain. An alternative is what to say when a claim is silenced, not a
        // ladder to climb until something sticks, and a provider that wanted two of them would be
        // describing a ranking rather than a substitution.
        return collected.Instead is { } instead && Survives(instead, cutoffs) ? instead : null;
    }

    /// <summary>
    /// Decides whether one candidate clears its kind's cutoff.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="cutoffs">The cutoff resolved for each tested kind.</param>
    /// <returns><see langword="true"/> where the candidate may be reported.</returns>
    /// <remarks>
    /// A candidate carrying no p-value passes untouched, and that is the whole of the rule for
    /// every kind the retry and failure-mode providers emit — <c>RetryMasked</c>,
    /// <c>RetryDeepening</c>, <c>RetryExhausted</c>, <c>Flaky</c>, <c>AlwaysFailing</c>,
    /// <c>TimingOut</c>, <c>BrokenFixture</c> and <c>SharedFailure</c> — and for
    /// <c>DurationUnstable</c>. They count things that demonstrably happened; there is no null
    /// hypothesis under which a retry that masked a failure did not happen, and correcting a count
    /// for multiplicity would be answering a question nobody asked. Nine of the thirteen kinds
    /// bypass this pass entirely, which is why a suite of three hundred flaky tests still reports
    /// three hundred findings.
    /// </remarks>
    private static bool Survives(
        FindingCandidate candidate, Dictionary<FindingKind, double?> cutoffs)
    {
        if (candidate.PValue is not { } p)
            return true;

        return cutoffs.TryGetValue(candidate.Kind, out double? cutoff) &&
               cutoff is { } bar &&
               p <= bar;
    }
}
