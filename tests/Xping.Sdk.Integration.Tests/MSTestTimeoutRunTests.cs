/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Integration.Tests;

using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xping.Sdk.Integration.Tests.Infrastructure;
using Xunit;

/// <summary>
/// Runs the MSTest sample's <c>[Timeout]</c> tests on each runner and checks that the stored session
/// records them as timeouts. MSTest runs cleanup on a test it gave up on with the outcome still
/// InProgress, so a mocked TestContext can't show what the real framework hands the SDK (#289).
/// </summary>
[Collection(SampleRunner.Collection)]
public sealed class MSTestTimeoutRunTests : IDisposable
{
    private static readonly string[] TimeoutTests =
    [
        "TimeoutTest_UnresponsiveDependency_BlocksForever",
        "TimeoutTest_UnresponsiveDependency_AwaitsForever",
    ];

    private readonly SampleRunner _runner = new();

    public void Dispose() => _runner.Dispose();

    [Theory]
    [InlineData(TestRunner.VsTest)]
    [InlineData(TestRunner.MtpDotnetTest)]
    [InlineData(TestRunner.MtpExecutable)]
    public async Task ATestStoppedByItsTimeoutIsRecordedAsATimeoutWithItsBudget(TestRunner runner)
    {
        (int exitCode, string output) = await _runner
            .RunAsync("SampleApp.MSTest", runner, "FullyQualifiedName~.TimeoutTest_", SampleRunner.LocalOnly)
            .ConfigureAwait(true);

        exitCode.Should().NotBe(0, "MSTest fails a timed-out test. Output:\n{0}", output);

        JsonElement[] executions = _runner.ReadStoredSession()
            .GetProperty("executions").EnumerateArray()
            .ToArray();

        executions.Select(MethodName).Should().BeEquivalentTo(TimeoutTests, "Output:\n{0}", output);

        foreach (JsonElement execution in executions)
        {
            string name = MethodName(execution);
            execution.GetProperty("outcome").GetString().Should().Be("timeout", name);
            Parse(execution.GetProperty("timeoutBudget")).Should().Be(TimeSpan.FromMilliseconds(500), name);

            // MSTest's budget covers [TestInitialize], where the SDK starts timing, so the duration
            // sits on the budget rather than short of it by the SDK's startup cost.
            Parse(execution.GetProperty("duration")).Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(450), name);
        }
    }

    // Not testName, which MSTest takes from the display name.
    private static string MethodName(JsonElement execution) =>
        execution.GetProperty("identity").GetProperty("methodName").GetString()!;

    private static TimeSpan Parse(JsonElement value) =>
        TimeSpan.Parse(value.GetString()!, CultureInfo.InvariantCulture);
}
