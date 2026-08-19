/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Model;

namespace Xping.Cli.Commands;

/// <summary>
/// The output format for <c>xping report</c>.
/// </summary>
internal enum ReportFormat
{
    /// <summary>A rendered block for a person reading a terminal.</summary>
    Text,

    /// <summary>The versioned envelope for a script or an agent.</summary>
    Json,

    /// <summary>One line, for a chat message, a commit trailer or a CI step title.</summary>
    Summary
}

/// <summary>
/// Options for <c>xping report</c>.
/// </summary>
internal sealed class ReportOptions
{
    /// <summary>
    /// Gets or sets the number of recent runs to analyse, or <see langword="null"/> for the default
    /// window.
    /// </summary>
    public int? Runs { get; set; }

    /// <summary>
    /// Gets or sets the commit or date to analyse from, or <see langword="null"/> for the default
    /// window.
    /// </summary>
    public string? Since { get; set; }

    /// <summary>
    /// Gets or sets the number of findings to show, or <see langword="null"/> to show all of them.
    /// </summary>
    public int? Top { get; set; }

    /// <summary>Gets or sets the kinds to report, or empty for all of them.</summary>
    public IReadOnlyList<FindingKind> Kinds { get; set; } = [];

    /// <summary>Gets or sets the test assembly to restrict to, or <see langword="null"/> to auto-scope.</summary>
    public string? Assembly { get; set; }

    /// <summary>Gets or sets the directory to resolve the store from. Defaults to the working directory.</summary>
    public string? Directory { get; set; }

    /// <summary>Gets or sets the output format.</summary>
    public ReportFormat Format { get; set; } = ReportFormat.Text;

    /// <summary>
    /// Gets or sets the least severity that should fail the command, or <see langword="null"/> to
    /// always succeed when a report was produced.
    /// </summary>
    public Severity? FailOn { get; set; }

    /// <summary>Gets or sets a value indicating whether to force the ASCII glyph set.</summary>
    public bool Ascii { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to suppress ANSI colour regardless of the terminal.
    /// </summary>
    public bool NoColor { get; set; }
}
