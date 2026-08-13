/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.XUnit.Retry;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xping.Sdk.XUnit;

/// <summary>
/// Custom xUnit test framework executor that wraps test execution with Xping tracking.
/// Intercepts test execution messages and records them via XpingMessageSink.
/// </summary>
public sealed class XpingTestFrameworkExecutor(
    AssemblyName assemblyName,
    ISourceInformationProvider sourceInformationProvider,
    IMessageSink diagnosticMessageSink,
    IExecutionTracker executionTracker,
    IRetryDetector<ITest> retryDetector,
    ITestIdentityGenerator identityGenerator,
    ILogger<XpingMessageSink> logger,
    bool captureStackTraces) : XunitTestFrameworkExecutor(
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
        // Wrap the message sink with our tracking sink
        var trackingSink = new XpingMessageSink(
            executionMessageSink,
            executionTracker,
            retryDetector,
            identityGenerator,
            logger,
            captureStackTraces,
            _assemblyName);

        // Retry libraries discard the messages of the attempts they retry, so those attempts are
        // invisible to any message sink. Wrapping such a test case lets Xping observe its retry loop
        // from the inside; every other test case is passed through untouched.
        // Materialized so that a re-enumeration downstream cannot hand out a second set of wrappers,
        // each with its own attempt counter, for the same test cases.
        List<IXunitTestCase> trackedTestCases = [.. testCases
            .Select(testCase => (IXunitTestCase?)XpingRetryTestCase.TryWrap(testCase, trackingSink) ?? testCase)];

        // Run tests with tracking enabled
        base.RunTestCases(trackedTestCases, trackingSink, executionOptions);
    }
}
