/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Configuration;
using Xping.Sdk.Core.Configuration;

namespace Xping.Sdk.Benchmarks;

/// <summary>
/// Benchmarks for configuration loading and validation.
/// Measures configuration initialization overhead from various sources.
/// </summary>
[MemoryDiagnoser]
public class ConfigurationBenchmarks
{
    private IConfiguration? _jsonConfig;
    private Dictionary<string, string?>? _envVariables;

    [GlobalSetup]
    public void Setup()
    {
        // Prepare JSON configuration
        var jsonData = new Dictionary<string, string?>
        {
            ["Xping:ApiKey"] = "test-api-key",
            ["Xping:ProjectId"] = "test-project-id",
            ["Xping:Enabled"] = "true",
            ["Xping:BatchSize"] = "100",
            ["Xping:FlushInterval"] = "00:00:30",
            ["Xping:SamplingRate"] = "1.0"
        };

        _jsonConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(jsonData)
            .Build();

        // Prepare environment variables simulation
        _envVariables = new Dictionary<string, string?>
        {
            ["XPING_APIKEY"] = "test-api-key",
            ["XPING_PROJECTID"] = "test-project-id",
            ["XPING_ENABLED"] = "true",
            ["XPING_BATCH_SIZE"] = "100"
        };
    }

    /// <summary>
    /// Benchmark: Load configuration from JSON (IConfiguration).
    /// </summary>
    [Benchmark(Description = "Load configuration from JSON")]
    public XpingConfiguration LoadFromJson()
    {
        var config = new XpingConfiguration();
        _jsonConfig!.GetSection("Xping").Bind(config);
        return config;
    }

    /// <summary>
    /// Benchmark: Create configuration programmatically.
    /// </summary>
    [Benchmark(Description = "Create configuration programmatically")]
    public XpingConfiguration CreateProgrammatically()
    {
        return new XpingConfiguration
        {
            ApiKey = "test-api-key",
            ProjectId = "test-project-id",
            Enabled = true,
            BatchSize = 100,
            FlushInterval = TimeSpan.FromSeconds(30),
            SamplingRate = 1.0
        };
    }

    /// <summary>
    /// Benchmark: Validate configuration.
    /// </summary>
    [Benchmark(Description = "Validate configuration")]
    public bool ValidateConfiguration()
    {
        var config = new XpingConfiguration
        {
            ApiKey = "test-api-key",
            ProjectId = "test-project-id"
        };

        // Simple validation logic
        return !string.IsNullOrWhiteSpace(config.ApiKey) &&
               !string.IsNullOrWhiteSpace(config.ProjectId) &&
               config.BatchSize > 0 &&
               config.FlushInterval >= TimeSpan.Zero &&
               config.SamplingRate >= 0.0 &&
               config.SamplingRate <= 1.0;
    }

    /// <summary>
    /// Benchmark: Options object instantiation.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Instantiate default configuration")]
    public XpingConfiguration InstantiateDefault()
    {
        return new XpingConfiguration();
    }
}
