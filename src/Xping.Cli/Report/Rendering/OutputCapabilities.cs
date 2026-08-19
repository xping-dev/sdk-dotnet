/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Reporting;

namespace Xping.Cli.Report.Rendering;

/// <summary>
/// What this invocation's stdout can carry, and what it should therefore be given.
/// </summary>
/// <remarks>
/// <para>
/// Resolved once per invocation and passed down, rather than each writer asking the console again.
/// The report already asked twice with two different answers possible; a single value is what makes
/// "is this going to a person or to a pipe" one decision instead of several.
/// </para>
/// <para>
/// The rule is the one every mainstream CLI settled on: a terminal gets decoration, a pipe gets
/// content. Escape codes that reach a file are noise a reader has to strip, and a marketing line
/// that reaches a pipe is noise a script has to parse around.
/// </para>
/// </remarks>
/// <param name="Glyphs">The character set the report is drawn with.</param>
/// <param name="Color">Whether ANSI colour may be emitted.</param>
/// <param name="Decorate">
/// Whether output beyond the report itself — the scope notice, the cloud invitation — may be
/// written. False whenever stdout is redirected, so <c>xping report | pbcopy</c> copies a report
/// and nothing else.
/// </param>
internal sealed record OutputCapabilities(ReportGlyphs Glyphs, bool Color, bool Decorate)
{
    private const string Reset = "\u001b[0m";

    // Bright black rather than SGR 2. Faint is widely ignored, and a "dim" that renders at full
    // strength on half the terminals is not a distinction a reader can rely on.
    private const string Faint = "\u001b[90m";

    /// <summary>
    /// Resolves capabilities from explicit inputs.
    /// </summary>
    /// <param name="forceAscii">Whether <c>--ascii</c> was given.</param>
    /// <param name="noColor">Whether <c>--no-color</c> was given.</param>
    /// <param name="redirected">Whether stdout is redirected.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The capabilities.</returns>
    /// <remarks>
    /// <c>NO_COLOR</c> and <c>FORCE_COLOR</c> are honoured as the informal standards define them:
    /// any value at all counts, and <c>NO_COLOR</c> wins, because a user who set both is more likely
    /// to have inherited <c>FORCE_COLOR</c> from a tool than to have meant it here.
    /// </remarks>
    public static OutputCapabilities Resolve(
        bool forceAscii, bool noColor, bool redirected, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        bool suppressed = noColor || environment("NO_COLOR") is { Length: > 0 };
        bool forced = environment("FORCE_COLOR") is { Length: > 0 };

        // A redirected stream gets ASCII whatever the console encoding says. The encoding describes
        // what a console could draw; there is no console on the other end of a pipe.
        ReportGlyphs glyphs = forceAscii || redirected
            ? ReportGlyphs.Ascii
            : ReportGlyphs.Detect();

        return new OutputCapabilities(
            glyphs,
            !suppressed && (forced || !redirected),

            // Deliberately not lifted by FORCE_COLOR. That variable says what the stream can render,
            // not that a caller piping the report into a file wants a call to action in it.
            !redirected);
    }

    /// <summary>
    /// Wraps text so it recedes, when colour is available.
    /// </summary>
    /// <param name="text">The text to wrap.</param>
    /// <returns>The text, dimmed or untouched.</returns>
    /// <remarks>
    /// For the reference data a reader scans past — an evidence band, a finding id. Dimming it costs
    /// no lines and no columns, which spacing it out would, and a terminal copies plain text, so the
    /// pasted block is byte-identical either way.
    /// </remarks>
    public string Dim(string text) => Color ? Faint + text + Reset : text;

    /// <summary>
    /// Wraps text in the colour for a severity, when colour is available.
    /// </summary>
    /// <param name="severity">The severity, as the envelope spells it.</param>
    /// <param name="text">The text to wrap.</param>
    /// <returns>The text, coloured or untouched.</returns>
    public string Colorize(string severity, string text)
    {
        if (!Color)
            return text;

        string? code = severity switch
        {
            "high" => "\u001b[31m",
            "medium" => "\u001b[33m",
            "low" => "\u001b[90m",
            _ => null
        };

        return code == null ? text : code + text + Reset;
    }
}
