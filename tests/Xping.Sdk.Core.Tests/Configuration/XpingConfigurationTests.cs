/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Configuration;

using Xping.Sdk.Core.Configuration;

public sealed class XpingConfigurationTests
{
    [Fact]
    public void DefaultConfigurationShouldHaveCorrectValues()
    {
        // Act
        var config = new XpingConfiguration();

        // Assert
        Assert.Equal("https://upload.xping.io/v1", config.ApiEndpoint);
        Assert.Null(config.ApiKey);
        Assert.Null(config.ProjectId);
        Assert.Equal(100, config.BatchSize);
        Assert.Equal(TimeSpan.FromSeconds(30), config.FlushInterval);
        Assert.Equal("Local", config.Environment);
        Assert.True(config.AutoDetectCIEnvironment);
        Assert.True(config.Enabled);
        Assert.True(config.CaptureStackTraces);
        Assert.True(config.EnableCompression);
        Assert.Equal(3, config.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(2), config.RetryDelay);
        Assert.Equal(1.0, config.SamplingRate);
        Assert.Equal(TimeSpan.FromSeconds(30), config.UploadTimeout);
    }

    [Fact]
    public void ValidateShouldReturnErrorsWhenApiKeyIsMissing()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ProjectId = "test-project"
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("ApiKey is required.", errors);
    }

    [Fact]
    public void ValidateShouldReturnErrorsWhenProjectIdIsMissing()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiKey = "test-key"
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("ProjectId is required.", errors);
    }

    [Fact]
    public void ValidateShouldReturnErrorsWhenApiEndpointIsInvalid()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiKey = "test-key",
            ProjectId = "test-project",
            ApiEndpoint = "not-a-valid-url"
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("ApiEndpoint must be a valid HTTP or HTTPS URL.", errors);
    }

    [Fact]
    public void ValidateShouldReturnErrorsWhenBatchSizeIsZero()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiKey = "test-key",
            ProjectId = "test-project",
            BatchSize = 0
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("BatchSize must be greater than zero.", errors);
    }

    [Fact]
    public void ValidateShouldReturnErrorsWhenBatchSizeExceedsMaximum()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiKey = "test-key",
            ProjectId = "test-project",
            BatchSize = 1001
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("BatchSize cannot exceed 1000.", errors);
    }

    [Fact]
    public void ValidateShouldReturnErrorsWhenFlushIntervalIsNegative()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiKey = "test-key",
            ProjectId = "test-project",
            FlushInterval = TimeSpan.FromSeconds(-1)
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("FlushInterval must be greater than zero.", errors);
    }

    [Fact]
    public void ValidateShouldReturnErrorsWhenMaxRetriesIsNegative()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiKey = "test-key",
            ProjectId = "test-project",
            MaxRetries = -1
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("MaxRetries cannot be negative.", errors);
    }

    [Fact]
    public void ValidateShouldReturnErrorsWhenMaxRetriesExceedsMaximum()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiKey = "test-key",
            ProjectId = "test-project",
            MaxRetries = 11
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Contains("MaxRetries cannot exceed 10.", errors);
    }

    [Fact]
    public void ValidateShouldReturnErrorsWhenSamplingRateIsInvalid()
    {
        // Arrange
        var config1 = new XpingConfiguration
        {
            ApiKey = "test-key",
            ProjectId = "test-project",
            SamplingRate = -0.1
        };

        var config2 = new XpingConfiguration
        {
            ApiKey = "test-key",
            ProjectId = "test-project",
            SamplingRate = 1.5
        };

        // Act
        var errors1 = config1.Validate();
        var errors2 = config2.Validate();

        // Assert
        Assert.Contains("SamplingRate must be between 0.0 and 1.0.", errors1);
        Assert.Contains("SamplingRate must be between 0.0 and 1.0.", errors2);
    }

    [Fact]
    public void ValidateShouldReturnNoErrorsForValidConfiguration()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiKey = "test-key",
            ProjectId = "test-project"
        };

        // Act
        var errors = config.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void IsValidShouldReturnTrueForValidConfiguration()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiKey = "test-key",
            ProjectId = "test-project"
        };

        // Act & Assert
        Assert.True(config.IsValid());
    }

    [Fact]
    public void IsValidShouldReturnFalseForInvalidConfiguration()
    {
        // Arrange
        var config = new XpingConfiguration();

        // Act & Assert
        Assert.False(config.IsValid());
    }

    [Fact]
    public void ShouldAllowSettingAllProperties()
    {
        // Arrange & Act
        var config = new XpingConfiguration
        {
            ApiEndpoint = "https://custom-api.example.com",
            ApiKey = "custom-key",
            ProjectId = "custom-project",
            BatchSize = 50,
            FlushInterval = TimeSpan.FromMinutes(1),
            Environment = "Production",
            AutoDetectCIEnvironment = false,
            Enabled = false,
            CaptureStackTraces = false,
            EnableCompression = false,
            MaxRetries = 5,
            RetryDelay = TimeSpan.FromSeconds(5),
            SamplingRate = 0.5,
            UploadTimeout = TimeSpan.FromMinutes(2)
        };

        // Assert
        Assert.Equal("https://custom-api.example.com", config.ApiEndpoint);
        Assert.Equal("custom-key", config.ApiKey);
        Assert.Equal("custom-project", config.ProjectId);
        Assert.Equal(50, config.BatchSize);
        Assert.Equal(TimeSpan.FromMinutes(1), config.FlushInterval);
        Assert.Equal("Production", config.Environment);
        Assert.False(config.AutoDetectCIEnvironment);
        Assert.False(config.Enabled);
        Assert.False(config.CaptureStackTraces);
        Assert.False(config.EnableCompression);
        Assert.Equal(5, config.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(5), config.RetryDelay);
        Assert.Equal(0.5, config.SamplingRate);
        Assert.Equal(TimeSpan.FromMinutes(2), config.UploadTimeout);
    }
}
