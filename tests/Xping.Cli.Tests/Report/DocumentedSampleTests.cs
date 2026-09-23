/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.RegularExpressions;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Rendering;
using Xping.Cli.Reporting;

using static Xping.Cli.Tests.Report.ReportText;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// Every rendered report printed in the documentation is what the renderer emits.
/// </summary>
/// <remarks>
/// <para>
/// Ten samples across nine files showed a report in a layout the tool had stopped emitting, and
/// nothing failed. A sample is a promise about output, so it is held to output the same way a
/// golden is: generated from a fixture, never typed, and re-checked on every build.
/// </para>
/// <para>
/// A sample opts in by naming its fixture in an HTML comment directly above its fence:
/// <c>&lt;!-- xping:sample docs-mstest --&gt;</c>, with <c>rows</c>, <c>trailer</c> or
/// <c>summary</c> after the fixture where the page shows less than the whole report.
/// The binding is in the document rather than in a table here so that somebody editing the page can
/// see what governs the block they are looking at.
/// </para>
/// <para>
/// Samples are drawn with the Unicode glyph set, which is what a reader's terminal gives them and
/// what every one of these pages already showed.
/// </para>
/// </remarks>
public sealed class DocumentedSampleTests
{
    /// <summary>Set to rewrite the documented samples instead of asserting against them.</summary>
    /// <remarks>
    /// The same switch the goldens use, for the same reason: a layout change should cost one
    /// command and a diff to review, not nine files edited by hand into nine slightly different
    /// approximations of the truth.
    /// </remarks>
    private const string UpdateVariable = "XPING_UPDATE_GOLDENS";

    /// <summary>Matches a sample marker and the fence it introduces.</summary>
    /// <remarks>
    /// The fence length is captured and back-referenced, because the CLI reference wraps its sample
    /// in four backticks so that the report's own three-backtick fence survives inside it.
    /// </remarks>
    private static readonly Regex Marker = new(
        @"<!-- xping:sample (?<fixture>[a-z0-9-]+)(?: (?<extent>rows|summary|trailer|latest-run))? -->" +
        @"\r?\n(?<fence>`{3,})[a-z]*\r?\n(?<body>.*?)\r?\n\k<fence>",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    /// <summary>The documents that print a report.</summary>
    /// <remarks>
    /// Listed rather than discovered, so that deleting the marker from a page fails here instead of
    /// quietly reducing what is covered.
    /// </remarks>
    public static TheoryData<string> Documents() =>
    [
        "README.md",
        "nuspec/README.Cli.md",
        "docs/index.md",
        "docs/cli/command-reference.md",
        "docs/getting-started/quickstart-xunit.md",
        "docs/getting-started/quickstart-mstest.md",
        "docs/getting-started/quickstart-nunit.md",
        "docs/getting-started/local-first.md",
        "docs/known-limitations.md",
        "docs/guides/working-with-tests/common-flaky-patterns.md"
    ];

    [Theory]
    [MemberData(nameof(Documents))]
    public void EveryDocumentedSampleIsWhatTheRendererEmits(string document)
    {
        string path = Path.Combine(RepositoryRoot(), document);

        Assert.True(File.Exists(path), $"no such document: '{path}'");

        string markdown = File.ReadAllText(path);
        MatchCollection samples = Marker.Matches(markdown);

        Assert.True(
            samples.Count > 0,
            $"'{document}' is listed as printing a report but carries no xping:sample marker");

        bool updating = Environment.GetEnvironmentVariable(UpdateVariable) is { Length: > 0 };
        string updated = markdown;

        foreach (Match sample in samples)
        {
            string fixture = sample.Groups["fixture"].Value;
            string expected = Sample(fixture, sample.Groups["extent"].Value);

            if (updating)
            {
                updated = updated.Replace(
                    sample.Value,
                    sample.Value.Replace(sample.Groups["body"].Value, expected, StringComparison.Ordinal),
                    StringComparison.Ordinal);

                continue;
            }

            Assert.Equal(expected, Normalise(sample.Groups["body"].Value));
        }

        if (updating && updated != markdown)
            File.WriteAllText(path, updated);

        Assert.False(
            updating,
            $"{UpdateVariable} was set: '{document}' was rewritten and nothing was asserted. " +
            "Review the diff and run again without it.");
    }

    /// <summary>
    /// Renders the block a document shows for a fixture.
    /// </summary>
    /// <param name="fixture">The fixture key.</param>
    /// <param name="extent">
    /// <c>rows</c> where the page shows finding rows, <c>trailer</c> where it shows a single
    /// finding's last line, <c>summary</c> where it shows the one-line report, <c>latest-run</c>
    /// where it shows that section alone, and empty where it shows the whole thing.
    /// </param>
    /// <returns>The block, without a trailing newline.</returns>
    private static string Sample(string fixture, string extent)
    {
        ReportEnvelope envelope = ReportFixtures.Get(fixture);

        if (extent == "trailer")
            return Trailer(Render(envelope, Drawn(ReportGlyphs.Unicode, color: false)));

        if (extent == "summary")
        {
            using var writer = new StringWriter();
            new SummaryReportRenderer().Render(envelope, writer);

            return Normalise(writer.ToString()).TrimEnd('\n');
        }

        string report = Render(envelope, Drawn(ReportGlyphs.Unicode, color: false));

        if (extent == "latest-run")
            return Normalise(string.Join("\n", LatestRunSection(report)));

        return Normalise(
            extent == "rows" ? string.Join("\n", Rows(report)).TrimEnd('\n') : report.TrimEnd('\n'));
    }

    /// <summary>Walks up from the build output to the repository root.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Xping.Sdk.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);

        return directory.FullName;
    }

    /// <summary>Normalises line endings, so one sample serves both platforms.</summary>
    private static string Normalise(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);
}
