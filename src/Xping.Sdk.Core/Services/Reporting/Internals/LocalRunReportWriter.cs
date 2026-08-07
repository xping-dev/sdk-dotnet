/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text;
using Xping.Sdk.Core.Models.Statistics;

namespace Xping.Sdk.Core.Services.Reporting.Internals;

/// <summary>
/// Writes the end-of-run local summary block directly to the OS stdout handle.
/// </summary>
/// <remarks>
/// <para>
/// PHASE 0 SPIKE. This renders only the session totals available from <see cref="QuickStatistics"/>.
/// The flakiness section requires the local run store, which does not exist yet; the placeholder line
/// is here to validate layout and terminal handling, not to ship.
/// </para>
/// <para>
/// Output goes to <see cref="Console.OpenStandardOutput()"/> rather than <see cref="Console.Out"/> for
/// the same reason <c>RawConsoleSink</c> does: MSTest, NUnit3TestAdapter and vstest all redirect
/// <see cref="Console.Out"/> to capture per-test output, so an ordinary write would be swallowed or
/// attributed to whichever test happened to be running.
/// </para>
/// </remarks>
internal static class LocalRunReportWriter
{
    private const int Width = 74;

    /// <summary>
    /// Renders the local run summary for a finalized session.
    /// </summary>
    /// <param name="stats">Session statistics, or <see langword="null"/> when unavailable.</param>
    /// <param name="storedRunCount">Number of runs currently in the local store (0 during the spike).</param>
    public static void Write(QuickStatistics? stats, int storedRunCount)
    {
        if (stats == null || stats.Total == 0)
            return;

        try
        {
            var sb = new StringBuilder();
            string rule = new('─', Width);

            sb.AppendLine();
            sb.AppendLine(rule);

            string title = "  Xping · local run summary";
            string totals = string.Format(
                CultureInfo.InvariantCulture,
                "{0} tests · {1:0.0}s  ",
                stats.Total,
                stats.WallClockDurationMs / 1000.0);
            sb.AppendLine(Pad(title, totals));
            sb.AppendLine(rule);

            var outcomes = new StringBuilder("  ✓ ");
            outcomes.Append(stats.Passed.ToString(CultureInfo.InvariantCulture)).Append(" passed");
            if (stats.Failed > 0)
                outcomes.Append("     ✗ ").Append(stats.Failed.ToString(CultureInfo.InvariantCulture)).Append(" failed");
            if (stats.Skipped > 0)
                outcomes.Append("     ○ ").Append(stats.Skipped.ToString(CultureInfo.InvariantCulture)).Append(" skipped");
            sb.AppendLine(outcomes.ToString());
            sb.AppendLine();

            // Placeholder until the local run store lands in Phase 1.
            sb.AppendLine(storedRunCount > 0
                ? $"  ◷  Collecting local history · {storedRunCount} runs stored"
                : "  ◷  [spike] local store not implemented — no cross-run analysis yet");

            sb.AppendLine(rule);

            WriteRaw(sb.ToString());
        }
        catch (IOException)
        {
            // The report must never disturb a test run. A closed or redirected stdout handle is
            // not a reason to fail anything.
        }
    }

    private static string Pad(string left, string right)
    {
        int gap = Width - left.Length - right.Length;
        return gap > 0 ? left + new string(' ', gap) + right : left + "  " + right;
    }

    private static void WriteRaw(string text)
    {
        // Not cached in a static field: the spike writes once per process, at shutdown, and holding
        // an open handle on the raw stream past that point risks interfering with host teardown.
        using var stream = Console.OpenStandardOutput();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };
        writer.Write(text);
    }
}
