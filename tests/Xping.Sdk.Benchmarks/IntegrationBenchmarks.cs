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
/// Benchmarks for end-to-end integration scenarios combining multiple SDK components.
/// Tests realistic workflows: collection + drain cycle.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class IntegrationBenchmarks
{
    private ITestExecutionCollector? _collector;
    private XpingConfiguration? _config;
    private readonly Random _random = new();

    [GlobalSetup]
    public void Setup()
    {
        _config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "bench-test-key",
            ProjectId = "bench-project",
            BatchSize = 10000,
            FlushInterval = TimeSpan.Zero,
            SamplingRate = 1.0
        };

        _collector = new TestExecutionCollector(Options.Create(_config));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _collector?.Dispose();
    }

    /// <summary>
    /// Baseline: Single test end-to-end (record + drain).
    /// Target: Should be minimal overhead beyond component benchmarks.
    /// </summary>
    [Benchmark]
    public void SingleTestEndToEnd()
    {
        _collector!.RecordTest(new TestExecutionBuilder()
            .WithTestName("Single Test")
            .WithOutcome(TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(100))
            .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
            .WithEndTime(DateTime.UtcNow)
            .Build());

        _ = _collector!.Drain();
    }

    /// <summary>
    /// Typical scenario: 10 passing tests in sequence.
    /// Validates: Batch collection efficiency, memory reuse.
    /// </summary>
    [Benchmark]
    public void TenPassingTests()
    {
        for (int i = 0; i < 10; i++)
        {
            _collector!.RecordTest(new TestExecutionBuilder()
                .WithTestName($"Passing Test {i}")
                .WithOutcome(TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(50))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-50))
                .WithEndTime(DateTime.UtcNow)
                .Build());
        }

        _ = _collector!.Drain();
    }

    /// <summary>
    /// Realistic mixed scenario: 20 tests with varied outcomes.
    /// Validates: Handling different test states, error data serialization.
    /// </summary>
    [Benchmark]
    public void MixedOutcomeTests()
    {
        var outcomes = new[] { TestOutcome.Passed, TestOutcome.Failed, TestOutcome.Skipped };

        for (int i = 0; i < 20; i++)
        {
            var outcome = outcomes[i % 3];
            var builder = new TestExecutionBuilder()
                .WithTestName($"Mixed Test {i}")
                .WithOutcome(outcome)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(10, 500)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
                .WithEndTime(DateTime.UtcNow);

            if (outcome == TestOutcome.Failed)
            {
                builder.WithException(null, "Test assertion failed", "   at Integration.Test.Method() in Test.cs:line 42");
            }

            _collector!.RecordTest(builder.Build());
        }

        _ = _collector!.Drain();
    }

    /// <summary>
    /// Failure scenario: Tests with full error details (message + stack trace).
    /// Validates: Error data overhead, large payload serialization.
    /// </summary>
    [Benchmark]
    public void FailedTestsWithStackTraces()
    {
        for (int i = 0; i < 10; i++)
        {
            _collector!.RecordTest(new TestExecutionBuilder()
                .WithTestName($"Failed Test {i}")
                .WithOutcome(TestOutcome.Failed)
                .WithDuration(TimeSpan.FromMilliseconds(75))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-75))
                .WithEndTime(DateTime.UtcNow)
                .WithException(null, "Expected value to be 42 but was 0",
                    @"   at Integration.Test.AssertEquals(Int32 expected, Int32 actual) in Assert.cs:line 15
   at Integration.Test.TestMethod() in Test.cs:line 42
   at TestFramework.Runner.Execute() in Runner.cs:line 123")
                .Build());
        }

        _ = _collector!.Drain();
    }

    /// <summary>
    /// Stress scenario: 100 tests in rapid succession.
    /// Validates: Sustained throughput, no memory leaks, batch efficiency.
    /// </summary>
    [Benchmark]
    public void RapidTestExecution()
    {
        for (int i = 0; i < 100; i++)
        {
            var outcome = i % 10 == 0 ? TestOutcome.Failed : TestOutcome.Passed;
            var builder = new TestExecutionBuilder()
                .WithTestName($"Rapid Test {i}")
                .WithOutcome(outcome)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(5, 100)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-50))
                .WithEndTime(DateTime.UtcNow);

            _collector!.RecordTest(builder.Build());
        }

        _ = _collector!.Drain();
    }
}
