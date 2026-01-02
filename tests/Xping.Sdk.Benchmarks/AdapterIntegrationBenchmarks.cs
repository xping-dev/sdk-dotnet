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
/// Benchmarks for adapter-specific integration patterns.
/// Simulates how NUnit/xUnit/MSTest adapters interact with the SDK.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class AdapterIntegrationBenchmarks
{
    private TestExecutionCollector? _collector;
    private ITestResultUploader? _uploader;
    private XpingConfiguration? _config;
    private readonly Random _random = new();

    private sealed class NoOpUploader : ITestResultUploader
    {
        public Task<UploadResult> UploadAsync(IEnumerable<TestExecution> executions, CancellationToken cancellationToken = default)
        {
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
            BatchSize = 100, // Typical batch size for adapters
            FlushInterval = TimeSpan.FromSeconds(5),
            SamplingRate = 1.0
        };

        _uploader = new NoOpUploader();
        _collector = new TestExecutionCollector(_uploader, _config);
    }

    /// <summary>
    /// NUnit pattern: Tests with categories, properties, and setup/teardown.
    /// Validates: Metadata handling, property serialization.
    /// </summary>
    [Benchmark]
    public async Task NUnitPatternWithCategories()
    {
        for (int i = 0; i < 20; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"nunit-test-{i}",
                    FullyQualifiedName = $"NUnit.Tests.TestClass.TestMethod{i}",
                    ClassName = "TestClass",
                    MethodName = $"TestMethod{i}",
                    Namespace = "NUnit.Tests"
                },
                TestName = $"NUnit Test {i}",
                Outcome = i % 5 == 0 ? TestOutcome.Failed : TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(50, 200)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-100),
                EndTimeUtc = DateTime.UtcNow,
                Metadata = new TestMetadata
                {
                    Categories = new[] { "Integration", "Slow" }
                }
            };

            execution.Metadata.CustomAttributes["Framework"] = "NUnit";
            execution.Metadata.CustomAttributes["Author"] = "TeamA";

            _collector!.RecordTest(execution);
        }

        await _collector!.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// xUnit pattern: Theory tests with data-driven parameters.
    /// Validates: Parameterized test identity, parameter hash handling.
    /// </summary>
    [Benchmark]
    public async Task XUnitTheoryPattern()
    {
        // Simulate 3 theory tests, each with 5 parameter sets
        for (int testIdx = 0; testIdx < 3; testIdx++)
        {
            for (int paramSet = 0; paramSet < 5; paramSet++)
            {
                var execution = new TestExecution
                {
                    ExecutionId = Guid.NewGuid(),
                    Identity = new TestIdentity 
                    { 
                        TestId = $"xunit-theory-{testIdx}-{paramSet}",
                        FullyQualifiedName = $"XUnit.Tests.TheoryClass.TheoryMethod{testIdx}",
                        ClassName = "TheoryClass",
                        MethodName = $"TheoryMethod{testIdx}",
                        Namespace = "XUnit.Tests",
                        ParameterHash = $"param-hash-{paramSet}",
                        DisplayName = $"TheoryMethod{testIdx}(param{paramSet})"
                    },
                    TestName = $"Theory Test {testIdx} [param{paramSet}]",
                    Outcome = (testIdx + paramSet) % 7 == 0 ? TestOutcome.Failed : TestOutcome.Passed,
                    Duration = TimeSpan.FromMilliseconds(_random.Next(20, 100)),
                    StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-50),
                    EndTimeUtc = DateTime.UtcNow
                };

                _collector!.RecordTest(execution);
            }
        }

        await _collector!.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// MSTest pattern: Tests with test context, owner, and priority.
    /// Validates: MSTest-specific metadata, test context serialization.
    /// </summary>
    [Benchmark]
    public async Task MSTestPatternWithContext()
    {
        for (int i = 0; i < 15; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"mstest-test-{i}",
                    FullyQualifiedName = $"MSTest.Tests.UnitTestClass.TestMethod{i}",
                    ClassName = "UnitTestClass",
                    MethodName = $"TestMethod{i}",
                    Namespace = "MSTest.Tests",
                    SourceFile = $"UnitTestClass.cs",
                    SourceLineNumber = 42 + i
                },
                TestName = $"MSTest Method {i}",
                Outcome = i % 6 == 0 ? TestOutcome.Failed : TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(30, 150)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-75),
                EndTimeUtc = DateTime.UtcNow,
                Metadata = new TestMetadata()
            };

            execution.Metadata.CustomAttributes["Owner"] = "Developer1";
            execution.Metadata.CustomAttributes["Priority"] = (i % 3).ToString(System.Globalization.CultureInfo.InvariantCulture);
            execution.Metadata.CustomAttributes["WorkItem"] = $"WI-{1000 + i}";

            _collector!.RecordTest(execution);
        }

        await _collector!.FlushAsync().ConfigureAwait(false);
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
                    var execution = new TestExecution
                    {
                        ExecutionId = Guid.NewGuid(),
                        Identity = new TestIdentity 
                        { 
                            TestId = $"parallel-adapter{capturedId}-test-{i}",
                            FullyQualifiedName = $"Parallel.Adapter{capturedId}.TestMethod{i}"
                        },
                        TestName = $"Adapter {capturedId} Test {i}",
                        Outcome = TestOutcome.Passed,
                        Duration = TimeSpan.FromMilliseconds(_random.Next(10, 50)),
                        StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-25),
                        EndTimeUtc = DateTime.UtcNow
                    };

                    _collector!.RecordTest(execution);
                }

                await Task.Delay(10).ConfigureAwait(false);
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        await _collector!.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Retry scenario: Tests with retry metadata (flaky test detection).
    /// Validates: Retry information serialization, flaky test patterns.
    /// </summary>
    [Benchmark]
    public async Task TestsWithRetryMetadata()
    {
        for (int i = 0; i < 10; i++)
        {
            var isFlaky = i % 3 == 0;
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"retry-test-{i}",
                    FullyQualifiedName = $"Retry.Tests.FlakyTest{i}"
                },
                TestName = $"Retry Test {i}",
                Outcome = isFlaky ? (i % 2 == 0 ? TestOutcome.Passed : TestOutcome.Failed) : TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(50, 150)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-100),
                EndTimeUtc = DateTime.UtcNow
            };

            if (isFlaky)
            {
                execution.Retry = new RetryMetadata
                {
                    AttemptNumber = (i % 3) + 1,
                    MaxRetries = 3,
                    PassedOnRetry = i % 3 > 0
                };
            }

            _collector!.RecordTest(execution);
        }

        await _collector!.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Session context scenario: Tests sharing session information.
    /// Validates: Session context optimization (first test has full context, rest null).
    /// </summary>
    [Benchmark]
    public async Task TestsWithSessionContext()
    {
        var sessionContext = new TestSession
        {
            SessionId = Guid.NewGuid().ToString(),
            StartedAt = DateTime.UtcNow,
            EnvironmentInfo = new EnvironmentInfo
            {
                MachineName = "BenchmarkMachine",
                OperatingSystem = "macOS 15.5",
                RuntimeVersion = ".NET 9.0",
                Framework = "BenchmarkDotNet"
            }
        };

        for (int i = 0; i < 20; i++)
        {
            var execution = new TestExecution
            {
                ExecutionId = Guid.NewGuid(),
                Identity = new TestIdentity 
                { 
                    TestId = $"session-test-{i}",
                    FullyQualifiedName = $"Session.Tests.TestMethod{i}"
                },
                TestName = $"Session Test {i}",
                Outcome = TestOutcome.Passed,
                Duration = TimeSpan.FromMilliseconds(_random.Next(20, 80)),
                StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-50),
                EndTimeUtc = DateTime.UtcNow,
                SessionContext = i == 0 ? sessionContext : null // Optimization: only first test has full context
            };

            _collector!.RecordTest(execution);
        }

        await _collector!.FlushAsync().ConfigureAwait(false);
    }
}
