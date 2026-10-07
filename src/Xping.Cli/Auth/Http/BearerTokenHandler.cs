/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Xping.Cli.Auth.Http;

/// <summary>
/// Sends a stored sign-in's access token with every DataGateway request (cli-auth-cli-spec §9.1).
/// </summary>
/// <remarks>
/// Sits inside the resilience handler, so every attempt carries a current token and the one
/// refresh-and-retry on 401 here is not counted as a resilience retry (§10.3).
/// </remarks>
internal sealed partial class BearerTokenHandler : DelegatingHandler
{
    /// <summary>
    /// Set on the request sent again after a refresh, so the caller can tell a second 401, which
    /// ends the sign-in, from a first one that never got a refresh (contract §8.2).
    /// </summary>
    public static readonly HttpRequestOptionsKey<bool> RetriedAfterRefresh = new("Xping.RetriedAfterRefresh");

    private readonly TokenRefresher _refresher;
    private readonly Uri _gateway;
    private readonly ILogger<BearerTokenHandler> _logger;

    public BearerTokenHandler(TokenRefresher refresher, ILogger<BearerTokenHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(refresher);

        _refresher = refresher;
        _logger = logger;

        // The trailing slash makes the path prefix check whole segments: /gw must not admit /gwx.
        _gateway = new Uri(refresher.DataGatewayUri + "/", UriKind.Absolute);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The token goes to the DataGateway and nowhere else (contract §2, §10.2).
        if (!IsUnderGateway(request.RequestUri))
            throw new InvalidOperationException($"An access token is only sent to the DataGateway at {_gateway}.");

        string token = await _refresher.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        Authorize(request, token);

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized || !IsInvalidToken(response))
            return response;

        // The header's error_description is the server's text; only the error code is logged.
        _logger.LogInformation("The DataGateway answered 401 invalid_token; refreshing once and retrying");
        response.Dispose();

        string fresh = await _refresher.ForceRefreshAsync(token, cancellationToken).ConfigureAwait(false);

        // A second 401 goes back to the caller unchanged; CloudApiClient ends the sign-in on it.
#pragma warning disable CA2000 // The response returned to the caller holds the retry as its RequestMessage.
        HttpRequestMessage retry = Clone(request);
#pragma warning restore CA2000
        Authorize(retry, fresh);
        retry.Options.Set(RetriedAfterRefresh, true);
        return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    private bool IsUnderGateway(Uri? uri) =>
        uri is { IsAbsoluteUri: true }
        && string.Equals(uri.Scheme, _gateway.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(uri.Host, _gateway.Host, StringComparison.OrdinalIgnoreCase)
        && uri.Port == _gateway.Port
        && uri.AbsolutePath.StartsWith(_gateway.AbsolutePath, StringComparison.Ordinal);

    private static void Authorize(HttpRequestMessage request, string token)
    {
        request.Headers.Remove(CredentialHeaders.ApiKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    // Contract §7.3: refresh only on invalid_token. MissingCredentials carries no error parameter,
    // and insufficient_scope comes with a 403.
    private static bool IsInvalidToken(HttpResponseMessage response) =>
        response.Headers.WwwAuthenticate.Any(challenge =>
            string.Equals(challenge.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            && challenge.Parameter is { } parameter
            && InvalidTokenParameter().IsMatch(parameter));

    // Reads are bodiless GETs, so method, URI, version and headers are the whole request.
    private static HttpRequestMessage Clone(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        foreach (KeyValuePair<string, object?> option in request.Options)
            ((IDictionary<string, object?>)clone.Options)[option.Key] = option.Value;

        return clone;
    }

    // "error_description=" must not match: the underscore is a word character, so \b fails there.
    [GeneratedRegex(@"\berror\s*=\s*""?invalid_token\b", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidTokenParameter();
}
