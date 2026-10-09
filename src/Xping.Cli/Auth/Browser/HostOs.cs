/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth.Browser;

/// <summary>
/// The operating system families the browser rules of cli-auth-cli-spec §5 tell apart.
/// </summary>
internal enum HostOs
{
    /// <summary>Windows.</summary>
    Windows,

    /// <summary>macOS.</summary>
    MacOS,

    /// <summary>Linux, including WSL.</summary>
    Linux,

    /// <summary>Anything else; no browser is launched.</summary>
    Other
}

/// <summary>
/// Reads the <see cref="HostOs"/> of this process.
/// </summary>
internal static class HostOsDetector
{
    /// <summary>
    /// Gets the operating system this process runs on.
    /// </summary>
    public static HostOs Current { get; } =
        OperatingSystem.IsWindows() ? HostOs.Windows
        : OperatingSystem.IsMacOS() ? HostOs.MacOS
        : OperatingSystem.IsLinux() ? HostOs.Linux
        : HostOs.Other;
}
