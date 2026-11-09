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
    /// Names the assembly whether or not the caller did. An id is a fact about one assembly's window,
    /// and the envelope this string lands in outlives the store it was printed from.
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
    /// they are already looking at should get exactly that. It names the assembly even when the
    /// caller did not: a bare <c>xping report</c> over a store that has since gained a second
    /// assembly is the overview, not this report.
    /// </remarks>
    public static string ForFullReport(ReportScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return Scoped("xping report --all", scope, scope.Assembly);
    }

    /// <summary>
    /// Builds the invocation that shows one assembly's report, for the overview to point at.
    /// </summary>
    /// <param name="scope">The overview's scope, with the assembly the command names.</param>
    /// <returns>The command.</returns>
    public static string ForAssembly(ReportScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return Scoped("xping report", scope, scope.Assembly);
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

        var builder = new StringBuilder("xping report --test ").Append(ShellQuote.Of(fingerprint, scope.Shell));

        if (!string.IsNullOrEmpty(scope.Assembly))
            builder.Append(" --assembly ").Append(ShellQuote.Of(scope.Assembly, scope.Shell));

        if (!string.IsNullOrEmpty(scope.Directory))
            builder.Append(" --directory ").Append(ShellQuote.Of(scope.Directory, scope.Shell));

        return builder.ToString();
    }

    private static string Scoped(string command, ReportScope scope, string? assembly)
    {
        var builder = new StringBuilder(command);

        if (!string.IsNullOrEmpty(assembly))
            builder.Append(" --assembly ").Append(ShellQuote.Of(assembly, scope.Shell));

        // A whole-store window has no flag of its own, and a bare command would open report's
        // default window instead. Its size is pinned, not its sessions: a run stored in between
        // joins the window and the oldest drops out (Amendment 2A).
        int? pinned = scope.Runs ?? (scope.Resolution == WindowResolution.Store ? scope.WindowRuns : null);

        if (pinned is { } runs)
            builder.Append(" --runs ").Append(runs.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrEmpty(scope.Since))
            builder.Append(" --since ").Append(ShellQuote.Of(scope.Since, scope.Shell));

        if (!string.IsNullOrEmpty(scope.Directory))
            builder.Append(" --directory ").Append(ShellQuote.Of(scope.Directory, scope.Shell));

        return builder.ToString();
    }
}
