/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text;
using Xping.Sdk.Core.Models.Statistics;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Sdk.Core.Services.Reporting.Internals;

/// <summary>
/// Renders the end-of-run local summary block.
/// </summary>
/// <remarks>
/// Rendering is separated from writing so the layout can be unit tested without touching a console.
/// </remarks>
internal static class LocalRunReportWriter
{
    private const int Width = 74;

    /// <summary>
    /// Renders the report, or returns <see langword="null"/> when there is nothing worth printing.
    /// </summary>
    /// <param name="stats">Session statistics.</param>
    /// <param name="analysis">Local cross-run analysis.</param>
    /// <param name="glyphs">Glyph set matched to the terminal's capabilities.</param>
    /// <param name="showCta">Whether to append the cloud invitation.</param>
    /// <param name="storePath">Store location, shown with the invitation.</param>
    /// <returns>The rendered block, or <see langword="null"/>.</returns>
    public static string? Render(
        QuickStatistics? stats,
        LocalAnalysis analysis,
        ReportGlyphs glyphs,
        bool showCta,
        string? storePath)
    {
        if (stats == null || stats.Total == 0)
            return null;

        analysis ??= LocalAnalysis.Empty;

        var sb = new StringBuilder();
        string rule = new(glyphs.HorizontalRule, Width);

        sb.AppendLine();
        sb.AppendLine(rule);
        sb.AppendLine(Pad(
            "  Xping " + glyphs.Separator + " local run summary",
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} tests {1} {2:0.0}s  ",
                stats.Total,
                glyphs.Separator,
                stats.WallClockDurationMs / 1000.0)));
        sb.AppendLine(rule);
        sb.AppendLine(RenderOutcomes(stats, glyphs));

        AppendUnstableSection(sb, analysis, glyphs);
        AppendConsistentFailureSection(sb, analysis, glyphs);
        AppendHistoryProgress(sb, analysis, glyphs);

        if (showCta)
            AppendCta(sb, analysis, glyphs, storePath);

        sb.AppendLine(rule);

        return sb.ToString();
    }

    private static string RenderOutcomes(QuickStatistics stats, ReportGlyphs glyphs)
    {
        var sb = new StringBuilder("  ");
        sb.Append(glyphs.Pass).Append(' ').Append(stats.Passed.ToString(CultureInfo.InvariantCulture))
            .Append(" passed");

        if (stats.Failed > 0)
        {
            sb.Append("     ").Append(glyphs.Fail).Append(' ')
                .Append(stats.Failed.ToString(CultureInfo.InvariantCulture)).Append(" failed");
        }

        if (stats.Skipped > 0)
        {
            sb.Append("     ").Append(glyphs.Skip).Append(' ')
                .Append(stats.Skipped.ToString(CultureInfo.InvariantCulture)).Append(" skipped");
        }

        return sb.ToString();
    }

    private static void AppendUnstableSection(
        StringBuilder sb, LocalAnalysis analysis, ReportGlyphs glyphs)
    {
        if (analysis.UnstableTests.Count == 0)
            return;

        sb.AppendLine();
        sb.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "  {0}  {1} unstable {2} {3} last {4} local {5}",
            glyphs.Warning,
            analysis.UnstableTests.Count,
            analysis.UnstableTests.Count == 1 ? "test" : "tests",
            glyphs.Separator,
            analysis.RunsAnalysed,
            analysis.RunsAnalysed == 1 ? "run" : "runs"));
        sb.AppendLine();

        const int SparklineIndent = 5;
        const int SparklineGap = 3;

        // Every sparkline is padded to the same width so the name column lines up regardless of how
        // much history each test has. Without this, a test seen in 4 runs and one seen in 12 would
        // start their names in different columns.
        int sparklineWidth = analysis.UnstableTests
            .Max(t => Math.Min(t.History.Count, MaxSparklinePoints));

        int nameColumn = SparklineIndent + sparklineWidth + SparklineGap;

        foreach (UnstableTest test in analysis.UnstableTests)
        {
            string sparkline = RenderSparkline(test.History, glyphs).PadLeft(sparklineWidth);
            string ratio = string.Format(
                CultureInfo.InvariantCulture, "{0}/{1}", test.PassCount, test.RunCount);

            sb.AppendLine(Pad(
                new string(' ', SparklineIndent) + sparkline + new string(' ', SparklineGap) +
                    Truncate(test.Name, Math.Max(20, Width - nameColumn - 10)),
                ratio + "  "));

            string description = DescribeKind(test, glyphs);
            if (description.Length > 0)
                sb.AppendLine(new string(' ', nameColumn) + description);

            sb.AppendLine();
        }
    }

    private static void AppendConsistentFailureSection(
        StringBuilder sb, LocalAnalysis analysis, ReportGlyphs glyphs)
    {
        if (analysis.ConsistentFailures.Count == 0)
            return;

        sb.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "  {0}  {1} {2} failed in all {3} runs - not flaky, likely real bugs",
            glyphs.Fail,
            analysis.ConsistentFailures.Count,
            analysis.ConsistentFailures.Count == 1 ? "test" : "tests",
            analysis.RunsAnalysed));

        // Naming them is the useful part; past a handful the list stops being scannable.
        const int MaxNamed = 3;
        var names = analysis.ConsistentFailures.Take(MaxNamed).Select(t => t.Name).ToList();
        string line = string.Join(" " + glyphs.Separator + " ", names);

        if (analysis.ConsistentFailures.Count > MaxNamed)
        {
            line += string.Format(
                CultureInfo.InvariantCulture,
                " (+{0} more)",
                analysis.ConsistentFailures.Count - MaxNamed);
        }

        sb.AppendLine("     " + Truncate(line, Width - 8));
        sb.AppendLine();
    }

    private static void AppendHistoryProgress(
        StringBuilder sb, LocalAnalysis analysis, ReportGlyphs glyphs)
    {
        if (analysis.HasSufficientHistory)
            return;

        // Without this the first runs would render an empty report, which is the worst possible
        // first impression for a feature whose whole job is the first impression.
        sb.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "  {0}  Collecting local history {1} {2} of {3} runs",
            glyphs.Pending,
            glyphs.Separator,
            analysis.RunsAnalysed,
            analysis.MinimumRunsForHistory));
        sb.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "     Cross-run flakiness detection starts after {0} runs.",
            analysis.MinimumRunsForHistory));
        sb.AppendLine();
    }

    private static void AppendCta(
        StringBuilder sb, LocalAnalysis analysis, ReportGlyphs glyphs, string? storePath)
    {
        string location = string.IsNullOrEmpty(storePath)
            ? ".xping/"
            : Shorten(storePath!);

        sb.AppendLine(string.Format(
            CultureInfo.InvariantCulture,
            "  {0} local {1} stored in {2} {3} visible only on this machine.",
            analysis.RunsAnalysed,
            analysis.RunsAnalysed == 1 ? "run" : "runs",
            location,
            glyphs.Separator));
        sb.AppendLine(
            "  See flakiness across CI and your whole team " + glyphs.Arrow +
            " https://xping.io/start");
        sb.AppendLine();
    }

    private static string DescribeKind(UnstableTest test, ReportGlyphs glyphs)
    {
        switch (test.Kind)
        {
            case InstabilityKind.FlakedInRun:
                return test.PassedOnAttempt is > 1
                    ? string.Format(
                        CultureInfo.InvariantCulture,
                        "flaked inside this run - passed on attempt {0}",
                        test.PassedOnAttempt)
                    : "flaked inside this run - passed after a retry";

            case InstabilityKind.NewlyFailing:
                return "newly failing " + glyphs.Separator + " first failure in this window";

            case InstabilityKind.FlakyAcrossRuns:
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "passed {0} of {1} runs {2} inconsistent",
                    test.PassCount,
                    test.RunCount,
                    glyphs.Separator);

            default:
                return string.Empty;
        }
    }

    private const int MaxSparklinePoints = 12;

    private static string RenderSparkline(IReadOnlyList<bool> history, ReportGlyphs glyphs)
    {
        var sb = new StringBuilder(MaxSparklinePoints);
        int start = Math.Max(0, history.Count - MaxSparklinePoints);

        for (int i = start; i < history.Count; i++)
            sb.Append(history[i] ? glyphs.HistoryPass : glyphs.HistoryFail);

        return sb.ToString();
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
            return value;

        return max <= 1 ? value.Substring(0, max) : value.Substring(0, max - 1) + "~";
    }

    private static string Shorten(string path)
    {
        // The full absolute path is noise; the last two segments locate it unambiguously enough.
        string[] parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Length <= 2
            ? path
            : Path.Combine(parts[parts.Length - 2], parts[parts.Length - 1]);
    }

    private static string Pad(string left, string right)
    {
        int gap = Width - left.Length - right.Length;
        return gap > 0 ? left + new string(' ', gap) + right : left + "  " + right;
    }
}
