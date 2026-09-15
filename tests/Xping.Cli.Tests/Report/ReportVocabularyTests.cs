/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Rendering;

namespace Xping.Cli.Tests.Report;

public sealed class ReportVocabularyTests
{
    [Theory]
    [InlineData(nameof(FindingKind.TimingOut), "timing out")]
    [InlineData(nameof(FindingKind.AlwaysFailing), "always failing")]
    [InlineData(nameof(FindingKind.RetryMasked), "masked by retry")]
    public void LabelFor_KnownKind_ReturnsItsLabel(string kind, string expected)
    {
        Assert.Equal(expected, ReportVocabulary.LabelFor(kind));
    }

    /// <summary>
    /// Every declared kind needs a label. The fallback prints the raw enum name, which is legible
    /// but reads as an internal identifier leaking into the report — and it degrades silently, so
    /// nothing else would catch a kind added without a word for it.
    /// </summary>
    [Fact]
    public void LabelFor_EveryDeclaredKind_HasALabelOfItsOwn()
    {
        foreach (FindingKind kind in Enum.GetValues<FindingKind>())
        {
            string name = kind.ToString();
            string label = ReportVocabulary.LabelFor(name);

            Assert.NotEqual(name, label);
            Assert.NotEmpty(label);
        }
    }

    /// <summary>
    /// The fallback exists so a renderer reading an envelope written by a newer version prints
    /// something rather than a blank column.
    /// </summary>
    [Fact]
    public void LabelFor_UnknownKind_EchoesWhatTheEnvelopeSaid()
    {
        Assert.Equal("SomethingNewer", ReportVocabulary.LabelFor("SomethingNewer"));
    }

    /// <summary>
    /// Every population rule resolves to a marker of its own.
    /// </summary>
    /// <remarks>
    /// The fallback echoes the envelope's own spelling, which exists for a rule written by a newer
    /// build. A rule this build knows reaching it means no marker was chosen for it, and the column
    /// would print `excludesPartialRuns` inside a seventy-two column fence.
    /// </remarks>
    [Fact]
    public void EveryPopulationRuleHasAMarkerOfItsOwn()
    {
        string[] tokens =
        [
            .. Enum.GetValues<PopulationRule>()
                .Select(rule => ReportVocabulary.PopulationTokenFor(CamelCase(rule.ToString())))
        ];

        foreach (string token in tokens)
        {
            Assert.NotEmpty(token);
            Assert.DoesNotContain("excludes", token, StringComparison.OrdinalIgnoreCase);

            // The trailer holds evidence, population, id and a source path inside the fence, and
            // the path is what makes a finding actionable. A marker wider than the widest already
            // shipped takes those columns from it.
            Assert.True(token.Length <= "-env-cluster".Length, $"'{token}' is {token.Length} columns");
        }

        Assert.Equal(tokens.Length, tokens.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The bands carry no punctuation of their own, so each caller supplies its own.
    /// </summary>
    /// <remarks>
    /// One producer, two callers: the section heading wraps this in parentheses and the one-line
    /// summary puts it after a count. Two tables of the same three words is how a report and its
    /// summary end up disagreeing about the same run.
    /// </remarks>
    [Theory]
    [InlineData(5, 2, 0, "5 high, 2 medium")]
    [InlineData(5, 0, 0, "5 high")]
    [InlineData(0, 0, 1, "1 low")]
    [InlineData(1, 1, 1, "1 high, 1 medium, 1 low")]
    public void SeverityBandsNamesOnlyTheBandsThatHaveSomethingInThem(
        int high, int medium, int low, string expected)
    {
        Assert.Equal(expected, ReportVocabulary.SeverityBands(Summary(high, medium, low)));
    }

    /// <summary>
    /// A report with nothing in it has no bands to name.
    /// </summary>
    [Fact]
    public void SeverityBandsIsEmptyWhereNothingWasFound()
    {
        Assert.Equal(string.Empty, ReportVocabulary.SeverityBands(Summary(0, 0, 0)));
    }

    /// <summary>
    /// The one-line summary's phrasing is unchanged by the heading taking the same bands.
    /// </summary>
    [Theory]
    [InlineData(1, 0, 0, "1 finding (1 high)")]
    [InlineData(5, 2, 0, "7 findings (5 high, 2 medium)")]
    public void TheFindingsPhraseStillReadsAsItDid(
        int high, int medium, int low, string expected)
    {
        Assert.Equal(expected, ReportVocabulary.FindingsPhrase(Summary(high, medium, low)));
    }

    [Fact]
    public void AReportWithNoFindingsSaysSoRatherThanCountingBands()
    {
        Assert.Equal("no findings", ReportVocabulary.FindingsPhrase(Summary(0, 0, 0)));
    }

    private static SummaryDto Summary(int high, int medium, int low) =>
        new(
            412,
            high + medium + low,
            new SeverityCountsDto(high, medium, low),
            0,
            412,
            0,
            0,
            new Dictionary<string, NotMeasuredDto>(StringComparer.Ordinal),
            0, 0, 0, 0, 0,
            []);

    private static string CamelCase(string value) =>
        char.ToLowerInvariant(value[0]) + value[1..];
}
