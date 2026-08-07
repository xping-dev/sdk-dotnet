/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;

namespace Xping.Sdk.Core.Services.Reporting.Internals;

/// <summary>
/// The character set used to draw the report, matched to what the terminal can actually render.
/// </summary>
/// <remarks>
/// Windows conhost still defaults to code page 437 in some configurations, and a box-drawing
/// character there renders as mojibake. Detecting the output encoding and degrading to ASCII keeps
/// the report legible instead of turning it into visual noise.
/// </remarks>
internal sealed class ReportGlyphs
{
    /// <summary>Unicode glyphs for terminals that can render them.</summary>
    public static ReportGlyphs Unicode { get; } = new()
    {
        HorizontalRule = '─',   // ─
        Pass = "✓",             // ✓
        Fail = "✗",             // ✗
        Skip = "○",             // ○
        Warning = "⚠",          // ⚠
        Pending = "◷",          // ◷
        Separator = "·",        // ·
        Arrow = "→",            // →
        HistoryPass = '●',      // ●
        HistoryFail = '○'       // ○
    };

    /// <summary>ASCII fallback for terminals that cannot.</summary>
    public static ReportGlyphs Ascii { get; } = new()
    {
        HorizontalRule = '-',
        Pass = "+",
        Fail = "x",
        Skip = "o",
        Warning = "!",
        Pending = "*",
        Separator = "|",
        Arrow = "->",
        HistoryPass = '#',
        HistoryFail = '.'
    };

    /// <summary>Gets the character used to draw horizontal rules.</summary>
    public char HorizontalRule { get; private init; }

    /// <summary>Gets the passed marker.</summary>
    public string Pass { get; private init; } = "+";

    /// <summary>Gets the failed marker.</summary>
    public string Fail { get; private init; } = "x";

    /// <summary>Gets the skipped marker.</summary>
    public string Skip { get; private init; } = "o";

    /// <summary>Gets the warning marker.</summary>
    public string Warning { get; private init; } = "!";

    /// <summary>Gets the in-progress marker.</summary>
    public string Pending { get; private init; } = "*";

    /// <summary>Gets the inline separator.</summary>
    public string Separator { get; private init; } = "|";

    /// <summary>Gets the arrow used for calls to action.</summary>
    public string Arrow { get; private init; } = "->";

    /// <summary>Gets the sparkline character for a passing run.</summary>
    public char HistoryPass { get; private init; } = '#';

    /// <summary>Gets the sparkline character for a failing run.</summary>
    public char HistoryFail { get; private init; } = '.';

    /// <summary>
    /// Selects a glyph set for the current console.
    /// </summary>
    /// <returns><see cref="Unicode"/> when the output encoding supports it, otherwise <see cref="Ascii"/>.</returns>
    public static ReportGlyphs Detect()
    {
        try
        {
            Encoding encoding = Console.OutputEncoding;

            bool unicodeCapable =
                encoding.CodePage == 65001 ||            // UTF-8
                encoding.CodePage == 1200 ||             // UTF-16 LE
                encoding.CodePage == 1201 ||             // UTF-16 BE
                encoding is UTF8Encoding or UnicodeEncoding;

            return unicodeCapable ? Unicode : Ascii;
        }
        catch (IOException)
        {
            // No console attached; ASCII is the safe assumption for a redirected stream.
            return Ascii;
        }
        catch (System.Security.SecurityException)
        {
            return Ascii;
        }
    }
}
