/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Browser;
using Xping.Cli.Auth.Flows;
using Xping.Cli.Auth.Store;
using Xping.Cli.Tests.Cloud;

namespace Xping.Cli.Tests.Auth.Flows;

/// <summary>
/// The device flow's polling state machine against the fake Cloud (cli-auth-cli-spec §6, §18.1).
/// </summary>
public sealed class DeviceFlowTests : IAsyncLifetime, IAsyncDisposable
{
    private const string Token = "/connect/token";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly AuthTestHost _host = new();
    private readonly RecordingLauncher _browser = new();
    private string? _userCode;

    public Task InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    private BrowserEnvironment Environment { get; set; } = BrowserEnvironment.Interactive;

    [Fact]
    public async Task PollsAtTheIntervalUntilTheUserApproves()
    {
        _host.Cloud.Fail(Token, 2, HttpStatusCode.BadRequest, OAuthProtocol.AuthorizationPending);

        CredentialRecord record = await RunAsync(approve: true);

        Assert.Equal(FakeCloud.Email, record.Email);
        Assert.Equal(_host.Cloud.CloudUrl, record.CloudUrl);
        Assert.Equal([Interval, Interval, Interval], PollGaps());
    }

    [Fact]
    public async Task SlowDownAddsFiveSecondsEachTime()
    {
        _host.Cloud.Fail(Token, 2, HttpStatusCode.BadRequest, OAuthProtocol.SlowDown);

        await RunAsync(approve: true);

        Assert.Equal([Interval, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15)], PollGaps());
    }

    [Fact]
    public async Task ANetworkErrorKeepsTheInterval()
    {
        _host.Cloud.Fail(Token, 1, HttpStatusCode.ServiceUnavailable, OAuthProtocol.TemporarilyUnavailable);
        _host.Cloud.Drop(Token, 1);
        _host.Cloud.Fail(Token, 1, HttpStatusCode.InternalServerError, OAuthProtocol.ServerError);

        await RunAsync(approve: true);

        // One poll per wait: a transient failure is never retried at once.
        Assert.Equal([Interval, Interval, Interval, Interval], PollGaps());
    }

    [Fact]
    public async Task TheCodeExpiresAtTheDeadlineWithoutAnotherPoll()
    {
        _host.Cloud.EditDeviceResponse = r => r["expires_in"] = 12;

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => RunAsync(approve: false));

        Assert.Equal(AuthExitCodes.LoginTimedOut, failure.ExitCode);
        Assert.Equal(AuthErrorCodes.Timeout, failure.ErrorCode);
        Assert.Equal("The sign-in code expired. Run `xping login --device` again.", failure.Message);
        Assert.Equal(2, _host.Cloud.RequestsTo(Token).Count);
    }

    [Theory]
    [InlineData(OAuthProtocol.AccessDenied, AuthExitCodes.LoginDeclined, AuthErrorCodes.AccessDenied, "You declined the sign-in request. Nothing was stored.")]
    [InlineData(OAuthProtocol.ExpiredToken, AuthExitCodes.LoginTimedOut, AuthErrorCodes.Timeout, "The sign-in code expired. Run `xping login --device` again.")]
    [InlineData(OAuthProtocol.InvalidGrant, AuthExitCodes.LoginFailed, AuthErrorCodes.OAuthError, "The device code was rejected. Run `xping login --device` again.")]
    [InlineData("weird_error", AuthExitCodes.LoginFailed, AuthErrorCodes.OAuthError, "Xping Cloud returned weird_error: Injected fault.")]
    public async Task AnErrorThatEndsTheFlowHasItsMessage(string error, int exitCode, string errorCode, string message)
    {
        _host.Cloud.Fail(Token, 1, HttpStatusCode.BadRequest, error);

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => RunAsync(approve: false));

        Assert.Equal(exitCode, failure.ExitCode);
        Assert.Equal(errorCode, failure.ErrorCode);
        Assert.Equal(message, failure.Message);
        Assert.Single(_host.Cloud.RequestsTo(Token));
    }

    [Fact]
    public async Task AnUnknownClientAtTheStartPointsAtTheCloudUrl()
    {
        _host.Cloud.Fail("/connect/device", 1, HttpStatusCode.Unauthorized, OAuthProtocol.InvalidClient);

        AuthFailureException failure = await Assert.ThrowsAsync<AuthFailureException>(() => RunAsync(approve: false));

        Assert.Equal(AuthExitCodes.CloudUnreachable, failure.ExitCode);
        Assert.Contains("Xping Cloud did not recognise this CLI.", failure.Message, StringComparison.Ordinal);
        Assert.Empty(_host.Cloud.RequestsTo(Token));
    }

    [Fact]
    public async Task TheWorkspaceIsSentWithTheStart()
    {
        await RunAsync(approve: true, workspaceId: FakeCloud.WorkspaceId);

        Assert.Equal(FakeCloud.WorkspaceId, Assert.Single(_host.Cloud.RequestsTo("/connect/device")).Form["xping_workspace_id"]);
    }

    [Fact]
    public async Task TheBrowserOpensThePageWithTheCodeFilledIn()
    {
        await RunAsync(approve: true);

        Assert.Equal(new Uri($"{_host.Cloud.CloudUrl}/device?user_code={_userCode}"), Assert.Single(_browser.Opened));
    }

    [Fact]
    public async Task WithoutACompletePageTheBrowserOpensThePlainOne()
    {
        _host.Cloud.EditDeviceResponse = r => r.Remove("verification_uri_complete");

        await RunAsync(approve: true);

        Assert.Equal(new Uri(_host.Cloud.CloudUrl + "/device"), Assert.Single(_browser.Opened));
    }

    [Fact]
    public async Task NoBrowserIsOpenedWithNoBrowserOrOnAHeadlessMachine()
    {
        await RunAsync(approve: true, noBrowser: true);
        Environment = BrowserEnvironment.Headless("SSH session");
        await RunAsync(approve: true);

        Assert.Empty(_browser.Opened);
    }

    private async Task<CredentialRecord> RunAsync(bool approve, string? workspaceId = null, bool noBrowser = false)
    {
        var flow = new DeviceFlow(
            _host.Discovery,
            _host.OAuth,
            _browser,
            new FixedDetector(this),
            _host.Time,
            NullLogger<DeviceFlow>.Instance);

        return await _host.DriveAsync(flow.RunAsync(
            _host.Cloud.CloudUrl,
            workspaceId,
            noBrowser,
            authorization =>
            {
                Assert.Equal(Interval, authorization.Interval);
                _userCode = authorization.UserCode;
                if (approve)
                    _host.Cloud.ApproveDevice(authorization.UserCode);
            },
            CancellationToken.None)).ConfigureAwait(false);
    }

    /// <summary>
    /// The time from the device start to the first poll, then between polls.
    /// </summary>
    private TimeSpan[] PollGaps()
    {
        DateTimeOffset[] times =
        [
            Assert.Single(_host.Cloud.RequestsTo("/connect/device")).ReceivedAt,
            .. _host.Cloud.RequestsTo(Token).Select(r => r.ReceivedAt)
        ];

        return [.. times.Zip(times.Skip(1), (earlier, later) => later - earlier)];
    }

    private sealed class FixedDetector(DeviceFlowTests tests) : IHeadlessDetector
    {
        public BrowserEnvironment Detect() => tests.Environment;
    }

    private sealed class RecordingLauncher : IBrowserLauncher
    {
        public List<Uri> Opened { get; } = [];

        public bool TryOpen(Uri url, BrowserEnvironment environment)
        {
            Opened.Add(url);
            return true;
        }
    }
}
