/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Integration.Tests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xping.Sdk.Integration.Tests.Infrastructure;
using Xunit;

/// <summary>
/// Runs the MSTest sample's <c>[Timeout]</c> tests on vstest and checks how the stored session records
/// them. What MSTest hands <c>[TestCleanup]</c> for a timed-out test (InProgress for an abandoned test,
/// Failed or Passed for a cooperative one, never a cancelled token) can't be mocked faithfully (#289).
/// <see cref="MtpRunTests"/> covers the plain timeout on Microsoft.Testing.Platform.
/// </summary>
[Collection(SampleRunner.Collection)]
public sealed class MSTestTimeoutRunTests : IDisposable
{
    private readonly SampleRunner _runner = new();

    public void Dispose() => _runner.Dispose();

    [Fact]
    public async Task EachKindOfTimeoutIsRecordedAsMSTestReportsIt()
    {
        (int exitCode, string output) = await _runner
            .RunAsync("SampleApp.MSTest", TestRunner.VsTest, "TestCategory=Timeout", SampleRunner.LocalOnly)
            .ConfigureAwait(true);

        exitCode.Should().NotBe(0, "MSTest fails a timed-out test. Output:\n{0}", output);

        Dictionary<string, JsonElement> executions = _runner.ReadStoredSession()
            .GetProperty("executions").EnumerateArray()
            .ToDictionary(MethodName);

        executions.Keys.Should().BeEquivalentTo(
            [
                "TimeoutTest_UnresponsiveDependency_BlocksForever",
                "TimeoutTest_UnresponsiveDependency_AwaitsForever",
                "CooperativeTimeoutTest_ObservesToken",
                "CooperativeTimeoutTest_IgnoresToken",
            ],
            "Output:\n{0}", output);

        foreach (string name in new[] { "TimeoutTest_UnresponsiveDependency_BlocksForever", "TimeoutTest_UnresponsiveDependency_AwaitsForever" })
        {
            JsonElement execution = executions[name];
            execution.GetProperty("outcome").GetString().Should().Be("timeout", name);
            Parse(execution.GetProperty("timeoutBudget")).Should().Be(TimeSpan.FromMilliseconds(500), name);

            // MSTest's clock also covers [TestInitialize] up to where the SDK starts timing, so the
            // duration sits just under the budget. The margin absorbs that and a slow agent.
            Parse(execution.GetProperty("duration")).Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(400), name);
        }

        // Cooperative timeouts reach cleanup looking like an ordinary failure or pass (known limitation).
        executions["CooperativeTimeoutTest_ObservesToken"].GetProperty("outcome").GetString().Should().Be("failed");
        executions["CooperativeTimeoutTest_IgnoresToken"].GetProperty("outcome").GetString().Should().Be("passed");
    }

    // Not testName, which MSTest takes from the display name.
    private static string MethodName(JsonElement execution) =>
        execution.GetProperty("identity").GetProperty("methodName").GetString()!;

    private static TimeSpan Parse(JsonElement value) =>
        TimeSpan.Parse(value.GetString()!, CultureInfo.InvariantCulture);
}
