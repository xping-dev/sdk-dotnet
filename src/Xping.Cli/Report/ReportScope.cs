/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

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
/// <param name="AssemblyGiven">Whether the caller named it with <c>--assembly</c>.</param>
/// <param name="Runs">The <c>--runs</c> the caller gave, or <see langword="null"/>.</param>
/// <param name="Since">The <c>--since</c> the caller gave, or <see langword="null"/>.</param>
/// <param name="Directory">The <c>--directory</c> the caller gave, or <see langword="null"/>.</param>
internal sealed record ReportScope(
    string? Assembly,
    bool AssemblyGiven = false,
    int? Runs = null,
    string? Since = null,
    string? Directory = null);
