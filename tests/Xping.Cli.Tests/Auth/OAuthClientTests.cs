/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Discovery;
using Xping.Cli.Tests.Cloud;

namespace Xping.Cli.Tests.Auth;

public sealed class OAuthClientTests : IAsyncLifetime, IAsyncDisposable
{
    private const string Token = "/connect/token";
    private const string Device = "/connect/device";
    private const string Revoke = "/connect/revoke";
    private const string RedirectUri = "http://127.0.0.1:50123/callback";
    private const string Verifier = "dBjftJeZ4CVP-mJ92K9IA4m8u2yNdxGzm3VdXvNN6ES";

    private readonly AuthTestHost _host = new();
    private DiscoveryDocument _discovery = null!;

    public async Task InitializeAsync() => _discovery = await _host.DiscoverAsync().ConfigureAwait(false);

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task ACodeIsExchangedForTokens()
    {
        TokenResponse tokens = await ExchangeAsync();

        Assert.StartsWith("eyJ", tokens.AccessToken, StringComparison.Ordinal);
        Assert.StartsWith("refresh-", tokens.RefreshToken, StringComparison.Ordinal);
        Assert.Equal(TimeSpan.FromMinutes(15), tokens.ExpiresIn);
        Assert.Equal("user:read offline_access", tokens.Scope);
    }

    [Fact]
    public async Task TheCodeExchangeSendsExactlyTheContractParameters()
    {
        await ExchangeAsync();

        RecordedRequest request = Assert.Single(_host.Cloud.RequestsTo(Token));
        Assert.Equal("POST", request.Method);
        Assert.StartsWith("application/x-www-form-urlencoded", request.Headers["Content-Type"], StringComparison.Ordinal);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["client_id"] = "xping-cli",
                ["grant_type"] = "authorization_code",
                ["code"] = request.Form["code"],
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = Verifier
            },
            request.Form);
        Assert.Empty(request.Query);
        Assert.StartsWith("xping-cli/", request.Headers["User-Agent"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AVerifierThatDoesNotMatchTheChallengeIsRefused()
    {
        string code = _host.Cloud.IssueCode(RedirectUri, Challenge(Verifier));

        OAuthException failure = await Assert.ThrowsAsync<OAuthException>(
            () => _host.OAuth.ExchangeCodeAsync(_discovery, code, RedirectUri, Verifier + "x", CancellationToken.None));

        Assert.Equal("invalid_grant", failure.Error.Error);
        Assert.Equal(400, failure.Error.StatusCode);
        Assert.Equal("Xping Cloud returned invalid_grant: The code is invalid.", failure.Message);
    }

    [Fact]
    public async Task ACodeWorksOnce()
    {
        string code = _host.Cloud.IssueCode(RedirectUri, Challenge(Verifier));
        await _host.OAuth.ExchangeCodeAsync(_discovery, code, RedirectUri, Verifier, CancellationToken.None);

        OAuthException failure = await Assert.ThrowsAsync<OAuthException>(
            () => _host.OAuth.ExchangeCodeAsync(_discovery, code, RedirectUri, Verifier, CancellationToken.None));

        Assert.Equal("invalid_grant", failure.Error.Error);
    }

    [Fact]
    public async Task ExtraMembersOfATokenResponseAreIgnored()
    {
        _host.Cloud.EditTokenResponse = r =>
        {
            r["id_token"] = "ignored";
            r["xping_future"] = new JsonObject { ["a"] = 1 };
        };

        TokenResponse tokens = await ExchangeAsync();

        Assert.Equal(TimeSpan.FromMinutes(15), tokens.ExpiresIn);
    }

    [Theory]
    [InlineData("access_token")]
    [InlineData("refresh_token")]
    [InlineData("expires_in")]
    [InlineData("token_type")]
    public async Task ATokenResponseWithoutARequiredMemberIsRefused(string member)
    {
        _host.Cloud.EditTokenResponse = r => r.Remove(member);

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => ExchangeAsync());

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Contains("incomplete response", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokensNeverReachAMessage()
    {
        TokenResponse tokens = await ExchangeAsync();

        Assert.DoesNotContain(tokens.AccessToken, tokens.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(tokens.RefreshToken, tokens.ToString(), StringComparison.Ordinal);
        Assert.Equal(Redaction.Placeholder, Redaction.Scrub(tokens.RefreshToken));
        Assert.Equal(Redaction.Placeholder, Redaction.Scrub(tokens.AccessToken));
    }

    [Fact]
    public async Task ARefreshSendsNoScope()
    {
        TokenResponse first = await ExchangeAsync();

        await _host.OAuth.RefreshAsync(_discovery, first.RefreshToken, CancellationToken.None);

        RecordedRequest refresh = _host.Cloud.RequestsTo(Token)[^1];
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["client_id"] = "xping-cli",
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = first.RefreshToken
            },
            refresh.Form);
    }

    [Fact]
    public async Task ARefreshRotatesTheRefreshToken()
    {
        TokenResponse first = await ExchangeAsync();

        TokenResponse second = await _host.OAuth.RefreshAsync(_discovery, first.RefreshToken, CancellationToken.None);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.NotEqual(first.AccessToken, second.AccessToken);
    }

    [Fact]
    public async Task ARotatedTokenStillWorksInsideTheLeeway()
    {
        TokenResponse first = await ExchangeAsync();
        await _host.OAuth.RefreshAsync(_discovery, first.RefreshToken, CancellationToken.None);

        _host.Time.Advance(TimeSpan.FromSeconds(30));
        await _host.OAuth.RefreshAsync(_discovery, first.RefreshToken, CancellationToken.None);
    }

    [Fact]
    public async Task ARotatedTokenAfterTheLeewayEndsTheSession()
    {
        TokenResponse first = await ExchangeAsync();
        TokenResponse second = await _host.OAuth.RefreshAsync(_discovery, first.RefreshToken, CancellationToken.None);

        _host.Time.Advance(TimeSpan.FromSeconds(31));
        OAuthException failure = await Assert.ThrowsAsync<OAuthException>(
            () => _host.OAuth.RefreshAsync(_discovery, first.RefreshToken, CancellationToken.None));

        Assert.Equal("invalid_grant", failure.Error.Error);
        Assert.True(_host.Cloud.IsSessionRevoked(second.RefreshToken));
    }

    [Fact]
    public async Task ADeviceFlowStartsWithBothScopes()
    {
        DeviceAuthorization device = await _host.OAuth.StartDeviceAsync(_discovery, workspaceId: null, CancellationToken.None);

        RecordedRequest request = Assert.Single(_host.Cloud.RequestsTo(Device));
        Assert.Equal(
            new Dictionary<string, string> { ["client_id"] = "xping-cli", ["scope"] = "user:read offline_access" },
            request.Form);
        Assert.Matches("^ABCD-[0-9]{4}$", device.UserCode);
        Assert.Equal(new Uri(_host.Cloud.CloudUrl + "/device"), device.VerificationUri);
        Assert.Equal(new Uri(_host.Cloud.CloudUrl + "/device?user_code=" + device.UserCode), device.VerificationUriComplete);
        Assert.Equal(TimeSpan.FromMinutes(10), device.ExpiresIn);
        Assert.Equal(TimeSpan.FromSeconds(5), device.Interval);
        Assert.DoesNotContain(device.DeviceCode, device.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWorkspaceIsSentOnlyWhenGiven()
    {
        await _host.OAuth.StartDeviceAsync(_discovery, FakeCloud.WorkspaceId, CancellationToken.None);

        Assert.Equal(FakeCloud.WorkspaceId, Assert.Single(_host.Cloud.RequestsTo(Device)).Form["xping_workspace_id"]);
    }

    [Fact]
    public async Task TheIntervalDefaultsToFiveSeconds()
    {
        _host.Cloud.EditDeviceResponse = r =>
        {
            r.Remove("interval");
            r.Remove("verification_uri_complete");
        };

        DeviceAuthorization device = await _host.OAuth.StartDeviceAsync(_discovery, workspaceId: null, CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(5), device.Interval);
        Assert.Null(device.VerificationUriComplete);
    }

    [Theory]
    [InlineData("http://app.example.com/device")]
    [InlineData("https://user:pass@app.example.com/device")]
    [InlineData("javascript:alert(1)")]
    public async Task AVerificationPageOutsideTheServerUrlRuleIsRefused(string page)
    {
        _host.Cloud.EditDeviceResponse = r => r["verification_uri"] = page;

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(
            () => _host.OAuth.StartDeviceAsync(_discovery, workspaceId: null, CancellationToken.None));

        Assert.Contains("incomplete response", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACompleteVerificationPageOverPlainHttpIsDropped()
    {
        _host.Cloud.EditDeviceResponse = r => r["verification_uri_complete"] = "http://app.example.com/device?user_code=X";

        DeviceAuthorization device = await _host.OAuth.StartDeviceAsync(_discovery, workspaceId: null, CancellationToken.None);

        Assert.Null(device.VerificationUriComplete);
    }

    [Theory]
    [InlineData("device_code")]
    [InlineData("user_code")]
    [InlineData("verification_uri")]
    [InlineData("expires_in")]
    public async Task ADeviceResponseWithoutARequiredMemberIsRefused(string member)
    {
        _host.Cloud.EditDeviceResponse = r => r.Remove(member);

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(
            () => _host.OAuth.StartDeviceAsync(_discovery, workspaceId: null, CancellationToken.None));

        Assert.Contains("incomplete response", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PollingAnswersPendingUntilTheUserApproves()
    {
        DeviceAuthorization device = await _host.OAuth.StartDeviceAsync(_discovery, workspaceId: null, CancellationToken.None);

        DevicePollResult pending = await PollAsync(device);
        _host.Cloud.ApproveDevice(device.UserCode);
        DevicePollResult approved = await PollAsync(device);

        Assert.Equal(DevicePollStatus.Pending, pending.Status);
        Assert.Equal(DevicePollStatus.Completed, approved.Status);
        Assert.StartsWith("eyJ", approved.Token!.AccessToken, StringComparison.Ordinal);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["client_id"] = "xping-cli",
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["device_code"] = device.DeviceCode
            },
            _host.Cloud.RequestsTo(Token)[^1].Form);
    }

    [Fact]
    public async Task PollingTooFastAnswersSlowDown()
    {
        DeviceAuthorization device = await _host.OAuth.StartDeviceAsync(_discovery, workspaceId: null, CancellationToken.None);

        await _host.OAuth.PollDeviceAsync(_discovery, device.DeviceCode, CancellationToken.None);
        DevicePollResult result = await _host.OAuth.PollDeviceAsync(_discovery, device.DeviceCode, CancellationToken.None);

        Assert.Equal(DevicePollStatus.SlowDown, result.Status);
    }

    [Theory]
    [InlineData(false, "access_denied")]
    [InlineData(true, "expired_token")]
    public async Task ADeniedOrExpiredDeviceFlowEndsWithItsError(bool expire, string expected)
    {
        DeviceAuthorization device = await _host.OAuth.StartDeviceAsync(_discovery, workspaceId: null, CancellationToken.None);
        if (expire)
            _host.Time.Advance(device.ExpiresIn);
        else
            _host.Cloud.DenyDevice(device.UserCode);

        OAuthException failure = await Assert.ThrowsAsync<OAuthException>(() => PollAsync(device));

        Assert.Equal(expected, failure.Error.Error);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "temporarily_unavailable", "temporarily unavailable")]
    [InlineData(HttpStatusCode.InternalServerError, "server_error", "server error")]
    [InlineData(HttpStatusCode.BadGateway, null, "server error")]
    public async Task ATransientPollFailureIsReturnedWithoutARetry(HttpStatusCode status, string? error, string reason)
    {
        DeviceAuthorization device = await _host.OAuth.StartDeviceAsync(_discovery, workspaceId: null, CancellationToken.None);
        _host.Cloud.Fail(Token, 1, status, error);

        DevicePollResult result = await PollAsync(device);

        Assert.Equal(DevicePollStatus.Transient, result.Status);
        Assert.Equal(reason, result.Reason);
        Assert.Single(_host.Cloud.RequestsTo(Token));
    }

    [Fact]
    public async Task ANetworkFailureWhilePollingIsReturnedWithoutARetry()
    {
        DeviceAuthorization device = await _host.OAuth.StartDeviceAsync(_discovery, workspaceId: null, CancellationToken.None);
        _host.Cloud.Drop(Token, 1);

        DevicePollResult result = await PollAsync(device);

        Assert.Equal(DevicePollStatus.Transient, result.Status);
        Assert.Single(_host.Cloud.RequestsTo(Token));
    }

    [Fact]
    public async Task RevocationSendsTheRefreshTokenWithItsHint()
    {
        TokenResponse tokens = await ExchangeAsync();

        await _host.OAuth.RevokeAsync(_discovery, tokens.RefreshToken, CancellationToken.None);

        RecordedRequest request = Assert.Single(_host.Cloud.RequestsTo(Revoke));
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["client_id"] = "xping-cli",
                ["token"] = tokens.RefreshToken,
                ["token_type_hint"] = "refresh_token"
            },
            request.Form);
        Assert.Empty(request.Query);
        Assert.True(_host.Cloud.IsSessionRevoked(tokens.RefreshToken));
    }

    [Fact]
    public async Task AnEmptyRevocationAnswerIsAccepted()
    {
        _host.Cloud.Fail(Revoke, 1, HttpStatusCode.OK, error: null);

        await _host.OAuth.RevokeAsync(_discovery, "refresh-unknown-token", CancellationToken.None);
    }

    [Fact]
    public async Task AServerErrorIsRetriedOnceAfterTwoSeconds()
    {
        _host.Cloud.Fail(Token, 1, HttpStatusCode.InternalServerError, "server_error");

        await _host.DriveAsync(ExchangeAsync());

        AssertGaps(_host.Cloud.RequestsTo(Token), 2);
    }

    [Fact]
    public async Task ASecondServerErrorIsReportedAsUnreachable()
    {
        _host.Cloud.Fail(Token, 2, HttpStatusCode.InternalServerError, "server_error");

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => _host.DriveAsync(ExchangeAsync()));

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Equal($"Could not reach Xping Cloud at {_host.Cloud.CloudUrl}: server error.", failure.Message);
        Assert.Equal(2, _host.Cloud.RequestsTo(Token).Count);
    }

    [Fact]
    public async Task TemporarilyUnavailableIsRetriedAfterTwoFourAndEightSeconds()
    {
        _host.Cloud.Fail(Token, 3, HttpStatusCode.ServiceUnavailable, "temporarily_unavailable");

        await _host.DriveAsync(ExchangeAsync());

        AssertGaps(_host.Cloud.RequestsTo(Token), 2, 4, 8);
    }

    [Fact]
    public async Task AFourthTemporarilyUnavailableIsReportedAsUnreachable()
    {
        _host.Cloud.Fail(Token, 4, HttpStatusCode.ServiceUnavailable, error: null);

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => _host.DriveAsync(ExchangeAsync()));

        Assert.Equal($"Could not reach Xping Cloud at {_host.Cloud.CloudUrl}: temporarily unavailable.", failure.Message);
        Assert.Equal(4, _host.Cloud.RequestsTo(Token).Count);
    }

    [Fact]
    public async Task ANetworkFailureIsRetriedLikeAServerError()
    {
        TokenResponse first = await ExchangeAsync();
        _host.Cloud.Drop(Token, 1);

        await _host.DriveAsync(_host.OAuth.RefreshAsync(_discovery, first.RefreshToken, CancellationToken.None));

        AssertGaps(_host.Cloud.RequestsTo(Token).Skip(1).ToList(), 2);
    }

    [Fact]
    public async Task ALostCodeExchangeIsNotRetried()
    {
        // The server may have redeemed the code before the answer was lost; a retry would only
        // answer invalid_grant and hide the network failure.
        _host.Cloud.Drop(Token, 1);

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => _host.DriveAsync(ExchangeAsync()));

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Single(_host.Cloud.RequestsTo(Token));
    }

    [Fact]
    public async Task RevocationIsRetriedToo()
    {
        _host.Cloud.Fail(Revoke, 1, HttpStatusCode.ServiceUnavailable, "temporarily_unavailable");

        await _host.DriveAsync(_host.OAuth.RevokeAsync(_discovery, "refresh-unknown-token", CancellationToken.None));

        Assert.Equal(2, _host.Cloud.RequestsTo(Revoke).Count);
    }

    [Theory]
    [InlineData("invalid_request")]
    [InlineData("invalid_scope")]
    [InlineData("unauthorized_client")]
    [InlineData("a_future_error")]
    public async Task OtherErrorsAreReturnedOnTheFirstAnswer(string error)
    {
        _host.Cloud.Fail(Token, 1, HttpStatusCode.BadRequest, error);

        OAuthException failure = await Assert.ThrowsAsync<OAuthException>(() => ExchangeAsync());

        Assert.Equal(error, failure.Error.Error);
        Assert.Equal("Injected fault.", failure.Error.ErrorDescription);
        Assert.Single(_host.Cloud.RequestsTo(Token));
    }

    [Fact]
    public async Task AnUnknownClientIsReportedWithItsStatus()
    {
        _host.Cloud.Fail(Token, 1, HttpStatusCode.Unauthorized, "invalid_client");

        OAuthException failure = await Assert.ThrowsAsync<OAuthException>(() => ExchangeAsync());

        Assert.Equal("invalid_client", failure.Error.Error);
        Assert.Equal(401, failure.Error.StatusCode);
    }

    [Fact]
    public async Task ARedirectIsNeitherFollowedNorASuccess()
    {
        _discovery = _discovery with { TokenEndpoint = new Uri(_host.Cloud.CloudUrl + "/moved") };

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => ExchangeAsync());

        Assert.Equal(
            $"Could not reach Xping Cloud at {_host.Cloud.CloudUrl}: the token endpoint answered HTTP 302.",
            failure.Message);
        Assert.Empty(_host.Cloud.RequestsTo(Token));
    }

    [Fact]
    public async Task AnErrorStatusWithoutAnOAuthBodyIsUnexpected()
    {
        _host.Cloud.Fail(Token, 1, HttpStatusCode.NotFound, error: null);

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => ExchangeAsync());

        Assert.Contains("answered HTTP 404", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellingDuringARetryWaitStopsAtOnce()
    {
        _host.Cloud.Fail(Token, 1, HttpStatusCode.ServiceUnavailable, "temporarily_unavailable");
        using var cancellation = new CancellationTokenSource();
        string code = _host.Cloud.IssueCode(RedirectUri, Challenge(Verifier));

        Task<TokenResponse> exchange = _host.OAuth.ExchangeCodeAsync(_discovery, code, RedirectUri, Verifier, cancellation.Token);
        while (_host.Cloud.RequestsTo(Token).Count == 0)
            await Task.Delay(5);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => exchange);
        Assert.Single(_host.Cloud.RequestsTo(Token));
    }

    private async Task<TokenResponse> ExchangeAsync()
    {
        string code = _host.Cloud.IssueCode(RedirectUri, Challenge(Verifier));
        return await _host.OAuth.ExchangeCodeAsync(_discovery, code, RedirectUri, Verifier, CancellationToken.None)
            .ConfigureAwait(false);
    }

    private Task<DevicePollResult> PollAsync(DeviceAuthorization device)
    {
        // The fake answers slow_down to a poll inside the interval, as the Portal does.
        _host.Time.Advance(device.Interval);
        return _host.OAuth.PollDeviceAsync(_discovery, device.DeviceCode, CancellationToken.None);
    }

    // Exact: the fake clock moves only by the waits the client starts.
    private static void AssertGaps(IReadOnlyList<RecordedRequest> requests, params int[] seconds)
    {
        Assert.Equal(seconds.Length + 1, requests.Count);

        for (int i = 0; i < seconds.Length; i++)
            Assert.Equal(TimeSpan.FromSeconds(seconds[i]), requests[i + 1].ReceivedAt - requests[i].ReceivedAt);
    }

    private static string Challenge(string verifier) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
