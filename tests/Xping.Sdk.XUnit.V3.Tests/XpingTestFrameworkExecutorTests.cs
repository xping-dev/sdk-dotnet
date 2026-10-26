/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.XUnit.V3.Probe;
using Xunit.Sdk;

namespace Xping.Sdk.XUnit.V3.Tests;

/// <summary>
/// Runs part of the probe assembly through a real xUnit v3 pipeline, as a filtered run does.
/// </summary>
[Collection(XpingContextCollection.Name)]
public sealed class XpingTestFrameworkExecutorTests
{
    /// <summary>
    /// xUnit v3 filters in the runner, after discovery. A filtered run still discovers the whole
    /// assembly and selects fewer cases, which is what tells it apart from a run after tests were deleted.
    /// </summary>
    [Fact]
    public async Task AFilteredRun_CountsTheWholeAssemblyAsDiscoveredAndTheFilteredCasesAsSelected()
    {
        int discovered = 0;
        using ProbeRun everything = await ProbeRun.RunAsync(onMessage: m =>
        {
            if (m is ITestCaseStarting)
                discovered++;
        });

        using ProbeRun filtered = await ProbeRun.RunAsync(
            filter: testCase => testCase.TestClass?.TestClassName == typeof(RetryTests).FullName);

        Assert.Equal(discovered, filtered.AssemblyStatistics.GetProperty("discoveredTestCases").GetInt32());
        Assert.Equal(1, filtered.AssemblyStatistics.GetProperty("selectedTestCases").GetInt32());
        Assert.Single(filtered.Executions);
    }

    /// <summary>
    /// The session ends with each run. A process that keeps the framework, such as an IDE's test server,
    /// records the next run in a session of its own, with the configuration the first was started with.
    /// </summary>
    [Fact]
    public async Task ASecondRunThroughTheSameFramework_IsRecordedInASessionOfItsOwn()
    {
        using ProbeRun run = await ProbeRun.RunAsync(
            filter: testCase => testCase.TestClass?.TestClassName == typeof(RetryTests).FullName,
            runs: 2);

        Assert.Equal(2, run.SessionCount);
    }

    [Fact]
    public async Task TheSessionEndsBeforeTheRunnerSeesTheAssemblyFinish()
    {
        // Once the runner sees the assembly finish it wraps up the run, and the process exits with
        // the session half written.
        bool? contextAliveWhenFinished = null;

        using ProbeRun run = await ProbeRun.RunAsync(
            filter: testCase => testCase.TestClass?.TestClassName == typeof(RetryTests).FullName,
            onMessage: m =>
            {
                if (m is ITestAssemblyFinished)
                    contextAliveWhenFinished = XpingContext.IsInitialized;
            });

        Assert.False(contextAliveWhenFinished);
    }
}
