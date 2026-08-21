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
internal sealed record AnalysisResult(
    IReadOnlyList<Finding> Findings,
    IReadOnlyList<string> FailedProviders,
    int ExcludedLowEvidence)
{
    /// <summary>Gets an empty result.</summary>
    public static AnalysisResult Empty { get; } = new([], [], 0);
}

/// <summary>
/// Runs every provider and assembles their output into one ranked set.
/// </summary>
/// <remarks>
/// This is where the rules that must not vary by kind live: the reporting floor, the evidence bands,
/// the impact formula and the sort. Providers observe; the coordinator judges. Pushing any of this
/// into providers is how six independently written metrics end up with three severity models.
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
        var findings = new List<Finding>();
        var failed = new List<string>();
        int excluded = 0;

        // Ordered by name so that provider execution — and therefore the order equally-ranked
        // findings were produced in — does not depend on registration or reflection order.
        foreach (IFindingProvider provider in providers.OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (kinds != null && !provider.Kinds.Any(kinds.Contains))
                continue;

            IReadOnlyList<FindingCandidate> candidates;

            try
            {
                candidates = [.. provider.Analyze(context)];
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

            foreach (FindingCandidate candidate in candidates)
            {
                if (kinds != null && !kinds.Contains(candidate.Kind))
                    continue;

                int executions = EvidenceLevelResolver.CountExecutions(candidate.Subject, context.Tests);

                if (!EvidenceLevelResolver.MeetsReportingFloor(executions, context.Window.SessionCount))
                {
                    excluded++;
                    continue;
                }

                double impact = ImpactScorer.Score(FindingCandidateInputs.From(candidate), context.Tests);

                findings.Add(new Finding(
                    FindingId.Compute(candidate.Kind, candidate.Subject.SortKey),
                    candidate.Kind,
                    candidate.Cap(ImpactScorer.Band(impact)),
                    EvidenceLevelResolver.Resolve(executions),
                    candidate.Subject,
                    candidate.Evidence,
                    candidate.DrillDownCommand,
                    impact));
            }
        }

        findings.Sort(FindingOrder.Instance);
        failed.Sort(StringComparer.Ordinal);

        return new AnalysisResult(findings, failed, excluded);
    }
}
