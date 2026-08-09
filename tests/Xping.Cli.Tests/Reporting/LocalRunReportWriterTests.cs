/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Statistics;
using Xping.Cli.Analysis;
using Xping.Cli.Reporting;

namespace Xping.Cli.Tests.Reporting;

public sealed class LocalRunReportWriterTests
{
    private static readonly string[] FailureNames = ["Alpha", "Beta", "Gamma"];

    private static QuickStatistics Stats(int total = 10, int passed = 9, int failed = 1) =>
        new()
        {
            Total = total,
            Passed = passed,
            Failed = failed,
            WallClockDurationMs = 38200
        };

    private static LocalAnalysis AnalysisWith(params UnstableTest[] tests) =>
        new()
        {
            UnstableTests = tests,
            RunsAnalysed = 12,
            MinimumRunsForHistory = 3
        };

    private static UnstableTest Flaky(string name, params bool[] history) => new()
    {
        Name = name,
        Kind = InstabilityKind.FlakyAcrossRuns,
        History = history,
        PassCount = history.Count(h => h),
        RunCount = history.Length
    };

    [Fact]
    public void ReturnsNullWhenNoTestsRan()
    {
        Assert.Null(LocalRunReportWriter.Render(
            new QuickStatistics(), LocalAnalysis.Empty, ReportGlyphs.Ascii, false, null));
    }

    [Fact]
    public void ReturnsNullWhenStatsAreMissing()
    {
        Assert.Null(LocalRunReportWriter.Render(
            null, LocalAnalysis.Empty, ReportGlyphs.Ascii, false, null));
    }

    [Fact]
    public void RendersOutcomeCounts()
    {
        string? report = LocalRunReportWriter.Render(
            new QuickStatistics { Total = 12, Passed = 9, Failed = 2, Skipped = 1 },
            LocalAnalysis.Empty,
            ReportGlyphs.Ascii,
            showCta: false,
            storePath: null);

        Assert.NotNull(report);
        Assert.Contains("9 passed", report, StringComparison.Ordinal);
        Assert.Contains("2 failed", report, StringComparison.Ordinal);
        Assert.Contains("1 skipped", report, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, "1 test ")]
    [InlineData(2, "2 tests ")]
    public void UsesTheCorrectPluralForTheTestCount(int total, string expected)
    {
        string? report = LocalRunReportWriter.Render(
            new QuickStatistics { Total = total, Passed = total },
            LocalAnalysis.Empty,
            ReportGlyphs.Ascii,
            showCta: false,
            storePath: null);

        Assert.NotNull(report);
        Assert.Contains(expected, report, StringComparison.Ordinal);
    }

    [Fact]
    public void OmitsZeroOutcomeCounts()
    {
        string? report = LocalRunReportWriter.Render(
            new QuickStatistics { Total = 9, Passed = 9 },
            LocalAnalysis.Empty,
            ReportGlyphs.Ascii,
            showCta: false,
            storePath: null);

        Assert.NotNull(report);
        Assert.DoesNotContain("failed", report, StringComparison.Ordinal);
        Assert.DoesNotContain("skipped", report, StringComparison.Ordinal);
    }

    [Fact]
    public void RendersSparklineOldestToNewest()
    {
        var analysis = AnalysisWith(Flaky("MyTest", true, false, true, true));

        string? report = LocalRunReportWriter.Render(
            Stats(), analysis, ReportGlyphs.Ascii, showCta: false, storePath: null);

        Assert.NotNull(report);
        Assert.Contains("#.##", report, StringComparison.Ordinal);
        Assert.Contains("MyTest", report, StringComparison.Ordinal);
        Assert.Contains("3/4", report, StringComparison.Ordinal);
    }

    [Fact]
    public void AlignsNameColumnAcrossDifferingHistoryLengths()
    {
        // Two tests with different amounts of history must still start their names in the same
        // column, otherwise the block reads as ragged noise.
        var analysis = AnalysisWith(
            Flaky("ShortHistory", true, false),
            Flaky("LongHistory", true, false, true, false, true, true));

        string? report = LocalRunReportWriter.Render(
            Stats(), analysis, ReportGlyphs.Ascii, showCta: false, storePath: null);

        Assert.NotNull(report);

        var lines = report!.Split('\n');
        int shortColumn = lines.Single(l => l.Contains("ShortHistory", StringComparison.Ordinal))
            .IndexOf("ShortHistory", StringComparison.Ordinal);
        int longColumn = lines.Single(l => l.Contains("LongHistory", StringComparison.Ordinal))
            .IndexOf("LongHistory", StringComparison.Ordinal);

        Assert.Equal(shortColumn, longColumn);
    }

    [Fact]
    public void CapsSparklineAtTwelvePoints()
    {
        var history = Enumerable.Range(0, 40).Select(i => i % 2 == 0).ToArray();
        var analysis = AnalysisWith(Flaky("MyTest", history));

        string? report = LocalRunReportWriter.Render(
            Stats(), analysis, ReportGlyphs.Ascii, showCta: false, storePath: null);

        Assert.NotNull(report);

        string line = report!.Split('\n').Single(l => l.Contains("MyTest", StringComparison.Ordinal));
        int marks = line.Count(c => c is '#' or '.');
        Assert.Equal(12, marks);
    }

    [Fact]
    public void DescribesIntraRunFlake()
    {
        var analysis = AnalysisWith(new UnstableTest
        {
            Name = "RetryTest",
            Kind = InstabilityKind.FlakedInRun,
            History = [true],
            PassCount = 1,
            RunCount = 1,
            PassedOnAttempt = 3
        });

        string? report = LocalRunReportWriter.Render(
            Stats(), analysis, ReportGlyphs.Ascii, showCta: false, storePath: null);

        Assert.NotNull(report);
        Assert.Contains("flaked inside this run", report, StringComparison.Ordinal);
        Assert.Contains("attempt 3", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowsHistoryProgressBeforeEnoughRuns()
    {
        // The first runs must never render an empty block.
        var analysis = new LocalAnalysis { RunsAnalysed = 1, MinimumRunsForHistory = 3 };

        string? report = LocalRunReportWriter.Render(
            Stats(), analysis, ReportGlyphs.Ascii, showCta: false, storePath: null);

        Assert.NotNull(report);
        Assert.Contains("Collecting local history", report, StringComparison.Ordinal);
        Assert.Contains("1 of 3 runs", report, StringComparison.Ordinal);
    }

    [Fact]
    public void HidesHistoryProgressOnceSufficient()
    {
        var analysis = AnalysisWith(Flaky("MyTest", true, false, true));

        string? report = LocalRunReportWriter.Render(
            Stats(), analysis, ReportGlyphs.Ascii, showCta: false, storePath: null);

        Assert.NotNull(report);
        Assert.DoesNotContain("Collecting local history", report, StringComparison.Ordinal);
    }

    [Fact]
    public void RendersConsistentFailuresSeparatelyFromFlakiness()
    {
        var analysis = new LocalAnalysis
        {
            ConsistentFailures =
            [
                new UnstableTest { Name = "AlwaysBroken", Kind = InstabilityKind.ConsistentlyFailing }
            ],
            RunsAnalysed = 12,
            MinimumRunsForHistory = 3
        };

        string? report = LocalRunReportWriter.Render(
            Stats(), analysis, ReportGlyphs.Ascii, showCta: false, storePath: null);

        Assert.NotNull(report);
        Assert.Contains("not flaky, likely real bugs", report, StringComparison.Ordinal);
        Assert.Contains("AlwaysBroken", report, StringComparison.Ordinal);
    }

    [Fact]
    public void IncludesCtaOnlyWhenRequested()
    {
        var analysis = AnalysisWith(Flaky("MyTest", true, false, true));

        string? without = LocalRunReportWriter.Render(
            Stats(), analysis, ReportGlyphs.Ascii, showCta: false, storePath: "/repo/.xping");
        string? with = LocalRunReportWriter.Render(
            Stats(), analysis, ReportGlyphs.Ascii, showCta: true, storePath: "/repo/.xping");

        Assert.DoesNotContain("xping.io/start", without!, StringComparison.Ordinal);
        Assert.Contains("xping.io/start", with!, StringComparison.Ordinal);
        Assert.Contains("visible only on this machine", with, StringComparison.Ordinal);
    }

    [Fact]
    public void AsciiGlyphsAvoidNonAsciiCharacters()
    {
        // The ASCII set exists for terminals that would render Unicode as mojibake, so it must be
        // strictly ASCII.
        var analysis = AnalysisWith(Flaky("MyTest", true, false, true));

        string? report = LocalRunReportWriter.Render(
            Stats(), analysis, ReportGlyphs.Ascii, showCta: true, storePath: "/repo/.xping");

        Assert.NotNull(report);
        Assert.All(report!, c => Assert.True(c < 128, $"Non-ASCII character '{c}' in ASCII report"));
    }

    [Fact]
    public void AggregateHeaderDoesNotClaimASharedRunCount()
    {
        // Arrange — 9 runs across 3 assemblies, but each finding is measured over its own window.
        // Saying "last 9 runs" above a finding that reads 2/3 contradicts itself.
        var analysis = new LocalAnalysis
        {
            UnstableTests = [new UnstableTest
            {
                Name = "T",
                Kind = InstabilityKind.FlakyAcrossRuns,
                History = [false, true, true],
                PassCount = 2,
                RunCount = 3,
                Assembly = "Alpha.Tests"
            }],
            RunsAnalysed = 9,
            AssembliesAnalysed = 3,
            MinimumRunsForHistory = 3
        };

        // Act
        string report = LocalRunReportWriter.RenderAggregate(
            analysis, ReportGlyphs.Ascii, showCta: false, storePath: null);

        // Assert
        Assert.Contains("across 3 assemblies", report, StringComparison.Ordinal);
        Assert.DoesNotContain("last 9 local runs", report, StringComparison.Ordinal);
    }

    [Fact]
    public void AggregateLabelsEachFindingWithItsAssembly()
    {
        var analysis = new LocalAnalysis
        {
            UnstableTests =
            [
                new UnstableTest
                {
                    Name = "SameName", Kind = InstabilityKind.FlakyAcrossRuns,
                    History = [true, false, true], PassCount = 2, RunCount = 3,
                    Assembly = "Alpha.Tests"
                }
            ],
            RunsAnalysed = 6,
            AssembliesAnalysed = 2,
            MinimumRunsForHistory = 3
        };

        string report = LocalRunReportWriter.RenderAggregate(
            analysis, ReportGlyphs.Ascii, showCta: false, storePath: null);

        Assert.Contains("Alpha.Tests", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ListsEveryNamedFailureRatherThanTruncatingSomeAway()
    {
        // Arrange — three long names cannot share one line at this width.
        var failures = FailureNames
            .Select(n => new UnstableTest
            {
                Name = n + new string('X', 40),
                Kind = InstabilityKind.ConsistentlyFailing,
                Assembly = n + ".Tests"
            })
            .ToArray();

        var analysis = new LocalAnalysis
        {
            ConsistentFailures = failures,
            RunsAnalysed = 9,
            AssembliesAnalysed = 3,
            MinimumRunsForHistory = 3
        };

        // Act
        string report = LocalRunReportWriter.RenderAggregate(
            analysis, ReportGlyphs.Ascii, showCta: false, storePath: null);

        // Assert — every name the reader is told about is actually visible.
        Assert.Contains("Alpha", report, StringComparison.Ordinal);
        Assert.Contains("Beta", report, StringComparison.Ordinal);
        Assert.Contains("Gamma", report, StringComparison.Ordinal);
    }

    [Fact]
    public void AggregateSaysSoWhenNothingIsUnstable()
    {
        var analysis = new LocalAnalysis
        {
            RunsAnalysed = 9,
            AssembliesAnalysed = 3,
            MinimumRunsForHistory = 3
        };

        string report = LocalRunReportWriter.RenderAggregate(
            analysis, ReportGlyphs.Ascii, showCta: false, storePath: null);

        Assert.Contains("Nothing unstable", report, StringComparison.Ordinal);
    }

    [Fact]
    public void NeverExceedsTheFixedWidth()
    {
        var analysis = AnalysisWith(
            Flaky(new string('X', 200), Enumerable.Range(0, 12).Select(i => i % 2 == 0).ToArray()));

        string? report = LocalRunReportWriter.Render(
            Stats(), analysis, ReportGlyphs.Unicode, showCta: true, storePath: "/repo/.xping");

        Assert.NotNull(report);
        Assert.All(
            report!.Split('\n'),
            line => Assert.True(line.TrimEnd('\r').Length <= 80, $"Line too wide: {line.Length}"));
    }
}
