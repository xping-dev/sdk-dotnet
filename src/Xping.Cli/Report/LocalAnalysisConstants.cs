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
    /// Sessions the subject test must have run in before a finding is emitted (5).
    /// </summary>
    /// <remarks>
    /// The per-test counterpart of <see cref="MinimumSessionsToReport"/>. A test added yesterday has
    /// history in the window but not history about itself.
    /// <para>
    /// Counted in sessions, not executions. A retried test records an execution per attempt, so one
    /// session that retried five times cleared an execution-denominated floor of five on its own —
    /// exactly the shape the floor exists to exclude, and worst for the tests that retry most.
    /// </para>
    /// </remarks>
    public const int MinimumSessionsPerTestToReport = 5;

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
    /// Share of a test's failures the dominant failure mode must account for before the test is
    /// called broken rather than flaky (0.70).
    /// </summary>
    /// <remarks>
    /// Applied alongside <see cref="AlwaysFailingRate"/>, and the reason that threshold is reachable
    /// at all. Failure modes are compared by exact hash over the exception type, the normalised
    /// message and five frames, so two runs of one broken assertion count as two modes whenever the
    /// message carries the data that differed — <c>Expected: "Alice" but was: "Bob"</c> against
    /// <c>"Carol"</c>. Demanding a single mode let a name in an error message decide the most severe
    /// classification the report makes. Below this share the failures genuinely do not agree, and a
    /// test that fails a different way every time is flaky however often it fails.
    /// </remarks>
    public const double AlwaysFailingModalShareMin = 0.70;

    /// <summary>
    /// Share of a test's failures that must be timeouts before it is reported as timing out (0.50).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A test that usually fails an assertion but hung once is still a failing test, and moving it
    /// into the timeout bucket would point its reader at a deadlock that is not the main problem.
    /// A test whose failures are mostly hangs is the reverse: reporting it as an ordinary failure
    /// hands the reader an assertion message that was never written.
    /// </para>
    /// <para>
    /// Compared against the share itself, unlike <see cref="RetryExhaustedShareMin"/>, which carries
    /// the same figure and is compared against its lower bound. The difference is what each decides.
    /// That one decides whether a finding is reported at all, where declining costs the reader a
    /// line; this one decides which of two evidence shapes describes a test that is reported either
    /// way, and the other shape groups failure signatures, which a killed run does not have. Being
    /// cautious here would not say less, it would say the wrong thing — and bounding a threshold of
    /// one half asks for nine kills in ten failures, or fifteen in twenty, at the window sizes this
    /// tool actually sees.
    /// </para>
    /// </remarks>
    public const double TimingOutShareMin = 0.50;

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
    /// Extra attempts a typical passing run must now need before a deepening is reported (1).
    /// </summary>
    /// <remarks>
    /// Both sides of the comparison are nearest-rank medians of attempt numbers, so the difference
    /// is always a whole attempt; a fractional threshold would round to this one and pretend to a
    /// precision the measurement does not have. One is also the smallest change worth a developer's
    /// attention and the most common: a test that used to pass first time and now needs two has
    /// doubled what every one of its runs costs, and a bar of two would silence exactly that case.
    /// </remarks>
    public const int RetryDeepeningMinAttempts = 1;

    /// <summary>
    /// Runs whose retries must have run out before exhaustion is reported (2).
    /// </summary>
    /// <remarks>
    /// One exhausted run is an incident. The claim this kind makes is that retries are not rescuing
    /// the test, and that has to have happened twice before it is a pattern rather than a bad
    /// afternoon — the guard <see cref="TimeSensitiveMinArmDays"/> supplies for a temporal split.
    /// Deliberately stricter than <c>RetryMasked</c>, which reports a single
    /// occurrence: a masked failure is invisible without the report, whereas an exhausted run
    /// already went red in the runner's own output.
    /// <para>
    /// No longer the binding gate. Exhausted runs are a subset of retried ones, so the share's lower
    /// bound is largest when every retried run ran out, and four in four is the first shape that
    /// clears <see cref="RetryExhaustedShareMin"/>. That threshold therefore already implies at
    /// least four, and this one only states the floor the kind was designed around and declines the
    /// common cases without computing an interval. It becomes binding again if the share ever moves.
    /// </para>
    /// </remarks>
    public const int RetryExhaustedMinRuns = 2;

    /// <summary>
    /// Share of a test's retried runs that must have run out before exhaustion is reported (0.50).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately the same figure as <see cref="TimingOutShareMin"/> and applied the same way.
    /// Below half, the retries rescue the test more often than not and the attribute is earning its
    /// keep; reporting that as retries running out would point a reader at the mitigation when the
    /// problem is the test.
    /// </para>
    /// <para>
    /// Applied the same way includes being compared against the 95% Wilson lower bound of the share
    /// rather than the share itself, which is what supplies the denominator that
    /// <see cref="RetryExhaustedMinRuns"/> cannot. The claim this kind makes is about a mechanism —
    /// that retries are not rescuing this test — and two retried runs that both ran out is a point
    /// estimate of 1.00 with nothing behind it. The bound rises towards the share as the runs
    /// accumulate, so the shapes that clear it are four runs in four, seven in eight, ten in twelve
    /// and fifteen in twenty — well above one half, and deliberately so while the denominator is
    /// small. The published <c>ExhaustedRate</c> is unaffected.
    /// </para>
    /// </remarks>
    public const double RetryExhaustedShareMin = 0.50;

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
    /// Distinct sessions each concurrency arm needs before the two are compared (5).
    /// </summary>
    /// <remarks>
    /// The test therefore needs ten sessions in the window, twice the general reporting floor.
    /// At five a side the weakest qualifying signal is around zero-of-five against two-of-five, which
    /// is already thin; below it a single unlucky execution clears
    /// <see cref="ParallelSensitivityDelta"/> on its own and the report starts ranking noise.
    /// <para>
    /// The gate is in sessions while the arms and their rate stay in executions, and the split is
    /// deliberate: a retried test does not repeat one concurrency reading, it takes several, so the
    /// within-session variation is the signal this finding is made of. The gate buys the breadth of
    /// independent occasions the rate cannot supply for itself — two sessions of five attempts each
    /// are one afternoon, not ten observations.
    /// </para>
    /// </remarks>
    public const int ParallelSensitiveMinArmSessions = 5;

    /// <summary>
    /// Difference in failure rate across a test's temporal split that indicates sensitivity (0.30).
    /// </summary>
    /// <remarks>
    /// Deliberately the same figure as <see cref="ParallelSensitivityDelta"/>, and applied the same
    /// way: two arms, an absolute comparison, either direction qualifying. The two findings ask the
    /// same question of different axes, and giving them different bars would mean a gap that counts
    /// as concurrency sensitivity does not count as time sensitivity, for no reason a reader could
    /// discover.
    /// </remarks>
    public const double TimeSensitivityDelta = 0.30;

    /// <summary>
    /// Sessions each side of a temporal split needs before the two are compared (5).
    /// </summary>
    /// <remarks>
    /// Matches <see cref="ParallelSensitiveMinArmSessions"/> for the same reason it was chosen
    /// there: below five a side, one unlucky session clears
    /// <see cref="TimeSensitivityDelta"/> on its own.
    /// <para>
    /// Sessions here are the arms themselves, not merely the gate. A session is read on one clock, so
    /// every attempt of a test within it lands in the same arm at the same local hour — an
    /// execution-denominated arm of five could be two sessions with retries, and its failure rate
    /// carried far less information than its denominator claimed.
    /// </para>
    /// </remarks>
    public const int TimeSensitiveMinArmSessions = 5;

    /// <summary>
    /// Distinct local dates the failing side of a temporal split must span (3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The guard the other thresholds cannot supply. Five failures inside one afternoon clear both
    /// the arm size and the delta, and reporting that as "fails between 12:00 and 18:00" describes a
    /// single bad session in the language of a recurring pattern.
    /// </para>
    /// <para>
    /// Three dates is what makes the claim a pattern rather than an incident: the behaviour has to
    /// have come back, on days that are not each other. It is also why this finding stays quiet on
    /// most windows, which is the correct outcome — a fortnight of runs rarely contains three
    /// separate days that agree.
    /// </para>
    /// </remarks>
    public const int TimeSensitiveMinArmDays = 3;

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
    /// Sessions a test must have run in before its evidence is better than <c>Low</c> (8).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Banded on sessions, which is the unit Xping Cloud already bands on: its
    /// <c>EvidenceLevelThresholds</c> classifies an effective sample size computed over runs that
    /// <c>RunCollapser</c> has reduced to one row per test per session. The unit agrees; the numbers
    /// do not, and deliberately.
    /// </para>
    /// <para>
    /// Cloud looks over a long lookback and can afford 15 and 40. <see cref="DefaultWindowSessions"/>
    /// caps the local window at twenty, so those figures would put <c>High</c> out of reach entirely
    /// and make <c>Moderate</c> mean "present in three quarters of every build the developer has
    /// run". Eight and fifteen carry the same intent against the window the CLI actually has.
    /// </para>
    /// </remarks>
    public const int EvidenceModerateSessions = 8;

    /// <summary>
    /// Sessions above which evidence is <c>High</c> (15).
    /// </summary>
    public const int EvidenceHighSessions = 15;

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
