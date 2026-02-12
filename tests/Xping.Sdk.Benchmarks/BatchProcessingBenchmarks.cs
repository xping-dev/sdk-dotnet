/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Upload;

#pragma warning disable CA1515 // Consider making public types internal
#pragma warning disable CA5394 // Do not use insecure randomness
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
#pragma warning disable CA1024 // Use properties where appropriate
#pragma warning disable IDE0055 // Fix formatting

namespace Xping.Sdk.Benchmarks;

using BenchmarkDotNet.Attributes;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Benchmarks for batch processing and flush behavior.
/// Tests different batch sizes and flush intervals for optimal configuration.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class BatchProcessingBenchmarks
{
    private readonly Random _random = new();

    private sealed class NoOpUploader : IXpingUploader
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
    /// Small batch (10 tests): Optimized for fast feedback.
    /// Validates: Quick flush overhead, minimal batching.
    /// </summary>
    [Benchmark]
    public async Task SmallBatch_10Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 10,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        for (int i = 0; i < 10; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity { TestId = $"batch-small-{i}", FullyQualifiedName = $"Batch.Small.Test{i}" },
                TestName = $"Small Batch Test {i}",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(50),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-50),
                EndTimeUtc = DateTime.UtcNow
            };

            collector.RecordTest(execution);
        }

        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Medium batch (50 tests): Balanced approach.
    /// Validates: Moderate batching efficiency, reasonable flush frequency.
    /// </summary>
    [Benchmark]
    public async Task MediumBatch_50Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 50,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        for (int i = 0; i < 50; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity { TestId = $"batch-medium-{i}", FullyQualifiedName = $"Batch.Medium.Test{i}" },
                TestName = $"Medium Batch Test {i}",
                Outcome = i % 10 == 0 ? TestOutcome.Failed : TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(20, 100)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-50),
                EndTimeUtc = DateTime.UtcNow
            };

            collector.RecordTest(execution);
        }

        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Large batch (100 tests): Optimized for throughput.
    /// Validates: Maximum batching efficiency, reduced flush overhead.
    /// </summary>
    [Benchmark]
    public async Task LargeBatch_100Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 100,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        for (int i = 0; i < 100; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity { TestId = $"batch-large-{i}", FullyQualifiedName = $"Batch.Large.Test{i}" },
                TestName = $"Large Batch Test {i}",
                Outcome = i % 10 == 0 ? TestOutcome.Failed : TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(10, 150)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-50),
                EndTimeUtc = DateTime.UtcNow
            };

            collector.RecordTest(execution);
        }

        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Extra-large batch (500 tests): Stress test for large test suites.
    /// Validates: Scalability, memory efficiency, no degradation at high volume.
    /// </summary>
    [Benchmark]
    public async Task ExtraLargeBatch_500Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 500,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        for (int i = 0; i < 500; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity { TestId = $"batch-xlarge-{i}", FullyQualifiedName = $"Batch.XLarge.Test{i}" },
                TestName = $"XLarge Batch Test {i}",
                Outcome = i % 20 == 0 ? TestOutcome.Failed : TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(5, 100)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-50),
                EndTimeUtc = DateTime.UtcNow
            };

            if (i % 20 == 0)
            {
                execution.ErrorMessage = $"Test {i} failed with assertion error";
                execution.StackTrace = "   at Test.Method() in Test.cs:line 42";
            }

            collector.RecordTest(execution);
        }

        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Multiple small batches: Simulate frequent flushes.
    /// Validates: Flush overhead, buffer reuse, GC pressure.
    /// </summary>
    [Benchmark]
    public async Task MultipleSmallBatches_5x10Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 10,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        // 5 batches of 10 tests each
        for (int batch = 0; batch < 5; batch++)
        {
            for (int i = 0; i < 10; i++)
            {
                var execution = new TestExecution
                {
                    ExecutionId = Guid.NewGuid(),
                    Identity = new TestIdentity { TestId = $"batch-multi-{batch}-{i}", FullyQualifiedName = $"Batch.Multi.Test{batch}_{i}" },
                    TestName = $"Multi Batch {batch} Test {i}",
                    Outcome = TestOutcome.Passed,
                    Duration = TimeSpan.FromMilliseconds(30),
                    StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-30),
                    EndTimeUtc = DateTime.UtcNow
                };

                collector.RecordTest(execution);
            }

            await collector.FlushAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Auto-flush behavior: Tests accumulated until batch size reached.
    /// Validates: Automatic batching, no manual flush needed.
    /// </summary>
    [Benchmark]
    public async Task AutoFlush_BatchSize50()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 25, // Will auto-flush after 25 tests
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        // Record 50 tests - should trigger 2 automatic flushes
        for (int i = 0; i < 50; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity { TestId = $"batch-auto-{i}", FullyQualifiedName = $"Batch.Auto.Test{i}" },
                TestName = $"Auto Flush Test {i}",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(20),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-20),
                EndTimeUtc = DateTime.UtcNow
            };

            collector.RecordTest(execution);
        }

        // Final flush for any remaining tests
        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Mixed size batches: Realistic scenario with variable test counts.
    /// Validates: Adaptability to varying workloads.
    /// </summary>
    [Benchmark]
    public async Task MixedSizeBatches_Variable()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 50,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        // Batch 1: 5 tests
        for (int i = 0; i < 5; i++)
        {
            collector.RecordTest(CreateTestExecution(i, "Batch1"));
        }
        await collector.FlushAsync().ConfigureAwait(false);

        // Batch 2: 15 tests
        for (int i = 0; i < 15; i++)
        {
            collector.RecordTest(CreateTestExecution(i, "Batch2"));
        }
        await collector.FlushAsync().ConfigureAwait(false);

        // Batch 3: 30 tests
        for (int i = 0; i < 30; i++)
        {
            collector.RecordTest(CreateTestExecution(i, "Batch3"));
        }
        await collector.FlushAsync().ConfigureAwait(false);
    }

    private TestExecution CreateTestExecution(int index, string batchName)
    {
        return new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = $"mixed-{batchName}-{index}", 
                FullyQualifiedName = $"Batch.Mixed.{batchName}.Test{index}" 
            },
            TestName = $"{batchName} Test {index}",
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(_random.Next(10, 80)),
            StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-40),
            EndTimeUtc = DateTime.UtcNow
        };
    }
}
