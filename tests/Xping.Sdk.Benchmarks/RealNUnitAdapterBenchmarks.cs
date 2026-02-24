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
using Xping.Sdk.NUnit;
using System;
using System.Threading.Tasks;

/// <summary>
/// Real NUnit adapter benchmarks testing actual framework integration.
/// Measures overhead of using XpingContext.Initialize(), RecordTest(), and FlushAsync() with NUnit-specific patterns.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class RealNUnitAdapterBenchmarks
{
    private readonly Random _random = new();

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Reset any existing context
        XpingContext.ShutdownAsync().AsTask().GetAwaiter().GetResult();

        // Initialize with benchmark configuration
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
    /// Minimal test execution via NUnit adapter.
    /// Baseline overhead of XpingContext.RecordTest() with simple NUnit test.
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
    /// NUnit test with categories (NUnit [Category] attribute pattern).
    /// </summary>
    [Benchmark]
    public void TestRecording_WithCategories()
    {
        var metadata = new TestMetadataBuilder()
            .AddCategory("Integration")
            .AddCategory("API")
            .AddCategory("Slow")
            .Build();

        var execution = new TestExecutionBuilder()
            .WithTestName("CategorizedTest")
            .WithOutcome(TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(1))
            .WithStartTime(DateTime.UtcNow.AddMilliseconds(-1))
            .WithEndTime(DateTime.UtcNow)
            .WithMetadata(metadata)
            .Build();

        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// Batch of 10 NUnit tests to measure throughput.
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
    /// Parameterized test recording (NUnit [TestCase] attribute pattern).
    /// Tests with parameters require parameter hash in TestIdentity.
    /// </summary>
    [Benchmark]
    public void ParameterizedTestRecording()
    {
        var testCases = new[] { (1, 2), (5, 10), (100, 200) };

        foreach (var (a, b) in testCases)
        {
            var identity = new TestIdentityBuilder()
                .WithTestFingerprint($"nunit-parameterized-test-{a}-{b}")
                .WithFullyQualifiedName("NUnit.Tests.ParameterTests.ParameterizedTest")
                .WithClassName("ParameterTests")
                .WithMethodName("ParameterizedTest")
                .WithNamespace("NUnit.Tests")
                .WithParameterHash($"hash-{a}-{b}")
                .WithDisplayName($"ParameterizedTest({a},{b})")
                .Build();

            var execution = new TestExecutionBuilder()
                .WithTestName($"ParameterizedTest({a},{b})")
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
    /// Failed test recording with exception details.
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
                "at NUnit.Tests.FailureTests.FailingTest() in FailureTests.cs:line 42")
            .Build();

        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// Skipped test recording (NUnit [Ignore] attribute pattern).
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
    /// NUnit test with custom attributes/properties.
    /// </summary>
    [Benchmark]
    public void TestRecording_WithCustomAttributes()
    {
        var metadata = new TestMetadataBuilder()
            .AddCategory("Integration")
            .AddCustomAttribute("Framework", "NUnit")
            .AddCustomAttribute("Author", "TeamA")
            .AddCustomAttribute("Priority", "High")
            .Build();

        var execution = new TestExecutionBuilder()
            .WithTestName("CustomAttributesTest")
            .WithOutcome(TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(_random.Next(50, 200)))
            .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
            .WithEndTime(DateTime.UtcNow)
            .WithMetadata(metadata)
            .Build();

        XpingContext.RecordTest(execution);
    }
}
