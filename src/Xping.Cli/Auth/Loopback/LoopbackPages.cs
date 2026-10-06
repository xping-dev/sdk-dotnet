/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using System.Text.RegularExpressions;

namespace Xping.Cli.Auth.Loopback;

/// <summary>
/// The pages the browser shows after the redirect to the CLI (cli-auth-cli-spec §4.5).
/// </summary>
/// <remarks>
/// Static HTML with no script and no external resource. Nothing from the request is echoed except
/// an OAuth <c>error</c> code, and only when it has the shape of one, so a crafted redirect cannot
/// put text of its choosing on a page served from the user's own machine. The code, the
/// <c>state</c>, the redirect URI and the Cloud URL never appear.
/// </remarks>
internal static partial class LoopbackPages
{
    private const string Style =
        "body{font-family:system-ui,-apple-system,Segoe UI,sans-serif;max-width:32rem;margin:4rem auto;padding:0 1rem;line-height:1.5;color:#1f2328}" +
        "h1{font-size:1.4rem}code{background:#f3f4f6;padding:.1rem .3rem;border-radius:4px}";

    /// <summary>
    /// The page after a successful redirect.
    /// </summary>
    public const string Success =
        "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Signed in to Xping</title>" +
        "<style>" + Style + "</style></head><body><h1>Signed in to Xping</h1>" +
        "<p>You can close this tab and return to your terminal.</p></body></html>";

    /// <summary>
    /// Returns the page for a redirect that did not complete the sign-in.
    /// </summary>
    /// <param name="error">The OAuth <c>error</c> code from the redirect, or <see langword="null"/>.</param>
    public static string Error(string? error)
    {
        string detail = error is not null && ErrorCodeShape().IsMatch(error)
            ? $"<p>Xping Cloud returned <code>{WebUtility.HtmlEncode(error)}</code>.</p>"
            : string.Empty;

        return
            "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Sign-in could not be completed</title>" +
            "<style>" + Style + "</style></head><body><h1>Sign-in could not be completed</h1>" + detail +
            "<p>Return to your terminal. If the problem continues, run <code>xping login</code> again.</p></body></html>";
    }

    // The catalog's codes are lowercase words joined by underscores (RFC 6749 §4.1.2.1).
    [GeneratedRegex("^[a-z][a-z_]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex ErrorCodeShape();
}
