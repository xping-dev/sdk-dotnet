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
/// Stress and load testing benchmarks for sustained high-volume scenarios.
/// Tests system behavior under continuous load, memory stability, and resource management.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class StressTestBenchmarks
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
    /// Sustained load: 1,000 tests continuously.
    /// Validates: No degradation over time, memory stability, consistent performance.
    /// </summary>
    [Benchmark]
    public async Task SustainedLoad_1000Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "stress-test-key",
            ProjectId = "stress-project",
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
                    TestId = $"stress-1k-{i}",
                    FullyQualifiedName = $"Stress.Load.Test{i}"
                },
                TestName = $"Stress Test {i}",
                Outcome = i % 10 == 0 ? TestOutcome.Failed : TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(10, 200)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-100),
                EndTimeUtc = DateTime.UtcNow
            };

            if (i % 10 == 0)
            {
                execution.ErrorMessage = $"Test {i} failed under load";
                execution.StackTrace = "   at Stress.Test.Method() in StressTest.cs:line 42";
            }

            collector.RecordTest(execution);

            // Trigger periodic flushes
            if (i % 100 == 99)
            {
                await collector.FlushAsync().ConfigureAwait(false);
            }
        }

        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// High-volume load: 5,000 tests.
    /// Validates: Scalability to large test suites, memory efficiency at scale.
    /// </summary>
    [Benchmark]
    public async Task HighVolumeLoad_5000Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "stress-test-key",
            ProjectId = "stress-project",
            BatchSize = 500,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        for (int i = 0; i < 5000; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"stress-5k-{i}",
                    FullyQualifiedName = $"Stress.HighVolume.Test{i}"
                },
                TestName = $"High Volume Test {i}",
                Outcome = i % 20 == 0 ? TestOutcome.Failed : TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(5, 150)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-75),
                EndTimeUtc = DateTime.UtcNow
            };

            collector.RecordTest(execution);

            // Periodic flushes
            if (i % 500 == 499)
            {
                await collector.FlushAsync().ConfigureAwait(false);
            }
        }

        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Burst load: Rapid fire 2,000 tests with minimal delay.
    /// Validates: Peak throughput handling, buffer management under pressure.
    /// </summary>
    [Benchmark]
    public async Task BurstLoad_2000Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "stress-test-key",
            ProjectId = "stress-project",
            BatchSize = 200,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        // Rapid-fire recording (no artificial delays)
        for (int i = 0; i < 2000; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"burst-{i}",
                    FullyQualifiedName = $"Stress.Burst.Test{i}"
                },
                TestName = $"Burst Test {i}",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(5, 50)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-25),
                EndTimeUtc = DateTime.UtcNow
            };

            collector.RecordTest(execution);
        }

        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Mixed workload: Combination of fast and slow tests with varying complexity.
    /// Validates: Real-world scenario handling, no bottlenecks with diverse test patterns.
    /// </summary>
    [Benchmark]
    public async Task MixedWorkload_1500Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "stress-test-key",
            ProjectId = "stress-project",
            BatchSize = 150,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        for (int i = 0; i < 1500; i++)
        {
            var isComplexTest = i % 5 == 0;
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"mixed-{i}",
                    FullyQualifiedName = $"Stress.Mixed.{(isComplexTest ? "Complex" : "Simple")}.Test{i}",
                    ClassName = isComplexTest ? "ComplexTestClass" : "SimpleTestClass",
                    MethodName = $"TestMethod{i}"
                },
                TestName = $"Mixed Test {i}",
                Outcome = i % 15 == 0 ? TestOutcome.Failed : TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(isComplexTest ? _random.Next(100, 500) : _random.Next(5, 50)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-100),
                EndTimeUtc = DateTime.UtcNow
            };

            if (isComplexTest)
            {
                execution.Metadata = new TestMetadata
                {
                    Categories = new[] { "Integration", "Slow" }
                };
                execution.Metadata.CustomAttributes["Priority"] = "High";
            }

            if (i % 15 == 0)
            {
                execution.ErrorMessage = "Complex failure scenario";
                execution.StackTrace = @"   at Complex.Method.Call() in Complex.cs:line 123
   at Integration.Test.Execute() in Test.cs:line 456";
            }

            collector.RecordTest(execution);

            if (i % 150 == 149)
            {
                await collector.FlushAsync().ConfigureAwait(false);
            }
        }

        await collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Memory stability: Repeated cycles to detect memory leaks.
    /// Validates: No memory accumulation, proper disposal, GC behavior.
    /// </summary>
    [Benchmark]
    public async Task MemoryStability_10Cycles_100TestsEach()
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
            await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

            for (int i = 0; i < 100; i++)
            {
                var execution = new TestExecution
                {
                    ExecutionId = Guid.NewGuid(),
                    Identity = new TestIdentity 
                    { 
                        TestId = $"memory-cycle{cycle}-test{i}",
                        FullyQualifiedName = $"Memory.Stability.Cycle{cycle}.Test{i}"
                    },
                    TestName = $"Memory Test C{cycle} T{i}",
                    Outcome = TestOutcome.Passed,
                    Duration = TimeSpan.FromMilliseconds(_random.Next(10, 100)),
                    StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-50),
                    EndTimeUtc = DateTime.UtcNow
                };

                collector.RecordTest(execution);
            }

            await collector.FlushAsync().ConfigureAwait(false);
            // Collector disposed at end of iteration
        }
    }

    /// <summary>
    /// Continuous pressure: Back-to-back test recording without pauses.
    /// Validates: Sustained throughput, no performance degradation over time.
    /// </summary>
    [Benchmark]
    public async Task ContinuousPressure_3000Tests()
    {
        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "stress-test-key",
            ProjectId = "stress-project",
            BatchSize = 300,
            FlushInterval = TimeSpan.Zero
        };

        await using var collector = new TestExecutionCollector(new NoOpUploader(), config);

        for (int i = 0; i < 3000; i++)
        {
            collector.RecordTest(new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"continuous-{i}",
                    FullyQualifiedName = $"Stress.Continuous.Test{i}"
                },
                TestName = $"Continuous Test {i}",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(10, 80)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-45),
                EndTimeUtc = DateTime.UtcNow
            });
        }

        await collector.FlushAsync().ConfigureAwait(false);
    }
}
