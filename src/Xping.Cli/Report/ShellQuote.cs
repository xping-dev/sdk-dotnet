/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report;

/// <summary>
/// Quotes a value for the shell a printed command is pasted into, when it needs it.
/// </summary>
/// <remarks>
/// Single quotes, because they read literally in POSIX shells and in PowerShell: no <c>$</c>,
/// <c>&amp;</c> or <c>;</c> inside them does anything, and a trailing <c>\</c> cannot escape the
/// closing quote the way it does inside double quotes. An embedded <c>'</c> closes the quote,
/// escapes itself and reopens it. A value made only of characters no shell treats specially is
/// left bare, so the common command reads as typed.
/// </remarks>
internal static class ShellQuote
{
    /// <summary>
    /// Quotes a value, or returns it bare when no shell would read anything into it.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value as it should be printed.</returns>
    public static string Of(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Length > 0 && value.All(IsPlain)
            ? value
            : "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }

    private static bool IsPlain(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '/' or ':' or '@' or '%' or '+' or '=' or ',' or '-';
}
