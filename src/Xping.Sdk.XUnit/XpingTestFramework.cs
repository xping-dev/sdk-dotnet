/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Services.Statistics;
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
            services.CaptureStackTraces,
            services.StatisticsAccumulator,
            _census);
    }

    // Shown with diagnosticMessages enabled in xunit.runner.json.
    private void ReportToRunner(string message) => DiagnosticMessageSink.OnMessage(new DiagnosticMessage(message));
}
