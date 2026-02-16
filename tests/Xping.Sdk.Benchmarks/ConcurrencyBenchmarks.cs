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

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        var tasks = Enumerable.Range(0, 4).Select(async threadId =>
        {
            for (int i = 0; i < 100; i++)
            {
                collector.RecordTest(new TestExecutionBuilder()
                    .WithTestName($"Thread {threadId} Test {i}")
                    .WithOutcome(TestOutcome.Passed)
                    .WithDuration(TimeSpan.FromMilliseconds(_random.Next(10, 100)))
                    .WithStartTime(DateTime.UtcNow.AddMilliseconds(-50))
                    .WithEndTime(DateTime.UtcNow)
                    .Build());
            }

            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _ = collector.Drain();
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

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        var tasks = Enumerable.Range(0, 8).Select(async threadId =>
        {
            for (int i = 0; i < 100; i++)
            {
                var outcome = i % 10 == 0 ? TestOutcome.Failed : TestOutcome.Passed;
                var builder = new TestExecutionBuilder()
                    .WithTestName($"Thread {threadId} Test {i}")
                    .WithOutcome(outcome)
                    .WithDuration(TimeSpan.FromMilliseconds(_random.Next(5, 80)))
                    .WithStartTime(DateTime.UtcNow.AddMilliseconds(-40))
                    .WithEndTime(DateTime.UtcNow);

                if (i % 10 == 0)
                {
                    builder.WithException(null, $"Thread {threadId} test {i} failed", null);
                }

                collector.RecordTest(builder.Build());
            }

            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _ = collector.Drain();
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

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        var tasks = Enumerable.Range(0, 16).Select(async threadId =>
        {
            for (int i = 0; i < 100; i++)
            {
                collector.RecordTest(new TestExecutionBuilder()
                    .WithTestName($"Thread {threadId} Test {i}")
                    .WithOutcome(TestOutcome.Passed)
                    .WithDuration(TimeSpan.FromMilliseconds(_random.Next(5, 60)))
                    .WithStartTime(DateTime.UtcNow.AddMilliseconds(-30))
                    .WithEndTime(DateTime.UtcNow)
                    .Build());
            }

            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _ = collector.Drain();
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

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        var tasks = Enumerable.Range(0, 32).Select(async threadId =>
        {
            for (int i = 0; i < 50; i++)
            {
                collector.RecordTest(new TestExecutionBuilder()
                    .WithTestName($"Thread {threadId} Test {i}")
                    .WithOutcome(TestOutcome.Passed)
                    .WithDuration(TimeSpan.FromMilliseconds(_random.Next(5, 50)))
                    .WithStartTime(DateTime.UtcNow.AddMilliseconds(-25))
                    .WithEndTime(DateTime.UtcNow)
                    .Build());
            }

            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _ = collector.Drain();
    }

    /// <summary>
    /// Contended drain: Multiple threads triggering drain simultaneously.
    /// Validates: Drain operation thread-safety, no race conditions.
    /// </summary>
    [Benchmark]
    public async Task ContendedDrain_8Threads_DrainAfterEach10Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "concurrency-test-key",
            ProjectId = "concurrency-project",
            BatchSize = 50,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        var tasks = Enumerable.Range(0, 8).Select(async threadId =>
        {
            for (int i = 0; i < 50; i++)
            {
                collector.RecordTest(new TestExecutionBuilder()
                    .WithTestName($"Flush Thread {threadId} Test {i}")
                    .WithOutcome(TestOutcome.Passed)
                    .WithDuration(TimeSpan.FromMilliseconds(_random.Next(10, 80)))
                    .WithStartTime(DateTime.UtcNow.AddMilliseconds(-45))
                    .WithEndTime(DateTime.UtcNow)
                    .Build());

                // Trigger drain every 10 tests
                if (i % 10 == 9)
                {
                    _ = collector.Drain();
                }
            }

            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _ = collector.Drain();
    }

    /// <summary>
    /// Mixed operations: Threads recording tests while others are draining.
    /// Validates: Read/write concurrency, no blocking on drain operations.
    /// </summary>
    [Benchmark]
    public async Task MixedOperations_RecordAndDrain_12Threads()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "concurrency-test-key",
            ProjectId = "concurrency-project",
            BatchSize = 100,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        // 8 threads recording, 4 threads periodically draining
        var recordTasks = Enumerable.Range(0, 8).Select(async threadId =>
        {
            for (int i = 0; i < 100; i++)
            {
                collector.RecordTest(new TestExecutionBuilder()
                    .WithTestName($"Record Thread {threadId} Test {i}")
                    .WithOutcome(TestOutcome.Passed)
                    .WithDuration(TimeSpan.FromMilliseconds(_random.Next(10, 70)))
                    .WithStartTime(DateTime.UtcNow.AddMilliseconds(-40))
                    .WithEndTime(DateTime.UtcNow)
                    .Build());

                await Task.Yield();
            }
        }).ToArray();

        var drainTasks = Enumerable.Range(0, 4).Select(async drainId =>
        {
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(5).ConfigureAwait(false); // Stagger drains
                _ = collector.Drain();
            }
        }).ToArray();

        await Task.WhenAll(recordTasks.Concat(drainTasks)).ConfigureAwait(false);
        _ = collector.Drain();
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

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        var tasks = Enumerable.Range(0, 20).Select(async threadId =>
        {
            for (int i = 0; i < 20; i++)
            {
                collector.RecordTest(new TestExecutionBuilder()
                    .WithTestName($"Burst Thread {threadId} Test {i}")
                    .WithOutcome(TestOutcome.Passed)
                    .WithDuration(TimeSpan.FromMilliseconds(_random.Next(5, 40)))
                    .WithStartTime(DateTime.UtcNow.AddMilliseconds(-20))
                    .WithEndTime(DateTime.UtcNow)
                    .Build());
            }

            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _ = collector.Drain();
    }
}
