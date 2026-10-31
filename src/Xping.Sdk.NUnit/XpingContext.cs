/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework.Interfaces;
using Xping.Sdk.Core;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.NUnit.Retry;
using Xping.Sdk.Shared;

namespace Xping.Sdk.NUnit;

/// <summary>
/// Global context for managing Xping SDK lifecycle in NUnit test assemblies.
/// Provides initialization, test recording, and cleanup functionality.
/// </summary>
public sealed class XpingContext : XpingContextOrchestrator
{
    private static readonly XpingContextSingleton<XpingContext> Context = new(configuration =>
        new XpingContext(CreateHostBuilder(configuration)
            .ConfigureServices(services => services.AddSingleton<IRetryDetector<ITest>, NUnitRetryDetector>())
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
    protected override string TestFramework => LocalSessionProperties.NUnitFramework;

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
    /// Starts the SDK, building the host on first use, and resolves the services
    /// <see cref="XpingTrackAttribute"/> needs to record test executions. Initializes the context when no
    /// one has.
    /// </summary>
    /// <returns>
    /// The services, or <see langword="null"/> when the SDK could not start: tests then run untracked until
    /// the session ends.
    /// </returns>
    /// <exception cref="XpingConfigurationException">Strict mode, and the SDK could not start.</exception>
    internal static XpingAttributeServices? StartAttributeServices()
    {
        // Called before every test. A failure is kept until the session ends rather than retried, and
        // reported, for each of them. No runner channel beyond stderr: the NUnit adapter shows stderr.
        return Context.Start(context => context.ResolveAttributeServices(), report: null, retryNextRun: false);
    }

    /// <summary>
    /// Finalizes the Xping session and uploads all buffered test executions.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task FinalizeAsync() => Context.FinalizeAsync();

    /// <summary>
    /// Finalizes the Xping session and shuts down the context. Call it from the
    /// <c>[OneTimeTearDown]</c> of a <c>[SetUpFixture]</c> in your test assembly's root namespace:
    /// uploads buffered executions, fails the run on a strict-mode network error, and always releases
    /// the underlying host afterward.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NUnit reports an exception from <c>[OneTimeTearDown]</c> as a warning and still lets the run
    /// exit zero, on vstest and on Microsoft.Testing.Platform alike, so a strict-mode network error
    /// fails the process instead. In a vstest test host it terminates the process, after waiting
    /// <see cref="TestHostResultFlushDelay"/> so the test results the host still holds reach
    /// <c>dotnet test</c> and stay in its reports. On Microsoft.Testing.Platform it sets a non-zero exit
    /// code as the process exits, after the platform has written its reports.
    /// </para>
    /// <para>
    /// Every other error is logged, and this method never throws.
    /// </para>
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task FinalizeAndShutdownAsync()
    {
        (TimeSpan resultFlushDelay, Action<XpingNetworkException> failProcess) =
            StrictModeFailure(CurrentRunner == LocalSessionProperties.TestingPlatformRunner);
        return FinalizeAndShutdownAsync(resultFlushDelay, failProcess);
    }

    /// <summary>
    /// How <see cref="FinalizeAndShutdownAsync()"/> fails the process on a strict-mode network error.
    /// </summary>
    /// <param name="onTestingPlatform">Whether the tests run on Microsoft.Testing.Platform.</param>
    /// <returns>How long to wait first, and what fails the process.</returns>
    internal static (TimeSpan ResultFlushDelay, Action<XpingNetworkException> FailProcess) StrictModeFailure(
        bool onTestingPlatform) =>
        onTestingPlatform
            ? (TimeSpan.Zero, FailProcessOnExit)
            : (TestHostResultFlushDelay, FailProcess);

    /// <summary>
    /// How long a strict-mode failure waits before it terminates the process. The vstest test host
    /// sends results in batches of ten, or 1.5 s after the last batch, and results it still holds when
    /// the process dies never reach TRX or any other report.
    /// </summary>
    internal static readonly TimeSpan TestHostResultFlushDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// <see cref="FinalizeAndShutdownAsync()"/> with the wait and the process termination replaced, so
    /// tests can reach the strict-mode path and survive it.
    /// </summary>
    /// <param name="resultFlushDelay">How long to wait before failing the process.</param>
    /// <param name="failProcess">Terminates the process on a strict-mode network error.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async Task FinalizeAndShutdownAsync(
        TimeSpan resultFlushDelay, Action<XpingNetworkException> failProcess)
    {
        XpingNetworkException? runFailure = await Context.EndSessionAsync().ConfigureAwait(false);
        if (runFailure is null)
            return;

        await Task.Delay(resultFlushDelay).ConfigureAwait(false);
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

    internal XpingAttributeServices ResolveAttributeServices()
    {
        return new XpingAttributeServices(
            executionTracker: Services.GetRequiredService<IExecutionTracker>(),
            retryDetector: Services.GetRequiredService<IRetryDetector<ITest>>(),
            identityGenerator: Services.GetRequiredService<ITestIdentityGenerator>(),
            captureStackTraces: CaptureStackTraceConfigurationResolver.ResolveCaptureStackTraces(Services));
    }
}
