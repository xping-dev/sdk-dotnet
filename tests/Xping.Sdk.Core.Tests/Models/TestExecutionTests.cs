/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Models;

using Xping.Sdk.Core.Models;

public class TestExecutionTests
{
    [Fact]
    public void ShouldAllowSettingAllProperties()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.AddSeconds(-10);
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        var environment = new EnvironmentInfo
        {
            MachineName = "test-machine",
            OperatingSystem = "macOS 14.0",
            RuntimeVersion = ".NET 8.0.0",
            Framework = "xUnit 2.6.5",
            EnvironmentName = "Local",
            IsCIEnvironment = false
        };

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
            TestId = "test-123",
            TestName = "Add_TwoNumbers_ReturnsSum",
            FullyQualifiedName = "Calculator.Tests.CalculatorTests.Add_TwoNumbers_ReturnsSum",
            Assembly = "Calculator.Tests",
            Namespace = "Calculator.Tests",
            Outcome = TestOutcome.Passed,
            Duration = duration,
            StartTimeUtc = startTime,
            EndTimeUtc = endTime,
            Environment = environment,
            Metadata = metadata,
            ErrorMessage = null,
            StackTrace = null
        };

        // Assert
        Assert.Equal(executionId, execution.ExecutionId);
        Assert.Equal("test-123", execution.TestId);
        Assert.Equal("Add_TwoNumbers_ReturnsSum", execution.TestName);
        Assert.Equal("Calculator.Tests.CalculatorTests.Add_TwoNumbers_ReturnsSum", execution.FullyQualifiedName);
        Assert.Equal("Calculator.Tests", execution.Assembly);
        Assert.Equal("Calculator.Tests", execution.Namespace);
        Assert.Equal(TestOutcome.Passed, execution.Outcome);
        Assert.Equal(duration, execution.Duration);
        Assert.Equal(startTime, execution.StartTimeUtc);
        Assert.Equal(endTime, execution.EndTimeUtc);
        Assert.Same(environment, execution.Environment);
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
            TestId = "test-456",
            TestName = "Divide_ByZero_ThrowsException",
            FullyQualifiedName = "Calculator.Tests.CalculatorTests.Divide_ByZero_ThrowsException",
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
            TestId = "test-789",
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
            TestId = "test-101",
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
    public void ShouldAllowNullEnvironment()
    {
        // Act
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            TestName = "Test",
            Environment = null
        };

        // Assert
        Assert.Null(execution.Environment);
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
}
