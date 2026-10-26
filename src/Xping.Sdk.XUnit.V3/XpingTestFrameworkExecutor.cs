/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.Statistics;
using Xunit.Sdk;
using Xunit.v3;

namespace Xping.Sdk.XUnit.V3;

/// <summary>
/// xUnit v3 executor that runs test cases with Xping recording their results.
/// </summary>
internal sealed class XpingTestFrameworkExecutor(
    IXunitTestAssembly testAssembly,
    XpingExecutorServices services,
    TestCaseCensus census) : XunitTestFrameworkExecutor(testAssembly)
{
    /// <inheritdoc/>
    public override ValueTask RunTestCases(
        IReadOnlyCollection<IXunitTestCase> testCases,
        IMessageSink executionMessageSink,
        ITestFrameworkExecutionOptions executionOptions,
        CancellationToken cancellationToken)
    {
        string assemblyName = TestAssembly.Assembly.GetName().Name ?? string.Empty;
        RecordSelection(assemblyName, testCases.Count);

        var trackingSink = new XpingMessageSink(
            executionMessageSink,
            testCases,
            services.ExecutionTracker,
            services.RetryDetector,
            services.IdentityGenerator,
            services.CaptureStackTraces,
            assemblyName);

        return base.RunTestCases(testCases, trackingSink, executionOptions, cancellationToken);
    }

    /// <summary>
    /// Adds a selection to the census and reports it with the discovery that preceded it.
    /// </summary>
    /// <remarks>
    /// On Microsoft.Testing.Platform the runner discovers the whole assembly in this process and filters
    /// it before running, so the census already holds the pre-filter count. Under vstest discovery runs
    /// in a process of its own, and the selection is reported alone.
    /// </remarks>
    private void RecordSelection(string assemblyName, int selected)
    {
        if (assemblyName.Length == 0)
            return;

        census.AddSelected(selected);

        services.StatisticsAccumulator.RecordTestCases(assemblyName, census.Discovered, census.Selected);
    }
}
