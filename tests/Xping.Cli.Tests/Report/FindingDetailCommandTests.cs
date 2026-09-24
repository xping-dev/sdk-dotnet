/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Commands;
using Xping.Cli.Report;
using Xping.Cli.Report.Model;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// <c>xping report --id</c>, driven through the command line against a real store.
/// </summary>
/// <remarks>
/// Ids are read back from a first JSON run, never typed: an id is a hash of what the coordinator
/// concluded, and one written into a test by hand is a test of the hand.
/// </remarks>
// Resolves the store from a temp directory and mutates XPING_NO_BANNER.
[Collection("Sequential")]
public sealed class FindingDetailCommandTests : IDisposable
{
    private const string Other = "Other.Tests";

    private readonly string _root;

    public FindingDetailCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-detail-tests", Guid.NewGuid().ToString("N"));
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
    public void AReportedIdIsTheFullEnvelopeWithOneFinding()
    {
        SeedVanishing();

        var (_, fullOutput, _) = Run("--all", "--format", "json");
        using JsonDocument full = JsonDocument.Parse(fullOutput);
        JsonElement second = full.RootElement.GetProperty("findings")[1];
        string id = second.GetProperty("id").GetString()!;

        var (code, output, error) = Run("--id", id, "--format", "json");

        Assert.Equal(0, code);
        Assert.Empty(error);

        using JsonDocument detail = JsonDocument.Parse(output);
        JsonElement root = detail.RootElement;

        Assert.Equal(second.GetRawText(), Assert.Single(root.GetProperty("findings").EnumerateArray()).GetRawText());
        Assert.Equal(
            full.RootElement.GetProperty("summary").GetRawText(), root.GetProperty("summary").GetRawText());
        Assert.Equal(
            full.RootElement.GetProperty("latestRun").GetRawText(), root.GetProperty("latestRun").GetRawText());

        JsonElement selection = root.GetProperty("selection");
        Assert.Equal(id, selection.GetProperty("id").GetString());
        Assert.True(selection.GetProperty("reported").GetBoolean());
        Assert.Equal("Vanished", selection.GetProperty("kind").GetString());
        Assert.Equal(2, selection.GetProperty("row").GetInt32());

        JsonElement truncated = root.GetProperty("truncated");
        Assert.Equal(1, truncated.GetProperty("shown").GetInt32());
        Assert.Equal(3, truncated.GetProperty("total").GetInt32());
    }

    [Fact]
    public void TheDrillDownOnEveryFindingRunsAndSelectsIt()
    {
        SeedVanishing();

        foreach (JsonElement finding in Json("--all").GetProperty("findings").EnumerateArray())
        {
            string drillDown = finding.GetProperty("drillDown").GetString()!;
            string[] args = drillDown.Split(' ');

            Assert.Equal(["xping", "report"], args[..2]);

            var (code, output, _) = Run([.. args[2..], "--format", "json"]);

            Assert.Equal(0, code);

            using JsonDocument detail = JsonDocument.Parse(output);
            Assert.Equal(
                finding.GetProperty("id").GetString(),
                detail.RootElement.GetProperty("selection").GetProperty("id").GetString());
        }
    }

    [Fact]
    public void UpperCaseHexSelectsTheSameFinding()
    {
        SeedVanishing();

        string id = FirstId();

        var (code, output, _) = Run("--id", "f_" + id[2..].ToUpperInvariant(), "--format", "json");

        Assert.Equal(0, code);

        using JsonDocument detail = JsonDocument.Parse(output);
        Assert.Equal(id, detail.RootElement.GetProperty("selection").GetProperty("id").GetString());
    }

    [Fact]
    public void AnUpperCasePrefixIsAccepted()
    {
        SeedVanishing();

        string id = FirstId();

        var (code, _, _) = Run("--id", id.ToUpperInvariant());

        Assert.Equal(0, code);
    }

    /// <summary>
    /// The printed drill-down lands on the report it was printed from, window included.
    /// </summary>
    /// <remarks>
    /// Asserted on the command and on the window it opens: a drill-down that dropped <c>--runs</c>
    /// would open the default window, which on a longer store is a different report.
    /// </remarks>
    [Fact]
    public void TheDrillDownRepeatsTheWindowItWasPrintedFrom()
    {
        SeedVanishing();

        JsonElement finding = Json("--runs", "8").GetProperty("findings")[0];
        string drillDown = finding.GetProperty("drillDown").GetString()!;

        Assert.EndsWith(" --assembly MyApp.Tests --runs 8", drillDown, StringComparison.Ordinal);

        var (code, output, _) = Run([.. drillDown.Split(' ')[2..], "--format", "json"]);

        Assert.Equal(0, code);

        using JsonDocument detail = JsonDocument.Parse(output);
        Assert.Equal(8, detail.RootElement.GetProperty("window").GetProperty("sessionCount").GetInt32());
    }

    /// <summary>
    /// The way back from a detail view reached by a drill-down names the assembly the drill-down did.
    /// </summary>
    [Fact]
    public void TheWayBackNamesTheAssemblyTheDetailWasOpenedIn()
    {
        SeedVanishing();
        SeedOtherAssembly();

        string id = FirstId("--assembly", "MyApp.Tests");

        var (code, output, _) = Run("--id", id, "--assembly", "MyApp.Tests");

        Assert.Equal(0, code);
        Assert.Contains("all: xping report --all --assembly MyApp.Tests", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AReportNarrowedByKindOffersNoDetailLine()
    {
        SeedVanishing();

        Assert.Contains("Detail of row 1: ", Run().Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Detail of row", Run("--kind", "Vanished").Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ATextReportOfAReportedIdSucceeds()
    {
        SeedVanishing();

        string id = FirstId();

        var (code, output, error) = Run("--id", id);

        Assert.Equal(0, code);
        Assert.Empty(error);
        Assert.Contains(id, output, StringComparison.Ordinal);
    }

    /// <summary>
    /// An absent id exits 3 with the envelope still on standard output for a script to read.
    /// </summary>
    [Fact]
    public void AnAbsentIdExitsThreeWithAParsableEnvelope()
    {
        SeedVanishing();

        var (code, output, error) = Run("--id", "f_00000000", "--format", "json");

        Assert.Equal(ExitCodes.FindingNotReported, code);

        using JsonDocument detail = JsonDocument.Parse(output);
        JsonElement root = detail.RootElement;

        Assert.Empty(root.GetProperty("findings").EnumerateArray());
        Assert.Equal(0, root.GetProperty("truncated").GetProperty("shown").GetInt32());
        Assert.Equal(3, root.GetProperty("summary").GetProperty("findings").GetInt32());

        JsonElement selection = root.GetProperty("selection");
        Assert.False(selection.GetProperty("reported").GetBoolean());
        Assert.Equal(JsonValueKind.Null, selection.GetProperty("kind").ValueKind);
        Assert.Equal(JsonValueKind.Null, selection.GetProperty("row").ValueKind);

        Assert.Equal(
            "Finding f_00000000 is not reported in this window (MyApp.Tests, 8 runs)." + Environment.NewLine,
            error);
    }

    /// <summary>
    /// A text report of nothing is not printed: the message already says what is absent.
    /// </summary>
    [Fact]
    public void AnAbsentIdPrintsNothingToStandardOutputInText()
    {
        SeedVanishing();

        var (code, output, error) = Run("--id", "f_00000000");

        Assert.Equal(ExitCodes.FindingNotReported, code);
        Assert.Empty(output);
        Assert.Contains("is not reported in this window", error, StringComparison.Ordinal);
    }

    /// <summary>
    /// An id naming another claim about a reported subject points at where that subject is now.
    /// </summary>
    [Fact]
    public void AnIdFromAnotherKindNamesTheSubjectsCurrentFinding()
    {
        SeedVanishing();

        JsonElement finding = Json().GetProperty("findings")[0];
        string fingerprint = finding.GetProperty("subject").GetProperty("fingerprint").GetString()!;
        string moved = FindingId.Compute(FindingKind.Flaky, fingerprint);

        var (code, _, error) = Run("--id", moved);

        Assert.Equal(ExitCodes.FindingNotReported, code);
        Assert.Equal(
            $"Finding {moved} (flaky) is not reported in this window (MyApp.Tests, 8 runs). " +
            $"Its subject is reported as {finding.GetProperty("id").GetString()} (stopped running, row 1)." +
            Environment.NewLine,
            error);
    }

    [Fact]
    public void AnIdFromAnotherAssemblyIsNotReportedAndTheMessageCountsTheOthers()
    {
        SeedVanishing();
        SeedOtherAssembly();

        string id = FirstId("--assembly", "MyApp.Tests");

        var (code, _, error) = Run("--id", id, "--assembly", Other);

        Assert.Equal(ExitCodes.FindingNotReported, code);
        Assert.Contains($"({Other}, ", error, StringComparison.Ordinal);
        Assert.EndsWith(
            " 1 other assembly in this store (use --assembly to switch)." + Environment.NewLine,
            error,
            StringComparison.Ordinal);

        Assert.Equal(0, Run("--id", id, "--assembly", "MyApp.Tests").Code);
    }

    [Fact]
    public void ANarrowerWindowCanLeaveAnIdOut()
    {
        SeedVanishing();

        string id = FirstId();

        var (code, _, error) = Run("--id", id, "--runs", "3");

        Assert.Equal(ExitCodes.FindingNotReported, code);
        Assert.Contains("(MyApp.Tests, 3 runs)", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("f_2a91")]
    [InlineData("g_2a91c0de")]
    [InlineData("f_2a91c0dez")]
    [InlineData("2a91c0de12")]
    [InlineData("f_2a91c0dg")]
    public void AMalformedIdIsRejectedAtParseTime(string id)
    {
        var (code, output, error) = Run("--id", id);

        Assert.Equal(ExitCodes.InsufficientData, code);
        Assert.Empty(output);
        Assert.Contains(
            $"--id expects a finding id of the form f_ followed by 8 hex digits, got '{id}'.",
            error,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Every flag that changes which findings exist, or speaks for more than one, is refused.
    /// </summary>
    /// <remarks>
    /// Against an empty store, so a rejection that happened after the store was opened would say
    /// there were no runs instead.
    /// </remarks>
    [Theory]
    [InlineData("--id and --kind are mutually exclusive.", "--kind", "Flaky")]
    [InlineData("--id and --top are mutually exclusive.", "--top", "3")]
    [InlineData("--id and --all are mutually exclusive.", "--all")]
    [InlineData("--id and --fail-on are mutually exclusive.", "--fail-on", "high")]
    [InlineData("--id and --summary are mutually exclusive.", "--summary")]
    [InlineData("--id and --format summary are mutually exclusive.", "--format", "summary")]
    public void FlagsThatSpeakForOtherFindingsAreRejected(string message, params string[] flag)
    {
        var (code, output, error) = Run(["--id", "f_2a91c0de", .. flag]);

        Assert.Equal(ExitCodes.InsufficientData, code);
        Assert.Empty(output);
        Assert.Contains(message, error, StringComparison.Ordinal);
        Assert.DoesNotContain("No runs recorded", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--json")]
    [InlineData("--ascii")]
    [InlineData("--no-color")]
    public void OrthogonalFlagsAreAccepted(string flag)
    {
        SeedVanishing();

        Assert.Equal(0, Run("--id", FirstId(), flag).Code);
    }

    /// <summary>
    /// Writes eight runs in which three tests stop after the fifth, so the report holds three
    /// <c>Vanished</c> findings.
    /// </summary>
    /// <remarks>
    /// The shape <c>ReportEnvelopeTests</c> seeds, for the reasons it gives: one more stable test
    /// than vanishing ones keeps the later runs above the partial-run share and the family small
    /// enough to clear its correction.
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

    /// <summary>Writes runs of a second assembly, newer than every other, so it is the one scoped to.</summary>
    private static void SeedOtherAssembly()
    {
        ILocalSessionStore store = LocalSessionStore.Create();

        for (int i = 20; i < 26; i++)
            store.Write(TestSessionFactory.Session(i, [TestSessionFactory.Execution("Elsewhere", assembly: Other)]));
    }

    private static string FirstId(params string[] args) =>
        Json(args).GetProperty("findings")[0].GetProperty("id").GetString()!;

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

        int code = Program.Run(["report", .. args], output, error);
        return (code, output.ToString(), error.ToString());
    }
}
