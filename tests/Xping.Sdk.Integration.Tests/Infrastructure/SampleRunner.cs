/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Integration.Tests.Infrastructure;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;

/// <summary>
/// How a sample's tests are run.
/// </summary>
#pragma warning disable CA1515 // Public because public xUnit theories take it as a parameter.
public enum TestRunner
{
    /// <summary><c>dotnet test</c> on vstest, the default without a <c>global.json</c> runner.</summary>
    VsTest,

    /// <summary><c>dotnet test</c> in the .NET 10 SDK's native Microsoft.Testing.Platform mode.</summary>
    MtpDotnetTest,

    /// <summary>The sample's own Microsoft.Testing.Platform executable, through <c>dotnet run</c>.</summary>
    MtpExecutable,
}
#pragma warning restore CA1515

/// <summary>
/// Builds and runs one of the samples in a child process, with a scratch directory for its local
/// store and test results.
/// </summary>
internal sealed class SampleRunner : IDisposable
{
    /// <summary>
    /// The xUnit collection of every test class that runs samples. They share the samples' bin/ and
    /// obj/, so they run one after another.
    /// </summary>
    public const string Collection = "Sample runs";

    private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(5);

    private readonly string _scratch = Path.Combine(
        Path.GetTempPath(), "xping-tests", Guid.NewGuid().ToString("N"));

    /// <summary>The scratch directory this runner deletes when disposed. Outside any repository.</summary>
    public string ScratchPath => _scratch;

    /// <summary>The local store the run writes to.</summary>
    public string StorePath => Path.Combine(_scratch, "store");

    private string ResultsPath => Path.Combine(_scratch, "results");

    public void Dispose()
    {
        if (Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    /// <summary>
    /// Runs the tests of <paramref name="sample"/> that match <paramref name="filter"/>, writing a TRX.
    /// </summary>
    /// <param name="sample">The sample's folder under <c>samples/</c>.</param>
    /// <param name="runner">How to run it.</param>
    /// <param name="filter">A vstest-syntax filter, which both MTP runners accept too.</param>
    /// <param name="environment">Xping settings for the run. It inherits none from this process.</param>
    /// <param name="msbuildProperties">Extra <c>-p:</c> properties for the sample's build.</param>
    public async Task<(int ExitCode, string Output)> RunAsync(
        string sample,
        TestRunner runner,
        string filter,
        IReadOnlyDictionary<string, string> environment,
        params string[] msbuildProperties)
    {
        string repositoryRoot = FindRepositoryRoot();
        string project = Path.Combine(repositoryRoot, "samples", sample);
        Directory.CreateDirectory(_scratch);

        // Release, as CI builds the solution: the nested build then finds the SDK already built.
        var build = new List<string> { "--configuration", "Release" };
        if (runner != TestRunner.VsTest)
            build.Add("-p:XpingTestRunner=MTP");
        build.AddRange(msbuildProperties.Select(property => $"-p:{property}"));

        string[] mtpArguments = ["--filter", filter, "--report-trx", "--results-directory", ResultsPath];
        (string workingDirectory, IEnumerable<string> arguments) = runner switch
        {
            TestRunner.VsTest => (repositoryRoot, new[] { "test", project }
                .Concat(build)
                .Concat(["--filter", filter, "--logger", "trx", "--results-directory", ResultsPath])),

            // dotnet test picks its runner from the global.json above the working directory. The repository
            // has none, so vstest stays its default; this one exists for the run alone.
            TestRunner.MtpDotnetTest => (WriteMtpGlobalJson(), new[] { "test", "--project", project }
                .Concat(build)
                .Concat(mtpArguments)),

            TestRunner.MtpExecutable => (repositoryRoot, new[] { "run", "--project", project }
                .Concat(build)
                .Append("--")
                .Concat(mtpArguments)),

            _ => throw new ArgumentOutOfRangeException(nameof(runner), runner, null),
        };

        return await DotnetAsync(workingDirectory, arguments, environment, $"{runner} run of {sample}").ConfigureAwait(true);
    }

    /// <summary>
    /// Runs <c>dotnet</c> with <paramref name="arguments"/> in a child process that inherits no Xping,
    /// MSBuild, vstest or testing platform setting from this one.
    /// </summary>
    /// <param name="workingDirectory">Where to run it.</param>
    /// <param name="arguments">The arguments after <c>dotnet</c>.</param>
    /// <param name="environment">Settings for the child on top of the cleaned environment.</param>
    /// <param name="description">What is being run, for the timeout message.</param>
    public async Task<(int ExitCode, string Output)> DotnetAsync(
        string workingDirectory,
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        string description)
    {
        Directory.CreateDirectory(_scratch);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        // The run under test inherits nothing from this one: not its Xping settings (CI sets a real API
        // key for the whole job), and not the MSBuild, vstest and testing platform variables of the
        // `dotnet test` running this suite, which would steer the nested build and test host.
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            string name = (string)entry.Key;
            if (name.StartsWith("XPING_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("VSTEST_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("TESTINGPLATFORM_", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.Environment.Remove(name);
            }
        }

        startInfo.Environment["XPING_LOCAL_STORE"] = StorePath;
        foreach (KeyValuePair<string, string> setting in environment)
            startInfo.Environment[setting.Key] = setting.Value;

        using var process = Process.Start(startInfo)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();

        Task exited = process.WaitForExitAsync();
        if (await Task.WhenAny(exited, Task.Delay(RunTimeout)).ConfigureAwait(true) != exited)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"{description} did not finish within {RunTimeout}.");
        }

        return (process.ExitCode, await stdout.ConfigureAwait(true) + await stderr.ConfigureAwait(true));
    }

    /// <summary>The single session the run wrote to the local store.</summary>
    public JsonElement ReadStoredSession()
    {
        string session = Directory.GetFiles(Path.Combine(StorePath, "sessions"), "*.json.gz")
            .Should().ContainSingle().Subject;

        using var gzip = new GZipStream(File.OpenRead(session), CompressionMode.Decompress);
        using JsonDocument document = JsonDocument.Parse(gzip);
        return document.RootElement.Clone();
    }

    /// <summary>The <c>Counters</c> element of the single TRX the run wrote.</summary>
    public XElement ReadTrxCounters()
    {
        string trx = Directory.GetFiles(ResultsPath, "*.trx", SearchOption.AllDirectories).Should().ContainSingle().Subject;
        return XDocument.Load(trx).Descendants().Single(e => e.Name.LocalName == "Counters");
    }

    private string WriteMtpGlobalJson()
    {
        string directory = Path.Combine(_scratch, "mtp");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "global.json"),
            """{ "test": { "runner": "Microsoft.Testing.Platform" } }""");
        return directory;
    }

    /// <summary>The repository root, found from this test assembly's output folder.</summary>
    public static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Xping.Sdk.sln")))
                return directory.FullName;
        }

        throw new InvalidOperationException($"No Xping.Sdk.sln above {AppContext.BaseDirectory}.");
    }
}
