/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Options;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Collector.Internals;

namespace Xping.Sdk.Benchmarks;

/// <summary>
/// Benchmarks for TestExecutionCollector performance.
/// Measures test recording overhead, throughput, and memory usage.
/// </summary>
/// <remarks>
/// Performance Targets:
/// - Test tracking overhead: &lt;5ms per test
/// - Memory per test: &lt;100 bytes
/// - Collection throughput: &gt;10,000 tests/sec
/// </remarks>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class CollectorBenchmarks
{
    private ITestExecutionCollector? _collector;
    private TestExecution? _sampleExecution;
    private List<TestExecution>? _executions100;
    private List<TestExecution>? _executions1000;
    private XpingConfiguration? _config;

    [GlobalSetup]
    public void Setup()
    {
        // Configure with no automatic flushing for pure measurement
        _config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 10000, // Large batch to avoid flushing during benchmark
            FlushInterval = TimeSpan.Zero, // Disable automatic flushing
            SamplingRate = 1.0 // 100% sampling
        };

        _collector = new TestExecutionCollector(Options.Create(_config));

        // Create sample test execution
        _sampleExecution = CreateTestExecution("Sample.Test.Method");

        // Pre-create test execution batches
        _executions100 = Enumerable.Range(0, 100)
            .Select(i => CreateTestExecution($"Test.Method.{i}"))
            .ToList();

        _executions1000 = Enumerable.Range(0, 1000)
            .Select(i => CreateTestExecution($"Test.Method.{i}"))
            .ToList();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _collector?.Dispose();
    }

    /// <summary>
    /// Benchmark: Record a single test execution.
    /// Target: &lt;5ms per test
    /// </summary>
    [Benchmark(Description = "Record single test (overhead measurement)")]
    public void RecordSingleTest()
    {
        _collector!.RecordTest(_sampleExecution!);
    }

    /// <summary>
    /// Benchmark: Record 100 test executions sequentially.
    /// Measures sequential recording performance.
    /// </summary>
    [Benchmark(Description = "Record 100 tests sequentially")]
    public void Record100Tests()
    {
        foreach (var execution in _executions100!)
        {
            _collector!.RecordTest(execution);
        }
    }

    /// <summary>
    /// Benchmark: Record 1000 test executions concurrently from multiple threads.
    /// Target: &gt;10,000 tests/sec throughput
    /// </summary>
    [Benchmark(Description = "Record 1000 tests concurrently (4 threads)")]
    public void Record1000TestsConcurrent()
    {
        Parallel.ForEach(
            _executions1000!,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            execution => _collector!.RecordTest(execution));
    }

    /// <summary>
    /// Benchmark: Test execution object creation.
    /// Measures the cost of creating TestExecution objects.
    /// </summary>
    [Benchmark(Description = "Create TestExecution object")]
    public TestExecution CreateTestExecutionObject()
    {
        return CreateTestExecution("Benchmark.Test.Method");
    }

    /// <summary>
    /// Benchmark: Sampling logic overhead.
    /// Measures the cost of sampling decision with 100% sampling rate.
    /// </summary>
    [Benchmark(Description = "Sampling with 100% rate")]
    public void SamplingOverhead100Percent()
    {
        // This will go through sampling logic (always sample)
        _collector!.RecordTest(_sampleExecution!);
    }

    /// <summary>
    /// Creates a sample TestExecution for benchmarking.
    /// </summary>
    private static TestExecution CreateTestExecution(string testName)
    {
        var identity = new TestIdentityBuilder()
            .WithTestId(Guid.NewGuid().ToString("N"))
            .WithFullyQualifiedName($"Benchmark.Tests.{testName}")
            .WithAssembly("Benchmark.Tests")
            .WithNamespace("Benchmark.Tests")
            .WithClassName("BenchmarkTests")
            .WithMethodName(testName)
            .Build();

        return new TestExecutionBuilder()
            .WithTestName(testName)
            .WithOutcome(TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(100))
            .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
            .WithEndTime(DateTime.UtcNow)
            .WithIdentity(identity)
            .Build();
    }
}
