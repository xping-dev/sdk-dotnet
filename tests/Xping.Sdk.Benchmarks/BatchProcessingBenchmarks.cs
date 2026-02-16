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
using Microsoft.Extensions.Options;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Collector.Internals;
using System;
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

    /// <summary>
    /// Small batch (10 tests): Optimized for fast feedback.
    /// Validates: Quick flush overhead, minimal batching.
    /// </summary>
    [Benchmark]
    public void SmallBatch_10Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 10,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        for (int i = 0; i < 10; i++)
        {
            collector.RecordTest(new TestExecutionBuilder()
                .WithTestName($"Small Batch Test {i}")
                .WithOutcome(TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(50))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-50))
                .WithEndTime(DateTime.UtcNow)
                .Build());
        }

        _ = collector.Drain();
    }

    /// <summary>
    /// Medium batch (50 tests): Balanced approach.
    /// Validates: Moderate batching efficiency, reasonable flush frequency.
    /// </summary>
    [Benchmark]
    public void MediumBatch_50Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 50,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        for (int i = 0; i < 50; i++)
        {
            var outcome = i % 10 == 0 ? TestOutcome.Failed : TestOutcome.Passed;
            collector.RecordTest(new TestExecutionBuilder()
                .WithTestName($"Medium Batch Test {i}")
                .WithOutcome(outcome)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(20, 100)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-50))
                .WithEndTime(DateTime.UtcNow)
                .Build());
        }

        _ = collector.Drain();
    }

    /// <summary>
    /// Large batch (100 tests): Optimized for throughput.
    /// Validates: Maximum batching efficiency, reduced flush overhead.
    /// </summary>
    [Benchmark]
    public void LargeBatch_100Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 100,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        for (int i = 0; i < 100; i++)
        {
            var outcome = i % 10 == 0 ? TestOutcome.Failed : TestOutcome.Passed;
            collector.RecordTest(new TestExecutionBuilder()
                .WithTestName($"Large Batch Test {i}")
                .WithOutcome(outcome)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(10, 150)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-50))
                .WithEndTime(DateTime.UtcNow)
                .Build());
        }

        _ = collector.Drain();
    }

    /// <summary>
    /// Extra-large batch (500 tests): Stress test for large test suites.
    /// Validates: Scalability, memory efficiency, no degradation at high volume.
    /// </summary>
    [Benchmark]
    public void ExtraLargeBatch_500Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 500,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        for (int i = 0; i < 500; i++)
        {
            var outcome = i % 20 == 0 ? TestOutcome.Failed : TestOutcome.Passed;
            var builder = new TestExecutionBuilder()
                .WithTestName($"XLarge Batch Test {i}")
                .WithOutcome(outcome)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(5, 100)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-50))
                .WithEndTime(DateTime.UtcNow);

            if (i % 20 == 0)
            {
                builder.WithException(null, $"Test {i} failed with assertion error", "   at Test.Method() in Test.cs:line 42");
            }

            collector.RecordTest(builder.Build());
        }

        _ = collector.Drain();
    }

    /// <summary>
    /// Multiple small batches: Simulate frequent flushes.
    /// Validates: Flush overhead, buffer reuse, GC pressure.
    /// </summary>
    [Benchmark]
    public void MultipleSmallBatches_5x10Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 10,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        // 5 batches of 10 tests each
        for (int batch = 0; batch < 5; batch++)
        {
            for (int i = 0; i < 10; i++)
            {
                collector.RecordTest(new TestExecutionBuilder()
                    .WithTestName($"Multi Batch {batch} Test {i}")
                    .WithOutcome(TestOutcome.Passed)
                    .WithDuration(TimeSpan.FromMilliseconds(30))
                    .WithStartTime(DateTime.UtcNow.AddMilliseconds(-30))
                    .WithEndTime(DateTime.UtcNow)
                    .Build());
            }

            _ = collector.Drain();
        }
    }

    /// <summary>
    /// Auto-flush behavior: Tests accumulated until batch size reached.
    /// Validates: Automatic batching, no manual flush needed.
    /// </summary>
    [Benchmark]
    public void AutoFlush_BatchSize50()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 25, // Will trigger BufferFull after 25 tests
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        // Record 50 tests
        for (int i = 0; i < 50; i++)
        {
            collector.RecordTest(new TestExecutionBuilder()
                .WithTestName($"Auto Flush Test {i}")
                .WithOutcome(TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(20))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-20))
                .WithEndTime(DateTime.UtcNow)
                .Build());
        }

        // Final drain for any remaining tests
        _ = collector.Drain();
    }

    /// <summary>
    /// Mixed size batches: Realistic scenario with variable test counts.
    /// Validates: Adaptability to varying workloads.
    /// </summary>
    [Benchmark]
    public void MixedSizeBatches_Variable()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 50,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        // Batch 1: 5 tests
        for (int i = 0; i < 5; i++)
        {
            collector.RecordTest(CreateTestExecution(i, "Batch1"));
        }
        _ = collector.Drain();

        // Batch 2: 15 tests
        for (int i = 0; i < 15; i++)
        {
            collector.RecordTest(CreateTestExecution(i, "Batch2"));
        }
        _ = collector.Drain();

        // Batch 3: 30 tests
        for (int i = 0; i < 30; i++)
        {
            collector.RecordTest(CreateTestExecution(i, "Batch3"));
        }
        _ = collector.Drain();
    }

    private TestExecution CreateTestExecution(int index, string batchName)
    {
        return new TestExecutionBuilder()
            .WithTestName($"{batchName} Test {index}")
            .WithOutcome(TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(_random.Next(10, 80)))
            .WithStartTime(DateTime.UtcNow.AddMilliseconds(-40))
            .WithEndTime(DateTime.UtcNow)
            .Build();
    }
}
