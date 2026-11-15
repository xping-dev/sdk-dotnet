/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly.Timeout;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Http;
using Xping.Cli.Cloud.Digest;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Cli.Cloud;

/// <summary>
/// Reads from the DataGateway and maps its answers (cli-auth-cli-spec §9.7, cloud-data-in-cli-cli-spec §2.2).
/// </summary>
/// <remarks>
/// Built by <see cref="CloudApiClientFactory"/> around a pipeline that already carries the
/// credential and the retries, so what reaches the mapping here is the final answer. Status decides
/// first and <c>title</c> second (contract §8.2), so an unknown title on a known status still gets
/// sensible behaviour.
/// </remarks>
internal sealed class CloudApiClient : ICloudApiClient
{
    private const string AmbiguousCredentials = "Error.Authentication.AmbiguousCredentials";
    private const string MissingCredentials = "Error.Authentication.MissingCredentials";
    private const string ApiKeyInsufficientScope = "Error.ApiKey.InsufficientScope";
    private const string ApiKeyFeatureNotAvailable = "Error.ApiKey.FeatureNotAvailable";

    private readonly HttpClient _http;
    private readonly string _gatewayUri;
    private readonly TokenRefresher? _refresher;
    private readonly IXpingSerializer _serializer;
    private readonly ILogger<CloudApiClient> _logger;

    /// <param name="http">The pipeline for one credential; owned and disposed by this client.</param>
    /// <param name="gatewayUri">The DataGateway base URI, without a trailing slash.</param>
    /// <param name="refresher">
    /// The sign-in behind the pipeline, or <see langword="null"/> for an API key; owned and disposed
    /// by this client.
    /// </param>
    /// <param name="serializer">Reads the answers.</param>
    /// <param name="logger">Logs statuses, never bodies.</param>
    public CloudApiClient(
        HttpClient http,
        string gatewayUri,
        TokenRefresher? refresher,
        IXpingSerializer serializer,
        ILogger<CloudApiClient> logger)
    {
        _http = http;
        _gatewayUri = gatewayUri;
        _refresher = refresher;
        _serializer = serializer;
        _logger = logger;
    }

    public IReadOnlyList<string> Warnings => _refresher?.Warnings ?? [];

    public async Task<PagedResult<ProjectSummary>> ListProjectsAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        string route = string.Create(CultureInfo.InvariantCulture, $"projects?pageNumber={pageNumber}&pageSize={pageSize}");
        PagedResult<ProjectSummary>? page = await GetAsync<PagedResult<ProjectSummary>>(route, cancellationToken).ConfigureAwait(false);

        // The route always exists; a 404 here is not "no projects".
        if (page is null)
            throw new CloudApiException(404, null, "The DataGateway has no project list at this address.");

        // Deserialization leaves a missing list, or a null element in it, null.
        if (page.Items is null)
            throw Incomplete("project list");

        return page with { Items = [.. page.Items.Where(p => p is { Id.Length: > 0 })] };
    }

    public async Task<DigestResponse?> GetDigestAsync(string projectKey, DigestRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectKey);
        ArgumentNullException.ThrowIfNull(request);

        string? body = await SendAsync(
            HttpMethod.Post,
            $"projects/{Uri.EscapeDataString(projectKey)}/tests/digest",
            _serializer.Serialize(request),
            cancellationToken).ConfigureAwait(false);

        if (body is null)
            return null;

        // Only a body without a test list is unreadable. An item the CLI cannot read costs that test,
        // not its batch (contract §8.2 F4), so CloudEnricher sorts the items.
        return Read<DigestResponse>(body) is { Tests: not null } digest
            ? digest
            : throw new CloudResponseException("The DataGateway answered the digest with a body the CLI cannot read.");
    }

    public async Task<HealthResponse?> GetHealthAsync(
        string projectKey, IReadOnlyList<string> sessionKeys, string? cursor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectKey);
        ArgumentNullException.ThrowIfNull(sessionKeys);

        var route = new StringBuilder(string.Create(
            CultureInfo.InvariantCulture,
            $"projects/{Uri.EscapeDataString(projectKey)}/tests/health?pageSize={CloudEnricher.MaxHealthPageSize}"));

        if (cursor is not null)
            route.Append("&cursor=").Append(Uri.EscapeDataString(cursor));

        foreach (string sessionKey in sessionKeys)
            route.Append("&sessionKeys=").Append(Uri.EscapeDataString(sessionKey));

        string? body = await SendAsync(HttpMethod.Get, route.ToString(), content: null, cancellationToken).ConfigureAwait(false);
        if (body is null)
            return null;

        // As the digest: only a body without a test list is unreadable.
        return Read<HealthResponse>(body) is { Tests: not null } health
            ? health
            : throw new CloudResponseException("The DataGateway answered the suite health with a body the CLI cannot read.");
    }

    public async Task<RunsResponse?> GetRunsAsync(string projectKey, string fingerprint, int limit, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectKey);
        ArgumentException.ThrowIfNullOrEmpty(fingerprint);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        string? body = await SendAsync(
            HttpMethod.Get,
            string.Create(
                CultureInfo.InvariantCulture,
                $"projects/{Uri.EscapeDataString(projectKey)}/tests/{Uri.EscapeDataString(fingerprint)}/runs?limit={limit}"),
            content: null,
            cancellationToken).ConfigureAwait(false);

        if (body is null)
            return null;

        return Read<RunsResponse>(body) is { Runs: { } runs } history && runs.All(r => r is not null)
            ? history
            : throw new CloudResponseException("The DataGateway answered the run history with a body the CLI cannot read.");
    }

    public void Dispose()
    {
        _http.Dispose();
        _refresher?.Dispose();
    }

    // Null only for 404; any other failure throws.
    private async Task<T?> GetAsync<T>(string route, CancellationToken cancellationToken)
        where T : class
    {
        string? body = await SendAsync(HttpMethod.Get, route, content: null, cancellationToken).ConfigureAwait(false);
        return body is null ? null : Read<T>(body) ?? throw Incomplete("response");
    }

    // The answer's body on 2xx, null on 404; any other failure throws.
    private async Task<string?> SendAsync(HttpMethod method, string route, string? content, CancellationToken cancellationToken)
    {
        var uri = new Uri($"{_gatewayUri}/v1/{route}", UriKind.Absolute);

        HttpStatusCode status;
        string body;
        bool retriedAfterRefresh;
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            if (content is not null)
                request.Content = new StringContent(content, Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            status = response.StatusCode;
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            retriedAfterRefresh = response.RequestMessage?.Options.TryGetValue(BearerTokenHandler.RetriedAfterRefresh, out bool retried) == true && retried;
        }
        catch (TimeoutRejectedException ex)
        {
            throw AuthFailureException.Unreachable(_gatewayUri, "timeout", ex);
        }
        catch (Exception ex) when (NetworkFailure.IsNetworkFailure(ex, cancellationToken))
        {
            throw AuthFailureException.Unreachable(_gatewayUri, NetworkFailure.Describe(ex), ex);
        }

        int code = (int)status;
        _logger.LogInformation("{Method} {Path} answered {Status}", method.Method, uri.AbsolutePath, code);

        if (code is >= 200 and < 300)
            return body;

        if (status == HttpStatusCode.NotFound)
            return null;

        throw await FailureAsync(code, Read<ProblemBody>(body), retriedAfterRefresh, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Exception> FailureAsync(int code, ProblemBody? problem, bool retriedAfterRefresh, CancellationToken cancellationToken)
    {
        string? title = problem?.Title;

        switch (code)
        {
            case 401 when _refresher is not null && title == MissingCredentials:
                // Nothing reached the server that could be wrong, so nothing is deleted.
                return new LoginRequiredException();

            case 401 when _refresher is not null && retriedAfterRefresh:
                // Refused again with a token refreshed for this request (contract §8.2: second 401).
                await _refresher.InvalidateAsync(cancellationToken).ConfigureAwait(false);
                return new LoginRequiredException();

            case 401 when _refresher is not null:
                // A first 401 without invalid_token, so the handler did not refresh: a proxy's page,
                // or a stripped challenge. Nothing says the sign-in is over, so it is kept.
                return new CloudApiException(code, title, Describe(code, problem));

            case 400 when title == AmbiguousCredentials:
                return new CloudApiException(code, title, "CLI bug: two credentials were sent in one request.");

            case 403 when title == ApiKeyInsufficientScope:
                return new CloudApiException(code, title, "This API key cannot read Cloud data (its scope does not include read). Sign in with `xping login` instead.");

            case 403 when title == ApiKeyFeatureNotAvailable:
                return new CloudApiException(code, title, "This API key cannot read Cloud data (the workspace's plan does not include API access). Sign in with `xping login` instead.");

            case 426:
                return AuthFailureException.VersionMismatch(
                    "Xping Cloud no longer accepts this version of the CLI. Please upgrade the xping CLI.");

            case 429:
                return new CloudApiException(code, title, "Xping Cloud is rate limiting requests; try again in a minute.");

            case >= 500:
                return new CloudApiException(code, title, string.Create(CultureInfo.InvariantCulture, $"Xping Cloud failed to answer (HTTP {code})."));

            default:
                return new CloudApiException(code, title, Describe(code, problem));
        }
    }

    private static string Describe(int code, ProblemBody? problem) =>
        (problem?.Title, problem?.Detail) switch
        {
            ({ Length: > 0 } title, { Length: > 0 } detail) => $"Xping Cloud refused the request ({title}): {Redaction.Scrub(detail)}",
            ({ Length: > 0 } title, _) => $"Xping Cloud refused the request ({title}).",
            _ => string.Create(CultureInfo.InvariantCulture, $"Xping Cloud refused the request (HTTP {code}).")
        };

    // Deserialization leaves a missing member null whatever its declared type.
    private AuthFailureException Incomplete(string what) =>
        AuthFailureException.Unreachable(_gatewayUri, $"the DataGateway answered with an incomplete {what}");

    private T? Read<T>(string body)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            return _serializer.Deserialize<T>(body);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// The DataGateway's <c>application/problem+json</c> body (contract §8.2).
    /// </summary>
    private sealed record ProblemBody(int? Status, string? Title, string? Detail);
}
