/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Rendering;
using Xping.Cli.Reporting;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The overview, as a person reads it: two lines per assembly and the command that opens one.
/// </summary>
public sealed class OverviewTextRendererTests
{
    private static readonly DateTime Newest = new(2026, 8, 3, 14, 5, 0, DateTimeKind.Utc);

    private static AssemblyOverviewDto Row(
        string assembly, int tests, int flagged, int runs, int high = 0, int medium = 0, int low = 0)
    {
        SummaryDto summary = ReportFixtures.Envelope().Summary with
        {
            Tests = tests,
            Flagged = flagged,
            Findings = high + medium + low,
            Counts = new SeverityCountsDto(high, medium, low)
        };

        var window = new WindowDto(Newest.AddDays(-2), Newest, runs, "default", null, 0, []);

        return new AssemblyOverviewDto(assembly, window, summary, null, $"xping report --assembly {assembly}", null);
    }

    private static string[] Render(params AssemblyOverviewDto[] rows)
    {
        using var writer = new StringWriter();
        new OverviewTextRenderer(ReportText.Capabilities(redirected: true))
            .Render(new OverviewEnvelope(ReportEnvelope.CurrentSchemaVersion, ReportScopes.Overview, rows), writer);

        return ReportText.Lines(writer.ToString());
    }

    private static OverviewCloudDto Cloud(
        string status = "ok", string reason = "none", string state = "ok", CloudVerdictCountsDto? verdicts = null, string? projectKey = "shop") =>
        new(status, reason, projectKey, new CloudHealthSummaryDto(state, reason, 40, 0, verdicts ?? CloudVerdictCountsDto.None));

    /// <summary>
    /// Cloud-data Amendment 8F: the row's third line is the token its report's header prints.
    /// </summary>
    [Fact]
    public void ARowCloudAnsweredForEndsOnItsVerdicts()
    {
        string[] lines = Render(Row("Alpha.Tests", 120, 3, 20) with { Cloud = Cloud(verdicts: new CloudVerdictCountsDto(2, 1, 0, 0)) });

        Assert.Equal("  cloud: 2 broken - 1 known flaky", lines[4]);
        Assert.Equal(string.Empty, lines[5]);
    }

    [Theory]
    [InlineData("ok", "none", "ok", "  cloud: no test flagged")]
    [InlineData("partial", "budget", "partial", "  cloud: no test flagged (partial)")]
    [InlineData("unavailable", "timeout", "unavailable", "  cloud: unavailable (timeout) - showing local results only")]
    [InlineData("login-required", "none", "not-attempted", "  cloud: login required - run xping login")]
    [InlineData("no-project", "not-found", "not-attempted", "  cloud: not in this workspace: project shop - pass --project <key>")]
    public void ARowsCloudLineIsItsTokenOrWhyThereIsNone(string status, string reason, string state, string expected)
    {
        string[] lines = Render(Row("Alpha.Tests", 120, 3, 20) with { Cloud = Cloud(status, reason, state) });

        Assert.Equal(expected, lines[4]);
    }

    [Fact]
    public void AReasonEveryRowSharesIsSaidOnceUnderTheHeader()
    {
        OverviewCloudDto timedOut = Cloud("unavailable", "credential-timeout", "unavailable");

        string[] lines = Render(
            Row("Alpha.Tests", 120, 3, 20) with { Cloud = timedOut },
            Row("Beta.Tests", 10, 0, 5) with { Cloud = timedOut });

        Assert.Equal("cloud: unavailable (credential-timeout) - showing local results only", lines[1]);
        Assert.Single(lines, l => l.Contains("cloud:", StringComparison.Ordinal));
    }

    [Fact]
    public void ReasonsThatDifferStayOnTheirRows()
    {
        string[] lines = Render(
            Row("Alpha.Tests", 120, 3, 20) with { Cloud = Cloud(verdicts: new CloudVerdictCountsDto(1, 0, 0, 0)) },
            Row("Beta.Tests", 10, 0, 5) with { Cloud = Cloud("no-project", "not-found", "not-attempted", projectKey: null) });

        Assert.Equal(string.Empty, lines[1]);
        Assert.Contains("  cloud: 1 broken", lines);
        Assert.Contains("  cloud: no project in this workspace for Beta.Tests - pass --project <key>", lines);
    }

    [Fact]
    public void AnUnboundRowNamesItsAssembly()
    {
        string[] lines = Render(Row("Alpha.Tests", 120, 3, 20) with { Cloud = Cloud("no-project", "not-found", "not-attempted", projectKey: null) });

        Assert.Equal("  cloud: no project in this workspace for Alpha.Tests - pass --project <key>", lines[4]);
    }

    [Fact]
    public void ARowCloudWasNotAskedAboutHasNoCloudLine()
    {
        string[] signedOut = Render(Row("Alpha.Tests", 120, 3, 20) with { Cloud = Cloud("not-attempted", "none", "not-attempted") });
        string[] local = Render(Row("Alpha.Tests", 120, 3, 20));

        Assert.Equal(local, signedOut);
        Assert.DoesNotContain(local, l => l.Contains("cloud", StringComparison.Ordinal));
    }

    [Fact]
    public void TheHeaderCountsTheAssemblies()
    {
        string[] lines = Render(Row("Alpha.Tests", 10, 0, 5), Row("Beta.Tests", 10, 0, 5));

        Assert.Equal("Xping | 2 assemblies", lines[0]);
        Assert.Equal(string.Empty, lines[1]);
    }

    [Fact]
    public void ARowIsItsNameAndVerdictThenTheCountsItsReportsHeaderPrints()
    {
        string[] lines = Render(Row("Alpha.Tests", 120, 3, 20, high: 2));

        Assert.StartsWith("Alpha.Tests ", lines[2], StringComparison.Ordinal);
        Assert.EndsWith("2 findings (2 high)", lines[2], StringComparison.Ordinal);
        Assert.Equal(TextReportRenderer.FenceWidth, lines[2].Length);
        Assert.Equal("  120 tests | 3 flagged | 20 runs | last 2026-08-03 14:05", lines[3]);
    }

    [Fact]
    public void ARowCountsTheLatestRunsFailuresAfterTheFlaggedAsItsReportsHeaderDoes()
    {
        AssemblyOverviewDto row = Row("Alpha.Tests", 120, 3, 20);
        row = row with { Summary = row.Summary! with { FailedLatestRun = 2 } };

        string[] lines = Render(row);

        Assert.Equal("  120 tests | 3 flagged | 2 failed in latest run | 20 runs | last 2026-08-03 14:05", lines[3]);
    }

    [Theory]
    [InlineData(0, 2, 1, "3 findings (2 medium, 1 low)")]
    [InlineData(0, 0, 1, "1 finding (1 low)")]
    public void TheVerdictIsTheFindingsPhraseAtItsMostSevereBand(int high, int medium, int low, string verdict)
    {
        string[] lines = Render(Row("Alpha.Tests", 10, 3, 5, high, medium, low));

        Assert.EndsWith(verdict, lines[2], StringComparison.Ordinal);
    }

    /// <summary>
    /// The verdict is painted in its most severe band's colour, as the report's own markers are.
    /// </summary>
    [Fact]
    public void TheVerdictIsColouredByItsMostSevereBand()
    {
        var colour = ReportText.Drawn(ReportGlyphs.Ascii, color: true);

        foreach ((int medium, int low, string band) in new[] { (1, 0, "medium"), (0, 1, "low") })
        {
            using var writer = new StringWriter();
            new OverviewTextRenderer(colour).Render(
                new OverviewEnvelope(ReportEnvelope.CurrentSchemaVersion, ReportScopes.Overview, [Row("Alpha.Tests", 1, 1, 1, 0, medium, low)]),
                writer);

            string verdict = ReportVocabulary.FindingsPhrase(Row("Alpha.Tests", 1, 1, 1, 0, medium, low).Summary!);
            Assert.Contains(colour.Colorize(band, verdict), writer.ToString(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Regression: the row said "1 tests", where the report it copies says "1 test".
    /// </summary>
    [Fact]
    public void OneTestAndOneRunAreSingular()
    {
        string[] lines = Render(Row("Alpha.Tests", 1, 0, 1));

        Assert.EndsWith("no findings", lines[2], StringComparison.Ordinal);
        Assert.Equal("  1 test | 0 flagged | 1 run | last 2026-08-03 14:05", lines[3]);
    }

    [Fact]
    public void AnAssemblyWithNoWindowSaysWhy()
    {
        string[] lines = Render(new AssemblyOverviewDto(
            "Alpha.Tests", null, null, "No sessions recorded on or after 2099-01-01.", "xping report --assembly Alpha.Tests", null));

        Assert.EndsWith("no window", lines[2], StringComparison.Ordinal);
        Assert.Equal("  No sessions recorded on or after 2099-01-01.", lines[3]);
    }

    [Fact]
    public void ANameTooLongForTheFenceKeepsOneSpaceBeforeItsVerdict()
    {
        string name = new('A', TextReportRenderer.FenceWidth);

        string[] lines = Render(Row(name, 1, 0, 1));

        Assert.Equal(name + " no findings", lines[2]);
    }

    [Fact]
    public void TheLastLineIsTheFirstRowsCommandOnItsOwn()
    {
        string[] lines = [.. Render(Row("Alpha.Tests", 1, 0, 1), Row("Beta.Tests", 1, 0, 1)).SkipLast(1)];

        Assert.Equal(string.Empty, lines[^2]);
        Assert.Equal("Details: xping report --assembly Alpha.Tests", lines[^1]);
    }

    /// <summary>
    /// D7: an assembly name is recorded text, and reaches the terminal only made printable.
    /// </summary>
    [Fact]
    public void AnAssemblyNameReachesTheTerminalPrintable()
    {
        string[] lines = Render(Row("Evil\u001b[2J.Tests", 1, 0, 1));

        Assert.DoesNotContain('\u001b', lines[2]);
    }
}
