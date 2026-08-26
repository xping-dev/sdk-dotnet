/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Commands;
using Xping.Cli.Tests.Report;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Commands;

// Resolves the store from a temp directory and mutates XPING_NO_BANNER.
[Collection("Sequential")]
public sealed class CliSurfaceTests : IDisposable
{
    private readonly string _root;

    public CliSurfaceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-surface-tests", Guid.NewGuid().ToString("N"));
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

    // Sessions are addressed by ordinal, so seeding two assemblies in one test has to keep handing
    // out fresh ones or the second suite overwrites the first.
    private int _nextOrdinal = 1000;

    /// <summary>
    /// Writes one session per outcome for the given assembly.
    /// </summary>
    private void Seed(string assembly, params bool[] outcomes)
    {
        ILocalSessionStore store = LocalSessionStore.Create();

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
    private static void SeedSessions(string assembly, int count, int startOrdinal = 0)
    {
        ILocalSessionStore store = LocalSessionStore.Create();

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
        ILocalSessionStore store = LocalSessionStore.Create();

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

    private static (int Code, string Output) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Rendered as if a person were watching, so the parts of the output only a terminal gets —
        // the scope notice, the cloud invitation — are exercised rather than silently suppressed.
        int code = Program.Run(args, output, error, input: null, isTerminal: true);
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
        SeedSessions("Alpha.Tests", 6);
        SeedSessions("Beta.Tests", 6, startOrdinal: 10);

        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(
            ["report"], output, error, input: null, isTerminal: false);

        string report = output.ToString();

        Assert.Equal(0, code);

        // `xping report | pbcopy` has to copy a report and nothing else.
        Assert.DoesNotContain("Reporting on", report, StringComparison.Ordinal);
        Assert.DoesNotContain("xping.io/start", report, StringComparison.Ordinal);

        // And it has to arrive drawable: no escape codes, no glyphs a legacy code page cannot show.
        Assert.DoesNotContain("\u001b", report, StringComparison.Ordinal);
        Assert.All(report, c => Assert.True(c < 0x80, $"non-ASCII '{c}' in a piped report"));

        // Not even blank lines around it. Breathing room is for a person looking at a terminal; a
        // clipboard and a script both do better without it.
        Assert.StartsWith("Xping", report, StringComparison.Ordinal);
        Assert.False(report.EndsWith("\n\n", StringComparison.Ordinal), "a piped report is padded");
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
    public void ReportScopesToTheNewestAssemblyAndSaysSo()
    {
        SeedSessions("Alpha.Tests", 6);
        SeedSessions("Beta.Tests", 6, startOrdinal: 10);

        var (code, output) = Run("report", "--ascii");

        Assert.Equal(0, code);
        Assert.Contains("Reporting on Beta.Tests", output, StringComparison.Ordinal);
        Assert.Contains("1 other assembly", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportScopedExplicitlyDoesNotPrintTheNotice()
    {
        // The notice exists to flag an omission the user did not ask for.
        SeedSessions("Alpha.Tests", 6);
        SeedSessions("Beta.Tests", 6, startOrdinal: 10);

        var (_, output) = Run("report", "--assembly", "Alpha.Tests", "--ascii");

        Assert.DoesNotContain("Reporting on", output, StringComparison.Ordinal);
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
    public void TheScopeNoticeCountsAnAssemblyThatSharedTheSameRun()
    {
        SeedMixedSessions("Alpha.Tests", "Beta.Tests", 6);

        var (_, output) = Run("report", "--ascii");

        Assert.Contains("Reporting on Alpha.Tests", output, StringComparison.Ordinal);
        Assert.Contains("1 other assembly", output, StringComparison.Ordinal);
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

        Assert.Equal("1.5", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(6, root.GetProperty("window").GetProperty("sessionCount").GetInt32());
        Assert.Equal("default", root.GetProperty("window").GetProperty("resolution").GetString());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("tests").GetInt32());
    }

    [Fact]
    public void JsonIsStillTheEnvelopeWhenSelectedByTheLegacyFlag()
    {
        // `--json` shipped before `--format`; scripts using it must keep working.
        SeedSessions("Alpha.Tests", 6);

        var (code, output) = Run("report", "--json");

        Assert.Equal(0, code);
        using JsonDocument doc = JsonDocument.Parse(output);
        Assert.Equal("1.5", doc.RootElement.GetProperty("schemaVersion").GetString());
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
        Assert.DoesNotContain("Reporting on", output, StringComparison.Ordinal);
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
        Assert.Equal(3, LocalSessionStore.Create().ReadRecent(100).Sessions.Count);
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
        Assert.Empty(LocalSessionStore.Create().ReadRecent(100).Sessions);
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

        var remaining = LocalSessionStore.Create().ReadRecent(100).Sessions;
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
        Assert.Single(LocalSessionStore.Create().ReadRecent(100).Sessions);
    }
}
