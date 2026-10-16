/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Windowing;

namespace Xping.Cli.Report;

/// <summary>
/// What a report was scoped to, as far as a command it prints has to reproduce it.
/// </summary>
/// <remarks>
/// Every command the report prints is one the reader runs next, and it has to land on the report it
/// was printed from. A drill-down that dropped <c>--since</c> would open a different window and
/// report the finding absent; one that dropped <c>--directory</c> would not find the store at all.
/// </remarks>
/// <param name="Assembly">The assembly the report covers, whether named or auto-scoped.</param>
/// <param name="Resolution">How the report's window was chosen.</param>
/// <param name="WindowRuns">
/// How many sessions the window held. A <see cref="WindowResolution.Store"/> window has no flag to
/// repeat, so a printed command pins its size with <c>--runs</c> instead. Its size, not its
/// sessions: a run stored in between joins the window and the oldest drops out.
/// Required, like <paramref name="Resolution"/>: a scope that defaulted them would print a command
/// for some other window without anything failing.
/// </param>
/// <param name="AssemblyGiven">Whether the caller named it with <c>--assembly</c>.</param>
/// <param name="Runs">The <c>--runs</c> the caller gave, or <see langword="null"/>.</param>
/// <param name="Since">The <c>--since</c> the caller gave, or <see langword="null"/>.</param>
/// <param name="Directory">The <c>--directory</c> the caller gave, or <see langword="null"/>.</param>
internal sealed record ReportScope(
    string? Assembly,
    WindowResolution Resolution,
    int WindowRuns,
    bool AssemblyGiven = false,
    int? Runs = null,
    string? Since = null,
    string? Directory = null);
