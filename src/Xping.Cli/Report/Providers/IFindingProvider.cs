/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

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
    /// <returns>Candidate findings, in any order.</returns>
    IEnumerable<FindingCandidate> Analyze(AnalysisContext context);
}
