/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Xping.Cli.Auth.Discovery;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Cli.Auth;

/// <summary>
/// The token, device authorization and revocation requests of the CLI (cli-auth-cli-spec §2.4).
/// </summary>
/// <remarks>
/// <para>
/// Every request is a form <c>POST</c> with <c>client_id=xping-cli</c> and no secret. The retries of
/// contract §8.1 live here and nowhere else: <c>server_error</c> and network failures once after
/// 2 s, <c>temporarily_unavailable</c> after 2, 4 and 8 s. Device polling never retries on its own,
/// because the next poll is the retry and it must not come before the interval (contract §3.2).
/// </para>
/// <para>
/// Bodies are never logged, in either direction: they carry codes and tokens.
/// </para>
/// </remarks>
internal sealed class OAuthClient(
    IHttpClientFactory httpClientFactory,
    IXpingSerializer serializer,
    TimeProvider timeProvider,
    ILogger<OAuthClient> logger)
{
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)];

    /// <summary>
    /// Exchanges an authorization code from the loopback redirect for tokens.
    /// </summary>
    /// <param name="discovery">The server's endpoints.</param>
    /// <param name="code">The code from the redirect.</param>
    /// <param name="redirectUri">The exact <c>redirect_uri</c> the authorization request used.</param>
    /// <param name="codeVerifier">The PKCE verifier whose challenge the authorization request sent.</param>
    /// <param name="cancellationToken">Cancels the request and any retry wait.</param>
    /// <returns>The tokens.</returns>
    /// <exception cref="OAuthException">The server refused the exchange.</exception>
    /// <exception cref="AuthFailureException">The server could not be reached or answered nonsense.</exception>
    public async Task<TokenResponse> ExchangeCodeAsync(
        DiscoveryDocument discovery,
        string code,
        string redirectUri,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        Redaction.AddSecret(code);
        Redaction.AddSecret(codeVerifier);

        // A code works once. After a network failure the server may already have redeemed it, and a
        // retry would answer invalid_grant and hide the failure that actually happened. A 5xx answer
        // means the server did not redeem it, so that is retried.
        Outcome outcome = await PostAsync(
            discovery.TokenEndpoint,
            [
                new("grant_type", OAuthProtocol.AuthorizationCodeGrant),
                new("code", code),
                new("redirect_uri", redirectUri),
                new("code_verifier", codeVerifier)
            ],
            Retry.ServerAnswersOnly,
            cancellationToken).ConfigureAwait(false);

        return ReadToken(discovery.CloudUrl, outcome);
    }

    /// <summary>
    /// Trades a refresh token for a new pair. The presented token stops working after the server's
    /// reuse leeway (contract §6.6).
    /// </summary>
    /// <remarks>No <c>scope</c> is sent: the refreshed pair keeps the scope of the session.</remarks>
    /// <param name="discovery">The server's endpoints.</param>
    /// <param name="refreshToken">The current refresh token.</param>
    /// <param name="cancellationToken">Cancels the request and any retry wait.</param>
    /// <returns>The new pair.</returns>
    /// <exception cref="OAuthException">
    /// The server refused; <c>invalid_grant</c> means the session is over.
    /// </exception>
    /// <exception cref="AuthFailureException">The server could not be reached or answered nonsense.</exception>
    public async Task<TokenResponse> RefreshAsync(
        DiscoveryDocument discovery,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        Redaction.AddSecret(refreshToken);

        // Retried after a network failure too: a rotated token stays valid for the server's 30 s reuse
        // leeway (contract §6.6), far longer than the 2 s wait.
        Outcome outcome = await PostAsync(
            discovery.TokenEndpoint,
            [
                new("grant_type", OAuthProtocol.RefreshTokenGrant),
                new("refresh_token", refreshToken)
            ],
            Retry.Always,
            cancellationToken).ConfigureAwait(false);

        return ReadToken(discovery.CloudUrl, outcome);
    }

    /// <summary>
    /// Starts a device flow (contract §4.5).
    /// </summary>
    /// <param name="discovery">The server's endpoints.</param>
    /// <param name="workspaceId">A workspace to preselect, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the request and any retry wait.</param>
    /// <returns>The codes and where the user enters them.</returns>
    /// <exception cref="OAuthException">The server refused.</exception>
    /// <exception cref="AuthFailureException">The server could not be reached or answered nonsense.</exception>
    public async Task<DeviceAuthorization> StartDeviceAsync(
        DiscoveryDocument discovery,
        string? workspaceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(discovery);

        List<KeyValuePair<string, string>> form = [new("scope", OAuthProtocol.Scope)];
        if (!string.IsNullOrWhiteSpace(workspaceId))
            form.Add(new("xping_workspace_id", workspaceId));

        Outcome outcome = await PostAsync(discovery.DeviceAuthorizationEndpoint, form, Retry.Always, cancellationToken)
            .ConfigureAwait(false);

        string body = SuccessBody(discovery.CloudUrl, "device authorization", outcome);
        DeviceResponseBody? response = TryDeserialize<DeviceResponseBody>(body);

        // The user signs in on these pages, so they meet the rule every server URL meets. The user
        // code is printed as sent, so a control character in it, which nobody could type anyway,
        // must not reach the terminal.
        if (response is not { DeviceCode.Length: > 0, UserCode.Length: > 0, ExpiresIn: > 0 }
            || response.UserCode.Any(char.IsControl)
            || !ServerUri.TryParse(response.VerificationUri, out Uri? verificationUri))
        {
            throw Incomplete(discovery.CloudUrl, "device authorization");
        }

        Redaction.AddSecret(response.DeviceCode);

        // An unusable complete URI is dropped rather than fatal: it is a convenience (contract §3.2).
        _ = ServerUri.TryParse(response.VerificationUriComplete, out Uri? complete);

        return new DeviceAuthorization(
            response.DeviceCode,
            response.UserCode,
            verificationUri,
            complete,
            TimeSpan.FromSeconds(response.ExpiresIn.Value),
            response.Interval is > 0 ? TimeSpan.FromSeconds(response.Interval.Value) : DeviceAuthorization.DefaultInterval);
    }

    /// <summary>
    /// Polls the token endpoint once for a device flow's tokens.
    /// </summary>
    /// <param name="discovery">The server's endpoints.</param>
    /// <param name="deviceCode">The device code from <see cref="StartDeviceAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Tokens, or what to do before the next poll.</returns>
    /// <exception cref="OAuthException">
    /// The flow is over: <c>access_denied</c>, <c>expired_token</c>, <c>invalid_grant</c> or an error
    /// this CLI does not know.
    /// </exception>
    /// <exception cref="AuthFailureException">The server answered nonsense.</exception>
    public async Task<DevicePollResult> PollDeviceAsync(
        DiscoveryDocument discovery,
        string deviceCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(discovery);

        Outcome outcome = await PostAsync(
            discovery.TokenEndpoint,
            [
                new("grant_type", OAuthProtocol.DeviceCodeGrant),
                new("device_code", deviceCode)
            ],
            Retry.Never,
            cancellationToken).ConfigureAwait(false);

        return outcome switch
        {
            Outcome.Transient transient => DevicePollResult.Transient(transient.Reason),

            // A rate limiter in front of the Portal: the next interval is the back-off.
            Outcome.Unexpected { StatusCode: 429 } => DevicePollResult.Transient("rate limited"),
            Outcome.Failed { Error.Error: OAuthProtocol.AuthorizationPending } => DevicePollResult.Pending,
            Outcome.Failed { Error.Error: OAuthProtocol.SlowDown } => DevicePollResult.SlowDown,
            _ => DevicePollResult.Completed(ReadToken(discovery.CloudUrl, outcome))
        };
    }

    /// <summary>
    /// Revokes the CLI session that <paramref name="refreshToken"/> belongs to (contract §4.4).
    /// </summary>
    /// <remarks>
    /// The body of the answer is ignored: it may be empty or <c>{}</c> (contract OQ-19).
    /// </remarks>
    /// <param name="discovery">The server's endpoints.</param>
    /// <param name="refreshToken">The stored refresh token.</param>
    /// <param name="cancellationToken">Cancels the request and any retry wait.</param>
    /// <returns>A task that completes when the server confirmed.</returns>
    /// <exception cref="OAuthException">The server refused.</exception>
    /// <exception cref="AuthFailureException">The server could not be reached or answered nonsense.</exception>
    public async Task RevokeAsync(DiscoveryDocument discovery, string refreshToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        Redaction.AddSecret(refreshToken);

        Outcome outcome = await PostAsync(
            discovery.RevocationEndpoint,
            [
                new("token", refreshToken),
                new("token_type_hint", "refresh_token")
            ],
            Retry.Always,
            cancellationToken).ConfigureAwait(false);

        _ = SuccessBody(discovery.CloudUrl, "revocation", outcome);
    }

    private async Task<Outcome> PostAsync(
        Uri endpoint,
        IEnumerable<KeyValuePair<string, string>> parameters,
        Retry retry,
        CancellationToken cancellationToken)
    {
        KeyValuePair<string, string>[] form = [new("client_id", OAuthProtocol.ClientId), .. parameters];

        for (int attempt = 0; ; attempt++)
        {
            Outcome outcome = await SendOnceAsync(endpoint, form, cancellationToken).ConfigureAwait(false);

            if (retry == Retry.Never
                || outcome is not Outcome.Transient transient
                || attempt >= transient.Retries
                || (transient.NetworkFailure && retry == Retry.ServerAnswersOnly))
            {
                return outcome;
            }

            TimeSpan delay = Backoff[attempt];
            logger.LogInformation(
                "POST {Path} failed ({Reason}); retrying in {Seconds} s",
                endpoint.AbsolutePath,
                transient.Reason,
                delay.TotalSeconds);

            await Task.Delay(delay, timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Outcome> SendOnceAsync(Uri endpoint, KeyValuePair<string, string>[] form, CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient(AuthHttpClients.OAuth);

        HttpStatusCode status;
        string body;
        try
        {
            using var content = new FormUrlEncodedContent(form);
            using HttpResponseMessage response = await client.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
            status = response.StatusCode;
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (NetworkFailure.IsNetworkFailure(ex, cancellationToken))
        {
            logger.LogInformation("POST {Path} failed: {Reason}", endpoint.AbsolutePath, NetworkFailure.Describe(ex));
            return new Outcome.Transient(NetworkFailure.Describe(ex), Retries: 1, NetworkFailure: true);
        }

        int code = (int)status;
        logger.LogInformation("POST {Path} answered {Status}", endpoint.AbsolutePath, code);

        if (code is >= 200 and < 300)
            return new Outcome.Succeeded(body);

        // The error code decides first, the status only when there is no usable body: the contract
        // gives each error its status, but a proxy in between may not.
        ErrorBody? error = code >= 400 ? TryDeserialize<ErrorBody>(body) : null;

        return error?.Error switch
        {
            OAuthProtocol.ServerError => new Outcome.Transient("server error", Retries: 1),
            OAuthProtocol.TemporarilyUnavailable => new Outcome.Transient("temporarily unavailable", Retries: 3),
            { Length: > 0 } name => new Outcome.Failed(new OAuthError(name, error.ErrorDescription, code)),
            _ when status == HttpStatusCode.ServiceUnavailable => new Outcome.Transient("temporarily unavailable", Retries: 3),
            _ when code >= 500 => new Outcome.Transient("server error", Retries: 1),

            // A redirect lands here too: it is never followed and never a success (§15.1).
            _ => new Outcome.Unexpected(code)
        };
    }

    private TokenResponse ReadToken(string cloudUrl, Outcome outcome)
    {
        string body = SuccessBody(cloudUrl, "token", outcome);
        TokenResponseBody? token = TryDeserialize<TokenResponseBody>(body);

        if (token is not { AccessToken.Length: > 0, RefreshToken.Length: > 0, ExpiresIn: > 0 }
            || !string.Equals(token.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            throw Incomplete(cloudUrl, "token");
        }

        Redaction.AddSecret(token.AccessToken);
        Redaction.AddSecret(token.RefreshToken);

        return new TokenResponse(token.AccessToken, TimeSpan.FromSeconds(token.ExpiresIn.Value), token.RefreshToken, token.Scope);
    }

    private static string SuccessBody(string cloudUrl, string endpointName, Outcome outcome) =>
        outcome switch
        {
            Outcome.Succeeded succeeded => succeeded.Body,
            Outcome.Failed failed => throw new OAuthException(failed.Error),
            Outcome.Transient transient => throw AuthFailureException.Unreachable(cloudUrl, transient.Reason),
            Outcome.Unexpected unexpected => throw AuthFailureException.Unreachable(
                cloudUrl,
                string.Create(CultureInfo.InvariantCulture, $"the {endpointName} endpoint answered HTTP {unexpected.StatusCode}")),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };

    private static AuthFailureException Incomplete(string cloudUrl, string endpointName) =>
        AuthFailureException.Unreachable(cloudUrl, $"the {endpointName} endpoint answered with an incomplete response");

    private T? TryDeserialize<T>(string body)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            return serializer.Deserialize<T>(body);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// How one request ended, before the caller decides what that means for its grant.
    /// </summary>
    private abstract record Outcome
    {
        internal sealed record Succeeded(string Body) : Outcome;

        internal sealed record Failed(OAuthError Error) : Outcome;

        /// <summary>
        /// A failure that may pass; <paramref name="Retries"/> is how often to try again, and
        /// <paramref name="NetworkFailure"/> says no answer arrived at all.
        /// </summary>
        internal sealed record Transient(string Reason, int Retries, bool NetworkFailure = false) : Outcome;

        internal sealed record Unexpected(int StatusCode) : Outcome;
    }

    /// <summary>
    /// Which transient failures a request is retried after.
    /// </summary>
    private enum Retry
    {
        /// <summary>None: the caller's next attempt is the retry.</summary>
        Never,

        /// <summary>Transient answers from the server, but not a request that got no answer.</summary>
        ServerAnswersOnly,

        /// <summary>Every transient failure.</summary>
        Always
    }

    private sealed record TokenResponseBody(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("scope")] string? Scope);

    private sealed record ErrorBody(
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);

    private sealed record DeviceResponseBody(
        [property: JsonPropertyName("device_code")] string? DeviceCode,
        [property: JsonPropertyName("user_code")] string? UserCode,
        [property: JsonPropertyName("verification_uri")] string? VerificationUri,
        [property: JsonPropertyName("verification_uri_complete")] string? VerificationUriComplete,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn,
        [property: JsonPropertyName("interval")] int? Interval);
}
