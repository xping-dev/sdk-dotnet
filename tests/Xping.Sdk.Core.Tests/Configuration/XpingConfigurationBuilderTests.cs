/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Configuration;

using Xping.Sdk.Core.Configuration;

public sealed class XpingConfigurationBuilderTests
{
    [Fact]
    public void BuilderShouldCreateConfigurationWithDefaultValues()
    {
        // Arrange
        var builder = new XpingConfigurationBuilder()
            .WithApiKey("test-key")
            .WithProjectId("test-project");

        // Act
        var config = builder.Build();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("test-key", config.ApiKey);
        Assert.Equal("test-project", config.ProjectId);
        Assert.Equal("https://upload.xping.io/v1", config.ApiEndpoint);
    }

    [Fact]
    public void BuilderShouldAllowSettingAllProperties()
    {
        // Arrange
        var builder = new XpingConfigurationBuilder()
            .WithApiEndpoint("https://custom.example.com")
            .WithApiKey("custom-key")
            .WithProjectId("custom-project")
            .WithBatchSize(50)
            .WithFlushInterval(TimeSpan.FromMinutes(1))
            .WithEnvironment("Production")
            .WithAutoDetectCIEnvironment(false)
            .WithCiEnvironmentName("Pipeline")
            .WithEnabled(false)
            .WithCaptureStackTraces(false)
            .WithEnableCompression(false)
            .WithMaxRetries(5)
            .WithRetryDelay(TimeSpan.FromSeconds(5))
            .WithSamplingRate(0.5)
            .WithUploadTimeout(TimeSpan.FromMinutes(2));

        // Act
        var config = builder.Build();

        // Assert
        Assert.Equal("https://custom.example.com", config.ApiEndpoint);
        Assert.Equal("custom-key", config.ApiKey);
        Assert.Equal("custom-project", config.ProjectId);
        Assert.Equal(50, config.BatchSize);
        Assert.Equal(TimeSpan.FromMinutes(1), config.FlushInterval);
        Assert.Equal("Production", config.Environment);
        Assert.False(config.AutoDetectCIEnvironment);
        Assert.Equal("Pipeline", config.CiEnvironmentName);
        Assert.False(config.Enabled);
        Assert.False(config.CaptureStackTraces);
        Assert.False(config.EnableCompression);
        Assert.Equal(5, config.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(5), config.RetryDelay);
        Assert.Equal(0.5, config.SamplingRate);
        Assert.Equal(TimeSpan.FromMinutes(2), config.UploadTimeout);
    }

    [Fact]
    public void BuildShouldThrowWhenConfigurationIsInvalid()
    {
        // Arrange — an empty builder now yields a valid local-only configuration, so invalidity
        // must come from a value that is wrong in every mode.
        var builder = new XpingConfigurationBuilder()
            .WithApiEndpoint("not-a-url");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("Invalid configuration", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildShouldSucceedWithNoCredentials()
    {
        // Arrange — zero-config must produce a usable local-only configuration.
        var builder = new XpingConfigurationBuilder();

        // Act
        var config = builder.Build();

        // Assert
        Assert.Equal(XpingMode.LocalOnly, config.ResolveMode());
    }

    [Fact]
    public void BuildShouldThrowWhenApiKeyIsMissingInCloudMode()
    {
        // Arrange
        var builder = new XpingConfigurationBuilder()
            .WithProjectId("test-project")
            .WithMode(XpingMode.Cloud);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("ApiKey is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildShouldSucceedWithoutProjectIdInCloudMode()
    {
        // Arrange — ProjectId is an optional pin, not a credential.
        var builder = new XpingConfigurationBuilder()
            .WithApiKey("test-key")
            .WithMode(XpingMode.Cloud);

        // Act
        var config = builder.Build();

        // Assert
        Assert.Null(config.ProjectId);
        Assert.Equal(XpingMode.Cloud, config.ResolveMode());
    }

    [Fact]
    public void BuildShouldThrowWhenCredentialsAreMissingAndStrictModeIsEnabled()
    {
        // Arrange — strict mode must not silently degrade to local-only.
        var builder = new XpingConfigurationBuilder()
            .WithStrictMode(true);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("ApiKey is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryBuildShouldReturnTrueForValidConfiguration()
    {
        // Arrange
        var builder = new XpingConfigurationBuilder()
            .WithApiKey("test-key")
            .WithProjectId("test-project");

        // Act
        var result = builder.TryBuild(out var config, out var errors);

        // Assert
        Assert.True(result);
        Assert.NotNull(config);
        Assert.Empty(errors);
        Assert.Equal("test-key", config.ApiKey);
        Assert.Equal("test-project", config.ProjectId);
    }

    [Fact]
    public void TryBuildShouldReturnFalseForInvalidConfiguration()
    {
        // Arrange — credentials are optional now, so use a mode that actually requires them.
        var builder = new XpingConfigurationBuilder()
            .WithMode(XpingMode.Cloud);

        // Act
        var result = builder.TryBuild(out var config, out var errors);

        // Assert
        Assert.False(result);
        Assert.Null(config);
        Assert.NotEmpty(errors);
        Assert.Contains("ApiKey is required in Cloud mode.", errors);
    }

    [Fact]
    public void TryBuildShouldReturnTrueForZeroConfiguration()
    {
        // Arrange
        var builder = new XpingConfigurationBuilder();

        // Act
        var result = builder.TryBuild(out var config, out var errors);

        // Assert
        Assert.True(result);
        Assert.NotNull(config);
        Assert.Empty(errors);
        Assert.Equal(XpingMode.LocalOnly, config!.ResolveMode());
    }

    [Fact]
    public void BuilderShouldSupportFluentChaining()
    {
        // Arrange & Act
        var config = new XpingConfigurationBuilder()
            .WithApiKey("test-key")
            .WithProjectId("test-project")
            .WithBatchSize(200)
            .WithEnabled(true)
            .Build();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("test-key", config.ApiKey);
        Assert.Equal("test-project", config.ProjectId);
        Assert.Equal(200, config.BatchSize);
        Assert.True(config.Enabled);
    }

    [Fact]
    public void BuilderShouldValidateBatchSize()
    {
        // Arrange
        var builder = new XpingConfigurationBuilder()
            .WithApiKey("test-key")
            .WithProjectId("test-project")
            .WithBatchSize(1001);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("BatchSize cannot exceed 1000", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuilderShouldValidateSamplingRate()
    {
        // Arrange
        var builder = new XpingConfigurationBuilder()
            .WithApiKey("test-key")
            .WithProjectId("test-project")
            .WithSamplingRate(1.5);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("SamplingRate must be between 0.0 and 1.0", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithCollectNetworkMetrics_ShouldSetProperty()
    {
        // Arrange & Act
        var config = new XpingConfigurationBuilder()
            .WithApiKey("test-key")
            .WithProjectId("test-project")
            .WithCollectNetworkMetrics(false)
            .Build();

        // Assert
        Assert.False(config.CollectNetworkMetrics);
    }

    [Fact]
    public void BuilderShouldValidateNegativeRetryDelay()
    {
        // Arrange
        var builder = new XpingConfigurationBuilder()
            .WithApiKey("test-key")
            .WithProjectId("test-project")
            .WithRetryDelay(TimeSpan.FromSeconds(-1));

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("RetryDelay cannot be negative", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuilderShouldValidateUploadTimeoutZero()
    {
        // Arrange
        var builder = new XpingConfigurationBuilder()
            .WithApiKey("test-key")
            .WithProjectId("test-project")
            .WithUploadTimeout(TimeSpan.Zero);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("UploadTimeout must be greater than zero", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithStrictMode_ShouldSetStrictModeToTrue()
    {
        // Arrange & Act
        var config = new XpingConfigurationBuilder()
            .WithApiKey("test-key")
            .WithProjectId("test-project")
            .WithStrictMode(true)
            .Build();

        // Assert
        Assert.True(config.StrictMode);
    }

    [Fact]
    public void WithStrictMode_DefaultShouldBeFalse()
    {
        // Arrange & Act
        var config = new XpingConfigurationBuilder()
            .WithApiKey("test-key")
            .WithProjectId("test-project")
            .Build();

        // Assert
        Assert.False(config.StrictMode);
    }
}
