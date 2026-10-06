/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Store;
using Xping.Cli.Tests.Cloud;

namespace Xping.Cli.Tests.Commands.Auth;

/// <summary>
/// <c>xping auth status</c>: every credential state, and never a request (cli-auth-cli-spec §3.4).
/// </summary>
public sealed class AuthStatusCommandTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly CliFlowHost _host = new();

    public Task InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public void AuthStatus_None()
    {
        CliResult result = _host.Run("auth", "status", "--json");

        Assert.Equal(AuthExitCodes.AuthRequired, result.Code);
        Assert.Contains("Credential    none", result.Error, StringComparison.Ordinal);
        Assert.Contains("Run `xping login` to sign in.", result.Error, StringComparison.Ordinal);
        Assert.Empty(_host.Cloud.Requests);

        JsonElement json = result.Json();
        Assert.Equal("1", json.GetProperty("schemaVersion").GetString());
        Assert.Equal(_host.Cloud.CloudUrl, json.GetProperty("cloudUrl").GetString());
        Assert.Equal("none", json.GetProperty("credential").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("credentialSource").ValueKind);
        Assert.False(json.GetProperty("loggedIn").GetBoolean());
        Assert.Equal(0, json.GetProperty("warnings").GetArrayLength());
    }

    [Fact]
    public void AuthStatus_StoredLogin()
    {
        CredentialRecord record = _host.SignIn();
        int requests = _host.Cloud.Requests.Count;
        _host.Time.Advance(TimeSpan.FromMinutes(3));

        CliResult result = _host.Run("auth", "status", "--json");

        Assert.Equal(0, result.Code);
        Assert.Equal(requests, _host.Cloud.Requests.Count);
        Assert.Contains("Credential    stored login (", result.Error, StringComparison.Ordinal);
        Assert.Contains($"Signed in     {FakeCloud.Email}", result.Error, StringComparison.Ordinal);
        Assert.Contains($"Workspace     {FakeCloud.WorkspaceId}", result.Error, StringComparison.Ordinal);
        Assert.Contains($"Session       ...{record.Sid![^6..]}", result.Error, StringComparison.Ordinal);
        Assert.Contains("Access token  expires in 12 minutes (refreshed automatically)", result.Error, StringComparison.Ordinal);

        JsonElement json = result.Json();
        Assert.Equal("stored-login", json.GetProperty("credential").GetString());
        Assert.Equal("file", json.GetProperty("credentialSource").GetString());
        Assert.True(json.GetProperty("loggedIn").GetBoolean());
        Assert.Equal(FakeCloud.Email, json.GetProperty("email").GetString());
        Assert.Equal(FakeCloud.UserId, json.GetProperty("sub").GetString());
        Assert.Equal(FakeCloud.WorkspaceId, json.GetProperty("workspaceId").GetString());
        Assert.Equal(record.Sid, json.GetProperty("sessionId").GetString());
        Assert.Equal("2026-09-29T10:15:00Z", json.GetProperty("accessTokenExpiresAt").GetString());
        Assert.Equal("2026-09-29T10:00:00Z", json.GetProperty("storedAt").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("fallbackApiKey").ValueKind);
        Assert.False(json.GetProperty("shadowedLogin").GetBoolean());

        Assert.DoesNotContain(record.RefreshToken, result.Output + result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(record.AccessToken!, result.Output + result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthStatus_ExpiredAccessToken()
    {
        _host.SignIn();
        _host.Time.Advance(TimeSpan.FromMinutes(20));

        CliResult result = _host.Run("auth", "status");

        Assert.Equal(0, result.Code);
        Assert.Contains("Access token  expired (refreshed automatically on next use)", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthStatus_StoredLoginWithEnvKey()
    {
        _host.SignIn();
        _host.Environment["XPING_APIKEY"] = "xpk_ambient_upload_key";

        CliResult result = _host.Run("auth", "status", "--json");

        Assert.Equal(0, result.Code);
        Assert.Contains("API key (XPING_APIKEY) also set; used only when no login is available.", result.Error, StringComparison.Ordinal);
        Assert.Equal("stored-login", result.Json().GetProperty("credential").GetString());
        Assert.Equal("env", result.Json().GetProperty("fallbackApiKey").GetString());
        Assert.DoesNotContain("xpk_ambient_upload_key", result.Output + result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthStatus_ApiKeyFlagShadowsTheLogin()
    {
        _host.SignIn();

        CliResult result = _host.Run("auth", "status", "--json", "--api-key", "xpk_typed_key");

        Assert.Equal(0, result.Code);
        Assert.Contains("Credential    API key (--api-key)", result.Error, StringComparison.Ordinal);
        Assert.Contains("A stored login also exists and is not used while --api-key is given.", result.Error, StringComparison.Ordinal);

        JsonElement json = result.Json();
        Assert.Equal("api-key", json.GetProperty("credential").GetString());
        Assert.Equal("flag", json.GetProperty("credentialSource").GetString());
        Assert.True(json.GetProperty("shadowedLogin").GetBoolean());
        Assert.DoesNotContain("xpk_typed_key", result.Output + result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthStatus_EnvKeyOnly()
    {
        _host.Environment["XPING_APIKEY"] = "xpk_team_key";

        CliResult result = _host.Run("auth", "status", "--json");

        // The key cannot be verified without a request, and status makes none.
        Assert.Equal(0, result.Code);
        Assert.Contains("Credential    API key (XPING_APIKEY)", result.Error, StringComparison.Ordinal);
        Assert.Equal("env", result.Json().GetProperty("credentialSource").GetString());
        Assert.False(result.Json().GetProperty("loggedIn").GetBoolean());
        Assert.Empty(_host.Cloud.Requests);
    }

    [Fact]
    public void AuthStatus_CorruptEntry()
    {
        _host.WriteCredentialsFile("not json");

        CliResult result = _host.Run("auth", "status", "--json");

        Assert.Equal(AuthExitCodes.AuthRequired, result.Code);
        JsonElement json = result.Json();
        Assert.Equal("none", json.GetProperty("credential").GetString());
        string warning = Assert.Single(json.GetProperty("warnings").EnumerateArray()).GetString()!;
        Assert.Contains("unreadable", warning, StringComparison.Ordinal);
        Assert.Contains(warning, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthStatus_RefusedFile()
    {
        if (OperatingSystem.IsWindows())
            return;

        _host.SignIn();
        File.SetUnixFileMode(_host.CredentialsFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        CliResult result = _host.Run("auth", "status", "--json");

        Assert.Equal(AuthExitCodes.AuthRequired, result.Code);
        JsonElement json = result.Json();
        Assert.Equal("none", json.GetProperty("credential").GetString());
        Assert.Contains("because other users can read it", Assert.Single(json.GetProperty("warnings").EnumerateArray()).GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AuthStatus_VerbosePrintsThePathsInUse()
    {
        CliResult result = _host.Run("auth", "status", "--verbose");

        Assert.Contains("Credentials   ", result.Error, StringComparison.Ordinal);
        Assert.Contains("credentials.json", result.Error, StringComparison.Ordinal);
        Assert.Contains(Path.Combine("cache", "discovery"), result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthStatus_DoesNotNeedATerminal()
    {
        _host.IsTerminal = false;
        _host.Environment["CI"] = "true";

        CliResult result = _host.Run("auth", "status", "--json");

        Assert.Equal(AuthExitCodes.AuthRequired, result.Code);
        Assert.Equal("none", result.Json().GetProperty("credential").GetString());
    }
}
