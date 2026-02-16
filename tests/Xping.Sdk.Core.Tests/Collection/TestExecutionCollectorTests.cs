/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Tests.Helpers;

namespace Xping.Sdk.Core.Tests.Collection;

public sealed class TestExecutionCollectorTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static ITestExecutionCollector BuildCollector(Action<XpingConfiguration>? configure = null)
        => ServiceHelper.BuildCollector(configure);

    private static TestExecution BuildExecution(string testName = "SomeTest")
        => new TestExecutionBuilder()
            .WithTestName(testName)
            .WithOutcome(TestOutcome.Passed)
            .Build();

    // ---------------------------------------------------------------------------
    // RecordTest
    // ---------------------------------------------------------------------------

    [Fact]
    public void RecordTest_ShouldEnqueueExecution_WhenEnabledAndSampled()
    {
        // Arrange
        using var collector = BuildCollector();
        var execution = BuildExecution();

        // Act
        collector.RecordTest(execution);
        var drained = collector.Drain();

        // Assert
        Assert.Single(drained);
        Assert.Equal(execution.ExecutionId, drained[0].ExecutionId);
    }

    [Fact]
    public void RecordTest_ShouldNotEnqueue_WhenSdkDisabled()
    {
        // Arrange
        using var collector = BuildCollector(o => o.Enabled = false);
        var execution = BuildExecution();

        // Act
        collector.RecordTest(execution);
        var drained = collector.Drain();

        // Assert
        Assert.Empty(drained);
    }

    [Fact]
    public void RecordTest_ShouldThrowArgumentNullException_ForNullExecution()
    {
        // Arrange
        using var collector = BuildCollector();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => collector.RecordTest(null!));
    }

    [Fact]
    public void RecordTest_ShouldThrowObjectDisposedException_AfterDispose()
    {
        // Arrange
        var collector = BuildCollector();
        collector.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => collector.RecordTest(BuildExecution()));
    }

    [Fact]
    public void RecordTest_ShouldDropAll_WhenSamplingRateIsZero()
    {
        // Arrange
        using var collector = BuildCollector(o => o.SamplingRate = 0.0);

        // Act
        for (int i = 0; i < 10; i++)
            collector.RecordTest(BuildExecution($"Test{i}"));

        // Assert
        Assert.Empty(collector.Drain());
    }

    [Fact]
    public void RecordTest_ShouldIncludeAll_WhenSamplingRateIsOne()
    {
        // Arrange
        using var collector = BuildCollector(o => o.SamplingRate = 1.0);

        // Act
        for (int i = 0; i < 5; i++)
            collector.RecordTest(BuildExecution($"Test{i}"));

        // Assert
        Assert.Equal(5, collector.Drain().Count);
    }

    [Fact]
    public void RecordTest_ShouldFireBufferFull_WhenBatchSizeReached()
    {
        // Arrange
        using var collector = BuildCollector(o => o.BatchSize = 3);
        bool fired = false;
        collector.BufferFull += (_, _) => fired = true;

        // Act — add exactly BatchSize items
        for (int i = 0; i < 3; i++)
            collector.RecordTest(BuildExecution($"Test{i}"));

        // Assert
        Assert.True(fired);
    }

    [Fact]
    public void RecordTest_ShouldNotFireBufferFull_WhenBelowBatchSize()
    {
        // Arrange
        using var collector = BuildCollector(o => o.BatchSize = 10);
        bool fired = false;
        collector.BufferFull += (_, _) => fired = true;

        // Act — add fewer than BatchSize items
        for (int i = 0; i < 5; i++)
            collector.RecordTest(BuildExecution($"Test{i}"));

        // Assert
        Assert.False(fired);
    }

    // ---------------------------------------------------------------------------
    // Drain
    // ---------------------------------------------------------------------------

    [Fact]
    public void Drain_ShouldReturnEmpty_WhenBufferIsEmpty()
    {
        // Arrange
        using var collector = BuildCollector();

        // Act
        var drained = collector.Drain();

        // Assert
        Assert.Empty(drained);
    }

    [Fact]
    public void Drain_ShouldReturnEmpty_AfterDispose()
    {
        // Arrange
        var collector = BuildCollector();
        collector.RecordTest(BuildExecution());
        collector.Dispose();

        // Act
        var drained = collector.Drain();

        // Assert
        Assert.Empty(drained);
    }

    [Fact]
    public void Drain_ShouldReturnAtMostBatchSize_Items()
    {
        // Arrange
        using var collector = BuildCollector(o => o.BatchSize = 3);

        for (int i = 0; i < 10; i++)
            collector.RecordTest(BuildExecution($"Test{i}"));

        // Act
        var drained = collector.Drain();

        // Assert
        Assert.Equal(3, drained.Count);
    }

    [Fact]
    public void Drain_ShouldRemoveItemsFromBuffer()
    {
        // Arrange
        using var collector = BuildCollector();
        collector.RecordTest(BuildExecution("First"));
        collector.RecordTest(BuildExecution("Second"));

        // Act
        collector.Drain();
        var secondDrain = collector.Drain();

        // Assert
        Assert.Empty(secondDrain);
    }

    // ---------------------------------------------------------------------------
    // GetStatsAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetStatsAsync_ShouldReflectRecordedAndSampledCounts()
    {
        // Arrange
        using var collector = BuildCollector(o => { o.SamplingRate = 1.0; o.BatchSize = 100; });

        for (int i = 0; i < 4; i++)
            collector.RecordTest(BuildExecution($"Test{i}"));

        // Act
        var stats = await collector.GetStatsAsync();

        // Assert
        Assert.Equal(4, stats.TotalRecorded);
        Assert.Equal(4, stats.TotalSampled);
        Assert.Equal(4, stats.BufferCount);
    }

    [Fact]
    public async Task GetStatsAsync_ShouldReflectZeroSampled_WhenSamplingRateIsZero()
    {
        // Arrange
        using var collector = BuildCollector(o => o.SamplingRate = 0.0);

        for (int i = 0; i < 5; i++)
            collector.RecordTest(BuildExecution($"Test{i}"));

        // Act
        var stats = await collector.GetStatsAsync();

        // Assert
        Assert.Equal(5, stats.TotalRecorded);
        Assert.Equal(0, stats.TotalSampled);
        Assert.Equal(0, stats.BufferCount);
    }

    [Fact]
    public async Task GetStatsAsync_ShouldReflectDecreasedBufferCount_AfterDrain()
    {
        // Arrange
        using var collector = BuildCollector(o => o.BatchSize = 100);

        for (int i = 0; i < 3; i++)
            collector.RecordTest(BuildExecution($"Test{i}"));

        collector.Drain();

        // Act
        var stats = await collector.GetStatsAsync();

        // Assert
        Assert.Equal(3, stats.TotalRecorded);
        Assert.Equal(0, stats.BufferCount);
    }
}
