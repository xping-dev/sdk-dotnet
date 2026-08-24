/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Statistics;

namespace Xping.Sdk.Core.Tests.Models.Statistics;

public sealed class SessionSummaryTests
{
    // ---------------------------------------------------------------------------
    // Outcomes — the per-attempt reading, used when nothing was retried
    // ---------------------------------------------------------------------------

    [Fact]
    public void From_NoRetries_ReadsTheExecutionLevelCounters()
    {
        var summary = SessionSummary.From(new QuickStatistics
        {
            Total = 13,
            DistinctTests = 13,
            Passed = 9,
            Failed = 2,
            Timeout = 1,
            Skipped = 1,
            // Final counters disagree on purpose: without a retry they must not be read.
            FinalPassed = 99,
        });

        Assert.False(summary.Retried);
        Assert.Equal(0, summary.Retries);
        Assert.Equal("9 passed, 2 failed, 1 timed out, 1 skipped", summary.Outcomes);
    }

    [Fact]
    public void From_OnlyPasses_OmitsEveryZeroedOutcome()
    {
        var summary = SessionSummary.From(new QuickStatistics { Total = 4, DistinctTests = 4, Passed = 4 });

        Assert.Equal("4 passed", summary.Outcomes);
    }

    [Fact]
    public void From_NothingRecorded_StillReportsZeroPassed()
    {
        var summary = SessionSummary.From(new QuickStatistics());

        Assert.False(summary.Retried);
        Assert.Equal("0 passed", summary.Outcomes);
        Assert.Equal(0, summary.Total);
    }

    [Fact]
    public void From_InconclusiveAndNotExecuted_AppearsAfterTheCommonOutcomes()
    {
        var summary = SessionSummary.From(new QuickStatistics
        {
            Total = 4,
            DistinctTests = 4,
            Passed = 1,
            Inconclusive = 2,
            NotExecuted = 1,
        });

        Assert.Equal("1 passed, 2 inconclusive, 1 not executed", summary.Outcomes);
    }

    [Fact]
    public void From_NoDistinctTestsRecorded_IsNotTreatedAsRetried()
    {
        // DistinctTests stays 0 when nothing identified the executions; that is missing data,
        // not a suite where every attempt was a retry.
        var summary = SessionSummary.From(new QuickStatistics { Total = 3, DistinctTests = 0, Passed = 3 });

        Assert.False(summary.Retried);
        Assert.Equal("3 passed", summary.Outcomes);
    }

    // ---------------------------------------------------------------------------
    // Outcomes — the test-level reading, used once anything was retried
    // ---------------------------------------------------------------------------

    [Fact]
    public void From_Retried_ReadsTheTestLevelCountersInstead()
    {
        var summary = SessionSummary.From(new QuickStatistics
        {
            Total = 13,
            DistinctTests = 11,
            // Attempt-level: two attempts failed and were retried into passes.
            Passed = 11,
            Failed = 2,
            // Test-level: the suite ended green.
            FinalPassed = 11,
            FinalFailed = 0,
        });

        Assert.True(summary.Retried);
        Assert.Equal(2, summary.Retries);
        Assert.Equal(13, summary.Total);
        Assert.Equal(11, summary.DistinctTests);
        Assert.Equal("11 passed", summary.Outcomes);
    }

    [Fact]
    public void From_RetriedButStillRed_ReportsTheFinalFailure()
    {
        var summary = SessionSummary.From(new QuickStatistics
        {
            Total = 5,
            DistinctTests = 3,
            FinalPassed = 2,
            FinalFailed = 1,
        });

        Assert.Equal("2 passed, 1 failed", summary.Outcomes);
    }

    // ---------------------------------------------------------------------------
    // Pluralization
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(1, "test")]
    [InlineData(0, "tests")]
    [InlineData(2, "tests")]
    public void TestLabel_AgreesWithTheDistinctTestCount(int distinctTests, string expected)
    {
        var summary = SessionSummary.From(new QuickStatistics { Total = 9, DistinctTests = distinctTests });

        Assert.Equal(expected, summary.TestLabel);
    }

    [Theory]
    [InlineData(2, 1, "retry")]
    [InlineData(3, 1, "retries")]
    public void RetryLabel_AgreesWithTheRetryCount(int total, int distinctTests, string expected)
    {
        var summary = SessionSummary.From(new QuickStatistics { Total = total, DistinctTests = distinctTests });

        Assert.Equal(expected, summary.RetryLabel);
    }

    // ---------------------------------------------------------------------------
    // Durations
    // ---------------------------------------------------------------------------

    [Fact]
    public void From_Durations_AreRenderedAtHumanScaleWithTheOverheadGap()
    {
        var summary = SessionSummary.From(new QuickStatistics
        {
            Total = 1,
            DistinctTests = 1,
            Passed = 1,
            TotalDurationMs = 591,
            WallClockDurationMs = 10_600,
        });

        Assert.Equal("591ms", summary.ExecutionDuration);
        Assert.Equal("10.6s", summary.WallClockDuration);
        Assert.Equal(" (+10s overhead)", summary.Overhead);
    }

    [Fact]
    public void From_TestsOutrunTheWallClock_ReportsNoOverhead()
    {
        // Parallel execution: the summed test time exceeds the elapsed session time.
        var summary = SessionSummary.From(new QuickStatistics
        {
            Total = 8,
            DistinctTests = 8,
            Passed = 8,
            TotalDurationMs = 40_000,
            WallClockDurationMs = 12_000,
        });

        Assert.Equal("40s", summary.ExecutionDuration);
        Assert.Equal("12s", summary.WallClockDuration);
        Assert.Equal(string.Empty, summary.Overhead);
    }

    // ---------------------------------------------------------------------------
    // Guards
    // ---------------------------------------------------------------------------

    [Fact]
    public void From_NullStatistics_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SessionSummary.From(null!));
    }
}
