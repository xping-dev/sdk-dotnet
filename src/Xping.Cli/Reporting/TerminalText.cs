/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Buffers;
using System.Text;

namespace Xping.Cli.Reporting;

/// <summary>
/// Which characters a piece of recorded text may keep when it is written to a terminal.
/// </summary>
internal enum TerminalCharset
{
    /// <summary>Everything but controls, separators, bidi and invisible format characters.</summary>
    Unicode,

    /// <summary>Printable ASCII only.</summary>
    Ascii,

    /// <summary>
    /// A command printed to be run: what <see cref="Unicode"/> keeps, in every glyph set. Replacing
    /// an <c>é</c> in a test name would make the command select nothing.
    /// </summary>
    Command
}

/// <summary>
/// Makes recorded text safe to write to a terminal (cli-failure-mode-spec D7).
/// </summary>
/// <remarks>
/// <para>
/// A test name, a message or a path is whatever the test author or the code under test wrote. An
/// escape sequence in one would recolour or rewrite the terminal, a line separator would forge a
/// row, and a bidi override would make the line read differently from what it holds.
/// </para>
/// <para>
/// One <c>?</c> per rune, so a cleaned value is never wider than the original and the report's width
/// rules hold as they are. A lone surrogate is not a rune and is replaced the same way.
/// </para>
/// </remarks>
internal static class TerminalText
{
    private const char Replacement = '?';

    /// <summary>
    /// Replaces every rune <paramref name="charset"/> does not allow with <c>?</c>.
    /// </summary>
    /// <param name="text">The recorded text.</param>
    /// <param name="charset">What the text may keep.</param>
    /// <returns>The text, or the same instance when nothing needed replacing.</returns>
    public static string Printable(string text, TerminalCharset charset)
    {
        ArgumentNullException.ThrowIfNull(text);

        StringBuilder? cleaned = null;
        int index = 0;

        while (index < text.Length)
        {
            OperationStatus status = Rune.DecodeFromUtf16(text.AsSpan(index), out Rune rune, out int consumed);
            bool safe = status == OperationStatus.Done && !IsUnsafe(rune.Value, charset);

            if (!safe)
                (cleaned ??= new StringBuilder(text.Length).Append(text, 0, index)).Append(Replacement);
            else
                cleaned?.Append(text, index, consumed);

            index += consumed;
        }

        return cleaned?.ToString() ?? text;
    }

    private static bool IsUnsafe(int value, TerminalCharset charset) =>
        value is <= 0x1F or (>= 0x7F and <= 0x9F)
        || (charset == TerminalCharset.Ascii && value >= 0x80)
        || value is 0x00AD or 0x061C or 0x2028 or 0x2029 or 0xFEFF
            // Zero-width space, non-joiner and joiner, then the LRM and RLM marks.
            or (>= 0x200B and <= 0x200F)
            // Bidi embeddings and overrides.
            or (>= 0x202A and <= 0x202E)
            // Word joiner and the invisible operators.
            or (>= 0x2060 and <= 0x2064)
            // Bidi isolates.
            or (>= 0x2066 and <= 0x2069);
}
