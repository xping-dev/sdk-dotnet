/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Services.Diagnostics;

/// <summary>
/// Joins messages from other code into one line of whole sentences.
/// </summary>
/// <remarks>
/// Validation errors and exception messages usually end with a period, but not always. A template
/// that adds its own period after them prints "mode.." for the first kind, and one that adds none
/// runs the second kind into the next sentence. The docs quote these lines word for word, so each
/// message is ended exactly once here instead.
/// </remarks>
internal static class Sentences
{
    /// <summary>
    /// Returns <paramref name="text"/> trimmed, with a period added unless it already ends with
    /// <c>.</c>, <c>!</c>, <c>?</c> or <c>…</c>, before any closing quotes and brackets.
    /// </summary>
    public static string End(string? text)
    {
        string trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return trimmed;

        // "Could not load 'appsettings.json.'" already ends its sentence inside the quote.
        int i = trimmed.Length - 1;
        while (i > 0 && trimmed[i] is '\'' or '"' or ')' or ']' or '\u2019' or '\u201D')
            i--;

        return trimmed[i] is '.' or '!' or '?' or '\u2026' ? trimmed : trimmed + ".";
    }

    /// <summary>
    /// Ends each message with <see cref="End"/> and joins them with a space, skipping blank ones.
    /// </summary>
    public static string Join(IEnumerable<string?> messages) =>
        string.Join(" ", messages.Select(End).Where(m => m.Length > 0));
}
