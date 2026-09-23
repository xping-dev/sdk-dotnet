/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

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
/// Every command produced here is one the tool accepts today. The target is
/// <c>xping report --id</c>, the detail view of one finding; nothing here names a verb that does
/// not exist, because this is the field a reader is most likely to run.
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
    /// <param name="assembly">
    /// The assembly its subject belongs to, or <see langword="null"/> when none was recorded.
    /// </param>
    /// <returns>The command.</returns>
    /// <remarks>
    /// Scoped to the assembly because an id is a fact about a window, and the window a bare
    /// <c>xping report</c> picks is the assembly that ran most recently — not necessarily this one.
    /// </remarks>
    public static string ForFinding(string id, string? assembly)
    {
        string command = $"xping report --id {id}";

        return string.IsNullOrEmpty(assembly)
            ? command
            : $"{command} --assembly {Quote(assembly)}";
    }

    /// <summary>
    /// Builds the invocation that shows the untruncated report.
    /// </summary>
    /// <returns>The command.</returns>
    /// <remarks>
    /// This is the "show me the other eleven" affordance, and a reader who wants eleven more of what
    /// they are already looking at should get exactly that.
    /// </remarks>
    public static string ForFullReport() => "xping report --all";

    private static string Quote(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
}
