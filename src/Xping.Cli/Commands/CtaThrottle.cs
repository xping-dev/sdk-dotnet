/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Commands;

/// <summary>
/// Decides whether to show the cloud invitation, and rate-limits it.
/// </summary>
/// <remarks>
/// Every rule here exists to keep the invitation from becoming noise. A tool that pitches on every
/// invocation gets uninstalled, and respecting these is what earns the right to print it at all.
/// </remarks>
internal sealed class CtaThrottle(IEnvironmentVariableProvider environment, TimeProvider timeProvider)
{
    internal const string SuppressBannerVariable = "XPING_NO_BANNER";
    internal const string StateFileName = "state.json";

    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>
    /// Returns whether the invitation should be shown, consuming the daily allowance if so.
    /// </summary>
    /// <param name="storeRoot">The local store root, used to persist the throttle timestamp.</param>
    /// <param name="hasFindings">Whether the report found anything worth acting on.</param>
    /// <param name="isConnected">Whether the runs were recorded by a cloud-connected project.</param>
    public bool ShouldShow(string? storeRoot, bool hasFindings, bool isConnected)
    {
        // Already a customer.
        if (isConnected)
            return false;

        // Only ask when the developer has just been shown a problem worth solving.
        if (!hasFindings)
            return false;

        if (environment.GetVariable(SuppressBannerVariable) is { Length: > 0 })
            return false;

        return TryConsumeAllowance(storeRoot);
    }

    private bool TryConsumeAllowance(string? storeRoot)
    {
        if (storeRoot == null)
            return false;

        string statePath = Path.Combine(storeRoot, StateFileName);
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            if (File.Exists(statePath))
            {
                string content = File.ReadAllText(statePath);
                if (TryParseLastShown(content, out DateTime lastShown) &&
                    now - lastShown < Interval)
                {
                    return false;
                }
            }

            File.WriteAllText(
                statePath,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\"ctaLastShownUtc\":\"{0:O}\"}}",
                    now));

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // If the throttle cannot be recorded, stay quiet rather than risk pitching every run.
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
            content.AsSpan(start, end - start),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out lastShown);
    }
}
