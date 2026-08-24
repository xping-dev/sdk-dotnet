/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

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
}
