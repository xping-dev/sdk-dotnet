/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Text;
using Xping.Cli.Report.Windowing;

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
/// Every command produced here is one the tool accepts today. The targets are
/// <c>xping report --id</c>, the detail view of one finding, and <c>xping report --test</c>, the
/// history of one test; nothing here names a verb that does not exist, because this is the field a
/// reader is most likely to run.
/// </para>
/// <para>
/// No command here carries <c>--format</c>. A caller that wants the envelope adds the flag, having
/// just used it; a person who copies a command out of a pasted JSON envelope wants the view they
/// would read, and one string then serves the envelope and the rendered report alike.
/// </para>
/// </remarks>
internal static class DrillDown
{
    /// <summary>
    /// Builds the invocation that shows one finding in detail.
    /// </summary>
    /// <param name="id">The finding's id.</param>
    /// <param name="scope">What the report was scoped to.</param>
    /// <returns>The command.</returns>
    /// <remarks>
    /// Names the assembly whether or not the caller did. An id is a fact about a window, and a bare
    /// <c>xping report</c> scopes itself to whichever assembly ran most recently; the envelope this
    /// string lands in outlives the moment that was this one.
    /// </remarks>
    public static string ForFinding(string id, ReportScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return Scoped($"xping report --id {id}", scope, scope.Assembly);
    }

    /// <summary>
    /// Builds the invocation that shows the untruncated report.
    /// </summary>
    /// <param name="scope">What the report was scoped to.</param>
    /// <returns>The command.</returns>
    /// <remarks>
    /// This is the "show me the other eleven" affordance, and a reader who wants eleven more of what
    /// they are already looking at should get exactly that. It repeats only what the caller typed:
    /// an auto-scoped report's bare <c>--all</c> scopes itself the same way, and one opened from a
    /// drill-down, which always names the assembly, gets the assembly back.
    /// </remarks>
    public static string ForFullReport(ReportScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return Scoped("xping report --all", scope, scope.AssemblyGiven ? scope.Assembly : null);
    }

    /// <summary>
    /// Builds the invocation that shows one test's run-by-run history.
    /// </summary>
    /// <param name="fingerprint">The test's short fingerprint.</param>
    /// <param name="scope">What the report was scoped to; its assembly is the test's.</param>
    /// <returns>The command.</returns>
    /// <remarks>
    /// Repeats the assembly and the directory, and not the window: the history's default window is
    /// the whole store, which is what a reader following this link wants, and a <c>--runs</c> copied
    /// from a finding's window would cut the history to the runs that finding was judged on.
    /// </remarks>
    public static string ForTest(string fingerprint, ReportScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var builder = new StringBuilder("xping report --test ").Append(Quote(fingerprint));

        if (!string.IsNullOrEmpty(scope.Assembly))
            builder.Append(" --assembly ").Append(Quote(scope.Assembly));

        if (!string.IsNullOrEmpty(scope.Directory))
            builder.Append(" --directory ").Append(Quote(scope.Directory));

        return builder.ToString();
    }

    private static string Scoped(string command, ReportScope scope, string? assembly)
    {
        var builder = new StringBuilder(command);

        if (!string.IsNullOrEmpty(assembly))
            builder.Append(" --assembly ").Append(Quote(assembly));

        // A whole-store window has no flag of its own, and a bare command would open report's
        // default window instead. Its size is pinned, not its sessions: a run stored in between
        // joins the window and the oldest drops out (Amendment 2A).
        int? pinned = scope.Runs ?? (scope.Resolution == WindowResolution.Store ? scope.WindowRuns : null);

        if (pinned is { } runs)
            builder.Append(" --runs ").Append(runs.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrEmpty(scope.Since))
            builder.Append(" --since ").Append(Quote(scope.Since));

        if (!string.IsNullOrEmpty(scope.Directory))
            builder.Append(" --directory ").Append(Quote(scope.Directory));

        return builder.ToString();
    }

    /// <summary>
    /// Quotes a value for the shell the command is pasted into, when it needs it.
    /// </summary>
    /// <remarks>
    /// Single quotes, because they read literally in POSIX shells and in PowerShell: no <c>$</c>,
    /// <c>&amp;</c> or <c>;</c> inside them does anything, and a trailing <c>\</c> cannot escape the
    /// closing quote the way it does inside double quotes. An embedded <c>'</c> closes the quote,
    /// escapes itself and reopens it. A value made only of characters no shell treats specially is
    /// left bare, so the common command reads as typed.
    /// </remarks>
    private static string Quote(string value) =>
        value.Length > 0 && value.All(IsPlain)
            ? value
            : "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static bool IsPlain(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '/' or ':' or '@' or '%' or '+' or '=' or ',' or '-';
}
