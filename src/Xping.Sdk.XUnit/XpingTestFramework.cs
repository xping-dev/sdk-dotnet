/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using System.Runtime.CompilerServices;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Services.Statistics;
using Xping.Sdk.Shared;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xping.Sdk.XUnit;

/// <summary>
/// Custom xUnit test framework that integrates Xping SDK for automatic test tracking.
/// Starts the SDK when the runner asks for an executor. The session ends when the test assembly finishes,
/// in <see cref="XpingMessageSink"/>: the runner never calls a <c>Dispose</c> declared here.
/// </summary>
/// <remarks>
/// Runners also create the framework in processes that only discover tests, so nothing is built until an
/// executor is asked for.
/// </remarks>
public sealed class XpingTestFramework : XunitTestFramework
{
    private readonly Func<XpingContext, XpingExecutorServices> _resolveServices;

    // Shared by the discoverer and the executor this framework creates. The runner asks for the
    // executor before it discovers, so the executor cannot be handed a finished count — only the
    // place where one will be.
    private readonly TestCaseCensus _census = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingTestFramework"/> class.
    /// </summary>
    /// <param name="messageSink">The message sink for diagnostic messages.</param>
    public XpingTestFramework(IMessageSink messageSink)
        : this(messageSink, context => context.ResolveExecutorServices())
    {
    }

    internal XpingTestFramework(IMessageSink messageSink, Func<XpingContext, XpingExecutorServices> resolveServices)
        : base(messageSink)
    {
        _resolveServices = resolveServices;
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
    /// Creates the test framework executor: one that records with Xping, or xUnit's own when the SDK
    /// could not start, so the tests still run.
    /// </summary>
    /// <param name="assemblyName">The assembly containing the tests.</param>
    /// <returns>The test framework executor.</returns>
    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
    {
        RunModuleInitializers(assemblyName.RequireNotNull());

        XpingExecutorServices? services;
        try
        {
            // Started per executor, not once per framework: the session ends with each run, and a
            // long-lived process may ask this framework for another.
            services = XpingContext.StartExecutorServices(_resolveServices, ReportToRunner);
        }
        catch (XpingConfigurationException ex)
        {
            // Strict mode must fail the run visibly, and nothing has run yet, so no result is lost.
            // An exception would not do: the runner reports it and carries on without an executor's
            // worth of results, which a CI step can read as a pass.
            Environment.FailFast($"[Xping] Strict mode configuration error: {ex.Message}", ex);
            throw;
        }

        if (services is null)
            return base.CreateExecutor(assemblyName);

        return new XpingTestFrameworkExecutor(
            assemblyName,
            SourceInformationProvider,
            DiagnosticMessageSink,
            services.ExecutionTracker,
            services.RetryDetector,
            services.IdentityGenerator,
            services.StatisticsAccumulator,
            _census);
    }

    /// <summary>
    /// Runs the test assembly's module initializers before the session starts. xUnit v2 creates this
    /// framework from the assembly's attribute, before any code in the test assembly has run, so a
    /// <c>[ModuleInitializer]</c> that calls <c>XpingContext.Initialize(config)</c> would otherwise run
    /// after the session had started from the default configuration, and its configuration would be
    /// ignored. The runtime runs a module constructor once per process, so this is safe to repeat.
    /// </summary>
    private void RunModuleInitializers(AssemblyName assemblyName)
    {
        try
        {
            RuntimeHelpers.RunModuleConstructor(Assembly.Load(assemblyName).ManifestModule.ModuleHandle);
        }
#pragma warning disable CA1031 // A failing module initializer is the test assembly's error, and xUnit reports it when it runs the tests; it must not stop the SDK from starting.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ReportToRunner($"[Xping] The module initializer of {assemblyName.Name} failed: {ex.Message}");
        }
    }

    // Shown with diagnosticMessages enabled in xunit.runner.json.
    private void ReportToRunner(string message) => DiagnosticMessageSink.OnMessage(new DiagnosticMessage(message));
}
