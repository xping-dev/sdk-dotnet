/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

#pragma warning disable CA1515 // Consider making public types internal
#pragma warning disable CA5394 // Do not use insecure randomness
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
#pragma warning disable CA1024 // Use properties where appropriate
#pragma warning disable IDE0055 // Fix formatting

namespace Xping.Sdk.Benchmarks;

using BenchmarkDotNet.Attributes;
using Xping.Sdk.Core.Collection;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Upload;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Concurrency stress testing benchmarks for multi-threaded scenarios.
/// Tests thread-safety, lock contention, and race condition detection under extreme parallelism.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class ConcurrencyBenchmarks
{
    private readonly Random _random = new();

    private sealed class NoOpUploader : ITestResultUploader
    {
        public Task<UploadResult> UploadAsync(IEnumerable<TestExecution> executions, CancellationToken cancellationToken = default)
        {
            var count = executions.Count();
            return Task.FromResult(new UploadResult 
            { 
                Success = true, 
                ExecutionCount = count 
            });
        }
    }

    /// <summary>
    /// Low parallelism: 4 threads recording simultaneously.
    /// Validates: Basic thread-safety, minimal contention.
    /// </summary>
    [Benchmark]
    public async Task LowParallelism_4Threads_100TestsEach()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "concurrency-test-key",
            ProjectId = "concurrency-project",
            BatchSize = 50,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        var tasks = Enumerable.Range(0, 4).Select(async threadId =>
        {
            for (int i = 0; i < 100; i++)
            {
                var execution = new TestExecution
                {
                    ExecutionId = Guid.NewGuid(),
                    Identity = new TestIdentity 
                    { 
                        TestId = $"thread{threadId}-test{i}",
                        FullyQualifiedName = $"Concurrency.Low.Thread{threadId}.Test{i}"
                    },
                    TestName = $"Thread {threadId} Test {i}",
                    Outcome = TestOutcome.Passed,
                    Duration = TimeSpan.FromMilliseconds(_random.Next(10, 100)),
                    StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-50),
                    EndTimeUtc = DateTime.UtcNow
                };

                collector.RecordTest(execution);
            }

            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Medium parallelism: 8 threads recording simultaneously.
    /// Validates: Moderate concurrency handling, lock efficiency.
    /// </summary>
    [Benchmark]
    public async Task MediumParallelism_8Threads_100TestsEach()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "concurrency-test-key",
            ProjectId = "concurrency-project",
            BatchSize = 100,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        var tasks = Enumerable.Range(0, 8).Select(async threadId =>
        {
            for (int i = 0; i < 100; i++)
            {
                var execution = new TestExecution
                {
                    ExecutionId = Guid.NewGuid(),
                    Identity = new TestIdentity 
                    { 
                        TestId = $"thread{threadId}-test{i}",
                        FullyQualifiedName = $"Concurrency.Medium.Thread{threadId}.Test{i}"
                    },
                    TestName = $"Thread {threadId} Test {i}",
                    Outcome = i % 10 == 0 ? TestOutcome.Failed : TestOutcome.Passed,
                    Duration = TimeSpan.FromMilliseconds(_random.Next(5, 80)),
                    StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-40),
                    EndTimeUtc = DateTime.UtcNow
                };

                if (i % 10 == 0)
                {
                    execution.ErrorMessage = $"Thread {threadId} test {i} failed";
                }

                collector.RecordTest(execution);
            }

            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// High parallelism: 16 threads recording simultaneously.
    /// Validates: High concurrency thread-safety, contention under pressure.
    /// </summary>
    [Benchmark]
    public async Task HighParallelism_16Threads_100TestsEach()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "concurrency-test-key",
            ProjectId = "concurrency-project",
            BatchSize = 200,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        var tasks = Enumerable.Range(0, 16).Select(async threadId =>
        {
            for (int i = 0; i < 100; i++)
            {
                var execution = new TestExecution
                {
                    ExecutionId = Guid.NewGuid(),
                    Identity = new TestIdentity 
                    { 
                        TestId = $"thread{threadId}-test{i}",
                        FullyQualifiedName = $"Concurrency.High.Thread{threadId}.Test{i}"
                    },
                    TestName = $"Thread {threadId} Test {i}",
                    Outcome = TestOutcome.Passed,
                    Duration = TimeSpan.FromMilliseconds(_random.Next(5, 60)),
                    StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-30),
                    EndTimeUtc = DateTime.UtcNow
                };

                collector.RecordTest(execution);
            }

            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Extreme parallelism: 32 threads recording simultaneously.
    /// Validates: Extreme concurrency limits, system behavior under maximum pressure.
    /// </summary>
    [Benchmark]
    public async Task ExtremeParallelism_32Threads_50TestsEach()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "concurrency-test-key",
            ProjectId = "concurrency-project",
            BatchSize = 200,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        var tasks = Enumerable.Range(0, 32).Select(async threadId =>
        {
            for (int i = 0; i < 50; i++)
            {
                var execution = new TestExecution
                {
                    ExecutionId = Guid.NewGuid(),
                    Identity = new TestIdentity 
                    { 
                        TestId = $"thread{threadId}-test{i}",
                        FullyQualifiedName = $"Concurrency.Extreme.Thread{threadId}.Test{i}"
                    },
                    TestName = $"Thread {threadId} Test {i}",
                    Outcome = TestOutcome.Passed,
                    Duration = TimeSpan.FromMilliseconds(_random.Next(5, 50)),
                    StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-25),
                    EndTimeUtc = DateTime.UtcNow
                };

                collector.RecordTest(execution);
            }

            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Contended flush: Multiple threads triggering flush simultaneously.
    /// Validates: Flush operation thread-safety, no race conditions during flush.
    /// </summary>
    [Benchmark]
    public async Task ContendedFlush_8Threads_FlushAfterEach10Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "concurrency-test-key",
            ProjectId = "concurrency-project",
            BatchSize = 50,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        var tasks = Enumerable.Range(0, 8).Select(async threadId =>
        {
            for (int i = 0; i < 50; i++)
            {
                var execution = new TestExecution
                {
                    ExecutionId = Guid.NewGuid(),
                    Identity = new TestIdentity 
                    { 
                        TestId = $"flush-thread{threadId}-test{i}",
                        FullyQualifiedName = $"Concurrency.Flush.Thread{threadId}.Test{i}"
                    },
                    TestName = $"Flush Thread {threadId} Test {i}",
                    Outcome = TestOutcome.Passed,
                    Duration = TimeSpan.FromMilliseconds(_random.Next(10, 80)),
                    StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-45),
                    EndTimeUtc = DateTime.UtcNow
                };

                collector.RecordTest(execution);

                // Trigger flush every 10 tests
                if (i % 10 == 9)
                {
                    await collector.FlushAsync().ConfigureAwait(false);
                }
            }

            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Mixed operations: Threads recording tests while others are flushing.
    /// Validates: Read/write concurrency, no blocking on flush operations.
    /// </summary>
    [Benchmark]
    public async Task MixedOperations_RecordAndFlush_12Threads()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "concurrency-test-key",
            ProjectId = "concurrency-project",
            BatchSize = 100,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        // 8 threads recording, 4 threads periodically flushing
        var recordTasks = Enumerable.Range(0, 8).Select(async threadId =>
        {
            for (int i = 0; i < 100; i++)
            {
                var execution = new TestExecution
                {
                    ExecutionId = Guid.NewGuid(),
                    Identity = new TestIdentity 
                    { 
                        TestId = $"record-thread{threadId}-test{i}",
                        FullyQualifiedName = $"Concurrency.Mixed.Record.Thread{threadId}.Test{i}"
                    },
                    TestName = $"Record Thread {threadId} Test {i}",
                    Outcome = TestOutcome.Passed,
                    Duration = TimeSpan.FromMilliseconds(_random.Next(10, 70)),
                    StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-40),
                    EndTimeUtc = DateTime.UtcNow
                };

                collector.RecordTest(execution);
                await Task.Yield();
            }
        }).ToArray();

        var flushTasks = Enumerable.Range(0, 4).Select(async flushId =>
        {
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(5).ConfigureAwait(false); // Stagger flushes
                await collector.FlushAsync().ConfigureAwait(false);
            }
        }).ToArray();

        await Task.WhenAll(recordTasks.Concat(flushTasks)).ConfigureAwait(false);
        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Rapid short bursts: Many threads with very short test sequences.
    /// Validates: Overhead of thread coordination, minimal contention impact.
    /// </summary>
    [Benchmark]
    public async Task RapidShortBursts_20Threads_20TestsEach()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "concurrency-test-key",
            ProjectId = "concurrency-project",
            BatchSize = 50,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        var tasks = Enumerable.Range(0, 20).Select(async threadId =>
        {
            for (int i = 0; i < 20; i++)
            {
                collector.RecordTest(new TestExecution
                {
                    ExecutionId = Guid.NewGuid(),
                    Identity = new TestIdentity 
                    { 
                        TestId = $"burst-thread{threadId}-test{i}",
                        FullyQualifiedName = $"Concurrency.Burst.Thread{threadId}.Test{i}"
                    },
                    TestName = $"Burst Thread {threadId} Test {i}",
                    Outcome = TestOutcome.Passed,
                    Duration = TimeSpan.FromMilliseconds(_random.Next(5, 40)),
                    StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-20),
                    EndTimeUtc = DateTime.UtcNow
                });
            }

            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        await collector.FlushAsync().ConfigureAwait(false);
    }
}
