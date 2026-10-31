/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.MSTest.Retry;
using Xping.Sdk.Shared;

namespace Xping.Sdk.MSTest;

/// <summary>
/// Global context for managing Xping SDK lifecycle in MSTest test assemblies.
/// Provides initialization, test recording, and cleanup functionality.
/// </summary>
public sealed class XpingContext : XpingContextOrchestrator
{
    private static readonly XpingContextSingleton<XpingContext> Context = new(configuration =>
        new XpingContext(CreateHostBuilder(configuration)
            .ConfigureServices(services => services.AddSingleton<IRetryDetector<TestContext>, MSTestRetryDetector>())
            .Build()));

    private static int _processExitHandlerRegistered;
    private readonly ILogger<XpingContext> _logger;

    /// <summary>
    /// Gets a value indicating whether <see cref="Initialize()"/> has been called and the context
    /// has not yet been shut down. Returns <see langword="true"/> even when building the DI host failed.
    /// </summary>
    public static bool IsInitialized => Context.IsInitialized;

    private XpingContext(IHost host) : base(host)
    {
        _logger = host.Services.GetRequiredService<ILogger<XpingContext>>();
    }

    /// <inheritdoc/>
    protected override string TestFramework => LocalSessionProperties.MSTestFramework;

    /// <summary>
    /// Initializes the Xping context with the default configuration, which loads from appsettings.json
    /// and environment variables, or with the configuration an earlier session in this process was
    /// initialized with.
    /// </summary>
    public static void Initialize()
    {
        EnsureProcessExitSafetyNetRegistered();
        Context.Initialize(null);
        BuildHost();
    }

    /// <summary>
    /// Initializes the Xping context with custom configuration.
    /// </summary>
    /// <param name="configuration">The configuration to use.</param>
    public static void Initialize(XpingConfiguration configuration)
    {
        EnsureProcessExitSafetyNetRegistered();
        Context.Initialize(configuration.RequireNotNull());
        BuildHost();
    }

    // Builds the host here rather than in the first test's [TestInitialize]. MSTest's [Timeout] clock
    // includes [TestInitialize], and XpingTestBase times a test from the end of it, so the build cost
    // would otherwise be missing from a timed-out first test and eat into its budget. A failure is not
    // lost: the Lazy caches the exception, and XpingTestBase hits it again when it resolves its
    // services, where strict mode already fails the run.
    private static void BuildHost()
    {
        try
        {
            _ = Context.Instance;
        }
        catch
        {
            // Rethrown from the cached Lazy on first use; see above.
        }
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
    /// Starts the SDK, building the host unless <see cref="Initialize()"/> already has, and resolves the
    /// services <see cref="XpingTestBase"/> needs to record test executions. Initializes the context when
    /// no one has.
    /// </summary>
    /// <param name="report">Also told why the SDK could not start.</param>
    /// <returns>
    /// The services, or <see langword="null"/> when the SDK could not start: tests then run untracked until
    /// the session ends.
    /// </returns>
    /// <exception cref="XpingConfigurationException">Strict mode, and the SDK could not start.</exception>
    internal static XpingBaseServices? StartBaseServices(Action<string>? report = null)
    {
        // Called before every test. A failure is kept until the session ends rather than retried, and
        // reported, for each of them.
        return Context.Start(context => context.ResolveBaseServices(), report, retryNextRun: false);
    }

    /// <summary>
    /// Finalizes the Xping session and uploads all buffered test executions.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task FinalizeAsync() => Context.FinalizeAsync();

    /// <summary>
    /// Finalizes the Xping session and shuts down the context, matching the behavior MSTest's
    /// <c>[AssemblyCleanup]</c> hook needs: uploads buffered executions, always releases the underlying
    /// host, and then throws a strict-mode network error.
    /// </summary>
    /// <remarks>
    /// MSTest fails the run on an exception from <c>[AssemblyCleanup]</c>: it reports the exception on
    /// the last test result, which then shows as failed, and <c>dotnet test</c> exits non-zero with every
    /// test that ran still in its reports. Every other error is logged and not thrown.
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="XpingNetworkException">Strict mode is on and the upload failed.</exception>
    public static async Task FinalizeAndShutdownAsync()
    {
        XpingNetworkException? runFailure = await FinalizeAndShutdownCoreAsync().ConfigureAwait(false);
        if (runFailure is not null)
            ExceptionDispatchInfo.Capture(runFailure).Throw();
    }

    /// <summary>
    /// <see cref="FinalizeAndShutdownAsync()"/> up to failing the run: returns the strict-mode network
    /// error instead of throwing it, so each caller can fail the run its own way.
    /// </summary>
    /// <returns>
    /// The strict-mode network error the run must fail on, or <see langword="null"/> when there is none.
    /// </returns>
    internal static Task<XpingNetworkException?> FinalizeAndShutdownCoreAsync()
    {
        return Context.EndSessionAsync();
    }

    /// <summary>
    /// Registers a process-exit safety net, once per process, that finalizes an initialized-but-not-yet-
    /// finalized session. MSTest runs <c>[AssemblyCleanup]</c> only when the test assembly declares one,
    /// a hook that throws never reaches the finalize call, and MSTest 3.7.x skipped the hook outright
    /// under method-level parallelization (issue #124; fixed in 3.8, this package's minimum). In every
    /// case an unfinalized session would silently discard every buffered execution. No-op when a
    /// session already finalized normally, since <see cref="ShutdownAsync"/> nulls out
    /// the context before the process exits.
    /// </summary>
    /// <remarks>
    /// This is a last resort, not a substitute for the cleanup hook. On vstest, <c>dotnet test</c>
    /// terminates the test host 100 ms after the run completes (issue #126), and a network upload rarely
    /// fits in that window. Finalization writes local history before it uploads, so the run survives on
    /// disk; the cloud upload needs <c>VSTEST_TESTHOST_SHUTDOWN_TIMEOUT</c> raised in the environment
    /// that runs <c>dotnet test</c>, which only the parent process reads. Microsoft.Testing.Platform runs
    /// in the test process and lets this handler finish, so the upload goes through there.
    /// </remarks>
    private static void EnsureProcessExitSafetyNetRegistered()
    {
        if (Interlocked.CompareExchange(ref _processExitHandlerRegistered, 1, 0) != 0)
            return;

        AppDomain.CurrentDomain.ProcessExit += (_, _) => FinalizeOnProcessExit(FailProcess);
    }

    // Internal, with the process termination replaced, so the adapter's tests can drive the safety net
    // without ending the test process.
    internal static void FinalizeOnProcessExit(Action<XpingNetworkException> failProcess)
    {
        XpingContext? context = Context.Built;
        if (context is null)
            return;

        context._logger.LogWarning(
            "[Xping] Process exiting with session {SessionId} still active: no [AssemblyCleanup] " +
            "finalized Xping. MSTest only runs assembly hooks declared in the test assembly itself, so " +
            "declare one that calls XpingContext.FinalizeAndShutdownAsync(), and check that yours does " +
            "not throw before that call. Finalizing as a safety net: local history is written first " +
            "and should survive. On vstest the cloud upload may not, because vstest terminates the test " +
            "host 100 ms after the run: set VSTEST_TESTHOST_SHUTDOWN_TIMEOUT=5000 in the environment " +
            "that runs `dotnet test` to give it time. Microsoft.Testing.Platform needs no setting.",
            context.SessionId);

        // FinalizeAndShutdownCoreAsync reports its own errors, but an exception out of a ProcessExit
        // handler crashes the host with a stack dump - including one from reporting an error at this late
        // stage.
        XpingNetworkException? runFailure = null;
        try
        {
            runFailure = FinalizeAndShutdownCoreAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Xping] Error finalizing Xping session during process-exit safety net: {ex}");
        }

        // The run is over, so no framework is left to fail it through: a strict-mode network error
        // terminates the process.
        if (runFailure is not null)
            failProcess(runFailure);
    }

    /// <summary>
    /// Disposes the singleton context instance, releasing the flush lock, timer, and host,
    /// then resets the context and forgets the configuration an earlier
    /// <see cref="Initialize(XpingConfiguration)"/> supplied, so <see cref="Initialize()"/> starts from scratch.
    /// Finalization is idempotent, so this is safe to call even when
    /// <see cref="FinalizeAsync"/> was already invoked.
    /// </summary>
    public static ValueTask ShutdownAsync() => Context.ShutdownAsync();

    internal XpingBaseServices ResolveBaseServices()
    {
        return new XpingBaseServices(
            executionTracker: Services.GetRequiredService<IExecutionTracker>(),
            retryDetector: Services.GetRequiredService<IRetryDetector<TestContext>>(),
            identityGenerator: Services.GetRequiredService<ITestIdentityGenerator>(),
            captureStackTraces: CaptureStackTraceConfigurationResolver.ResolveCaptureStackTraces(Services));
    }
}
