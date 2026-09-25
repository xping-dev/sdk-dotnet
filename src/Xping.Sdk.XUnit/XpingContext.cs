/*
 * © 2025 Xping.io. All Rights Reserved.
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
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.Core.Services.Statistics;
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
                // Register the xUnit-specific retry detector. The registered instance also implements
                // IXUnitRetryDetector, which callers that know the attempt number reach by casting.
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
    internal static XpingExecutorServices GetExecutorServices()
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
    /// Finalizes the session and shuts down the context. Called once, when the test assembly finishes:
    /// uploads buffered executions, fails the process fast on a strict-mode network error, and always
    /// releases the underlying host afterward.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the only place the xUnit adapter can end a session. The runner never reaches a
    /// <c>Dispose</c> on <see cref="XpingTestFramework"/>: <c>TestFramework.Dispose()</c> is not virtual,
    /// and the runner disposes through <see cref="IDisposable"/>. A strict-mode network error must be
    /// acted on here: finalization is idempotent, and a second call reports the failure without
    /// throwing again.
    /// </para>
    /// <para>
    /// Never throws, apart from what <paramref name="failFast"/> does: every error is reported here.
    /// </para>
    /// </remarks>
    /// <param name="failFast">
    /// Terminates the process. <see cref="Environment.FailFast(string, Exception)"/> in production;
    /// replaced in tests, which cannot survive the real one.
    /// </param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async Task FinalizeAndShutdownAsync(Action<string, Exception> failFast)
    {
        try
        {
            await FinalizeAsync().ConfigureAwait(false);
        }
        catch (XpingNetworkException ex)
        {
            // xUnit catches and reports exceptions from message sinks without failing the run, so a
            // re-throw would leave the exit code at zero. FailFast aborts the process with a non-zero
            // exit code, which is the correct behavior for strict mode.
            failFast($"[Xping] {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            // Logged here rather than by the caller: the logger lives in the host ShutdownAsync is
            // about to dispose.
            _instance?.Value._logger.LogError(ex, "Error finalizing Xping session on assembly finished");
        }
        finally
        {
            try
            {
                await ShutdownAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // The logger went down with the host, so stderr is all that is left.
                await Console.Error.WriteLineAsync($"[Xping] Error shutting down Xping: {ex}").ConfigureAwait(false);
            }
        }
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
            retryDetector: Services.GetRequiredService<IRetryDetector<ITest>>(),
            identityGenerator: Services.GetRequiredService<ITestIdentityGenerator>(),
            captureStackTraces: CaptureStackTraceConfigurationResolver.ResolveCaptureStackTraces(Services),
            statisticsAccumulator: Services.GetRequiredService<IRunningStatisticsAccumulator>());
    }
}
