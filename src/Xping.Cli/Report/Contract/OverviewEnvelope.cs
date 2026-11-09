/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Contract;

/// <summary>
/// The values of an envelope's <c>scope</c>, which tell the two envelopes apart.
/// </summary>
internal static class ReportScopes
{
    /// <summary>One assembly's report: <see cref="ReportEnvelope"/>.</summary>
    public const string Assembly = "assembly";

    /// <summary>One row per assembly: <see cref="OverviewEnvelope"/>.</summary>
    public const string Overview = "overview";
}

/// <summary>
/// What a bare <c>xping report</c> writes over a store whose runs name more than one assembly.
/// </summary>
/// <remarks>
/// Property declaration order is the serialised property order, so it is part of the contract. The
/// schema version is <see cref="ReportEnvelope"/>'s: the two envelopes are one contract, and a
/// script reads <c>scope</c> to know which one it holds.
/// </remarks>
/// <param name="SchemaVersion">Version of the envelopes' shape.</param>
/// <param name="Scope">Always <see cref="ReportScopes.Overview"/>.</param>
/// <param name="Assemblies">One row per assembly, most severe first.</param>
internal sealed record OverviewEnvelope(
    string SchemaVersion,
    string Scope,
    IReadOnlyList<AssemblyOverviewDto> Assemblies);

/// <summary>
/// One assembly's row in the overview.
/// </summary>
/// <remarks>
/// <see cref="Window"/> and <see cref="Summary"/> are the objects that assembly's own report
/// writes, taken from it rather than recomputed. That is what keeps a row from ever disagreeing with
/// the report its command opens (report-format Amendment 6B).
/// </remarks>
/// <param name="Assembly">The assembly's name, as the runs recorded it.</param>
/// <param name="Window">The window its report reads; null when it could not be opened.</param>
/// <param name="Summary">Its report's counts; null when the window could not be opened.</param>
/// <param name="Unavailable">Why the window could not be opened; null when it was.</param>
/// <param name="Command">The command that opens this assembly's report.</param>
internal sealed record AssemblyOverviewDto(
    string Assembly,
    WindowDto? Window,
    SummaryDto? Summary,
    string? Unavailable,
    string Command);
