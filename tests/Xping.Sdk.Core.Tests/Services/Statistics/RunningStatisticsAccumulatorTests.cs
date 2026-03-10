/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Statistics.Internals;

namespace Xping.Sdk.Core.Tests.Services.Statistics;

public sealed class RunningStatisticsAccumulatorTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static TestExecution BuildExecution(
        string name,
        TestOutcome outcome,
        TimeSpan duration = default)
    {
        return new TestExecutionBuilder()
            .WithTestName(name)
            .WithOutcome(outcome)
            .WithDuration(duration)
            .Build();
    }

    // ---------------------------------------------------------------------------
    // Record — guard clauses
    // ---------------------------------------------------------------------------

    [Fact]
    public void Record_NullExecution_ThrowsArgumentNullException()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => accumulator.Record(null!));
    }

    // ---------------------------------------------------------------------------
    // GetSnapshot — empty state
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSnapshot_WithNoRecordedExecutions_ReturnsAllZeros()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(0, snapshot.Total);
        Assert.Equal(0, snapshot.Passed);
        Assert.Equal(0, snapshot.Failed);
        Assert.Equal(0, snapshot.Skipped);
        Assert.Equal(0, snapshot.Inconclusive);
        Assert.Equal(0, snapshot.NotExecuted);
        Assert.Equal(0.0, snapshot.SuccessRate);
        Assert.Equal(0L, snapshot.TotalDurationMs);
        Assert.Equal(0L, snapshot.AverageDurationMs);
        Assert.Equal(0L, snapshot.SlowestTestDurationMs);
        Assert.Null(snapshot.SlowestTestName);
    }

    // ---------------------------------------------------------------------------
    // Record — outcome counter increments
    // ---------------------------------------------------------------------------

    [Fact]
    public void Record_PassedExecution_IncrementsPassedAndTotalCounters()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.Total);
        Assert.Equal(1, snapshot.Passed);
    }

    [Fact]
    public void Record_FailedExecution_IncrementsFailedAndTotalCounters()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildExecution("T1", TestOutcome.Failed));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.Total);
        Assert.Equal(1, snapshot.Failed);
    }

    [Fact]
    public void Record_SkippedExecution_IncrementsSkippedAndTotalCounters()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildExecution("T1", TestOutcome.Skipped));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.Total);
        Assert.Equal(1, snapshot.Skipped);
    }

    [Fact]
    public void Record_InconclusiveExecution_IncrementsInconclusiveAndTotalCounters()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildExecution("T1", TestOutcome.Inconclusive));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.Total);
        Assert.Equal(1, snapshot.Inconclusive);
    }

    [Fact]
    public void Record_NotExecutedExecution_IncrementsNotExecutedAndTotalCounters()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildExecution("T1", TestOutcome.NotExecuted));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(1, snapshot.Total);
        Assert.Equal(1, snapshot.NotExecuted);
    }

    [Fact]
    public void Record_MultipleOutcomes_AllCountersReflectActualValues()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed));
        accumulator.Record(BuildExecution("T2", TestOutcome.Passed));
        accumulator.Record(BuildExecution("T3", TestOutcome.Failed));
        accumulator.Record(BuildExecution("T4", TestOutcome.Skipped));
        accumulator.Record(BuildExecution("T5", TestOutcome.Inconclusive));
        accumulator.Record(BuildExecution("T6", TestOutcome.NotExecuted));

        // Assert
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(6, snapshot.Total);
        Assert.Equal(2, snapshot.Passed);
        Assert.Equal(1, snapshot.Failed);
        Assert.Equal(1, snapshot.Skipped);
        Assert.Equal(1, snapshot.Inconclusive);
        Assert.Equal(1, snapshot.NotExecuted);
    }

    // ---------------------------------------------------------------------------
    // GetSnapshot — SuccessRate calculation
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSnapshot_AllTestsPassed_SuccessRateIs100()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed));
        accumulator.Record(BuildExecution("T2", TestOutcome.Passed));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(100.0, snapshot.SuccessRate, precision: 5);
    }

    [Fact]
    public void GetSnapshot_NoTestsPassed_SuccessRateIsZero()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Failed));
        accumulator.Record(BuildExecution("T2", TestOutcome.Failed));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(0.0, snapshot.SuccessRate, precision: 5);
    }

    [Fact]
    public void GetSnapshot_HalfTestsPassed_SuccessRateIs50()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed));
        accumulator.Record(BuildExecution("T2", TestOutcome.Failed));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(50.0, snapshot.SuccessRate, precision: 5);
    }

    [Fact]
    public void GetSnapshot_NoExecutions_SuccessRateIsZero()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert — no division by zero
        Assert.Equal(0.0, snapshot.SuccessRate);
    }

    // ---------------------------------------------------------------------------
    // GetSnapshot — duration calculations
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSnapshot_SingleExecution_TotalDurationMatchesExecutionDuration()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        var duration = TimeSpan.FromMilliseconds(500);
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed, duration));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(500L, snapshot.TotalDurationMs);
    }

    [Fact]
    public void GetSnapshot_MultipleExecutions_TotalDurationIsSumOfAllDurations()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed, TimeSpan.FromMilliseconds(100)));
        accumulator.Record(BuildExecution("T2", TestOutcome.Passed, TimeSpan.FromMilliseconds(200)));
        accumulator.Record(BuildExecution("T3", TestOutcome.Passed, TimeSpan.FromMilliseconds(300)));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(600L, snapshot.TotalDurationMs);
    }

    [Fact]
    public void GetSnapshot_MultipleExecutions_AverageDurationIsCorrect()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed, TimeSpan.FromMilliseconds(100)));
        accumulator.Record(BuildExecution("T2", TestOutcome.Passed, TimeSpan.FromMilliseconds(300)));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(200L, snapshot.AverageDurationMs);
    }

    [Fact]
    public void GetSnapshot_NoExecutions_AverageDurationIsZero()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert — no division by zero
        Assert.Equal(0L, snapshot.AverageDurationMs);
    }

    // ---------------------------------------------------------------------------
    // GetSnapshot — slowest test tracking
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSnapshot_SingleExecution_SlowestTestIsTheOnlyExecution()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("SlowTest", TestOutcome.Passed, TimeSpan.FromMilliseconds(750)));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal("SlowTest", snapshot.SlowestTestName);
        Assert.Equal(750L, snapshot.SlowestTestDurationMs);
    }

    [Fact]
    public void GetSnapshot_MultipleExecutions_SlowestTestHasLongestDuration()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("Fast", TestOutcome.Passed, TimeSpan.FromMilliseconds(100)));
        accumulator.Record(BuildExecution("Slowest", TestOutcome.Passed, TimeSpan.FromMilliseconds(800)));
        accumulator.Record(BuildExecution("Medium", TestOutcome.Passed, TimeSpan.FromMilliseconds(400)));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal("Slowest", snapshot.SlowestTestName);
        Assert.Equal(800L, snapshot.SlowestTestDurationMs);
    }

    [Fact]
    public void GetSnapshot_NoExecutions_SlowestTestNameIsNull()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Null(snapshot.SlowestTestName);
        Assert.Equal(0L, snapshot.SlowestTestDurationMs);
    }

    [Fact]
    public void GetSnapshot_AllExecutionsHaveZeroDuration_SlowestDurationIsZero()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed, TimeSpan.Zero));
        accumulator.Record(BuildExecution("T2", TestOutcome.Passed, TimeSpan.Zero));

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(0L, snapshot.SlowestTestDurationMs);
    }

    // ---------------------------------------------------------------------------
    // Reset — resets all state
    // ---------------------------------------------------------------------------

    [Fact]
    public void Reset_AfterRecordingExecutions_GetSnapshotReturnsAllZeros()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Passed, TimeSpan.FromMilliseconds(500)));
        accumulator.Record(BuildExecution("T2", TestOutcome.Failed, TimeSpan.FromMilliseconds(200)));

        // Act
        accumulator.Reset();
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(0, snapshot.Total);
        Assert.Equal(0, snapshot.Passed);
        Assert.Equal(0, snapshot.Failed);
        Assert.Equal(0.0, snapshot.SuccessRate);
        Assert.Equal(0L, snapshot.TotalDurationMs);
        Assert.Equal(0L, snapshot.AverageDurationMs);
        Assert.Null(snapshot.SlowestTestName);
        Assert.Equal(0L, snapshot.SlowestTestDurationMs);
    }

    [Fact]
    public void Reset_CanRecordNewExecutionsAfterReset()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        accumulator.Record(BuildExecution("T1", TestOutcome.Failed));
        accumulator.Reset();

        // Act
        accumulator.Record(BuildExecution("T2", TestOutcome.Passed, TimeSpan.FromMilliseconds(100)));
        var snapshot = accumulator.GetSnapshot();

        // Assert — only the post-reset execution should be reflected
        Assert.Equal(1, snapshot.Total);
        Assert.Equal(1, snapshot.Passed);
        Assert.Equal(0, snapshot.Failed);
    }

    [Fact]
    public void Reset_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();

        // Act & Assert
        for (int i = 0; i < 3; i++)
        {
            var ex = Record.Exception(() => accumulator.Reset());
            Assert.Null(ex);
        }
    }

    // ---------------------------------------------------------------------------
    // Thread safety — concurrent Record calls produce consistent totals
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Record_ConcurrentCalls_TotalCountIsConsistent()
    {
        // Arrange
        var accumulator = new RunningStatisticsAccumulator();
        const int parallelism = 8;
        const int recordsPerTask = 100;

        // Act — fire-and-forget tasks that all record executions concurrently
        var tasks = Enumerable.Range(0, parallelism)
            .Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < recordsPerTask; i++)
                    accumulator.Record(BuildExecution("ConcurrentTest", TestOutcome.Passed));
            }));

        await Task.WhenAll(tasks);

        // Assert — every Record must be counted exactly once
        var snapshot = accumulator.GetSnapshot();
        Assert.Equal(parallelism * recordsPerTask, snapshot.Total);
        Assert.Equal(parallelism * recordsPerTask, snapshot.Passed);
    }
}
