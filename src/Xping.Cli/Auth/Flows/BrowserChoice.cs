/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Xping.Cli.Auth.Browser;

namespace Xping.Cli.Auth.Flows;

/// <summary>
/// Whether a sign-in flow opens a browser, and if not, why not; the wording around the link
/// depends on it (cli-auth-cli-spec §3.2).
/// </summary>
internal enum LinkMode
{
    /// <summary>A browser is opened on the link.</summary>
    Browser,

    /// <summary>The machine has no browser the CLI can open.</summary>
    Headless,

    /// <summary><c>--no-browser</c> was given.</summary>
    NoBrowser
}

/// <summary>
/// The one decision both flows make before showing a link: open a browser or not (§5.2, §6.2).
/// </summary>
/// <param name="Mode">What the user is told.</param>
/// <param name="Environment">The detected environment, for <see cref="LinkMode.Browser"/> only.</param>
internal sealed record BrowserChoice(LinkMode Mode, BrowserEnvironment? Environment)
{
    /// <summary>
    /// Decides; the detection runs only when <c>--no-browser</c> was not given.
    /// </summary>
    public static BrowserChoice Make(IHeadlessDetector headless, bool noBrowser, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(headless);

        if (noBrowser)
            return new BrowserChoice(LinkMode.NoBrowser, null);

        BrowserEnvironment environment = headless.Detect();
        if (!environment.IsHeadless)
            return new BrowserChoice(LinkMode.Browser, environment);

        logger.LogInformation("Not opening a browser: {Reason}", environment.Reason);
        return new BrowserChoice(LinkMode.Headless, null);
    }

    /// <summary>
    /// Opens <paramref name="url"/> when the choice was to open a browser. Failing to open changes
    /// nothing: the link is on the screen either way.
    /// </summary>
    public void Open(IBrowserLauncher browser, Uri url)
    {
        ArgumentNullException.ThrowIfNull(browser);

        if (Environment is { } environment)
            browser.TryOpen(url, environment);
    }
}
