/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth.Http;

/// <summary>
/// Sends an API key with every DataGateway request (cli-auth-cli-spec §9.1).
/// </summary>
/// <remarks>
/// No refresh and no retry on 401: a refused key stays refused. The CLI does not check the key's
/// scope or plan before sending it; the DataGateway's answer says.
/// </remarks>
internal sealed class ApiKeyHandler : DelegatingHandler
{
    private readonly string _apiKey;

    public ApiKeyHandler(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _apiKey = apiKey;
        Redaction.AddSecret(apiKey);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Exactly one credential header (contract §7.1), whatever was set before this handler.
        request.Headers.Authorization = null;
        request.Headers.Remove(CredentialHeaders.ApiKey);
        request.Headers.Add(CredentialHeaders.ApiKey, _apiKey);

        return base.SendAsync(request, cancellationToken);
    }
}
