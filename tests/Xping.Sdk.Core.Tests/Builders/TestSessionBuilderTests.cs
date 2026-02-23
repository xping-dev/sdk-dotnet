/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Tests.Builders;

public sealed class TestSessionBuilderTests
{
    private static TestExecution BuildExecution(string name = "Test")
        => new TestExecutionBuilder().WithTestName(name).WithOutcome(TestOutcome.Passed).Build();

    // ---------------------------------------------------------------------------
    // Build — defaults
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_ShouldReturnSession_WithNonEmptySessionId()
    {
        var session = new TestSessionBuilder().Build();
        Assert.NotEqual(Guid.Empty, session.SessionId);
    }

    [Fact]
    public void Build_ShouldReturnSession_WithStartedAtApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var session = new TestSessionBuilder().Build();
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(session.StartedAt, before, after);
    }

    [Fact]
    public void Build_ShouldReturnSession_WithNullEndedAtByDefault()
    {
        var session = new TestSessionBuilder().Build();
        Assert.Null(session.EndedAt);
    }

    [Fact]
    public void Build_ShouldReturnSession_WithEmptyExecutionsByDefault()
    {
        var session = new TestSessionBuilder().Build();
        Assert.Empty(session.Executions);
    }

    [Fact]
    public void Build_ShouldReturnSession_WithNullTotalTestsExpectedByDefault()
    {
        var session = new TestSessionBuilder().Build();
        Assert.Null(session.TotalTestsExpected);
    }

    // ---------------------------------------------------------------------------
    // WithSessionId
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithSessionId_ShouldSetSessionId()
    {
        var id = Guid.NewGuid();
        var session = new TestSessionBuilder().WithSessionId(id).Build();
        Assert.Equal(id, session.SessionId);
    }

    [Fact]
    public void WithSessionId_EmptyGuid_ShouldThrowArgumentException()
    {
        var builder = new TestSessionBuilder();
        Assert.Throws<ArgumentException>(() => builder.WithSessionId(Guid.Empty));
    }

    // ---------------------------------------------------------------------------
    // WithStartedAt / WithEndedAt
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithStartedAt_ShouldSetStartedAt()
    {
        var time = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var session = new TestSessionBuilder().WithStartedAt(time).Build();
        Assert.Equal(time, session.StartedAt);
    }

    [Fact]
    public void WithEndedAt_ShouldSetEndedAt()
    {
        var endTime = new DateTime(2024, 6, 1, 13, 0, 0, DateTimeKind.Utc);
        var session = new TestSessionBuilder().WithEndedAt(endTime).Build();
        Assert.Equal(endTime, session.EndedAt);
    }

    [Fact]
    public void WithEndedAt_Null_ShouldSetNull()
    {
        var session = new TestSessionBuilder()
            .WithEndedAt(DateTime.UtcNow)
            .WithEndedAt(null)
            .Build();

        Assert.Null(session.EndedAt);
    }

    // ---------------------------------------------------------------------------
    // WithEnvironmentInfo
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithEnvironmentInfo_ShouldSetEnvironmentInfo()
    {
        var env = new EnvironmentInfoBuilder().WithMachineName("ci-host").Build();
        var session = new TestSessionBuilder().WithEnvironmentInfo(env).Build();
        Assert.Equal("ci-host", session.EnvironmentInfo.MachineName);
    }

    [Fact]
    public void WithEnvironmentInfo_Null_ShouldThrowArgumentNullException()
    {
        var builder = new TestSessionBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.WithEnvironmentInfo(null!));
    }

    // ---------------------------------------------------------------------------
    // AddExecution / AddExecutions
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddExecution_ShouldAddExecution()
    {
        var session = new TestSessionBuilder().AddExecution(BuildExecution("T1")).Build();
        Assert.Single(session.Executions);
    }

    [Fact]
    public void AddExecution_Null_ShouldBeIgnored()
    {
        var session = new TestSessionBuilder().AddExecution(null!).Build();
        Assert.Empty(session.Executions);
    }

    [Fact]
    public void AddExecutions_ShouldAddAllNonNullExecutions()
    {
        var executions = new[] { BuildExecution("A"), BuildExecution("B"), BuildExecution("C") };
        var session = new TestSessionBuilder().AddExecutions(executions).Build();
        Assert.Equal(3, session.Executions.Count);
    }

    [Fact]
    public void AddExecutions_NullCollection_ShouldBeIgnored()
    {
        var session = new TestSessionBuilder().AddExecutions(null!).Build();
        Assert.Empty(session.Executions);
    }

    [Fact]
    public void AddExecutions_FiltersNullItems()
    {
        var executions = new TestExecution?[] { BuildExecution("A"), null, BuildExecution("B") };
        var session = new TestSessionBuilder().AddExecutions(executions!).Build();
        Assert.Equal(2, session.Executions.Count);
    }

    // ---------------------------------------------------------------------------
    // WithTotalTestsExpected
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithTotalTestsExpected_ShouldSetCount()
    {
        var session = new TestSessionBuilder().WithTotalTestsExpected(50).Build();
        Assert.Equal(50, session.TotalTestsExpected);
    }

    [Fact]
    public void WithTotalTestsExpected_Null_ShouldSetNull()
    {
        var session = new TestSessionBuilder().WithTotalTestsExpected(10).WithTotalTestsExpected(null).Build();
        Assert.Null(session.TotalTestsExpected);
    }

    // ---------------------------------------------------------------------------
    // Reset
    // ---------------------------------------------------------------------------

    [Fact]
    public void Reset_ShouldGenerateNewSessionId()
    {
        var builder = new TestSessionBuilder();
        var firstId = builder.Build().SessionId;
        builder.Reset();
        var secondId = builder.Build().SessionId;

        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public void Reset_ShouldClearExecutions()
    {
        var builder = new TestSessionBuilder().AddExecution(BuildExecution());
        builder.Reset();
        var session = builder.Build();
        Assert.Empty(session.Executions);
    }

    [Fact]
    public void Reset_ShouldClearEndedAt()
    {
        var builder = new TestSessionBuilder().WithEndedAt(DateTime.UtcNow);
        builder.Reset();
        Assert.Null(builder.Build().EndedAt);
    }

    [Fact]
    public void Reset_ShouldClearTotalTestsExpected()
    {
        var builder = new TestSessionBuilder().WithTotalTestsExpected(100);
        builder.Reset();
        Assert.Null(builder.Build().TotalTestsExpected);
    }

    [Fact]
    public void Reset_ShouldReturnBuilderInstance_ForChaining()
    {
        var builder = new TestSessionBuilder();
        Assert.Same(builder, builder.Reset());
    }

    // ---------------------------------------------------------------------------
    // Shared-reference behaviour (AsReadOnly)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_ExecutionsList_IsSharedReference_NotACopy()
    {
        // Demonstrates the known AsReadOnly() behaviour: after Reset() the
        // previously-built session's Executions collection becomes empty because
        // it is a live view of the builder's backing list.
        var builder = new TestSessionBuilder();
        builder.AddExecution(BuildExecution("T1"));

        var session = builder.Build();
        Assert.Single(session.Executions); // still populated before reset

        builder.Reset(); // clears the backing list
        Assert.Empty(session.Executions); // live view now shows empty
    }
}
