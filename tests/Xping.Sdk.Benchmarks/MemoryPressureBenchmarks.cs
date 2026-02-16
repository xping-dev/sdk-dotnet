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
/// Memory pressure and leak detection benchmarks.
/// Tests memory allocation patterns, GC behavior, and long-term stability.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class MemoryPressureBenchmarks
{
    private readonly Random _random = new();

    /// <summary>
    /// Large batch: 10,000 tests in a single session.
    /// Validates: Memory efficiency at scale, no excessive allocations.
    /// </summary>
    [Benchmark]
    public void LargeBatch_10000Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "memory-test-key",
            ProjectId = "memory-project",
            BatchSize = 1000,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        for (int i = 0; i < 10000; i++)
        {
            collector.RecordTest(new TestExecutionBuilder()
                .WithTestName($"Large Test {i}")
                .WithOutcome(TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(5, 100)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-50))
                .WithEndTime(DateTime.UtcNow)
                .Build());

            // Periodic drain to simulate realistic usage
            if (i % 1000 == 999)
            {
                _ = collector.Drain();
            }
        }

        _ = collector.Drain();
    }

    /// <summary>
    /// Very large batch: 25,000 tests to stress memory management.
    /// Validates: Scalability to very large test suites, memory growth patterns.
    /// </summary>
    [Benchmark]
    public void VeryLargeBatch_25000Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "memory-test-key",
            ProjectId = "memory-project",
            BatchSize = 2500,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        for (int i = 0; i < 25000; i++)
        {
            collector.RecordTest(new TestExecutionBuilder()
                .WithTestName($"XLarge Test {i}")
                .WithOutcome(TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(5, 80)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-40))
                .WithEndTime(DateTime.UtcNow)
                .Build());

            if (i % 2500 == 2499)
            {
                _ = collector.Drain();
            }
        }

        _ = collector.Drain();
    }

    /// <summary>
    /// Complex objects: Tests with large stack traces and metadata.
    /// Validates: Memory handling of complex test data, string allocations.
    /// </summary>
    [Benchmark]
    public void ComplexObjects_1000TestsWithLargeData()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "memory-test-key",
            ProjectId = "memory-project",
            BatchSize = 100,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        for (int i = 0; i < 1000; i++)
        {
            var outcome = i % 5 == 0 ? TestOutcome.Failed : TestOutcome.Passed;

            var metadataBuilder = new TestMetadataBuilder()
                .AddCategories(new[] { "Integration", "Slow", "Complex", $"Category{i % 10}" })
                .AddTags(new[] { $"Tag{i % 20}", $"Tag{i % 15}", $"Tag{i % 5}" })
                .WithDescription($"Detailed test description for test {i} with multiple lines of context and documentation.");

            for (int j = 0; j < 5; j++)
            {
                metadataBuilder.AddCustomAttribute($"Attribute{j}", $"Value{j}_{i}");
            }

            var identity = new TestIdentityBuilder()
                .WithTestId($"complex-{i}")
                .WithFullyQualifiedName($"Memory.Complex.LongNamespace.SubNamespace.TestClass.Method{i}")
                .WithClassName($"ComplexTestClass_{i}")
                .WithMethodName($"ComplexTestMethod_{i}")
                .WithDisplayName($"Complex Test with Long Display Name {i}")
                .Build();

            var builder = new TestExecutionBuilder()
                .WithTestName($"Complex Test {i}")
                .WithOutcome(outcome)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(50, 300)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-200))
                .WithEndTime(DateTime.UtcNow)
                .WithIdentity(identity)
                .WithMetadata(metadataBuilder.Build());

            if (outcome == TestOutcome.Failed)
            {
                builder.WithException(null,
                    $"Complex failure scenario with detailed error message for test {i}. This includes multiple lines of error context and diagnostic information.",
                    @"   at ComplexNamespace.ComplexClass.ComplexMethod(String parameter1, Int32 parameter2) in ComplexFile.cs:line 123
   at ComplexNamespace.AnotherClass.AnotherMethod() in AnotherFile.cs:line 456
   at ComplexNamespace.TestRunner.Execute() in TestRunner.cs:line 789
   at ComplexNamespace.TestFramework.Run() in Framework.cs:line 1011");
            }

            collector.RecordTest(builder.Build());

            if (i % 100 == 99)
            {
                _ = collector.Drain();
            }
        }

        _ = collector.Drain();
    }

    /// <summary>
    /// Repeated cycles: Many create/dispose cycles to detect memory leaks.
    /// Validates: Proper cleanup, no memory accumulation over time.
    /// </summary>
    [Benchmark]
    public void RepeatedCycles_50Cycles_200TestsEach()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "memory-test-key",
            ProjectId = "memory-project",
            BatchSize = 200,
            FlushInterval = TimeSpan.Zero
        };

        for (int cycle = 0; cycle < 50; cycle++)
        {
            using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

            for (int i = 0; i < 200; i++)
            {
                collector.RecordTest(new TestExecutionBuilder()
                    .WithTestName($"Cycle {cycle} Test {i}")
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
    /// Allocation stress: Rapid object creation to stress GC.
    /// Validates: GC efficiency, allocation rate handling.
    /// </summary>
    [Benchmark]
    public void AllocationStress_5000RapidTests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "memory-test-key",
            ProjectId = "memory-project",
            BatchSize = 500,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        for (int i = 0; i < 5000; i++)
        {
            // Rapid allocation with no artificial delays
            collector.RecordTest(new TestExecutionBuilder()
                .WithTestName($"Allocation Test {i}")
                .WithOutcome(TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(5, 50)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-25))
                .WithEndTime(DateTime.UtcNow)
                .Build());
        }

        _ = collector.Drain();
    }

    /// <summary>
    /// String-heavy workload: Tests with extensive string data.
    /// Validates: String interning, large string handling, LOH allocations.
    /// </summary>
    [Benchmark]
    public void StringHeavyWorkload_2000Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "memory-test-key",
            ProjectId = "memory-project",
            BatchSize = 200,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        for (int i = 0; i < 2000; i++)
        {
            var longString = new string('X', 1000); // 1KB string
            var outcome = i % 10 == 0 ? TestOutcome.Failed : TestOutcome.Passed;

            var identity = new TestIdentityBuilder()
                .WithTestId($"string-{i}")
                .WithFullyQualifiedName($"Memory.Strings.Test{i}.{longString.Substring(0, 50)}")
                .Build();

            var builder = new TestExecutionBuilder()
                .WithTestName($"String Test {i} {longString.Substring(0, 100)}")
                .WithOutcome(outcome)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(10, 150)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-75))
                .WithEndTime(DateTime.UtcNow)
                .WithIdentity(identity);

            if (outcome == TestOutcome.Failed)
            {
                builder.WithException(null, $"Error: {longString}", $"Stack trace with context: {longString}\n   at Test.Method() in File.cs:line {i}");
            }

            collector.RecordTest(builder.Build());

            if (i % 200 == 199)
            {
                _ = collector.Drain();
            }
        }

        _ = collector.Drain();
    }

    /// <summary>
    /// Concurrent memory pressure: Multiple threads with heavy allocations.
    /// Validates: Concurrent GC handling, memory pressure under parallelism.
    /// </summary>
    [Benchmark]
    public async Task ConcurrentMemoryPressure_8Threads_500TestsEach()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "memory-test-key",
            ProjectId = "memory-project",
            BatchSize = 500,
            FlushInterval = TimeSpan.Zero
        };

        using ITestExecutionCollector collector = new TestExecutionCollector(Options.Create(config));

        var tasks = Enumerable.Range(0, 8).Select(async threadId =>
        {
            for (int i = 0; i < 500; i++)
            {
                var outcome = i % 20 == 0 ? TestOutcome.Failed : TestOutcome.Passed;
                var metadataBuilder = new TestMetadataBuilder()
                    .AddCategory($"Thread{threadId}")
                    .AddCategory("Concurrent");

                var builder = new TestExecutionBuilder()
                    .WithTestName($"Memory Thread {threadId} Test {i}")
                    .WithOutcome(outcome)
                    .WithDuration(TimeSpan.FromMilliseconds(_random.Next(10, 120)))
                    .WithStartTime(DateTime.UtcNow.AddMilliseconds(-60))
                    .WithEndTime(DateTime.UtcNow)
                    .WithMetadata(metadataBuilder.Build());

                if (outcome == TestOutcome.Failed)
                {
                    builder.WithException(null,
                        $"Thread {threadId} test {i} failure with context data",
                        $"   at Thread{threadId}.Method() in File{threadId}.cs:line {i}");
                }

                collector.RecordTest(builder.Build());
            }

            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _ = collector.Drain();
    }
}
