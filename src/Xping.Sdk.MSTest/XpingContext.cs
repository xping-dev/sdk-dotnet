/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.Core.Services.Upload;
using Xping.Sdk.MSTest.Retry;
using Xping.Sdk.Shared;

namespace Xping.Sdk.MSTest;

/// <summary>
/// Global context for managing Xping SDK lifecycle in MSTest test assemblies.
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
                // Register the MSTest-specific retry detector
                services.AddSingleton<IRetryDetector<TestContext>, MSTestRetryDetector>();
            })
            .Build();

        return new XpingContext(host);
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
    /// Resolves the fixed set of services required by <see cref="XpingTestBase"/> to record test executions.
    /// Must be called after <see cref="Initialize()"/> has been invoked.
    /// </summary>
    /// <returns>A <see cref="XpingBaseServices"/> holding all base class dependencies.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="Initialize()"/> has not been called before this method.
    /// </exception>
    public static XpingBaseServices GetBaseServices()
    {
        if (_instance == null)
            throw new InvalidOperationException(
                "XpingContext must be initialized before resolving base services. " +
                "Ensure Initialize() has been called.");

        // Accessing .Value materializes the lazy (builds the host) if it has not been
        // accessed yet. This is intentional: Initialize() only registers the Lazy<> wrapper;
        // the host is built on first .Value access, which happens here or in RecordTest().
        return _instance.Value.ResolveBaseServices();
    }

    /// <summary>
    /// Finalizes the Xping session and uploads all buffered test executions.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task FinalizeAsync()
    {
        // Only finalize if the host was actually built (i.e., at least one test ran or
        // GetBaseServices() was called). Avoids building the host just to send an
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
        _logger.LogInformation("Finalizing session {SessionId}", SessionId);
        return base.OnSessionFinalizingAsync(cancellationToken);
    }

    /// <inheritdoc/>
    protected override Task OnSessionFinalizedAsync(UploadResult result, CancellationToken cancellationToken)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));

        _logger.LogInformation(
            "Session {SessionId} finalized. Success: {Success}, Executions: {ExecutionCount}, ReceiptId: {ReceiptId}",
            SessionId, result.Success, result.ExecutionCount, result.ReceiptId);
        return base.OnSessionFinalizedAsync(result, cancellationToken);
    }

    private XpingBaseServices ResolveBaseServices()
    {
        return new XpingBaseServices(
            executionTracker: Services.GetRequiredService<IExecutionTracker>(),
            retryDetector: Services.GetRequiredService<IRetryDetector<TestContext>>(),
            identityGenerator: Services.GetRequiredService<ITestIdentityGenerator>());
    }
}
