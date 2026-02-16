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
/// Stress and load testing benchmarks for sustained high-volume scenarios.
/// Tests system behavior under continuous load, memory stability, and resource management.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class StressTestBenchmarks
{
    private readonly Random _random = new();

    /// <summary>
    /// Sustained load: 1,000 tests continuously.
    /// Validates: No degradation over time, memory stability, consistent performance.
    /// </summary>
    [Benchmark]
    public void SustainedLoad_1000Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "stress-test-key",
            ProjectId = "stress-project",
            BatchSize = 100,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        for (int i = 0; i < 1000; i++)
        {
            var outcome = i % 10 == 0 ? TestOutcome.Failed : TestOutcome.Passed;
            var builder = new TestExecutionBuilder()
                .WithTestName($"Stress Test {i}")
                .WithOutcome(outcome)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(10, 200)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
                .WithEndTime(DateTime.UtcNow);

            if (outcome == TestOutcome.Failed)
            {
                builder.WithException(null, $"Test {i} failed under load", "   at Stress.Test.Method() in StressTest.cs:line 42");
            }

            collector.RecordTest(builder.Build());

            // Trigger periodic drains
            if (i % 100 == 99)
            {
                _ = collector.Drain();
            }
        }

        _ = collector.Drain();
    }

    /// <summary>
    /// High-volume load: 5,000 tests.
    /// Validates: Scalability to large test suites, memory efficiency at scale.
    /// </summary>
    [Benchmark]
    public void HighVolumeLoad_5000Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "stress-test-key",
            ProjectId = "stress-project",
            BatchSize = 500,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        for (int i = 0; i < 5000; i++)
        {
            collector.RecordTest(new TestExecutionBuilder()
                .WithTestName($"High Volume Test {i}")
                .WithOutcome(i % 20 == 0 ? TestOutcome.Failed : TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(5, 150)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-75))
                .WithEndTime(DateTime.UtcNow)
                .Build());

            // Periodic drains
            if (i % 500 == 499)
            {
                _ = collector.Drain();
            }
        }

        _ = collector.Drain();
    }

    /// <summary>
    /// Burst load: Rapid fire 2,000 tests with minimal delay.
    /// Validates: Peak throughput handling, buffer management under pressure.
    /// </summary>
    [Benchmark]
    public void BurstLoad_2000Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "stress-test-key",
            ProjectId = "stress-project",
            BatchSize = 200,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        // Rapid-fire recording (no artificial delays)
        for (int i = 0; i < 2000; i++)
        {
            collector.RecordTest(new TestExecutionBuilder()
                .WithTestName($"Burst Test {i}")
                .WithOutcome(TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(5, 50)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-25))
                .WithEndTime(DateTime.UtcNow)
                .Build());
        }

        _ = collector.Drain();
    }

    /// <summary>
    /// Mixed workload: Combination of fast and slow tests with varying complexity.
    /// Validates: Real-world scenario handling, no bottlenecks with diverse test patterns.
    /// </summary>
    [Benchmark]
    public void MixedWorkload_1500Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "stress-test-key",
            ProjectId = "stress-project",
            BatchSize = 150,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        for (int i = 0; i < 1500; i++)
        {
            var isComplexTest = i % 5 == 0;
            var outcome = i % 15 == 0 ? TestOutcome.Failed : TestOutcome.Passed;

            var identity = new TestIdentityBuilder()
                .WithTestId($"mixed-{i}")
                .WithFullyQualifiedName($"Stress.Mixed.{(isComplexTest ? "Complex" : "Simple")}.Test{i}")
                .WithClassName(isComplexTest ? "ComplexTestClass" : "SimpleTestClass")
                .WithMethodName($"TestMethod{i}")
                .Build();

            var builder = new TestExecutionBuilder()
                .WithTestName($"Mixed Test {i}")
                .WithOutcome(outcome)
                .WithDuration(TimeSpan.FromMilliseconds(isComplexTest ? _random.Next(100, 500) : _random.Next(5, 50)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
                .WithEndTime(DateTime.UtcNow)
                .WithIdentity(identity);

            if (isComplexTest)
            {
                builder.WithMetadata(new TestMetadataBuilder()
                    .AddCategory("Integration")
                    .AddCategory("Slow")
                    .AddCustomAttribute("Priority", "High")
                    .Build());
            }

            if (outcome == TestOutcome.Failed)
            {
                builder.WithException(null, "Complex failure scenario",
                    @"   at Complex.Method.Call() in Complex.cs:line 123
   at Integration.Test.Execute() in Test.cs:line 456");
            }

            collector.RecordTest(builder.Build());

            if (i % 150 == 149)
            {
                _ = collector.Drain();
            }
        }

        _ = collector.Drain();
    }

    /// <summary>
    /// Memory stability: Repeated cycles to detect memory leaks.
    /// Validates: No memory accumulation, proper disposal, GC behavior.
    /// </summary>
    [Benchmark]
    public void MemoryStability_10Cycles_100TestsEach()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "stress-test-key",
            ProjectId = "stress-project",
            BatchSize = 100,
            FlushInterval = TimeSpan.Zero
        };

        // 10 cycles of 100 tests each
        for (int cycle = 0; cycle < 10; cycle++)
        {
            using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

            for (int i = 0; i < 100; i++)
            {
                collector.RecordTest(new TestExecutionBuilder()
                    .WithTestName($"Memory Test C{cycle} T{i}")
                    .WithOutcome(TestOutcome.Passed)
                    .WithDuration(TimeSpan.FromMilliseconds(_random.Next(10, 100)))
                    .WithStartTime(DateTime.UtcNow.AddMilliseconds(-50))
                    .WithEndTime(DateTime.UtcNow)
                    .Build());
            }

            _ = collector.Drain();
            // Collector disposed at end of using block
        }
    }

    /// <summary>
    /// Continuous pressure: Back-to-back test recording without pauses.
    /// Validates: Sustained throughput, no performance degradation over time.
    /// </summary>
    [Benchmark]
    public void ContinuousPressure_3000Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "stress-test-key",
            ProjectId = "stress-project",
            BatchSize = 300,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        for (int i = 0; i < 3000; i++)
        {
            collector.RecordTest(new TestExecutionBuilder()
                .WithTestName($"Continuous Test {i}")
                .WithOutcome(TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(10, 80)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-45))
                .WithEndTime(DateTime.UtcNow)
                .Build());
        }

        _ = collector.Drain();
    }
}
