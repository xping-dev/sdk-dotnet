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
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.XUnit;
using System;
using System.Threading.Tasks;

/// <summary>
/// Real xUnit adapter benchmarks testing actual framework integration.
/// Measures overhead of using XpingContext with xUnit-specific patterns.
/// xUnit uses custom test framework (XpingTestFramework) for automatic tracking.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class RealXUnitAdapterBenchmarks
{
    private readonly Random _random = new();

    [GlobalSetup]
    public void GlobalSetup()
    {
        XpingContext.ShutdownAsync().AsTask().GetAwaiter().GetResult();

        var config = new XpingConfiguration
        {
            Enabled = true,
            ApiKey = "benchmark-key",
            ProjectId = "benchmark-project",
            BatchSize = 100,
            FlushInterval = TimeSpan.FromSeconds(30)
        };

        XpingContext.Initialize(config);
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        if (XpingContext.IsInitialized)
        {
            await XpingContext.FinalizeAsync().ConfigureAwait(false);
            await XpingContext.ShutdownAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Minimal xUnit test recording.
    /// Baseline overhead of xUnit adapter.
    /// </summary>
    [Benchmark]
    public void MinimalTestRecording()
    {
        var execution = new TestExecutionBuilder()
            .WithTestName("SimpleTest")
            .WithOutcome(TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(1))
            .WithStartTime(DateTime.UtcNow.AddMilliseconds(-1))
            .WithEndTime(DateTime.UtcNow)
            .Build();

        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// xUnit test with Trait attributes (xUnit metadata pattern).
    /// </summary>
    [Benchmark]
    public void TestRecording_WithTraits()
    {
        var metadata = new TestMetadataBuilder()
            .AddCategory("Integration")
            .AddCategory("API")
            .AddCategory("Fast")
            .Build();

        var execution = new TestExecutionBuilder()
            .WithTestName("TraitTest")
            .WithOutcome(TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(1))
            .WithStartTime(DateTime.UtcNow.AddMilliseconds(-1))
            .WithEndTime(DateTime.UtcNow)
            .WithMetadata(metadata)
            .Build();

        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// Batch of 10 xUnit tests.
    /// </summary>
    [Benchmark]
    public void BatchRecording_10Tests()
    {
        for (int i = 0; i < 10; i++)
        {
            var execution = new TestExecutionBuilder()
                .WithTestName($"Test{i}")
                .WithOutcome(TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(1))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-1))
                .WithEndTime(DateTime.UtcNow)
                .Build();

            XpingContext.RecordTest(execution);
        }
    }

    /// <summary>
    /// xUnit Theory tests with InlineData (parameterized tests).
    /// Each parameter combination requires unique TestIdentity with parameter hash.
    /// </summary>
    [Benchmark]
    public void TheoryTestRecording()
    {
        var testCases = new[] { (1, 2, 3), (5, 10, 15), (100, 200, 300) };

        foreach (var (a, b, expected) in testCases)
        {
            var identity = new TestIdentityBuilder()
                .WithTestFingerprint($"xunit-theory-test-{a}-{b}-{expected}")
                .WithFullyQualifiedName("XUnit.Tests.TheoryTests.AdditionTheory")
                .WithClassName("TheoryTests")
                .WithMethodName("AdditionTheory")
                .WithNamespace("XUnit.Tests")
                .WithParameterHash($"hash-{a}-{b}-{expected}")
                .WithDisplayName($"AdditionTheory(a: {a}, b: {b}, expected: {expected})")
                .Build();

            var execution = new TestExecutionBuilder()
                .WithTestName($"AdditionTheory(a: {a}, b: {b}, expected: {expected})")
                .WithOutcome(TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(1))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-1))
                .WithEndTime(DateTime.UtcNow)
                .WithIdentity(identity)
                .Build();

            XpingContext.RecordTest(execution);
        }
    }

    /// <summary>
    /// Failed xUnit test with exception details.
    /// </summary>
    [Benchmark]
    public void FailedTestRecording_WithException()
    {
        var execution = new TestExecutionBuilder()
            .WithTestName("FailingTest")
            .WithOutcome(TestOutcome.Failed)
            .WithDuration(TimeSpan.FromMilliseconds(1))
            .WithStartTime(DateTime.UtcNow.AddMilliseconds(-1))
            .WithEndTime(DateTime.UtcNow)
            .WithException(
                "System.InvalidOperationException",
                "Test failed for demonstration",
                "at XUnit.Tests.FailureTests.FailingTest() in FailureTests.cs:line 42")
            .Build();

        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// Skipped xUnit test (Skip parameter on Fact/Theory).
    /// </summary>
    [Benchmark]
    public void SkippedTestRecording()
    {
        var execution = new TestExecutionBuilder()
            .WithTestName("SkippedTest")
            .WithOutcome(TestOutcome.Skipped)
            .WithDuration(TimeSpan.Zero)
            .Build();

        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// xUnit test with collection fixture pattern.
    /// Tests using fixtures have shared context metadata.
    /// </summary>
    [Benchmark]
    public void TestRecording_WithFixture()
    {
        var metadata = new TestMetadataBuilder()
            .AddCategory("Integration")
            .AddCustomAttribute("Framework", "xUnit")
            .AddCustomAttribute("Collection", "DatabaseFixture")
            .Build();

        var execution = new TestExecutionBuilder()
            .WithTestName("FixtureTest")
            .WithOutcome(TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(_random.Next(50, 200)))
            .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
            .WithEndTime(DateTime.UtcNow)
            .WithMetadata(metadata)
            .Build();

        XpingContext.RecordTest(execution);
    }
}
