/*
 * © 2026 Xping.io. All Rights Reserved.
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
using Xping.Sdk.XUnit.V3.Retry;
using Xunit.v3;

namespace Xping.Sdk.XUnit.V3;

/// <summary>
/// Global context for managing the Xping SDK lifecycle in xUnit v3 test assemblies.
/// Provides initialization, test recording, and cleanup functionality.
/// </summary>
public sealed class XpingContext : XpingContextOrchestrator
{
    private static readonly XpingContextSingleton<XpingContext> Context = new(configuration =>
        new XpingContext(CreateHostBuilder(configuration)
            .ConfigureServices(services => services.AddSingleton<IRetryDetector<IXunitTestCase>, XUnitRetryDetector>())
            .Build()));

    // Detected by XpingTestFramework, which is the only code that runs inside the runner's call stack.
    // Static rather than per instance: the runner is a property of the process, and Initialize() may
    // build the context before or after the framework has looked.
    private static string? _runner;

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
    protected override string TestFramework => LocalSessionProperties.XUnitV3Framework;

    /// <inheritdoc/>
    /// <remarks>
    /// An xUnit v3 test assembly is its own executable on every runner and references
    /// Microsoft.Testing.Platform even when vstest runs it, so the entry assembly the base class reads
    /// would call every run an MTP run.
    /// </remarks>
    protected override string? TestRunner => DetectedRunner;

    /// <summary>Gets the runner <see cref="XpingTestFramework"/> detected, or <see langword="null"/>.</summary>
    internal static string? DetectedRunner => Volatile.Read(ref _runner);

    /// <summary>
    /// Records the test runner <see cref="XpingTestFramework"/> found hosting this process.
    /// </summary>
    /// <param name="runner">The runner, or <see langword="null"/> when it could not tell.</param>
    /// <remarks>
    /// xUnit may build the framework more than once in a process, and a later build can happen off the
    /// runner's call stack. The runner cannot change within a process, so a detection that could not tell
    /// never replaces one that could.
    /// </remarks>
    internal static void RecordRunner(string? runner)
    {
        if (runner != null)
            Volatile.Write(ref _runner, runner);
    }

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
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task FinalizeAsync() => Context.FinalizeAsync();

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
        return Context.EndSessionAsync();
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
        if (FailsThroughExitCode(DetectedRunner))
            FailProcessOnExit(runFailure);
    }

    /// <summary>
    /// Whether a strict-mode failure also has to set the exit code on this runner.
    /// </summary>
    /// <param name="runner">The detected runner, or <see langword="null"/> when it is not known.</param>
    /// <returns><see langword="false"/> only for vstest.</returns>
    /// <remarks>
    /// Only vstest is excluded, because the error message already fails the run there. An unknown runner
    /// may be Microsoft.Testing.Platform, which would otherwise pass the run, and on xUnit's console runner
    /// a second failing signal is harmless.
    /// </remarks>
    internal static bool FailsThroughExitCode(string? runner) =>
        !string.Equals(runner, LocalSessionProperties.VSTestRunner, StringComparison.Ordinal);

    /// <summary>
    /// Disposes the singleton context instance, releasing the flush lock, timer, and host,
    /// then resets the context and forgets the configuration an earlier
    /// <see cref="Initialize(XpingConfiguration)"/> supplied, so <see cref="Initialize()"/> starts from scratch.
    /// Finalization is idempotent, so this is safe to call even when
    /// <see cref="FinalizeAsync"/> was already invoked.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static ValueTask ShutdownAsync() => Context.ShutdownAsync();

    internal XpingExecutorServices ResolveExecutorServices()
    {
        return new XpingExecutorServices(
            executionTracker: Services.GetRequiredService<IExecutionTracker>(),
            retryDetector: Services.GetRequiredService<IRetryDetector<IXunitTestCase>>(),
            identityGenerator: Services.GetRequiredService<ITestIdentityGenerator>(),
            captureStackTraces: CaptureStackTraceConfigurationResolver.ResolveCaptureStackTraces(Services),
            statisticsAccumulator: Services.GetRequiredService<IRunningStatisticsAccumulator>());
    }
}
