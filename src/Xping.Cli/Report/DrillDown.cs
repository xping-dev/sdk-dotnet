/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Model;

namespace Xping.Cli.Report;

/// <summary>
/// Builds the command that expands a finding.
/// </summary>
/// <remarks>
/// <para>
/// Every finding carries one, and it is not optional. It is how both a human and an agent navigate
/// from a summary to the detail without going to find documentation first — a report that says a
/// test is unreliable but not how to look closer forces the reader to guess at a command.
/// </para>
/// <para>
/// Every command produced here is one the tool accepts today. A per-test <c>xping test</c> verb is
/// planned and would be a better target, but emitting it before it exists would put a command that
/// fails into the field the reader is most likely to run.
/// </para>
/// </remarks>
internal static class DrillDown
{
    /// <summary>
    /// Builds the invocation that expands a finding about one test.
    /// </summary>
    /// <param name="kind">The kind of finding being expanded.</param>
    /// <param name="test">The test the finding is about.</param>
    /// <returns>The command.</returns>
    public static string ForTest(FindingKind kind, TestReference test)
    {
        string command = $"xping report --kind {kind} --format json";

        return string.IsNullOrEmpty(test.Assembly)
            ? command
            : $"{command} --assembly {Quote(test.Assembly)}";
    }

    /// <summary>
    /// Builds the invocation that shows the untruncated report.
    /// </summary>
    /// <returns>The command.</returns>
    public static string ForFullReport() => "xping report --all --format json";

    private static string Quote(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
}
