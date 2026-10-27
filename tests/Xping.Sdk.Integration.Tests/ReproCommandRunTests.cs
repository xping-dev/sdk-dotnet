/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Integration.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xping.Cli.Report;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Integration.Tests.Infrastructure;
using Xunit;

/// <summary>
/// Records a sample test, builds its repro command from what the store holds, and runs that command
/// as a reader would: from the repository root, through a shell. The command has to select exactly
/// the recorded test and write no session. Pins the runner behaviour the command relies on (escaping,
/// data rows, <c>-e XPING_ENABLED=false</c>) against framework and SDK upgrades.
/// </summary>
[Collection(SampleRunner.Collection)]
public sealed class ReproCommandRunTests : IDisposable
{
    private static readonly Dictionary<string, string> LocalOnly = new() { ["XPING_MODE"] = "LocalOnly" };

    private readonly SampleRunner _recording = new();
    private readonly SampleRunner _repro = new();

    public void Dispose()
    {
        _repro.Dispose();
        _recording.Dispose();
    }

    // The case is picked from the recorded names by `chosen`: NUnit stores one name per case, MSTest and
    // xUnit one per method, which runs every row. The second NUnit string case is the one a filter
    // that let MSBuild decode %20 would run instead.
    [Theory]
    [InlineData("SampleApp.NUnit", TestRunner.VsTest, "ParameterizedTestIsTracked", "(1,2,3)", new[] { "ParameterizedTestIsTracked(1,2,3)" })]
    [InlineData("SampleApp.NUnit", TestRunner.VsTest, "StringParameterIsTracked", "(\"50%20off\")", new[] { "StringParameterIsTracked(\"50%20off\")" })]
    [InlineData("SampleApp.NUnit", TestRunner.MtpExecutable, "ParameterizedTestIsTracked", "(1,2,3)", new[] { "ParameterizedTestIsTracked(1,2,3)" })]
    [InlineData("SampleApp.NUnit", TestRunner.MtpExecutable, "StringParameterIsTracked", "(\"50%20off\")", new[] { "StringParameterIsTracked(\"50%20off\")" })]
    [InlineData("SampleApp.MSTest", TestRunner.VsTest, "Add_MultipleInputs_ReturnsExpectedSum", "", new[] { "Add_MultipleInputs_ReturnsExpectedSum (2,3,5)", "Add_MultipleInputs_ReturnsExpectedSum (10,5,15)", "Add_MultipleInputs_ReturnsExpectedSum (-1,1,0)" })]
    [InlineData("SampleApp.MSTest", TestRunner.MtpExecutable, "Add_MultipleInputs_ReturnsExpectedSum", "", new[] { "Add_MultipleInputs_ReturnsExpectedSum (2,3,5)", "Add_MultipleInputs_ReturnsExpectedSum (10,5,15)", "Add_MultipleInputs_ReturnsExpectedSum (-1,1,0)" })]
    [InlineData("SampleApp.XUnit", TestRunner.VsTest, "ParameterizedTestIsTracked", "", new[] { "SampleApp.XUnit.SampleTests.ParameterizedTestIsTracked(a: 1, b: 2, expected: 3)", "SampleApp.XUnit.SampleTests.ParameterizedTestIsTracked(a: 5, b: 5, expected: 10)", "SampleApp.XUnit.SampleTests.ParameterizedTestIsTracked(a: 0, b: 0, expected: 0)" })]
    public async Task TheReproCommandRunsTheRecordedTestAndWritesNoSession(
        string sample, TestRunner runner, string method, string chosen, string[] expectedResults)
    {
        (int exitCode, string output) = await _recording
            .RunAsync(sample, runner, $"FullyQualifiedName~.{method}", LocalOnly)
            .ConfigureAwait(true);
        exitCode.Should().Be(0, "Output:\n{0}", output);

        JsonElement session = _recording.ReadStoredSession();
        string fullyQualifiedName = session.GetProperty("executions").EnumerateArray()
            .Select(execution => execution.GetProperty("identity").GetProperty("fullyQualifiedName").GetString()!)
            .Distinct(StringComparer.Ordinal)
            .Should().ContainSingle(name => name.EndsWith(method + chosen, StringComparison.Ordinal)).Subject;

        Repro repro = ReproCommand.For(FlakyFinding(fullyQualifiedName, sample), Target(session))!;

        // Appended after the command, so it runs as printed; the TRX names the tests the filter selected.
        string trx = runner == TestRunner.VsTest
            ? $" --logger trx --results-directory {ShellQuote.Of(_repro.TrxDirectory)}"
            : $" --report-trx --results-directory {ShellQuote.Of(_repro.TrxDirectory)}";

        // LocalOnly would record the run; only the command's -e stops it. XpingTestRunner is read by
        // MSBuild from the environment, so the MTP flavour of the sample builds without touching the
        // command.
        Dictionary<string, string> environment = new(LocalOnly);
        if (runner != TestRunner.VsTest)
            environment["XpingTestRunner"] = "MTP";

        (exitCode, output) = await _repro
            .ShellAsync(repro.Command + trx, environment, $"repro of {fullyQualifiedName}")
            .ConfigureAwait(true);

        exitCode.Should().Be(0, "Command: {0}\nOutput:\n{1}", repro.Command, output);
        _repro.ReadTrxTestNames().Should().BeEquivalentTo(expectedResults, "Command: {0}\nOutput:\n{1}", repro.Command, output);
        _repro.StoredSessionCount().Should().Be(0, "the command sets XPING_ENABLED=false. Output:\n{0}", output);
    }

    private static ReproTarget Target(JsonElement session)
    {
        JsonElement properties = session.GetProperty("environmentInfo").GetProperty("customProperties");
        return new ReproTarget(
            properties.GetProperty(LocalSessionProperties.Runner).GetString(),
            properties.GetProperty(LocalSessionProperties.ProjectPath).GetString());
    }

    // Flaky at 1 of 2 runs: the kind and counts only size the run count, which this test does not run.
    private static Finding FlakyFinding(string fullyQualifiedName, string assembly) => new(
        "f_repro",
        FindingKind.Flaky,
        Severity.High,
        EvidenceLevel.Low,
        2,
        new FindingSubject.SingleTest(new TestReference("fingerprint", fullyQualifiedName, fullyQualifiedName, null, null, assembly)),
        new FlakyEvidence(1, 2, 2, 1, 1, 2, 0.5, 0, 0, 1, [], [], null),
        1);
}
