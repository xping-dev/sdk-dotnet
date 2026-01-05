/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit;

using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

/// <summary>
/// Custom xUnit test framework that integrates Xping SDK for automatic test tracking.
/// Initialize the SDK when the framework is created and cleanup when disposed.
/// </summary>
public sealed class XpingTestFramework : XunitTestFramework
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XpingTestFramework"/> class.
    /// </summary>
    /// <param name="messageSink">The message sink for diagnostic messages.</param>
    public XpingTestFramework(IMessageSink messageSink)
        : base(messageSink)
    {
        // Initialize Xping SDK when a framework is created
        XpingContext.Initialize();
    }

    /// <summary>
    /// Creates the test framework executor.
    /// </summary>
    /// <param name="assemblyName">The assembly containing the tests.</param>
    /// <returns>The test framework executor.</returns>
    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
    {
        return new XpingTestFrameworkExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
    }

    /// <summary>
    /// Disposes the test framework and flushes pending test executions.
    /// </summary>
    public new void Dispose()
    {
        // Flush and cleanup Xping SDK
        var flushTask = XpingContext.FlushAsync();
        flushTask.GetAwaiter().GetResult();

        var disposeTask = XpingContext.DisposeAsync();
        if (!disposeTask.IsCompleted)
        {
            disposeTask.AsTask().GetAwaiter().GetResult();
        }

        base.Dispose();
    }
}
