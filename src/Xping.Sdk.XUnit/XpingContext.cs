/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xping.Sdk.Core;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.Core.Services.Statistics;
using Xping.Sdk.Shared;
using Xping.Sdk.XUnit.Retry;
using Xunit.Abstractions;

namespace Xping.Sdk.XUnit;

/// <summary>
/// Global context for managing Xping SDK lifecycle in xUnit test assemblies.
/// Provides initialization, test recording, and cleanup functionality.
/// </summary>
public sealed class XpingContext : XpingContextOrchestrator
{
    // The registered retry detector also implements IXUnitRetryDetector, which callers that know the
    // attempt number reach by casting.
    private static readonly XpingContextSingleton<XpingContext> Context = new(configuration =>
        new XpingContext(CreateHostBuilder(configuration)
            .ConfigureServices(services => services.AddSingleton<IRetryDetector<ITest>, XUnitRetryDetector>())
            .Build()));

    /// <summary>
    /// Gets a value indicating whether <see cref="Initialize()"/> has been called and the context
    /// has not yet been shut down. Returns <see langword="true"/> as soon as <see cref="Initialize()"/>
    /// sets the internal lazy wrapper, even before the DI host is fully built on first use.
    /// </summary>
    public static bool IsInitialized => Context.IsInitialized;

    private XpingContext(IHost host) : base(host)
    {
    }

    /// <inheritdoc/>
    protected override string TestFramework => LocalSessionProperties.XUnitFramework;

    /// <summary>
    /// Starts the SDK, building the host on first use, and resolves the services
    /// <see cref="XpingTestFrameworkExecutor"/> needs. Initializes the context when no one has.
    /// </summary>
    /// <param name="report">Also told why the SDK could not start.</param>
    /// <returns>The services, or <see langword="null"/> when the SDK could not start.</returns>
    /// <exception cref="XpingConfigurationException">Strict mode, and the SDK could not start.</exception>
    internal static XpingExecutorServices? StartExecutorServices(Action<string>? report = null) =>
        StartExecutorServices(context => context.ResolveExecutorServices(), report);

    /// <inheritdoc cref="StartExecutorServices(Action{string}?)"/>
    /// <param name="resolve">Resolves the services from the built context.</param>
    /// <param name="report">Also told why the SDK could not start.</param>
    internal static XpingExecutorServices? StartExecutorServices(
        Func<XpingContext, XpingExecutorServices> resolve,
        Action<string>? report)
    {
        // A run that falls back never installs the message sink that ends its session, so the failed
        // context is dropped at once and the next run in a long-lived process tries again.
        return Context.Start(resolve, report, retryNextRun: true);
    }

    /// <summary>
    /// Initializes the Xping context with the default configuration, which loads from appsettings.json
    /// and environment variables, or with the configuration an earlier session in this process was
    /// initialized with.
    /// </summary>
    public static void Initialize()
    {
        Context.Initialize(null);
    }

    /// <summary>
    /// Initializes the Xping context with custom configuration.
    /// </summary>
    /// <param name="configuration">The configuration to use.</param>
    public static void Initialize(XpingConfiguration configuration)
    {
        Context.Initialize(configuration.RequireNotNull());
    }

    /// <summary>
    /// Records a test execution to the collector.
    /// </summary>
    /// <param name="execution">The test execution to record.</param>
    public static void RecordTest(TestExecution execution) => Context.RecordTest(execution);

    /// <summary>
    /// Flushes all pending test executions to Xping Cloud.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task FlushAsync() => Context.FlushAsync();

    /// <summary>
    /// Finalizes the Xping session and uploads all buffered test executions.
    /// </summary>
    /// <returns></returns>
    public static Task FinalizeAsync() => Context.FinalizeAsync();

    /// <summary>
    /// Finalizes the session and shuts down the context. Called once, when the test assembly finishes:
    /// uploads buffered executions and always releases the underlying host afterward.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the only place the xUnit adapter can end a session. The runner never reaches a
    /// <c>Dispose</c> on <see cref="XpingTestFramework"/>: <c>TestFramework.Dispose()</c> is not virtual,
    /// and the runner disposes through <see cref="IDisposable"/>.
    /// </para>
    /// <para>
    /// Never throws. See <see cref="XpingContextOrchestrator.EndSessionAsync(XpingContextOrchestrator?)"/>.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The strict-mode network error the run must fail on, or <see langword="null"/> when there is none.
    /// </returns>
    internal static Task<XpingNetworkException?> FinalizeAndShutdownAsync()
    {
        return Context.EndSessionAsync();
    }

    /// <summary>
    /// Disposes the singleton context instance, releasing the flush lock, timer, and host,
    /// then resets the context and forgets the configuration an earlier
    /// <see cref="Initialize(XpingConfiguration)"/> supplied, so <see cref="Initialize()"/> starts from scratch.
    /// Finalization is idempotent, so this is safe to call even when
    /// <see cref="FinalizeAsync"/> was already invoked.
    /// </summary>
    public static ValueTask ShutdownAsync() => Context.ShutdownAsync();

    internal XpingExecutorServices ResolveExecutorServices()
    {
        return new XpingExecutorServices(
            executionTracker: Services.GetRequiredService<IExecutionTracker>(),
            retryDetector: Services.GetRequiredService<IRetryDetector<ITest>>(),
            identityGenerator: Services.GetRequiredService<ITestIdentityGenerator>(),
            captureStackTraces: CaptureStackTraceConfigurationResolver.ResolveCaptureStackTraces(Services),
            statisticsAccumulator: Services.GetRequiredService<IRunningStatisticsAccumulator>());
    }
}
