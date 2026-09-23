/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Model;

namespace Xping.Cli.Report;

/// <summary>
/// What the process returns, and why.
/// </summary>
/// <remarks>
/// Four outcomes. The distinction between the second and third is the point: a build step needs to
/// tell "I looked and found problems" apart from "I could not look" — collapsing both into a
/// non-zero code would make a broken store indistinguishable from a failing test suite. The fourth
/// answers the one question <c>--id</c> asks, and is not a failure of the tool.
/// </remarks>
internal static class ExitCodes
{
    /// <summary>The report ran and nothing met the failure threshold.</summary>
    public const int Success = 0;

    /// <summary>The report ran and something met the failure threshold.</summary>
    public const int FindingsAtThreshold = 1;

    /// <summary>There was not enough data to produce a report at all.</summary>
    public const int InsufficientData = 2;

    /// <summary>The report ran, and the finding <c>--id</c> asked for is not in it.</summary>
    public const int FindingNotReported = 3;

    /// <summary>
    /// Chooses the exit code for a finished report.
    /// </summary>
    /// <param name="findings">The findings produced, before truncation.</param>
    /// <param name="failOn">
    /// Least severity that should fail the command, or <see langword="null"/> to never fail on
    /// findings.
    /// </param>
    /// <returns>The exit code.</returns>
    /// <remarks>
    /// Judged against every finding produced, not merely the ones shown. A <c>--top 5</c> that hid
    /// the sixth finding must not also hide its effect on the exit code, or the threshold would
    /// quietly depend on the display limit.
    /// </remarks>
    public static int ForReport(IReadOnlyList<Finding> findings, Severity? failOn)
    {
        if (failOn is not { } threshold)
            return Success;

        // Severity is declared most-severe-first, so "at least as severe as" is <=.
        foreach (Finding finding in findings)
        {
            if (finding.Severity <= threshold)
                return FindingsAtThreshold;
        }

        return Success;
    }
}
