/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Rendering;
using Xping.Cli.Reporting;
using static Xping.Cli.Tests.Report.ReportFixtures;
using static Xping.Cli.Tests.Report.ReportText;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The text view of one finding, as <c>xping report --id</c> renders it.
/// </summary>
public sealed class DetailRenderingTests
{
    private const int FenceWidth = 72;

    [Fact]
    public void TheHeadingNamesTheRowAndHowManyTheFullReportHolds()
    {
        ReportEnvelope full = Get("spec-section-3");
        string[] fenced = Fenced(Render(Detail(full, 5)));

        Assert.StartsWith("FINDING ", fenced[0], StringComparison.Ordinal);
        Assert.EndsWith($" row 5 of {full.Summary.Findings}", fenced[0], StringComparison.Ordinal);
        Assert.Equal(FenceWidth, fenced[0].Length);
    }

    /// <summary>
    /// The row is the one the full report prints, number and annotation included.
    /// </summary>
    [Fact]
    public void TheRowIsTheFullReportsRow()
    {
        ReportEnvelope full = Get("spec-section-3");

        string[] row = Row(Fenced(Render(full)), "5.");
        string[] detail = Row(Fenced(Render(Detail(full, 5))), "5.");

        Assert.Equal(row, detail);
    }

    [Fact]
    public void ASingleTestCarriesItsSubjectAndMetricsBlocks()
    {
        ReportEnvelope full = Get("spec-section-3");
        FindingDto finding = full.Findings[4];
        string[] fenced = Fenced(Render(Detail(full, 5)));

        // The label column is the block's longest label and two spaces.
        Assert.Contains("    assembly  MyApp.Tests", fenced);
        Assert.Contains(fenced, l => l.StartsWith("    test      ", StringComparison.Ordinal));

        int column = finding.Metrics.Max(m => m.Label.Length) + 2;
        foreach (MetricDto metric in finding.Metrics)
            Assert.Contains($"    {metric.Label.PadRight(column)}{metric.Value}", fenced);
    }

    [Fact]
    public void AQualifiedNameWrapsAfterADotAndNeverInsideASegment()
    {
        string[] fenced = Fenced(Render(Detail(Get("spec-section-3"), 5)));

        int test = Array.FindIndex(fenced, l => l.StartsWith("    test ", StringComparison.Ordinal));

        Assert.Equal("    test      SampleApp.XUnit.SampleTests.", fenced[test]);
        Assert.Equal(
            "              FlakyTest_EnvironmentState_FailsBasedOnSystemState", fenced[test + 1]);
    }

    [Fact]
    public void TheSameSubjectLineNamesTheSiblingByRowKindAndId()
    {
        ReportEnvelope full = Get("spec-section-3");
        FindingDto sibling = full.Findings[3];

        string[] fenced = Fenced(Render(Detail(full, 5)));

        Assert.Equal(
            $"    Also about this test: #4 {ReportVocabulary.LabelFor(sibling.Kind)} ({sibling.Id})",
            fenced[^1]);
    }

    [Fact]
    public void AClusterHasNoSubjectBlockAndNoSameSubjectLine()
    {
        string[] fenced = Fenced(Render(Detail(Get("spec-section-3"), 3)));

        Assert.DoesNotContain(fenced, l => l.StartsWith("    test ", StringComparison.Ordinal));
        Assert.DoesNotContain(fenced, l => l.Contains("Also about", StringComparison.Ordinal));
    }

    [Fact]
    public void AFortyMemberClusterPrintsFortyMembersWithTheirLocations()
    {
        FindingDto cluster = Cluster("UnprovisionedDatabase..ctor", 40, "blocked 40 tests");
        cluster = cluster with
        {
            Subject = cluster.Subject with
            {
                Members =
                [
                    .. cluster.Subject.Members!.Select((member, i) => member with
                    {
                        SourceFile = "tests/MyApp.Tests/FixtureTests.cs",
                        SourceLineNumber = 10 + i
                    })
                ]
            }
        };

        string[] fenced = Fenced(Render(Detail(Envelope(cluster), 1)));

        for (int i = 0; i < 40; i++)
        {
            int member = Array.IndexOf(fenced, $"      FixtureTests.Member{i.ToString(CultureInfo.InvariantCulture)}");

            Assert.True(member >= 0, $"member {i} missing");
            Assert.Equal(
                $"        tests/MyApp.Tests/FixtureTests.cs:{(10 + i).ToString(CultureInfo.InvariantCulture)}",
                fenced[member + 1]);
        }

        Assert.DoesNotContain(fenced, l => l.Contains(" more", StringComparison.Ordinal));
    }

    /// <summary>
    /// Nothing inside the fence runs past it, save one token with nowhere to break.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReportFixtures.Names), MemberType = typeof(ReportFixtures))]
    public void NoDetailLineExceedsTheFenceExceptOneUnbreakableToken(string fixture)
    {
        ReportEnvelope full = Get(fixture);

        for (int row = 1; row <= full.Findings.Count; row++)
        {
            foreach (string line in Fenced(Render(Detail(full, row))))
            {
                if (line.Length <= FenceWidth)
                    continue;

                Assert.True(
                    IsOneUnbreakableToken(line),
                    $"{fixture} row {row}: '{line}' is {line.Length} columns and could have been broken");
            }
        }
    }

    /// <summary>
    /// Colour adds escape sequences and nothing else: every column lands where it does in a pipe.
    /// </summary>
    [Fact]
    public void ColourChangesOnlyEscapeSequences()
    {
        ReportEnvelope detail = Detail(Get("spec-section-3"), 5);

        string plain = Render(detail, Drawn(ReportGlyphs.Unicode, color: false));
        string coloured = Render(detail, Drawn(ReportGlyphs.Unicode, color: true));

        Assert.NotEqual(plain, coloured);
        Assert.Equal(plain, Strip(coloured));
    }

    [Fact]
    public void TheLatestRunSectionIsNotPrinted()
    {
        ReportEnvelope full = Get("latest-run");

        Assert.Contains(Fenced(Render(full)), l => l.StartsWith("LATEST RUN", StringComparison.Ordinal));
        Assert.DoesNotContain(
            Fenced(Render(Detail(full, 1))), l => l.StartsWith("LATEST RUN", StringComparison.Ordinal));
    }

    /// <summary>
    /// Below the fence: the legend, then the way back, and no detail line naming the command just run.
    /// </summary>
    [Fact]
    public void TheWayBackIsAlwaysOfferedAndTheDetailLineNever()
    {
        // One finding in the whole report: shown equals total, and the line is still printed.
        string report = Render(Detail(Get("cluster"), 1));

        Assert.EndsWith(
            ReportVocabulary.PopulationLegend[^1] + Environment.NewLine + Environment.NewLine +
            "Showing 1 of 1 | all: xping report --all" + Environment.NewLine,
            report,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Detail of row", report, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFullReportEndsOnTheFirstRowsCommand()
    {
        ReportEnvelope full = Get("spec-section-3");

        Assert.EndsWith(
            "Detail of row 1: xping report --id f_c7b87f12 --assembly SampleApp.XUnit" + Environment.NewLine,
            Render(full),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A path wraps at its directories, never at the dot of its extension.
    /// </summary>
    [Theory]
    [InlineData("tests/MyApp.Tests/Integration/Checkout/CheckoutServiceTests.cs:1420", "tests/MyApp.Tests/Integration/Checkout/", "CheckoutServiceTests.cs:1420")]
    [InlineData(@"C:\src\repo\tests\MyApp.Tests\Checkout\CheckoutServiceTests.cs:42", @"C:\src\repo\tests\MyApp.Tests\Checkout\", "CheckoutServiceTests.cs:42")]
    public void APathWrapsAtADirectory(string source, string first, string second)
    {
        ArgumentNullException.ThrowIfNull(source);

        string[] fenced = Fenced(Render(Detail(Envelope(Located(source)), 1)));

        int line = Array.FindIndex(fenced, l => l.StartsWith("    source ", StringComparison.Ordinal));

        Assert.Equal("    source    " + first, fenced[line]);
        Assert.Equal("              " + second, fenced[line + 1]);
    }

    /// <summary>
    /// A parameterised name wraps before its argument list, where a dot is a decimal point.
    /// </summary>
    [Fact]
    public void ANameWrapsBeforeItsArguments()
    {
        const string name = "MyApp.Tests.Calculations.SampleTests.AddsTwoNumbersTogether(1.5, 2.25, 3.125)";

        FindingDto finding = Finding("Flaky", "high", name, "failed 7 of 20 executions (35%)");
        string[] fenced = Fenced(Render(Detail(Envelope(finding), 1)));

        int line = Array.FindIndex(fenced, l => l.StartsWith("    test ", StringComparison.Ordinal));

        Assert.Equal("    test      MyApp.Tests.Calculations.SampleTests.", fenced[line]);
        Assert.Equal("              AddsTwoNumbersTogether(1.5, 2.25, 3.125)", fenced[line + 1]);
    }

    [Fact]
    public void ANotReportedSelectionSaysSoInsteadOfAnEmptyFence()
    {
        ReportEnvelope full = Get("spec-section-3");
        ReportEnvelope absent = full with
        {
            Selection = new SelectionDto("f_00000000", false, null, null, []),
            Findings = [],
            Truncated = full.Truncated with { Shown = 0 }
        };

        Assert.Equal(["f_00000000 is not reported in this window."], Fenced(Render(absent)));
    }

    /// <summary>
    /// A report narrowed by <c>--kind</c> does not offer its row 1, which is not the detail view's.
    /// </summary>
    [Fact]
    public void ANarrowedReportOffersNoDetailLine()
    {
        using var writer = new StringWriter();
        new TextReportRenderer(Capabilities(redirected: true), detailCommand: false).Render(Get("spec-section-3"), writer);

        Assert.DoesNotContain("Detail of row", writer.ToString(), StringComparison.Ordinal);
    }

    private static FindingDto Located(string source)
    {
        FindingDto finding = Finding("Flaky", "high", "MyApp.Tests.CheckoutTests.Completes", "failed 7 of 20 executions (35%)");

        int colon = source.LastIndexOf(':');

        return finding with
        {
            Subject = finding.Subject with
            {
                SourceFile = source[..colon],
                SourceLineNumber = int.Parse(source[(colon + 1)..], CultureInfo.InvariantCulture)
            }
        };
    }

    [Fact]
    public void AnEmptyReportHasNoDetailLine()
    {
        Assert.DoesNotContain("Detail of row", Render(Get("empty")), StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether an over-long line is one token with nowhere to break: a line with no space in it, or
    /// a labelled pair whose value is a single segment, a dot or slash ending it at most.
    /// </summary>
    private static bool IsOneUnbreakableToken(string line)
    {
        string content = line.TrimStart();

        if (!content.Contains(' ', StringComparison.Ordinal))
            return true;

        int gap = content.IndexOf("  ", StringComparison.Ordinal);
        if (gap < 0)
            return false;

        string value = content[gap..].TrimStart();

        return !value.Contains(' ', StringComparison.Ordinal)
            && value.TrimEnd('.', '/').IndexOfAny(['.', '/']) < 0;
    }

    /// <summary>The lines of one row, from its header to its trailer.</summary>
    private static string[] Row(string[] fenced, string number)
    {
        int start = Array.FindIndex(fenced, l => l.StartsWith(number + " ", StringComparison.Ordinal));
        int end = Array.FindIndex(fenced, start, l => l.Contains(" | f_", StringComparison.Ordinal));

        return fenced[start..(end + 1)];
    }
}
