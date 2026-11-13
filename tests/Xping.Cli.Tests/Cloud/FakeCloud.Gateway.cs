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
/// scripted with <see cref="AddProject"/>, <see cref="AddTest"/>, <see cref="AddRun"/> and
/// <see cref="AddSession"/>; the digest validates its request as Cloud does (contract §6.1).
/// </remarks>
internal sealed partial class FakeCloud
{
    public const string GatewayPrefix = "/gw/";

    private readonly List<JsonObject> _projects = [];
    private readonly Dictionary<(string Project, string Fingerprint), JsonObject> _tests = [];
    private readonly Dictionary<(string Project, string Fingerprint), List<JsonObject>> _runs = [];
    private readonly Dictionary<string, JsonObject> _digestSessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ApiKeyScript> _apiKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _bodies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task> _holds = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets whether every access token is refused as invalid, including ones issued later.
    /// </summary>
    public bool RefuseAccessTokens { get; set; }

    /// <summary>
    /// Gets or sets the <c>signatureAlgorithm</c> every digest answers with.
    /// </summary>
    public string SignatureAlgorithm { get; set; } = "emh-1";

    /// <summary>
    /// Gets or sets whether the run history says older runs exist past the ones it returned.
    /// </summary>
    public bool RunsHaveMore { get; set; }

    /// <summary>
    /// Gets or sets how many tests a suite-health page holds at most, below the asked page size.
    /// </summary>
    public int HealthPageSize { get; set; } = 500;

    /// <summary>
    /// Makes every answer to <paramref name="path"/> wait for <paramref name="until"/> after the
    /// request was recorded, as a slow Cloud would.
    /// </summary>
    /// <remarks>
    /// Per path, and taken in the same lock that records the request: a test that sees the request
    /// and moves on to hold the next path cannot leave this one waiting on the wrong gate.
    /// </remarks>
    public void HoldGateway(string path, Task until)
    {
        lock (_gate)
            _holds[path] = until;
    }

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
    /// Adds a test the project knows, with a digest (contract §6.2) that <paramref name="edit"/>
    /// changes before it is stored.
    /// </summary>
    /// <remarks>
    /// A project with a test is one the workspace has, whether or not <see cref="AddProject"/> listed
    /// it. A requested signature the digest does not hold is answered with zero counts, as Cloud does.
    /// </remarks>
    public void AddTest(string projectKey, string fingerprint, Action<JsonObject>? edit = null)
    {
        var test = new JsonObject
        {
            ["fingerprint"] = fingerprint,
            ["state"] = "ok",
            ["testName"] = "Shop.Tests.CheckoutTests.Checkout_Succeeds",
            ["lastExecutedAt"] = "2026-09-28T12:00:00Z",
            ["totalRuns"] = 812,
            ["verdict"] = "known-flaky",
            ["confidence"] = new JsonObject
            {
                ["state"] = "scored",
                ["score"] = 0.62,
                ["lowerBound"] = 0.55,
                ["upperBound"] = 0.69,
                ["category"] = "moderately-reliable",
                ["trend"] = "stable",
                ["delta"] = -0.03,
                ["effectiveSampleSize"] = 48.0,
                ["evidenceLevel"] = "robust",
                ["canonicalRuns"] = 40,
                ["speculativeRuns"] = 6,
                ["untrackedRuns"] = 2,
                ["window"] = Window(35),
                ["evidenceWindow"] = Window(14),
                ["computedAt"] = "2026-09-28T12:05:00Z",
                ["freshness"] = "current"
            },
            ["flaky"] = new JsonObject
            {
                ["state"] = "flaky",
                ["category"] = "timing",
                ["severity"] = "major",
                ["since"] = "2026-09-20T08:00:00Z",
                ["lastDetectionAt"] = "2026-09-28T12:05:00Z",
                ["window"] = Window(14),
                ["computedAt"] = "2026-09-28T12:05:00Z",
                ["freshness"] = "current"
            },
            ["canonical"] = new JsonObject
            {
                ["runs"] = 10,
                ["failed"] = 3,
                ["consecutiveFailures"] = 0,
                ["failingSince"] = null,
                ["latestOutcome"] = "passed",
                ["latestAt"] = "2026-09-28T12:00:00Z",
                ["window"] = new JsonObject { ["kind"] = "runs", ["length"] = 10, ["oldestAt"] = "2026-09-25T08:00:00Z" }
            },
            ["lastTransition"] = new JsonObject
            {
                ["type"] = "flakiness-introduced",
                ["from"] = null,
                ["to"] = "major",
                ["occurredAt"] = "2026-09-20T08:00:00Z"
            },
            ["signatures"] = new JsonArray()
        };

        edit?.Invoke(test);

        lock (_gate)
            _tests[(projectKey, fingerprint)] = test;

        static JsonObject Window(int days) =>
            new() { ["kind"] = "days", ["length"] = days, ["end"] = "2026-09-28T12:05:00Z" };
    }

    /// <summary>
    /// Adds a run to a test's Cloud history (contract §6.3), after the ones added before it: add
    /// them newest first. <paramref name="edit"/> changes it before it is stored.
    /// </summary>
    public void AddRun(string projectKey, string fingerprint, Action<JsonObject>? edit = null)
    {
        var run = new JsonObject
        {
            ["sessionKey"] = Guid.NewGuid().ToString(),
            ["startedAt"] = "2026-09-28T12:00:00Z",
            ["outcome"] = "failed",
            ["attempts"] = 1,
            ["firstAttemptOutcome"] = "failed",
            ["durationMs"] = 812,
            ["tier"] = "canonical",
            ["isCi"] = true,
            ["machineName"] = "gh-runner-17",
            ["branch"] = "main",
            ["commitSha"] = "0053b0ef0053b0ef0053b0ef0053b0ef0053b0ef",
            ["pullRequestNumber"] = null,
            ["exceptionType"] = "InvalidOperationException",
            ["errorMessageHash"] = new string('a', 64)
        };

        edit?.Invoke(run);

        lock (_gate)
        {
            if (!_runs.TryGetValue((projectKey, fingerprint), out List<JsonObject>? runs))
                _runs[(projectKey, fingerprint)] = runs = [];

            runs.Add(run);
        }
    }

    /// <summary>
    /// Makes the digest answer for an uploaded session; any other key is <c>unknown</c>.
    /// </summary>
    public void AddSession(string sessionKey, string state = "complete", bool scored = true, bool flakyExamined = true)
    {
        lock (_gate)
        {
            _digestSessions[sessionKey] = new JsonObject
            {
                ["sessionKey"] = sessionKey,
                ["state"] = state,
                ["scored"] = scored,
                ["flakyExamined"] = flakyExamined
            };
        }
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
                _faults.Add(new Fault(path, status, title, Drop: false, "Injected fault.", Problem: true, retryAfter));
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
    /// Answers every authorized request to <paramref name="path"/> with 200 and <paramref name="body"/>
    /// as it is, for a response the scripted data cannot produce.
    /// </summary>
    public void SetGatewayBody(string path, string body)
    {
        lock (_gate)
            _bodies[path] = body;
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

    private Reply Gateway(string method, string path, NameValueCollection query, NameValueCollection headers, string body)
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

        string[] segments = [.. path[GatewayPrefix.Length..].Split('/').Select(Uri.UnescapeDataString)];

        // Contract §6.1: the digest is the DataGateway's one POST, a read with a body.
        bool digest = segments is ["v1", "projects", _, "tests", "digest"];
        if (method != (digest ? "POST" : "GET"))
            return Problem(405, "Error.MethodNotAllowed", "Method not allowed.");

        lock (_gate)
        {
            if (_bodies.TryGetValue(path, out string? scripted))
                return new Reply(200, scripted);

            return segments switch
            {
                ["v1", "projects"] => Projects(query),
                ["v1", "projects", var key] => _projects.Find(p => (string?)p["id"] == key) is { } project
                    ? new Reply(200, project.ToJsonString())
                    : NotFound(),
                ["v1", "projects", var key, "tests", "digest"] => Digest(key, body),
                ["v1", "projects", var key, "tests", "health"] => Health(key, query),
                ["v1", "projects", var key, "tests", var fingerprint, "runs"] => Runs(key, fingerprint, query),
                _ => NotFound()
            };
        }
    }

    private bool HasProject(string key) =>
        _projects.Exists(p => (string?)p["id"] == key) || _tests.Keys.Any(t => t.Project == key);

    private Reply Digest(string projectKey, string body)
    {
        JsonObject? request;
        try
        {
            request = JsonNode.Parse(body) as JsonObject;
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request?["tests"] is not JsonArray tests || tests.Count is 0 or > 100)
            return Problem(400, "Validation.Tests", "Between 1 and 100 tests.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonNode? item in tests)
        {
            if (item?["fingerprint"]?.GetValue<string>() is not { Length: > 0 and <= 128 } fingerprint || !seen.Add(fingerprint))
                return Problem(400, "Validation.Tests.Fingerprint", "A fingerprint is missing, too long or repeated.");

            JsonArray hashes = item["errorMessageHashes"] as JsonArray ?? [];
            if (hashes.Count > 3 || hashes.Any(h => h?.GetValue<string>() is not { Length: 64 } hash || !hash.All(char.IsAsciiHexDigitLower)))
                return Problem(400, "Validation.Tests.ErrorMessageHashes", "At most 3 hashes of 64 lowercase hex.");
        }

        JsonArray sessionKeys = request["sessionKeys"] as JsonArray ?? [];
        if (sessionKeys.Count > 5)
            return Problem(400, "Validation.SessionKeys", "At most 5 session keys.");

        if (!HasProject(projectKey))
            return NotFound();

        var answer = new JsonObject
        {
            ["computedAt"] = _time.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
            ["signatureAlgorithm"] = SignatureAlgorithm,
            ["retentionDays"] = 365,
            ["tests"] = new JsonArray([.. tests.Select(t => (JsonNode)DigestOf(projectKey, t!))]),
            ["sessions"] = new JsonArray([.. sessionKeys.Select(k => (JsonNode)SessionOf(k!.GetValue<string>()))])
        };

        return new Reply(200, answer.ToJsonString());
    }

    /// <summary>
    /// Contract §6.5: every test of the project whose verdict is unhealthy, ordered by fingerprint,
    /// a page at a time. The cursor is the index of the page's first test.
    /// </summary>
    private Reply Health(string projectKey, NameValueCollection query)
    {
        if (!int.TryParse(query["pageSize"] ?? "500", CultureInfo.InvariantCulture, out int pageSize) || pageSize is < 1 or > 500)
            return Problem(400, "Validation.PageSize", "Between 1 and 500.");

        string[] sessionKeys = query.GetValues("sessionKeys") ?? [];
        if (sessionKeys.Length > 5)
            return Problem(400, "Validation.SessionKeys", "At most 5 session keys.");

        if (!HasProject(projectKey))
            return NotFound();

        int start = query["cursor"] is { } cursor ? int.Parse(cursor, CultureInfo.InvariantCulture) : 0;
        int size = Math.Min(pageSize, HealthPageSize);

        List<JsonObject> unhealthy =
        [
            .. _tests
                .Where(t => t.Key.Project == projectKey && (string?)t.Value["verdict"] is "known-flaky" or "broken" or "unstable")
                .OrderBy(t => t.Key.Fingerprint, StringComparer.Ordinal)
                .Select(t => t.Value)
        ];

        var answer = new JsonObject
        {
            ["computedAt"] = _time.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
            ["signatureAlgorithm"] = SignatureAlgorithm,
            ["retentionDays"] = 365,
            ["totalTests"] = _tests.Keys.Count(t => t.Project == projectKey),
            ["tests"] = new JsonArray([.. unhealthy.Skip(start).Take(size).Select(t => t.DeepClone())]),
            ["sessions"] = new JsonArray([.. sessionKeys.Select(k => (JsonNode)SessionOf(k))]),
            ["next"] = start + size < unhealthy.Count ? (start + size).ToString(CultureInfo.InvariantCulture) : null
        };

        return new Reply(200, answer.ToJsonString());
    }

    private JsonObject DigestOf(string projectKey, JsonNode requested)
    {
        string fingerprint = requested["fingerprint"]!.GetValue<string>();
        if (!_tests.TryGetValue((projectKey, fingerprint), out JsonObject? stored))
            return new JsonObject { ["fingerprint"] = fingerprint, ["state"] = "unknown" };

        var digest = (JsonObject)stored.DeepClone();
        if (digest["signatures"] is not JsonArray signatures)
            return digest;

        foreach (string hash in (requested["errorMessageHashes"] as JsonArray ?? []).Select(h => h!.GetValue<string>()))
        {
            if (signatures.FirstOrDefault(s => (string?)s?["errorMessageHash"] == hash) is JsonObject known)
            {
                known["requested"] = true;
                continue;
            }

            signatures.Add(new JsonObject
            {
                ["errorMessageHash"] = hash,
                ["requested"] = true,
                ["runs"] = 0,
                ["canonicalRuns"] = 0,
                ["ciRuns"] = 0,
                ["localRuns"] = 0,
                ["machines"] = 0
            });
        }

        return digest;
    }

    private JsonObject SessionOf(string sessionKey) =>
        _digestSessions.TryGetValue(sessionKey, out JsonObject? session)
            ? (JsonObject)session.DeepClone()
            : new JsonObject { ["sessionKey"] = sessionKey, ["state"] = "unknown", ["scored"] = false, ["flakyExamined"] = false };

    private Reply Runs(string projectKey, string fingerprint, NameValueCollection query)
    {
        if (!int.TryParse(query["limit"], CultureInfo.InvariantCulture, out int limit) || limit is < 1 or > 200)
            return Problem(400, "Validation.Limit", "Between 1 and 200.");

        if (!_tests.ContainsKey((projectKey, fingerprint)))
            return NotFound();

        List<JsonObject> runs = _runs.TryGetValue((projectKey, fingerprint), out List<JsonObject>? stored) ? stored : [];

        var answer = new JsonObject
        {
            ["retentionDays"] = 365,
            ["hasMore"] = RunsHaveMore || runs.Count > limit,
            ["runs"] = new JsonArray([.. runs.Take(limit).Select(r => r.DeepClone())])
        };

        return new Reply(200, answer.ToJsonString());
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
