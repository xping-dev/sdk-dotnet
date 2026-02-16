/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Tests.Builders;

public sealed class TestOrchestrationBuilderTests
{
    // ---------------------------------------------------------------------------
    // Build — defaults
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_ShouldReturnRecord_WithDefaults()
    {
        var record = new TestOrchestrationBuilder().Build();

        Assert.Equal(0, record.PositionInSuite);
        Assert.Null(record.GlobalPosition);
        Assert.Null(record.PreviousTestId);
        Assert.Null(record.PreviousTestName);
        Assert.Null(record.PreviousTestOutcome);
        Assert.False(record.WasParallelized);
        Assert.Equal(1, record.ConcurrentTestCount);
        Assert.Equal(string.Empty, record.ThreadId);
        Assert.Equal(string.Empty, record.WorkerId);
        Assert.Equal(TimeSpan.Zero, record.SuiteElapsedTime);
        Assert.Null(record.CollectionName);
    }

    // ---------------------------------------------------------------------------
    // With* methods — ordering
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithPositionInSuite_ShouldSetPosition()
    {
        var record = new TestOrchestrationBuilder().WithPositionInSuite(7).Build();
        Assert.Equal(7, record.PositionInSuite);
    }

    [Fact]
    public void WithGlobalPosition_ShouldSetGlobalPosition()
    {
        var record = new TestOrchestrationBuilder().WithGlobalPosition(42).Build();
        Assert.Equal(42, record.GlobalPosition);
    }

    [Fact]
    public void WithGlobalPosition_Null_ShouldSetNull()
    {
        var record = new TestOrchestrationBuilder().WithGlobalPosition(5).WithGlobalPosition(null).Build();
        Assert.Null(record.GlobalPosition);
    }

    // ---------------------------------------------------------------------------
    // With* methods — previous test
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithPreviousTest_ShouldSetAllThreeFields()
    {
        var record = new TestOrchestrationBuilder()
            .WithPreviousTest("prev-id", "Previous Test", TestOutcome.Passed)
            .Build();

        Assert.Equal("prev-id", record.PreviousTestId);
        Assert.Equal("Previous Test", record.PreviousTestName);
        Assert.Equal(TestOutcome.Passed, record.PreviousTestOutcome);
    }

    [Fact]
    public void WithPreviousTestId_ShouldSetId()
    {
        var record = new TestOrchestrationBuilder().WithPreviousTestId("id-123").Build();
        Assert.Equal("id-123", record.PreviousTestId);
    }

    [Fact]
    public void WithPreviousTestName_ShouldSetName()
    {
        var record = new TestOrchestrationBuilder().WithPreviousTestName("SomePreviousTest").Build();
        Assert.Equal("SomePreviousTest", record.PreviousTestName);
    }

    [Fact]
    public void WithPreviousTestOutcome_ShouldSetOutcome()
    {
        var record = new TestOrchestrationBuilder()
            .WithPreviousTestOutcome(TestOutcome.Failed)
            .Build();

        Assert.Equal(TestOutcome.Failed, record.PreviousTestOutcome);
    }

    // ---------------------------------------------------------------------------
    // With* methods — parallelization
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithParallelization_ShouldSetBothFields()
    {
        var record = new TestOrchestrationBuilder().WithParallelization(true, 4).Build();

        Assert.True(record.WasParallelized);
        Assert.Equal(4, record.ConcurrentTestCount);
    }

    [Fact]
    public void WithWasParallelized_True_ShouldSetFlag()
    {
        var record = new TestOrchestrationBuilder().WithWasParallelized(true).Build();
        Assert.True(record.WasParallelized);
    }

    [Fact]
    public void WithConcurrentTestCount_ShouldSetCount()
    {
        var record = new TestOrchestrationBuilder().WithConcurrentTestCount(8).Build();
        Assert.Equal(8, record.ConcurrentTestCount);
    }

    // ---------------------------------------------------------------------------
    // With* methods — threading
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithThreadId_ShouldSetThreadId()
    {
        var record = new TestOrchestrationBuilder().WithThreadId("thread-7").Build();
        Assert.Equal("thread-7", record.ThreadId);
    }

    [Fact]
    public void WithWorkerId_ShouldSetWorkerId()
    {
        var record = new TestOrchestrationBuilder().WithWorkerId("worker-2").Build();
        Assert.Equal("worker-2", record.WorkerId);
    }

    // ---------------------------------------------------------------------------
    // With* methods — suite context
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithSuiteElapsedTime_ShouldSetElapsedTime()
    {
        var elapsed = TimeSpan.FromSeconds(15);
        var record = new TestOrchestrationBuilder().WithSuiteElapsedTime(elapsed).Build();
        Assert.Equal(elapsed, record.SuiteElapsedTime);
    }

    [Fact]
    public void WithCollectionName_ShouldSetCollectionName()
    {
        var record = new TestOrchestrationBuilder().WithCollectionName("MyCollection").Build();
        Assert.Equal("MyCollection", record.CollectionName);
    }

    [Fact]
    public void WithCollectionName_Null_ShouldSetNull()
    {
        var record = new TestOrchestrationBuilder()
            .WithCollectionName("col")
            .WithCollectionName(null)
            .Build();

        Assert.Null(record.CollectionName);
    }

    // ---------------------------------------------------------------------------
    // Reset
    // ---------------------------------------------------------------------------

    [Fact]
    public void Reset_ShouldRestoreDefaults()
    {
        var builder = new TestOrchestrationBuilder()
            .WithPositionInSuite(5)
            .WithGlobalPosition(10)
            .WithPreviousTest("id", "name", TestOutcome.Failed)
            .WithParallelization(true, 4)
            .WithThreadId("t1")
            .WithWorkerId("w1")
            .WithSuiteElapsedTime(TimeSpan.FromMinutes(1))
            .WithCollectionName("col");

        builder.Reset();
        var record = builder.Build();

        Assert.Equal(0, record.PositionInSuite);
        Assert.Null(record.GlobalPosition);
        Assert.Null(record.PreviousTestId);
        Assert.Null(record.PreviousTestName);
        Assert.Null(record.PreviousTestOutcome);
        Assert.False(record.WasParallelized);
        Assert.Equal(1, record.ConcurrentTestCount);
        Assert.Equal(string.Empty, record.ThreadId);
        Assert.Equal(string.Empty, record.WorkerId);
        Assert.Equal(TimeSpan.Zero, record.SuiteElapsedTime);
    }

    [Fact]
    public void Reset_ShouldReturnBuilderInstance_ForChaining()
    {
        var builder = new TestOrchestrationBuilder();
        Assert.Same(builder, builder.Reset());
    }
}
