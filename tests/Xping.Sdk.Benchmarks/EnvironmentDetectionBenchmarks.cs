/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using BenchmarkDotNet.Attributes;

namespace Xping.Sdk.Benchmarks;

/// <summary>
/// Benchmarks for environment detection capabilities.
/// Measures performance of OS, CI, and runtime environment detection.
/// </summary>
[MemoryDiagnoser]
public class EnvironmentDetectionBenchmarks
{
    /// <summary>
    /// Benchmark: Detect operating system information.
    /// </summary>
    [Benchmark(Description = "Detect OS information")]
    public string DetectOperatingSystem()
    {
        return Environment.OSVersion.ToString();
    }

    /// <summary>
    /// Benchmark: Detect runtime version.
    /// </summary>
    [Benchmark(Description = "Detect runtime version")]
    public string DetectRuntimeVersion()
    {
        return Environment.Version.ToString();
    }

    /// <summary>
    /// Benchmark: Detect CI environment (multiple checks).
    /// </summary>
    [Benchmark(Description = "Detect CI environment")]
    public bool DetectCiEnvironment()
    {
        // Check common CI environment variables
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_PIPELINES")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JENKINS_URL")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TRAVIS")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CIRCLECI"));
    }

    /// <summary>
    /// Benchmark: Collect all environment information (typical use case).
    /// </summary>
    [Benchmark(Description = "Collect all environment info")]
    public object CollectEnvironmentInfo()
    {
        return new EnvironmentInfo
        {
            OperatingSystem = Environment.OSVersion.ToString(),
            RuntimeVersion = Environment.Version.ToString(),
            MachineName = Environment.MachineName,
            ProcessorCount = Environment.ProcessorCount,
            IsCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
        };
    }

    /// <summary>
    /// Benchmark: Check processor count.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Get processor count")]
    public int GetProcessorCount()
    {
        return Environment.ProcessorCount;
    }

    /// <summary>
    /// Helper class to represent collected environment information.
    /// </summary>
    internal sealed class EnvironmentInfo
    {
        public string? OperatingSystem { get; set; }
        public string? RuntimeVersion { get; set; }
        public string? MachineName { get; set; }
        public int ProcessorCount { get; set; }
        public bool IsCI { get; set; }
    }
}
