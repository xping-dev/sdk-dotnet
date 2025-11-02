/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Persistence;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Persistence;
using Xunit;

/// <summary>
/// Unit tests for <see cref="FileBasedOfflineQueue"/>.
/// </summary>
public sealed class FileBasedOfflineQueueTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly List<FileBasedOfflineQueue> _queues = new List<FileBasedOfflineQueue>();
    private bool _disposed;

    public FileBasedOfflineQueueTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"xping-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var queue in _queues)
        {
            queue.Dispose();
        }

        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        _disposed = true;
    }

    private FileBasedOfflineQueue CreateQueue(int maxQueueSize = 10000, int cleanupAgeDays = 7)
    {
        var queue = new FileBasedOfflineQueue(_testDirectory, maxQueueSize, cleanupAgeDays);
        _queues.Add(queue);
        return queue;
    }

    private List<TestExecution> CreateTestExecutions(int count)
    {
        var executions = new List<TestExecution>();
        for (int i = 0; i < count; i++)
        {
            executions.Add(new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                TestName = $"Test{i}",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(100),
                StartTimeUtc = DateTime.UtcNow,
                EndTimeUtc = DateTime.UtcNow.AddMilliseconds(100),
            });
        }

        return executions;
    }

    [Fact]
    public async Task EnqueueAsync_WithValidExecutions_StoresSuccessfully()
    {
        var queue = CreateQueue();
        var executions = CreateTestExecutions(5);

        await queue.EnqueueAsync(executions);

        var size = await queue.GetQueueSizeAsync();
        Assert.Equal(5, size);
    }

    [Fact]
    public async Task EnqueueAsync_WithNull_DoesNothing()
    {
        var queue = CreateQueue();

        await queue.EnqueueAsync(null);

        var size = await queue.GetQueueSizeAsync();
        Assert.Equal(0, size);
    }

    [Fact]
    public async Task EnqueueAsync_WithEmptyList_DoesNothing()
    {
        var queue = CreateQueue();

        await queue.EnqueueAsync(new List<TestExecution>());

        var size = await queue.GetQueueSizeAsync();
        Assert.Equal(0, size);
    }

    [Fact]
    public async Task DequeueAsync_WithItemsInQueue_ReturnsCorrectCount()
    {
        var queue = CreateQueue();
        var executions = CreateTestExecutions(10);
        await queue.EnqueueAsync(executions);

        var dequeued = await queue.DequeueAsync(5);

        Assert.Equal(5, dequeued.Count());
        var remainingSize = await queue.GetQueueSizeAsync();
        Assert.Equal(5, remainingSize);
    }

    [Fact]
    public async Task DequeueAsync_WithEmptyQueue_ReturnsEmpty()
    {
        var queue = CreateQueue();

        var dequeued = await queue.DequeueAsync(10);

        Assert.Empty(dequeued);
    }

    [Fact]
    public async Task DequeueAsync_WithZeroCount_ReturnsEmpty()
    {
        var queue = CreateQueue();
        var executions = CreateTestExecutions(5);
        await queue.EnqueueAsync(executions);

        var dequeued = await queue.DequeueAsync(0);

        Assert.Empty(dequeued);
        var size = await queue.GetQueueSizeAsync();
        Assert.Equal(5, size);
    }

    [Fact]
    public async Task DequeueAsync_WithMoreThanAvailable_ReturnsAll()
    {
        var queue = CreateQueue();
        var executions = CreateTestExecutions(5);
        await queue.EnqueueAsync(executions);

        var dequeued = await queue.DequeueAsync(10);

        Assert.Equal(5, dequeued.Count());
        var size = await queue.GetQueueSizeAsync();
        Assert.Equal(0, size);
    }

    [Fact]
    public async Task GetQueueSizeAsync_WithMultipleBatches_ReturnsCorrectTotal()
    {
        var queue = CreateQueue();
        var batch1 = CreateTestExecutions(5);
        var batch2 = CreateTestExecutions(3);
        var batch3 = CreateTestExecutions(7);

        await queue.EnqueueAsync(batch1);
        await queue.EnqueueAsync(batch2);
        await queue.EnqueueAsync(batch3);

        var size = await queue.GetQueueSizeAsync();

        Assert.Equal(15, size);
    }

    [Fact]
    public async Task ClearAsync_RemovesAllItems()
    {
        var queue = CreateQueue();
        await queue.EnqueueAsync(CreateTestExecutions(10));
        await queue.EnqueueAsync(CreateTestExecutions(5));

        await queue.ClearAsync();

        var size = await queue.GetQueueSizeAsync();
        Assert.Equal(0, size);
    }

    [Fact]
    public async Task EnqueueAsync_ExceedingMaxSize_ThrowsException()
    {
        var queue = CreateQueue(maxQueueSize: 10);
        await queue.EnqueueAsync(CreateTestExecutions(5));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await queue.EnqueueAsync(CreateTestExecutions(10)));

        Assert.Contains("Queue size limit reached", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnqueueAsync_AtMaxSize_AcceptsItems()
    {
        var queue = CreateQueue(maxQueueSize: 10);

        await queue.EnqueueAsync(CreateTestExecutions(10));

        var size = await queue.GetQueueSizeAsync();
        Assert.Equal(10, size);
    }

    [Fact]
    public async Task CleanupAsync_RemovesOldFiles()
    {
        var queue = CreateQueue(cleanupAgeDays: 0);
        await queue.EnqueueAsync(CreateTestExecutions(5));

        // Wait a moment to ensure file timestamp is in the past
        await Task.Delay(100);

        await queue.CleanupAsync();

        var size = await queue.GetQueueSizeAsync();
        Assert.Equal(0, size);
    }

    [Fact]
    public async Task CleanupAsync_PreservesRecentFiles()
    {
        var queue = CreateQueue(cleanupAgeDays: 7);
        await queue.EnqueueAsync(CreateTestExecutions(5));

        await queue.CleanupAsync();

        var size = await queue.GetQueueSizeAsync();
        Assert.Equal(5, size);
    }

    [Fact]
    public async Task DequeueAsync_WithCorruptedFile_SkipsFile()
    {
        var queue = CreateQueue();
        await queue.EnqueueAsync(CreateTestExecutions(5));

        // Corrupt a file
        var files = Directory.GetFiles(_testDirectory, "*.json");
        if (files.Length > 0)
        {
            await File.WriteAllTextAsync(files[0], "{ invalid json }");
        }

        // Should skip corrupted file and not throw
        var dequeued = await queue.DequeueAsync(10);

        Assert.Empty(dequeued);
    }

    [Fact]
    public async Task DequeueAsync_PreservesTestExecutionData()
    {
        var queue = CreateQueue();
        var original = new List<TestExecution>
        {
            new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                TestName = "MyTest",
                FullyQualifiedName = "MyNamespace.MyClass.MyTest",
                Namespace = "MyNamespace",
                Outcome = TestOutcome.Failed,
                Duration = TimeSpan.FromMilliseconds(1234),
                ErrorMessage = "Test failed",
                StackTrace = "at MyClass.MyTest()",
                StartTimeUtc = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                EndTimeUtc = new DateTime(2025, 1, 15, 12, 0, 1, DateTimeKind.Utc),
            },
        };

        await queue.EnqueueAsync(original);
        var dequeued = (await queue.DequeueAsync(1)).ToList();

        Assert.Single(dequeued);
        var item = dequeued[0];
        Assert.Equal("MyTest", item.TestName);
        Assert.Equal("MyNamespace.MyClass.MyTest", item.FullyQualifiedName);
        Assert.Equal(TestOutcome.Failed, item.Outcome);
        Assert.Equal(TimeSpan.FromMilliseconds(1234), item.Duration);
        Assert.Equal("Test failed", item.ErrorMessage);
        Assert.Equal("at MyClass.MyTest()", item.StackTrace);
    }

    [Fact]
    public async Task ConcurrentOperations_MaintainConsistency()
    {
        var queue = CreateQueue();
        var tasks = new List<Task>();

        // Create all batches first to avoid calling CreateTestExecutions concurrently
        var batches = new List<List<TestExecution>>();
        for (int i = 0; i < 10; i++)
        {
            batches.Add(CreateTestExecutions(5));
        }

        // Enqueue concurrently
        foreach (var batch in batches)
        {
            tasks.Add(Task.Run(async () => await queue.EnqueueAsync(batch)));
        }

        await Task.WhenAll(tasks);

        var size = await queue.GetQueueSizeAsync();
        Assert.Equal(50, size);
    }

    [Fact]
    public async Task DequeueAsync_MaintainsOrderByTimestamp()
    {
        var queue = CreateQueue();

        // Enqueue three batches with delays
        var batch1 = new List<TestExecution>
        {
            new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                TestName = "First",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(100),
                StartTimeUtc = DateTime.UtcNow,
                EndTimeUtc = DateTime.UtcNow,
            },
        };
        await queue.EnqueueAsync(batch1);
        await Task.Delay(50);

        var batch2 = new List<TestExecution>
        {
            new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                TestName = "Second",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(100),
                StartTimeUtc = DateTime.UtcNow,
                EndTimeUtc = DateTime.UtcNow,
            },
        };
        await queue.EnqueueAsync(batch2);
        await Task.Delay(50);

        var batch3 = new List<TestExecution>
        {
            new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                TestName = "Third",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(100),
                StartTimeUtc = DateTime.UtcNow,
                EndTimeUtc = DateTime.UtcNow,
            },
        };
        await queue.EnqueueAsync(batch3);

        // Dequeue should return in order
        var first = (await queue.DequeueAsync(1)).FirstOrDefault();
        Assert.Equal("First", first?.TestName);

        var second = (await queue.DequeueAsync(1)).FirstOrDefault();
        Assert.Equal("Second", second?.TestName);

        var third = (await queue.DequeueAsync(1)).FirstOrDefault();
        Assert.Equal("Third", third?.TestName);
    }

    [Fact]
    public async Task MultipleQueues_OperateIndependently()
    {
        var dir1 = Path.Combine(_testDirectory, "queue1");
        var dir2 = Path.Combine(_testDirectory, "queue2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        var queue1 = new FileBasedOfflineQueue(dir1);
        var queue2 = new FileBasedOfflineQueue(dir2);
        _queues.Add(queue1);
        _queues.Add(queue2);

        await queue1.EnqueueAsync(CreateTestExecutions(5));
        await queue2.EnqueueAsync(CreateTestExecutions(10));

        var size1 = await queue1.GetQueueSizeAsync();
        var size2 = await queue2.GetQueueSizeAsync();

        Assert.Equal(5, size1);
        Assert.Equal(10, size2);
    }
}
