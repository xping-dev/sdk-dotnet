/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
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
    ILogger<XpingMessageSink> logger) : XunitTestFrameworkExecutor(
        assemblyName,
        sourceInformationProvider,
        diagnosticMessageSink)
{
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
            logger);

        // Run tests with tracking enabled
        base.RunTestCases(testCases, trackingSink, executionOptions);
    }
}
