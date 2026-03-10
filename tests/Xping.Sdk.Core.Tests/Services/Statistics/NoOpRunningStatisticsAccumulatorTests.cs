/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Statistics.Internals;

namespace Xping.Sdk.Core.Tests.Services.Statistics;

public sealed class NoOpRunningStatisticsAccumulatorTests
{
    // ---------------------------------------------------------------------------
    // Record — no-op
    // ---------------------------------------------------------------------------

    [Fact]
    public void Record_WithValidExecution_DoesNotThrow()
    {
        // Arrange
        var accumulator = new NoOpRunningStatisticsAccumulator();
        var execution = new TestExecutionBuilder()
            .WithTestName("Test")
            .WithOutcome(TestOutcome.Passed)
            .Build();

        // Act & Assert
        var ex = Record.Exception(() => accumulator.Record(execution));
        Assert.Null(ex);
    }

    [Fact]
    public void Record_WithNullExecution_DoesNotThrow()
    {
        // Arrange
        var accumulator = new NoOpRunningStatisticsAccumulator();

        // Act & Assert — no-op implementation must silently absorb null
        var ex = Record.Exception(() => accumulator.Record(null!));
        Assert.Null(ex);
    }

    [Fact]
    public void Record_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var accumulator = new NoOpRunningStatisticsAccumulator();

        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            var execution = new TestExecutionBuilder()
                .WithTestName($"Test{i}")
                .WithOutcome(TestOutcome.Passed)
                .Build();
            var ex = Record.Exception(() => accumulator.Record(execution));
            Assert.Null(ex);
        }
    }

    // ---------------------------------------------------------------------------
    // GetSnapshot — returns zeroed statistics
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetSnapshot_WithNoRecordedExecutions_ReturnsZeroedStatistics()
    {
        // Arrange
        var accumulator = new NoOpRunningStatisticsAccumulator();

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.NotNull(snapshot);
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

    [Fact]
    public void GetSnapshot_AfterRecordingExecutions_StillReturnsZeroedStatistics()
    {
        // Arrange — no-op accumulator discards all recordings
        var accumulator = new NoOpRunningStatisticsAccumulator();
        accumulator.Record(new TestExecutionBuilder()
            .WithTestName("Test1")
            .WithOutcome(TestOutcome.Passed)
            .Build());
        accumulator.Record(new TestExecutionBuilder()
            .WithTestName("Test2")
            .WithOutcome(TestOutcome.Failed)
            .Build());

        // Act
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(0, snapshot.Total);
    }

    [Fact]
    public void GetSnapshot_CalledMultipleTimes_AlwaysReturnsZeroedStatistics()
    {
        // Arrange
        var accumulator = new NoOpRunningStatisticsAccumulator();

        // Act & Assert
        for (int i = 0; i < 3; i++)
        {
            var snapshot = accumulator.GetSnapshot();
            Assert.Equal(0, snapshot.Total);
        }
    }

    // ---------------------------------------------------------------------------
    // Reset — no-op
    // ---------------------------------------------------------------------------

    [Fact]
    public void Reset_DoesNotThrow()
    {
        // Arrange
        var accumulator = new NoOpRunningStatisticsAccumulator();

        // Act & Assert
        var ex = Record.Exception(() => accumulator.Reset());
        Assert.Null(ex);
    }

    [Fact]
    public void Reset_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var accumulator = new NoOpRunningStatisticsAccumulator();

        // Act & Assert
        for (int i = 0; i < 3; i++)
        {
            var ex = Record.Exception(() => accumulator.Reset());
            Assert.Null(ex);
        }
    }

    [Fact]
    public void Reset_AfterRecordingExecutions_GetSnapshotStillReturnsZero()
    {
        // Arrange
        var accumulator = new NoOpRunningStatisticsAccumulator();
        accumulator.Record(new TestExecutionBuilder()
            .WithTestName("Test")
            .WithOutcome(TestOutcome.Passed)
            .Build());

        // Act
        accumulator.Reset();
        var snapshot = accumulator.GetSnapshot();

        // Assert
        Assert.Equal(0, snapshot.Total);
    }
}
