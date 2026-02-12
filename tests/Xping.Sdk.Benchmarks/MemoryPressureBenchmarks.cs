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
/// Memory pressure and leak detection benchmarks.
/// Tests memory allocation patterns, GC behavior, and long-term stability.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class MemoryPressureBenchmarks
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
    /// Large batch: 10,000 tests in a single session.
    /// Validates: Memory efficiency at scale, no excessive allocations.
    /// </summary>
    [Benchmark]
    public async Task LargeBatch_10000Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "memory-test-key",
            ProjectId = "memory-project",
            BatchSize = 1000,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        for (int i = 0; i < 10000; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"large-{i}",
                    FullyQualifiedName = $"Memory.Large.Test{i}"
                },
                TestName = $"Large Test {i}",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(5, 100)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-50),
                EndTimeUtc = DateTime.UtcNow
            };

            collector.RecordTest(execution);

            // Periodic flush to simulate realistic usage
            if (i % 1000 == 999)
            {
                await collector.FlushAsync().ConfigureAwait(false);
            }
        }

        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Very large batch: 25,000 tests to stress memory management.
    /// Validates: Scalability to very large test suites, memory growth patterns.
    /// </summary>
    [Benchmark]
    public async Task VeryLargeBatch_25000Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "memory-test-key",
            ProjectId = "memory-project",
            BatchSize = 2500,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        for (int i = 0; i < 25000; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"xlarge-{i}",
                    FullyQualifiedName = $"Memory.XLarge.Test{i}"
                },
                TestName = $"XLarge Test {i}",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(5, 80)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-40),
                EndTimeUtc = DateTime.UtcNow
            };

            collector.RecordTest(execution);

            if (i % 2500 == 2499)
            {
                await collector.FlushAsync().ConfigureAwait(false);
            }
        }

        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Complex objects: Tests with large stack traces and metadata.
    /// Validates: Memory handling of complex test data, string allocations.
    /// </summary>
    [Benchmark]
    public async Task ComplexObjects_1000TestsWithLargeData()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "memory-test-key",
            ProjectId = "memory-project",
            BatchSize = 100,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        for (int i = 0; i < 1000; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"complex-{i}",
                    FullyQualifiedName = $"Memory.Complex.LongNamespace.SubNamespace.TestClass.Method{i}",
                    ClassName = $"ComplexTestClass_{i}",
                    MethodName = $"ComplexTestMethod_{i}",
                    DisplayName = $"Complex Test with Long Display Name {i}"
                },
                TestName = $"Complex Test {i}",
                Outcome = i % 5 == 0 ? TestOutcome.Failed : TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(50, 300)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-200),
                EndTimeUtc = DateTime.UtcNow,
                ErrorMessage = i % 5 == 0 ? $"Complex failure scenario with detailed error message for test {i}. This includes multiple lines of error context and diagnostic information." : null,
                StackTrace = i % 5 == 0 ? @"   at ComplexNamespace.ComplexClass.ComplexMethod(String parameter1, Int32 parameter2) in ComplexFile.cs:line 123
   at ComplexNamespace.AnotherClass.AnotherMethod() in AnotherFile.cs:line 456
   at ComplexNamespace.TestRunner.Execute() in TestRunner.cs:line 789
   at ComplexNamespace.TestFramework.Run() in Framework.cs:line 1011" : null,
                Metadata = new TestMetadata
                {
                    Categories = new[] { "Integration", "Slow", "Complex", $"Category{i % 10}" },
                    Tags = new[] { $"Tag{i % 20}", $"Tag{i % 15}", $"Tag{i % 5}" },
                    Description = $"Detailed test description for test {i} with multiple lines of context and documentation."
                }
            };

            // Add custom attributes
            for (int j = 0; j < 5; j++)
            {
                execution.Metadata.CustomAttributes[$"Attribute{j}"] = $"Value{j}_{i}";
            }

            collector.RecordTest(execution);

            if (i % 100 == 99)
            {
                await collector.FlushAsync().ConfigureAwait(false);
            }
        }

        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Repeated cycles: Many create/dispose cycles to detect memory leaks.
    /// Validates: Proper cleanup, no memory accumulation over time.
    /// </summary>
    [Benchmark]
    public async Task RepeatedCycles_50Cycles_200TestsEach()
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
            await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

            for (int i = 0; i < 200; i++)
            {
                var execution = new TestExecution
                {
                    ExecutionId = Guid.NewGuid(),
                    Identity = new TestIdentity 
                    { 
                        TestId = $"cycle{cycle}-test{i}",
                        FullyQualifiedName = $"Memory.Cycles.Cycle{cycle}.Test{i}"
                    },
                    TestName = $"Cycle {cycle} Test {i}",
                    Outcome = TestOutcome.Passed,
                    Duration = TimeSpan.FromMilliseconds(_random.Next(10, 100)),
                    StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-50),
                    EndTimeUtc = DateTime.UtcNow
                };

                collector.RecordTest(execution);
            }

            await collector.FlushAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Allocation stress: Rapid object creation to stress GC.
    /// Validates: GC efficiency, allocation rate handling.
    /// </summary>
    [Benchmark]
    public async Task AllocationStress_5000RapidTests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "memory-test-key",
            ProjectId = "memory-project",
            BatchSize = 500,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        for (int i = 0; i < 5000; i++)
        {
            // Rapid allocation with no artificial delays
            collector.RecordTest(new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"alloc-{i}",
                    FullyQualifiedName = $"Memory.Allocation.Test{i}"
                },
                TestName = $"Allocation Test {i}",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(5, 50)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-25),
                EndTimeUtc = DateTime.UtcNow
            });
        }

        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// String-heavy workload: Tests with extensive string data.
    /// Validates: String interning, large string handling, LOH allocations.
    /// </summary>
    [Benchmark]
    public async Task StringHeavyWorkload_2000Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "memory-test-key",
            ProjectId = "memory-project",
            BatchSize = 200,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        for (int i = 0; i < 2000; i++)
        {
            var longString = new string('X', 1000); // 1KB string
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"string-{i}",
                    FullyQualifiedName = $"Memory.Strings.Test{i}.{longString.Substring(0, 50)}"
                },
                TestName = $"String Test {i} {longString.Substring(0, 100)}",
                Outcome = i % 10 == 0 ? TestOutcome.Failed : TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(10, 150)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-75),
                EndTimeUtc = DateTime.UtcNow,
                ErrorMessage = i % 10 == 0 ? $"Error: {longString}" : null,
                StackTrace = i % 10 == 0 ? $"Stack trace with context: {longString}\n   at Test.Method() in File.cs:line {i}" : null
            };

            collector.RecordTest(execution);

            if (i % 200 == 199)
            {
                await collector.FlushAsync().ConfigureAwait(false);
            }
        }

        await collector.FlushAsync().ConfigureAwait(false);
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

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        var tasks = Enumerable.Range(0, 8).Select(async threadId =>
        {
            for (int i = 0; i < 500; i++)
            {
                var execution = new TestExecution
                {
                    ExecutionId = Guid.NewGuid(),
                    Identity = new TestIdentity 
                    { 
                        TestId = $"mem-thread{threadId}-test{i}",
                        FullyQualifiedName = $"Memory.Concurrent.Thread{threadId}.Test{i}"
                    },
                    TestName = $"Memory Thread {threadId} Test {i}",
                    Outcome = i % 20 == 0 ? TestOutcome.Failed : TestOutcome.Passed,
                    Duration = TimeSpan.FromMilliseconds(_random.Next(10, 120)),
                    StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-60),
                    EndTimeUtc = DateTime.UtcNow,
                    Metadata = new TestMetadata
                    {
                        Categories = new[] { $"Thread{threadId}", "Concurrent" }
                    }
                };

                if (i % 20 == 0)
                {
                    execution.ErrorMessage = $"Thread {threadId} test {i} failure with context data";
                    execution.StackTrace = $"   at Thread{threadId}.Method() in File{threadId}.cs:line {i}";
                }

                collector.RecordTest(execution);
            }

            await Task.Yield();
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        await collector.FlushAsync().ConfigureAwait(false);
    }
}
