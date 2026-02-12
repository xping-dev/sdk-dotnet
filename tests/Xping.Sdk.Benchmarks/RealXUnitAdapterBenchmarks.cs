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
    /// Minimal xUnit test recording.
    /// Baseline overhead of xUnit adapter.
    /// </summary>
    [Benchmark]
    public void MinimalTestRecording()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "xunit-simple-test",
                FullyQualifiedName = "XUnit.Tests.SimpleTests.SimpleTest",
                ClassName = "SimpleTests",
                MethodName = "SimpleTest",
                Namespace = "XUnit.Tests"
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
    /// xUnit test with Trait attributes (xUnit metadata pattern).
    /// </summary>
    [Benchmark]
    public void TestRecording_WithTraits()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "xunit-trait-test",
                FullyQualifiedName = "XUnit.Tests.TraitTests.TraitTest",
                ClassName = "TraitTests",
                MethodName = "TraitTest",
                Namespace = "XUnit.Tests"
            },
            TestName = "TraitTest",
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(1),
            StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-1),
            EndTimeUtc = DateTime.UtcNow,
            Metadata = new TestMetadata
            {
                Categories = new[] { "Integration", "API", "Fast" }
            }
        };

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
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"xunit-batch-test-{i}",
                    FullyQualifiedName = $"XUnit.Tests.BatchTests.Test{i}",
                    ClassName = "BatchTests",
                    MethodName = $"Test{i}",
                    Namespace = "XUnit.Tests"
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
    /// xUnit Theory tests with InlineData (parameterized tests).
    /// Each parameter combination requires unique TestIdentity with parameter hash.
    /// </summary>
    [Benchmark]
    public void TheoryTestRecording()
    {
        var testCases = new[] { (1, 2, 3), (5, 10, 15), (100, 200, 300) };

        foreach (var (a, b, expected) in testCases)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"xunit-theory-test-{a}-{b}-{expected}",
                    FullyQualifiedName = "XUnit.Tests.TheoryTests.AdditionTheory",
                    ClassName = "TheoryTests",
                    MethodName = "AdditionTheory",
                    Namespace = "XUnit.Tests",
                    ParameterHash = $"hash-{a}-{b}-{expected}",
                    DisplayName = $"AdditionTheory(a: {a}, b: {b}, expected: {expected})"
                },
                TestName = $"AdditionTheory(a: {a}, b: {b}, expected: {expected})",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(1),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-1),
                EndTimeUtc = DateTime.UtcNow
            };

            XpingContext.RecordTest(execution);
        }
    }

    /// <summary>
    /// Failed xUnit test with exception details.
    /// </summary>
    [Benchmark]
    public void FailedTestRecording_WithException()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "xunit-failed-test",
                FullyQualifiedName = "XUnit.Tests.FailureTests.FailingTest",
                ClassName = "FailureTests",
                MethodName = "FailingTest",
                Namespace = "XUnit.Tests"
            },
            TestName = "FailingTest",
            Outcome = TestOutcome.Failed,
            Duration = TimeSpan.FromMilliseconds(1),
            StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-1),
            EndTimeUtc = DateTime.UtcNow,
            ExceptionType = "System.InvalidOperationException",
            ErrorMessage = "Test failed for demonstration",
            StackTrace = "at XUnit.Tests.FailureTests.FailingTest() in FailureTests.cs:line 42"
        };

        XpingContext.RecordTest(execution);
    }

    /// <summary>
    /// Skipped xUnit test (Skip parameter on Fact/Theory).
    /// </summary>
    [Benchmark]
    public void SkippedTestRecording()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "xunit-skipped-test",
                FullyQualifiedName = "XUnit.Tests.SkippedTests.SkippedTest",
                ClassName = "SkippedTests",
                MethodName = "SkippedTest",
                Namespace = "XUnit.Tests"
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
    /// xUnit test with collection fixture pattern.
    /// Tests using fixtures have shared context metadata.
    /// </summary>
    [Benchmark]
    public void TestRecording_WithFixture()
    {
        var execution = new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity 
            { 
                TestId = "xunit-fixture-test",
                FullyQualifiedName = "XUnit.Tests.FixtureTests.FixtureTest",
                ClassName = "FixtureTests",
                MethodName = "FixtureTest",
                Namespace = "XUnit.Tests"
            },
            TestName = "FixtureTest",
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(_random.Next(50, 200)),
            StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-100),
            EndTimeUtc = DateTime.UtcNow,
            Metadata = new TestMetadata
            {
                Categories = new[] { "Integration" }
            }
        };

        execution.Metadata.CustomAttributes["Framework"] = "xUnit";
        execution.Metadata.CustomAttributes["Collection"] = "DatabaseFixture";

        XpingContext.RecordTest(execution);
    }
}
