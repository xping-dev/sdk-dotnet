/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Commands;

/// <summary>
/// Options for <c>xping report</c>.
/// </summary>
internal sealed class ReportOptions
{
    /// <summary>Gets or sets the number of recent runs to analyse.</summary>
    public int Last { get; set; } = 12;

    /// <summary>Gets or sets the test assembly to restrict the report to, or <see langword="null"/> for all.</summary>
    public string? Assembly { get; set; }

    /// <summary>Gets or sets the directory to resolve the store from. Defaults to the working directory.</summary>
    public string? Directory { get; set; }

    /// <summary>Gets or sets a value indicating whether per-test history should be printed.</summary>
    public bool Details { get; set; }

    /// <summary>Gets or sets a value indicating whether to force the ASCII glyph set.</summary>
    public bool Ascii { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to report across every assembly in the store rather
    /// than scoping to one.
    /// </summary>
    public bool All { get; set; }

    /// <summary>Gets or sets a value indicating whether to emit JSON instead of a rendered block.</summary>
    public bool Json { get; set; }
}
