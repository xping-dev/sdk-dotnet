/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Local;
using Xping.Sdk.Core.Models.Statistics;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Sdk.Core.Services.Reporting.Internals;

/// <summary>
/// Default <see cref="ILocalRunReporter"/>: writes the run to the store, analyses recent history and
/// prints the summary.
/// </summary>
internal sealed class LocalRunReporter(
    ILocalRunStore store,
    LocalStoreOptions storeOptions,
    XpingMode mode,
    bool isCiEnvironment,
    ILogger<LocalRunReporter> logger) : ILocalRunReporter
{
    internal const string SuppressReportVariable = "XPING_REPORT";
    internal const string SuppressBannerVariable = "XPING_NO_BANNER";
    internal const string StateFileName = "state.json";

    private static readonly TimeSpan CtaInterval = TimeSpan.FromHours(24);

    public void Report(LocalRun run, QuickStatistics? stats)
    {
        if (run == null)
            throw new ArgumentNullException(nameof(run));

        try
        {
            store.Write(run);

            if (!ShouldRenderReport())
                return;

            // Scope history to this test assembly. All test projects in a solution share one store,
            // so an unfiltered window would report another project's tests as if they were ours.
            IReadOnlyList<LocalRun> recent =
                store.ReadRecent(storeOptions.AnalysisWindow, run.Header.Assembly);

            // A store that could not be written still deserves a report for the current run, so fall
            // back to analysing just this one.
            if (recent.Count == 0)
                recent = [run];

            LocalAnalysis analysis = LocalFlakinessAnalyzer.Analyze(recent);

            string? report = LocalRunReportWriter.Render(
                stats,
                analysis,
                ReportGlyphs.Detect(),
                ShouldShowCta(analysis),
                store.StorePath);

            if (report != null)
                TerminalWriter.Write(report);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Reporting is a side channel. It must never be the reason a test run fails.
            logger.LogDebug("Local run report skipped: {Message}", ex.Message);
        }
    }

    private bool ShouldRenderReport()
    {
        // Connected users get the report too, but only once they have local history worth showing;
        // their primary surface is the dashboard.
        if (mode == XpingMode.Disabled)
            return false;

        string? setting = System.Environment.GetEnvironmentVariable(SuppressReportVariable);
        return !string.Equals(setting, "off", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Decides whether to append the cloud invitation.
    /// </summary>
    /// <remarks>
    /// Every condition here exists to keep the invitation from becoming noise. An SDK that pitches
    /// on every run gets uninstalled, and the right to print anything at all depends on respecting
    /// these.
    /// </remarks>
    private bool ShouldShowCta(LocalAnalysis analysis)
    {
        // Already a customer.
        if (mode != XpingMode.LocalOnly)
            return false;

        // CI logs are read when something breaks. A signup pitch there is pure noise.
        if (isCiEnvironment)
            return false;

        if (System.Environment.GetEnvironmentVariable(SuppressBannerVariable) is { Length: > 0 })
            return false;

        // Only ask when the developer has just been shown a problem worth solving.
        if (!analysis.HasFindings)
            return false;

        return TryConsumeCtaAllowance();
    }

    /// <summary>
    /// Returns whether the once-per-day invitation budget is available, consuming it if so.
    /// </summary>
    private bool TryConsumeCtaAllowance()
    {
        string? root = store.StorePath;
        if (root == null)
            return false;

        string statePath = Path.Combine(root, StateFileName);

        try
        {
            if (File.Exists(statePath))
            {
                string content = File.ReadAllText(statePath);
                if (TryParseLastShown(content, out DateTime lastShown) &&
                    DateTime.UtcNow - lastShown < CtaInterval)
                {
                    return false;
                }
            }

            File.WriteAllText(
                statePath,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\"ctaLastShownUtc\":\"{0:O}\"}}",
                    DateTime.UtcNow));

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // If the throttle cannot be recorded, stay quiet rather than risk pitching every run.
            logger.LogDebug("CTA throttle unavailable: {Message}", ex.Message);
            return false;
        }
    }

    private static bool TryParseLastShown(string content, out DateTime lastShown)
    {
        lastShown = default;

        const string Key = "\"ctaLastShownUtc\":\"";
        int start = content.IndexOf(Key, StringComparison.Ordinal);
        if (start < 0)
            return false;

        start += Key.Length;
        int end = content.IndexOf('"', start);
        if (end < 0)
            return false;

        return DateTime.TryParse(
            content.Substring(start, end - start),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out lastShown);
    }
}
