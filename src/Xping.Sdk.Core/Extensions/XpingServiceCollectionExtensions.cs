/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

#if !DEBUG
using Xping.Sdk.Core.Extensions.Internals;
#endif

using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Serilog;
using Serilog.Events;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Collector.Internals;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Environment.Internals;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Identity.Internals;
using Xping.Sdk.Core.Services.Network;
using Xping.Sdk.Core.Services.Network.Internals;
using Xping.Sdk.Core.Services.Serialization;
using Xping.Sdk.Core.Services.Serialization.Internals;
using Xping.Sdk.Core.Services.Upload;
using Xping.Sdk.Core.Services.Upload.Internals;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Extensions;

/// <summary>
/// Provides extension methods for adding Xping SDK services to the dependency injection container.
/// </summary>
public static class XpingServiceCollectionExtensions
{
    private const string ConfigurationSectionName = "Xping";
    private const string EnvironmentVariablePrefix = "XPING_";

    /// <summary>
    /// Adds Xping SDK with automatic configuration loading from multiple sources.
    /// Loads from appsettings.json, appsettings.{Environment}.json, and environment variables.
    ///
    /// Supports TWO environment variable formats:
    /// - Standard .NET: Xping__ApiKey, Xping__ProjectId (double underscore for section nesting)
    /// - Legacy/Backward compatible: XPING_APIKEY, XPING_PROJECTID (uppercase with prefix)
    ///
    /// Priority order (highest to lowest):
    /// 1. XPING_* environment variables (e.g., XPING_APIKEY)
    /// 2. Xping__* environment variables (e.g., Xping__ApiKey)
    /// 3. appsettings.{Environment}.json
    /// 4. appsettings.json
    /// 5. Default values in XpingConfiguration class
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="basePath">Optional base path for configuration files. Defaults to the current directory.</param>
    /// <param name="environmentName">
    /// Optional environment name (Development, Production, etc.). Auto-detected from
    /// ASPNETCORE_ENVIRONMENT or DOTNET_ENVIRONMENT if not specified.
    /// </param>
    /// <example>
    /// <code>
    /// // Example usage with both environment variable formats:
    /// // Standard .NET format:
    /// //   export Xping__ApiKey="secret"
    /// //   export Xping__BatchSize=100
    /// // OR Legacy format (backward compatible):
    /// //   export XPING_APIKEY="secret"
    /// //   export XPING_BATCHSIZE=100
    ///
    /// services.AddXping();
    /// </code>
    /// </example>
    public static IServiceCollection AddXping(
        this IServiceCollection services,
        string? basePath = null,
        string? environmentName = null)
    {
        var configuration = BuildXpingConfiguration(basePath, environmentName);
        return services.AddXping(configuration);
    }

    /// <summary>
    /// Adds Xping SDK with configuration binding from an existing IConfiguration.
    /// Supports both environment variable formats:
    /// - Standard .NET: Xping__ApiKey (double underscore for section nesting)
    /// - Legacy format: XPING_APIKEY (uppercase with single underscore prefix)
    /// Priority: XPING_* (highest) > Xping__* > appsettings.{Environment}.json > appsettings.json (lowest)
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance containing the "Xping" section.</param>
    public static IServiceCollection AddXping(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddXpingInfrastructure()
            .AddXpingConfigurationFromConfiguration(configuration)
            .AddXpingEnvironment()
            .AddXpingCollection()
            .AddXpingUpload();
    }

    /// <summary>
    /// Adds Xping SDK with fluent builder configuration.
    /// </summary>
    public static IServiceCollection AddXping(
        this IServiceCollection services,
        Action<XpingConfigurationBuilder> configureBuilder)
    {
        var builder = new XpingConfigurationBuilder();
        configureBuilder
            .RequireNotNull()
            .Invoke(builder);

        if (!builder.TryBuild(out var config, out var errors) || config == null)
        {
            throw new InvalidOperationException($"Invalid Xping configuration: {string.Join(", ", errors)}");
        }

        return services
            .AddXpingInfrastructure()
            .AddXpingConfigurationFromInstance(config)
            .AddXpingEnvironment()
            .AddXpingCollection()
            .AddXpingUpload();
    }

    /// <summary>
    /// Adds Xping SDK with a pre-built configuration instance.
    /// </summary>
    public static IServiceCollection AddXping(
        this IServiceCollection services,
        XpingConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        var errors = configuration.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Invalid configuration: {string.Join(", ", errors)}");
        }

        return services
            .AddXpingInfrastructure()
            .AddXpingConfigurationFromInstance(configuration)
            .AddXpingEnvironment()
            .AddXpingCollection()
            .AddXpingUpload();
    }

    #region Feature: Infrastructure (Logging, Serialization, HTTP)

    /// <summary>
    /// Adds cross-cutting infrastructure services: logging, serialization, HTTP clients.
    /// Use this for minimal setup or when composing services manually.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddXpingInfrastructure(this IServiceCollection services)
    {
        // Configure Serilog with a simple console output.
        // Debug: full exception with stack trace. Release: clean single-line output with exception type and message.
        services.AddLogging(builder =>
        {
            // Clear any existing providers to ensure we don't have duplicate loggers'
            builder.ClearProviders();

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Information()
                // Suppress infrastructure logs (request start/stop, retry or handler pipeline)
                // generated by Microsoft.Extensions.Http, Polly — these are noise for SDK users.
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Fatal)
                .MinimumLevel.Override("Polly", LogEventLevel.Fatal);
#if DEBUG
            loggerConfig.WriteTo.Console(
                outputTemplate: "[Xping {Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture);
#else
            loggerConfig.WriteTo.Console(formatter: new XpingConsoleFormatter());
#endif

            builder.AddSerilog(loggerConfig.CreateLogger(), dispose: true);
        });

        // Register JSON serializer with API options (compact, camelCase)
        services.AddSingleton<IXpingSerializer>(_ => new XpingJsonSerializer(XpingSerializerOptions.ApiOptions));

        return services;
    }

    #endregion

    #region Feature: Options (Binding, Validation)

    /// <summary>
    /// Adds Xping configuration from IConfiguration with validation.
    /// Binds from the "Xping" section and applies XPING_* environment variable overrides.
    /// Use this when you have an existing IConfiguration and want to bind from appsettings.json.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration containing the "Xping" section.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddXpingConfigurationFromConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. Bind from IConfiguration (includes JSON files and Xping__* env vars)
        services.Configure<XpingConfiguration>(
            configuration
                .RequireNotNull()
                .GetSection(key: ConfigurationSectionName));

        // 2. Apply XPING_* environment variables as final override (backward compatibility)
        services.PostConfigure<XpingConfiguration>(options =>
        {
            BindEnvironmentVariablesWithPrefix(options, EnvironmentVariablePrefix);
        });

        // 3. Add validation
        services.AddOptions<XpingConfiguration>()
            .ValidateDataAnnotations()
            .Validate(config => config.Validate().Count == 0);

        return services;
    }

    /// <summary>
    /// Adds Xping configuration from a pre-built instance.
    /// Use this when you have a pre-configured XpingConfiguration object.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The pre-built configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddXpingConfigurationFromInstance(
        this IServiceCollection services,
        XpingConfiguration configuration)
    {
        services.Configure<XpingConfiguration>(options =>
        {
            CopyConfiguration(configuration, options);
        });

        return services;
    }

    #endregion

    #region Feature: Environment Detection

    /// <summary>
    /// Adds environment detection services for detecting OS, runtime, CI platform, containers, etc.
    /// Use this when you need environment diagnostics without the full SDK.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddXpingEnvironment(this IServiceCollection services)
    {
        // Register a network metrics collector for diagnostics used in environment detection
        services.AddSingleton<INetworkMetricsCollector, NetworkMetricsCollector>();
        // Register an environment detector e.g for CI platform detection
        services.AddSingleton<IEnvironmentDetector, EnvironmentDetector>();

        return services;
    }

    #endregion

    #region Feature: Test Execution Collection

    /// <summary>
    /// Adds test execution collection services (collectors, batching, sampling).
    /// Use this when you need a test collection without upload capabilities.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddXpingCollection(this IServiceCollection services)
    {
        // Register a test execution collector for batching and sampling
        services.AddSingleton<ITestExecutionCollector, TestExecutionCollector>();
        // Register an execution tracker for tracking test executions across threads
        services.AddSingleton<IExecutionTracker, ExecutionTracker>();
        // Register a test identity generator for producing stable, deterministic test IDs
        services.AddSingleton<ITestIdentityGenerator, TestIdentityGenerator>();

        return services;
    }

    #endregion

    #region Feature: Test Result Upload

    /// <summary>
    /// Adds test result upload services (HTTP clients, uploaders, retry policies).
    /// Use this when you need upload capabilities without a collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddXpingUpload(this IServiceCollection services)
    {
        // Register HttpClient for XpingUploader with proper configuration and resilience policies
        services.AddHttpClient<IXpingUploader, XpingUploader>((serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IOptions<XpingConfiguration>>().Value;

            // Configure base settings
            client.BaseAddress = new Uri(config.ApiEndpoint);
            client.Timeout = config.UploadTimeout;

            // Configure headers
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-API-Key", config.ApiKey);
            client.DefaultRequestHeaders.Add("X-Project-Id", config.ProjectId);
            client.DefaultRequestHeaders.Add("User-Agent", "Xping-SDK-DotNet/1.0");
        })
        .AddResilienceHandler("xping-upload-resilience", (builder, context) =>
        {
            var config = context.ServiceProvider.GetRequiredService<IOptions<XpingConfiguration>>().Value;

            // Retry strategy with exponential backoff
            builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = config.MaxRetries,
                Delay = config.RetryDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(response =>
                    {
                        // Retry on 5xx errors and 429 (Too Many Requests)
                        var statusCode = (int)response.StatusCode;
                        return statusCode >= 500 || statusCode == 429;
                    })
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
            });

            // Circuit breaker to prevent cascading failures
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5, // Open circuit if 50% of requests fail
                MinimumThroughput = 10, // Minimum 10 requests before evaluating
                BreakDuration = TimeSpan.FromSeconds(30), // Stay open for 30 seconds
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(response => (int)response.StatusCode >= 500)
                    .Handle<HttpRequestException>()
            });
        });

        return services;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Builds an IConfiguration with proper source priority for Xping.
    /// Supports both environment variable formats:
    /// - Standard .NET: Xping__ApiKey (double underscore for nesting)
    /// - Legacy format: XPING_APIKEY (applied via PostConfigure)
    /// Priority: XPING_* (highest via PostConfigure) > Xping__* > appsettings.{Environment}.json > appsettings.json (lowest).
    /// </summary>
    private static IConfiguration BuildXpingConfiguration(string? basePath, string? environmentName)
    {
        var env = environmentName
                  ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                  ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                  ?? "Production";

        return new ConfigurationBuilder()
            .SetBasePath(basePath ?? Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables() // No prefix - use Xping__PropertyName format
            .Build();
    }

    /// <summary>
    /// Binds environment variables with the specified prefix to the configuration options.
    /// This ensures environment variables always have the highest priority.
    /// </summary>
    private static void BindEnvironmentVariablesWithPrefix(XpingConfiguration config, string prefix)
    {
        // API Options
        if (GetEnv("APIENDPOINT") is { } apiEndpoint)
            config.ApiEndpoint = apiEndpoint;

        if (GetEnv("APIKEY") is { } apiKey)
            config.ApiKey = apiKey;

        if (GetEnv("PROJECTID") is { } projectId)
            config.ProjectId = projectId;

        // Batch Options
        if (GetEnv("BATCHSIZE") is { } batchSize && int.TryParse(batchSize, out var bs))
            config.BatchSize = bs;

        if (GetEnv("FLUSHINTERVAL") is { } flushInterval)
        {
            // Try parsing as seconds first, then as TimeSpan
            if (int.TryParse(flushInterval, out var seconds))
                config.FlushInterval = TimeSpan.FromSeconds(seconds);
            else if (TimeSpan.TryParse(flushInterval, out var ts))
                config.FlushInterval = ts;
        }

        // Environment Options
        if (GetEnv("ENVIRONMENT") is { } environment)
            config.Environment = environment;

        if (GetEnv("AUTODETECTCIENVIRONMENT") is { } autoDetect
            && bool.TryParse(autoDetect, out var ad))
            config.AutoDetectCIEnvironment = ad;

        // Feature Flags
        if (GetEnv("ENABLED") is { } enabled && bool.TryParse(enabled, out var e))
            config.Enabled = e;

        if (GetEnv("CAPTURESTACKTRACES") is { } captureStackTraces
            && bool.TryParse(captureStackTraces, out var cst))
            config.CaptureStackTraces = cst;

        if (GetEnv("ENABLECOMPRESSION") is { } enableCompression
            && bool.TryParse(enableCompression, out var ec))
            config.EnableCompression = ec;

        // Retry Options
        if (GetEnv("MAXRETRIES") is { } maxRetries && int.TryParse(maxRetries, out var mr))
            config.MaxRetries = mr;

        if (GetEnv("RETRYDELAY") is { } retryDelay)
        {
            if (int.TryParse(retryDelay, out var seconds))
                config.RetryDelay = TimeSpan.FromSeconds(seconds);
            else if (TimeSpan.TryParse(retryDelay, out var ts))
                config.RetryDelay = ts;
        }

        // Sampling Options
        if (GetEnv("SAMPLINGRATE") is { } samplingRate && double.TryParse(samplingRate, out var sr))
            config.SamplingRate = sr;

        // Timeout Options
        if (GetEnv("UPLOADTIMEOUT") is { } uploadTimeout)
        {
            if (int.TryParse(uploadTimeout, out var seconds))
                config.UploadTimeout = TimeSpan.FromSeconds(seconds);
            else if (TimeSpan.TryParse(uploadTimeout, out var ts))
                config.UploadTimeout = ts;
        }

        // Network Metrics Options
        if (GetEnv("COLLECTNETWORKMETRICS") is { } collectMetrics
            && bool.TryParse(collectMetrics, out var cm))
            config.CollectNetworkMetrics = cm;
        return;

        string? GetEnv(string name) => System.Environment.GetEnvironmentVariable(prefix + name);
    }

    private static void CopyConfiguration(XpingConfiguration source, XpingConfiguration target)
    {
        target.ApiEndpoint = source.ApiEndpoint;
        target.ApiKey = source.ApiKey;
        target.ProjectId = source.ProjectId;
        target.BatchSize = source.BatchSize;
        target.FlushInterval = source.FlushInterval;
        target.Environment = source.Environment;
        target.AutoDetectCIEnvironment = source.AutoDetectCIEnvironment;
        target.Enabled = source.Enabled;
        target.CaptureStackTraces = source.CaptureStackTraces;
        target.EnableCompression = source.EnableCompression;
        target.MaxRetries = source.MaxRetries;
        target.RetryDelay = source.RetryDelay;
        target.SamplingRate = source.SamplingRate;
        target.UploadTimeout = source.UploadTimeout;
        target.CollectNetworkMetrics = source.CollectNetworkMetrics;
    }

    #endregion
}
