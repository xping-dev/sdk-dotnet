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
using Xping.Sdk.Core.Models;
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
        XpingContext.Reset();

        // Initialize with a NoOp uploader
        var config = new XpingConfiguration
        {
            Enabled = true, // Must be enabled for tracking
            ApiKey = "benchmark-key",
            ProjectId = "benchmark-project",
            BatchSize = 100,
            FlushInterval = TimeSpan.FromSeconds(30)
        };

        // Since XpingContext doesn't expose a way to pass custom uploader in Initialize(),
        // we initialize normally - the benchmarks will use XpingContext.RecordTest() API
        XpingContext.Initialize(config);
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        if (XpingContext.IsInitialized)
        {
            await XpingContext.FlushAsync();
            await XpingContext.DisposeAsync();
        }
    }

    /// <summary>
    /// Minimal test execution via NUnit adapter.
    /// Baseline overhead of XpingContext.RecordTest() with simple NUnit test.
    /// </summary>
    [Benchmark]
    public void MinimalTestRecording()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "nunit-simple-test",
                FullyQualifiedName = "NUnit.Tests.SimpleTests.SimpleTest",
                ClassName = "SimpleTests",
                MethodName = "SimpleTest",
                Namespace = "NUnit.Tests"
            },
            TestName = "SimpleTest",
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(1),
            StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-1),
            EndTimeUtc = DateTime.UtcNow
        };

        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// NUnit test with categories (NUnit [Category] attribute pattern).
    /// </summary>
    [Benchmark]
    public void TestRecording_WithCategories()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "nunit-categorized-test",
                FullyQualifiedName = "NUnit.Tests.CategoryTests.CategorizedTest",
                ClassName = "CategoryTests",
                MethodName = "CategorizedTest",
                Namespace = "NUnit.Tests"
            },
            TestName = "CategorizedTest",
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(1),
            StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-1),
            EndTimeUtc = DateTime.UtcNow,
            Metadata = new TestMetadata
            {
                Categories = new[] { "Integration", "API", "Slow" }
            }
        };

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
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"nunit-batch-test-{i}",
                    FullyQualifiedName = $"NUnit.Tests.BatchTests.Test{i}",
                    ClassName = "BatchTests",
                    MethodName = $"Test{i}",
                    Namespace = "NUnit.Tests"
                },
                TestName = $"Test{i}",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(1),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-1),
                EndTimeUtc = DateTime.UtcNow
            };

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
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"nunit-parameterized-test-{a}-{b}",
                    FullyQualifiedName = "NUnit.Tests.ParameterTests.ParameterizedTest",
                    ClassName = "ParameterTests",
                    MethodName = "ParameterizedTest",
                    Namespace = "NUnit.Tests",
                    ParameterHash = $"hash-{a}-{b}",
                    DisplayName = $"ParameterizedTest({a},{b})"
                },
                TestName = $"ParameterizedTest({a},{b})",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(1),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-1),
                EndTimeUtc = DateTime.UtcNow
            };

            XpingContext.RecordTest(execution);
        }
    }

    /// <summary>
    /// Failed test recording with exception details.
    /// </summary>
    [Benchmark]
    public void FailedTestRecording_WithException()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "nunit-failed-test",
                FullyQualifiedName = "NUnit.Tests.FailureTests.FailingTest",
                ClassName = "FailureTests",
                MethodName = "FailingTest",
                Namespace = "NUnit.Tests"
            },
            TestName = "FailingTest",
            Outcome = TestOutcome.Failed,
            Duration = TimeSpan.FromMilliseconds(1),
            StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-1),
            EndTimeUtc = DateTime.UtcNow,
            ExceptionType = "System.InvalidOperationException",
            ErrorMessage = "Test failed for demonstration",
            StackTrace = "at NUnit.Tests.FailureTests.FailingTest() in FailureTests.cs:line 42"
        };

        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// Skipped test recording (NUnit [Ignore] attribute pattern).
    /// </summary>
    [Benchmark]
    public void SkippedTestRecording()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "nunit-skipped-test",
                FullyQualifiedName = "NUnit.Tests.SkippedTests.SkippedTest",
                ClassName = "SkippedTests",
                MethodName = "SkippedTest",
                Namespace = "NUnit.Tests"
            },
            TestName = "SkippedTest",
            Outcome = TestOutcome.Skipped,
            Duration = TimeSpan.Zero,
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow
        };

        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// NUnit test with custom attributes/properties.
    /// </summary>
    [Benchmark]
    public void TestRecording_WithCustomAttributes()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "nunit-custom-attributes-test",
                FullyQualifiedName = "NUnit.Tests.CustomTests.CustomAttributesTest",
                ClassName = "CustomTests",
                MethodName = "CustomAttributesTest",
                Namespace = "NUnit.Tests"
            },
            TestName = "CustomAttributesTest",
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(_random.Next(50, 200)),
            StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-100),
            EndTimeUtc = DateTime.UtcNow,
            Metadata = new TestMetadata
            {
                Categories = new[] { "Integration" }
            }
        };

        execution.Metadata.CustomAttributes["Framework"] = "NUnit";
        execution.Metadata.CustomAttributes["Author"] = "TeamA";
        execution.Metadata.CustomAttributes["Priority"] = "High";

        XpingContext.RecordTest(execution);
    }
}
