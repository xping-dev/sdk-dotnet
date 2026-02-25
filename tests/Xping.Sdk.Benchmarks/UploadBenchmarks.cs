/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Benchmarks;

/// <summary>
/// Benchmarks for upload and serialization performance.
/// Measures JSON serialization, batch processing, and simulated network overhead.
/// </summary>
/// <remarks>
/// Performance Targets:
/// - Batch upload (100 tests): &lt;500ms
/// - Serialization overhead: Minimal
/// - Memory efficiency during serialization
/// </remarks>
[MemoryDiagnoser]
public class UploadBenchmarks
{
    private List<TestExecution>? _executions10;
    private List<TestExecution>? _executions100;
    private List<TestExecution>? _executions1000;
    private JsonSerializerOptions? _jsonOptions;

    [GlobalSetup]
    public void Setup()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        // Create sample test execution batches
        _executions10 = CreateTestExecutions(10);
        _executions100 = CreateTestExecutions(100);
        _executions1000 = CreateTestExecutions(1000);
    }

    /// <summary>
    /// Benchmark: Serialize 10 test executions to JSON.
    /// </summary>
    [Benchmark(Description = "Serialize 10 test executions to JSON")]
    public string Serialize10Tests()
    {
        return JsonSerializer.Serialize(_executions10, _jsonOptions);
    }

    /// <summary>
    /// Benchmark: Serialize 100 test executions to JSON.
    /// Target: Part of &lt;500ms total upload time
    /// </summary>
    [Benchmark(Description = "Serialize 100 test executions to JSON")]
    public string Serialize100Tests()
    {
        return JsonSerializer.Serialize(_executions100, _jsonOptions);
    }

    /// <summary>
    /// Benchmark: Serialize 1000 test executions to JSON.
    /// </summary>
    [Benchmark(Description = "Serialize 1000 test executions to JSON")]
    public string Serialize1000Tests()
    {
        return JsonSerializer.Serialize(_executions1000, _jsonOptions);
    }

    /// <summary>
    /// Benchmark: Measure memory allocated for serializing 100 tests.
    /// </summary>
    [Benchmark(Description = "Memory allocation for serializing 100 tests")]
    public byte[] SerializeToUtf8Bytes100Tests()
    {
        return JsonSerializer.SerializeToUtf8Bytes(_executions100, _jsonOptions);
    }

    /// <summary>
    /// Benchmark: Calculate JSON payload size for 100 tests.
    /// </summary>
    [Benchmark(Description = "Calculate payload size for 100 tests")]
    public int CalculatePayloadSize100Tests()
    {
        var json = JsonSerializer.Serialize(_executions100, _jsonOptions);
        return System.Text.Encoding.UTF8.GetByteCount(json);
    }

    /// <summary>
    /// Creates a list of sample TestExecution objects.
    /// </summary>
    private static List<TestExecution> CreateTestExecutions(int count)
    {
        var executions = new List<TestExecution>(count);

        for (int i = 0; i < count; i++)
        {
            var start = DateTime.UtcNow.AddMinutes(-i);
            var duration = TimeSpan.FromMilliseconds(50 + (i % 200));
            var outcome = i % 10 == 0 ? TestOutcome.Failed : TestOutcome.Passed;

            var identity = new TestIdentityBuilder()
                .WithTestFingerprint(Guid.NewGuid().ToString("N"))
                .WithFullyQualifiedName($"Benchmark.Tests.TestClass.TestMethod{i}")
                .WithAssembly("Benchmark.Tests")
                .WithNamespace("Benchmark.Tests")
                .WithClassName("TestClass")
                .WithMethodName($"TestMethod{i}")
                .Build();

            var builder = new TestExecutionBuilder()
                .WithTestName($"TestMethod{i}")
                .WithOutcome(outcome)
                .WithDuration(duration)
                .WithStartTime(start)
                .WithEndTime(start.Add(duration))
                .WithIdentity(identity);

            if (outcome == TestOutcome.Failed)
            {
                builder.WithException(null, $"Test failed with error {i}", "  at TestMethod() in TestClass.cs:line 42");
            }

            executions.Add(builder.Build());
        }

        return executions;
    }
}
