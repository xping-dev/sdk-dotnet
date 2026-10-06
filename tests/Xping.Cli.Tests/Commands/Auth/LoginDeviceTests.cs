/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using System.Text.Json;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Browser;
using Xping.Cli.Auth.Store;
using Xping.Cli.Commands.Auth;
using Xping.Cli.Hosting;
using Xping.Cli.Tests.Cloud;

namespace Xping.Cli.Tests.Commands.Auth;

/// <summary>
/// <c>xping login --device</c>, end to end against the fake Cloud (cli-auth-cli-spec §3.2, §6,
/// §18.2).
/// </summary>
public sealed class LoginDeviceTests : IAsyncLifetime, IAsyncDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    private readonly CliFlowHost _host = new();

    public Task InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task Login_Device_Success()
    {
        CliFlowHost host = _host;
        host.Environment["NO_COLOR"] = "1";

        Task<CliResult> run = host.Start("login", "--device", "--json");
        await host.WaitForTimerAsync(Interval).ConfigureAwait(true);
        string userCode = Assert.Single(host.Cloud.DeviceUserCodes);
        host.Cloud.ApproveDevice(userCode);
        host.Time.Advance(Interval);
        CliResult result = await run.ConfigureAwait(true);

        Assert.True(result.Code == 0, result.Error);

        CredentialRecord record = Assert.IsType<CredentialRecord>(host.StoredRecord());
        Assert.Equal(FakeCloud.Email, record.Email);
        Assert.Equal(FakeCloud.WorkspaceId, record.WorkspaceId);
        Assert.Equal(host.Cloud.CloudUrl + "/gw", record.DataGatewayUri);

        Assert.Contains($"To sign in, open  {host.Cloud.CloudUrl}/device", result.Error, StringComparison.Ordinal);
        Assert.Contains($"and enter the code  {userCode}", result.Error, StringComparison.Ordinal);
        Assert.Contains($"(or open {host.Cloud.CloudUrl}/device?user_code={userCode})", result.Error, StringComparison.Ordinal);
        Assert.Contains("Waiting for you to approve in the browser (up to 10 minutes)...", result.Error, StringComparison.Ordinal);
        Assert.Contains($"Signed in as {FakeCloud.Email}", result.Error, StringComparison.Ordinal);

        JsonElement json = result.Json();
        Assert.Equal("signed-in", json.GetProperty("result").GetString());
        Assert.Equal("device", json.GetProperty("flow").GetString());
        Assert.Equal(FakeCloud.WorkspaceId, json.GetProperty("workspaceId").GetString());

        // The browser gets the page with the code filled in; the device code is never shown.
        Assert.Equal(new Uri($"{host.Cloud.CloudUrl}/device?user_code={userCode}"), Assert.Single(host.Browser.Opened));
        string deviceCode = host.Cloud.RequestsTo("/connect/token")[^1].Form["device_code"];
        foreach (string output in new[] { result.Output, result.Error })
        {
            Assert.DoesNotContain(deviceCode, output, StringComparison.Ordinal);
            Assert.DoesNotContain(record.RefreshToken, output, StringComparison.Ordinal);
            Assert.DoesNotContain("eyJ", output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ACloudThatCannotBeReachedWhilePollingIsReportedOnce()
    {
        CliFlowHost host = _host;
        host.Environment["NO_COLOR"] = "1";
        host.Cloud.Drop("/connect/token", 2);

        Task<CliResult> run = host.Start("login", "--device");
        for (int poll = 0; poll < 2; poll++)
        {
            await host.WaitForTimerAsync(Interval).ConfigureAwait(true);
            host.Time.Advance(Interval);
        }

        await host.WaitForTimerAsync(Interval).ConfigureAwait(true);
        host.Cloud.ApproveDevice(Assert.Single(host.Cloud.DeviceUserCodes));
        host.Time.Advance(Interval);
        CliResult result = await run.ConfigureAwait(true);

        Assert.True(result.Code == 0, result.Error);
        const string Warning = "Could not reach Xping Cloud (";
        int first = result.Error.IndexOf(Warning, StringComparison.Ordinal);
        Assert.True(first >= 0, result.Error);
        Assert.Equal(-1, result.Error.IndexOf(Warning, first + 1, StringComparison.Ordinal));
        Assert.Contains("Still waiting; an approval made in the meantime is not lost.", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AControlCharacterInAServerErrorNeverReachesTheTerminal()
    {
        CliFlowHost host = _host;

        // Without colour, any escape character left in the output came from the server.
        host.Environment["NO_COLOR"] = "1";
        host.Cloud.Fail("/connect/token", 1, HttpStatusCode.BadRequest, "odd\u001b[2J", "x\u001b]52;c;eA==\u0007");

        Task<CliResult> run = host.Start("login", "--device", "--json");
        await host.WaitForTimerAsync(Interval).ConfigureAwait(true);
        host.Time.Advance(Interval);
        CliResult result = await run.ConfigureAwait(true);

        Assert.Equal(AuthExitCodes.LoginFailed, result.Code);
        Assert.DoesNotContain('\u001b', result.Error);
        Assert.DoesNotContain('\u001b', result.Output);
        Assert.Equal("odd[2J", result.Json().GetProperty("oauthError").GetString());
    }

    [Theory]
    [InlineData(600, "10 minutes")]
    [InlineData(60, "1 minute")]
    [InlineData(90, "90 seconds")]
    [InlineData(20, "20 seconds")]
    public void TheWaitingLineNamesTheLifetimeWithoutRoundingItAway(int seconds, string expected) =>
        Assert.Equal(expected, LoginCommand.Lifetime(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public async Task Login_Device_SlowDown()
    {
        CliFlowHost host = _host;
        host.Cloud.Fail("/connect/token", 1, HttpStatusCode.BadRequest, OAuthProtocol.SlowDown);

        Task<CliResult> run = host.Start("login", "--device");
        await host.WaitForTimerAsync(Interval).ConfigureAwait(true);
        host.Time.Advance(Interval);

        // The next wait is the interval plus five seconds.
        TimeSpan slower = Interval + TimeSpan.FromSeconds(5);
        await host.WaitForTimerAsync(slower).ConfigureAwait(true);
        host.Cloud.ApproveDevice(Assert.Single(host.Cloud.DeviceUserCodes));
        host.Time.Advance(slower);
        CliResult result = await run.ConfigureAwait(true);

        Assert.True(result.Code == 0, result.Error);
        Assert.Equal(2, host.Cloud.RequestsTo("/connect/token").Count);
        Assert.NotNull(host.StoredRecord());
    }

    [Fact]
    public async Task Login_Device_Denied()
    {
        CliFlowHost host = _host;

        Task<CliResult> run = host.Start("login", "--device", "--json");
        await host.WaitForTimerAsync(Interval).ConfigureAwait(true);
        host.Cloud.DenyDevice(Assert.Single(host.Cloud.DeviceUserCodes));
        host.Time.Advance(Interval);
        CliResult result = await run.ConfigureAwait(true);

        Assert.Equal(AuthExitCodes.LoginDeclined, result.Code);
        Assert.Contains("You declined the sign-in request. Nothing was stored.", result.Error, StringComparison.Ordinal);
        Assert.Equal("access_denied", result.Json().GetProperty("error").GetString());
        Assert.False(File.Exists(host.CredentialsFile));
    }

    [Fact]
    public async Task Login_Device_Expired()
    {
        CliFlowHost host = _host;
        host.Cloud.EditDeviceResponse = r => r["expires_in"] = 5;

        Task<CliResult> run = host.Start("login", "--device", "--json");
        await host.WaitForTimerAsync(Interval).ConfigureAwait(true);
        host.Time.Advance(Interval);
        CliResult result = await run.ConfigureAwait(true);

        Assert.Equal(AuthExitCodes.LoginTimedOut, result.Code);
        Assert.Contains("The sign-in code expired. Run `xping login --device` again.", result.Error, StringComparison.Ordinal);
        Assert.Equal("timeout", result.Json().GetProperty("error").GetString());
        Assert.Empty(host.Cloud.RequestsTo("/connect/token"));
        Assert.False(File.Exists(host.CredentialsFile));
    }

    [Fact]
    public async Task Login_Device_ServerExpired()
    {
        CliFlowHost host = _host;
        host.Cloud.Fail("/connect/token", 1, HttpStatusCode.BadRequest, OAuthProtocol.ExpiredToken);

        Task<CliResult> run = host.Start("login", "--device");
        await host.WaitForTimerAsync(Interval).ConfigureAwait(true);
        host.Time.Advance(Interval);
        CliResult result = await run.ConfigureAwait(true);

        Assert.Equal(AuthExitCodes.LoginTimedOut, result.Code);
        Assert.Contains("The sign-in code expired.", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_Device_CancelledWhilePolling()
    {
        CliFlowHost host = _host;
        using var cancellation = new CancellationTokenSource();

        Task<CliResult> run = Task.Run(() => host.Run(new CancelKeyHandler(cancellation), "login", "--device", "--json"));
        await host.WaitForTimerAsync(Interval).ConfigureAwait(true);
        await cancellation.CancelAsync().ConfigureAwait(true);
        CliResult result = await run.ConfigureAwait(true);

        Assert.Equal(AuthExitCodes.Cancelled, result.Code);
        Assert.Equal("cancelled", result.Json().GetProperty("error").GetString());
        Assert.Empty(host.Cloud.RequestsTo("/connect/token"));
        Assert.False(File.Exists(host.CredentialsFile));
    }

    [Fact]
    public async Task NoBrowserIsOpenedWithNoBrowser()
    {
        CliFlowHost host = _host;

        CliResult result = await SignInAsync(host, "login", "--device", "--no-browser").ConfigureAwait(true);

        Assert.True(result.Code == 0, result.Error);
        Assert.Empty(host.Browser.Opened);
    }

    [Fact]
    public async Task NoBrowserIsOpenedOnAHeadlessMachine()
    {
        CliFlowHost host = _host;
        host.BrowserEnvironment = BrowserEnvironment.Headless("SSH session");

        CliResult result = await SignInAsync(host, "login", "--device").ConfigureAwait(true);

        Assert.True(result.Code == 0, result.Error);
        Assert.Empty(host.Browser.Opened);
    }

    [Fact]
    public async Task TheWorkspaceIsSentWithTheStart()
    {
        CliFlowHost host = _host;

        CliResult result = await SignInAsync(host, "login", "--device", "--workspace", FakeCloud.WorkspaceId).ConfigureAwait(true);

        Assert.True(result.Code == 0, result.Error);
        Assert.Equal(FakeCloud.WorkspaceId, Assert.Single(host.Cloud.RequestsTo("/connect/device")).Form["xping_workspace_id"]);
    }

    [Fact]
    public void TheDeviceFlowStillNeedsAnInteractiveTerminal()
    {
        CliFlowHost host = _host;
        host.IsTerminal = false;

        CliResult result = host.Run("login", "--device", "--json");

        Assert.Equal(AuthExitCodes.InteractiveRequired, result.Code);
        Assert.Empty(host.Cloud.Requests);
    }

    private static async Task<CliResult> SignInAsync(CliFlowHost host, params string[] args)
    {
        Task<CliResult> run = host.Start(args);
        await host.WaitForTimerAsync(Interval).ConfigureAwait(true);
        host.Cloud.ApproveDevice(Assert.Single(host.Cloud.DeviceUserCodes));
        host.Time.Advance(Interval);
        return await run.ConfigureAwait(true);
    }
}
