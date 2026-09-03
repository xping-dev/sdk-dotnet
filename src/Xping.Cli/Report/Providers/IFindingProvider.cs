/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.ObjectModel;
using Xping.Cli.Report.Model;

namespace Xping.Cli.Report.Providers;

/// <summary>
/// What a provider hands back for one subject, before severity and evidence are resolved.
/// </summary>
/// <remarks>
/// A provider states what it observed and how unreliable that makes the subject. It does not band
/// severity, compute an evidence level or apply the reporting floor: those are defined once, for
/// every kind, by the coordinator. A provider that scored its own findings would drift from the
/// others the first time a threshold moved.
/// </remarks>
/// <param name="Kind">What this candidate claims.</param>
/// <param name="Subject">The test or group it claims it about.</param>
/// <param name="Evidence">The kind-specific observations behind the claim.</param>
/// <param name="Unreliability">
/// How unreliable the subject is, in [0,1], defined per kind. The dominant term in the impact score.
/// Wherever the kind's measure is a proportion, this is a lower confidence bound on it rather than
/// the ratio itself — see <see cref="Scoring.WilsonInterval"/> — so that a finding grows with the
/// runs behind it instead of ranking <c>2 of 2</c> alongside <c>50 of 50</c>. Evidence records still
/// publish the point estimate, because a reader wants "4 of 20" and not "0.31".
/// </param>
/// <param name="SessionsSinceLastOccurrence">
/// How many sessions back the behaviour was last seen; 0 means the newest session.
/// </param>
/// <param name="DrillDownCommand">The exact CLI invocation that expands this finding.</param>
/// <param name="PValue">
/// How probable an observation this extreme would be if the kind's claim were false, or
/// <see langword="null"/> where no hypothesis was tested. Null is not "not computed yet": kinds like
/// <c>RetryMasked</c> and <c>SharedFailure</c> count things that demonstrably happened, and a
/// probability of their happening by chance is not a question. The coordinator carries this so that
/// a multiplicity correction can be applied once, across every fingerprint a kind was tested on —
/// which a provider cannot do, because by contract it cannot see the others.
/// </param>
/// <param name="SeverityCeiling">
/// The most severe band this kind may reach, or <see langword="null"/> for no cap.
/// </param>
internal sealed record FindingCandidate(
    FindingKind Kind,
    FindingSubject Subject,
    FindingEvidence Evidence,
    double Unreliability,
    int SessionsSinceLastOccurrence,
    string DrillDownCommand,
    double? PValue = null,
    Severity? SeverityCeiling = null)
{
    /// <summary>
    /// Applies this candidate's ceiling to a banded severity.
    /// </summary>
    /// <param name="banded">What the impact score alone would say.</param>
    /// <returns>The capped severity.</returns>
    /// <remarks>
    /// Some kinds are worth reporting but never worth interrupting anyone over — a test that stopped
    /// running is usually a deliberate deletion. Without a cap the generic impact formula would rank
    /// such a finding above a genuinely failing test, because "ran constantly and then stopped"
    /// scores highly on every term the formula measures.
    /// </remarks>
    public Severity Cap(Severity banded) =>
        SeverityCeiling is { } ceiling && banded < ceiling ? ceiling : banded;
}

/// <summary>
/// Produces findings of one kind, or of a group of closely related kinds.
/// </summary>
/// <remarks>
/// <para>
/// Providers are discovered through dependency injection rather than by scanning the assembly:
/// reflection order is not guaranteed to be stable, and the report must be byte-identical between
/// runs. Adding a provider is one registration line in the CLI's service collection.
/// </para>
/// <para>
/// A provider that throws does not take the report down. The coordinator records the failure against
/// <see cref="Name"/> and renders everything else — one broken metric must never cost a developer
/// the whole command.
/// </para>
/// </remarks>
internal interface IFindingProvider
{
    /// <summary>
    /// Gets a stable name, used to order providers and to name failures in the report summary.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the kinds this provider can emit.
    /// </summary>
    /// <remarks>
    /// Plural because some kinds only make sense decided together: whether a failing test is flaky
    /// or simply broken is one judgement, and splitting it across two providers would let both claim
    /// the same test.
    /// </remarks>
    IReadOnlyList<FindingKind> Kinds { get; }

    /// <summary>
    /// Examines the window and returns what it observed.
    /// </summary>
    /// <param name="context">The window, sessions and shared indexes.</param>
    /// <returns>Candidate findings, in any order, and the size of every family behind them.</returns>
    ProviderReport Analyze(AnalysisContext context);
}

/// <summary>
/// What one provider observed in a window, and how many questions it asked to observe it.
/// </summary>
/// <remarks>
/// <para>
/// The second half is the reason this is a record rather than a list. A p-value on its own cannot
/// be judged: the same 0.02 is strong evidence from one comparison and the commonest thing three
/// hundred comparisons produce. The coordinator applies
/// <see cref="Scoring.BenjaminiHochberg"/> once per kind and needs the denominator, and only the
/// provider knows it — the count includes every fingerprint whose answer never became a candidate,
/// which by definition is not in <see cref="Candidates"/>.
/// </para>
/// <para>
/// Eager rather than an iterator, unlike the shape this replaced. A family size is not known until
/// the last fingerprint has been examined, so a provider that streamed its candidates would have to
/// publish the count before it had it. Nothing is lost: the coordinator materialised every
/// provider's output before reading any of it in any case, because a provider that throws must cost
/// its own findings and no one else's.
/// </para>
/// </remarks>
/// <param name="Candidates">What the provider is claiming, in any order.</param>
/// <param name="HypothesesTested">
/// Per kind, the number of fingerprints on which that kind's hypothesis test was well posed —
/// counted where the last precondition on the shape of the data is met, and before any gate that
/// reads the data's direction or magnitude. Absent for a kind that tests no hypothesis. Counting it
/// after the gates instead would report a family in which every member is a discovery, and correct
/// for nothing.
/// </param>
internal sealed record ProviderReport(
    IReadOnlyList<FindingCandidate> Candidates,
    IReadOnlyDictionary<FindingKind, int> HypothesesTested)
{
    /// <summary>
    /// A report from a provider that counted things rather than testing anything.
    /// </summary>
    /// <param name="candidates">What it counted.</param>
    /// <returns>A report claiming no family.</returns>
    /// <remarks>
    /// <c>RetryMasked</c>, <c>SharedFailure</c> and <c>BrokenFixture</c> are observations of things
    /// that demonstrably happened. There is no null hypothesis to reject and so nothing to correct
    /// for, and the empty family is what carries them past the multiplicity pass untouched.
    /// </remarks>
    public static ProviderReport Observations(IReadOnlyList<FindingCandidate> candidates) =>
        new(candidates, ReadOnlyDictionary<FindingKind, int>.Empty);
}

/// <summary>
/// What examining one fingerprint produced.
/// </summary>
/// <remarks>
/// The distinction the pair exists to draw is between a fingerprint the question could not be asked
/// of and one it was asked of and answered no. Both yield no candidate, and only the second is a
/// hypothesis test that the multiplicity correction has to be charged for. A provider returning a
/// bare <c>FindingCandidate?</c> conflates them, and undercounting the family is the direction that
/// invents findings.
/// </remarks>
/// <param name="Tested">Whether the kind's hypothesis test was computed on this fingerprint.</param>
/// <param name="Candidate">What survived the gates after it, if anything did.</param>
internal readonly record struct Examination(bool Tested, FindingCandidate? Candidate)
{
    /// <summary>Gets the result for a fingerprint the question could not be asked of.</summary>
    public static Examination NotPosed { get; }

    /// <summary>
    /// The result for a fingerprint the test was computed on, whatever the gates then said.
    /// </summary>
    /// <param name="candidate">The candidate, or <see langword="null"/> if a gate declined it.</param>
    /// <returns>An examination that counts towards the family.</returns>
    public static Examination Of(FindingCandidate? candidate) => new(true, candidate);
}
