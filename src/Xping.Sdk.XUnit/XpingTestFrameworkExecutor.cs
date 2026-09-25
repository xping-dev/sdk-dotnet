/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.Core.Services.Statistics;
using Xping.Sdk.XUnit.Retry;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xping.Sdk.XUnit;

/// <summary>
/// Custom xUnit test framework executor that wraps test execution with Xping tracking.
/// Intercepts test execution messages and records them via XpingMessageSink.
/// </summary>
internal sealed class XpingTestFrameworkExecutor(
    AssemblyName assemblyName,
    ISourceInformationProvider sourceInformationProvider,
    IMessageSink diagnosticMessageSink,
    IExecutionTracker executionTracker,
    IRetryDetector<ITest> retryDetector,
    ITestIdentityGenerator identityGenerator,
    bool captureStackTraces,
    IRunningStatisticsAccumulator statisticsAccumulator,
    TestCaseCensus census) : XunitTestFrameworkExecutor(
        assemblyName,
        sourceInformationProvider,
        diagnosticMessageSink)
{
    // Captured separately (rather than referencing the primary constructor parameter directly in
    // RunTestCases) to avoid CS9107: the parameter is also passed to the base constructor, and the
    // compiler forbids capturing it into the instance as well.
    private readonly string _assemblyName = assemblyName.Name ?? string.Empty;

    /// <summary>
    /// Runs test cases with Xping tracking enabled.
    /// </summary>
    /// <param name="testCases">The test cases to run.</param>
    /// <param name="executionMessageSink">The execution message sink.</param>
    /// <param name="executionOptions">The execution options.</param>
    protected override void RunTestCases(
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink executionMessageSink,
        ITestFrameworkExecutionOptions executionOptions)
    {
        // Materialized once, before anything runs: the census needs the selection, and the runner's
        // enumerable is not promised to be re-enumerable.
        List<IXunitTestCase> selected = [.. testCases];
        RecordSelection(selected);

        // Wrap the message sink with our tracking sink
        var trackingSink = new XpingMessageSink(
            executionMessageSink,
            executionTracker,
            retryDetector,
            identityGenerator,
            captureStackTraces,
            _assemblyName);

        // Retry libraries discard the messages of the attempts they retry, so those attempts are
        // invisible to any message sink. Wrapping such a test case lets Xping observe its retry loop
        // from the inside; every other test case is passed through untouched.
        // Materialized so that a re-enumeration downstream cannot hand out a second set of wrappers,
        // each with its own attempt counter, for the same test cases.
        List<IXunitTestCase> trackedTestCases = [.. selected
            .Select(testCase => (IXunitTestCase?)XpingRetryTestCase.TryWrap(testCase, trackingSink) ?? testCase)];

        // Run tests with tracking enabled
        base.RunTestCases(trackedTestCases, trackingSink, executionOptions);
    }

    /// <summary>
    /// Adds a selection to the census and reports it with the discovery that preceded it.
    /// </summary>
    /// <remarks>
    /// Under <c>dotnet test</c> the runner has finished discovering the whole assembly by the time it
    /// selects, so the census already holds the pre-filter count. An executor asked to run test cases
    /// discovered elsewhere reports the selection alone, and the discovered count stays unknown.
    /// </remarks>
    private void RecordSelection(List<IXunitTestCase> selected)
    {
        if (_assemblyName.Length == 0)
            return;

        census.AddSelected(selected.Count);

        statisticsAccumulator.RecordTestCases(_assemblyName, census.Discovered, census.Selected);
    }
}
