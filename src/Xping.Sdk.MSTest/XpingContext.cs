/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Statistics;
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
    private static int _processExitHandlerRegistered;
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
        EnsureProcessExitSafetyNetRegistered();

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
        EnsureProcessExitSafetyNetRegistered();

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
    /// Finalizes the Xping session and shuts down the context, matching the behavior MSTest's
    /// <c>[AssemblyCleanup]</c> hook needs: uploads buffered executions, fails the process fast on a
    /// strict-mode network error, and always releases the underlying host afterward.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task FinalizeAndShutdownAsync()
    {
        try
        {
            try
            {
                await FinalizeAsync().ConfigureAwait(false);
            }
            catch (XpingNetworkException ex)
            {
                // FailFast aborts the process immediately, which is the correct behavior for strict mode
                // where observability must be guaranteed. Re-throwing here may not cause the CI pipeline
                // to fail with a non-zero exit code.
                Environment.FailFast($"[Xping] {ex.Message}", ex);
            }
        }
        finally
        {
            // Runs even if FinalizeAsync throws something other than XpingNetworkException, so the
            // "always releases the underlying host" guarantee above actually holds.
            await ShutdownAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Registers a process-exit safety net, once per process, that finalizes an initialized-but-not-yet-
    /// finalized session. MSTest skips <c>[AssemblyCleanup]</c> under some configurations (e.g. method-
    /// level parallelization — see issue #124), which otherwise silently discards every buffered
    /// execution. No-op when a session already finalized normally, since <see cref="ShutdownAsync"/>
    /// nulls out <see cref="_instance"/> before the process exits.
    /// </summary>
    private static void EnsureProcessExitSafetyNetRegistered()
    {
        if (Interlocked.CompareExchange(ref _processExitHandlerRegistered, 1, 0) != 0)
            return;

        AppDomain.CurrentDomain.ProcessExit += (_, _) => FinalizeOnProcessExit();
    }

    private static void FinalizeOnProcessExit()
    {
        Lazy<XpingContext>? instance = _instance;
        if (instance is not { IsValueCreated: true })
            return;

        XpingContext context = instance.Value;
        context._logger.LogWarning(
            "[Xping] Process exiting with session {SessionId} still active. The test framework's " +
            "assembly cleanup hook did not run (e.g. MSTest with method-level parallelization). " +
            "Finalizing now as a safety net.",
            context.SessionId);

        try
        {
            FinalizeAndShutdownAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            context._logger.LogError(ex, "[Xping] Error finalizing Xping session during process-exit safety net");
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
            // A retried test is recorded once per attempt, so the outcome counters count attempts.
            // When any retry happened, report the test-level outcomes instead — a suite that recovered
            // on retry is green — and keep the attempt count visible beside them.
            bool retried = stats.DistinctTests > 0 && stats.DistinctTests != stats.Total;

            int passed = retried ? stats.FinalPassed : stats.Passed;
            int failed = retried ? stats.FinalFailed : stats.Failed;
            int skipped = retried ? stats.FinalSkipped : stats.Skipped;
            int inconclusive = retried ? stats.FinalInconclusive : stats.Inconclusive;
            int notExecuted = retried ? stats.FinalNotExecuted : stats.NotExecuted;

            var parts = new StringBuilder();
            parts.Append($"{passed} passed");
            if (failed > 0)        parts.Append($", {failed} failed");
            if (skipped > 0)       parts.Append($", {skipped} skipped");
            if (inconclusive > 0)  parts.Append($", {inconclusive} inconclusive");
            if (notExecuted > 0)   parts.Append($", {notExecuted} not executed");

            // TotalDurationMs sums each test's own execution time; WallClockDurationMs is the
            // elapsed time for the whole session (includes fixture setup/teardown, discovery, etc.).
            if (retried)
            {
                int retries = stats.Total - stats.DistinctTests;

                _logger.LogInformation(
                    "Total tests recorded: {Total} ({DistinctTests} {TestLabel}, {Retries} {RetryLabel}) · {Outcomes} · exec: {TotalDurationMs}ms · wall: {WallClockDurationMs}ms",
                    stats.Total, stats.DistinctTests, stats.DistinctTests == 1 ? "test" : "tests",
                    retries, retries == 1 ? "retry" : "retries",
                    parts.ToString(), stats.TotalDurationMs, stats.WallClockDurationMs);
            }
            else
            {
                _logger.LogInformation(
                    "Total tests recorded: {Total} · {Outcomes} · exec: {TotalDurationMs}ms · wall: {WallClockDurationMs}ms",
                    stats.Total, parts.ToString(), stats.TotalDurationMs, stats.WallClockDurationMs);
            }
        }
        else
        {
            _logger.LogInformation("Session finalized.");
        }

        return base.OnSessionFinalizedAsync(result, cancellationToken);
    }

    private XpingBaseServices ResolveBaseServices()
    {
        return new XpingBaseServices(
            executionTracker: Services.GetRequiredService<IExecutionTracker>(),
            retryDetector: Services.GetRequiredService<IRetryDetector<TestContext>>(),
            identityGenerator: Services.GetRequiredService<ITestIdentityGenerator>(),
            captureStackTraces: CaptureStackTraceConfigurationResolver.ResolveCaptureStackTraces(Services));
    }
}
