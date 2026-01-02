/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Collection;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Xping.Sdk.Core.Collection;
using Xping.Sdk.Core.Models;
using Xunit;

public sealed class ExecutionTrackerTests
{
    private static readonly int[] ExpectedSequentialPositions = { 1, 2, 3, 4, 5 };

    [Fact]
    public void Constructor_InitializesWithUniqueSuiteId()
    {
        // Arrange & Act
        var tracker1 = new ExecutionTracker();
        var tracker2 = new ExecutionTracker();

        // Assert
        Assert.NotEqual(tracker1.SuiteId, tracker2.SuiteId);
        Assert.NotEmpty(tracker1.SuiteId);
        Assert.NotEmpty(tracker2.SuiteId);
    }

    [Fact]
    public void Constructor_SetsSuiteStartTime()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var tracker = new ExecutionTracker();
        var after = DateTime.UtcNow;

        // Assert
        Assert.True(tracker.SuiteStartTime >= before);
        Assert.True(tracker.SuiteStartTime <= after);
    }

    [Fact]
    public void CreateContext_ForFirstTest_ReturnsPositionOne()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        var context = tracker.CreateContext();

        // Assert
        Assert.Equal(1, context.PositionInSuite);
        Assert.Equal(1, context.GlobalPosition);
    }

    [Fact]
    public void CreateContext_ForFirstTest_HasNoPreviousTest()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        var context = tracker.CreateContext();

        // Assert
        Assert.Null(context.PreviousTestId);
        Assert.Null(context.PreviousTestName);
        Assert.Null(context.PreviousTestOutcome);
    }

    [Fact]
    public void CreateContext_ForFirstTest_IsNotParallelized()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        var context = tracker.CreateContext();

        // Assert
        Assert.False(context.WasParallelized);
        Assert.Equal(1, context.ConcurrentTestCount);
    }

    [Fact]
    public void CreateContext_SequentialTests_IncrementsPosition()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        var context1 = tracker.CreateContext();
        var context2 = tracker.CreateContext();
        var context3 = tracker.CreateContext();

        // Assert
        Assert.Equal(1, context1.PositionInSuite);
        Assert.Equal(2, context2.PositionInSuite);
        Assert.Equal(3, context3.PositionInSuite);
    }

    [Fact]
    public void CreateContext_SequentialTests_IncrementsGlobalPosition()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        var context1 = tracker.CreateContext();
        var context2 = tracker.CreateContext();
        var context3 = tracker.CreateContext();

        // Assert
        Assert.Equal(1, context1.GlobalPosition);
        Assert.Equal(2, context2.GlobalPosition);
        Assert.Equal(3, context3.GlobalPosition);
    }

    [Fact]
    public void CreateContext_SetsThreadId()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        var context = tracker.CreateContext();

        // Assert
        Assert.NotEmpty(context.ThreadId);
        Assert.Equal(Environment.CurrentManagedThreadId.ToString(System.Globalization.CultureInfo.InvariantCulture), context.ThreadId);
    }

    [Fact]
    public void CreateContext_WithWorkerId_SetsWorkerId()
    {
        // Arrange
        var tracker = new ExecutionTracker();
        const string workerId = "worker-123";

        // Act
        var context = tracker.CreateContext(workerId);

        // Assert
        Assert.Equal(workerId, context.WorkerId);
    }

    [Fact]
    public void CreateContext_WithCollectionName_SetsCollectionName()
    {
        // Arrange
        var tracker = new ExecutionTracker();
        const string collectionName = "MyTestCollection";

        // Act
        var context = tracker.CreateContext(collectionName: collectionName);

        // Assert
        Assert.Equal(collectionName, context.CollectionName);
    }

    [Fact]
    public void CreateContext_SetsSuiteId()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        var context1 = tracker.CreateContext();
        var context2 = tracker.CreateContext();

        // Assert
        Assert.Equal(tracker.SuiteId, context1.TestSuiteId);
        Assert.Equal(tracker.SuiteId, context2.TestSuiteId);
        Assert.Equal(context1.TestSuiteId, context2.TestSuiteId);
    }

    [Fact]
    public void CreateContext_SetsSuiteElapsedTime()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        var context = tracker.CreateContext();

        // Assert
        Assert.True(context.SuiteElapsedTime >= TimeSpan.Zero);
        Assert.True(context.SuiteElapsedTime < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RecordTestCompletion_StoresPreviousTestInfo()
    {
        // Arrange
        var tracker = new ExecutionTracker();
        const string testId = "test-123";
        const string testName = "MyTest";
        const TestOutcome outcome = TestOutcome.Passed;

        // Act
        tracker.RecordTestCompletion(null, testId, testName, outcome);
        var context = tracker.CreateContext();

        // Assert
        Assert.Equal(testId, context.PreviousTestId);
        Assert.Equal(testName, context.PreviousTestName);
        Assert.Equal(outcome, context.PreviousTestOutcome);
    }

    [Fact]
    public void RecordTestCompletion_WithDifferentWorkers_TracksSeparately()
    {
        // Arrange
        var tracker = new ExecutionTracker();
        const string worker1 = "worker-1";
        const string worker2 = "worker-2";

        // Act
        tracker.RecordTestCompletion(worker1, "test-1", "Test1", TestOutcome.Passed);
        tracker.RecordTestCompletion(worker2, "test-2", "Test2", TestOutcome.Failed);

        var context1 = tracker.CreateContext(worker1);
        var context2 = tracker.CreateContext(worker2);

        // Assert
        Assert.Equal("test-1", context1.PreviousTestId);
        Assert.Equal("Test1", context1.PreviousTestName);
        Assert.Equal(TestOutcome.Passed, context1.PreviousTestOutcome);

        Assert.Equal("test-2", context2.PreviousTestId);
        Assert.Equal("Test2", context2.PreviousTestName);
        Assert.Equal(TestOutcome.Failed, context2.PreviousTestOutcome);
    }

    [Fact]
    public void RecordTestCompletion_UpdatesPreviousTest()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        tracker.RecordTestCompletion(null, "test-1", "Test1", TestOutcome.Passed);
        var context1 = tracker.CreateContext();

        tracker.RecordTestCompletion(null, "test-2", "Test2", TestOutcome.Failed);
        var context2 = tracker.CreateContext();

        // Assert
        Assert.Equal("test-1", context1.PreviousTestId);
        Assert.Equal("test-2", context2.PreviousTestId);
    }

    [Fact]
    public void GlobalPosition_ReturnsCurrentValue()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        _ = tracker.CreateContext();
        _ = tracker.CreateContext();
        var position = tracker.GlobalPosition;

        // Assert
        Assert.Equal(2, position);
    }

    [Fact]
    public void ActiveWorkerCount_ReturnsZeroInitially()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        var count = tracker.ActiveWorkerCount;

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void ActiveWorkerCount_IncrementsWithNewWorkers()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        _ = tracker.CreateContext("worker-1");
        var count1 = tracker.ActiveWorkerCount;

        _ = tracker.CreateContext("worker-2");
        var count2 = tracker.ActiveWorkerCount;

        // Assert
        Assert.Equal(1, count1);
        Assert.Equal(2, count2);
    }

    [Fact]
    public void GetWorkerPosition_ReturnsZeroForNewWorker()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        var position = tracker.GetWorkerPosition("worker-1");

        // Assert
        Assert.Equal(0, position);
    }

    [Fact]
    public void GetWorkerPosition_ReturnsCurrentPositionForActiveWorker()
    {
        // Arrange
        var tracker = new ExecutionTracker();
        const string workerId = "worker-1";

        // Act
        _ = tracker.CreateContext(workerId);
        _ = tracker.CreateContext(workerId);
        var position = tracker.GetWorkerPosition(workerId);

        // Assert
        Assert.Equal(2, position);
    }

    [Fact]
    public void GetPreviousTest_ReturnsNullForNewWorker()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        var previousTest = tracker.GetPreviousTest("worker-1");

        // Assert
        Assert.Null(previousTest);
    }

    [Fact]
    public void GetPreviousTest_ReturnsPreviousTestInfo()
    {
        // Arrange
        var tracker = new ExecutionTracker();
        const string workerId = "worker-1";
        const string testId = "test-123";
        const string testName = "MyTest";
        const TestOutcome outcome = TestOutcome.Passed;

        // Act
        tracker.RecordTestCompletion(workerId, testId, testName, outcome);
        var previousTest = tracker.GetPreviousTest(workerId);

        // Assert
        Assert.NotNull(previousTest);
        Assert.Equal(testId, previousTest!.TestId);
        Assert.Equal(testName, previousTest.TestName);
        Assert.Equal(outcome, previousTest.Outcome);
    }

    [Fact]
    public void Clear_ResetsAllState()
    {
        // Arrange
        var tracker = new ExecutionTracker();
        _ = tracker.CreateContext("worker-1");
        _ = tracker.CreateContext("worker-2");
        tracker.RecordTestCompletion("worker-1", "test-1", "Test1", TestOutcome.Passed);

        // Act
        tracker.Clear();

        // Assert
        Assert.Equal(0, tracker.GlobalPosition);
        Assert.Equal(0, tracker.ActiveWorkerCount);
        Assert.Equal(0, tracker.GetWorkerPosition("worker-1"));
        Assert.Null(tracker.GetPreviousTest("worker-1"));
    }

    [Fact]
    public async Task CreateContext_ParallelExecution_DetectsParallelization()
    {
        // Arrange
        var tracker = new ExecutionTracker();
        var contexts = new ConcurrentBag<Core.Models.ExecutionContext>();

        // Act - Create contexts from multiple threads simultaneously
        var tasks = Enumerable.Range(1, 5).Select(i => Task.Run(() =>
        {
            var workerId = $"worker-{i}";
            var context = tracker.CreateContext(workerId);
            contexts.Add(context);
        }));

        await Task.WhenAll(tasks);

        // Assert - At least some tests should detect parallelization
        Assert.Contains(contexts, c => c.WasParallelized);
        Assert.Contains(contexts, c => c.ConcurrentTestCount > 1);
    }

    [Fact]
    public async Task CreateContext_ParallelExecution_TracksPerWorkerPosition()
    {
        // Arrange
        var tracker = new ExecutionTracker();
        var contexts = new ConcurrentBag<Core.Models.ExecutionContext>();

        // Act - Each worker creates multiple contexts
        var tasks = Enumerable.Range(1, 3).Select(i => Task.Run(() =>
        {
            var workerId = $"worker-{i}";
            for (var j = 0; j < 5; j++)
            {
                var context = tracker.CreateContext(workerId);
                contexts.Add(context);
            }
        }));

        await Task.WhenAll(tasks);

        // Assert - Each worker should have positions 1-5
        var workerContexts = contexts.GroupBy(c => c.WorkerId);
        foreach (var workerGroup in workerContexts)
        {
            var positions = workerGroup.Select(c => c.PositionInSuite).OrderBy(p => p).ToArray();
            Assert.Equal(ExpectedSequentialPositions, positions);
        }
    }

    [Fact]
    public async Task CreateContext_ParallelExecution_GlobalPositionIsUnique()
    {
        // Arrange
        var tracker = new ExecutionTracker();
        var contexts = new ConcurrentBag<Core.Models.ExecutionContext>();

        // Act - Create contexts from multiple threads
        var tasks = Enumerable.Range(1, 10).Select(i => Task.Run(() =>
        {
            var workerId = $"worker-{i}";
            var context = tracker.CreateContext(workerId);
            contexts.Add(context);
        }));

        await Task.WhenAll(tasks);

        // Assert - All global positions should be unique
        var globalPositions = contexts.Select(c => c.GlobalPosition).ToList();
        Assert.Equal(10, globalPositions.Distinct().Count());
        Assert.All(globalPositions, pos => Assert.True(pos >= 1 && pos <= 10));
    }

    [Fact]
    public async Task RecordTestCompletion_ParallelExecution_IsThreadSafe()
    {
        // Arrange
        var tracker = new ExecutionTracker();
        var exceptions = new ConcurrentBag<Exception>();

        // Act - Record completions from multiple threads
        var tasks = Enumerable.Range(1, 100).Select(i => Task.Run(() =>
        {
            try
            {
                var workerId = $"worker-{i % 5}";
                tracker.RecordTestCompletion(workerId, $"test-{i}", $"Test{i}", TestOutcome.Passed);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }));

        await Task.WhenAll(tasks);

        // Assert - No exceptions should be thrown
        Assert.Empty(exceptions);
    }

    [Fact]
    public void CreateContext_AfterRecordTestCompletion_LinksPreviousTest()
    {
        // Arrange
        var tracker = new ExecutionTracker();
        const string workerId = "worker-1";

        // Act - Simulate sequential test execution
        var context1 = tracker.CreateContext(workerId);
        tracker.RecordTestCompletion(workerId, "test-1", "Test1", TestOutcome.Passed);

        var context2 = tracker.CreateContext(workerId);
        tracker.RecordTestCompletion(workerId, "test-2", "Test2", TestOutcome.Failed);

        var context3 = tracker.CreateContext(workerId);

        // Assert
        Assert.Null(context1.PreviousTestId);
        Assert.Equal("test-1", context2.PreviousTestId);
        Assert.Equal("Test1", context2.PreviousTestName);
        Assert.Equal(TestOutcome.Passed, context2.PreviousTestOutcome);

        Assert.Equal("test-2", context3.PreviousTestId);
        Assert.Equal("Test2", context3.PreviousTestName);
        Assert.Equal(TestOutcome.Failed, context3.PreviousTestOutcome);
    }

    [Fact]
    public void CreateContext_WithDifferentWorkers_MaintainsSeparatePositions()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        var worker1Context1 = tracker.CreateContext("worker-1");
        var worker2Context1 = tracker.CreateContext("worker-2");
        var worker1Context2 = tracker.CreateContext("worker-1");
        var worker2Context2 = tracker.CreateContext("worker-2");

        // Assert
        Assert.Equal(1, worker1Context1.PositionInSuite);
        Assert.Equal(1, worker2Context1.PositionInSuite);
        Assert.Equal(2, worker1Context2.PositionInSuite);
        Assert.Equal(2, worker2Context2.PositionInSuite);
    }

    [Fact]
    public void CreateContext_SuiteElapsedTime_IncreasesOverTime()
    {
        // Arrange
        var tracker = new ExecutionTracker();

        // Act
        var context1 = tracker.CreateContext();
        System.Threading.Thread.Sleep(10);
        var context2 = tracker.CreateContext();

        // Assert
        Assert.True(context2.SuiteElapsedTime > context1.SuiteElapsedTime);
    }
}
