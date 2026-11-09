/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Auth;
using Xping.Cli.Cloud;
using Xping.Cli.Commands;
using Xping.Cli.Report;
using Xping.Cli.Tests.Report;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Commands;

public sealed class CliSurfaceTests : IDisposable
{
    private readonly string _root;

    public CliSurfaceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-surface-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    // Sessions are addressed by ordinal, so seeding two assemblies in one test has to keep handing
    // out fresh ones or the second suite overwrites the first.
    private int _nextOrdinal = 1000;

    /// <summary>
    /// Writes one session per outcome for the given assembly.
    /// </summary>
    private void Seed(string assembly, params bool[] outcomes)
    {
        ILocalSessionStore store = LocalSessionStore.Create(storePath: _root);

        for (int i = 0; i < outcomes.Length; i++)
        {
            store.Write(TestSessionFactory.Session(
                _nextOrdinal++,
                [
                    TestSessionFactory.Execution(
                        "Sample",
                        outcome: outcomes[i] ? TestOutcome.Passed : TestOutcome.Failed,
                        assembly: assembly)
                ]));
        }
    }

    /// <summary>
    /// Writes <paramref name="count"/> passing sessions for the given assembly.
    /// </summary>
    private void SeedSessions(string assembly, int count, int startOrdinal = 0)
    {
        ILocalSessionStore store = LocalSessionStore.Create(storePath: _root);

        for (int i = 0; i < count; i++)
        {
            store.Write(TestSessionFactory.Session(
                startOrdinal + i,
                [TestSessionFactory.Execution("Sample", assembly: assembly)]));
        }
    }

    /// <summary>
    /// Writes <paramref name="count"/> sessions that each recorded both assemblies, as one test host
    /// running two test projects does.
    /// </summary>
    private void SeedMixedSessions(string first, string second, int count)
    {
        ILocalSessionStore store = LocalSessionStore.Create(storePath: _root);

        for (int i = 0; i < count; i++)
        {
            store.Write(TestSessionFactory.Session(
                _nextOrdinal++,
                [
                    TestSessionFactory.Execution("FirstSuiteTest", assembly: first),
                    TestSessionFactory.Execution("SecondSuiteTest", assembly: second)
                ]));
        }
    }

    /// <summary>
    /// Writes one session whose executions name no assembly, as a run recorded before identity
    /// generation completed looks.
    /// </summary>
    private void SeedUnattributableSession()
    {
        ILocalSessionStore store = LocalSessionStore.Create(storePath: _root);

        store.Write(TestSessionFactory.Session(
            _nextOrdinal++,
            [TestSessionFactory.Execution("Mystery", assembly: string.Empty)]));
    }

    /// <summary>
    /// Seeds, then overwrites every file the seeding added with bytes that are no session, keeping
    /// the names, and so the tags, the store gave them.
    /// </summary>
    private void SeedUnreadable(Action seed)
    {
        string directory = LocalSessionStore.Create(storePath: _root).SessionsPath!;
        HashSet<string> before = Directory.Exists(directory) ? [.. Directory.GetFiles(directory)] : [];

        seed();

        foreach (string file in Directory.GetFiles(directory).Where(f => !before.Contains(f)))
            File.WriteAllBytes(file, [0x00]);
    }

    private (int Code, string Output) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Rendered as if a person were watching, so the parts of the output only a terminal gets are
        // exercised rather than silently suppressed. The cloud invitation is not: ReviewFixesTests
        // and ReportEnrichmentTests cover it.
        int code = Program.Run(args, output, error, input: null, isTerminal: true, configureServices: OfflineCli.Store(_root, OfflineCli.NoBanner));
        return (code, output.ToString() + error.ToString());
    }

    // -----------------------------------------------------------------------
    // report
    // -----------------------------------------------------------------------

    [Fact]
    public void ReportOnAnEmptyStoreExitsWithInsufficientDataNotFailure()
    {
        // A build step has to tell "I looked and found problems" apart from "I could not look".
        var (code, output) = Run("report");

        Assert.Equal(2, code);
        Assert.Contains("No runs recorded yet", output, StringComparison.Ordinal);
    }

    [Fact]
    public void APipedReportCarriesNothingButTheReport()
    {
        // A finding, so a terminal run would print the invitation; the pipe has to leave it out.
        Seed("Beta.Tests", [.. Enumerable.Range(0, 12).Select(i => i % 2 == 0)]);

        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(
            ["report"], output, error, input: null, isTerminal: false, configureServices: OfflineCli.Store(_root));

        string report = output.ToString();

        Assert.Equal(0, code);

        // `xping report | pbcopy` has to copy a report and nothing else.
        Assert.DoesNotContain("xping.io/start", report + error, StringComparison.Ordinal);

        // Run second: the pipe consumed no daily allowance, so the terminal still has one to spend.
        using var terminal = new StringWriter();
        Program.Run(["report"], terminal, terminal, input: null, isTerminal: true, configureServices: OfflineCli.Store(_root));
        Assert.Contains("xping.io/start", terminal.ToString(), StringComparison.Ordinal);

        // And it has to arrive drawable: no escape codes, no glyphs a legacy code page cannot show.
        Assert.DoesNotContain("\u001b", report, StringComparison.Ordinal);
        Assert.All(report, c => Assert.True(c < 0x80, $"non-ASCII '{c}' in a piped report"));

        // Not even blank lines around it. Breathing room is for a person looking at a terminal; a
        // clipboard and a script both do better without it.
        Assert.StartsWith("Xping", report, StringComparison.Ordinal);
        Assert.False(report.EndsWith("\n\n", StringComparison.Ordinal), "a piped report is padded");
    }

    /// <summary>
    /// Every line inside the fence fits the fence, through the real command and a real store.
    /// </summary>
    /// <remarks>
    /// The golden sweep asserts this over envelopes built by hand. This asserts it over an envelope
    /// the analysis produced, so a value only the builder can make — a headline phrased from real
    /// evidence, a path resolved from a real identity — cannot overrun the fence unobserved.
    /// </remarks>
    [Fact]
    public void APipedReportFitsInsideTheFence()
    {
        SeedSessions("Alpha.Tests", 6);

        using var output = new StringWriter();
        using var error = new StringWriter();

        Program.Run(["report"], output, error, input: null, isTerminal: false, configureServices: OfflineCli.Store(_root, OfflineCli.NoBanner));

        foreach (string line in ReportText.Fenced(output.ToString()))
        {
            if (line.Length <= ReportText.FenceWidth)
                continue;

            Assert.True(
                !line.TrimStart().Contains(' ', StringComparison.Ordinal),
                $"'{line}' is {line.Length} columns, over the {ReportText.FenceWidth} the fence " +
                "allows, and is not a single unbreakable token");
        }
    }

    /// <summary>
    /// <c>--no-color</c> strips the decoration and leaves the report identical.
    /// </summary>
    /// <remarks>
    /// The flag exists for the caller who cannot set <c>NO_COLOR</c>, so it has to reach the same
    /// place the pipe does: not merely "fewer escapes" but the same characters in the same columns.
    /// The two right-aligned annotations are padded from the plain marker's width, and a report that
    /// shifted by the length of an escape sequence would show it here first.
    /// </remarks>
    [Fact]
    public void NoColorOnATerminalProducesTheSameReportAPipeReceives()
    {
        SeedSessions("Alpha.Tests", 6);

        var (code, terminal) = Run("report", "--ascii", "--no-color");

        Assert.Equal(0, code);
        Assert.DoesNotContain('\u001b', terminal);

        using var piped = new StringWriter();
        using var error = new StringWriter();

        Program.Run(["report"], piped, error, input: null, isTerminal: false, configureServices: OfflineCli.Store(_root, OfflineCli.NoBanner));

        // The terminal run is padded; the report between the fences is what the two have to agree on.
        Assert.Equal(ReportText.Fenced(piped.ToString()), ReportText.Fenced(terminal));
    }

    /// <summary>
    /// The colour variables are read from the command's environment, so the host decides them and
    /// not whatever the shell running the tests happens to export.
    /// </summary>
    [Fact]
    public void ForceColorInTheCommandsEnvironmentColoursAPipedReport()
    {
        // A test that keeps flipping, so the report has a finding to colour.
        Seed("Alpha.Tests", [.. Enumerable.Range(0, 12).Select(i => i % 2 == 0)]);

        using var plain = new StringWriter();
        using var forced = new StringWriter();
        using var error = new StringWriter();

        Program.Run(["report"], plain, error, input: null, isTerminal: false, configureServices: OfflineCli.Store(_root, OfflineCli.NoBanner));
        Program.Run(
            ["report"], forced, error, input: null, isTerminal: false,
            configureServices: OfflineCli.Store(_root, OfflineCli.NoBanner, ("FORCE_COLOR", "1")));

        Assert.DoesNotContain('\u001b', plain.ToString());
        Assert.Contains('\u001b', forced.ToString());
    }

    [Fact]
    public void ATerminalReportIsGivenBreathingRoomOnEitherSide()
    {
        SeedSessions("Alpha.Tests", 6);

        var (code, output) = Run("report", "--ascii");

        Assert.Equal(0, code);
        Assert.StartsWith(Environment.NewLine, output, StringComparison.Ordinal);
        Assert.EndsWith(Environment.NewLine + Environment.NewLine, output, StringComparison.Ordinal);
    }

    [Fact]
    public void ABareReportOverSeveralAssembliesHasARowForEachAndNamesNone()
    {
        // Beta ran last. Before #346 the report was Beta's alone, and Alpha's finding went unseen.
        Seed("Alpha.Tests", [.. Enumerable.Range(0, 12).Select(i => i % 2 == 0)]);
        SeedSessions("Beta.Tests", 6, startOrdinal: 5000);

        var (code, output) = Run("report", "--ascii");

        Assert.Equal(0, code);
        Assert.Contains("Xping | 2 assemblies", output, StringComparison.Ordinal);
        Assert.Contains("Beta.Tests", output, StringComparison.Ordinal);

        // Most severe first, so the row with the finding is the one the command opens.
        Assert.True(
            output.IndexOf("Alpha.Tests", StringComparison.Ordinal) < output.IndexOf("Beta.Tests", StringComparison.Ordinal),
            output);
        Assert.Contains("Details: xping report --assembly Alpha.Tests", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AStoreWithOneAssemblyGetsThatAssemblysReport()
    {
        SeedSessions("Alpha.Tests", 6);

        var (code, output) = Run("report", "--ascii");

        Assert.Equal(0, code);
        Assert.Contains("Xping | Alpha.Tests | 6 runs", output, StringComparison.Ordinal);
        Assert.DoesNotContain("assemblies", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportScopedExplicitlyIsThatAssemblysReport()
    {
        SeedSessions("Alpha.Tests", 6);
        SeedSessions("Beta.Tests", 6, startOrdinal: 10);

        var (_, output) = Run("report", "--assembly", "Alpha.Tests", "--ascii");

        Assert.Contains("Xping | Alpha.Tests | 6 runs", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Beta.Tests", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ASolutionWideRunIsReportableUnderEveryAssemblyItCovered()
    {
        // One `dotnet test` over a solution, one session file, two suites. Before this, one of the
        // two reports came back empty and the other counted the wrong tests.
        SeedMixedSessions("Alpha.Tests", "Beta.Tests", 6);

        var (alphaCode, alpha) = Run("report", "--assembly", "Alpha.Tests", "--ascii");
        var (betaCode, beta) = Run("report", "--assembly", "Beta.Tests", "--ascii");

        Assert.Equal(0, alphaCode);
        Assert.Equal(0, betaCode);
        Assert.Contains("6 runs", alpha, StringComparison.Ordinal);
        Assert.Contains("6 runs", beta, StringComparison.Ordinal);
        Assert.Contains("Alpha.Tests", alpha, StringComparison.Ordinal);
        Assert.Contains("Beta.Tests", beta, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOverviewHasARowForEachAssemblyASharedRunCovered()
    {
        SeedMixedSessions("Alpha.Tests", "Beta.Tests", 6);

        var (_, output) = Run("report", "--ascii");

        Assert.Contains("Xping | 2 assemblies", output, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(output, @"1 test \| 0 flagged \| 6 runs"));
    }

    [Fact]
    public void TheOverviewLeavesOutRunsThatNameNoAssembly()
    {
        // Pooling the unattributable run into a row would give numbers no --assembly value reproduces.
        SeedSessions("Alpha.Tests", 6);
        SeedSessions("Beta.Tests", 6, startOrdinal: 10);
        SeedUnattributableSession();

        var (code, output) = Run("report", "--ascii");

        Assert.Equal(0, code);
        Assert.Contains("Xping | 2 assemblies", output, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(output, @"\| 6 runs \|"));
    }

    /// <summary>
    /// Regression for #346: <c>--fail-on</c> judged only the newest assembly, so a build passed while
    /// an older suite had findings at the threshold.
    /// </summary>
    [Fact]
    public void FailOnJudgesEveryAssemblyNotJustTheNewest()
    {
        Seed("Alpha.Tests", [.. Enumerable.Range(0, 12).Select(i => i % 2 == 0)]);
        SeedSessions("Beta.Tests", 6, startOrdinal: 5000);

        var (code, _) = Run("report", "--fail-on", "low", "--ascii");

        Assert.Equal(ExitCodes.FindingsAtThreshold, code);
    }

    [Fact]
    public void FailOnPassesWhenNoAssemblyReachesTheThreshold()
    {
        SeedSessions("Alpha.Tests", 6);
        SeedSessions("Beta.Tests", 6, startOrdinal: 10);

        var (code, _) = Run("report", "--fail-on", "low", "--ascii");

        Assert.Equal(ExitCodes.Success, code);
    }

    [Fact]
    public void AnAssemblyWithNoWindowIsARowAndTheOthersStillDecideTheExitCode()
    {
        // Beta's runs are all older than the --since date, so its window cannot open; Alpha's can.
        SeedSessions("Beta.Tests", 6);
        Seed("Alpha.Tests", [.. Enumerable.Range(0, 12).Select(i => i % 2 == 0)]);
        string since = TestSessionFactory.Epoch.AddMinutes(500).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        var (code, output) = Run("report", "--since", since, "--fail-on", "low", "--ascii");

        Assert.Equal(ExitCodes.FindingsAtThreshold, code);
        Assert.Contains("no window", output, StringComparison.Ordinal);
        Assert.Contains("No sessions recorded on or after", output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSummaryGivesAnAssemblyWithNoWindowALineThatSaysWhy()
    {
        SeedSessions("Beta.Tests", 6);
        Seed("Alpha.Tests", [.. Enumerable.Range(0, 12).Select(i => i % 2 == 0)]);
        string since = TestSessionFactory.Epoch.AddMinutes(500).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        var (_, output) = Run("report", "--since", since, "--summary", "--ascii");

        Assert.Contains("Xping: Beta.Tests: No sessions recorded on or after", output, StringComparison.Ordinal);
        Assert.Contains("Alpha.Tests", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WhenNoAssemblysWindowOpensTheOverviewIsUnavailable()
    {
        SeedSessions("Alpha.Tests", 6);
        SeedSessions("Beta.Tests", 6, startOrdinal: 10);

        var (code, output) = Run("report", "--since", "2099-01-01", "--fail-on", "low", "--ascii");

        Assert.Equal(ExitCodes.InsufficientData, code);
        Assert.Contains("No sessions recorded on or after 2099-01-01", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Regression: the warning summed every assembly's count, so a run both assemblies covered was
    /// counted twice.
    /// </summary>
    [Fact]
    public void TheOverviewCountsARunStampedAheadOnceThoughTwoAssembliesShareIt()
    {
        SeedSessions("Alpha.Tests", 6);
        SeedSessions("Beta.Tests", 6, startOrdinal: 10);

        LocalSessionStore.Create(storePath: _root).Write(TestSessionFactory.Session(
            _nextOrdinal++,
            [
                TestSessionFactory.Execution("FirstSuiteTest", assembly: "Alpha.Tests"),
                TestSessionFactory.Execution("SecondSuiteTest", assembly: "Beta.Tests")
            ],
            startedAt: new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        var (_, output) = Run("report", "--ascii");

        Assert.Contains("warning: 1 run(s) stamped ahead of this machine's clock", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Regression: the warning took the largest assembly's count, but each assembly's read skips
    /// the files tagged for another, so one assembly's damaged run hid the other's.
    /// </summary>
    [Fact]
    public void TheOverviewCountsEachUnreadableRunOnceWhicheverAssemblysReadMetIt()
    {
        SeedSessions("Alpha.Tests", 6);
        SeedSessions("Beta.Tests", 6, startOrdinal: 10);
        SeedUnreadable(() => SeedSessions("Alpha.Tests", 1, startOrdinal: 20));
        SeedUnreadable(() => SeedSessions("Beta.Tests", 1, startOrdinal: 21));
        SeedUnreadable(() => SeedMixedSessions("Alpha.Tests", "Beta.Tests", 1));

        var (_, output) = Run("report", "--ascii");

        Assert.Contains("warning: 3 unreadable run(s) skipped.", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Regression: with no window open, only the first assembly's read warnings were said.
    /// </summary>
    [Fact]
    public void WhenNoAssemblysWindowOpensEveryAssemblysReadWarningsAreSaid()
    {
        SeedSessions("Alpha.Tests", 6);
        SeedSessions("Beta.Tests", 6, startOrdinal: 10);
        SeedUnreadable(() => SeedSessions("Alpha.Tests", 1, startOrdinal: 20));
        SeedUnreadable(() => SeedSessions("Beta.Tests", 1, startOrdinal: 21));

        var (code, output) = Run("report", "--since", "2099-01-01", "--ascii");

        Assert.Equal(ExitCodes.InsufficientData, code);
        Assert.Equal(1, Regex.Count(output, "No sessions recorded on or after 2099-01-01"));
        Assert.Contains("warning: 2 unreadable run(s) skipped.", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--kind", "flaky", "--kind")]
    [InlineData("--top", "3", "--top")]
    [InlineData("--all", null, "--all")]
    public void AFindingListFlagWithoutAnAssemblyIsRefusedOverSeveral(string flag, string? value, string named)
    {
        SeedSessions("Alpha.Tests", 6);
        SeedSessions("Beta.Tests", 6, startOrdinal: 10);

        var (code, output) = Run(value == null ? ["report", flag] : ["report", flag, value]);

        Assert.Equal(ExitCodes.InsufficientData, code);
        Assert.Contains($"{named} needs --assembly", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AFindingListFlagStillWorksOverAStoreWithOneAssembly()
    {
        SeedSessions("Alpha.Tests", 6);

        var (code, _) = Run("report", "--top", "3", "--ascii");

        Assert.Equal(0, code);
    }

    [Fact]
    public void TheOverviewInJsonIsItsOwnEnvelope()
    {
        Seed("Alpha.Tests", [.. Enumerable.Range(0, 12).Select(i => i % 2 == 0)]);
        SeedSessions("Beta.Tests", 6, startOrdinal: 5000);

        var (code, output) = Run("report", "--json");

        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(output);
        JsonElement root = doc.RootElement;

        Assert.Equal("1.29", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("overview", root.GetProperty("scope").GetString());

        JsonElement[] rows = [.. root.GetProperty("assemblies").EnumerateArray()];
        Assert.Equal(["Alpha.Tests", "Beta.Tests"], rows.Select(r => r.GetProperty("assembly").GetString()));
        Assert.Equal("xping report --assembly Alpha.Tests", rows[0].GetProperty("command").GetString());
        Assert.Equal(JsonValueKind.Null, rows[0].GetProperty("unavailable").ValueKind);
    }

    /// <summary>
    /// Report-format Amendment 6B: a row never says anything its assembly's report does not.
    /// </summary>
    [Fact]
    public void EveryRowIsTheHeaderOfTheReportItsCommandOpens()
    {
        Seed("Alpha.Tests", [.. Enumerable.Range(0, 12).Select(i => i % 2 == 0)]);
        SeedSessions("Beta.Tests", 6, startOrdinal: 5000);
        SeedMixedSessions("Beta.Tests", "Gamma.Tests", 3);

        var (_, overview) = Run("report", "--json");
        using var doc = JsonDocument.Parse(overview);

        foreach (JsonElement row in doc.RootElement.GetProperty("assemblies").EnumerateArray())
        {
            var (_, report) = Run("report", "--assembly", row.GetProperty("assembly").GetString()!, "--json");
            using var single = JsonDocument.Parse(report);

            // Compared compact: the two envelopes indent the same object to different depths.
            Assert.Equal(JsonSerializer.Serialize(single.RootElement.GetProperty("window")), JsonSerializer.Serialize(row.GetProperty("window")));
            Assert.Equal(JsonSerializer.Serialize(single.RootElement.GetProperty("summary")), JsonSerializer.Serialize(row.GetProperty("summary")));
        }
    }

    [Fact]
    public void TheSummaryOverSeveralAssembliesIsALineEach()
    {
        Seed("Alpha.Tests", [.. Enumerable.Range(0, 12).Select(i => i % 2 == 0)]);
        SeedSessions("Beta.Tests", 6, startOrdinal: 5000);

        var (code, output) = Run("report", "--summary");

        Assert.Equal(0, code);
        string[] lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.EndsWith("in 12 runs of Alpha.Tests", lines[0], StringComparison.Ordinal);
        Assert.Equal("Xping: no findings in 6 runs of Beta.Tests", lines[1]);
    }

    [Fact]
    public void AStoreWithNothingAttributableIsReportedAsSuchRatherThanPooled()
    {
        SeedUnattributableSession();
        SeedUnattributableSession();

        var (code, output) = Run("report", "--ascii");

        Assert.Equal(ExitCodes.InsufficientData, code);
        Assert.Contains(
            "record which test assembly they belong to", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyStoreIsStillDistinguishableFromAnUnattributableOne()
    {
        var (code, output) = Run("report", "--ascii");

        Assert.Equal(ExitCodes.InsufficientData, code);
        Assert.Contains("No runs recorded yet", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RunsAndSinceAreMutuallyExclusive()
    {
        var (code, output) = Run("report", "--runs", "3", "--since", "abc123");

        Assert.Equal(2, code);
        Assert.Contains("mutually exclusive", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AllAndTopAreMutuallyExclusive()
    {
        var (code, output) = Run("report", "--all", "--top", "2");

        Assert.Equal(2, code);
        Assert.Contains("mutually exclusive", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RunsRejectsANonPositiveValueWithAnActionableMessage()
    {
        var (code, output) = Run("report", "--runs", "0");

        Assert.Equal(2, code);
        Assert.Contains("--runs expects a positive number", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownKindNamesTheKindsThatExist()
    {
        // The built-in enum converter reports the CLR type name, which tells a user nothing.
        var (code, output) = Run("report", "--kind", "Nonsense");

        Assert.Equal(2, code);
        Assert.Contains("Unknown finding kind 'Nonsense'", output, StringComparison.Ordinal);
        Assert.Contains("Vanished", output, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // --format json
    // -----------------------------------------------------------------------

    [Fact]
    public void JsonEmitsTheVersionedEnvelope()
    {
        SeedSessions("Alpha.Tests", 6);

        var (code, output) = Run("report", "--format", "json");

        Assert.Equal(0, code);

        using JsonDocument doc = JsonDocument.Parse(output);
        JsonElement root = doc.RootElement;

        Assert.Equal("1.29", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(6, root.GetProperty("window").GetProperty("sessionCount").GetInt32());
        Assert.Equal("default", root.GetProperty("window").GetProperty("resolution").GetString());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("tests").GetInt32());
    }

    [Fact]
    public void Report_NoCredential_NoNetwork()
    {
        // With nothing to sign in with, the report is today's local report: no request of any kind,
        // and only a null `cloud` added to the envelope.
        SeedSessions("Alpha.Tests", 6);
        using var handler = new NoNetworkHandler();

        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(
            ["report", "--json"], output, error, input: null, isTerminal: true,
            configureServices: services =>
            {
                OfflineCli.Store(_root, OfflineCli.NoBanner)(services);
                services.AddHttpClient(CloudHttp.ClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
                services.AddHttpClient(AuthHttpClients.OAuth).ConfigurePrimaryHttpMessageHandler(() => handler);
            });

        Assert.Equal(0, code);
        Assert.Equal(0, handler.Requests);
        Assert.Empty(error.ToString());

        using JsonDocument doc = JsonDocument.Parse(output.ToString());
        Assert.Equal("not-attempted", doc.RootElement.GetProperty("context").GetProperty("cloud").GetProperty("status").GetString());
    }

    [Fact]
    public void JsonIsStillTheEnvelopeWhenSelectedByTheLegacyFlag()
    {
        // `--json` shipped before `--format`; scripts using it must keep working.
        SeedSessions("Alpha.Tests", 6);

        var (code, output) = Run("report", "--json");

        Assert.Equal(0, code);
        using JsonDocument doc = JsonDocument.Parse(output);
        Assert.Equal("1.29", doc.RootElement.GetProperty("schemaVersion").GetString());
    }

    [Fact]
    public void RunsIsStillAcceptedUnderItsLegacyName()
    {
        SeedSessions("Alpha.Tests", 8);

        var (code, output) = Run("report", "--last", "3", "--format", "json");

        Assert.Equal(0, code);
        using JsonDocument doc = JsonDocument.Parse(output);
        Assert.Equal(3, doc.RootElement.GetProperty("window").GetProperty("sessionCount").GetInt32());
    }

    [Fact]
    public void JsonEmitsNoRenderedReport()
    {
        // A script consuming stdout must not have to strip a box-drawn block first.
        SeedSessions("Alpha.Tests", 6);

        var (_, output) = Run("report", "--format", "json");

        Assert.DoesNotContain("Xping report", output, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // where
    // -----------------------------------------------------------------------

    [Fact]
    public void WherePrintsTheStorePathAndPerAssemblyCounts()
    {
        // Arrange
        Seed("Alpha.Tests", true, true);
        Seed("Beta.Tests", true);

        // Act
        var (code, output) = Run("where");

        // Assert
        Assert.Equal(0, code);
        Assert.Contains(_root, output, StringComparison.Ordinal);
        Assert.Contains("3 runs", output, StringComparison.Ordinal);
        Assert.Contains("Alpha.Tests", output, StringComparison.Ordinal);
        Assert.Contains("Beta.Tests", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WhereCountsASharedRunUnderEveryAssemblyItCovered()
    {
        // Three files, but six runs of history: each run belongs to both suites. The file count and
        // the per-assembly counts answer different questions and are not meant to agree.
        SeedMixedSessions("Alpha.Tests", "Beta.Tests", 3);

        var (code, output) = Run("where");

        Assert.Equal(0, code);
        Assert.Contains("3 runs", output, StringComparison.Ordinal);
        Assert.Matches(@"Alpha\.Tests\s+3 runs", output);
        Assert.Matches(@"Beta\.Tests\s+3 runs", output);
    }

    [Fact]
    public void WhereAccountsForRunsThatNameNoAssemblyInsteadOfListingThem()
    {
        // The SDK no longer writes such a run, but a store can still hold some from before it
        // stopped. They fit no assembly line, so the header would otherwise claim more runs than
        // the list explains.
        Seed("Alpha.Tests", true);
        SeedUnattributableSession();
        SeedUnattributableSession();

        var (code, output) = Run("where");

        Assert.Equal(0, code);
        Assert.Contains("3 runs", output, StringComparison.Ordinal);
        Assert.Contains("2 runs name no assembly and are not shown", output, StringComparison.Ordinal);
        Assert.Matches(@"Alpha\.Tests\s+1 run", output);
        Assert.DoesNotContain("(unknown)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WhereReportsAnEmptyStoreWithoutFailing()
    {
        var (code, output) = Run("where");

        Assert.Equal(0, code);
        Assert.Contains("no runs recorded yet", output, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // clear
    // -----------------------------------------------------------------------

    [Fact]
    public void ClearRefusesWithoutConfirmationWhenInputIsRedirected()
    {
        // Arrange — under `dotnet test` stdin is redirected, which is exactly the non-interactive
        // case the guard exists for.
        Seed("Alpha.Tests", true, true, true);

        // Act
        var (code, output) = Run("clear");

        // Assert
        Assert.Equal(1, code);
        Assert.Contains("Refusing to delete", output, StringComparison.Ordinal);
        Assert.Equal(3, LocalSessionStore.Create(storePath: _root).ReadRecent(100).Sessions.Count);
    }

    [Fact]
    public void ClearWithForceDeletesEverything()
    {
        // Arrange
        Seed("Alpha.Tests", true, true, true);

        // Act
        var (code, output) = Run("clear", "--force");

        // Assert
        Assert.Equal(0, code);
        Assert.Contains("Deleted 3 runs", output, StringComparison.Ordinal);
        Assert.Empty(LocalSessionStore.Create(storePath: _root).ReadRecent(100).Sessions);
    }

    [Fact]
    public void ClearScopedToAnAssemblyLeavesOthersAlone()
    {
        // Arrange
        Seed("Alpha.Tests", true, true);
        Seed("Beta.Tests", true, true, true);

        // Act
        var (code, output) = Run("clear", "--assembly", "Alpha.Tests", "--force");

        // Assert
        Assert.Equal(0, code);
        Assert.Contains("Deleted 2 runs", output, StringComparison.Ordinal);

        var remaining = LocalSessionStore.Create(storePath: _root).ReadRecent(100).Sessions;
        Assert.Equal(3, remaining.Count);
        Assert.All(
            remaining,
            session => Assert.Equal("Beta.Tests", session.Executions.First().Identity.Assembly));
    }

    [Fact]
    public void ClearOnAnEmptyStoreSucceedsQuietly()
    {
        var (code, output) = Run("clear", "--force");

        Assert.Equal(0, code);
        Assert.Contains("Nothing to clear", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ClearScopedToAnAssemblyLeavesTheRestOfASharedRunReportable()
    {
        // The two suites shared every run. Clearing one has to leave the other with its full
        // history, or `xping clear --assembly` would destroy history its caller never named.
        SeedMixedSessions("Alpha.Tests", "Beta.Tests", 4);

        var (code, output) = Run("clear", "--assembly", "Alpha.Tests", "--force");

        Assert.Equal(0, code);
        Assert.Contains("Deleted 4 runs.", output, StringComparison.Ordinal);

        var (reportCode, report) = Run("report", "--assembly", "Beta.Tests", "--ascii");

        Assert.Equal(0, reportCode);
        Assert.Contains("4 runs", report, StringComparison.Ordinal);

        var (_, gone) = Run("report", "--assembly", "Alpha.Tests", "--ascii");

        Assert.Contains("No runs recorded for assembly 'Alpha.Tests'", gone, StringComparison.Ordinal);
    }

    [Fact]
    public void ClearScopedToASharedAssemblyKeepsTheRunOnDisk()
    {
        SeedMixedSessions("Alpha.Tests", "Beta.Tests", 3);

        Run("clear", "--assembly", "Alpha.Tests", "--force");

        var (_, output) = Run("where");

        // Three runs still stored, now belonging to one suite instead of two.
        Assert.Contains("3 runs", output, StringComparison.Ordinal);
        Assert.Contains("Beta.Tests", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Alpha.Tests", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ClearReportsWhenTheAssemblyHasNoRuns()
    {
        Seed("Alpha.Tests", true);

        var (code, output) = Run("clear", "--assembly", "Nonexistent.Tests", "--force");

        Assert.Equal(0, code);
        Assert.Contains("No runs recorded for assembly", output, StringComparison.Ordinal);
        Assert.Single(LocalSessionStore.Create(storePath: _root).ReadRecent(100).Sessions);
    }
}
