/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector.Internals;

namespace Xping.Sdk.Core.Tests.Services.Collector;

public sealed class NoOpTestExecutionCollectorTests
{
    // ---------------------------------------------------------------------------
    // RecordTest
    // ---------------------------------------------------------------------------

    [Fact]
    public void RecordTest_WithValidExecution_DoesNotThrow()
    {
        // Arrange
        using var collector = new NoOpTestExecutionCollector();
        var execution = new TestExecutionBuilder()
            .WithTestName("Test")
            .WithOutcome(TestOutcome.Passed)
            .Build();

        // Act & Assert
        var ex = Record.Exception(() => collector.RecordTest(execution));
        Assert.Null(ex);
    }

    // ---------------------------------------------------------------------------
    // Drain
    // ---------------------------------------------------------------------------

    [Fact]
    public void Drain_AfterRecordingExecutions_ReturnsEmptyList()
    {
        // Arrange
        using var collector = new NoOpTestExecutionCollector();
        collector.RecordTest(new TestExecutionBuilder().WithTestName("T1").WithOutcome(TestOutcome.Passed).Build());
        collector.RecordTest(new TestExecutionBuilder().WithTestName("T2").WithOutcome(TestOutcome.Failed).Build());

        // Act
        var result = collector.Drain();

        // Assert
        Assert.Empty(result);
    }

    // ---------------------------------------------------------------------------
    // GetStatsAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetStatsAsync_ReturnsZeroStats()
    {
        // Arrange
        using var collector = new NoOpTestExecutionCollector();
        collector.RecordTest(new TestExecutionBuilder().WithTestName("T1").WithOutcome(TestOutcome.Passed).Build());

        // Act
        var stats = await collector.GetStatsAsync();

        // Assert
        Assert.Equal(0, stats.TotalRecorded);
        Assert.Equal(0, stats.BufferCount);
    }

    // ---------------------------------------------------------------------------
    // Dispose
    // ---------------------------------------------------------------------------

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var collector = new NoOpTestExecutionCollector();
        collector.Dispose();

        // Act & Assert — second call must not throw
        var ex = Record.Exception(collector.Dispose);
        Assert.Null(ex);
    }

    // ---------------------------------------------------------------------------
    // BufferFull event
    // ---------------------------------------------------------------------------

    [Fact]
    public void BufferFull_AddAndRemoveHandler_DoesNotThrow()
    {
        // Arrange
        using var collector = new NoOpTestExecutionCollector();
        EventHandler handler = (_, _) => { };

        // Act & Assert — subscription and un-subscription must be no-ops safe
        var ex = Record.Exception(() =>
        {
            collector.BufferFull += handler;
            collector.BufferFull -= handler;
        });
        Assert.Null(ex);
    }
}
