/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Web;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Browser;
using Xping.Cli.Auth.Flows;
using Xping.Cli.Auth.Loopback;
using Xping.Cli.Auth.Store;
using Xping.Cli.Hosting;
using Xping.Cli.Tests.Cloud;

namespace Xping.Cli.Tests.Commands.Auth;

/// <summary>
/// <c>xping login</c> through the loopback flow, end to end against the fake Cloud
/// (cli-auth-cli-spec §3.2, §4, §18.2).
/// </summary>
public sealed class LoginCommandTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly CliFlowHost _host = new();

    public Task InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public void Login_Loopback_Success()
    {
        CliFlowHost host = _host;

        CliResult result = host.Run("login", "--json");

        Assert.True(result.Code == 0, result.Error);

        CredentialRecord record = Assert.IsType<CredentialRecord>(host.StoredRecord());
        Assert.Equal(host.Cloud.CloudUrl, record.CloudUrl);
        Assert.Equal(FakeCloud.Email, record.Email);
        Assert.Equal(FakeCloud.UserId, record.Sub);
        Assert.Equal(FakeCloud.WorkspaceId, record.WorkspaceId);
        Assert.NotNull(record.Sid);
        Assert.NotNull(record.AccessToken);
        Assert.Equal(host.Time.GetUtcNow() + TimeSpan.FromMinutes(15), record.AccessTokenExpiresAt);
        Assert.Equal(host.Cloud.CloudUrl + "/gw", record.DataGatewayUri);
        Assert.Equal(host.Time.GetUtcNow(), record.StoredAt);

        Assert.Contains($"Opening your browser to sign in to Xping Cloud ({host.Cloud.CloudUrl}).", result.Error, StringComparison.Ordinal);
        Assert.Contains($"Signed in as {FakeCloud.Email}", result.Error, StringComparison.Ordinal);
        Assert.Contains($"Workspace  {FakeCloud.WorkspaceId}", result.Error, StringComparison.Ordinal);
        Assert.Contains($"Session    ...{record.Sid![^6..]}", result.Error, StringComparison.Ordinal);
        Assert.Contains("Stored in  ", result.Error, StringComparison.Ordinal);

        JsonElement json = result.Json();
        Assert.Equal("1", json.GetProperty("schemaVersion").GetString());
        Assert.Equal("signed-in", json.GetProperty("result").GetString());
        Assert.Equal(host.Cloud.CloudUrl, json.GetProperty("cloudUrl").GetString());
        Assert.Equal("loopback", json.GetProperty("flow").GetString());
        Assert.Equal(FakeCloud.Email, json.GetProperty("email").GetString());
        Assert.Equal(FakeCloud.UserId, json.GetProperty("sub").GetString());
        Assert.Equal(FakeCloud.WorkspaceId, json.GetProperty("workspaceId").GetString());
        Assert.Equal(record.Sid, json.GetProperty("sessionId").GetString());
        Assert.Equal("file", json.GetProperty("store").GetString());
        Assert.Equal("2026-09-29T10:15:00Z", json.GetProperty("accessTokenExpiresAt").GetString());

        // Nothing secret in either stream.
        foreach (string output in new[] { result.Output, result.Error })
        {
            Assert.DoesNotContain(record.RefreshToken, output, StringComparison.Ordinal);
            Assert.DoesNotContain(record.AccessToken!, output, StringComparison.Ordinal);
            Assert.DoesNotContain("eyJ", output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheLinkIsPrintedOnceAndTheBrowserIsOpenedWithIt()
    {
        CliFlowHost host = _host;

        CliResult result = host.Run("login");

        Uri opened = Assert.Single(host.Browser.Opened);
        Assert.Equal(1, Occurrences(result.Error, opened.AbsoluteUri));
        Assert.Empty(result.Output);
        Assert.Contains("Waiting for you to finish in the browser (up to 5 minutes)...", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAuthorizationRequestCarriesTheContractParameters()
    {
        CliFlowHost host = _host;

        host.Run("login");

        var query = HttpUtility.ParseQueryString(Assert.Single(host.Browser.Opened).Query);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("xping-cli", query["client_id"]);
        Assert.Equal("user:read offline_access", query["scope"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Matches(@"^http://127\.0\.0\.1:\d+/callback$", query["redirect_uri"]);
        Assert.Equal(43, query["state"]!.Length);

        // The token request proves possession of the verifier behind the challenge, with the same
        // redirect URI, and the access token never goes to the Portal.
        RecordedRequest exchange = Assert.Single(host.Cloud.RequestsTo("/connect/token"));
        Assert.Equal("authorization_code", exchange.Form["grant_type"]);
        Assert.Equal(query["redirect_uri"], exchange.Form["redirect_uri"]);
        Assert.Equal(query["code_challenge"], Pkce.Challenge(exchange.Form["code_verifier"]));
        Assert.All(host.Cloud.Requests, r => Assert.False(r.Headers.ContainsKey("Authorization")));
    }

    [Fact]
    public async Task TheBrowserPagesNeverShowTheCodeOrState()
    {
        CliFlowHost host = _host;

        host.Run("login");
        await host.Browser.Played.ConfigureAwait(true);

        var query = HttpUtility.ParseQueryString(Assert.Single(host.Browser.Opened).Query);
        (HttpStatusCode status, string body) = Assert.Single(host.Browser.Callbacks);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Signed in to Xping", body, StringComparison.Ordinal);
        Assert.DoesNotContain(query["state"]!, body, StringComparison.Ordinal);
        Assert.DoesNotContain("code-", body, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Cloud.CloudUrl, body, StringComparison.Ordinal);
    }

    [Fact]
    public void Login_Loopback_Denied()
    {
        CliFlowHost host = _host;
        host.Cloud.QueueAuthorize(AuthorizeScript.Deny);

        CliResult result = host.Run("login", "--json");

        Assert.Equal(AuthExitCodes.LoginDeclined, result.Code);
        Assert.Contains("You declined the sign-in request. Nothing was stored.", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(host.CredentialsFile));
        Assert.Empty(host.Cloud.RequestsTo("/connect/token"));

        JsonElement json = result.Json();
        Assert.Equal("failed", json.GetProperty("result").GetString());
        Assert.Equal("access_denied", json.GetProperty("error").GetString());
    }

    [Fact]
    public void ACallbackErrorOtherThanDenialFailsWithTheServersCode()
    {
        CliFlowHost host = _host;
        host.Cloud.QueueAuthorize(AuthorizeScript.ServerError);

        CliResult result = host.Run("login", "--json");

        Assert.Equal(AuthExitCodes.LoginFailed, result.Code);
        Assert.Contains("Xping Cloud returned server_error: Something broke.", result.Error, StringComparison.Ordinal);

        JsonElement json = result.Json();
        Assert.Equal("oauth_error", json.GetProperty("error").GetString());
        Assert.Equal("server_error", json.GetProperty("oauthError").GetString());

        // The page shows the error code, never the description a redirect could have written.
        (_, string body) = Assert.Single(host.Browser.Callbacks);
        Assert.Contains("server_error", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Something broke", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_Loopback_Timeout()
    {
        CliFlowHost host = _host;
        host.Cloud.QueueAuthorize(AuthorizeScript.NoRedirect);

        Task<CliResult> run = host.Start("login", "--json");
        await host.WaitForTimerAsync(LoopbackFlow.Timeout).ConfigureAwait(true);
        await host.Browser.Played.ConfigureAwait(true);
        host.Time.Advance(LoopbackFlow.Timeout);
        CliResult result = await run.ConfigureAwait(true);

        Assert.Equal(AuthExitCodes.LoginTimedOut, result.Code);
        Assert.Contains("No sign-in arrived within 5 minutes.", result.Error, StringComparison.Ordinal);
        Assert.Contains("xping login --device", result.Error, StringComparison.Ordinal);
        Assert.Equal("timeout", result.Json().GetProperty("error").GetString());
        Assert.False(File.Exists(host.CredentialsFile));
        AssertListenerClosed(host);
    }

    [Fact]
    public async Task Login_Loopback_StateMismatch_ThenGenuine()
    {
        CliFlowHost host = _host;
        host.Cloud.QueueAuthorize(AuthorizeScript.WrongState, AuthorizeScript.Approve);
        host.Browser.Visits = 2;

        CliResult result = host.Run("login");
        await host.Browser.Played.ConfigureAwait(true);

        Assert.True(result.Code == 0, result.Error);
        Assert.Equal([HttpStatusCode.BadRequest, HttpStatusCode.OK], host.Browser.Callbacks.Select(c => c.Status));

        // Only the genuine code was exchanged; the one that came back with the wrong state was not.
        Assert.Single(host.Cloud.RequestsTo("/connect/token"));
        Assert.NotNull(host.StoredRecord());
    }

    [Fact]
    public async Task Login_Loopback_StateMismatch_Only()
    {
        CliFlowHost host = _host;
        host.Cloud.QueueAuthorize(AuthorizeScript.WrongState);

        Task<CliResult> run = host.Start("login");
        await host.WaitForTimerAsync(LoopbackFlow.Timeout).ConfigureAwait(true);
        await host.Browser.Played.ConfigureAwait(true);
        host.Time.Advance(LoopbackFlow.Timeout);
        CliResult result = await run.ConfigureAwait(true);

        Assert.Equal(AuthExitCodes.LoginTimedOut, result.Code);
        Assert.Equal(HttpStatusCode.BadRequest, Assert.Single(host.Browser.Callbacks).Status);
        Assert.Empty(host.Cloud.RequestsTo("/connect/token"));
        Assert.Null(host.StoredRecord());
    }

    [Fact]
    public void Login_Discovery_IssuerMismatch()
    {
        CliFlowHost host = _host;
        host.Cloud.EditDiscovery = d => d["issuer"] = "https://evil.example/";

        CliResult result = host.Run("login", "--json");

        Assert.Equal(AuthExitCodes.CloudUnreachable, result.Code);
        Assert.Equal("cloud_unreachable", result.Json().GetProperty("error").GetString());
        AssertNoSignInStarted(host);
    }

    [Fact]
    public void Login_Discovery_ContractVersion()
    {
        CliFlowHost host = _host;
        host.Cloud.EditDiscovery = d => d["xping_contract_version"] = 2;

        CliResult result = host.Run("login", "--json");

        Assert.Equal(AuthExitCodes.CloudVersionMismatch, result.Code);
        Assert.Equal("version_mismatch", result.Json().GetProperty("error").GetString());
        AssertNoSignInStarted(host);
    }

    [Fact]
    public void Login_Discovery_MinCliVersion()
    {
        CliFlowHost host = _host;
        host.Cloud.EditDiscovery = d => d["xping_cli_min_version"] = "999.0.0";

        CliResult result = host.Run("login");

        Assert.Equal(AuthExitCodes.CloudVersionMismatch, result.Code);
        Assert.Contains("Please upgrade the xping CLI", result.Error, StringComparison.Ordinal);
        AssertNoSignInStarted(host);
    }

    [Fact]
    public void LoginAlwaysFetchesAFreshDiscoveryDocument()
    {
        CliFlowHost host = _host;
        host.SignIn();

        host.Cloud.EditDiscovery = d => d["xping_cli_min_version"] = "999.0.0";
        CliResult result = host.Run("login");

        Assert.Equal(AuthExitCodes.CloudVersionMismatch, result.Code);
    }

    [Fact]
    public void Login_NoTty()
    {
        CliFlowHost host = _host;
        host.IsTerminal = false;

        CliResult result = host.Run("login", "--json");

        Assert.Equal(AuthExitCodes.InteractiveRequired, result.Code);
        Assert.Contains("xping login needs an interactive terminal.", result.Error, StringComparison.Ordinal);
        Assert.Equal("interactive_required", result.Json().GetProperty("error").GetString());
        Assert.Empty(host.Cloud.Requests);
        Assert.Empty(host.Browser.Opened);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData("YES")]
    public void Login_CiSet(string value)
    {
        CliFlowHost host = _host;
        host.Environment["CI"] = value;

        CliResult result = host.Run("login");

        Assert.Equal(AuthExitCodes.InteractiveRequired, result.Code);
        Assert.Empty(host.Cloud.Requests);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("")]
    public void ACiValueThatIsNotTruthyDoesNotBlockTheSignIn(string value)
    {
        CliFlowHost host = _host;
        host.Environment["CI"] = value;

        Assert.Equal(0, host.Run("login").Code);
    }

    [Fact]
    public void Login_WithApiKeySet_Proceeds()
    {
        CliFlowHost host = _host;
        host.Environment["XPING_APIKEY"] = "xpk_ambient_upload_key";

        CliResult login = host.Run("login");

        Assert.Equal(0, login.Code);
        Assert.DoesNotContain("API key", login.Error, StringComparison.Ordinal);

        JsonElement status = host.Run("auth", "status", "--json").Json();
        Assert.Equal("stored-login", status.GetProperty("credential").GetString());
        Assert.Equal("env", status.GetProperty("fallbackApiKey").GetString());
    }

    [Fact]
    public void VerboseNotesTheApiKeyWithoutPrintingIt()
    {
        CliFlowHost host = _host;
        host.Environment["XPING_APIKEY"] = "xpk_ambient_upload_key";

        CliResult login = host.Run("login", "--verbose");

        Assert.Equal(0, login.Code);
        Assert.Contains("An API key is also set (XPING_APIKEY)", login.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("xpk_ambient_upload_key", login.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AHeadlessMachinePrintsTheLinkWithoutOpeningABrowser()
    {
        CliFlowHost host = _host;
        host.BrowserEnvironment = BrowserEnvironment.Headless("SSH session");
        host.Environment["NO_COLOR"] = "1";

        Task<CliResult> run = host.Start("login");
        await host.WaitForTimerAsync(LoopbackFlow.Timeout).ConfigureAwait(true);
        host.Time.Advance(LoopbackFlow.Timeout);
        CliResult result = await run.ConfigureAwait(true);

        Assert.Empty(host.Browser.Opened);
        Assert.Contains("No browser was found on this machine. Open this link in a browser on this machine:", result.Error, StringComparison.Ordinal);
        Assert.Contains("press Ctrl+C and run `xping login --device`", result.Error, StringComparison.Ordinal);
        Assert.Contains("/connect/authorize?", result.Error, StringComparison.Ordinal);
        Assert.Equal(AuthExitCodes.LoginTimedOut, result.Code);
    }

    [Fact]
    public void ABrowserThatDoesNotOpenChangesNothing()
    {
        CliFlowHost host = _host;
        host.Browser.Opens = false;

        Assert.Equal(0, host.Run("login").Code);
    }

    [Fact]
    public void ARejectedCodeFailsWithTheReloginMessage()
    {
        CliFlowHost host = _host;
        host.Cloud.Fail("/connect/token", 1, HttpStatusCode.BadRequest, "invalid_grant");

        CliResult result = host.Run("login", "--json");

        Assert.Equal(AuthExitCodes.LoginFailed, result.Code);
        Assert.Contains("The sign-in code was rejected (expired or already used).", result.Error, StringComparison.Ordinal);
        Assert.Equal("invalid_grant", result.Json().GetProperty("oauthError").GetString());
        Assert.Null(host.StoredRecord());
    }

    [Fact]
    public void AnUnknownClientPointsAtTheCloudUrl()
    {
        CliFlowHost host = _host;
        host.Cloud.Fail("/connect/token", 1, HttpStatusCode.Unauthorized, "invalid_client");

        CliResult result = host.Run("login");

        Assert.Equal(AuthExitCodes.CloudUnreachable, result.Code);
        Assert.Contains("Xping Cloud did not recognise this CLI.", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void CancellingWritesTheJsonDocumentAndStoresNothing()
    {
        CliFlowHost host = _host;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        CliResult result = host.Run(new CancelKeyHandler(cancellation), "login", "--json");

        Assert.Equal(AuthExitCodes.Cancelled, result.Code);
        Assert.Equal("cancelled", result.Json().GetProperty("error").GetString());
        Assert.Empty(host.Cloud.Requests);
    }

    [Fact]
    public void ASecondSignInReplacesTheFirst()
    {
        CliFlowHost host = _host;
        CredentialRecord first = host.SignIn();

        CredentialRecord second = host.SignIn();

        Assert.NotEqual(first.Sid, second.Sid);
        Assert.Equal(second.RefreshToken, host.StoredRecord()!.RefreshToken);
    }

    [Fact]
    public void AServerMessageIsScrubbedBeforeItIsShown()
    {
        CliFlowHost host = _host;
        host.Cloud.Fail("/connect/token", 1, HttpStatusCode.BadRequest, "invalid_request", "bad form code_verifier=SECRETVALUE&x=1");

        CliResult result = host.Run("login", "--json");

        Assert.Equal(AuthExitCodes.LoginFailed, result.Code);
        Assert.Contains("code_verifier=[redacted]", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRETVALUE", result.Error + result.Output, StringComparison.Ordinal);
        Assert.Contains("[redacted]", result.Json().GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ARejectedRequestWithoutADescriptionReadsCleanly()
    {
        CliFlowHost host = _host;
        host.Cloud.Fail("/connect/token", 1, HttpStatusCode.BadRequest, "invalid_request", string.Empty);

        CliResult result = host.Run("login");

        Assert.Contains("Xping Cloud rejected the request (invalid_request). This is a CLI or server bug", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFailureDocumentNamesTheConfiguredCloudUrl()
    {
        CliFlowHost host = _host;
        host.PassCloudUrl = false;
        host.Environment["XPING_CLOUDURL"] = host.Cloud.CloudUrl;
        host.Environment["CI"] = "true";

        CliResult result = host.Run("login", "--json");

        Assert.Equal(AuthExitCodes.InteractiveRequired, result.Code);
        Assert.Equal(host.Cloud.CloudUrl, result.Json().GetProperty("cloudUrl").GetString());
    }

    private static void AssertNoSignInStarted(CliFlowHost host)
    {
        Assert.Empty(host.Cloud.RequestsTo("/connect/authorize"));
        Assert.Empty(host.Browser.Opened);
        Assert.False(File.Exists(host.CredentialsFile));
    }

    private static void AssertListenerClosed(CliFlowHost host)
    {
        var redirect = new Uri(HttpUtility.ParseQueryString(Assert.Single(host.Browser.Opened).Query)["redirect_uri"]!);

        using var client = new TcpClient();
        Assert.ThrowsAny<SocketException>(() => client.Connect(IPAddress.Loopback, redirect.Port));
    }

    private static int Occurrences(string text, string value)
    {
        int count = 0;
        for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
            count++;

        return count;
    }
}
