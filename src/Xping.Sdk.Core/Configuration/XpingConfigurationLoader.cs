/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Configuration;

using Microsoft.Extensions.Configuration;
using System;
using System.IO;

/// <summary>
/// Provides methods for loading <see cref="XpingConfiguration"/> from various sources.
/// </summary>
public static class XpingConfigurationLoader
{
    private const string ConfigurationSectionName = "Xping";
    private const string EnvironmentVariablePrefix = "XPING_";

    /// <summary>
    /// Loads configuration from multiple sources with priority:
    /// 1. Environment variables (highest)
    /// 2. appsettings.json
    /// 3. Default values (lowest)
    /// </summary>
    /// <param name="basePath">The base path for loading configuration files. If null, uses current directory.</param>
    /// <returns>The loaded configuration.</returns>
    public static XpingConfiguration Load(string basePath = null)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath ?? Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{GetEnvironmentName()}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(EnvironmentVariablePrefix)
            .Build();

        var xpingConfig = new XpingConfiguration();
        config.GetSection(ConfigurationSectionName).Bind(xpingConfig);

        // Also bind environment variables directly (with prefix)
        BindEnvironmentVariables(xpingConfig);

        return xpingConfig;
    }

    /// <summary>
    /// Loads configuration from a specific JSON file.
    /// </summary>
    /// <param name="filePath">The path to the JSON configuration file.</param>
    /// <returns>The loaded configuration.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static XpingConfiguration LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}", filePath);
        }

        var config = new ConfigurationBuilder()
            .AddJsonFile(filePath, optional: false, reloadOnChange: false)
            .Build();

        var xpingConfig = new XpingConfiguration();
        config.GetSection(ConfigurationSectionName).Bind(xpingConfig);

        return xpingConfig;
    }

    /// <summary>
    /// Loads configuration from environment variables only.
    /// </summary>
    /// <returns>The loaded configuration.</returns>
    public static XpingConfiguration LoadFromEnvironmentVariables()
    {
        var xpingConfig = new XpingConfiguration();
        BindEnvironmentVariables(xpingConfig);
        return xpingConfig;
    }

    /// <summary>
    /// Loads configuration from an existing <see cref="IConfiguration"/> instance.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="sectionName">The section name. Defaults to "Xping".</param>
    /// <returns>The loaded configuration.</returns>
    public static XpingConfiguration LoadFromConfiguration(
        IConfiguration configuration,
        string sectionName = ConfigurationSectionName)
    {
        var xpingConfig = new XpingConfiguration();
        configuration.GetSection(sectionName).Bind(xpingConfig);
        return xpingConfig;
    }

    private static void BindEnvironmentVariables(XpingConfiguration config)
    {
        // API Configuration
        config.ApiEndpoint = GetEnvironmentVariable("APIENDPOINT", config.ApiEndpoint);
        config.ApiKey = GetEnvironmentVariable("APIKEY", config.ApiKey);
        config.ProjectId = GetEnvironmentVariable("PROJECTID", config.ProjectId);

        // Batch Configuration
        config.BatchSize = GetEnvironmentVariable("BATCHSIZE", config.BatchSize);
        config.FlushInterval = GetEnvironmentVariableTimeSpan("FLUSHINTERVAL", config.FlushInterval);

        // Environment Configuration
        config.Environment = GetEnvironmentVariable("ENVIRONMENT", config.Environment);
        config.AutoDetectCIEnvironment = GetEnvironmentVariable("AUTODETECTCIENVIRONMENT", config.AutoDetectCIEnvironment);

        // Feature Flags
        config.Enabled = GetEnvironmentVariable("ENABLED", config.Enabled);
        config.CaptureStackTraces = GetEnvironmentVariable("CAPTURESTACKTRACES", config.CaptureStackTraces);
        config.EnableCompression = GetEnvironmentVariable("ENABLECOMPRESSION", config.EnableCompression);
        config.EnableOfflineQueue = GetEnvironmentVariable("ENABLEOFFLINEQUEUE", config.EnableOfflineQueue);

        // Retry Configuration
        config.MaxRetries = GetEnvironmentVariable("MAXRETRIES", config.MaxRetries);
        config.RetryDelay = GetEnvironmentVariableTimeSpan("RETRYDELAY", config.RetryDelay);

        // Sampling Configuration
        config.SamplingRate = GetEnvironmentVariable("SAMPLINGRATE", config.SamplingRate);

        // Timeout Configuration
        config.UploadTimeout = GetEnvironmentVariableTimeSpan("UPLOADTIMEOUT", config.UploadTimeout);
    }

    private static string GetEnvironmentVariable(string name, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariablePrefix + name);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static int GetEnvironmentVariable(string name, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariablePrefix + name);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    private static bool GetEnvironmentVariable(string name, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariablePrefix + name);
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    private static double GetEnvironmentVariable(string name, double defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariablePrefix + name);
        return double.TryParse(value, out var result) ? result : defaultValue;
    }

    private static TimeSpan GetEnvironmentVariableTimeSpan(string name, TimeSpan defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(EnvironmentVariablePrefix + name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        // Try parsing as seconds (integer)
        if (int.TryParse(value, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        // Try parsing as TimeSpan string
        if (TimeSpan.TryParse(value, out var timeSpan))
        {
            return timeSpan;
        }

        return defaultValue;
    }

    private static string GetEnvironmentName()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";
    }
}
