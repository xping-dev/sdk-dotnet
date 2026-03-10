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
/// These statistics represent the full test population, regardless of the sampling rate
/// configured for the <c>Executions</c> payload. When sampling is active, <see cref="Total"/>
/// may exceed the combined count of executions across all batch uploads
/// (i.e. <see cref="Total"/> may be greater than the sum of all uploaded execution counts).
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
        SuccessRate = 0.0;
        TotalDurationMs = 0L;
        AverageDurationMs = 0L;
        SlowestTestName = null;
        SlowestTestDurationMs = 0L;
    }

    /// <summary>
    /// Internal constructor for creation by <c>IRunningStatisticsAccumulator</c>.
    /// </summary>
    internal QuickStatistics(
        int total,
        int passed,
        int failed,
        int skipped,
        int inconclusive,
        int notExecuted,
        double successRate,
        long totalDurationMs,
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
        SuccessRate = successRate;
        TotalDurationMs = totalDurationMs;
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
    /// Gets the percentage of tests that passed, from 0.0 to 100.0.
    /// Returns 0.0 when <see cref="Total"/> is zero.
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Gets the combined duration of all test executions in milliseconds.
    /// </summary>
    public long TotalDurationMs { get; init; }

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
