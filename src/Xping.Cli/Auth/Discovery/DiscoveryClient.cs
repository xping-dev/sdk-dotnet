/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Cli.Auth.Discovery;

/// <summary>
/// Fetches, validates and caches the discovery document of a Cloud URL (cli-auth-cli-spec §2.3).
/// </summary>
internal sealed class DiscoveryClient(
    IHttpClientFactory httpClientFactory,
    IXpingSerializer serializer,
    DiscoveryCache cache,
    CliVersion cliVersion,
    ILogger<DiscoveryClient> logger)
{
    /// <summary>
    /// Returns the validated discovery document of <paramref name="cloudUrl"/>.
    /// </summary>
    /// <param name="cloudUrl">The normalized Cloud URL.</param>
    /// <param name="useCache">
    /// Whether a cached document may answer. <c>login</c> passes <see langword="false"/>, so a
    /// raised <c>xping_cli_min_version</c> stops an outdated CLI before it opens a browser.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The document.</returns>
    /// <exception cref="AuthFailureException">
    /// The server cannot be reached, or its document cannot be trusted or used.
    /// </exception>
    public async Task<DiscoveryDocument> GetAsync(string cloudUrl, bool useCache, CancellationToken cancellationToken)
    {
        if (useCache && cache.Read(cloudUrl) is { } cached)
        {
            // Validated again because the CLI may have changed since it was cached. A cached document
            // that no longer passes is not an answer; the server's current one is.
            try
            {
                return DiscoveryDocument.Validate(cloudUrl, cached, cliVersion);
            }
            catch (AuthFailureException)
            {
                logger.LogInformation("Cached discovery document for {CloudUrl} is no longer valid; fetching it again", cloudUrl);
            }
        }

        DiscoveryResponse fetched = await FetchAsync(cloudUrl, cancellationToken).ConfigureAwait(false);
        DiscoveryDocument document = DiscoveryDocument.Validate(cloudUrl, fetched, cliVersion);

        cache.Write(cloudUrl, fetched);
        return document;
    }

    private async Task<DiscoveryResponse> FetchAsync(string cloudUrl, CancellationToken cancellationToken)
    {
        var uri = new Uri(cloudUrl + "/.well-known/openid-configuration");
        HttpClient client = httpClientFactory.CreateClient(AuthHttpClients.OAuth);

        HttpStatusCode status;
        string body;
        try
        {
            using HttpResponseMessage response = await client.GetAsync(uri, cancellationToken).ConfigureAwait(false);
            status = response.StatusCode;
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (NetworkFailure.IsNetworkFailure(ex, cancellationToken))
        {
            throw AuthFailureException.Unreachable(cloudUrl, NetworkFailure.Describe(ex), ex);
        }

        logger.LogInformation("GET {Path} answered {Status}", uri.AbsolutePath, (int)status);

        // A redirect is not followed (the client refuses to) and is not a document either.
        if (status != HttpStatusCode.OK)
        {
            throw AuthFailureException.Unreachable(
                cloudUrl,
                string.Create(CultureInfo.InvariantCulture, $"discovery answered HTTP {(int)status}"));
        }

        try
        {
            return serializer.Deserialize<DiscoveryResponse>(body)
                ?? throw AuthFailureException.Unreachable(cloudUrl, "its discovery document is empty");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw AuthFailureException.Unreachable(cloudUrl, "its discovery document is not valid JSON", ex);
        }
    }
}
