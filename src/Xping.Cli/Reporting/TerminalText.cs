/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Buffers;
using System.Globalization;
using System.Text;

namespace Xping.Cli.Reporting;

/// <summary>
/// Which characters a piece of recorded text may keep when it is written to a terminal.
/// </summary>
internal enum TerminalCharset
{
    /// <summary>Everything but controls, line and paragraph separators, and format characters.</summary>
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
/// <para>
/// By general category rather than by list. Cf holds every bidi control and invisible character,
/// tag characters included, and a category stays complete when Unicode adds one (Amendment 2D).
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
            bool safe = status == OperationStatus.Done && !IsUnsafe(rune, charset);

            if (!safe)
                (cleaned ??= new StringBuilder(text.Length).Append(text, 0, index)).Append(Replacement);
            else
                cleaned?.Append(text, index, consumed);

            index += consumed;
        }

        return cleaned?.ToString() ?? text;
    }

    /// <summary>
    /// Takes at most <paramref name="length"/> characters from the start, one fewer where the last
    /// would be half of a surrogate pair.
    /// </summary>
    public static string Head(string text, int length)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length <= length)
            return text;

        int end = length > 0 && char.IsHighSurrogate(text[length - 1]) ? length - 1 : length;
        return text[..end];
    }

    /// <summary>
    /// Takes at most <paramref name="length"/> characters from the end, one fewer where the first
    /// would be half of a surrogate pair.
    /// </summary>
    public static string Tail(string text, int length)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length <= length)
            return text;

        int start = text.Length - length;
        return text[(char.IsLowSurrogate(text[start]) ? start + 1 : start)..];
    }

    private static bool IsUnsafe(Rune rune, TerminalCharset charset) =>
        (charset == TerminalCharset.Ascii && rune.Value >= 0x80)
        || Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format
            or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}
