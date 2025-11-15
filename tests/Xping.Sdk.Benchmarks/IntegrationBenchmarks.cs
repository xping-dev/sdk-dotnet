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
/// Benchmarks for end-to-end integration scenarios combining multiple SDK components.
/// Tests realistic workflows: collection + serialization + upload.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class IntegrationBenchmarks
{
    private TestExecutionCollector? _collector;
    private ITestResultUploader? _uploader;
    private XpingConfiguration? _config;
    private readonly Random _random = new();

    private sealed class NoOpUploader : ITestResultUploader
    {
        public Task<UploadResult> UploadAsync(IEnumerable<TestExecution> executions, CancellationToken cancellationToken = default)
        {
            // Simulate minimal processing without actual network I/O
            var count = executions.Count();
            return Task.FromResult(new UploadResult 
            { 
                Success = true, 
                ExecutionCount = count 
            });
        }
    }    

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

        _uploader = new NoOpUploader();
        _collector = new TestExecutionCollector(_uploader, _config);
    }

    /// <summary>
    /// Baseline: Single test end-to-end (record + serialize + upload).
    /// Target: Should be minimal overhead beyond component benchmarks.
    /// </summary>
    [Benchmark]
    public async Task SingleTestEndToEnd()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity { TestId = "integration-test-1", FullyQualifiedName = "Integration.Test.SingleTest" },
            TestName = "Single Test",
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(100),
            StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-100),
            EndTimeUtc = DateTime.UtcNow
        };

        _collector!.RecordTest(execution);
        await _collector!.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Typical scenario: 10 passing tests in sequence.
    /// Validates: Batch collection efficiency, memory reuse.
    /// </summary>
    [Benchmark]
    public async Task TenPassingTests()
    {
        for (int i = 0; i < 10; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity { TestId = $"integration-test-{i}", FullyQualifiedName = $"Integration.Test.PassingTest{i}" },
                TestName = $"Passing Test {i}",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(50),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-50),
                EndTimeUtc = DateTime.UtcNow
            };

            _collector!.RecordTest(execution);
        }

        await _collector!.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Realistic mixed scenario: 20 tests with varied outcomes.
    /// Validates: Handling different test states, error data serialization.
    /// </summary>
    [Benchmark]
    public async Task MixedOutcomeTests()
    {
        var outcomes = new[] { TestOutcome.Passed, TestOutcome.Failed, TestOutcome.Skipped };

        for (int i = 0; i < 20; i++)
        {
            var outcome = outcomes[i % 3];
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity { TestId = $"integration-mixed-{i}", FullyQualifiedName = $"Integration.Test.MixedTest{i}" },
                TestName = $"Mixed Test {i}",
                Outcome = outcome,
                Duration = TimeSpan.FromMilliseconds(_random.Next(10, 500)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-100),
                EndTimeUtc = DateTime.UtcNow
            };

            if (outcome == TestOutcome.Failed)
            {
                execution.ErrorMessage = "Test assertion failed";
                execution.StackTrace = "   at Integration.Test.Method() in Test.cs:line 42";
            }

            _collector!.RecordTest(execution);
        }

        await _collector!.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Failure scenario: Tests with full error details (message + stack trace).
    /// Validates: Error data overhead, large payload serialization.
    /// </summary>
    [Benchmark]
    public async Task FailedTestsWithStackTraces()
    {
        for (int i = 0; i < 10; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity { TestId = $"integration-failed-{i}", FullyQualifiedName = $"Integration.Test.FailedTest{i}" },
                TestName = $"Failed Test {i}",
                Outcome = TestOutcome.Failed,
                Duration = TimeSpan.FromMilliseconds(75),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-75),
                EndTimeUtc = DateTime.UtcNow,
                ErrorMessage = "Expected value to be 42 but was 0",
                StackTrace = @"   at Integration.Test.AssertEquals(Int32 expected, Int32 actual) in Assert.cs:line 15
   at Integration.Test.TestMethod() in Test.cs:line 42
   at TestFramework.Runner.Execute() in Runner.cs:line 123"
            };

            _collector!.RecordTest(execution);
        }

        await _collector!.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stress scenario: 100 tests in rapid succession.
    /// Validates: Sustained throughput, no memory leaks, batch efficiency.
    /// </summary>
    [Benchmark]
    public async Task RapidTestExecution()
    {
        for (int i = 0; i < 100; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity { TestId = $"integration-rapid-{i}", FullyQualifiedName = $"Integration.Test.RapidTest{i}" },
                TestName = $"Rapid Test {i}",
                Outcome = i % 10 == 0 ? TestOutcome.Failed : TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(5, 100)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-50),
                EndTimeUtc = DateTime.UtcNow
            };

            _collector!.RecordTest(execution);
        }

        await _collector!.FlushAsync().ConfigureAwait(false);
    }
}
