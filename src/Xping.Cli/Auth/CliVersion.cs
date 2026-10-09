/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth;

/// <summary>
/// The version of this CLI, as compared with the server's <c>xping_cli_min_version</c> and sent in
/// <c>User-Agent</c>.
/// </summary>
/// <remarks>Injected so tests can be an older or newer CLI than the one being built.</remarks>
/// <param name="Value">A SemVer string such as <c>1.2.3</c> or <c>1.3.0-beta.1</c>.</param>
internal sealed record CliVersion(string Value)
{
    /// <summary>
    /// Gets the <c>User-Agent</c> of every request to Xping Cloud (contract §7.7).
    /// </summary>
    /// <remarks>
    /// The operating system family only: the full description names a kernel build, which tells the
    /// server nothing it needs and identifies the machine more than it should.
    /// </remarks>
    public string UserAgent => $"xping-cli/{Value} ({OperatingSystemFamily})";

    private static string OperatingSystemFamily =>
        OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsMacOS() ? "macos"
        : OperatingSystem.IsLinux() ? "linux"
        : "other";
}
