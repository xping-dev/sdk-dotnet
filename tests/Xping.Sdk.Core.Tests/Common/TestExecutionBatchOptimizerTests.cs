/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Common;

using System;
using System.Collections.Generic;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Upload;
using Xunit;

public class TestExecutionBatchOptimizerTests
{
    [Fact]
    public void OptimizeForTransport_WithNullExecutions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            TestExecutionBatchOptimizer.OptimizeForTransport(null!));
    }

    [Fact]
    public void OptimizeForTransport_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var executions = new List<TestExecution>();

        // Act
        var result = TestExecutionBatchOptimizer.OptimizeForTransport(executions);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void OptimizeForTransport_WithSingleExecution_PreservesSessionContext()
    {
        // Arrange
        var sessionContext = CreateSessionContext();
        var executions = new List<TestExecution>
        {
            CreateTestExecution("Test1", sessionContext)
        };

        // Act
        var result = TestExecutionBatchOptimizer.OptimizeForTransport(executions);

        // Assert
        Assert.Single(result);
        Assert.NotNull(result[0].SessionContext);
        Assert.Equal(sessionContext.SessionId, result[0].SessionContext!.SessionId);
    }

    [Fact]
    public void OptimizeForTransport_WithMultipleExecutions_OnlyFirstHasSessionContext()
    {
        // Arrange
        var sessionContext = CreateSessionContext();
        var executions = new List<TestExecution>
        {
            CreateTestExecution("Test1", sessionContext),
            CreateTestExecution("Test2", sessionContext),
            CreateTestExecution("Test3", sessionContext)
        };

        // Act
        var result = TestExecutionBatchOptimizer.OptimizeForTransport(executions);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.NotNull(result[0].SessionContext);
        Assert.Equal(sessionContext.SessionId, result[0].SessionContext!.SessionId);
        Assert.Null(result[1].SessionContext);
        Assert.Null(result[2].SessionContext);
    }

    [Fact]
    public void OptimizeForTransport_PreservesExecutionOrder()
    {
        // Arrange
        var sessionContext = CreateSessionContext();
        var executions = new List<TestExecution>
        {
            CreateTestExecution("Test1", sessionContext),
            CreateTestExecution("Test2", sessionContext),
            CreateTestExecution("Test3", sessionContext)
        };

        // Act
        var result = TestExecutionBatchOptimizer.OptimizeForTransport(executions);

        // Assert
        Assert.Equal("Test1", result[0].TestName);
        Assert.Equal("Test2", result[1].TestName);
        Assert.Equal("Test3", result[2].TestName);
    }

    [Fact]
    public void RehydrateFromTransport_WithNullExecutions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            TestExecutionBatchOptimizer.RehydrateFromTransport(null!));
    }

    [Fact]
    public void RehydrateFromTransport_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var executions = new List<TestExecution>();

        // Act
        var result = TestExecutionBatchOptimizer.RehydrateFromTransport(executions);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void RehydrateFromTransport_WithSingleExecution_PreservesSessionContext()
    {
        // Arrange
        var sessionContext = CreateSessionContext();
        var executions = new List<TestExecution>
        {
            CreateTestExecution("Test1", sessionContext)
        };

        // Act
        var result = TestExecutionBatchOptimizer.RehydrateFromTransport(executions);

        // Assert
        Assert.Single(result);
        Assert.NotNull(result[0].SessionContext);
        Assert.Equal(sessionContext.SessionId, result[0].SessionContext!.SessionId);
    }

    [Fact]
    public void RehydrateFromTransport_PopulatesNullContextsFromFirst()
    {
        // Arrange
        var sessionContext = CreateSessionContext();
        var executions = new List<TestExecution>
        {
            CreateTestExecution("Test1", sessionContext),
            CreateTestExecution("Test2", null),
            CreateTestExecution("Test3", null)
        };

        // Act
        var result = TestExecutionBatchOptimizer.RehydrateFromTransport(executions);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.NotNull(result[0].SessionContext);
        Assert.NotNull(result[1].SessionContext);
        Assert.NotNull(result[2].SessionContext);
        Assert.Equal(sessionContext.SessionId, result[0].SessionContext!.SessionId);
        Assert.Equal(sessionContext.SessionId, result[1].SessionContext!.SessionId);
        Assert.Equal(sessionContext.SessionId, result[2].SessionContext!.SessionId);
        // Verify it's the same instance (reference equality)
        Assert.Same(result[0].SessionContext, result[1].SessionContext);
        Assert.Same(result[0].SessionContext, result[2].SessionContext);
    }

    [Fact]
    public void RehydrateFromTransport_WithFirstExecutionHavingNullContext_ReturnsUnchanged()
    {
        // Arrange
        var executions = new List<TestExecution>
        {
            CreateTestExecution("Test1", null),
            CreateTestExecution("Test2", null)
        };

        // Act
        var result = TestExecutionBatchOptimizer.RehydrateFromTransport(executions);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Null(result[0].SessionContext);
        Assert.Null(result[1].SessionContext);
    }

    [Fact]
    public void RehydrateFromTransport_PreservesExistingContexts()
    {
        // Arrange
        var sessionContext1 = CreateSessionContext("session-1");
        var sessionContext2 = CreateSessionContext("session-2");
        var executions = new List<TestExecution>
        {
            CreateTestExecution("Test1", sessionContext1),
            CreateTestExecution("Test2", sessionContext2),
            CreateTestExecution("Test3", null)
        };

        // Act
        var result = TestExecutionBatchOptimizer.RehydrateFromTransport(executions);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("session-1", result[0].SessionContext!.SessionId);
        Assert.Equal("session-2", result[1].SessionContext!.SessionId);
        Assert.Equal("session-1", result[2].SessionContext!.SessionId); // Gets context from first
    }

    [Fact]
    public void RoundTrip_OptimizeAndRehydrate_RestoresOriginalState()
    {
        // Arrange
        var sessionContext = CreateSessionContext();
        var originalExecutions = new List<TestExecution>
        {
            CreateTestExecution("Test1", sessionContext),
            CreateTestExecution("Test2", sessionContext),
            CreateTestExecution("Test3", sessionContext)
        };

        // Act
        var optimized = TestExecutionBatchOptimizer.OptimizeForTransport(originalExecutions);
        var rehydrated = TestExecutionBatchOptimizer.RehydrateFromTransport(optimized);

        // Assert
        Assert.Equal(3, rehydrated.Count);
        Assert.All(rehydrated, execution => Assert.NotNull(execution.SessionContext));
        Assert.All(rehydrated, execution =>
            Assert.Equal(sessionContext.SessionId, execution.SessionContext!.SessionId));
    }

    private static TestSession CreateSessionContext(string? sessionId = null)
    {
        return new TestSession
        {
            SessionId = sessionId ?? Guid.NewGuid().ToString(),
            StartedAt = DateTime.UtcNow,
            EnvironmentInfo = new EnvironmentInfo
            {
                MachineName = "test-machine",
                OperatingSystem = "Windows 11",
                RuntimeVersion = ".NET 8.0"
            }
        };
    }

    private static TestExecution CreateTestExecution(string testName, TestSession? sessionContext)
    {
        return new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            TestName = testName,
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(100),
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow,
            SessionContext = sessionContext,
            Metadata = new TestMetadata()
        };
    }
}
