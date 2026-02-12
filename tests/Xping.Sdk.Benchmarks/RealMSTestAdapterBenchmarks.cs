/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;

#pragma warning disable CA1515 // Consider making public types internal
#pragma warning disable CA5394 // Do not use insecure randomness
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
#pragma warning disable CA1024 // Use properties where appropriate
#pragma warning disable IDE0055 // Fix formatting

namespace Xping.Sdk.Benchmarks;

using BenchmarkDotNet.Attributes;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;
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
        XpingContext.Reset();

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
            await XpingContext.FlushAsync();
            await XpingContext.DisposeAsync();
        }
    }

    /// <summary>
    /// Minimal MSTest test recording.
    /// Baseline overhead of MSTest adapter.
    /// </summary>
    [Benchmark]
    public void MinimalTestRecording()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "mstest-simple-test",
                FullyQualifiedName = "MSTest.Tests.SimpleTests.SimpleTest",
                ClassName = "SimpleTests",
                MethodName = "SimpleTest",
                Namespace = "MSTest.Tests"
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
    /// MSTest test with TestCategory attributes.
    /// </summary>
    [Benchmark]
    public void TestRecording_WithCategories()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "mstest-categorized-test",
                FullyQualifiedName = "MSTest.Tests.CategoryTests.CategorizedTest",
                ClassName = "CategoryTests",
                MethodName = "CategorizedTest",
                Namespace = "MSTest.Tests"
            },
            TestName = "CategorizedTest",
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(1),
            StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-1),
            EndTimeUtc = DateTime.UtcNow,
            Metadata = new TestMetadata
            {
                Categories = new[] { "Integration", "API", "Unit" }
            }
        };

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
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"mstest-batch-test-{i}",
                    FullyQualifiedName = $"MSTest.Tests.BatchTests.Test{i}",
                    ClassName = "BatchTests",
                    MethodName = $"Test{i}",
                    Namespace = "MSTest.Tests"
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
    /// MSTest DataRow tests (parameterized tests).
    /// Each DataRow requires unique TestIdentity with parameter hash.
    /// </summary>
    [Benchmark]
    public void DataRowTestRecording()
    {
        var dataRows = new[] { (2, 3, 5), (10, 5, 15), (-1, 1, 0) };

        foreach (var (a, b, expected) in dataRows)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"mstest-datarow-test-{a}-{b}-{expected}",
                    FullyQualifiedName = "MSTest.Tests.DataTests.AdditionDataTest",
                    ClassName = "DataTests",
                    MethodName = "AdditionDataTest",
                    Namespace = "MSTest.Tests",
                    ParameterHash = $"hash-{a}-{b}-{expected}",
                    DisplayName = $"AdditionDataTest ({a}, {b}, {expected})"
                },
                TestName = $"AdditionDataTest ({a}, {b}, {expected})",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(1),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-1),
                EndTimeUtc = DateTime.UtcNow
            };

            XpingContext.RecordTest(execution);
        }
    }

    /// <summary>
    /// Failed MSTest test with exception details.
    /// </summary>
    [Benchmark]
    public void FailedTestRecording_WithException()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "mstest-failed-test",
                FullyQualifiedName = "MSTest.Tests.FailureTests.FailingTest",
                ClassName = "FailureTests",
                MethodName = "FailingTest",
                Namespace = "MSTest.Tests"
            },
            TestName = "FailingTest",
            Outcome = TestOutcome.Failed,
            Duration = TimeSpan.FromMilliseconds(1),
            StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-1),
            EndTimeUtc = DateTime.UtcNow,
            ExceptionType = "System.InvalidOperationException",
            ErrorMessage = "Test failed for demonstration",
            StackTrace = "at MSTest.Tests.FailureTests.FailingTest() in FailureTests.cs:line 42"
        };

        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// Ignored MSTest test (Ignore attribute).
    /// </summary>
    [Benchmark]
    public void IgnoredTestRecording()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "mstest-ignored-test",
                FullyQualifiedName = "MSTest.Tests.IgnoredTests.IgnoredTest",
                ClassName = "IgnoredTests",
                MethodName = "IgnoredTest",
                Namespace = "MSTest.Tests"
            },
            TestName = "IgnoredTest",
            Outcome = TestOutcome.Skipped,
            Duration = TimeSpan.Zero,
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow
        };

        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// MSTest test with custom properties (Priority, Owner, etc.).
    /// </summary>
    [Benchmark]
    public void TestRecording_WithCustomProperties()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "mstest-custom-properties-test",
                FullyQualifiedName = "MSTest.Tests.CustomTests.CustomPropertiesTest",
                ClassName = "CustomTests",
                MethodName = "CustomPropertiesTest",
                Namespace = "MSTest.Tests"
            },
            TestName = "CustomPropertiesTest",
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(_random.Next(50, 200)),
            StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-100),
            EndTimeUtc = DateTime.UtcNow,
            Metadata = new TestMetadata
            {
                Categories = new[] { "Unit" }
            }
        };

        execution.Metadata.CustomAttributes["Framework"] = "MSTest";
        execution.Metadata.CustomAttributes["Priority"] = "1";
        execution.Metadata.CustomAttributes["Owner"] = "TeamA";

        XpingContext.RecordTest(execution);
    }
}
