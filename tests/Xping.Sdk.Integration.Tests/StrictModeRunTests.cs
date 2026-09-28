/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Integration.Tests;

using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

/// <summary>
/// Runs each sample under <c>dotnet test</c> in strict mode against an endpoint that refuses every
/// connection: the run has to fail, and the TRX still has to list every test that ran.
/// </summary>
/// <remarks>
/// End to end on purpose. What is under test is how the test framework, its vstest adapter and
/// <c>dotnet test</c> react to the way each Xping adapter fails the run, and no unit test reaches
/// them. Each case builds and runs a sample, so they share one class and run one after another.
/// </remarks>
public sealed class StrictModeRunTests : IDisposable
{
    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(5);

    private readonly string _scratch = Path.Combine(
        Path.GetTempPath(), "xping-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    // Each filter selects two passing tests. MSTest reports an [AssemblyCleanup] failure on the last
    // test result, so one of them shows as failed there; the others report the failure on their own.
    [Theory]
    [InlineData("SampleApp.XUnit", "FullyQualifiedName~PassingTestIsTracked|FullyQualifiedName~AnotherPassingTest", 2, 0)]
    [InlineData("SampleApp.NUnit", "Name~Passing", 2, 0)]
    [InlineData("SampleApp.MSTest", "Name~Add_TwoNumbers|Name~Subtract_TwoNumbers", 1, 1)]
    public async Task StrictModeUploadFails_FailsRunAndKeepsEveryResult(
        string sample, string filter, int expectedPassed, int expectedFailed)
    {
        (int exitCode, string output) = await RunSampleAsync(sample, filter).ConfigureAwait(true);

        exitCode.Should().NotBe(0, "strict mode must fail the run on an upload error. Output:\n{0}", output);

        XElement counters = ReadCounters();
        counters.Attribute("total")!.Value.Should().Be("2", "every test that ran stays in the report. Output:\n{0}", output);
        counters.Attribute("passed")!.Value.Should().Be(expectedPassed.ToString(CultureInfo.InvariantCulture), "Output:\n{0}", output);
        counters.Attribute("failed")!.Value.Should().Be(expectedFailed.ToString(CultureInfo.InvariantCulture), "Output:\n{0}", output);
        output.Should().Contain("Xping network error in strict mode");
    }

    private async Task<(int ExitCode, string Output)> RunSampleAsync(string sample, string filter)
    {
        string repositoryRoot = FindRepositoryRoot();
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in new[]
        {
            "test", Path.Combine(repositoryRoot, "samples", sample),
            // Release, as CI builds the solution: the nested build then finds the SDK already built.
            "--configuration", "Release",
            "--filter", filter,
            "--logger", "trx",
            "--results-directory", Path.Combine(_scratch, "results"),
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        // The run under test inherits nothing from this one: not its Xping settings (CI sets a real API
        // key for the whole job), and not the MSBuild and vstest variables of the `dotnet test` running
        // this suite, which would steer the nested build and test host.
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            string name = (string)entry.Key;
            if (name.StartsWith("XPING_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("VSTEST_", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.Environment.Remove(name);
            }
        }

        startInfo.Environment["XPING_MODE"] = "Cloud";
        startInfo.Environment["XPING_APIKEY"] = "test-key";
        startInfo.Environment["XPING_PROJECTID"] = "test-project";
        // Port 9 (discard) is closed on loopback, so every upload is refused immediately.
        startInfo.Environment["XPING_APIENDPOINT"] = "http://127.0.0.1:9/v1";
        startInfo.Environment["XPING_MAXRETRIES"] = "0";
        startInfo.Environment["XPING_STRICTMODE"] = "true";
        startInfo.Environment["XPING_LOCALSTOREPATH"] = Path.Combine(_scratch, "store");

        using var process = Process.Start(startInfo)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();

        Task exited = process.WaitForExitAsync();
        if (await Task.WhenAny(exited, Task.Delay(RunTimeout)).ConfigureAwait(true) != exited)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet test {sample} did not finish within {RunTimeout}.");
        }

        return (process.ExitCode, await stdout.ConfigureAwait(true) + await stderr.ConfigureAwait(true));
    }

    private XElement ReadCounters()
    {
        string trx = Directory.GetFiles(Path.Combine(_scratch, "results"), "*.trx").Should().ContainSingle().Subject;
        return XDocument.Load(trx).Descendants().Single(e => e.Name.LocalName == "Counters");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Xping.Sdk.sln")))
                return directory.FullName;
        }

        throw new InvalidOperationException($"No Xping.Sdk.sln above {AppContext.BaseDirectory}.");
    }
}
