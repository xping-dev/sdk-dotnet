/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Tests.Models;

using Xping.Sdk.Core.Models;

public sealed class TestExecutionTests
{
    [Fact]
    public void ShouldAllowSettingAllProperties()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var sessionId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow.AddSeconds(-10);
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        var metadata = new TestMetadata
        {
            Categories = new List<string> { "Unit" },
            Tags = new List<string> { "fast" },
            Description = "Test description"
        };

        // Act
        var execution = new TestExecution
        {
            ExecutionId = executionId,
            Identity = new TestIdentity
            {
                TestId = "test-123",
                FullyQualifiedName = "Calculator.Tests.CalculatorTests.Add_TwoNumbers_ReturnsSum",
                Assembly = "Calculator.Tests",
                Namespace = "Calculator.Tests",
                ClassName = "CalculatorTests",
                MethodName = "Add_TwoNumbers_ReturnsSum"
            },
            TestName = "Add_TwoNumbers_ReturnsSum",
            Outcome = TestOutcome.Passed,
            Duration = duration,
            StartTimeUtc = startTime,
            EndTimeUtc = endTime,
            SessionContext = new TestSession
            {
                SessionId = sessionId,
                StartedAt = startTime,
                EnvironmentInfo = new EnvironmentInfo()
            },
            Metadata = metadata,
            ErrorMessage = null,
            StackTrace = null
        };

        // Assert
        Assert.NotNull(execution);
        Assert.Equal(executionId, execution.ExecutionId);
        Assert.Equal("test-123", execution.Identity.TestId);
        Assert.Equal("Add_TwoNumbers_ReturnsSum", execution.TestName);
        Assert.Equal("Calculator.Tests.CalculatorTests.Add_TwoNumbers_ReturnsSum", execution.Identity.FullyQualifiedName);
        Assert.Equal("Calculator.Tests", execution.Identity.Assembly);
        Assert.Equal("Calculator.Tests", execution.Identity.Namespace);
        Assert.Equal(TestOutcome.Passed, execution.Outcome);
        Assert.Equal(duration, execution.Duration);
        Assert.Equal(startTime, execution.StartTimeUtc);
        Assert.Equal(endTime, execution.EndTimeUtc);
        Assert.Equal(sessionId, execution.SessionContext?.SessionId);
        Assert.Same(metadata, execution.Metadata);
        Assert.Null(execution.ErrorMessage);
        Assert.Null(execution.StackTrace);
    }

    [Fact]
    public void ShouldSupportFailedTestWithErrorDetails()
    {
        // Arrange
        var errorMessage = "Expected: 5\nActual: 4";
        var stackTrace = "   at Calculator.Tests.CalculatorTests.Add_TwoNumbers_ReturnsSum() in CalculatorTests.cs:line 15";

        // Act
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity
            {
                TestId = "test-456",
                FullyQualifiedName = "Calculator.Tests.CalculatorTests.Divide_ByZero_ThrowsException"
            },
            TestName = "Divide_ByZero_ThrowsException",
            Outcome = TestOutcome.Failed,
            ErrorMessage = errorMessage,
            StackTrace = stackTrace
        };

        // Assert
        Assert.Equal(TestOutcome.Failed, execution.Outcome);
        Assert.Equal(errorMessage, execution.ErrorMessage);
        Assert.Equal(stackTrace, execution.StackTrace);
    }

    [Fact]
    public void ShouldSupportSkippedTest()
    {
        // Act
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity { TestId = "test-789" },
            TestName = "SlowTest_Skipped",
            Outcome = TestOutcome.Skipped
        };

        // Assert
        Assert.Equal(TestOutcome.Skipped, execution.Outcome);
    }

    [Fact]
    public void ShouldSupportInconclusiveTest()
    {
        // Act
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity { TestId = "test-101" },
            TestName = "TestWithInconclusiveResult",
            Outcome = TestOutcome.Inconclusive
        };

        // Assert
        Assert.Equal(TestOutcome.Inconclusive, execution.Outcome);
    }

    [Fact]
    public void ShouldAllowZeroDuration()
    {
        // Act
        var execution = new TestExecution
        {
            Duration = TimeSpan.Zero
        };

        // Assert
        Assert.Equal(TimeSpan.Zero, execution.Duration);
    }

    [Fact]
    public void ShouldAllowLongDuration()
    {
        // Arrange
        var longDuration = TimeSpan.FromHours(2);

        // Act
        var execution = new TestExecution
        {
            Duration = longDuration
        };

        // Assert
        Assert.Equal(longDuration, execution.Duration);
    }

    [Fact]
    public void ExecutionIdShouldSupportGuidEmpty()
    {
        // Act
        var execution = new TestExecution
        {
            ExecutionId = Guid.Empty
        };

        // Assert
        Assert.Equal(Guid.Empty, execution.ExecutionId);
    }

    [Fact]
    public void ShouldAllowNullSessionContext()
    {
        // Act
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            TestName = "Test",
            SessionContext = null
        };

        // Assert
        Assert.Null(execution.SessionContext);
    }

    [Fact]
    public void ShouldAllowNullMetadata()
    {
        // Act
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            TestName = "Test",
            Metadata = null
        };

        // Assert
        Assert.Null(execution.Metadata);
    }

    [Fact]
    public void Should_Allow_Null_Retry_Metadata()
    {
        // Act
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            TestName = "Test",
            Retry = null
        };

        // Assert
        Assert.Null(execution.Retry);
    }

    [Fact]
    public void Should_Support_Test_With_Retry_Metadata()
    {
        // Arrange
        var retryMetadata = new RetryMetadata
        {
            AttemptNumber = 2,
            MaxRetries = 3,
            PassedOnRetry = true,
            DelayBetweenRetries = TimeSpan.FromMilliseconds(100),
            RetryReason = "Timeout",
            RetryAttributeName = "RetryFact"
        };

        // Act
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity { TestId = "test-retry-123" },
            TestName = "FlakyTest_PassesOnRetry",
            Outcome = TestOutcome.Passed,
            Retry = retryMetadata
        };

        // Assert
        Assert.NotNull(execution.Retry);
        Assert.Equal(2, execution.Retry.AttemptNumber);
        Assert.Equal(3, execution.Retry.MaxRetries);
        Assert.True(execution.Retry.PassedOnRetry);
        Assert.Equal(TimeSpan.FromMilliseconds(100), execution.Retry.DelayBetweenRetries);
        Assert.Equal("Timeout", execution.Retry.RetryReason);
        Assert.Equal("RetryFact", execution.Retry.RetryAttributeName);
    }

    [Fact]
    public void Should_Support_First_Attempt_With_Retry_Configuration()
    {
        // Arrange - test configured for retry but passes on first attempt
        var retryMetadata = new RetryMetadata
        {
            AttemptNumber = 1,
            MaxRetries = 3,
            PassedOnRetry = false,
            RetryAttributeName = "Retry"
        };

        // Act
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity { TestId = "test-stable-123" },
            TestName = "StableTest_PassesImmediately",
            Outcome = TestOutcome.Passed,
            Retry = retryMetadata
        };

        // Assert
        Assert.NotNull(execution.Retry);
        Assert.Equal(1, execution.Retry.AttemptNumber);
        Assert.False(execution.Retry.PassedOnRetry);
    }

    [Fact]
    public void Should_Support_Failed_Test_With_Retry_Metadata()
    {
        // Arrange - test that failed even with retry
        var retryMetadata = new RetryMetadata
        {
            AttemptNumber = 4,
            MaxRetries = 3,
            PassedOnRetry = false,
            RetryAttributeName = "RetryTheory"
        };

        // Act
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity { TestId = "test-failed-retry-123" },
            TestName = "AlwaysFailingTest",
            Outcome = TestOutcome.Failed,
            ErrorMessage = "Test failed after all retry attempts",
            Retry = retryMetadata
        };

        // Assert
        Assert.NotNull(execution.Retry);
        Assert.Equal(TestOutcome.Failed, execution.Outcome);
        Assert.Equal(4, execution.Retry.AttemptNumber);
        Assert.False(execution.Retry.PassedOnRetry);
    }
}
