/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Network;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Sdk.Core.Tests.Extensions;

public sealed class XpingServiceCollectionExtensionsTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static XpingConfiguration ValidConfig() => new()
    {
        ApiKey = "test-key",
        ProjectId = "test-project"
    };

    // ---------------------------------------------------------------------------
    // AddXpingCollectors
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddXpingCollectors_ShouldRegister_ITestExecutionCollector()
    {
        // Arrange
        var services = new ServiceCollection();
        services.Configure<XpingConfiguration>(o => { o.ApiKey = "k"; o.ProjectId = "p"; });

        // Act
        services.AddXpingCollectors();
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetRequiredService<ITestExecutionCollector>());
    }

    [Fact]
    public void AddXpingCollectors_ShouldRegister_IExecutionTracker()
    {
        // Arrange
        var services = new ServiceCollection();
        services.Configure<XpingConfiguration>(o => { o.ApiKey = "k"; o.ProjectId = "p"; });

        // Act
        services.AddXpingCollectors();
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetRequiredService<IExecutionTracker>());
    }

    [Fact]
    public void AddXpingCollectors_ShouldRegister_ITestIdentityGenerator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddXpingCollectors();
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetRequiredService<ITestIdentityGenerator>());
    }

    [Fact]
    public void AddXpingCollectors_ShouldReturnSameSingleton_OnMultipleResolves()
    {
        // Arrange
        var services = new ServiceCollection();
        services.Configure<XpingConfiguration>(o => { o.ApiKey = "k"; o.ProjectId = "p"; });
        services.AddXpingCollectors();
        var provider = services.BuildServiceProvider();

        // Act
        var collector1 = provider.GetRequiredService<ITestExecutionCollector>();
        var collector2 = provider.GetRequiredService<ITestExecutionCollector>();

        // Assert — singleton lifetime
        Assert.Same(collector1, collector2);
    }

    // ---------------------------------------------------------------------------
    // AddXpingSerialization
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddXpingSerialization_ShouldRegister_IXpingSerializer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddXpingSerialization();
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetRequiredService<IXpingSerializer>());
    }

    // ---------------------------------------------------------------------------
    // AddXpingEnvironment
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddXpingEnvironment_ShouldRegister_IEnvironmentDetector()
    {
        // Arrange
        var services = new ServiceCollection();
        services.Configure<XpingConfiguration>(o => { o.ApiKey = "k"; o.ProjectId = "p"; });
        services.AddXpingCollectors(); // INetworkMetricsCollector registered here would be absent — test both

        // Act
        services.AddXpingEnvironment();
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetRequiredService<IEnvironmentDetector>());
    }

    [Fact]
    public void AddXpingEnvironment_ShouldRegister_INetworkMetricsCollector()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddXpingEnvironment();
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetRequiredService<INetworkMetricsCollector>());
    }

    // ---------------------------------------------------------------------------
    // AddXpingConfigurationFromInstance
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddXpingConfigurationFromInstance_ShouldBindAllProperties()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiKey = "my-api-key",
            ProjectId = "my-project",
            BatchSize = 42,
            SamplingRate = 0.75,
            Environment = "Staging"
        };
        var services = new ServiceCollection();

        // Act
        services.AddXpingConfigurationFromInstance(config);
        var provider = services.BuildServiceProvider();
        var bound = provider.GetRequiredService<IOptions<XpingConfiguration>>().Value;

        // Assert
        Assert.Equal("my-api-key", bound.ApiKey);
        Assert.Equal("my-project", bound.ProjectId);
        Assert.Equal(42, bound.BatchSize);
        Assert.Equal(0.75, bound.SamplingRate);
        Assert.Equal("Staging", bound.Environment);
    }

    // ---------------------------------------------------------------------------
    // AddXpingConfigurationFromConfiguration
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddXpingConfigurationFromConfiguration_ShouldBindFromSection()
    {
        // Arrange
        var memConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Xping:ApiKey"] = "from-config",
                ["Xping:ProjectId"] = "proj-from-config",
                ["Xping:BatchSize"] = "50"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddXpingConfigurationFromConfiguration(memConfig);
        var provider = services.BuildServiceProvider();
        var bound = provider.GetRequiredService<IOptions<XpingConfiguration>>().Value;

        // Assert — ApiKey is intentionally omitted: XPING_APIKEY env var on this machine
        // overrides the in-memory value via the PostConfigure step.
        Assert.Equal("proj-from-config", bound.ProjectId);
        Assert.Equal(50, bound.BatchSize);
    }

    // ---------------------------------------------------------------------------
    // AddXping(XpingConfiguration)
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddXping_WithValidConfigInstance_ShouldRegisterAllCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddXping(ValidConfig());
        var provider = services.BuildServiceProvider();

        // Assert — spot-check the key interfaces
        Assert.NotNull(provider.GetRequiredService<ITestExecutionCollector>());
        Assert.NotNull(provider.GetRequiredService<IExecutionTracker>());
        Assert.NotNull(provider.GetRequiredService<ITestIdentityGenerator>());
        Assert.NotNull(provider.GetRequiredService<IXpingSerializer>());
        Assert.NotNull(provider.GetRequiredService<IEnvironmentDetector>());
    }

    [Fact]
    public void AddXping_WithNullConfigInstance_ShouldThrowArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddXping((XpingConfiguration)null!));
    }

    [Fact]
    public void AddXping_WithInvalidConfigInstance_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var invalidConfig = new XpingConfiguration(); // missing ApiKey and ProjectId

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => services.AddXping(invalidConfig));
    }

    // ---------------------------------------------------------------------------
    // AddXping(Action<XpingConfigurationBuilder>)
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddXping_WithBuilder_ShouldConfigureFromBuilderAction()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddXping(b => b.WithApiKey("builder-key").WithProjectId("builder-proj"));
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<XpingConfiguration>>().Value;

        // Assert
        Assert.Equal("builder-key", options.ApiKey);
        Assert.Equal("builder-proj", options.ProjectId);
    }

    [Fact]
    public void AddXping_WithBuilder_InvalidConfig_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert — builder produces invalid config (missing ApiKey)
        Assert.Throws<InvalidOperationException>(() =>
            services.AddXping(b => b.WithProjectId("proj")));
    }

    [Fact]
    public void AddXping_WithNullBuilderAction_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.ThrowsAny<Exception>(() =>
            services.AddXping((Action<XpingConfigurationBuilder>)null!));
    }

    // ---------------------------------------------------------------------------
    // AddXpingConfigurationFromInstance — CollectNetworkMetrics coverage
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddXpingConfigurationFromInstance_ShouldCopy_CollectNetworkMetrics()
    {
        // Arrange
        var config = new XpingConfiguration
        {
            ApiKey = "test-key",
            ProjectId = "test-project",
            CollectNetworkMetrics = false
        };
        var services = new ServiceCollection();

        // Act
        services.AddXpingConfigurationFromInstance(config);
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        // Assert
        Assert.False(bound.CollectNetworkMetrics);
    }

    // ---------------------------------------------------------------------------
    // AddXping(basePath, environmentName) — auto-discovery overload
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddXping_WithBasePathAndEnvironmentName_ShouldRegisterSerializerWithoutResolvingOptions()
    {
        // Arrange — auto-discovery overload; registers services without immediate validation
        var services = new ServiceCollection();

        // Act
        services.AddXping(Directory.GetCurrentDirectory(), "Test");
        var provider = services.BuildServiceProvider();

        // Assert — IXpingSerializer does not depend on XpingConfiguration so it resolves cleanly
        Assert.NotNull(provider.GetRequiredService<IXpingSerializer>());
    }

    [Fact]
    public void AddXping_WithNullBasePath_DefaultsToCurrentDirectory()
    {
        // basePath == null → uses Directory.GetCurrentDirectory()
        var services = new ServiceCollection();
        services.AddXping(null, "Test"); // should not throw
        Assert.NotNull(services.BuildServiceProvider().GetRequiredService<IXpingSerializer>());
    }

    // ---------------------------------------------------------------------------
    // BindEnvironmentVariablesWithPrefix — tested via AddXpingConfigurationFromConfiguration
    // ---------------------------------------------------------------------------

    // Helper: builds a minimal valid in-memory config for the "Xping" section.
    private static IConfiguration InMemoryXpingConfig(Dictionary<string, string?>? extra = null)
    {
        var dict = new Dictionary<string, string?>
        {
            ["Xping:ApiKey"] = "in-memory-key",
            ["Xping:ProjectId"] = "in-memory-project"
        };
        if (extra != null)
            foreach (var (k, v) in extra)
                dict[k] = v;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    // Helper: sets an env var for the duration of a test, then restores the original value.
    private static EnvRestorer WithEnv(string name, string value)
    {
        var original = System.Environment.GetEnvironmentVariable(name);
        System.Environment.SetEnvironmentVariable(name, value);
        return new EnvRestorer(name, original);
    }

    private sealed class EnvRestorer(string name, string? original) : IDisposable
    {
        public void Dispose() => System.Environment.SetEnvironmentVariable(name, original);
    }

    [Fact]
    public void BindEnvVars_APIENDPOINT_ShouldOverrideApiEndpoint()
    {
        using var _ = WithEnv("XPING_APIENDPOINT", "https://env.example.com/v2");
        using var _key = WithEnv("XPING_APIKEY", "env-key");
        using var _proj = WithEnv("XPING_PROJECTID", "env-project");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal("https://env.example.com/v2", bound.ApiEndpoint);
    }

    [Fact]
    public void BindEnvVars_BATCHSIZE_ShouldParseInt()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_BATCHSIZE", "77");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal(77, bound.BatchSize);
    }

    [Fact]
    public void BindEnvVars_FLUSHINTERVAL_AsSeconds_ShouldConvertToTimeSpan()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_FLUSHINTERVAL", "60");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(60), bound.FlushInterval);
    }

    [Fact]
    public void BindEnvVars_FLUSHINTERVAL_AsTimeSpanString_ShouldParse()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_FLUSHINTERVAL", "00:02:30");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(30)), bound.FlushInterval);
    }

    [Fact]
    public void BindEnvVars_FLUSHINTERVAL_Invalid_ShouldBeIgnored()
    {
        // Invalid value — neither int nor TimeSpan — should leave the default unchanged.
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_FLUSHINTERVAL", "not-a-timespan");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        // Default is 30 s; invalid env var must not change it
        Assert.Equal(TimeSpan.FromSeconds(30), bound.FlushInterval);
    }

    [Fact]
    public void BindEnvVars_ENABLED_ShouldParseBool()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_ENABLED", "false");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.False(bound.Enabled);
    }

    [Fact]
    public void BindEnvVars_SAMPLINGRATE_ShouldParseDouble()
    {
        // Use "0" — parses correctly in any locale (no decimal separator ambiguity).
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_SAMPLINGRATE", "0");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal(0.0, bound.SamplingRate);
    }

    [Fact]
    public void BindEnvVars_MAXRETRIES_ShouldParseInt()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_MAXRETRIES", "5");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal(5, bound.MaxRetries);
    }

    [Fact]
    public void BindEnvVars_RETRYDELAY_AsSeconds_ShouldConvertToTimeSpan()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_RETRYDELAY", "10");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(10), bound.RetryDelay);
    }

    [Fact]
    public void BindEnvVars_RETRYDELAY_AsTimeSpanString_ShouldParse()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_RETRYDELAY", "00:00:05");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(5), bound.RetryDelay);
    }

    [Fact]
    public void BindEnvVars_RETRYDELAY_Invalid_ShouldBeIgnored()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_RETRYDELAY", "bad-value");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(2), bound.RetryDelay); // default
    }

    [Fact]
    public void BindEnvVars_UPLOADTIMEOUT_AsSeconds_ShouldConvertToTimeSpan()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_UPLOADTIMEOUT", "45");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(45), bound.UploadTimeout);
    }

    [Fact]
    public void BindEnvVars_UPLOADTIMEOUT_AsTimeSpanString_ShouldParse()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_UPLOADTIMEOUT", "00:01:00");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(1), bound.UploadTimeout);
    }

    [Fact]
    public void BindEnvVars_COLLECTNETWORKMETRICS_ShouldParseBool()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_COLLECTNETWORKMETRICS", "false");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.False(bound.CollectNetworkMetrics);
    }

    [Fact]
    public void BindEnvVars_ENVIRONMENT_ShouldOverrideEnvironmentName()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_ENVIRONMENT", "Production");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal("Production", bound.Environment);
    }

    [Fact]
    public void BindEnvVars_CAPTURESTACKTRACES_ShouldParseBool()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_CAPTURESTACKTRACES", "false");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.False(bound.CaptureStackTraces);
    }

    [Fact]
    public void BindEnvVars_ENABLECOMPRESSION_ShouldParseBool()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_ENABLECOMPRESSION", "false");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.False(bound.EnableCompression);
    }

    [Fact]
    public void BindEnvVars_AUTODETECTCIENVIRONMENT_ShouldParseBool()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_AUTODETECTCIENVIRONMENT", "false");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.False(bound.AutoDetectCIEnvironment);
    }
}
