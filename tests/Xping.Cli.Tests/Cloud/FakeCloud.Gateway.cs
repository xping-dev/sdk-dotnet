/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// The DataGateway half of <see cref="FakeCloud"/>: <c>/gw/v1/…</c> (contract §7, cli-auth-cli-spec §18.2).
/// </summary>
/// <remarks>
/// Enforces exactly one credential header, accepts the access tokens the fake issued until they
/// expire or their session is revoked, and accepts the API keys a test registered. Responses are
/// scripted with <see cref="AddProject"/> and <see cref="AddTest"/>.
/// </remarks>
internal sealed partial class FakeCloud
{
    public const string GatewayPrefix = "/gw/";

    private readonly List<JsonObject> _projects = [];
    private readonly Dictionary<(string Project, string Fingerprint), JsonObject> _tests = [];
    private readonly Dictionary<string, ApiKeyScript> _apiKeys = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets whether every access token is refused as invalid, including ones issued later.
    /// </summary>
    public bool RefuseAccessTokens { get; set; }

    /// <summary>
    /// Gets the DataGateway base URI the discovery document announces.
    /// </summary>
    public string GatewayUri => CloudUrl + "/gw";

    /// <summary>
    /// Adds a project to the workspace.
    /// </summary>
    public void AddProject(string id, string? displayName = null, string? slug = null)
    {
        lock (_gate)
        {
            _projects.Add(new JsonObject
            {
                ["id"] = id,
                ["displayName"] = displayName,
                ["slug"] = slug ?? "slug-" + id,
                ["totalTestCount"] = 1,
                ["totalSessionCount"] = 1,
                ["createdAtUtc"] = "2026-09-01T00:00:00Z"
            });
        }
    }

    /// <summary>
    /// Adds a test with a <c>TestResponse</c> body; <paramref name="edit"/> changes it before it is stored.
    /// </summary>
    public void AddTest(string projectKey, string fingerprint, Action<JsonObject>? edit = null)
    {
        var test = new JsonObject
        {
            ["testFingerprint"] = fingerprint,
            ["testName"] = "Checkout_Succeeds",
            ["className"] = "CheckoutTests",
            ["namespace"] = "Shop.Tests",
            ["averageDuration"] = "00:00:01.2000000",
            ["lastExecutedAtUtc"] = "2026-09-28T12:00:00Z",
            ["totalExecutions"] = 812,
            ["successRate"] = 0.91,
            ["confidenceScore"] = 0.62,
            ["scoreCategory"] = "ModeratelyReliable",
            ["scoreDelta"] = -0.03,
            ["evidenceLevel"] = "Robust",
            ["scoreTrend"] = "Stable",
            ["recommendation"] = "Investigate.",
            ["isEnabled"] = true,
            ["isFlaky"] = true,
            ["createdAtUtc"] = "2026-09-01T00:00:00Z"
        };

        edit?.Invoke(test);

        lock (_gate)
            _tests[(projectKey, fingerprint)] = test;
    }

    /// <summary>
    /// Registers an API key the DataGateway accepts, with what it may do.
    /// </summary>
    public void AddApiKey(string key, ApiKeyScript script = ApiKeyScript.Read)
    {
        lock (_gate)
            _apiKeys[key] = script;
    }

    /// <summary>
    /// Makes the next <paramref name="count"/> requests to <paramref name="path"/> answer with a
    /// DataGateway problem.
    /// </summary>
    /// <param name="path">The path, such as <c>/gw/v1/projects</c>.</param>
    /// <param name="count">How many requests in a row.</param>
    /// <param name="status">The status to answer with.</param>
    /// <param name="title">The problem's <c>title</c>.</param>
    /// <param name="retryAfter">The <c>Retry-After</c> seconds, or <see langword="null"/> for none.</param>
    public void FailGateway(string path, int count, HttpStatusCode status, string title, int? retryAfter = null)
    {
        lock (_gate)
        {
            for (int i = 0; i < count; i++)
                _faults.Enqueue(new Fault(path, status, title, Drop: false, "Injected fault.", Problem: true, retryAfter));
        }
    }

    /// <summary>
    /// Starts a CLI session as a completed sign-in would, without the browser.
    /// </summary>
    /// <returns>The session's first access and refresh token.</returns>
    public (string AccessToken, string RefreshToken) StartSession()
    {
        lock (_gate)
        {
            JsonObject response = JsonNode.Parse(IssueTokens(NewSession()).Body)!.AsObject();
            return ((string)response["access_token"]!, (string)response["refresh_token"]!);
        }
    }

    /// <summary>
    /// Revokes the session <paramref name="refreshToken"/> belongs to, as the Portal sessions page does.
    /// </summary>
    public void RevokeSession(string refreshToken)
    {
        lock (_gate)
            _sessions[_refreshTokens[refreshToken].SessionId].Revoked = true;
    }

    /// <summary>
    /// Gets the gateway requests received, in order.
    /// </summary>
    public IReadOnlyList<RecordedRequest> GatewayRequests =>
        [.. Requests.Where(r => r.Path.StartsWith(GatewayPrefix, StringComparison.Ordinal))];

    private Reply Gateway(string method, string path, NameValueCollection query, NameValueCollection headers)
    {
        string? authorization = headers["Authorization"];
        string? apiKey = headers["X-API-Key"];

        // Contract §7.1: both is a 400 without looking at either; none is a 401.
        if (authorization is not null && apiKey is not null)
            return Problem(400, "Error.Authentication.AmbiguousCredentials", "Send one credential.");

        if (authorization is null && apiKey is null)
        {
            return Problem(401, "Error.Authentication.MissingCredentials", "No credentials.") with
            {
                WwwAuthenticate = "Bearer realm=\"xping\""
            };
        }

        Reply? refused = authorization is not null ? CheckBearer(authorization) : CheckApiKey(apiKey!);
        if (refused is not null)
            return refused;

        if (method != "GET")
            return Problem(405, "Error.MethodNotAllowed", "Reads only.");

        string[] segments = [.. path[GatewayPrefix.Length..].Split('/').Select(Uri.UnescapeDataString)];

        lock (_gate)
        {
            return segments switch
            {
                ["v1", "projects"] => Projects(query),
                ["v1", "projects", var key] => _projects.Find(p => (string?)p["id"] == key) is { } project
                    ? new Reply(200, project.ToJsonString())
                    : NotFound(),
                ["v1", "projects", var key, "tests", var fingerprint] => _tests.TryGetValue((key, fingerprint), out JsonObject? test)
                    ? new Reply(200, test.ToJsonString())
                    : NotFound(),
                _ => NotFound()
            };
        }
    }

    private Reply? CheckBearer(string authorization)
    {
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return InvalidToken("Error.AccessToken.Invalid", "The access token is malformed.");

        lock (_gate)
        {
            if (!_accessTokens.TryGetValue(authorization["Bearer ".Length..], out IssuedAccessToken? issued)
                || _sessions[issued.SessionId].Revoked
                || RefuseAccessTokens)
            {
                return InvalidToken("Error.AccessToken.Invalid", "The access token is invalid.");
            }

            return _time.GetUtcNow() >= issued.ExpiresAt
                ? InvalidToken("Error.AccessToken.Expired", "The access token has expired.")
                : null;
        }
    }

    private Reply? CheckApiKey(string apiKey)
    {
        ApiKeyScript script;
        lock (_gate)
        {
            if (!_apiKeys.TryGetValue(apiKey, out script))
                return Problem(401, "Error.ApiKey.Invalid", "The API key is invalid.");
        }

        return script switch
        {
            ApiKeyScript.UploadOnly => Problem(403, "Error.ApiKey.InsufficientScope", "The API key lacks the read scope."),
            ApiKeyScript.NoApiAccess => Problem(403, "Error.ApiKey.FeatureNotAvailable", "The plan does not include API access."),
            _ => null
        };
    }

    private Reply Projects(NameValueCollection query)
    {
        int pageNumber = int.TryParse(query["pageNumber"], CultureInfo.InvariantCulture, out int n) && n > 0 ? n : 1;
        int pageSize = int.TryParse(query["pageSize"], CultureInfo.InvariantCulture, out int s) && s > 0 ? s : 20;

        var page = new JsonObject
        {
            ["items"] = new JsonArray([.. _projects.Skip((pageNumber - 1) * pageSize).Take(pageSize).Select(p => (JsonNode)p.DeepClone())]),
            ["totalCount"] = _projects.Count,
            ["pageNumber"] = pageNumber,
            ["pageSize"] = pageSize,
            ["isTotalCountExact"] = true
        };

        return new Reply(200, page.ToJsonString());
    }

    private static Reply NotFound() => Problem(404, "Error.NotFound", "Not found.");

    private static Reply InvalidToken(string title, string description) =>
        Problem(401, title, description) with
        {
            WwwAuthenticate = $"Bearer realm=\"xping\", error=\"invalid_token\", error_description=\"{description}\""
        };

    private static Reply Problem(int status, string title, string detail) =>
        new(status, JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["status"] = status,
            ["title"] = title,
            ["detail"] = detail,
            ["type"] = string.Create(CultureInfo.InvariantCulture, $"https://httpstatuses.com/{status}")
        }));
}

/// <summary>
/// What an API key registered on <see cref="FakeCloud"/> may do.
/// </summary>
internal enum ApiKeyScript
{
    /// <summary>A key with the <c>read</c> scope on a plan with API access.</summary>
    Read,

    /// <summary>The SDK's upload key: 403 <c>Error.ApiKey.InsufficientScope</c>.</summary>
    UploadOnly,

    /// <summary>A plan without API access: 403 <c>Error.ApiKey.FeatureNotAvailable</c>.</summary>
    NoApiAccess
}
