/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Integration.Tests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Xping.Sdk.Integration.Tests.Infrastructure;
using Xunit;

/// <summary>
/// Runs each sample in strict mode against an endpoint that refuses every connection, on each test
/// runner it supports: the run has to fail, and the TRX still has to list every test that ran.
/// </summary>
/// <remarks>
/// End to end on purpose. What is under test is how the test framework, its runner and <c>dotnet
/// test</c> react to the way each Xping adapter fails the run, and no unit test reaches them.
/// </remarks>
[Collection(SampleRunner.Collection)]
public sealed class StrictModeRunTests : IDisposable
{
    private static readonly Dictionary<string, string> StrictModeAgainstClosedPort = new()
    {
        ["XPING_MODE"] = "Cloud",
        ["XPING_APIKEY"] = "test-key",
        ["XPING_PROJECTID"] = "test-project",
        // Port 9 (discard) is closed on loopback, so every upload is refused immediately.
        ["XPING_APIENDPOINT"] = "http://127.0.0.1:9/v1",
        ["XPING_MAXRETRIES"] = "0",
        ["XPING_STRICTMODE"] = "true",
    };

    private readonly SampleRunner _runner = new();

    public void Dispose() => _runner.Dispose();

    // Each filter selects two passing tests. MSTest reports an [AssemblyCleanup] failure on the last
    // test result, on either runner, so one of them shows as failed there; the others report the
    // failure on their own. xUnit v2 has no Microsoft.Testing.Platform runner.
    [Theory]
    [InlineData("SampleApp.XUnit", TestRunner.VsTest, "FullyQualifiedName~PassingTestIsTracked|FullyQualifiedName~AnotherPassingTest", 2, 0)]
    [InlineData("SampleApp.NUnit", TestRunner.VsTest, "Name~Passing", 2, 0)]
    [InlineData("SampleApp.NUnit", TestRunner.MtpDotnetTest, "Name~Passing", 2, 0)]
    [InlineData("SampleApp.NUnit", TestRunner.MtpExecutable, "Name~Passing", 2, 0)]
    [InlineData("SampleApp.MSTest", TestRunner.VsTest, "Name~Add_TwoNumbers|Name~Subtract_TwoNumbers", 1, 1)]
    [InlineData("SampleApp.MSTest", TestRunner.MtpDotnetTest, "Name~Add_TwoNumbers|Name~Subtract_TwoNumbers", 1, 1)]
    [InlineData("SampleApp.MSTest", TestRunner.MtpExecutable, "Name~Add_TwoNumbers|Name~Subtract_TwoNumbers", 1, 1)]
    public async Task StrictModeUploadFails_FailsRunAndKeepsEveryResult(
        string sample, TestRunner runner, string filter, int expectedPassed, int expectedFailed)
    {
        (int exitCode, string output) = await _runner
            .RunAsync(sample, runner, filter, StrictModeAgainstClosedPort)
            .ConfigureAwait(true);

        exitCode.Should().NotBe(0, "strict mode must fail the run on an upload error. Output:\n{0}", output);

        XElement counters = _runner.ReadTrxCounters();
        counters.Attribute("total")!.Value.Should().Be("2", "every test that ran stays in the report. Output:\n{0}", output);
        counters.Attribute("passed")!.Value.Should().Be(expectedPassed.ToString(CultureInfo.InvariantCulture), "Output:\n{0}", output);
        counters.Attribute("failed")!.Value.Should().Be(expectedFailed.ToString(CultureInfo.InvariantCulture), "Output:\n{0}", output);
        output.Should().Contain("Xping network error in strict mode");
    }
}
