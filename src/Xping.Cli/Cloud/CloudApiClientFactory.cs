/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net.Http.Headers;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Discovery;
using Xping.Cli.Auth.Http;
using Xping.Cli.Auth.Store;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Cli.Cloud;

/// <summary>
/// Builds a DataGateway client for one resolved credential (cli-auth-cli-spec §2.2, §9, §10).
/// </summary>
/// <remarks>
/// <para>
/// The pipeline is resilience → credential handler → the <see cref="CloudHttp.ClientName"/>
/// primary handler. The credential handler holds per-credential state (the token refresher, the
/// key), which the factory's pooled handlers must not, so it is composed here for each client
/// rather than registered on the named client.
/// </para>
/// <para>
/// Each call builds a new pipeline, so a client for a fallback credential (§8.1) cannot carry a
/// header from the one before it.
/// </para>
/// </remarks>
internal sealed class CloudApiClientFactory(
    IHttpMessageHandlerFactory handlerFactory,
    CredentialStoreSelector selector,
    DiscoveryClient discovery,
    OAuthClient oauth,
    CrossProcessLock processLock,
    IXpingSerializer serializer,
    CliVersion cliVersion,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory) : ICloudApiClientFactory
{
    /// <summary>
    /// Returns a client that reads with <paramref name="credential"/>.
    /// </summary>
    /// <param name="credential">A sign-in or an API key; the caller has already handled none.</param>
    /// <param name="cloudUrl">The normalized Cloud URL the credential belongs to.</param>
    /// <param name="cancellationToken">Cancels discovery for an API key.</param>
    /// <exception cref="ArgumentException"><paramref name="credential"/> is none.</exception>
    /// <exception cref="AuthFailureException">
    /// An API key's DataGateway could not be discovered: Cloud unreachable or incompatible.
    /// </exception>
    public async Task<ICloudApiClient> CreateAsync(ResolvedCredential credential, string cloudUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);

        if (credential is { Source: CredentialSource.StoredLogin, Login: { } login })
        {
            var refresher = new TokenRefresher(
                login,
                selector.Select(cloudUrl),
                discovery,
                oauth,
                processLock,
                timeProvider,
                loggerFactory.CreateLogger<TokenRefresher>());

            return Create(
                new BearerTokenHandler(refresher, loggerFactory.CreateLogger<BearerTokenHandler>()),
                login.Record.DataGatewayUri,
                refresher);
        }

        if (credential.ApiKey is { } apiKey)
        {
            // A sign-in records its DataGateway; a key learns it from discovery, cached for a day.
            DiscoveryDocument document = await discovery.GetAsync(cloudUrl, useCache: true, cancellationToken).ConfigureAwait(false);
            return Create(new ApiKeyHandler(apiKey.Value), document.DataGatewayUri, refresher: null);
        }

        throw new ArgumentException("There is no credential to read Xping Cloud with.", nameof(credential));
    }

    private CloudApiClient Create(DelegatingHandler credentialHandler, string gatewayUri, TokenRefresher? refresher)
    {
        // The factory's handler is pooled and ignores Dispose, so disposing the chain is safe.
        credentialHandler.InnerHandler = handlerFactory.CreateHandler(CloudHttp.ClientName);

#pragma warning disable CA2000 // Owned by the HttpClient, which the returned client disposes.
        var resilience = new ResilienceHandler(CloudResilience.Build(timeProvider)) { InnerHandler = credentialHandler };
#pragma warning restore CA2000

        var http = new HttpClient(resilience, disposeHandler: true) { Timeout = CloudHttp.TotalTimeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(cliVersion.UserAgent);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return new CloudApiClient(http, gatewayUri, refresher, serializer, loggerFactory.CreateLogger<CloudApiClient>());
    }
}
