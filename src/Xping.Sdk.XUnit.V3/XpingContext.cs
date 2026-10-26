/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Statistics;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.Core.Services.Statistics;
using Xping.Sdk.Core.Services.Upload;
using Xping.Sdk.Shared;
using Xping.Sdk.XUnit.V3.Retry;
using Xunit.v3;

namespace Xping.Sdk.XUnit.V3;

/// <summary>
/// Global context for managing the Xping SDK lifecycle in xUnit v3 test assemblies.
/// Provides initialization, test recording, and cleanup functionality.
/// </summary>
public class XpingContext : XpingContextOrchestrator
{
    private static Lazy<XpingContext>? _instance;

    // Detected by XpingTestFramework, which is the only code that runs inside the runner's call stack.
    // Static rather than per instance: the runner is a property of the process, and Initialize() may
    // build the context before or after the framework has looked.
    private static string? _runner;

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

    /// <inheritdoc/>
    protected override string TestFramework => LocalSessionProperties.XUnitV3Framework;

    /// <inheritdoc/>
    /// <remarks>
    /// An xUnit v3 test assembly is its own executable on every runner and references
    /// Microsoft.Testing.Platform even when vstest runs it, so the entry assembly the base class reads
    /// would call every run an MTP run.
    /// </remarks>
    protected override string? TestRunner => Volatile.Read(ref _runner);

    /// <summary>
    /// Records the test runner <see cref="XpingTestFramework"/> found hosting this process.
    /// </summary>
    /// <param name="runner">The runner, or <see langword="null"/> when it could not tell.</param>
    internal static void RecordRunner(string? runner) => Volatile.Write(ref _runner, runner);

    private static XpingContext CreateInstance(XpingConfiguration? configuration = null)
    {
        IHost host = CreateHostBuilder(configuration)
            .ConfigureServices(services =>
            {
                services.AddSingleton<IRetryDetector<IXunitTestCase>, XUnitRetryDetector>();
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
    internal static XpingExecutorServices GetExecutorServices()
    {
        if (_instance == null)
            throw new InvalidOperationException(
                "XpingContext must be initialized before resolving executor services. " +
                "Ensure Initialize() has been called.");

        // Builds the host on first access: Initialize() only registers the Lazy<> wrapper.
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
    /// Flushes all pending test executions to Xping Cloud.
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
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task FinalizeAsync()
    {
        // Only finalize if the host was actually built, so an Initialize() with no tests behind it
        // does not build a host just to send an empty session.
        if (_instance is not { IsValueCreated: true })
            return Task.CompletedTask;

        return _instance.Value.FinalizeSessionAsync(CancellationToken.None);
    }

    /// <summary>
    /// Finalizes the session and shuts down the context. Called once, when the test assembly finishes:
    /// uploads buffered executions and always releases the underlying host afterward.
    /// </summary>
    /// <remarks>
    /// Never throws. See <see cref="XpingContextOrchestrator.EndSessionAsync(XpingContextOrchestrator?)"/>.
    /// </remarks>
    /// <returns>
    /// The strict-mode network error the run must fail on, or <see langword="null"/> when there is none.
    /// </returns>
    internal static Task<XpingNetworkException?> FinalizeAndShutdownAsync()
    {
        // Claimed before finalizing, so the context finalized is the one shut down, even if Initialize()
        // installs a new one in between.
        Lazy<XpingContext>? instance = Interlocked.Exchange(ref _instance, null);

        return EndSessionAsync(instance is { IsValueCreated: true } ? instance.Value : null);
    }

    /// <summary>
    /// Fails the run on a strict-mode network error where the error message the sink reports is not
    /// enough.
    /// </summary>
    /// <param name="runFailure">The error the session ended with.</param>
    /// <remarks>
    /// xUnit's own runner, which vstest drives, fails the run on an error message. Its
    /// Microsoft.Testing.Platform runner reports the message and still exits zero, so there the run
    /// fails through the exit code, set as the process exits, after the platform has written its
    /// reports.
    /// </remarks>
    internal static void FailRun(XpingNetworkException runFailure)
    {
        if (Volatile.Read(ref _runner) == LocalSessionProperties.TestingPlatformRunner)
            FailProcessOnExit(runFailure);
    }

    /// <summary>
    /// Disposes the singleton context instance, releasing the flush lock, timer, and host,
    /// then resets the context so <see cref="Initialize()"/> can be called again.
    /// Finalization is idempotent, so this is safe to call even when
    /// <see cref="FinalizeAsync"/> was already invoked.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
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
            SessionSummary summary = SessionSummary.From(stats);

            if (summary.Retried)
            {
                _logger.LogInformation(
                    "Total tests recorded: {Total} ({DistinctTests} {TestLabel}, {Retries} {RetryLabel}) · {Outcomes} · exec: {ExecDuration} · wall: {WallClockDuration}{Overhead}",
                    summary.Total, summary.DistinctTests, summary.TestLabel,
                    summary.Retries, summary.RetryLabel, summary.Outcomes,
                    summary.ExecutionDuration, summary.WallClockDuration, summary.Overhead);
            }
            else
            {
                _logger.LogInformation(
                    "Total tests recorded: {Total} · {Outcomes} · exec: {ExecDuration} · wall: {WallClockDuration}{Overhead}",
                    summary.Total, summary.Outcomes,
                    summary.ExecutionDuration, summary.WallClockDuration, summary.Overhead);
            }
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
            retryDetector: Services.GetRequiredService<IRetryDetector<IXunitTestCase>>(),
            identityGenerator: Services.GetRequiredService<ITestIdentityGenerator>(),
            captureStackTraces: CaptureStackTraceConfigurationResolver.ResolveCaptureStackTraces(Services),
            statisticsAccumulator: Services.GetRequiredService<IRunningStatisticsAccumulator>());
    }
}
