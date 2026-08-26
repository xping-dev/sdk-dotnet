/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models.Statistics;

/// <summary>
/// Immutable summary statistics calculated locally from running totals before cloud analysis.
/// Accumulated incrementally across all batch uploads to ensure accuracy when batching is active.
/// Only populated on the <c>TestSessionState.Finalized</c> upload.
/// </summary>
/// <remarks>
/// <para>
/// These statistics cover every execution the session recorded. Nothing is dropped between
/// recording and upload, so <see cref="Total"/> equals the combined count of executions across
/// all batch uploads.
/// </para>
/// <para>
/// Two readings of the same session coexist here. The unprefixed counters
/// (<see cref="Total"/>, <see cref="Passed"/>, <see cref="Failed"/>, <see cref="Timeout"/>, …) count <b>executions</b>,
/// so every retry attempt is counted separately. The <c>Final*</c> counters
/// (<see cref="DistinctTests"/>, <see cref="FinalPassed"/>, <see cref="FinalFailed"/>, …) count
/// <b>distinct tests</b>, each one contributing only the outcome of its highest-numbered attempt.
/// A suite that went green on retry therefore reports <see cref="FinalSuccessRate"/> of 1.0 while
/// <see cref="SuccessRate"/> stays below it, and <see cref="Total"/> minus
/// <see cref="DistinctTests"/> is the number of retry attempts the runner performed.
/// </para>
/// <para>
/// Duration values are expressed in whole milliseconds for cross-platform serialization
/// compatibility.
/// </para>
/// </remarks>
public sealed class QuickStatistics
{
    /// <summary>
    /// Parameterless constructor for JSON deserialization.
    /// </summary>
    public QuickStatistics()
    {
        Total = 0;
        Passed = 0;
        Failed = 0;
        Skipped = 0;
        Inconclusive = 0;
        NotExecuted = 0;
        Timeout = 0;
        SuccessRate = 0.0;
        DistinctTests = 0;
        FinalPassed = 0;
        FinalFailed = 0;
        FinalSkipped = 0;
        FinalInconclusive = 0;
        FinalNotExecuted = 0;
        FinalTimeout = 0;
        FinalSuccessRate = 0.0;
        TotalDurationMs = 0L;
        WallClockDurationMs = 0L;
        AverageDurationMs = 0L;
        SlowestTestName = null;
        SlowestTestDurationMs = 0L;
    }

    /// <summary>
    /// Internal constructor for creation by <c>IRunningStatisticsAccumulator</c>.
    /// </summary>
    /// <remarks>
    /// The distinct-test counters are not parameters; the accumulator sets them through an object
    /// initializer on top of this constructor, keeping the parameter list readable.
    /// </remarks>
    internal QuickStatistics(
        int total,
        int passed,
        int failed,
        int skipped,
        int inconclusive,
        int notExecuted,
        int timeout,
        double successRate,
        long totalDurationMs,
        long wallClockDurationMs,
        long averageDurationMs,
        string? slowestTestName,
        long slowestTestDurationMs)
    {
        Total = total;
        Passed = passed;
        Failed = failed;
        Skipped = skipped;
        Inconclusive = inconclusive;
        NotExecuted = notExecuted;
        Timeout = timeout;
        SuccessRate = successRate;
        TotalDurationMs = totalDurationMs;
        WallClockDurationMs = wallClockDurationMs;
        AverageDurationMs = averageDurationMs;
        SlowestTestName = slowestTestName;
        SlowestTestDurationMs = slowestTestDurationMs;
    }

    /// <summary>
    /// Gets the total number of test executions recorded in this session.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Gets the number of tests that passed.
    /// </summary>
    public int Passed { get; init; }

    /// <summary>
    /// Gets the number of tests that failed.
    /// </summary>
    public int Failed { get; init; }

    /// <summary>
    /// Gets the number of tests that were skipped.
    /// </summary>
    public int Skipped { get; init; }

    /// <summary>
    /// Gets the number of tests with an inconclusive result.
    /// </summary>
    public int Inconclusive { get; init; }

    /// <summary>
    /// Gets the number of tests that were not executed.
    /// </summary>
    public int NotExecuted { get; init; }

    /// <summary>
    /// Gets the number of tests killed by their framework for exceeding a timeout.
    /// </summary>
    /// <remarks>
    /// Counted apart from <see cref="Failed"/> because a hang and a failed assertion are different
    /// defects. Both are failures for the purpose of <see cref="SuccessRate"/>, which counts only
    /// passes in its numerator, so a timeout has never inflated it.
    /// </remarks>
    public int Timeout { get; init; }

    /// <summary>
    /// Gets the proportion of executions that passed, as a ratio from 0.0 to 1.0.
    /// Returns 0.0 when <see cref="Total"/> is zero.
    /// </summary>
    /// <remarks>
    /// This is <see cref="Passed"/> divided by <see cref="Total"/>, so a test that failed once and
    /// passed on retry lowers the ratio even though the suite ended green. See
    /// <see cref="FinalSuccessRate"/> for the test-level reading.
    /// </remarks>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Gets the number of distinct tests recorded in this session, counting all attempts of a
    /// retried test as one test.
    /// </summary>
    /// <remarks>
    /// Tests are identified by <c>TestIdentity.TestFingerprint</c> within an assembly. When an
    /// execution carries no fingerprint, the fully qualified name identifies it instead, then the
    /// test name; an execution carrying none of those counts as its own distinct test rather than
    /// merging with every other unidentified one. <see cref="Total"/> minus this value is the number
    /// of retry attempts performed.
    /// </remarks>
    public int DistinctTests { get; init; }

    /// <summary>
    /// Gets the number of distinct tests whose highest-numbered attempt passed.
    /// </summary>
    public int FinalPassed { get; init; }

    /// <summary>
    /// Gets the number of distinct tests whose highest-numbered attempt failed.
    /// </summary>
    public int FinalFailed { get; init; }

    /// <summary>
    /// Gets the number of distinct tests whose highest-numbered attempt was skipped.
    /// </summary>
    public int FinalSkipped { get; init; }

    /// <summary>
    /// Gets the number of distinct tests whose highest-numbered attempt was inconclusive.
    /// </summary>
    public int FinalInconclusive { get; init; }

    /// <summary>
    /// Gets the number of distinct tests whose highest-numbered attempt was not executed.
    /// </summary>
    public int FinalNotExecuted { get; init; }

    /// <summary>
    /// Gets the number of distinct tests whose highest-numbered attempt timed out.
    /// </summary>
    public int FinalTimeout { get; init; }

    /// <summary>
    /// Gets the proportion of distinct tests that ended passed, as a ratio from 0.0 to 1.0.
    /// Returns 0.0 when <see cref="DistinctTests"/> is zero.
    /// </summary>
    /// <remarks>
    /// This is <see cref="FinalPassed"/> divided by <see cref="DistinctTests"/>: the answer to
    /// "did the suite pass", unaffected by attempts a retry later recovered from.
    /// </remarks>
    public double FinalSuccessRate { get; init; }

    /// <summary>
    /// Gets the combined duration of all test executions in milliseconds.
    /// </summary>
    public long TotalDurationMs { get; init; }

    /// <summary>
    /// Gets the wall-clock duration of the entire session, from initialization to finalization,
    /// in milliseconds. Unlike <see cref="TotalDurationMs"/>, this includes framework overhead
    /// such as fixture setup/teardown and test discovery, not just time spent inside test bodies.
    /// </summary>
    public long WallClockDurationMs { get; init; }

    /// <summary>
    /// Gets the mean duration per test in milliseconds.
    /// Returns 0 when <see cref="Total"/> is zero.
    /// </summary>
    public long AverageDurationMs { get; init; }

    /// <summary>
    /// Gets the display name of the slowest test, or <c>null</c> when no tests were recorded.
    /// </summary>
    public string? SlowestTestName { get; init; }

    /// <summary>
    /// Gets the duration of the slowest test in milliseconds.
    /// </summary>
    public long SlowestTestDurationMs { get; init; }
}
