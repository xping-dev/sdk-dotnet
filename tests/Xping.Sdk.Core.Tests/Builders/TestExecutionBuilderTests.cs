/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Tests.Builders;

public sealed class TestExecutionBuilderTests
{
    // ---------------------------------------------------------------------------
    // Build — defaults
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithExecutionId_ShouldSetExecutionId()
    {
        var id = Guid.NewGuid();
        var execution = new TestExecutionBuilder().WithExecutionId(id).Build();
        Assert.Equal(id, execution.ExecutionId);
    }

    [Fact]
    public void WithStartTime_ShouldSetStartTimeUtc()
    {
        var time = new DateTime(2024, 3, 15, 9, 0, 0, DateTimeKind.Utc);
        var execution = new TestExecutionBuilder().WithStartTime(time).Build();
        Assert.Equal(time, execution.StartTimeUtc);
    }

    [Fact]
    public void WithEndTime_ShouldSetEndTimeUtc()
    {
        var time = new DateTime(2024, 3, 15, 9, 5, 0, DateTimeKind.Utc);
        var execution = new TestExecutionBuilder().WithEndTime(time).Build();
        Assert.Equal(time, execution.EndTimeUtc);
    }

    [Fact]
    public void WithTestOrchestrationRecord_ShouldSetOrchestration()
    {
        var record = new TestOrchestrationBuilder().WithPositionInSuite(3).Build();
        var execution = new TestExecutionBuilder().WithTestOrchestrationRecord(record).Build();
        Assert.Equal(3, execution.TestOrchestrationRecord.PositionInSuite);
    }

    [Fact]
    public void Build_ShouldReturnExecution_WithDefaultOutcome_NotExecuted()
    {
        // Arrange & Act
        var execution = new TestExecutionBuilder().Build();

        // Assert
        Assert.Equal(TestOutcome.NotExecuted, execution.Outcome);
    }

    [Fact]
    public void Build_ShouldReturnExecution_WithNewExecutionId_EachCall()
    {
        // Arrange
        var builder = new TestExecutionBuilder();

        // Act
        var e1 = builder.Build();
        var e2 = builder.Build();

        // Assert — same builder, same state → same ID until reset
        Assert.Equal(e1.ExecutionId, e2.ExecutionId);
    }

    [Fact]
    public void Build_AfterReset_ShouldGenerateNewExecutionId()
    {
        // Arrange
        var builder = new TestExecutionBuilder();
        var first = builder.Build().ExecutionId;

        // Act
        builder.Reset();
        var second = builder.Build().ExecutionId;

        // Assert
        Assert.NotEqual(first, second);
    }

    // ---------------------------------------------------------------------------
    // With* methods
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithTestName_ShouldSetTestName()
    {
        // Arrange & Act
        var execution = new TestExecutionBuilder()
            .WithTestName("My_Special_Test")
            .Build();

        // Assert
        Assert.Equal("My_Special_Test", execution.TestName);
    }

    [Fact]
    public void WithTestName_ShouldThrowArgumentNullException_ForNull()
    {
        // Arrange
        var builder = new TestExecutionBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithTestName(null!));
    }

    [Fact]
    public void WithOutcome_ShouldSetOutcome()
    {
        // Arrange & Act
        var execution = new TestExecutionBuilder()
            .WithOutcome(TestOutcome.Failed)
            .Build();

        // Assert
        Assert.Equal(TestOutcome.Failed, execution.Outcome);
    }

    [Fact]
    public void WithDuration_ShouldSetDuration()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(123);

        // Act
        var execution = new TestExecutionBuilder()
            .WithDuration(duration)
            .Build();

        // Assert
        Assert.Equal(duration, execution.Duration);
    }

    [Fact]
    public void WithTimeRange_ShouldSetTimesAndCalculateDuration()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddSeconds(5);

        // Act
        var execution = new TestExecutionBuilder()
            .WithTimeRange(start, end)
            .Build();

        // Assert
        Assert.Equal(start, execution.StartTimeUtc);
        Assert.Equal(end, execution.EndTimeUtc);
        Assert.Equal(TimeSpan.FromSeconds(5), execution.Duration);
    }

    [Fact]
    public void WithException_ShouldSetAllExceptionFields()
    {
        // Arrange & Act
        var execution = new TestExecutionBuilder()
            .WithException("System.InvalidOperationException", "Something went wrong", "  at SomeMethod()")
            .Build();

        // Assert
        Assert.Equal("System.InvalidOperationException", execution.ExceptionType);
        Assert.Equal("Something went wrong", execution.ErrorMessage);
        Assert.Equal("  at SomeMethod()", execution.StackTrace);
    }

    [Fact]
    public void WithException_ShouldAllowNullStackTrace()
    {
        // Arrange & Act
        var execution = new TestExecutionBuilder()
            .WithException("SomeException", "Msg")
            .Build();

        // Assert
        Assert.Null(execution.StackTrace);
    }

    [Fact]
    public void WithErrorMessageHash_ShouldSetHash()
    {
        // Arrange & Act
        var execution = new TestExecutionBuilder()
            .WithErrorMessageHash("abc123")
            .Build();

        // Assert
        Assert.Equal("abc123", execution.ErrorMessageHash);
    }

    [Fact]
    public void WithStackTraceHash_ShouldSetHash()
    {
        // Arrange & Act
        var execution = new TestExecutionBuilder()
            .WithStackTraceHash("def456")
            .Build();

        // Assert
        Assert.Equal("def456", execution.StackTraceHash);
    }

    [Fact]
    public void WithMetadata_ShouldSetMetadata()
    {
        // Arrange
        var metadata = new TestMetadataBuilder()
            .AddCategory("Regression")
            .Build();

        // Act
        var execution = new TestExecutionBuilder()
            .WithMetadata(metadata)
            .Build();

        // Assert
        Assert.Contains("Regression", execution.Metadata.Categories);
    }

    [Fact]
    public void WithRetry_ShouldSetRetryMetadata()
    {
        // Arrange
        var retry = new RetryMetadataBuilder()
            .WithAttemptNumber(2)
            .WithMaxRetries(3)
            .Build();

        // Act
        var execution = new TestExecutionBuilder()
            .WithRetry(retry)
            .Build();

        // Assert
        Assert.NotNull(execution.Retry);
        Assert.Equal(2, execution.Retry!.AttemptNumber);
        Assert.Equal(3, execution.Retry.MaxRetries);
    }

    [Fact]
    public void WithRetry_NullRetry_ShouldSetRetryToNull()
    {
        // Arrange & Act
        var execution = new TestExecutionBuilder()
            .WithRetry(null)
            .Build();

        // Assert
        Assert.Null(execution.Retry);
    }

    [Fact]
    public void WithIdentity_ShouldSetIdentity()
    {
        // Arrange
        var identity = new TestIdentityBuilder()
            .WithTestFingerprint("stable-id")
            .WithFullyQualifiedName("Ns.Class.Method")
            .WithAssembly("TestAssembly")
            .WithMethodName("Method")
            .WithClassName("Class")
            .WithNamespace("Ns")
            .WithDisplayName("Method")
            .Build();

        // Act
        var execution = new TestExecutionBuilder()
            .WithIdentity(identity)
            .Build();

        // Assert
        Assert.Equal("stable-id", execution.Identity.TestFingerprint);
    }

    [Fact]
    public void WithIdentity_NullIdentity_ShouldThrowArgumentNullException()
    {
        // Arrange
        var builder = new TestExecutionBuilder();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithIdentity(null!));
    }

    // ---------------------------------------------------------------------------
    // Reset
    // ---------------------------------------------------------------------------

    [Fact]
    public void Reset_ShouldRestoreDefaultValues()
    {
        // Arrange
        var builder = new TestExecutionBuilder()
            .WithTestName("OldName")
            .WithOutcome(TestOutcome.Failed)
            .WithException("Ex", "Msg", "Trace");

        // Act
        builder.Reset();
        var execution = builder.Build();

        // Assert
        Assert.Equal(string.Empty, execution.TestName);
        Assert.Equal(TestOutcome.NotExecuted, execution.Outcome);
        Assert.Null(execution.ExceptionType);
        Assert.Null(execution.ErrorMessage);
        Assert.Null(execution.StackTrace);
    }

    [Fact]
    public void Reset_ShouldReturnBuilderInstance_ForChaining()
    {
        // Arrange
        var builder = new TestExecutionBuilder();

        // Act
        var returned = builder.Reset();

        // Assert
        Assert.Same(builder, returned);
    }

    // ---------------------------------------------------------------------------
    // Fluent chaining
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_ShouldSupportFullFluentChain()
    {
        // Arrange
        var start = new DateTime(2024, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var end = start.AddSeconds(2);

        // Act
        var execution = new TestExecutionBuilder()
            .WithTestName("Full_Chain_Test")
            .WithOutcome(TestOutcome.Passed)
            .WithTimeRange(start, end)
            .WithException(null, null)
            .WithErrorMessageHash(null)
            .WithStackTraceHash(null)
            .Build();

        // Assert
        Assert.Equal("Full_Chain_Test", execution.TestName);
        Assert.Equal(TestOutcome.Passed, execution.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(2), execution.Duration);
    }
}
