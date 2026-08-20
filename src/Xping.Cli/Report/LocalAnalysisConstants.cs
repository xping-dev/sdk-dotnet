/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report;

/// <summary>
/// Every threshold local analysis uses.
/// </summary>
/// <remarks>
/// <para>
/// One class, no configuration surface. Configuration is a later decision, taken once we know which
/// thresholds users actually disagree with — shipping knobs first would freeze a guess into a public
/// contract.
/// </para>
/// <para>
/// These values are provisional but binding. An implementer who believes one is wrong raises a spec
/// amendment rather than changing it inline, because six sessions each nudging a threshold produces
/// a report nobody can reason about.
/// </para>
/// </remarks>
internal static class LocalAnalysisConstants
{
    /// <summary>
    /// Sessions a window must contain before any finding is emitted (5).
    /// </summary>
    /// <remarks>
    /// Below this, a single unlucky run dominates every rate. Reporting from three sessions would
    /// tell a developer their test is 33% flaky on the strength of one failure.
    /// </remarks>
    public const int MinimumSessionsToReport = 5;

    /// <summary>
    /// Executions of the subject test a window must contain before a finding is emitted (5).
    /// </summary>
    /// <remarks>
    /// The per-test counterpart of <see cref="MinimumSessionsToReport"/>. A test added yesterday has
    /// history in the window but not history about itself.
    /// </remarks>
    public const int MinimumExecutionsToReport = 5;

    /// <summary>
    /// Sessions in the default window (20).
    /// </summary>
    /// <remarks>
    /// Enough for a rate to mean something, few enough that a fix made last week is not drowned by
    /// the month before it.
    /// </remarks>
    public const int DefaultWindowSessions = 20;

    /// <summary>
    /// Days in the default window (14).
    /// </summary>
    /// <remarks>
    /// Applied together with <see cref="DefaultWindowSessions"/>, whichever yields fewer sessions.
    /// A developer returning from leave should not have last month's flakiness reported as current.
    /// </remarks>
    public const int DefaultWindowDays = 14;

    /// <summary>
    /// Sessions forming the "now" side of a delta (3).
    /// </summary>
    /// <remarks>
    /// Drops to 1 in windows smaller than <see cref="SmallWindowSessionCount"/>, where three
    /// sessions would be most of the history and leave nothing to compare against.
    /// </remarks>
    public const int CurrentSliceSize = 3;

    /// <summary>
    /// Window size below which the current slice narrows to a single session (8).
    /// </summary>
    public const int SmallWindowSessionCount = 8;

    /// <summary>
    /// Failure rate at or above which a test is broken rather than flaky (0.90).
    /// </summary>
    /// <remarks>
    /// Not 1.0: a test that fails 19 times in 20 is broken, and the one pass is noise. Leaving it in
    /// the flaky bucket is how a real regression gets ignored.
    /// </remarks>
    public const double AlwaysFailingRate = 0.90;

    /// <summary>
    /// Distinct tests failing with one signature before the cluster is reported as shared (3).
    /// </summary>
    /// <remarks>
    /// Two tests failing alike is a coincidence worth ignoring; three is a cause. This is the
    /// threshold that turns "47 failures" into "3 causes".
    /// </remarks>
    public const int SharedFailureMinTests = 3;

    /// <summary>
    /// Relative p50 duration increase required to report a regression (0.50).
    /// </summary>
    public const double DurationRegressionPct = 0.50;

    /// <summary>
    /// Absolute p50 duration increase required to report a regression, in milliseconds (100).
    /// </summary>
    /// <remarks>
    /// Guards the relative test: 2 ms becoming 4 ms is a 100% regression and means nothing.
    /// </remarks>
    public const double DurationRegressionMinMs = 100;

    /// <summary>
    /// Highest baseline coefficient of variation a regression may be claimed against (0.50).
    /// </summary>
    /// <remarks>
    /// A test with historically huge variance has not "regressed" when it happens to run slow; it
    /// has done what it always does.
    /// </remarks>
    public const double DurationStableCvMax = 0.50;

    /// <summary>
    /// Coefficient of variation at or above which a test's duration is called unstable (0.50).
    /// </summary>
    public const double DurationUnstableCvMin = 0.50;

    /// <summary>
    /// Baseline p50 below which duration findings are suppressed, in milliseconds (50).
    /// </summary>
    /// <remarks>
    /// Below this, the coefficient of variation measures scheduler noise rather than the test.
    /// </remarks>
    public const double DurationTrivialMs = 50;

    /// <summary>
    /// Failure rate given a predecessor required to suspect order dependence (0.70).
    /// </summary>
    public const double OrderDependentConditionalMin = 0.70;

    /// <summary>
    /// Highest unconditional failure rate an order-dependent test may have (0.30).
    /// </summary>
    /// <remarks>
    /// The pair of bounds is the finding: high failure after one predecessor, low failure otherwise.
    /// A test that fails everywhere is not order-dependent, it is broken.
    /// </remarks>
    public const double OrderDependentUnconditionalMax = 0.30;

    /// <summary>
    /// Executions preceded by the same test required before the pairing counts (5).
    /// </summary>
    public const int OrderDependentMinPairings = 5;

    /// <summary>
    /// Difference in failure rate across a test's concurrency split that indicates sensitivity (0.30).
    /// </summary>
    /// <remarks>
    /// Applied across the median split described in §5.8, not to a parallel-versus-serial boolean.
    /// The comparison is absolute, so it catches a test that fails more when it runs nearly alone as
    /// well as one that fails more when the suite is crowded.
    /// </remarks>
    public const double ParallelSensitivityDelta = 0.30;

    /// <summary>
    /// Executions each concurrency arm needs before the two are compared (5).
    /// </summary>
    /// <remarks>
    /// The test therefore needs ten executions in the window, twice the general reporting floor.
    /// At five a side the weakest qualifying signal is around zero-of-five against two-of-five, which
    /// is already thin; below it a single unlucky execution clears
    /// <see cref="ParallelSensitivityDelta"/> on its own and the report starts ranking noise.
    /// </remarks>
    public const int ParallelSensitiveMinArmExecutions = 5;

    /// <summary>
    /// Session failure rate at which the session itself is suspected, not the tests (0.30).
    /// </summary>
    /// <remarks>
    /// Applied together with <see cref="EnvironmentalSessionMinFailures"/>. Without this, one broken
    /// Docker daemon poisons every test's history and the whole report becomes noise.
    /// </remarks>
    public const double EnvironmentalSessionFailureRate = 0.30;

    /// <summary>
    /// Failures a session needs before it can be discounted as environmental (10).
    /// </summary>
    /// <remarks>
    /// Guards the rate: a five-test suite with two failures is at 40% and is not an outage.
    /// </remarks>
    public const int EnvironmentalSessionMinFailures = 10;

    /// <summary>
    /// Stack frames contributing to a failure signature (5).
    /// </summary>
    public const int SignatureFrameCount = 5;

    /// <summary>
    /// Characters of raw message text an exemplar may carry before elision (500).
    /// </summary>
    public const int ExemplarCharBudget = 500;

    /// <summary>
    /// Impact at or above which a finding is <c>High</c> severity (0.60).
    /// </summary>
    public const double SeverityHighThreshold = 0.60;

    /// <summary>
    /// Impact at or above which a finding is <c>Medium</c> severity (0.30).
    /// </summary>
    public const double SeverityMediumThreshold = 0.30;

    /// <summary>
    /// Executions below which evidence is <c>Low</c> (15).
    /// </summary>
    /// <remarks>
    /// These bands are shared verbatim with Xping Cloud. Divergence here produces a product
    /// that contradicts itself in front of a customer.
    /// </remarks>
    public const int EvidenceModerateExecutions = 15;

    /// <summary>
    /// Executions above which evidence is <c>High</c> (40).
    /// </summary>
    public const int EvidenceHighExecutions = 40;

    /// <summary>
    /// Sessions over which the recency term halves (5).
    /// </summary>
    /// <remarks>
    /// Used as <c>0.5 ^ (sessionsSinceLastOccurrence / RecencyHalfLifeSessions)</c>, so a test that
    /// last misbehaved five sessions ago counts half as much as one that misbehaved in the newest.
    /// </remarks>
    public const double RecencyHalfLifeSessions = 5.0;

    /// <summary>
    /// Findings shown before the list is truncated (10).
    /// </summary>
    public const int DefaultTopFindings = 10;

    /// <summary>
    /// Sessions a fingerprint must appear in, in the baseline slice, before its absence counts (3).
    /// </summary>
    /// <remarks>
    /// A test seen once and never again was probably never really there — a mistyped filter, a
    /// parameterized case whose arguments changed. Three appearances make the absence a change.
    /// </remarks>
    public const int VanishedMinBaselineSessions = 3;
}
