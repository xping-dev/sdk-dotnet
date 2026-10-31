/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Integration.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Xping.Cli.Report;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Services.Serialization;
using Xping.Sdk.Integration.Tests.Infrastructure;
using Xunit;

/// <summary>
/// Records sample tests, builds their repro command from what the store holds, and runs that command
/// as a reader would: from the repository root, through a shell. The command has to select exactly
/// the recorded tests and write no session. Pins the runner behaviour the command relies on
/// (escaping, data rows, <c>|</c> between members, <c>-e XPING_ENABLED=false</c>) against framework
/// and SDK upgrades.
/// </summary>
[Collection(SampleRunner.Collection)]
public sealed class ReproCommandRunTests : IDisposable
{
    private readonly SampleRunner _recording = new();
    private readonly SampleRunner _repro = new();

    public void Dispose()
    {
        _repro.Dispose();
        _recording.Dispose();
    }

    // Each entry of `chosen` picks one recorded name by its ending: NUnit stores one name per case,
    // MSTest and xUnit one per method, which runs every row. Two entries make a shared failure, whose
    // command joins them with |. "50 off" is the case a filter that let MSBuild decode %20 would run
    // instead of "50%20off"; the apostrophe in "50's off" has to survive the shell quoting.
    [Theory]
    [InlineData("SampleApp.NUnit", TestRunner.VsTest, new[] { "ParameterizedTestIsTracked(1,2,3)" }, new[] { "ParameterizedTestIsTracked(1,2,3)" })]
    [InlineData("SampleApp.NUnit", TestRunner.VsTest, new[] { "StringParameterIsTracked(\"50%20off\")" }, new[] { "StringParameterIsTracked(\"50%20off\")" })]
    [InlineData("SampleApp.NUnit", TestRunner.VsTest, new[] { "StringParameterIsTracked(\"50's off\")", "ParameterizedTestIsTracked(5,5,10)" }, new[] { "StringParameterIsTracked(\"50's off\")", "ParameterizedTestIsTracked(5,5,10)" })]
    [InlineData("SampleApp.NUnit", TestRunner.MtpExecutable, new[] { "ParameterizedTestIsTracked(1,2,3)" }, new[] { "ParameterizedTestIsTracked(1,2,3)" })]
    [InlineData("SampleApp.NUnit", TestRunner.MtpExecutable, new[] { "StringParameterIsTracked(\"50%20off\")" }, new[] { "StringParameterIsTracked(\"50%20off\")" })]
    [InlineData("SampleApp.NUnit", TestRunner.MtpExecutable, new[] { "StringParameterIsTracked(\"50's off\")", "ParameterizedTestIsTracked(5,5,10)" }, new[] { "StringParameterIsTracked(\"50's off\")", "ParameterizedTestIsTracked(5,5,10)" })]
    [InlineData("SampleApp.MSTest", TestRunner.VsTest, new[] { "Add_MultipleInputs_ReturnsExpectedSum" }, new[] { "Add_MultipleInputs_ReturnsExpectedSum (2,3,5)", "Add_MultipleInputs_ReturnsExpectedSum (10,5,15)", "Add_MultipleInputs_ReturnsExpectedSum (-1,1,0)" })]
    [InlineData("SampleApp.MSTest", TestRunner.MtpExecutable, new[] { "Add_MultipleInputs_ReturnsExpectedSum" }, new[] { "Add_MultipleInputs_ReturnsExpectedSum (2,3,5)", "Add_MultipleInputs_ReturnsExpectedSum (10,5,15)", "Add_MultipleInputs_ReturnsExpectedSum (-1,1,0)" })]
    [InlineData("SampleApp.XUnit", TestRunner.VsTest, new[] { "ParameterizedTestIsTracked" }, new[] { "SampleApp.XUnit.SampleTests.ParameterizedTestIsTracked(a: 1, b: 2, expected: 3)", "SampleApp.XUnit.SampleTests.ParameterizedTestIsTracked(a: 5, b: 5, expected: 10)", "SampleApp.XUnit.SampleTests.ParameterizedTestIsTracked(a: 0, b: 0, expected: 0)" })]
    [InlineData("SampleApp.XUnitV3", TestRunner.VsTest, new[] { "ParameterizedTestIsTracked" }, new[] { "SampleApp.XUnitV3.SampleTests.ParameterizedTestIsTracked(a: 1, b: 2, expected: 3)", "SampleApp.XUnitV3.SampleTests.ParameterizedTestIsTracked(a: 5, b: 5, expected: 10)", "SampleApp.XUnitV3.SampleTests.ParameterizedTestIsTracked(a: 0, b: 0, expected: 0)" })]
    [InlineData("SampleApp.XUnitV3", TestRunner.MtpExecutable, new[] { "ParameterizedTestIsTracked" }, new[] { "SampleApp.XUnitV3.SampleTests.ParameterizedTestIsTracked(a: 1, b: 2, expected: 3)", "SampleApp.XUnitV3.SampleTests.ParameterizedTestIsTracked(a: 5, b: 5, expected: 10)", "SampleApp.XUnitV3.SampleTests.ParameterizedTestIsTracked(a: 0, b: 0, expected: 0)" })]
    [InlineData("SampleApp.XUnitV3", TestRunner.MtpExecutable, new[] { "ParameterizedTestIsTracked", "PassingTestIsTracked" }, new[] { "SampleApp.XUnitV3.SampleTests.ParameterizedTestIsTracked(a: 1, b: 2, expected: 3)", "SampleApp.XUnitV3.SampleTests.ParameterizedTestIsTracked(a: 5, b: 5, expected: 10)", "SampleApp.XUnitV3.SampleTests.ParameterizedTestIsTracked(a: 0, b: 0, expected: 0)", "SampleApp.XUnitV3.SampleTests.PassingTestIsTracked" })]
    public Task TheReproCommandRunsTheRecordedTestsAndWritesNoSession(
        string sample, TestRunner runner, string[] chosen, string[] expectedResults) =>
        RunReproAsync(sample, runner, chosen, expectedResults, powerShell: false);

    // The one name whose quoting differs between the dialects, with a " beside the apostrophe. This
    // pins how PowerShell parses the printed command. It does not pin the Windows command line pwsh
    // would build from it: on Linux pwsh hands the program its arguments as a list.
    [Theory]
    [InlineData(TestRunner.VsTest)]
    [InlineData(TestRunner.MtpExecutable)]
    public Task InPowerShellTheReproCommandRunsTheRecordedTest(TestRunner runner) =>
        RunReproAsync(
            "SampleApp.NUnit", runner, ["StringParameterIsTracked(\"50's off\")"], ["StringParameterIsTracked(\"50's off\")"], powerShell: true);

    private async Task RunReproAsync(
        string sample, TestRunner runner, string[] chosen, string[] expectedResults, bool powerShell)
    {
        ShellDialect shell = powerShell ? ShellDialect.PowerShell : ShellDialect.Posix;

        string recordFilter = string.Join("|", chosen
            .Select(name => name.Split('(')[0])
            .Distinct(StringComparer.Ordinal)
            .Select(method => $"FullyQualifiedName~.{method}"));

        (int exitCode, string output) = await _recording
            .RunAsync(sample, runner, recordFilter, SampleRunner.LocalOnly)
            .ConfigureAwait(true);
        exitCode.Should().Be(0, "Output:\n{0}", output);

        TestSession session = ReadStoredSession();
        string[] recorded = [.. session.Executions.Select(e => e.Identity.FullyQualifiedName).Distinct(StringComparer.Ordinal)];
        string[] names =
        [
            .. chosen.Select(ending => recorded
                .Should().ContainSingle(name => name.EndsWith("." + ending, StringComparison.Ordinal)).Subject)
        ];

        // The same mapping report uses, so a change to it, or to the properties the SDK stamps, shows here.
        ReproTarget target = ReproTarget.From(session);
        Repro? repro = ReproCommand.For(Finding(names, sample), target, shell);
        repro.Should().NotBeNull("the session recorded runner {0} and project {1}", target.Runner, target.ProjectPath);

        // Appended after the command, so it runs as printed; the TRX names the tests the filter selected.
        string trx = runner == TestRunner.VsTest
            ? $" --logger trx --results-directory {ShellQuote.Of(_repro.ResultsPath, shell)}"
            : $" --report-trx --results-directory {ShellQuote.Of(_repro.ResultsPath, shell)}";

        // LocalOnly would record the run; only the command's -e stops it. MSBuild reads the other two
        // from the environment, so the command runs unchanged: XpingTestRunner builds the MTP flavour
        // of the sample, and Release reuses the build the recording run (and CI) already made rather
        // than compiling the SDK again in Debug.
        Dictionary<string, string> environment = new(SampleRunner.LocalOnly) { ["Configuration"] = "Release" };
        if (runner != TestRunner.VsTest)
            environment["XpingTestRunner"] = "MTP";

        string description = $"repro of {string.Join(", ", names)}";
        (exitCode, output) = await (powerShell
                ? _repro.PowerShellAsync(repro!.Command + trx, environment, description)
                : _repro.ShellAsync(repro!.Command + trx, environment, description))
            .ConfigureAwait(true);

        exitCode.Should().Be(0, "Command: {0}\nOutput:\n{1}", repro.Command, output);
        _repro.ReadTrxTestNames().Should().BeEquivalentTo(expectedResults, "Command: {0}\nOutput:\n{1}", repro.Command, output);
        _repro.StoredSessionCount().Should().Be(0, "the command sets XPING_ENABLED=false. Output:\n{0}", output);
    }

    // Deserialized with the options the local store reads with; the store itself only opens the
    // repository's default location, which XPING_LOCAL_STORE steers through this process's environment.
    private TestSession ReadStoredSession()
    {
        string file = Directory.GetFiles(Path.Combine(_recording.StorePath, "sessions"), "*.json.gz")
            .Should().ContainSingle().Subject;

        using var gzip = new GZipStream(File.OpenRead(file), CompressionMode.Decompress);
        return JsonSerializer.Deserialize<TestSession>(gzip, XpingSerializerOptions.FileOptions)!;
    }

    // The kind and counts only size the run count, which this test does not run: one name is a flaky
    // test at 1 of 2 runs, several a shared failure.
    private static Finding Finding(string[] names, string assembly)
    {
        TestReference[] tests = [.. names.Select(name => new TestReference(name, name, name, null, null, assembly))];

        if (tests.Length == 1)
        {
            return new Finding(
                "f_repro", FindingKind.Flaky, Severity.High, EvidenceLevel.Low, 2,
                new FindingSubject.SingleTest(tests[0]),
                new FlakyEvidence(1, 2, 2, 1, 1, 2, 0.5, 0, 0, 1, [], [], null),
                1);
        }

        var signature = new SignatureView("hash", null, "message", [], false, false, false, names.Length, DateTime.UnixEpoch, null, 0, true, false);
        ClusterMember[] members = [.. names.Select(name => new ClusterMember(name, name, 1, 1, 2))];

        return new Finding(
            "f_repro", FindingKind.SharedFailure, Severity.High, EvidenceLevel.Low, 2,
            new FindingSubject.Group("g_repro", tests),
            new SharedFailureEvidence(signature, members.Length, members, members.Length, 1, 2, members.Length, DateTime.UnixEpoch, null, []),
            1);
    }
}
