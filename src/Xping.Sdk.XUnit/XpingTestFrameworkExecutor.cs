/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit;

using System.Collections.Generic;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

/// <summary>
/// Custom xUnit test framework executor that wraps test execution with Xping tracking.
/// Intercepts test execution messages and records them via XpingMessageSink.
/// </summary>
public sealed class XpingTestFrameworkExecutor : XunitTestFrameworkExecutor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XpingTestFrameworkExecutor"/> class.
    /// </summary>
    /// <param name="assemblyName">The assembly containing the tests.</param>
    /// <param name="sourceInformationProvider">The source information provider.</param>
    /// <param name="diagnosticMessageSink">The diagnostic message sink.</param>
    public XpingTestFrameworkExecutor(
        AssemblyName assemblyName,
        ISourceInformationProvider sourceInformationProvider,
        IMessageSink diagnosticMessageSink)
        : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
    {
    }

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
        var trackingSink = new XpingMessageSink(executionMessageSink);

        // Run tests with tracking enabled
        base.RunTestCases(testCases, trackingSink, executionOptions);
    }
}
