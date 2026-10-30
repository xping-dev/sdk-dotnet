/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth.Browser;

namespace Xping.Cli.Report;

/// <summary>
/// The shell a printed command is quoted for.
/// </summary>
internal enum ShellDialect
{
    /// <summary>bash, zsh, sh.</summary>
    Posix,

    /// <summary>PowerShell 7.3 or later.</summary>
    PowerShell
}

/// <summary>
/// Chooses the <see cref="ShellDialect"/> a reader of this process's output pastes into.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Variable"/> first, because nothing the process can see names the shell that started it:
/// <c>PSModulePath</c> is set system-wide on Windows, cmd.exe included, and <c>ComSpec</c> is always
/// set. The OS is the fallback, and Git Bash on Windows, where coding agents usually run commands, is
/// the case it gets wrong.
/// </para>
/// <para>
/// A value it does not know is warned about and the fallback applies. A report is never refused over
/// how its commands are quoted.
/// </para>
/// </remarks>
internal static class ShellDialects
{
    /// <summary>
    /// The environment variable that names the dialect: <c>posix</c> or <c>powershell</c>.
    /// </summary>
    public const string Variable = "XPING_SHELL";

    /// <summary>
    /// Resolves the dialect.
    /// </summary>
    /// <param name="setting">The value of <see cref="Variable"/>, or <see langword="null"/>.</param>
    /// <param name="os">The operating system this process runs on.</param>
    /// <returns>The dialect, and a warning to print when the setting was not understood.</returns>
    public static (ShellDialect Dialect, string? Warning) Resolve(string? setting, HostOs os)
    {
        ShellDialect fallback = os == HostOs.Windows ? ShellDialect.PowerShell : ShellDialect.Posix;

        if (string.IsNullOrWhiteSpace(setting))
            return (fallback, null);

        return setting.Trim().ToUpperInvariant() switch
        {
            "POSIX" => (ShellDialect.Posix, null),
            "POWERSHELL" => (ShellDialect.PowerShell, null),
            _ => (fallback,
                $"xping: {Variable}=\"{setting}\" is not posix or powershell; " +
                $"commands are quoted for {(fallback == ShellDialect.PowerShell ? "powershell" : "posix")}.")
        };
    }
}
