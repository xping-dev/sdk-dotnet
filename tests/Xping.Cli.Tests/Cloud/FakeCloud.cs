/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// An in-process Xping Cloud Portal that implements the CLI auth contract (cli-auth-cli-spec §18.2).
/// </summary>
/// <remarks>
/// <para>
/// Listens on an ephemeral <c>127.0.0.1</c> port, so its Cloud URL is a valid <c>http</c> one.
/// Codes, refresh rotation and device codes run on the injected <see cref="TimeProvider"/>.
/// </para>
/// <para>
/// Every request is recorded, so a test can assert what was sent where: that a refresh token
/// never appeared in a URL, that <c>client_id</c> was on every grant.
/// </para>
/// </remarks>
internal sealed partial class FakeCloud : IAsyncDisposable
{
    public const string WorkspaceId = "01J8K2V6XN7Y0Q4R5S6T7V8V9X";
    public const string UserId = "01J8K2V6XN7Y0Q4R5S6T7V8V9W";
    public const string Email = "jane@example.com";

    private static readonly TimeSpan CodeLifetime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ReuseLeeway = TimeSpan.FromSeconds(30);

    private readonly HttpListener _listener;
    private readonly Task _loop;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private readonly List<RecordedRequest> _requests = [];
    private readonly Queue<Fault> _faults = new();
    private readonly Dictionary<string, AuthorizationCode> _codes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RefreshToken> _refreshTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DeviceGrant> _deviceGrants = new(StringComparer.Ordinal);
    private readonly Queue<AuthorizeScript> _authorizeScripts = new();
    private readonly Dictionary<string, IssuedAccessToken> _accessTokens = new(StringComparer.Ordinal);
    private int _sequence;

    public FakeCloud(TimeProvider time)
    {
        _time = time;
        (_listener, Port) = Bind();
        CloudUrl = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{Port}");
        _loop = Task.Run(AcceptLoopAsync);
    }

    public int Port { get; }

    /// <summary>
    /// Gets the normalized Cloud URL, as the CLI keys everything by it.
    /// </summary>
    public string CloudUrl { get; }

    /// <summary>
    /// Gets or sets a change applied to every discovery document before it is sent.
    /// </summary>
    public Action<JsonObject>? EditDiscovery { get; set; }

    /// <summary>
    /// Gets or sets a change applied to every device authorization response before it is sent.
    /// </summary>
    public Action<JsonObject>? EditDeviceResponse { get; set; }

    /// <summary>
    /// Gets or sets a change applied to every successful token response before it is sent.
    /// </summary>
    public Action<JsonObject>? EditTokenResponse { get; set; }

    /// <summary>
    /// Gets or sets a task every token answer waits for after the server has acted on the request,
    /// as a slow network would; the tokens are already issued and rotated when it starts waiting.
    /// </summary>
    public Task? HoldTokenResponses { get; set; }

    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_gate)
                return [.. _requests];
        }
    }

    public IReadOnlyList<RecordedRequest> RequestsTo(string path) =>
        [.. Requests.Where(r => string.Equals(r.Path, path, StringComparison.Ordinal))];

    /// <summary>
    /// Makes the next <paramref name="count"/> requests to <paramref name="path"/> fail.
    /// </summary>
    /// <param name="path">The path, such as <c>/connect/token</c>.</param>
    /// <param name="count">How many requests in a row.</param>
    /// <param name="status">The status to answer with.</param>
    /// <param name="error">The OAuth <c>error</c>, or <see langword="null"/> for a body that is not JSON.</param>
    /// <param name="description">The <c>error_description</c>.</param>
    public void Fail(string path, int count, HttpStatusCode status, string? error, string description = "Injected fault.")
    {
        lock (_gate)
        {
            for (int i = 0; i < count; i++)
                _faults.Enqueue(new Fault(path, status, error, Drop: false, description, Problem: false, RetryAfter: null));
        }
    }

    /// <summary>
    /// Makes the next <paramref name="count"/> requests to <paramref name="path"/> lose their
    /// connection before an answer.
    /// </summary>
    public void Drop(string path, int count)
    {
        lock (_gate)
        {
            for (int i = 0; i < count; i++)
                _faults.Enqueue(new Fault(path, 0, null, Drop: true, string.Empty, Problem: false, RetryAfter: null));
        }
    }

    /// <summary>
    /// Decides how the next requests to <c>/connect/authorize</c> end, one script per request; once
    /// they are used up, the user approves.
    /// </summary>
    public void QueueAuthorize(params AuthorizeScript[] scripts)
    {
        lock (_gate)
        {
            foreach (AuthorizeScript script in scripts)
                _authorizeScripts.Enqueue(script);
        }
    }

    /// <summary>
    /// Issues an authorization code as <c>/connect/authorize</c> would after consent.
    /// </summary>
    public string IssueCode(string redirectUri, string codeChallenge)
    {
        lock (_gate)
        {
            string code = NewOpaque("code");
            _codes[code] = new AuthorizationCode(redirectUri, codeChallenge, _time.GetUtcNow() + CodeLifetime);
            return code;
        }
    }

    /// <summary>
    /// Gets the user codes of the device flows started and not yet completed, oldest first.
    /// </summary>
    public IReadOnlyList<string> DeviceUserCodes
    {
        get
        {
            lock (_gate)
                return [.. _deviceGrants.Values.Select(g => g.UserCode).Order(StringComparer.Ordinal)];
        }
    }

    /// <summary>
    /// Plays the user approving the device flow of <paramref name="userCode"/> in the browser.
    /// </summary>
    public void ApproveDevice(string userCode) => SetDeviceDecision(userCode, DeviceDecision.Approved);

    /// <summary>
    /// Plays the user denying the device flow of <paramref name="userCode"/> in the browser.
    /// </summary>
    public void DenyDevice(string userCode) => SetDeviceDecision(userCode, DeviceDecision.Denied);

    /// <summary>
    /// Gets whether the session that <paramref name="refreshToken"/> belongs to was revoked.
    /// </summary>
    public bool IsSessionRevoked(string refreshToken)
    {
        lock (_gate)
            return _refreshTokens.TryGetValue(refreshToken, out RefreshToken? token) && _sessions[token.SessionId].Revoked;
    }

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        _listener.Close();

        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            // Stopping the listener ends the loop this way.
        }

        // Contract §7.1, for every test that talks to the fake: never both credential headers
        // (Report_BothHeadersNever, cli-auth-cli-spec §18.2).
        Assert.DoesNotContain(Requests, r => r.Headers.ContainsKey("Authorization") && r.Headers.ContainsKey("X-API-Key"));
    }

    private static (HttpListener Listener, int Port) Bind()
    {
        // HttpListener cannot bind port 0; a port is probed and then taken, and the race with
        // another process taking it in between is retried.
        for (int attempt = 0; ; attempt++)
        {
            int port;
            using (var probe = new TcpListener(IPAddress.Loopback, 0))
            {
                probe.Start();
                port = ((IPEndPoint)probe.LocalEndpoint).Port;
            }

            var listener = new HttpListener();
            listener.Prefixes.Add(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/"));

            try
            {
                listener.Start();
                return (listener, port);
            }
            catch (HttpListenerException) when (attempt < 5)
            {
                listener.Close();
            }
        }
    }

    private async Task AcceptLoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        string path = request.Url!.AbsolutePath;

        string body;
        using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
            body = await reader.ReadToEndAsync().ConfigureAwait(false);

        NameValueCollection form = string.Equals(request.ContentType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
            ? HttpUtility.ParseQueryString(body)
            : [];

        Fault? fault;
        lock (_gate)
        {
            _requests.Add(new RecordedRequest(
                request.HttpMethod,
                path,
                request.Url.Query,
                request.Headers.AllKeys.Where(k => k is not null).ToDictionary(k => k!, k => request.Headers[k]!, StringComparer.OrdinalIgnoreCase),
                form.AllKeys.Where(k => k is not null).ToDictionary(k => k!, k => form[k]!, StringComparer.Ordinal),
                body,
                _time.GetUtcNow()));

            fault = _faults.TryPeek(out Fault? next) && string.Equals(next.Path, path, StringComparison.Ordinal)
                ? _faults.Dequeue()
                : null;
        }

        if (fault is { Drop: true })
        {
            // A body cut short: aborting before any byte is written still sends an empty 200.
            context.Response.StatusCode = 200;
            context.Response.ContentLength64 = 1000;
            await context.Response.OutputStream.WriteAsync("{\"acc"u8.ToArray()).ConfigureAwait(false);
            await context.Response.OutputStream.FlushAsync().ConfigureAwait(false);
            context.Response.Abort();
            return;
        }

        Reply reply = fault switch
        {
            null => Route(request.HttpMethod, path, form, request.QueryString, request.Headers),
            { Problem: true } => Problem((int)fault.Status, fault.Error ?? "Error.Injected", fault.Description) with
            {
                RetryAfter = fault.RetryAfter
            },
            _ => new Reply((int)fault.Status, fault.Error is null ? "<html>unavailable</html>" : ErrorJson(fault.Error, fault.Description))
        };

        if (path == "/connect/token" && HoldTokenResponses is { } hold)
            await hold.ConfigureAwait(false);

        context.Response.StatusCode = reply.Status;
        context.Response.ContentType = reply.Body.StartsWith('<') ? "text/html" : "application/json";
        context.Response.Headers["Cache-Control"] = "no-store";
        if (reply.Location is not null)
            context.Response.Headers["Location"] = reply.Location;
        if (reply.WwwAuthenticate is not null)
            context.Response.Headers["WWW-Authenticate"] = reply.WwwAuthenticate;
        if (reply.RetryAfter is { } retryAfter)
            context.Response.Headers["Retry-After"] = retryAfter.ToString(CultureInfo.InvariantCulture);

        byte[] bytes = Encoding.UTF8.GetBytes(reply.Body);
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }

    private Reply Route(string method, string path, NameValueCollection form, NameValueCollection query, NameValueCollection headers) =>
        (method, path) switch
        {
            (_, _) when path.StartsWith(GatewayPrefix, StringComparison.Ordinal) => Gateway(method, path, query, headers),
            ("GET", "/.well-known/openid-configuration") => Discovery(),
            ("GET", "/connect/authorize") => Authorize(query),
            (_, "/moved") => new Reply(302, string.Empty, CloudUrl + "/connect/token"),
            ("POST", "/connect/token") => WithClient(form, Token),
            ("POST", "/connect/device") => WithClient(form, Device),
            ("POST", "/connect/revoke") => WithClient(form, Revoke),
            _ => new Reply(404, string.Empty)
        };

    private Reply Discovery()
    {
        var document = new JsonObject
        {
            ["issuer"] = CloudUrl + "/",
            ["authorization_endpoint"] = CloudUrl + "/connect/authorize",
            ["token_endpoint"] = CloudUrl + "/connect/token",
            ["revocation_endpoint"] = CloudUrl + "/connect/revoke",
            ["device_authorization_endpoint"] = CloudUrl + "/connect/device",
            ["jwks_uri"] = CloudUrl + "/.well-known/jwks",
            ["scopes_supported"] = new JsonArray("user:read", "offline_access"),
            ["code_challenge_methods_supported"] = new JsonArray("S256"),
            ["xping_data_gateway_uri"] = CloudUrl + "/gw",
            ["xping_contract_version"] = 1,
            ["xping_cli_min_version"] = "0.1.0"
        };

        EditDiscovery?.Invoke(document);
        return new Reply(200, document.ToJsonString());
    }

    // Contract §4.2. A request that breaks the contract renders a page and never redirects (OQ-18),
    // so a CLI bug shows up as the loopback timeout rather than as a callback.
    private Reply Authorize(NameValueCollection query)
    {
        string redirectUri = query["redirect_uri"] ?? string.Empty;
        string state = query["state"] ?? string.Empty;
        string challenge = query["code_challenge"] ?? string.Empty;

        bool valid =
            query["response_type"] == "code"
            && query["client_id"] == "xping-cli"
            && query["code_challenge_method"] == "S256"
            && challenge.Length == 43
            && HasContractScope(query["scope"])
            && LoopbackRedirect().IsMatch(redirectUri)
            && state.Length > 0;

        if (!valid)
            return new Reply(400, "<html>invalid authorization request</html>");

        AuthorizeScript script;
        lock (_gate)
            script = _authorizeScripts.TryDequeue(out AuthorizeScript next) ? next : AuthorizeScript.Approve;

        return script switch
        {
            AuthorizeScript.Approve => Redirect(redirectUri, ("code", IssueCode(redirectUri, challenge)), ("state", state)),
            AuthorizeScript.Deny => Redirect(redirectUri, ("error", "access_denied"), ("error_description", "The user declined."), ("state", state)),
            AuthorizeScript.WrongState => Redirect(redirectUri, ("code", IssueCode(redirectUri, challenge)), ("state", "wrong-" + state)),
            AuthorizeScript.ServerError => Redirect(redirectUri, ("error", "server_error"), ("error_description", "Something broke."), ("state", state)),
            _ => new Reply(200, "<html>consent page; the user never decides</html>")
        };
    }

    private static Reply Redirect(string redirectUri, params (string Name, string Value)[] parameters) =>
        new(302, string.Empty, redirectUri + "?" + string.Join('&', parameters.Select(p => $"{Uri.EscapeDataString(p.Name)}={Uri.EscapeDataString(p.Value)}")));

    [GeneratedRegex(@"^http://(127\.0\.0\.1|\[::1\]):\d+/callback$", RegexOptions.CultureInvariant)]
    private static partial Regex LoopbackRedirect();

    private static Reply WithClient(NameValueCollection form, Func<NameValueCollection, Reply> handler) =>
        string.Equals(form["client_id"], "xping-cli", StringComparison.Ordinal)
            ? handler(form)
            : new Reply(401, ErrorJson("invalid_client", "Unknown client."));

    private Reply Token(NameValueCollection form)
    {
        lock (_gate)
        {
            return form["grant_type"] switch
            {
                "authorization_code" => ExchangeCode(form),
                "refresh_token" => Refresh(form),
                "urn:ietf:params:oauth:grant-type:device_code" => PollDevice(form),
                _ => new Reply(400, ErrorJson("unsupported_grant_type", "Unknown grant."))
            };
        }
    }

    private Reply ExchangeCode(NameValueCollection form)
    {
        string code = form["code"] ?? string.Empty;

        // Single use: a code is gone after its first presentation, right or wrong.
        if (!_codes.Remove(code, out AuthorizationCode? issued)
            || _time.GetUtcNow() >= issued.ExpiresAt
            || !string.Equals(form["redirect_uri"], issued.RedirectUri, StringComparison.Ordinal)
            || !string.Equals(Challenge(form["code_verifier"] ?? string.Empty), issued.CodeChallenge, StringComparison.Ordinal))
        {
            return new Reply(400, ErrorJson("invalid_grant", "The code is invalid."));
        }

        return IssueTokens(NewSession());
    }

    private Reply Refresh(NameValueCollection form)
    {
        if (!_refreshTokens.TryGetValue(form["refresh_token"] ?? string.Empty, out RefreshToken? presented))
            return new Reply(400, ErrorJson("invalid_grant", "Unknown refresh token."));

        Session session = _sessions[presented.SessionId];
        if (session.Revoked)
            return new Reply(400, ErrorJson("invalid_grant", "The session was revoked."));

        // Contract §6.6: a rotated token still works for 30 seconds; after that it is theft.
        if (presented.RotatedAt is { } rotatedAt && _time.GetUtcNow() - rotatedAt > ReuseLeeway)
        {
            session.Revoked = true;
            return new Reply(400, ErrorJson("invalid_grant", "Refresh token reuse detected."));
        }

        presented.RotatedAt ??= _time.GetUtcNow();
        return IssueTokens(session);
    }

    private Reply PollDevice(NameValueCollection form)
    {
        if (!_deviceGrants.TryGetValue(form["device_code"] ?? string.Empty, out DeviceGrant? grant))
            return new Reply(400, ErrorJson("invalid_grant", "Unknown device code."));

        DateTimeOffset now = _time.GetUtcNow();
        if (now >= grant.ExpiresAt)
            return new Reply(400, ErrorJson("expired_token", "The device code expired."));

        DateTimeOffset? previous = grant.LastPolledAt;
        grant.LastPolledAt = now;

        if (previous is { } last && now - last < grant.Interval)
        {
            grant.Interval += TimeSpan.FromSeconds(5);
            return new Reply(400, ErrorJson("slow_down", "Polling too fast."));
        }

        switch (grant.Decision)
        {
            case DeviceDecision.Pending:
                return new Reply(400, ErrorJson("authorization_pending", "Waiting for the user."));
            case DeviceDecision.Denied:
                return new Reply(400, ErrorJson("access_denied", "The user declined."));
            default:
                _deviceGrants.Remove(form["device_code"]!);
                return IssueTokens(NewSession());
        }
    }

    private Reply Device(NameValueCollection form)
    {
        if (!HasContractScope(form["scope"]))
            return new Reply(400, ErrorJson("invalid_scope", "Both scopes are required."));

        lock (_gate)
        {
            string deviceCode = NewOpaque("device");
            string userCode = string.Create(CultureInfo.InvariantCulture, $"ABCD-{_sequence:D4}");
            _deviceGrants[deviceCode] = new DeviceGrant(userCode, _time.GetUtcNow() + TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(5));

            var response = new JsonObject
            {
                ["device_code"] = deviceCode,
                ["user_code"] = userCode,
                ["verification_uri"] = CloudUrl + "/device",
                ["verification_uri_complete"] = CloudUrl + "/device?user_code=" + userCode,
                ["expires_in"] = 600,
                ["interval"] = 5
            };

            EditDeviceResponse?.Invoke(response);
            return new Reply(200, response.ToJsonString());
        }
    }

    private Reply Revoke(NameValueCollection form)
    {
        lock (_gate)
        {
            if (_refreshTokens.TryGetValue(form["token"] ?? string.Empty, out RefreshToken? token))
                _sessions[token.SessionId].Revoked = true;
        }

        // OpenIddict answers {} (contract OQ-19), known token or not.
        return new Reply(200, "{}");
    }

    private Session NewSession()
    {
        var session = new Session(NewUlidLike());
        _sessions[session.Id] = session;
        return session;
    }

    private Reply IssueTokens(Session session)
    {
        string refreshToken = NewOpaque("refresh");
        _refreshTokens[refreshToken] = new RefreshToken(session.Id);

        var response = new JsonObject
        {
            ["access_token"] = AccessToken(session),
            ["token_type"] = "Bearer",
            ["expires_in"] = (int)AccessTokenLifetime.TotalSeconds,
            ["refresh_token"] = refreshToken,
            ["scope"] = "user:read offline_access",
            ["id_token_hint"] = "ignored-extra-member"
        };

        EditTokenResponse?.Invoke(response);
        return new Reply(200, response.ToJsonString());
    }

    // JWT-shaped, unsigned: the CLI decodes the payload for display only (contract §6.1).
    private string AccessToken(Session session)
    {
        DateTimeOffset now = _time.GetUtcNow();
        string jti = NewOpaque("jti");
        var header = new JsonObject { ["alg"] = "none", ["typ"] = "at+jwt" };
        var payload = new JsonObject
        {
            ["iss"] = CloudUrl + "/",
            ["aud"] = "xping-datagateway",
            ["sub"] = UserId,
            ["workspace_id"] = WorkspaceId,
            ["sid"] = session.Id,
            ["email"] = Email,
            ["scope"] = "user:read",
            ["client_id"] = "xping-cli",
            ["jti"] = jti,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = (now + AccessTokenLifetime).ToUnixTimeSeconds()
        };

        string token = $"{Base64Url(header.ToJsonString())}.{Base64Url(payload.ToJsonString())}.fake-signature";
        _accessTokens[token] = new IssuedAccessToken(session.Id, now + AccessTokenLifetime);
        return token;
    }

    private void SetDeviceDecision(string userCode, DeviceDecision decision)
    {
        lock (_gate)
            _deviceGrants.Values.Single(g => g.UserCode == userCode).Decision = decision;
    }

    private string NewOpaque(string kind) =>
        string.Create(CultureInfo.InvariantCulture, $"{kind}-{Interlocked.Increment(ref _sequence):D4}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(12))}");

    private string NewUlidLike() =>
        string.Create(CultureInfo.InvariantCulture, $"01J8K2V6XN7Y0Q4R5S6T7V{Interlocked.Increment(ref _sequence):D4}");

    private static bool HasContractScope(string? scope) =>
        (scope ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal)
            .SequenceEqual(["offline_access", "user:read"]);

    private static string Challenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(string text) => Base64Url(Encoding.UTF8.GetBytes(text));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string ErrorJson(string error, string description) =>
        JsonSerializer.Serialize(new Dictionary<string, string> { ["error"] = error, ["error_description"] = description });

    private sealed record Reply(int Status, string Body, string? Location = null)
    {
        public string? WwwAuthenticate { get; init; }

        public int? RetryAfter { get; init; }
    }

    private sealed record Fault(string Path, HttpStatusCode Status, string? Error, bool Drop, string Description, bool Problem, int? RetryAfter);

    private sealed record IssuedAccessToken(string SessionId, DateTimeOffset ExpiresAt);

    private sealed record AuthorizationCode(string RedirectUri, string CodeChallenge, DateTimeOffset ExpiresAt);

    private sealed record RefreshToken(string SessionId)
    {
        public DateTimeOffset? RotatedAt { get; set; }
    }

    private sealed record Session(string Id)
    {
        public bool Revoked { get; set; }
    }

    private enum DeviceDecision
    {
        Pending,
        Approved,
        Denied
    }

    private sealed record DeviceGrant(string UserCode, DateTimeOffset ExpiresAt, TimeSpan Interval)
    {
        public TimeSpan Interval { get; set; } = Interval;

        public DateTimeOffset? LastPolledAt { get; set; }

        public DeviceDecision Decision { get; set; }
    }
}

/// <summary>
/// How a visit to <see cref="FakeCloud"/>'s <c>/connect/authorize</c> ends.
/// </summary>
internal enum AuthorizeScript
{
    /// <summary>The user consents; redirect with a code and the same state.</summary>
    Approve,

    /// <summary>The user declines; redirect with <c>error=access_denied</c>.</summary>
    Deny,

    /// <summary>Redirect with a code and a state the CLI did not send.</summary>
    WrongState,

    /// <summary>Redirect with <c>error=server_error</c>.</summary>
    ServerError,

    /// <summary>Show the consent page and never redirect.</summary>
    NoRedirect
}

/// <summary>
/// One request <see cref="FakeCloud"/> received.
/// </summary>
internal sealed record RecordedRequest(
    string Method,
    string Path,
    string Query,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Form,
    string Body,
    DateTimeOffset ReceivedAt);
