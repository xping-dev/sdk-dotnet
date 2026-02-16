/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;

#pragma warning disable CA1515 // Consider making public types internal
#pragma warning disable CA5394 // Do not use insecure randomness
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
#pragma warning disable CA1024 // Use properties where appropriate
#pragma warning disable IDE0055 // Fix formatting

namespace Xping.Sdk.Benchmarks;

using BenchmarkDotNet.Attributes;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.MSTest;
using System;
using System.Threading.Tasks;

/// <summary>
/// Real MSTest adapter benchmarks testing actual framework integration.
/// Measures overhead of using XpingContext with MSTest-specific patterns.
/// MSTest uses XpingTestBase inheritance for automatic tracking.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class RealMSTestAdapterBenchmarks
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
            await XpingContext.FinalizeAsync();
            await XpingContext.ShutdownAsync();
        }
    }

    /// <summary>
    /// Minimal MSTest test recording.
    /// Baseline overhead of MSTest adapter.
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
    /// MSTest test with TestCategory attributes.
    /// </summary>
    [Benchmark]
    public void TestRecording_WithCategories()
    {
        var metadata = new TestMetadataBuilder()
            .AddCategory("Integration")
            .AddCategory("API")
            .AddCategory("Unit")
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
    /// Batch of 10 MSTest tests.
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
    /// MSTest DataRow tests (parameterized tests).
    /// Each DataRow requires unique TestIdentity with parameter hash.
    /// </summary>
    [Benchmark]
    public void DataRowTestRecording()
    {
        var dataRows = new[] { (2, 3, 5), (10, 5, 15), (-1, 1, 0) };

        foreach (var (a, b, expected) in dataRows)
        {
            var execution = new TestExecutionBuilder()
                .WithTestName($"AdditionDataTest ({a}, {b}, {expected})")
                .WithOutcome(TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(1))
                .WithStartTime(DateTime.UtcNow.AddMilliseconds(-1))
                .WithEndTime(DateTime.UtcNow)
                .Build();

            XpingContext.RecordTest(execution);
        }
    }

    /// <summary>
    /// Failed MSTest test with exception details.
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
                "at MSTest.Tests.FailureTests.FailingTest() in FailureTests.cs:line 42")
            .Build();

        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// Ignored MSTest test (Ignore attribute).
    /// </summary>
    [Benchmark]
    public void IgnoredTestRecording()
    {
        var execution = new TestExecutionBuilder()
            .WithTestName("IgnoredTest")
            .WithOutcome(TestOutcome.Skipped)
            .WithDuration(TimeSpan.Zero)
            .Build();

        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// MSTest test with custom properties (Priority, Owner, etc.).
    /// </summary>
    [Benchmark]
    public void TestRecording_WithCustomProperties()
    {
        var metadata = new TestMetadataBuilder()
            .AddCategory("Unit")
            .AddCustomAttribute("Framework", "MSTest")
            .AddCustomAttribute("Priority", "1")
            .AddCustomAttribute("Owner", "TeamA")
            .Build();

        var execution = new TestExecutionBuilder()
            .WithTestName("CustomPropertiesTest")
            .WithOutcome(TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(_random.Next(50, 200)))
            .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
            .WithEndTime(DateTime.UtcNow)
            .WithMetadata(metadata)
            .Build();

        XpingContext.RecordTest(execution);
    }
}
