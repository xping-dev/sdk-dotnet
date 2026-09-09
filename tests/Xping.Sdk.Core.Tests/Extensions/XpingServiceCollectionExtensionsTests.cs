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
using Xping.Sdk.Core.Services.Serialization;
using Xping.Sdk.Core.Services.Upload;

namespace Xping.Sdk.Core.Tests.Extensions;

// Sets process-wide XPING_* variables, so it shares the collection with every other test that
// reads or writes them. Left in its own collection, xUnit would run it in parallel with those and
// each would see the other's variables.
[Collection("Sequential")]
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
        services.AddXpingCollectors();

        // Act
        services.AddXpingEnvironment();
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetRequiredService<IEnvironmentDetector>());
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
        // Arrange — missing credentials is no longer invalid; a malformed endpoint still is.
        var services = new ServiceCollection();
        var invalidConfig = new XpingConfiguration
        {
            ApiKey = "key",
            ProjectId = "proj",
            ApiEndpoint = "not-a-url"
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => services.AddXping(invalidConfig));
    }

    [Fact]
    public void AddXping_WithNoCredentials_ShouldRegisterLocalOnlyAndSkipHttpClient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddXping(new XpingConfiguration());
        var provider = services.BuildServiceProvider();
        var uploader = provider.GetRequiredService<IXpingUploader>();
        var options = provider.GetRequiredService<IOptions<XpingConfiguration>>().Value;

        // Assert — the no-op uploader stands in for the HTTP pipeline, so local-only mode makes no
        // outbound calls at all.
        Assert.Equal("NoOpXpingUploader", uploader.GetType().Name);
        Assert.Equal(XpingMode.LocalOnly, options.ResolveMode());
    }

    [Fact]
    public void AddXping_WithCredentials_ShouldRegisterHttpUploader()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddXping(new XpingConfiguration { ApiKey = "key", ProjectId = "proj" });
        var provider = services.BuildServiceProvider();
        var uploader = provider.GetRequiredService<IXpingUploader>();
        var options = provider.GetRequiredService<IOptions<XpingConfiguration>>().Value;

        // Assert
        Assert.Equal("XpingUploader", uploader.GetType().Name);
        Assert.Equal(XpingMode.Cloud, options.ResolveMode());
    }

    [Fact]
    public void AddXping_WithApiKeyOnly_ShouldRegisterHttpUploader()
    {
        // Arrange — proves the registration-time mode probe agrees with the runtime one now that
        // ProjectId is out of the credential set. Resolving the uploader also runs the typed-client
        // delegate, which would throw if it still added a null X-Project-Id header.
        var services = new ServiceCollection();

        // Act
        services.AddXping(new XpingConfiguration { ApiKey = "key" });
        var provider = services.BuildServiceProvider();
        var uploader = provider.GetRequiredService<IXpingUploader>();
        var options = provider.GetRequiredService<IOptions<XpingConfiguration>>().Value;

        // Assert
        Assert.Equal("XpingUploader", uploader.GetType().Name);
        Assert.Equal(XpingMode.Cloud, options.ResolveMode());
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

        // Act & Assert — missing ApiKey is only invalid once Cloud mode is requested
        Assert.Throws<InvalidOperationException>(() =>
            services.AddXping(b => b.WithProjectId("proj").WithMode(XpingMode.Cloud)));
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
    public void BindEnvVars_COLLECTLOCALGITAUTHOR_ShouldParseBool()
    {
        using var _key = WithEnv("XPING_APIKEY", "k");
        using var _proj = WithEnv("XPING_PROJECTID", "p");
        using var _ = WithEnv("XPING_COLLECTLOCALGITAUTHOR", "true");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(InMemoryXpingConfig());
        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.True(bound.CollectLocalGitAuthor);
    }

    // ---------------------------------------------------------------------------
    // BindEnvironmentVariablesWithPrefix — applies on the instance path too
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddXpingConfigurationFromInstance_ShouldApplyEnvironmentVariableOverrides()
    {
        using var _key = WithEnv("XPING_APIKEY", "env-key");
        using var _batch = WithEnv("XPING_BATCHSIZE", "777");
        using var _env = WithEnv("XPING_ENVIRONMENT", "env-staging");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromInstance(new XpingConfiguration
        {
            ApiKey = "code-key",
            Environment = "code-staging"
        });

        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal("env-key", bound.ApiKey);
        Assert.Equal(777, bound.BatchSize);
        Assert.Equal("env-staging", bound.Environment);
    }

    [Fact]
    public void AddXpingConfigurationFromInstance_WithNoEnvironmentVariables_ShouldKeepInstanceValues()
    {
        var services = new ServiceCollection();
        services.AddXpingConfigurationFromInstance(new XpingConfiguration
        {
            ApiKey = "code-key",
            BatchSize = 42,
            Environment = "code-staging"
        });

        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal("code-key", bound.ApiKey);
        Assert.Equal(42, bound.BatchSize);
        Assert.Equal("code-staging", bound.Environment);
    }

    [Fact]
    public void AddXpingConfigurationFromInstance_ShouldNotMutateTheCallersInstance()
    {
        using var _key = WithEnv("XPING_APIKEY", "env-key");

        var original = new XpingConfiguration { ApiKey = "code-key" };

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromInstance(original);
        services.BuildServiceProvider().GetRequiredService<IOptions<XpingConfiguration>>();

        Assert.Equal("code-key", original.ApiKey);
    }

    [Fact]
    public void AddXping_WithInstanceAndEnvApiKey_ShouldResolveCloudModeAndRegisterHttpUploader()
    {
        using var _key = WithEnv("XPING_APIKEY", "env-key");

        var services = new ServiceCollection();

        // No API key in code: the mode chosen at registration time has to come from the
        // environment, or a Cloud run would be wired up with the local-only pipeline.
        services.AddXping(new XpingConfiguration());

        var provider = services.BuildServiceProvider();

        Assert.Equal(XpingMode.Cloud,
            provider.GetRequiredService<IOptions<XpingConfiguration>>().Value.ResolveMode());
        Assert.Equal("XpingUploader", provider.GetRequiredService<IXpingUploader>().GetType().Name);
    }

    [Fact]
    public void AddXping_WithBuilderAndEnvApiKey_ShouldNotReportTheKeyAsMissing()
    {
        using var _key = WithEnv("XPING_APIKEY", "env-key");

        var services = new ServiceCollection();

        // StrictMode forces Cloud, which requires an API key. Supplying it through the environment
        // must satisfy that requirement rather than throw.
        services.AddXping(b => b.WithStrictMode(true));

        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal("env-key", bound.ApiKey);
    }

    // ---------------------------------------------------------------------------
    // BindEnvironmentVariablesWithPrefix — an empty variable is "not set"
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BindEnvVars_WithEmptyENVIRONMENT_ShouldNotClearTheConfiguredEnvironment(string value)
    {
        // How a pipeline reaches this state: XPING_ENVIRONMENT: ${{ env.ASPNETCORE_ENVIRONMENT }}
        // expands to empty whenever the source variable is unset - the very pass-through the CI
        // guide recommends.
        using var _ = WithEnv("XPING_ENVIRONMENT", value);

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromInstance(new XpingConfiguration { Environment = "Staging" });

        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal("Staging", bound.Environment);
        Assert.Equal("Staging", bound.ResolvedEnvironment);
    }

    [Fact]
    public void BindEnvVars_WithEmptyAPIKEY_ShouldNotDropTheRunToLocalOnly()
    {
        using var _ = WithEnv("XPING_APIKEY", "");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromInstance(new XpingConfiguration { ApiKey = "configured-key" });

        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        // Clearing the key here would silently stop the suite uploading, which is the loudest
        // possible consequence for the quietest possible cause.
        Assert.Equal("configured-key", bound.ApiKey);
        Assert.Equal(XpingMode.Cloud, bound.ResolveMode());
    }

    [Fact]
    public void BindEnvVars_ShouldTrimSoOneEnvironmentDoesNotBecomeTwo()
    {
        using var _ = WithEnv("XPING_ENVIRONMENT", "  Staging  ");

        var services = new ServiceCollection();
        services.AddXpingConfigurationFromInstance(new XpingConfiguration());

        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal("Staging", bound.Environment);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BindStandardFormat_WithBlankApiKey_ShouldNotDropTheRunToLocalOnly(string value)
    {
        // Xping__ApiKey: ${{ secrets.XPING_APIKEY }} writes an empty string when the secret is
        // missing. Left set-but-empty it resolves LocalOnly and the suite stops uploading silently.
        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Xping:ApiKey"] = value,
            }).Build());

        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Null(bound.ApiKey);
        Assert.Equal(XpingMode.LocalOnly, bound.ResolveMode());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BindStandardFormat_WithBlankEnvironment_ShouldResolveToDefault(string value)
    {
        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Xping:Environment"] = value,
            }).Build());

        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Null(bound.Environment);
        Assert.Equal("Default", bound.ResolvedEnvironment);
    }

    [Fact]
    public void BindStandardFormat_ShouldTrimEnvironment()
    {
        var services = new ServiceCollection();
        services.AddXpingConfigurationFromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Xping:Environment"] = "  Staging  ",
            }).Build());

        var bound = services.BuildServiceProvider()
            .GetRequiredService<IOptions<XpingConfiguration>>().Value;

        Assert.Equal("Staging", bound.Environment);
    }
}
