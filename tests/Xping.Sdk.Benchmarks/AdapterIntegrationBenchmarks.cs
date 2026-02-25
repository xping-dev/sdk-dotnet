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
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Benchmarks for adapter-specific integration patterns.
/// Simulates how NUnit/xUnit/MSTest adapters interact with the SDK.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class AdapterIntegrationBenchmarks
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
            BatchSize = 100,
            FlushInterval = TimeSpan.FromSeconds(5),
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
    /// NUnit pattern: Tests with categories, properties, and setup/teardown.
    /// Validates: Metadata handling, property serialization.
    /// </summary>
    [Benchmark]
    public void NUnitPatternWithCategories()
    {
        for (int i = 0; i < 20; i++)
        {
            var metadata = new TestMetadataBuilder()
                .AddCategory("Integration")
                .AddCategory("Slow")
                .AddCustomAttribute("Framework", "NUnit")
                .AddCustomAttribute("Author", "TeamA")
                .Build();

            var identity = new TestIdentityBuilder()
                .WithTestFingerprint($"nunit-test-{i}")
                .WithFullyQualifiedName($"NUnit.Tests.TestClass.TestMethod{i}")
                .WithClassName("TestClass")
                .WithMethodName($"TestMethod{i}")
                .WithNamespace("NUnit.Tests")
                .Build();

            var outcome = i % 5 == 0 ? TestOutcome.Failed : TestOutcome.Passed;
            _collector!.RecordTest(new TestExecutionBuilder()
                .WithTestName($"NUnit Test {i}")
                .WithOutcome(outcome)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(50, 200)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
                .WithEndTime(DateTime.UtcNow)
                .WithIdentity(identity)
                .WithMetadata(metadata)
                .Build());
        }

        _ = _collector!.Drain();
    }

    /// <summary>
    /// xUnit pattern: Theory tests with data-driven parameters.
    /// Validates: Parameterized test identity, parameter hash handling.
    /// </summary>
    [Benchmark]
    public void XUnitTheoryPattern()
    {
        // Simulate 3 theory tests, each with 5 parameter sets
        for (int testIdx = 0; testIdx < 3; testIdx++)
        {
            for (int paramSet = 0; paramSet < 5; paramSet++)
            {
                var identity = new TestIdentityBuilder()
                    .WithTestFingerprint($"xunit-theory-{testIdx}-{paramSet}")
                    .WithFullyQualifiedName($"XUnit.Tests.TheoryClass.TheoryMethod{testIdx}")
                    .WithClassName("TheoryClass")
                    .WithMethodName($"TheoryMethod{testIdx}")
                    .WithNamespace("XUnit.Tests")
                    .WithParameterHash($"param-hash-{paramSet}")
                    .WithDisplayName($"TheoryMethod{testIdx}(param{paramSet})")
                    .Build();

                _collector!.RecordTest(new TestExecutionBuilder()
                    .WithTestName($"Theory Test {testIdx} [param{paramSet}]")
                    .WithOutcome((testIdx + paramSet) % 7 == 0 ? TestOutcome.Failed : TestOutcome.Passed)
                    .WithDuration(TimeSpan.FromMilliseconds(_random.Next(20, 100)))
                    .WithStartTime(DateTime.UtcNow.AddMilliseconds(-50))
                    .WithEndTime(DateTime.UtcNow)
                    .WithIdentity(identity)
                    .Build());
            }
        }

        _ = _collector!.Drain();
    }

    /// <summary>
    /// MSTest pattern: Tests with test context, owner, and priority.
    /// Validates: MSTest-specific metadata, test context serialization.
    /// </summary>
    [Benchmark]
    public void MSTestPatternWithContext()
    {
        for (int i = 0; i < 15; i++)
        {
            var metadata = new TestMetadataBuilder()
                .AddCustomAttribute("Owner", "Developer1")
                .AddCustomAttribute("Priority", (i % 3).ToString(System.Globalization.CultureInfo.InvariantCulture))
                .AddCustomAttribute("WorkItem", $"WI-{1000 + i}")
                .Build();

            var identity = new TestIdentityBuilder()
                .WithTestFingerprint($"mstest-test-{i}")
                .WithFullyQualifiedName($"MSTest.Tests.UnitTestClass.TestMethod{i}")
                .WithClassName("UnitTestClass")
                .WithMethodName($"TestMethod{i}")
                .WithNamespace("MSTest.Tests")
                .WithSourceFile("UnitTestClass.cs")
                .WithSourceLineNumber(42 + i)
                .Build();

            _collector!.RecordTest(new TestExecutionBuilder()
                .WithTestName($"MSTest Method {i}")
                .WithOutcome(i % 6 == 0 ? TestOutcome.Failed : TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(30, 150)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-75))
                .WithEndTime(DateTime.UtcNow)
                .WithIdentity(identity)
                .WithMetadata(metadata)
                .Build());
        }

        _ = _collector!.Drain();
    }

    /// <summary>
    /// Parallel adapter scenario: Multiple test adapters running concurrently.
    /// Validates: Thread-safety, concurrent collection handling.
    /// </summary>
    [Benchmark]
    public async Task ParallelAdapterExecution()
    {
        var tasks = new List<Task>();

        // Simulate 3 adapters running in parallel
        for (int adapterId = 0; adapterId < 3; adapterId++)
        {
            int capturedId = adapterId;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    _collector!.RecordTest(new TestExecutionBuilder()
                        .WithTestName($"Adapter {capturedId} Test {i}")
                        .WithOutcome(TestOutcome.Passed)
                        .WithDuration(TimeSpan.FromMilliseconds(_random.Next(10, 50)))
                        .WithStartTime(DateTime.UtcNow.AddMilliseconds(-25))
                        .WithEndTime(DateTime.UtcNow)
                        .Build());
                }

                await Task.Delay(10).ConfigureAwait(false);
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _ = _collector!.Drain();
    }

    /// <summary>
    /// Retry scenario: Tests with retry metadata (flaky test detection).
    /// Validates: Retry information serialization, flaky test patterns.
    /// </summary>
    [Benchmark]
    public void TestsWithRetryMetadata()
    {
        for (int i = 0; i < 10; i++)
        {
            var isFlaky = i % 3 == 0;
            var outcome = isFlaky
                ? (i % 2 == 0 ? TestOutcome.Passed : TestOutcome.Failed)
                : TestOutcome.Passed;

            var builder = new TestExecutionBuilder()
                .WithTestName($"Retry Test {i}")
                .WithOutcome(outcome)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(50, 150)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
                .WithEndTime(DateTime.UtcNow);

            if (isFlaky)
            {
                var retry = new RetryMetadataBuilder()
                    .WithAttemptNumber((i % 3) + 1)
                    .WithMaxRetries(3)
                    .WithPassedOnRetry(i % 3 > 0)
                    .Build();
                builder.WithRetry(retry);
            }

            _collector!.RecordTest(builder.Build());
        }

        _ = _collector!.Drain();
    }

    /// <summary>
    /// Metadata-rich scenario: Tests with extensive custom attributes.
    /// Validates: Custom attribute serialization, metadata overhead.
    /// </summary>
    [Benchmark]
    public void TestsWithExtensiveMetadata()
    {
        for (int i = 0; i < 20; i++)
        {
            var metadataBuilder = new TestMetadataBuilder()
                .AddCustomAttribute("SessionId", Guid.NewGuid().ToString())
                .AddCustomAttribute("MachineName", "BenchmarkMachine")
                .AddCustomAttribute("Framework", "BenchmarkDotNet");

            _collector!.RecordTest(new TestExecutionBuilder()
                .WithTestName($"Session Test {i}")
                .WithOutcome(TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(_random.Next(20, 80)))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-50))
                .WithEndTime(DateTime.UtcNow)
                .WithMetadata(metadataBuilder.Build())
                .Build());
        }

        _ = _collector!.Drain();
    }
}
