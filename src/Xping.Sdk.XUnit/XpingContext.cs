/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xping.Sdk.Core;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Statistics;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.Core.Services.Upload;
using Xping.Sdk.Shared;
using Xping.Sdk.XUnit.Retry;
using Xunit.Abstractions;

namespace Xping.Sdk.XUnit;

/// <summary>
/// Global context for managing Xping SDK lifecycle in xUnit test assemblies.
/// Provides initialization, test recording, and cleanup functionality.
/// </summary>
public class XpingContext : XpingContextOrchestrator
{
    private static Lazy<XpingContext>? _instance;
    private readonly ILogger<XpingContext> _logger;

    /// <summary>
    /// Gets a value indicating whether <see cref="Initialize()"/> has been called and the context
    /// has not yet been shut down. Returns <see langword="true"/> as soon as <see cref="Initialize()"/>
    /// sets the internal lazy wrapper, even before the DI host is fully built on first use.
    /// </summary>
    public static bool IsInitialized => _instance != null;

    private XpingContext(IHost host) : base(host)
    {
        _logger = host.Services.GetRequiredService<ILogger<XpingContext>>();
    }

    private static XpingContext CreateInstance(XpingConfiguration? configuration = null)
    {
        IHost host = CreateHostBuilder(configuration)
            .ConfigureServices(services =>
            {
                // Register the xUnit-specific retry detector
                services.AddSingleton<IRetryDetector<ITest>, XUnitRetryDetector>();
            })
            .Build();

        return new XpingContext(host);
    }

    /// <summary>
    /// Resolves the fixed set of services required to construct <see cref="XpingTestFrameworkExecutor"/>.
    /// Must be called after <see cref="Initialize()"/> has been invoked.
    /// </summary>
    /// <returns>A <see cref="XpingExecutorServices"/> holding all executor dependencies.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="Initialize()"/> has not been called before this method.
    /// </exception>
    public static XpingExecutorServices GetExecutorServices()
    {
        if (_instance == null)
            throw new InvalidOperationException(
                "XpingContext must be initialized before resolving executor services. " +
                "Ensure Initialize() has been called.");

        // Accessing .Value materializes the lazy (builds the host) if it has not been
        // accessed yet. This is intentional: Initialize() only registers the Lazy<> wrapper;
        // the host is built on first .Value access, which happens here.
        return _instance.Value.ResolveExecutorServices();
    }

    /// <summary>
    /// Initializes the Xping context with the default configuration.
    /// Loads configuration from appsettings.json or environment variables.
    /// </summary>
    public static void Initialize()
    {
        if (IsInitialized)
            return;

        Lazy<XpingContext> newInstance = new(
            valueFactory: () => CreateInstance(),
            LazyThreadSafetyMode.ExecutionAndPublication);

        Interlocked.CompareExchange(ref _instance, newInstance, null);
    }

    /// <summary>
    /// Initializes the Xping context with custom configuration.
    /// </summary>
    /// <param name="configuration">The configuration to use.</param>
    public static void Initialize(XpingConfiguration configuration)
    {
        if (IsInitialized)
            return;

        Lazy<XpingContext> newInstance = new(
            valueFactory: () => CreateInstance(configuration),
            LazyThreadSafetyMode.ExecutionAndPublication);

        Interlocked.CompareExchange(ref _instance, newInstance, null);
    }

    /// <summary>
    /// Records a test execution to the collector.
    /// </summary>
    /// <param name="execution">The test execution to record.</param>
    public static void RecordTest(TestExecution execution)
    {
        _instance
            .RequireNotNull()
            .Value
            .RecordTestExecution(execution);
    }

    /// <summary>
    /// Flushes all pending test executions to the Cloud Platform.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task FlushAsync()
    {
        return _instance
            .RequireNotNull()
            .Value
            .FlushSessionAsync();
    }

    /// <summary>
    /// Finalizes the Xping session and uploads all buffered test executions.
    /// </summary>
    /// <returns></returns>
    public static Task FinalizeAsync()
    {
        // Only finalize if the host was actually built (i.e., at least one test ran or
        // GetExecutorServices() was called). Avoids building the host just to send an
        // empty session when Initialize() was called but no tests executed.
        if (_instance is not { IsValueCreated: true })
            return Task.CompletedTask;

        return _instance.Value.FinalizeSessionAsync(CancellationToken.None);
    }

    /// <summary>
    /// Disposes the singleton context instance, releasing the flush lock, timer, and host,
    /// then resets the context so <see cref="Initialize()"/> can be called again.
    /// Finalization is idempotent, so this is safe to call even when
    /// <see cref="FinalizeAsync"/> was already invoked.
    /// </summary>
    public static async ValueTask ShutdownAsync()
    {
        // Atomically claim the instance and replace with null so that IsInitialized
        // returns false immediately and Initialize() can be called again afterward.
        Lazy<XpingContext>? instance = Interlocked.Exchange(ref _instance, null);

        if (instance?.IsValueCreated == true)
            await ((IAsyncDisposable)instance.Value).DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override Task OnSessionFinalizingAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Finalizing session");
        return base.OnSessionFinalizingAsync(cancellationToken);
    }

    /// <inheritdoc/>
    protected override Task OnSessionFinalizedAsync(UploadResult result, CancellationToken cancellationToken)
    {
        result = result.RequireNotNull();
        QuickStatistics? stats = result.Success ? result.QuickStatistics : null;

        if (stats != null)
        {
            var parts = new StringBuilder();
            parts.Append($"{stats.Passed} passed");
            if (stats.Failed > 0)        parts.Append($", {stats.Failed} failed");
            if (stats.Skipped > 0)       parts.Append($", {stats.Skipped} skipped");
            if (stats.Inconclusive > 0)  parts.Append($", {stats.Inconclusive} inconclusive");
            if (stats.NotExecuted > 0)   parts.Append($", {stats.NotExecuted} not executed");

            _logger.LogInformation(
                "Total tests recorded: {Total} · {Outcomes} · {TotalDurationMs}ms total",
                stats.Total, parts.ToString(), stats.TotalDurationMs);
        }
        else
        {
            _logger.LogInformation("Session finalized.");
        }

        return base.OnSessionFinalizedAsync(result, cancellationToken);
    }

    private XpingExecutorServices ResolveExecutorServices()
    {
        return new XpingExecutorServices(
            executionTracker: Services.GetRequiredService<IExecutionTracker>(),
            retryDetector: Services.GetRequiredService<IRetryDetector<ITest>>(),
            identityGenerator: Services.GetRequiredService<ITestIdentityGenerator>(),
            logger: Services.GetRequiredService<ILogger<XpingMessageSink>>(),
            captureStackTraces: ResolveCaptureStackTraces(Services));
    }

    private static bool ResolveCaptureStackTraces(IServiceProvider services)
    {
        try
        {
            return services.GetRequiredService<IOptions<XpingConfiguration>>().Value.CaptureStackTraces;
        }
        catch (OptionsValidationException)
        {
            IConfiguration? configuration = services.GetService<IConfiguration>();
            bool? configuredValue = configuration?.GetSection("Xping").GetValue<bool?>("CaptureStackTraces");
            if (configuredValue.HasValue)
            {
                return configuredValue.Value;
            }

            string? legacyEnvValue = Environment.GetEnvironmentVariable("XPING_CAPTURESTACKTRACES");
            if (bool.TryParse(legacyEnvValue, out bool parsedLegacyValue))
            {
                return parsedLegacyValue;
            }

            return true;
        }
    }
}
