/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xping.Sdk.XUnit;

/// <summary>
/// Custom xUnit test framework that integrates Xping SDK for automatic test tracking.
/// Initialize the SDK when the framework is created and cleanup when disposed.
/// </summary>
public sealed class XpingTestFramework : XunitTestFramework
{
    // Initialized in constructor after XpingContext.Initialize() is called, so we can
    // be sure the context is ready and any configuration issues have been surfaced.
    private readonly XpingExecutorServices _services = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingTestFramework"/> class.
    /// </summary>
    /// <param name="messageSink">The message sink for diagnostic messages.</param>
    public XpingTestFramework(IMessageSink messageSink) : base(messageSink)
    {
        XpingContext.Initialize();

        // Resolve and cache services for xUnit test execution. GetExecutorServices()
        // materializes the Lazy<XpingContext> on the first call (building the DI host), so
        // later calls on the same instance are a no-op field read.
        try
        {
            _services = XpingContext.GetExecutorServices();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Xping] SDK initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates the test framework executor.
    /// </summary>
    /// <param name="assemblyName">The assembly containing the tests.</param>
    /// <returns>The test framework executor.</returns>
    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
    {
        return new XpingTestFrameworkExecutor(
            assemblyName,
            SourceInformationProvider,
            DiagnosticMessageSink,
            _services.ExecutionTracker,
            _services.RetryDetector,
            _services.IdentityGenerator,
            _services.Logger);
    }

    /// <summary>
    /// Disposes the test framework and flushes pending test executions.
    /// </summary>
    /// <remarks>
    /// The <c>new</c> modifier is intentional: <c>TestFramework.Dispose()</c> is not virtual and
    /// the xUnit base class exposes no <c>Dispose(bool)</c> hook, so method hiding is the only
    /// way to inject disposal logic. In practice xUnit always constructs this type directly, so
    /// the correct implementation is always called.
    /// </remarks>
    public new void Dispose()
    {
        // Task.Run offloads the async work to a thread-pool thread that has no
        // synchronization context, which prevents the deadlock that would occur if
        // any continuation inside ShutdownAsync tried to resume on a single-threaded
        // context (e.g., the xUnit runner's message-loop thread).
        Task.Run(async () => await XpingContext
                .ShutdownAsync()
                .ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();

        base.Dispose();
    }
}
