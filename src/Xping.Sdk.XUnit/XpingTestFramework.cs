/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Xping.Sdk.Core.Exceptions;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xping.Sdk.XUnit;

/// <summary>
/// Custom xUnit test framework that integrates Xping SDK for automatic test tracking.
/// Initializes the SDK when the framework is created. The session ends when the test assembly finishes,
/// in <see cref="XpingMessageSink"/>: the runner never calls a <c>Dispose</c> declared here.
/// </summary>
public sealed class XpingTestFramework : XunitTestFramework
{
    // Initialized in constructor after XpingContext.Initialize() is called, so we can
    // be sure the context is ready and any configuration issues have been surfaced.
    private readonly XpingExecutorServices _services = null!;

    // Shared by the discoverer and the executor this framework creates. The runner asks for the
    // executor before it discovers, so the executor cannot be handed a finished count — only the
    // place where one will be.
    private readonly TestCaseCensus _census = new();

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
        catch (XpingConfigurationException ex)
        {
            // Re-throwing from a custom xUnit test framework constructor is insufficient —
            // xUnit runners (e.g. xunit.runner.visualstudio used by `dotnet test`) catch
            // framework constructor exceptions and fall back to default behavior, so tests
            // continue to run untracked. FailFast aborts the process immediately and returns
            // a non-zero exit code, which is the correct behavior for strict mode.
            Environment.FailFast($"[Xping] Strict mode configuration error: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Xping] SDK initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates the test framework discoverer, wrapped to count what it discovers.
    /// </summary>
    /// <param name="assemblyInfo">The assembly to discover tests in.</param>
    /// <returns>The test framework discoverer.</returns>
    protected override ITestFrameworkDiscoverer CreateDiscoverer(IAssemblyInfo assemblyInfo)
    {
        return new XpingTestFrameworkDiscoverer(base.CreateDiscoverer(assemblyInfo), _census);
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
            _services.Logger,
            _services.CaptureStackTraces,
            _services.StatisticsAccumulator,
            _census);
    }
}
