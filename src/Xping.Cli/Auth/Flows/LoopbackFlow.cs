/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;
using Microsoft.Extensions.Logging;
using Xping.Cli.Auth.Browser;
using Xping.Cli.Auth.Discovery;
using Xping.Cli.Auth.Loopback;
using Xping.Cli.Auth.Store;

namespace Xping.Cli.Auth.Flows;

/// <summary>
/// Shows the authorization link to the user before the browser is opened.
/// </summary>
/// <param name="authorizationUrl">The link; printed exactly once, always (contract §3.1 step 4).</param>
/// <param name="environment">Whether a browser will be opened, which changes the wording.</param>
/// <param name="timeout">How long the flow waits for the browser.</param>
internal delegate void ShowAuthorizationLink(Uri authorizationUrl, BrowserEnvironment environment, TimeSpan timeout);

/// <summary>
/// One sign-in through the browser with PKCE and a loopback redirect (cli-auth-cli-spec §4).
/// </summary>
/// <remarks>
/// Discovery is fetched fresh, never from the cache, so a raised minimum CLI version stops an
/// outdated CLI before it opens a browser (§2.3). The verifier and <c>state</c> live in memory for
/// the length of this call only.
/// </remarks>
internal sealed class LoopbackFlow(
    DiscoveryClient discovery,
    OAuthClient oauth,
    IBrowserLauncher browser,
    IHeadlessDetector headless,
    TimeProvider timeProvider,
    ILogger<LoopbackFlow> logger)
{
    /// <summary>
    /// How long the flow waits for the browser, from the moment the link is shown (contract §3.1).
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Runs the flow and returns the record to store.
    /// </summary>
    /// <param name="cloudUrl">The normalized Cloud URL.</param>
    /// <param name="showLink">Prints the link and the waiting line.</param>
    /// <param name="cancellationToken">Ctrl+C.</param>
    /// <exception cref="AuthFailureException">The sign-in failed; the exit code and message are on it.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> fired.</exception>
    public async Task<CredentialRecord> RunAsync(string cloudUrl, ShowAuthorizationLink showLink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(showLink);

        DiscoveryDocument document = await discovery.GetAsync(cloudUrl, useCache: false, cancellationToken).ConfigureAwait(false);
        BrowserEnvironment environment = headless.Detect();
        if (environment.Reason is { } reason)
            logger.LogInformation("Not opening a browser: {Reason}", reason);

        using Pkce pkce = Pkce.Create();

        string redirectUri;
        CallbackResult callback;

        LoopbackListener listener = LoopbackListener.Start(logger);
        await using (listener.ConfigureAwait(false))
        {
            redirectUri = listener.RedirectUri;
            Uri authorizationUrl = AuthorizationUrl(document.AuthorizationEndpoint, pkce, redirectUri);

            // Started before the link is shown: the five minutes are the user's, counted from the
            // moment they can act.
            using var timeout = new CancellationTokenSource(Timeout, timeProvider);
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

            showLink(authorizationUrl, environment, Timeout);

            if (!environment.IsHeadless)
                browser.TryOpen(authorizationUrl, environment);

            try
            {
                callback = await listener.WaitForCallbackAsync(pkce, wait.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new AuthFailureException(
                    AuthExitCodes.LoginTimedOut,
                    AuthErrorCodes.Timeout,
                    "No sign-in arrived within 5 minutes. Run `xping login` again, or `xping login --device` " +
                    "if your browser is on another machine.");
            }
        }

        string code = callback switch
        {
            CallbackResult.Code success => success.Value,
            CallbackResult.Error error => throw Declined(error),
            _ => throw new InvalidOperationException($"Unknown callback result {callback.GetType().Name}.")
        };

        TokenResponse tokens;
        try
        {
            tokens = await oauth.ExchangeCodeAsync(document, code, redirectUri, pkce.CodeVerifier, cancellationToken).ConfigureAwait(false);
        }
        catch (OAuthException ex)
        {
            throw ExchangeFailed(cloudUrl, ex.Error);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        AccessTokenClaims claims = AccessTokenClaims.Read(tokens.AccessToken);

        return new CredentialRecord(
            CredentialRecord.CurrentSchemaVersion,
            cloudUrl,
            tokens.RefreshToken,
            tokens.AccessToken,

            // Receipt time plus expires_in, not the token's exp: the machine's clock skew then applies
            // to both ends of the comparison (§7.2).
            now + tokens.ExpiresIn,
            claims.WorkspaceId,
            claims.Sid,
            claims.Sub,
            claims.Email,
            document.DataGatewayUri,
            now);
    }

    /// <summary>
    /// Builds the authorization request of contract §4.2 on the server's endpoint.
    /// </summary>
    internal static Uri AuthorizationUrl(Uri endpoint, Pkce pkce, string redirectUri)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(pkce);

        KeyValuePair<string, string>[] parameters =
        [
            new("response_type", "code"),
            new("client_id", OAuthProtocol.ClientId),
            new("redirect_uri", redirectUri),
            new("scope", OAuthProtocol.Scope),
            new("state", pkce.State),
            new("code_challenge", pkce.CodeChallenge),
            new("code_challenge_method", Pkce.ChallengeMethod)
        ];

        var query = new StringBuilder(endpoint.Query.TrimStart('?'));
        foreach ((string name, string value) in parameters)
        {
            if (query.Length > 0)
                query.Append('&');

            query.Append(Uri.EscapeDataString(name)).Append('=').Append(Uri.EscapeDataString(value));
        }

        return new UriBuilder(endpoint) { Query = query.ToString() }.Uri;
    }

    private static AuthFailureException Declined(CallbackResult.Error error)
    {
        if (string.Equals(error.Value, OAuthProtocol.AccessDenied, StringComparison.Ordinal))
        {
            return new AuthFailureException(
                AuthExitCodes.LoginDeclined,
                AuthErrorCodes.AccessDenied,
                "You declined the sign-in request. Nothing was stored.");
        }

        // The description arrived through the browser, so anything could have written it; control
        // characters are dropped before it reaches the terminal.
        string description = Printable(error.Description);
        return new AuthFailureException(
            AuthExitCodes.LoginFailed,
            AuthErrorCodes.OAuthError,
            description.Length > 0
                ? $"Xping Cloud returned {Printable(error.Value)}: {description}"
                : $"Xping Cloud returned {Printable(error.Value)}.")
        {
            OAuthErrorCode = Printable(error.Value)
        };
    }

    private static AuthFailureException ExchangeFailed(string cloudUrl, OAuthError error)
    {
        string description = Printable(error.ErrorDescription);

        (int exitCode, string errorCode, string message) = error.Error switch
        {
            OAuthProtocol.InvalidGrant => (
                AuthExitCodes.LoginFailed,
                AuthErrorCodes.OAuthError,
                "The sign-in code was rejected (expired or already used). Run `xping login` again."),

            OAuthProtocol.InvalidClient => (
                AuthExitCodes.CloudUnreachable,
                AuthErrorCodes.CloudUnreachable,
                $"Xping Cloud did not recognise this CLI. Check `--cloud-url` ({cloudUrl})."),

            OAuthProtocol.InvalidRequest or OAuthProtocol.UnauthorizedClient
                or OAuthProtocol.UnsupportedGrantType or OAuthProtocol.InvalidScope => (
                AuthExitCodes.LoginFailed,
                AuthErrorCodes.OAuthError,
                description.Length > 0
                    ? $"Xping Cloud rejected the request ({error.Error}): {description}. This is a CLI or server bug; please report it."
                    : $"Xping Cloud rejected the request ({error.Error}). This is a CLI or server bug; please report it."),

            _ => (
                AuthExitCodes.LoginFailed,
                AuthErrorCodes.OAuthError,
                description.Length > 0 ? $"Xping Cloud returned {error.Error}: {description}" : $"Xping Cloud returned {error.Error}.")
        };

        return new AuthFailureException(exitCode, errorCode, message) { OAuthErrorCode = error.Error };
    }

    private static string Printable(string? text) =>
        text is null ? string.Empty : new string([.. text.Where(c => !char.IsControl(c))]).Trim();
}
