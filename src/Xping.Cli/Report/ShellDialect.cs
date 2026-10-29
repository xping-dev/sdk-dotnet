/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

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
/// Reads the <see cref="ShellDialect"/> a reader of this process's output most likely pastes into.
/// </summary>
/// <remarks>
/// By OS, not by environment: <c>PSModulePath</c> is set system-wide on Windows, cmd.exe included,
/// and <c>ComSpec</c> is always set, so neither names the parent shell. The two dialects differ only
/// for a value holding a quote, so the miss that leaves (Git Bash on Windows) rarely shows.
/// </remarks>
internal static class ShellDialects
{
    /// <summary>
    /// Gets the dialect for the operating system this process runs on.
    /// </summary>
    public static ShellDialect Current { get; } =
        OperatingSystem.IsWindows() ? ShellDialect.PowerShell : ShellDialect.Posix;
}
