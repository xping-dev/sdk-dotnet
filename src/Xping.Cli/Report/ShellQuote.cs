/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;

namespace Xping.Cli.Report;

/// <summary>
/// Quotes a value for the shell a printed command is pasted into, when it needs it.
/// </summary>
/// <remarks>
/// <para>
/// Single quotes, because they read literally in POSIX shells and in PowerShell: no <c>$</c>,
/// <c>&amp;</c> or <c>;</c> inside them does anything, and a trailing <c>\</c> cannot escape the
/// closing quote the way it does inside double quotes. A value made only of characters neither shell
/// treats specially is left bare, so the common command reads as typed. <c>,</c> and <c>@</c> are not
/// among them: PowerShell reads a bare <c>a,b</c> as an array and a leading <c>@</c> as a splat.
/// </para>
/// <para>
/// Only an embedded quote differs. POSIX closes the quote, escapes it and reopens it; PowerShell
/// doubles it, and treats the typographic single quotes as quotes too.
/// </para>
/// </remarks>
internal static class ShellQuote
{
    /// <summary>
    /// Quotes a value, or returns it bare when no shell would read anything into it.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="shell">The shell the command is printed for.</param>
    /// <returns>The value as it should be printed.</returns>
    public static string Of(string value, ShellDialect shell)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length > 0 && value.All(IsPlain))
            return value;

        if (shell == ShellDialect.Posix)
            return "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

        var builder = new StringBuilder(value.Length + 2).Append('\'');

        foreach (char c in value)
        {
            if (IsPowerShellQuote(c))
                builder.Append(c);

            builder.Append(c);
        }

        return builder.Append('\'').ToString();
    }

    private static bool IsPlain(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '/' or ':' or '%' or '+' or '=' or '-';

    private static bool IsPowerShellQuote(char c) =>
        c is '\'' or '‘' or '’' or '‚' or '‛';
}
