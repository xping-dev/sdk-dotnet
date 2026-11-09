/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Rendering;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The overview, as a person reads it: two lines per assembly and the command that opens one.
/// </summary>
public sealed class OverviewTextRendererTests
{
    private static readonly DateTime Newest = new(2026, 8, 3, 14, 5, 0, DateTimeKind.Utc);

    private static AssemblyOverviewDto Row(string assembly, int tests, int flagged, int runs, int high = 0)
    {
        SummaryDto summary = ReportFixtures.Envelope().Summary with
        {
            Tests = tests,
            Flagged = flagged,
            Findings = high,
            Counts = new SeverityCountsDto(high, 0, 0)
        };

        var window = new WindowDto(Newest.AddDays(-2), Newest, runs, "default", null, 0, []);

        return new AssemblyOverviewDto(assembly, window, summary, null, $"xping report --assembly {assembly}");
    }

    private static string[] Render(params AssemblyOverviewDto[] rows)
    {
        using var writer = new StringWriter();
        new OverviewTextRenderer(ReportText.Capabilities(redirected: true))
            .Render(new OverviewEnvelope(ReportEnvelope.CurrentSchemaVersion, ReportScopes.Overview, rows), writer);

        return ReportText.Lines(writer.ToString());
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
            "Alpha.Tests", null, null, "No sessions recorded on or after 2099-01-01.", "xping report --assembly Alpha.Tests"));

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
