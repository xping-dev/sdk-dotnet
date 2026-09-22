/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Indexes;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Report.Model;

/// <summary>
/// What one test's failure in the newest session is, against its own history.
/// </summary>
/// <remarks>
/// An observation about one session and not a graded claim, which is why this is not a
/// <see cref="Severity"/>. A <see cref="New"/> failure and a <see cref="SeenBefore"/> one carry no
/// ranking between them; the section they appear in orders them for reading, not for urgency.
/// </remarks>
internal enum LatestRunStatus
{
    /// <summary>The test had passed in every prior session it appeared in.</summary>
    New,

    /// <summary>The test appeared in no prior session in the window.</summary>
    NewTest,

    /// <summary>The test had failed before, and no finding explains it yet.</summary>
    SeenBefore
}

/// <summary>
/// One test that ended the newest session red and carries no finding.
/// </summary>
/// <param name="Status">What the failure is, against the test's history.</param>
/// <param name="Test">The test, as its newest execution named it.</param>
/// <param name="Execution">
/// The deciding attempt that failed. Carried so the renderer can say what went wrong without the
/// analyzer deciding how much of it to say.
/// </param>
/// <param name="PriorSessions">Sessions before this one in which the test recorded a verdict.</param>
/// <param name="PriorFailures">Of those, how many it failed in.</param>
internal sealed record LatestRunFailure(
    LatestRunStatus Status,
    TestReference Test,
    TestExecution Execution,
    int PriorSessions,
    int PriorFailures);

/// <summary>
/// The newest session, read against the sessions before it.
/// </summary>
/// <remarks>
/// <para>
/// A fact about one session plus a count of prior ones. There is no inference in it, no threshold
/// and no correction for how many tests were looked at, which is why it is produced beside the
/// findings and never among them: routing it through a provider would rank it, correct it and grade
/// it, and every one of those would be wrong.
/// </para>
/// <para>
/// Complete, or explicitly not. Every test that ended the session red is either a row, counted in
/// <see cref="ExplainedByFindings"/>, or withheld under a rule the values here make visible:
/// <see cref="Suppressed"/> for a window with nothing to contrast against,
/// <see cref="SessionView.IsLikelyEnvironmental"/> on <see cref="Session"/> for a run the report
/// declines to itemise, and <see cref="FailuresTotal"/> against <see cref="Failures"/> for the
/// cap.
/// </para>
/// </remarks>
/// <param name="Session">The newest analysed session.</param>
/// <param name="Suppressed">
/// Whether the window holds this session alone. Every row would then restate the test runner, so
/// none is produced.
/// </param>
/// <param name="TestsExecuted">Tests that recorded a verdict in this session.</param>
/// <param name="TestsFailed">Of those, how many ended it as a failure.</param>
/// <param name="ExplainedByFindings">Failing tests that a finding already accounts for.</param>
/// <param name="ExplainingFindings">
/// Those findings, each once, in the order they were given — which is the order the report ranks
/// them in.
/// </param>
/// <param name="Failures">The rows, ordered for reading and cut to the cap.</param>
/// <param name="FailuresTotal">Rows before the cap.</param>
internal sealed record LatestRunAnalysis(
    SessionView Session,
    bool Suppressed,
    int TestsExecuted,
    int TestsFailed,
    int ExplainedByFindings,
    IReadOnlyList<Finding> ExplainingFindings,
    IReadOnlyList<LatestRunFailure> Failures,
    int FailuresTotal);
