/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Windowing;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The overview copies each assembly's report header and orders the rows by what most needs reading.
/// </summary>
public sealed class OverviewBuilderTests
{
    private static ReportEnvelope WithCounts(int high, int medium, int low)
    {
        ReportEnvelope envelope = ReportFixtures.Envelope();

        return envelope with
        {
            Summary = envelope.Summary with
            {
                Counts = new SeverityCountsDto(high, medium, low),
                Findings = high + medium + low
            }
        };
    }

    private static OverviewEnvelope Build(params OverviewRow[] rows) =>
        OverviewBuilder.Build(
            rows,
            assembly => new ReportScope(assembly, WindowResolution.Default, 0, ShellDialect.Posix));

    [Fact]
    public void RowsAreOrderedByTheirMostSevereBandThenByName()
    {
        OverviewEnvelope overview = Build(
            new OverviewRow("Clean.Tests", WithCounts(0, 0, 0), null),
            new OverviewRow("Missing.Tests", null, "No runs recorded."),
            new OverviewRow("Low.Tests", WithCounts(0, 0, 4), null),
            new OverviewRow("B.High.Tests", WithCounts(1, 0, 0), null),
            new OverviewRow("Medium.Tests", WithCounts(0, 9, 9), null),
            new OverviewRow("A.High.Tests", WithCounts(1, 0, 0), null));

        Assert.Equal(
            ["A.High.Tests", "B.High.Tests", "Medium.Tests", "Low.Tests", "Clean.Tests", "Missing.Tests"],
            overview.Assemblies.Select(row => row.Assembly));
    }

    /// <summary>
    /// Report-format Amendment 6B: one high finding outranks any tally of medium ones.
    /// </summary>
    [Fact]
    public void OneHighFindingOutranksManyMediumOnes()
    {
        OverviewEnvelope overview = Build(
            new OverviewRow("Many.Tests", WithCounts(0, 40, 0), null),
            new OverviewRow("One.Tests", WithCounts(1, 0, 0), null));

        Assert.Equal("One.Tests", overview.Assemblies[0].Assembly);
    }

    /// <summary>
    /// Report-format Amendment 6B: the row holds the report's own objects, not numbers of its own.
    /// </summary>
    [Fact]
    public void EveryRowCopiesItsReportsWindowAndSummaryAsTheyAre()
    {
        ReportEnvelope report = WithCounts(2, 1, 0);

        AssemblyOverviewDto row = Build(new OverviewRow("MyApp.Tests", report, null)).Assemblies.Single();

        Assert.Same(report.Window, row.Window);
        Assert.Same(report.Summary, row.Summary);
        Assert.Null(row.Unavailable);
    }

    [Fact]
    public void ARowWithNoWindowCarriesItsReasonAndNoNumbers()
    {
        AssemblyOverviewDto row = Build(new OverviewRow("MyApp.Tests", null, "No runs recorded.")).Assemblies.Single();

        Assert.Null(row.Window);
        Assert.Null(row.Summary);
        Assert.Equal("No runs recorded.", row.Unavailable);
    }

    [Fact]
    public void EveryRowsCommandOpensThatAssemblysReportOverTheCallersWindow()
    {
        OverviewEnvelope overview = OverviewBuilder.Build(
            [new OverviewRow("My App.Tests", WithCounts(0, 0, 0), null)],
            assembly => new ReportScope(assembly, WindowResolution.Default, 0, ShellDialect.Posix, Runs: 5));

        Assert.Equal("xping report --assembly 'My App.Tests' --runs 5", overview.Assemblies[0].Command);
    }

    [Fact]
    public void TheEnvelopeSaysItIsAnOverviewAtTheCurrentSchema()
    {
        OverviewEnvelope overview = Build();

        Assert.Equal(ReportScopes.Overview, overview.Scope);
        Assert.Equal(ReportEnvelope.CurrentSchemaVersion, overview.SchemaVersion);
        Assert.Empty(overview.Assemblies);
    }
}
