/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Commands;
using Xping.Cli.Report;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// <c>xping report --test</c>, driven through the command line against a real store.
/// </summary>
/// <remarks>
/// Rows and ids are read back from a full JSON report, never typed: both are what the coordinator
/// concluded, and the point of <c>--test</c> is that it agrees with the report.
/// </remarks>
// Resolves the store from a temp directory and mutates XPING_NO_BANNER.
[Collection("Sequential")]
public sealed class TestHistoryCommandTests : IDisposable
{
    private const string Assembly = TestSessionFactory.DefaultAssembly;
    private const string Other = "Other.Tests";

    private readonly string _root;

    public TestHistoryCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-history-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("XPING_LOCAL_STORE", _root);
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, "1");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XPING_LOCAL_STORE", null);
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, null);

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void AMethodNameShowsThatTestsHistoryAndOnlyItsFindings()
    {
        SeedVanishing();

        JsonElement full = Json("--all");
        List<JsonElement> rows = [.. full.GetProperty("findings").EnumerateArray()];
        int index = rows.FindIndex(f =>
            f.GetProperty("subject").GetProperty("fingerprint").GetString() == "fp-Removed1");
        JsonElement finding = rows[index];
        int row = index + 1;

        var (code, output, error) = Run("--test", "Removed1", "--format", "json");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Empty(error);

        using JsonDocument document = JsonDocument.Parse(output);
        JsonElement root = document.RootElement;
        JsonElement test = root.GetProperty("test");

        Assert.Equal("Removed1", test.GetProperty("query").GetString());
        Assert.Equal("fp-Removed1", test.GetProperty("test").GetProperty("fingerprint").GetString());
        Assert.Equal(8, test.GetProperty("sessions").GetArrayLength());
        Assert.Empty(test.GetProperty("candidates").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("selection").ValueKind);

        // The same finding as the full report's, except that its drill-down pins the whole-store
        // window's size (D10).
        JsonElement shown = Assert.Single(root.GetProperty("findings").EnumerateArray());
        Assert.Equal(finding.GetProperty("id").GetString(), shown.GetProperty("id").GetString());
        Assert.Equal(finding.GetProperty("evidence").GetRawText(), shown.GetProperty("evidence").GetRawText());
        Assert.Equal(
            finding.GetProperty("drillDown").GetString() + " --runs 8", shown.GetProperty("drillDown").GetString());

        JsonElement findingRow = Assert.Single(test.GetProperty("findingRows").EnumerateArray());
        Assert.Equal(finding.GetProperty("id").GetString(), findingRow.GetProperty("id").GetString());
        Assert.Equal(row, findingRow.GetProperty("row").GetInt32());

        Assert.Equal(full.GetProperty("summary").GetRawText(), root.GetProperty("summary").GetRawText());
    }

    /// <summary>
    /// The window is the whole store, not report's default of twenty runs.
    /// </summary>
    [Fact]
    public void TheDefaultWindowIsEveryRunTheStoreHolds()
    {
        ILocalSessionStore store = LocalSessionStore.Create();
        for (int i = 0; i < 25; i++)
            store.Write(TestSessionFactory.Session(i, "Stable"));

        JsonElement root = Json("--test", "Stable");

        Assert.Equal(25, root.GetProperty("test").GetProperty("sessions").GetArrayLength());
        Assert.Equal(25, root.GetProperty("window").GetProperty("sessionCount").GetInt32());
    }

    [Fact]
    public void RunsNarrowsTheWindow()
    {
        SeedVanishing();

        JsonElement root = Json("--test", "Stable0", "--runs", "3");

        Assert.Equal(3, root.GetProperty("test").GetProperty("sessions").GetArrayLength());
    }

    /// <summary>
    /// Every command the view prints names the assembly and pins the whole-store window's size.
    /// </summary>
    [Fact]
    public void EveryPrintedCommandNamesTheAssemblyAndTheWindowsSize()
    {
        SeedVanishing();

        JsonElement root = Json("--test", "Removed1");

        Assert.Equal(
            $"xping report --all --assembly {Assembly} --runs 8",
            root.GetProperty("truncated").GetProperty("command").GetString());

        foreach (JsonElement finding in root.GetProperty("findings").EnumerateArray())
        {
            string drillDown = finding.GetProperty("drillDown").GetString()!;
            Assert.EndsWith($"--assembly {Assembly} --runs 8", drillDown, StringComparison.Ordinal);

            string[] args = drillDown.Split(' ');
            var (code, output, _) = Run([.. args[2..], "--format", "json"]);

            Assert.Equal(ExitCodes.Success, code);

            using JsonDocument detail = JsonDocument.Parse(output);
            Assert.Equal(
                finding.GetProperty("id").GetString(),
                detail.RootElement.GetProperty("selection").GetProperty("id").GetString());
        }
    }

    /// <summary>
    /// A narrowed window keeps naming the assembly, which came from the name and not from the reader.
    /// </summary>
    [Fact]
    public void ANarrowedWindowStillNamesTheAssembly()
    {
        SeedVanishing();
        SeedOtherAssembly();

        JsonElement root = Json("--test", "Stable0", "--runs", "3");

        Assert.Equal(
            $"xping report --all --assembly {Assembly} --runs 3",
            root.GetProperty("truncated").GetProperty("command").GetString());
    }

    /// <summary>
    /// The lookup spans every assembly; the newest one does not decide which test a name means.
    /// </summary>
    [Fact]
    public void ATestInAnAssemblyThatDidNotRunLastIsFound()
    {
        SeedVanishing();
        SeedOtherAssembly();

        var (code, output, error) = Run("--test", "Stable0", "--format", "json");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Empty(error);

        using JsonDocument document = JsonDocument.Parse(output);
        Assert.Equal(Assembly, document.RootElement.GetProperty("test").GetProperty("test").GetProperty("assembly").GetString());
        Assert.Equal(8, document.RootElement.GetProperty("window").GetProperty("sessionCount").GetInt32());
    }

    [Fact]
    public void AHistoryInTextSucceedsWithNoScopeNotice()
    {
        SeedVanishing();
        SeedOtherAssembly();

        var (code, output, _) = Run("--test", "Removed1");

        Assert.Equal(ExitCodes.Success, code);
        Assert.DoesNotContain("Reporting on", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AHistoryInTextShowsTheTestItsRunsAndItsFindings()
    {
        SeedVanishing();

        string id = Json("--test", "Removed1").GetProperty("findings")[0].GetProperty("id").GetString()!;
        int row = Json("--test", "Removed1").GetProperty("test").GetProperty("findingRows")[0].GetProperty("row").GetInt32();

        string[] lines = Run("--test", "Removed1").Output.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        Assert.Contains(lines, l => l.StartsWith("TEST ", StringComparison.Ordinal) && l.EndsWith(" fp fp-Removed1", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("ran in 5 of 8 runs: 5 passed", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("RUNS ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("absent from 3 runs", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("FINDINGS ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains(id, StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("1 finding about this test ", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith($"Detail of row {row}: xping report --id {id}", StringComparison.Ordinal));
    }

    [Fact]
    public void AHistoryInJsonCarriesTheShortFingerprint()
    {
        SeedVanishing();

        Assert.Equal(
            "fp-Removed1",
            Json("--test", "Removed1").GetProperty("test").GetProperty("shortFingerprint").GetString());
    }

    [Fact]
    public void AnAmbiguousNameListsTheCandidatesAndExitsFour()
    {
        SeedParameterised(2);

        var (code, output, error) = Run("--test", "Add");

        Assert.Equal(ExitCodes.TestAmbiguous, code);
        Assert.Empty(output);

        string nl = Environment.NewLine;
        Assert.Equal(
            $"'Add' matches 2 tests. Name one by its fingerprint:{nl}" +
            $"  fp-Add-0  {Assembly}{nl}" +
            $"    MyApp.Tests.SampleTests.Add(0,1){nl}" +
            $"  fp-Add-1  {Assembly}{nl}" +
            $"    MyApp.Tests.SampleTests.Add(1,2){nl}" +
            $"xping report --test fp-Add-0 --assembly {Assembly}{nl}",
            error);
    }

    [Fact]
    public void TheReadyToRunLineNamesOneTest()
    {
        SeedParameterised(2);

        string command = Run("--test", "Add").Error.TrimEnd().Split(Environment.NewLine)[^1];
        string[] args = command.Split(' ');

        var (code, output, _) = Run([.. args[2..], "--format", "json"]);

        Assert.Equal(ExitCodes.Success, code);

        using JsonDocument document = JsonDocument.Parse(output);
        Assert.Equal("fp-Add-0", document.RootElement.GetProperty("test").GetProperty("test").GetProperty("fingerprint").GetString());
    }

    [Fact]
    public void AnAmbiguousNameInJsonCarriesTheCandidatesAndNoHistory()
    {
        SeedParameterised(2);

        var (code, output, _) = Run("--test", "Add", "--format", "json");

        Assert.Equal(ExitCodes.TestAmbiguous, code);

        using JsonDocument document = JsonDocument.Parse(output);
        JsonElement root = document.RootElement;
        JsonElement test = root.GetProperty("test");

        Assert.Equal(JsonValueKind.Null, test.GetProperty("test").ValueKind);
        Assert.Empty(test.GetProperty("sessions").EnumerateArray());
        Assert.Empty(root.GetProperty("findings").EnumerateArray());
        Assert.Equal(
            ["fp-Add-0", "fp-Add-1"],
            test.GetProperty("candidates").EnumerateArray().Select(c => c.GetProperty("fingerprintPrefix").GetString()));
        Assert.Equal(
            $"xping report --test fp-Add-0 --assembly {Assembly}",
            test.GetProperty("candidates")[0].GetProperty("command").GetString());

        // The report around the answer is report's own scope over the whole store (Amendment 5A).
        Assert.Equal(8, root.GetProperty("window").GetProperty("sessionCount").GetInt32());
    }

    [Fact]
    public void PastTenCandidatesTheRestAreCounted()
    {
        SeedParameterised(12);

        string error = Run("--test", "Add").Error;

        Assert.Equal(10, error.Split(Environment.NewLine).Count(line => line.StartsWith("  fp-Add-", StringComparison.Ordinal)));
        Assert.Contains($"  +2 more{Environment.NewLine}", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ANameThatMatchesNothingExitsThreeAndCountsTheStore()
    {
        SeedVanishing();
        SeedOtherAssembly();

        var (code, output, error) = Run("--test", "Nope");

        Assert.Equal(ExitCodes.NotFound, code);
        Assert.Empty(output);
        Assert.Equal(
            "No test matching 'Nope' in this store (2 assemblies, 14 runs)." + Environment.NewLine, error);
    }

    [Fact]
    public void ANameThatMatchesNothingInTheNamedAssemblySaysSo()
    {
        SeedVanishing();
        SeedOtherAssembly();

        var (code, _, error) = Run("--test", "Stable0", "--assembly", Other);

        Assert.Equal(ExitCodes.NotFound, code);
        Assert.Equal($"No test matching 'Stable0' in {Other} (6 runs)." + Environment.NewLine, error);
    }

    [Fact]
    public void ANameThatMatchesNothingInJsonStillWritesTheEnvelope()
    {
        SeedVanishing();

        var (code, output, _) = Run("--test", "Nope", "--format", "json");

        Assert.Equal(ExitCodes.NotFound, code);

        using JsonDocument document = JsonDocument.Parse(output);
        JsonElement root = document.RootElement;
        JsonElement test = root.GetProperty("test");

        Assert.Equal("Nope", test.GetProperty("query").GetString());
        Assert.Equal(JsonValueKind.Null, test.GetProperty("test").ValueKind);
        Assert.Equal(JsonValueKind.Null, test.GetProperty("counts").ValueKind);
        Assert.Empty(test.GetProperty("candidates").EnumerateArray());
        Assert.Empty(root.GetProperty("findings").EnumerateArray());
        Assert.Equal(3, root.GetProperty("summary").GetProperty("findings").GetInt32());
    }

    /// <summary>
    /// A test that ran only before a narrowed window is named, with the flag that hid it.
    /// </summary>
    [Fact]
    public void ATestOnlyInOlderRunsNamesTheFlagThatHidIt()
    {
        SeedVanishing();

        var (code, output, error) = Run("--test", "Removed0", "--runs", "3");

        Assert.Equal(ExitCodes.NotFound, code);
        Assert.Empty(output);
        Assert.Equal(
            "No run of 'Removed0' in this window (--runs 3). It ran in 5 earlier runs; drop --runs to see them." +
            Environment.NewLine,
            error);
    }

    [Fact]
    public void ATestOnlyInOlderRunsInJsonIsTheReportOverTheNarrowedWindow()
    {
        SeedVanishing();

        var (code, output, _) = Run("--test", "Removed0", "--runs", "3", "--format", "json");

        Assert.Equal(ExitCodes.NotFound, code);

        using JsonDocument document = JsonDocument.Parse(output);
        JsonElement root = document.RootElement;

        Assert.Equal(3, root.GetProperty("window").GetProperty("sessionCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("test").GetProperty("test").ValueKind);
    }

    /// <summary>
    /// A <c>--since</c> later than every run of the test's assembly opens no window, and is still a
    /// test outside the window rather than an empty store.
    /// </summary>
    [Fact]
    public void ASinceLaterThanEveryRunNamesTheFlagRatherThanAnEmptyStore()
    {
        SeedVanishing();

        var (code, output, error) = Run("--test", "Removed0", "--since", "2026-08-02", "--format", "json");

        Assert.Equal(ExitCodes.NotFound, code);
        Assert.Empty(output);
        Assert.Equal(
            "No run of 'Removed0' in this window (--since 2026-08-02). It ran in 5 earlier runs; drop --since to see them." +
            Environment.NewLine,
            error);
    }

    /// <summary>
    /// The answer to an ambiguous name is about the store, so a <c>--since</c> that only another
    /// assembly's runs can resolve does not replace it.
    /// </summary>
    [Theory]
    [InlineData("text")]
    [InlineData("json")]
    public void ASinceThatTheNewestAssemblyCannotResolveDoesNotHideTheCandidates(string format)
    {
        SeedParameterised(2, sha: "abc1234");
        SeedOtherAssembly();

        var (code, _, error) = Run("--test", "Add", "--since", "abc1234", "--format", format);

        Assert.Equal(ExitCodes.TestAmbiguous, code);
        Assert.StartsWith("'Add' matches 2 tests.", error, StringComparison.Ordinal);
    }

    /// <summary>
    /// A test whose runs sit only in unreadable files is reported absent with the count beside it,
    /// so the reader is not told it never ran.
    /// </summary>
    [Fact]
    public void ANameThatMatchesNothingCountsTheUnreadableRuns()
    {
        SeedVanishing();

        string sessions = Path.GetDirectoryName(
            Directory.GetFiles(_root, "session-*.json.gz", SearchOption.AllDirectories)[0])!;
        File.WriteAllBytes(Path.Combine(sessions, "session-0699999999999999999-deadbeef.json.gz"), [1, 2, 3]);

        var (code, _, error) = Run("--test", "Nope");

        Assert.Equal(ExitCodes.NotFound, code);
        Assert.StartsWith("warning: 1 unreadable run(s) skipped." + Environment.NewLine, error, StringComparison.Ordinal);
        Assert.EndsWith("No test matching 'Nope' in this store (1 assembly, 8 runs)." + Environment.NewLine, error, StringComparison.Ordinal);
    }

    /// <summary>
    /// A store holding nothing but damaged files has no runs to name, and says how many it skipped,
    /// rather than claiming no test ever ran.
    /// </summary>
    [Fact]
    public void AStoreOfUnreadableFilesCountsThem()
    {
        string sessions = Path.Combine(_root, "sessions");
        Directory.CreateDirectory(sessions);
        File.WriteAllBytes(Path.Combine(sessions, "session-0699999999999999998-deadbeef.json.gz"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(sessions, "session-0699999999999999999-feedface.json.gz"), [1, 2, 3]);

        var (code, _, error) = Run("--test", "Nope");

        Assert.Equal(ExitCodes.InsufficientData, code);
        Assert.Contains("No runs recorded yet", error, StringComparison.Ordinal);
        Assert.Contains("warning: 2 unreadable run(s) skipped.", error, StringComparison.Ordinal);
    }

    /// <summary>
    /// A suite recorded by a machine whose clock runs ahead is searched, as <c>--assembly</c> would
    /// search it, and named, since its runs were kept rather than left out.
    /// </summary>
    [Fact]
    public void ASuiteWhoseEveryRunIsAheadOfTheClockIsNamed()
    {
        SeedVanishing();
        LocalSessionStore.Create().Write(TestSessionFactory.Session(
            40,
            [TestSessionFactory.Execution("Elsewhere", assembly: Other)],
            startedAt: DateTime.UtcNow.AddDays(10)));

        var (code, _, error) = Run("--test", "Nope");

        Assert.Equal(ExitCodes.NotFound, code);
        Assert.Contains(
            $"warning: every run of {Other} is stamped ahead of this machine's clock. " +
            "Check the clock on the machine that recorded them.",
            error,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyStoreCannotBeReportedOn()
    {
        var (code, output, error) = Run("--test", "Anything");

        Assert.Equal(ExitCodes.InsufficientData, code);
        Assert.Empty(output);
        Assert.Contains("No runs recorded yet", error, StringComparison.Ordinal);
    }

    /// <summary>
    /// A history never fails a build, even when the test has a finding at every severity.
    /// </summary>
    [Fact]
    public void AHistoryNeverFailsTheCommand()
    {
        SeedVanishing();

        Assert.Equal(ExitCodes.Success, Run("--test", "Removed0").Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankNameIsRejectedAtParseTime(string name)
    {
        var (code, output, error) = Run("--test", name);

        Assert.Equal(ExitCodes.InsufficientData, code);
        Assert.Empty(output);
        Assert.Contains("--test expects a test name or fingerprint.", error, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every flag that speaks for a ranked list, or is another selection, is refused.
    /// </summary>
    /// <remarks>
    /// Against an empty store, so a rejection that happened after the store was opened would say
    /// there were no runs instead.
    /// </remarks>
    [Theory]
    [InlineData("--test and --id are mutually exclusive.", "--id", "f_2a91c0de")]
    [InlineData("--test and --kind are mutually exclusive.", "--kind", "Flaky")]
    [InlineData("--test and --top are mutually exclusive.", "--top", "3")]
    [InlineData("--test and --all are mutually exclusive.", "--all")]
    [InlineData("--test and --fail-on are mutually exclusive.", "--fail-on", "high")]
    [InlineData("--test and --summary are mutually exclusive.", "--summary")]
    [InlineData("--test and --format summary are mutually exclusive.", "--format", "summary")]
    public void FlagsThatSpeakForARankedListAreRejected(string message, params string[] flag)
    {
        var (code, output, error) = Run(["--test", "Stable0", .. flag]);

        Assert.Equal(ExitCodes.InsufficientData, code);
        Assert.Empty(output);
        Assert.Contains(message, error, StringComparison.Ordinal);
        Assert.DoesNotContain("No runs recorded", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--json")]
    [InlineData("--ascii")]
    [InlineData("--no-color")]
    [InlineData("--format", "text")]
    [InlineData("--runs", "4")]
    [InlineData("--since", "2026-08-01")]
    [InlineData("--assembly", Assembly)]
    public void OrthogonalFlagsAreAccepted(params string[] flag)
    {
        SeedVanishing();

        Assert.Equal(ExitCodes.Success, Run(["--test", "Stable0", .. flag]).Code);
    }

    /// <summary>
    /// Writes eight runs in which three tests stop after the fifth, so the report holds three
    /// <c>Vanished</c> findings.
    /// </summary>
    /// <remarks>
    /// The shape <c>FindingDetailCommandTests</c> seeds, for the reasons <c>ReportEnvelopeTests</c>
    /// gives.
    /// </remarks>
    private static void SeedVanishing()
    {
        ILocalSessionStore store = LocalSessionStore.Create();

        for (int i = 0; i < 8; i++)
        {
            var executions = new List<TestExecution>();

            for (int t = 0; t <= 3; t++)
                executions.Add(TestSessionFactory.Execution($"Stable{t}"));

            if (i < 5)
            {
                for (int t = 0; t < 3; t++)
                    executions.Add(TestSessionFactory.Execution($"Removed{t}"));
            }

            store.Write(TestSessionFactory.Session(i, executions));
        }
    }

    /// <summary>Writes eight runs of a parameterised <c>Add</c> with the given number of cases.</summary>
    private static void SeedParameterised(int cases, string? sha = null)
    {
        ILocalSessionStore store = LocalSessionStore.Create();

        for (int i = 0; i < 8; i++)
        {
            store.Write(TestSessionFactory.Session(
                i,
                [
                    .. Enumerable.Range(0, cases).Select(c => TestSessionFactory.Execution(
                        $"Add{c}",
                        fingerprint: $"fp-Add-{c}",
                        fullyQualifiedName: $"MyApp.Tests.SampleTests.Add({c},{c + 1})"))
                ],
                sha: sha));
        }
    }

    /// <summary>Writes runs of a second assembly, newer than every other, so it is the one report scopes to.</summary>
    private static void SeedOtherAssembly()
    {
        ILocalSessionStore store = LocalSessionStore.Create();

        for (int i = 20; i < 26; i++)
            store.Write(TestSessionFactory.Session(i, [TestSessionFactory.Execution("Elsewhere", assembly: Other)]));
    }

    private static JsonElement Json(params string[] args)
    {
        var (_, output, _) = Run([.. args, "--format", "json"]);

        using JsonDocument document = JsonDocument.Parse(output);
        return document.RootElement.Clone();
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(["report", .. args], output, error, configureServices: OfflineCli.Configure);
        return (code, output.ToString(), error.ToString());
    }
}
