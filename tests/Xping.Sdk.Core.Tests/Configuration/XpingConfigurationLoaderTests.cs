/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Configuration;

using System;
using System.IO;
using Xping.Sdk.Core.Configuration;
using Microsoft.Extensions.Configuration;

public class XpingConfigurationLoaderTests
{
    [Fact]
    public void LoadShouldReturnDefaultConfigurationWhenNoFilesExist()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        try
        {
            // Act
            var config = XpingConfigurationLoader.Load(tempPath);

            // Assert
            Assert.NotNull(config);
            Assert.Equal("https://api.xping.io", config.ApiEndpoint);
            Assert.Equal("Local", config.Environment);
        }
        finally
        {
            Directory.Delete(tempPath, true);
        }
    }

    [Fact]
    public void LoadFromFileShouldLoadConfigurationFromJson()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var jsonContent = @"{
            ""Xping"": {
                ""ApiKey"": ""test-key-from-file"",
                ""ProjectId"": ""test-project-from-file"",
                ""BatchSize"": 150,
                ""Environment"": ""Testing""
            }
        }";
        File.WriteAllText(tempFile, jsonContent);

        try
        {
            // Act
            var config = XpingConfigurationLoader.LoadFromFile(tempFile);

            // Assert
            Assert.Equal("test-key-from-file", config.ApiKey);
            Assert.Equal("test-project-from-file", config.ProjectId);
            Assert.Equal(150, config.BatchSize);
            Assert.Equal("Testing", config.Environment);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void LoadFromFileShouldThrowWhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            XpingConfigurationLoader.LoadFromFile(nonExistentFile));
    }

    [Fact]
    public void LoadFromEnvironmentVariablesShouldLoadConfiguration()
    {
        // Arrange
        try
        {
            Environment.SetEnvironmentVariable("XPING_APIKEY", "env-key");
            Environment.SetEnvironmentVariable("XPING_PROJECTID", "env-project");
            Environment.SetEnvironmentVariable("XPING_BATCHSIZE", "250");
            Environment.SetEnvironmentVariable("XPING_ENABLED", "false");

            // Act
            var config = XpingConfigurationLoader.LoadFromEnvironmentVariables();

            // Assert
            Assert.Equal("env-key", config.ApiKey);
            Assert.Equal("env-project", config.ProjectId);
            Assert.Equal(250, config.BatchSize);
            Assert.False(config.Enabled);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("XPING_APIKEY", null);
            Environment.SetEnvironmentVariable("XPING_PROJECTID", null);
            Environment.SetEnvironmentVariable("XPING_BATCHSIZE", null);
            Environment.SetEnvironmentVariable("XPING_ENABLED", null);
        }
    }

    [Fact]
    public void LoadFromEnvironmentVariablesShouldParseTimeSpanFromSeconds()
    {
        // Arrange
        try
        {
            Environment.SetEnvironmentVariable("XPING_FLUSHINTERVAL", "60");
            Environment.SetEnvironmentVariable("XPING_UPLOADTIMEOUT", "120");

            // Act
            var config = XpingConfigurationLoader.LoadFromEnvironmentVariables();

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(60), config.FlushInterval);
            Assert.Equal(TimeSpan.FromSeconds(120), config.UploadTimeout);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XPING_FLUSHINTERVAL", null);
            Environment.SetEnvironmentVariable("XPING_UPLOADTIMEOUT", null);
        }
    }

    [Fact]
    public void LoadFromEnvironmentVariablesShouldParseBooleanValues()
    {
        // Arrange
        try
        {
            Environment.SetEnvironmentVariable("XPING_CAPTURESTACKTRACES", "false");
            Environment.SetEnvironmentVariable("XPING_ENABLECOMPRESSION", "true");

            // Act
            var config = XpingConfigurationLoader.LoadFromEnvironmentVariables();

            // Assert
            Assert.False(config.CaptureStackTraces);
            Assert.True(config.EnableCompression);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XPING_CAPTURESTACKTRACES", null);
            Environment.SetEnvironmentVariable("XPING_ENABLECOMPRESSION", null);
        }
    }

    [Fact]
    public void LoadFromEnvironmentVariablesShouldParseDoubleValues()
    {
        // Arrange
        try
        {
            Environment.SetEnvironmentVariable("XPING_SAMPLINGRATE", "0.75");

            // Act
            var config = XpingConfigurationLoader.LoadFromEnvironmentVariables();

            // Assert
            Assert.Equal(0.75, config.SamplingRate);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XPING_SAMPLINGRATE", null);
        }
    }

    [Fact]
    public void LoadFromConfigurationShouldBindFromIConfiguration()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Xping:ApiKey"] = "config-key",
            ["Xping:ProjectId"] = "config-project",
            ["Xping:BatchSize"] = "300",
            ["Xping:Environment"] = "Staging"
        });
        var configuration = configBuilder.Build();

        // Act
        var config = XpingConfigurationLoader.LoadFromConfiguration(configuration);

        // Assert
        Assert.Equal("config-key", config.ApiKey);
        Assert.Equal("config-project", config.ProjectId);
        Assert.Equal(300, config.BatchSize);
        Assert.Equal("Staging", config.Environment);
    }

    [Fact]
    public void LoadFromConfigurationShouldSupportCustomSectionName()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CustomSection:ApiKey"] = "custom-key",
            ["CustomSection:ProjectId"] = "custom-project"
        });
        var configuration = configBuilder.Build();

        // Act
        var config = XpingConfigurationLoader.LoadFromConfiguration(configuration, "CustomSection");

        // Assert
        Assert.Equal("custom-key", config.ApiKey);
        Assert.Equal("custom-project", config.ProjectId);
    }

    [Fact]
    public void LoadShouldMergeMultipleSources()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        var appsettingsFile = Path.Combine(tempPath, "appsettings.json");
        var jsonContent = @"{
            ""Xping"": {
                ""ApiKey"": ""json-key"",
                ""ProjectId"": ""json-project"",
                ""BatchSize"": 150
            }
        }";
        File.WriteAllText(appsettingsFile, jsonContent);

        try
        {
            Environment.SetEnvironmentVariable("XPING_APIKEY", "env-key-override");
            Environment.SetEnvironmentVariable("XPING_MAXRETRIES", "7");

            // Act
            var config = XpingConfigurationLoader.Load(tempPath);

            // Assert - env var should override JSON
            Assert.Equal("env-key-override", config.ApiKey);
            // JSON value should be used when no env var
            Assert.Equal("json-project", config.ProjectId);
            Assert.Equal(150, config.BatchSize);
            // Env var for property not in JSON
            Assert.Equal(7, config.MaxRetries);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XPING_APIKEY", null);
            Environment.SetEnvironmentVariable("XPING_MAXRETRIES", null);
            Directory.Delete(tempPath, true);
        }
    }
}
