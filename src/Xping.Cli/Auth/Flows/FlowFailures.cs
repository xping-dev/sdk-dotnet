/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth.Flows;

/// <summary>
/// The failures both sign-in flows report the same way (cli-auth-cli-spec §4.8).
/// </summary>
internal static class FlowFailures
{
    /// <summary>
    /// The user declined in the browser.
    /// </summary>
    public static AuthFailureException Declined() =>
        new(AuthExitCodes.LoginDeclined, AuthErrorCodes.AccessDenied, "You declined the sign-in request. Nothing was stored.");

    /// <summary>
    /// The Portal refused a request with an error the flow has no message of its own for.
    /// </summary>
    public static AuthFailureException ServerRefused(string cloudUrl, OAuthError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        // The code is matched raw, against constants, and shown only after control characters are
        // dropped: whoever answered wrote it.
        string code = Printable(error.Error);
        string description = Printable(error.ErrorDescription);

        (int exitCode, string errorCode, string message) = error.Error switch
        {
            OAuthProtocol.InvalidClient => (
                AuthExitCodes.CloudUnreachable,
                AuthErrorCodes.CloudUnreachable,
                $"Xping Cloud did not recognise this CLI. Check `--cloud-url` ({cloudUrl})."),

            OAuthProtocol.InvalidRequest or OAuthProtocol.UnauthorizedClient
                or OAuthProtocol.UnsupportedGrantType or OAuthProtocol.InvalidScope => (
                AuthExitCodes.LoginFailed,
                AuthErrorCodes.OAuthError,
                description.Length > 0
                    ? $"Xping Cloud rejected the request ({code}): {description}. This is a CLI or server bug; please report it."
                    : $"Xping Cloud rejected the request ({code}). This is a CLI or server bug; please report it."),

            _ => (AuthExitCodes.LoginFailed, AuthErrorCodes.OAuthError, Returned(code, description))
        };

        return new AuthFailureException(exitCode, errorCode, message) { OAuthErrorCode = code };
    }

    /// <summary>
    /// The Portal returned an error this CLI shows as it came: the code and its description,
    /// without control characters.
    /// </summary>
    public static AuthFailureException Verbatim(string error, string? description) =>
        new(AuthExitCodes.LoginFailed, AuthErrorCodes.OAuthError, Returned(Printable(error), Printable(description)))
        {
            OAuthErrorCode = Printable(error)
        };

    /// <summary>
    /// Drops control characters, so text a server or a redirect wrote cannot drive the terminal.
    /// </summary>
    public static string Printable(string? text) =>
        text is null ? string.Empty : new string([.. text.Where(c => !char.IsControl(c))]).Trim();

    private static string Returned(string error, string description) =>
        description.Length > 0 ? $"Xping Cloud returned {error}: {description}" : $"Xping Cloud returned {error}.";
}
