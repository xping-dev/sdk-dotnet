/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Rendering;
using Xping.Cli.Reporting;

using static Xping.Cli.Tests.Report.ReportText;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The whole rendered report, pinned to a file, at every width and every charset.
/// </summary>
/// <remarks>
/// <para>
/// The tests beside this one each assert one rule and say in their name what it is. This asserts
/// the composition — that the rules still produce the block a reader pastes — which no per-rule
/// assertion can, because every one of them reads a line the layout is free to move.
/// </para>
/// <para>
/// <b>The goldens are generated, never authored.</b> Running with <c>XPING_UPDATE_GOLDENS</c> set
/// rewrites them from the renderer and then fails, so a rewrite can never be what makes a build
/// green. Hand-editing a golden to match a spec inverts the relationship and is how a width
/// violation gets pinned as correct.
/// </para>
/// </remarks>
public sealed class GoldenReportTests
{
    /// <summary>Set to rewrite the goldens instead of asserting against them.</summary>
    private const string UpdateVariable = "XPING_UPDATE_GOLDENS";

    /// <summary>The glyph sets a golden is recorded for.</summary>
    /// <remarks>
    /// Two, not four. Colour adds escape sequences and changes nothing else — which is itself
    /// asserted below — so a coloured golden would be the same file with unreadable bytes in it.
    /// </remarks>
    private static readonly (string Mode, ReportGlyphs Glyphs)[] Modes =
    [
        ("ascii", ReportGlyphs.Ascii),
        ("unicode", ReportGlyphs.Unicode)
    ];

    // ---------------------------------------------------------------------
    // The goldens
    // ---------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(ReportFixtures.Names), MemberType = typeof(ReportFixtures))]
    public void TheRenderedReportMatchesItsGolden(string fixture)
    {
        foreach ((string mode, ReportGlyphs glyphs) in Modes)
        {
            string rendered = Normalise(Render(ReportFixtures.Get(fixture), Drawn(glyphs, color: false)));
            string path = GoldenPath(fixture, mode);

            if (Environment.GetEnvironmentVariable(UpdateVariable) is { Length: > 0 })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, rendered);
                continue;
            }

            Assert.True(
                File.Exists(path),
                $"no golden for '{fixture}.{mode}'. Run with {UpdateVariable}=1 to write one.");

            Assert.Equal(Normalise(File.ReadAllText(path)), rendered);
        }

        Assert.False(
            Environment.GetEnvironmentVariable(UpdateVariable) is { Length: > 0 },
            $"{UpdateVariable} was set: the goldens were rewritten and nothing was asserted. " +
            "Review the diff and run again without it.");
    }

    /// <summary>
    /// Every fixture has a golden for every mode, and no golden is left behind by a fixture that
    /// was renamed or deleted.
    /// </summary>
    /// <remarks>
    /// An orphan golden is worse than a missing one: the missing golden fails the test above, while
    /// the orphan sits in the tree looking like coverage of something nothing renders any more.
    /// </remarks>
    [Fact]
    public void EveryGoldenOnDiskBelongsToAFixture()
    {
        var expected = new HashSet<string>(
            from string fixture in ReportFixtures.Keys
            from (string Mode, ReportGlyphs Glyphs) mode in Modes
            select $"{fixture}.{mode.Mode}.txt",
            StringComparer.Ordinal);

        string directory = Path.GetDirectoryName(GoldenPath("empty", "ascii"))!;

        Assert.True(Directory.Exists(directory), $"no goldens directory at '{directory}'");

        var found = new HashSet<string>(
            Directory.EnumerateFiles(directory, "*.txt").Select(Path.GetFileName)!,
            StringComparer.Ordinal);

        Assert.Equal(expected.OrderBy(n => n, StringComparer.Ordinal), found.OrderBy(n => n, StringComparer.Ordinal));
    }

    // ---------------------------------------------------------------------
    // Width — D10.3
    // ---------------------------------------------------------------------

    /// <summary>
    /// Every line inside the fence fits the fence, across the whole catalogue.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asserted rather than spot-checked, because a line over the width wraps in a chat client and
    /// loses the alignment the fence exists to preserve — and the fixture that reproduces it is
    /// never the one somebody was looking at.
    /// </para>
    /// <para>
    /// One exception, which the layout states rather than tolerates: a single unbreakable token
    /// wider than the budget is emitted whole. Half an identifier is unsearchable, and overflowing
    /// by a few columns is the smaller harm. So the exemption is not "this line is long" but "this
    /// line is one token" — a long line with a space in it is a wrapping defect either way.
    /// </para>
    /// <para>
    /// This counts UTF-16 units, not display columns. Every character the report can emit today is
    /// single-width and the two agree; a future glyph that is not would need this reading again.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(ReportFixtures.Names), MemberType = typeof(ReportFixtures))]
    public void NoLineInsideTheFenceExceedsTheWidth(string fixture)
    {
        foreach ((string mode, ReportGlyphs glyphs) in Modes)
        {
            foreach (string line in Fenced(Render(ReportFixtures.Get(fixture), Drawn(glyphs, color: false))))
            {
                if (line.Length <= FenceWidth)
                    continue;

                Assert.True(
                    !line.TrimStart().Contains(' ', StringComparison.Ordinal),
                    $"'{line}' is {line.Length} columns in {fixture}.{mode}, over the {FenceWidth} " +
                    "the fence allows, and is not a single unbreakable token");
            }
        }
    }

    // ---------------------------------------------------------------------
    // Charset — D10.4
    // ---------------------------------------------------------------------

    /// <summary>
    /// The ASCII report is ASCII, everywhere, including below the fence.
    /// </summary>
    /// <remarks>
    /// This is what a pipe receives. The legend and the truncation line sit outside the fence and
    /// are pasted with it, so they are held to the same rule as the block.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ReportFixtures.Names), MemberType = typeof(ReportFixtures))]
    public void TheAsciiReportCarriesNothingButPrintableAscii(string fixture)
    {
        string report = Render(ReportFixtures.Get(fixture), Drawn(ReportGlyphs.Ascii, color: false));

        Assert.DoesNotContain('\u001b', report);
        Assert.All(report, c => Assert.True(
            c < 0x80, $"non-ASCII '{c}' (U+{(int)c:X4}) in the ASCII rendering of {fixture}"));
    }

    /// <summary>
    /// The Unicode report draws only with characters a glyph pair already defines.
    /// </summary>
    /// <remarks>
    /// A bare non-ASCII literal in the renderer would have no ASCII counterpart, so it would reach a
    /// legacy code page as mojibake and would break the piped-output charset rule the moment
    /// somebody rendered it. Derived from <see cref="ReportGlyphs.Unicode"/> rather than listed, so
    /// adding a pair widens the permitted set and adding a literal does not.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ReportFixtures.Names), MemberType = typeof(ReportFixtures))]
    public void TheUnicodeReportDrawsOnlyWithGlyphsThatHaveAnAsciiCounterpart(string fixture)
    {
        HashSet<char> permitted = [.. GlyphCharacters(ReportGlyphs.Unicode)];
        string report = Render(ReportFixtures.Get(fixture), Drawn(ReportGlyphs.Unicode, color: false));

        Assert.All(report, c => Assert.True(
            c < 0x80 || permitted.Contains(c),
            $"'{c}' (U+{(int)c:X4}) appears in {fixture} but no ReportGlyphs pair defines it"));
    }

    /// <summary>Returns every character a glyph set can draw with.</summary>
    private static IEnumerable<char> GlyphCharacters(ReportGlyphs glyphs) =>
        new[] { glyphs.Pass, glyphs.Fail, glyphs.Skip, glyphs.Warning, glyphs.Pending, glyphs.Separator, glyphs.Arrow }
            .SelectMany(g => g)
            .Concat([glyphs.HorizontalRule, glyphs.HistoryPass, glyphs.HistoryFail]);

    // ---------------------------------------------------------------------
    // Colour
    // ---------------------------------------------------------------------

    /// <summary>
    /// Colour adds escape sequences and moves nothing.
    /// </summary>
    /// <remarks>
    /// The two right-aligned annotations — the heading's ordering note and a sibling's <c>same test
    /// as #N</c> — are padded from the plain marker's length rather than the written string's, and
    /// this is what says so. Asserted across the catalogue because the alignment is only visible on
    /// the fixtures that carry an annotation at all.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ReportFixtures.Names), MemberType = typeof(ReportFixtures))]
    public void ColourChangesNothingButTheEscapeSequences(string fixture)
    {
        ReportEnvelope envelope = ReportFixtures.Get(fixture);

        foreach ((_, ReportGlyphs glyphs) in Modes)
        {
            string plain = Render(envelope, Drawn(glyphs, color: false));
            string coloured = Render(envelope, Drawn(glyphs, color: true));

            Assert.Equal(plain, Strip(coloured));
        }
    }

    // ---------------------------------------------------------------------
    // Agreement between the two text renderers
    // ---------------------------------------------------------------------

    /// <summary>
    /// The one-line report and the fenced one state the same severity breakdown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The breakdown used to live on the header's second line, and moved to the section heading
    /// when that line became the three suite-state counts. <c>SummaryReportRenderer</c> kept its
    /// call to <c>FindingsPhrase</c> through that move, which is correct — it is one line and has
    /// no heading to move anything into — but it left two renderers spelling one number, and two
    /// places a number is spelled are two places it can drift.
    /// </para>
    /// <para>
    /// They cannot drift while both read <c>SeverityBands</c>, and this is what says so. Asserted
    /// over the catalogue rather than one report, because an empty band is omitted rather than
    /// printed as zero and the two would have to agree about that independently.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(ReportFixtures.Names), MemberType = typeof(ReportFixtures))]
    public void TheOneLineReportStatesTheSameBreakdownAsTheHeading(string fixture)
    {
        ReportEnvelope envelope = ReportFixtures.Get(fixture);

        // A detail view has no findings heading, and --summary is refused beside --id: there is no
        // one-line report of a selection for the two to disagree about.
        if (envelope.Selection != null)
            return;

        using var writer = new StringWriter();
        new SummaryReportRenderer().Render(envelope, writer);

        string bands = ReportVocabulary.SeverityBands(envelope.Summary);
        string summary = writer.ToString().TrimEnd();

        if (bands.Length == 0)
        {
            // Nothing was found, so there is no heading at all and nothing to agree with.
            Assert.DoesNotContain("NEEDS ATTENTION", Render(envelope), StringComparison.Ordinal);
            Assert.DoesNotContain("(", summary, StringComparison.Ordinal);

            return;
        }

        // The findings heading, which the latest-run section may sit above.
        string heading = Fenced(Render(envelope))
            .First(l => l.StartsWith("NEEDS ATTENTION", StringComparison.Ordinal));

        Assert.StartsWith($"NEEDS ATTENTION ({bands})", heading, StringComparison.Ordinal);
        Assert.Contains($"({bands})", summary, StringComparison.Ordinal);

        // And the count beside the bands is the findings produced, in both, rather than the rows
        // one of them happened to show.
        Assert.StartsWith(
            $"Xping: {envelope.Summary.Findings.ToString(CultureInfo.InvariantCulture)} ",
            summary,
            StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // Paths
    // ---------------------------------------------------------------------

    /// <summary>
    /// Returns where a fixture's golden lives, in the source tree rather than the build output.
    /// </summary>
    /// <remarks>
    /// Resolved back to the source tree so that a rewrite lands where it can be reviewed and
    /// committed. Reading from the copy in the output directory would work for the comparison and
    /// silently discard every rewrite.
    /// </remarks>
    private static string GoldenPath(string fixture, string mode) =>
        Path.Combine(ProjectDirectory(), "Report", "Goldens", $"{fixture}.{mode}.txt");

    /// <summary>Walks up from the build output to the directory holding the test project.</summary>
    private static string ProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Xping.Cli.Tests.csproj")))
            directory = directory.Parent;

        Assert.NotNull(directory);

        return directory.FullName;
    }

    /// <summary>
    /// Normalises line endings, so one golden serves both platforms.
    /// </summary>
    /// <remarks>
    /// The renderer writes <see cref="Environment.NewLine"/>, and a golden committed on one platform
    /// would otherwise fail on the other for a reason that has nothing to do with the report.
    /// </remarks>
    private static string Normalise(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);
}
