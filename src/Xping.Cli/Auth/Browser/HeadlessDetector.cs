/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Auth.Browser;

/// <summary>
/// Whether a browser can be opened on this machine, and how.
/// </summary>
internal enum BrowserEnvironmentKind
{
    /// <summary>A desktop session; the browser is opened.</summary>
    Interactive,

    /// <summary>No browser on this machine; the link is printed for the user to open.</summary>
    Headless,

    /// <summary>WSL; the browser is opened on the Windows side.</summary>
    Wsl
}

/// <summary>
/// What <see cref="IHeadlessDetector"/> found.
/// </summary>
/// <param name="Kind">The kind of environment.</param>
/// <param name="Reason">Why it is headless ("SSH session"), for <c>--verbose</c>; otherwise <see langword="null"/>.</param>
internal sealed record BrowserEnvironment(BrowserEnvironmentKind Kind, string? Reason)
{
    public static BrowserEnvironment Interactive { get; } = new(BrowserEnvironmentKind.Interactive, null);

    public static BrowserEnvironment Wsl { get; } = new(BrowserEnvironmentKind.Wsl, null);

    public static BrowserEnvironment Headless(string reason) => new(BrowserEnvironmentKind.Headless, reason);

    /// <summary>
    /// Gets whether a browser is not opened.
    /// </summary>
    public bool IsHeadless => Kind == BrowserEnvironmentKind.Headless;
}

/// <summary>
/// Decides whether <c>login</c> tries to open a browser (cli-auth-cli-spec §5.2).
/// </summary>
internal interface IHeadlessDetector
{
    /// <summary>
    /// Inspects the environment of this process.
    /// </summary>
    BrowserEnvironment Detect();
}

/// <summary>
/// Reads the signals of cli-auth-cli-spec §5.2, in the table's order.
/// </summary>
/// <remarks>
/// Reads environment variables and two file paths only, through the injected providers, so every
/// row is testable on any machine.
/// </remarks>
internal sealed class HeadlessDetector(IEnvironmentVariableProvider environment, HostOs os, Func<string, bool> fileExists)
    : IHeadlessDetector
{
    public BrowserEnvironment Detect()
    {
        if (IsSet("WSL_DISTRO_NAME") || IsSet("WSL_INTEROP"))
            return BrowserEnvironment.Wsl;

        if (IsSet("SSH_CLIENT") || IsSet("SSH_TTY") || IsSet("SSH_CONNECTION"))
            return BrowserEnvironment.Headless("SSH session");

        if (os == HostOs.Linux && !IsSet("DISPLAY") && !IsSet("WAYLAND_DISPLAY"))
            return BrowserEnvironment.Headless("no display");

        if (fileExists("/.dockerenv")
            || fileExists("/run/.containerenv")
            || IsSet("REMOTE_CONTAINERS")
            || IsSet("CODESPACES")
            || IsSet("DEVCONTAINER"))
        {
            return BrowserEnvironment.Headless("container");
        }

        return BrowserEnvironment.Interactive;
    }

    private bool IsSet(string name) => !string.IsNullOrEmpty(environment.GetVariable(name));
}
