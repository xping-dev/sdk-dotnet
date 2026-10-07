/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Store;
using Xping.Cli.Tests.Auth.Store;
using Xping.Cli.Tests.Cloud;

namespace Xping.Cli.Tests.Commands.Auth;

/// <summary>
/// <c>xping logout</c> against the fake Cloud (cli-auth-cli-spec §3.3, §12).
/// </summary>
public sealed class LogoutCommandTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly CliFlowHost _host = new();

    public Task InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public void Logout_Revokes_ThenDeletes()
    {
        CredentialRecord record = _host.SignIn();
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(_host.HomeDirectory, "cache", "discovery")));

        CliResult result = _host.Run("logout", "--json");

        Assert.Equal(0, result.Code);
        Assert.Contains($"Signed out of {_host.Cloud.CloudUrl} ({FakeCloud.Email}).", result.Error, StringComparison.Ordinal);

        RecordedRequest revoke = Assert.Single(_host.Cloud.RequestsTo("/connect/revoke"));
        Assert.Equal(record.RefreshToken, revoke.Form["token"]);
        Assert.Equal("refresh_token", revoke.Form["token_type_hint"]);
        Assert.True(_host.Cloud.IsSessionRevoked(record.RefreshToken));

        Assert.Null(_host.StoredRecord());
        Assert.Empty(Directory.GetFiles(Path.Combine(_host.HomeDirectory, "cache", "discovery")));

        JsonElement json = result.Json();
        Assert.Equal("signed-out", json.GetProperty("result").GetString());
        Assert.True(json.GetProperty("revoked").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("warning").ValueKind);
        Assert.DoesNotContain(record.RefreshToken, result.Output + result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logout_Offline_DeletesAndWarns()
    {
        CredentialRecord record = _host.SignIn();
        _host.Cloud.Drop("/connect/revoke", 2);

        // The second attempt waits two seconds on the fake clock (§2.4).
        Task<CliResult> run = _host.Start("logout", "--json");
        await _host.WaitForTimerAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
        _host.Time.Advance(TimeSpan.FromSeconds(2));
        CliResult result = await run.ConfigureAwait(true);

        Assert.Equal(0, result.Code);
        Assert.Contains($"Could not reach {_host.Cloud.CloudUrl} to revoke the session (", result.Error, StringComparison.Ordinal);
        Assert.Contains("Your local credentials were removed.", result.Error, StringComparison.Ordinal);
        Assert.Contains("Signed out locally.", result.Error, StringComparison.Ordinal);
        Assert.False(_host.Cloud.IsSessionRevoked(record.RefreshToken));
        Assert.Null(_host.StoredRecord());

        JsonElement json = result.Json();
        Assert.Equal("signed-out-locally", json.GetProperty("result").GetString());
        Assert.False(json.GetProperty("revoked").GetBoolean());
        Assert.StartsWith("Could not reach", json.GetProperty("warning").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ARefusedRevocationStillSignsOutLocally()
    {
        _host.SignIn();
        _host.Cloud.Fail("/connect/revoke", 1, System.Net.HttpStatusCode.Unauthorized, "invalid_client");

        CliResult result = _host.Run("logout");

        Assert.Equal(0, result.Code);
        Assert.Contains($"Xping Cloud at {_host.Cloud.CloudUrl} refused to revoke the session (invalid_client).", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Could not reach", result.Error, StringComparison.Ordinal);
        Assert.Null(_host.StoredRecord());
    }

    [Fact]
    public void Logout_NotSignedIn()
    {
        CliResult result = _host.Run("logout", "--json");

        Assert.Equal(0, result.Code);
        Assert.Contains($"You are not signed in to {_host.Cloud.CloudUrl}.", result.Error, StringComparison.Ordinal);
        Assert.Empty(_host.Cloud.Requests);

        JsonElement json = result.Json();
        Assert.Equal("not-signed-in", json.GetProperty("result").GetString());
        Assert.False(json.GetProperty("revoked").GetBoolean());
    }

    [Fact]
    public void ASignInInAKeychainThatFailedItsProbeIsStillRemoved()
    {
        // Signed in from the desktop, signing out over SSH: before the fix the keychain was skipped
        // and logout said "not signed in".
        var keychain = new FakeCredentialStore(CredentialStoreKind.Keychain, "Test Keychain")
        {
            ProbeResult = KeychainProbe.Unavailable("keychain locked"),
        };
        keychain.Add(CredentialTestData.Record(_host.Cloud.CloudUrl));
        _host.Keychain = keychain;

        CliResult result = _host.Run("logout", "--json");

        Assert.Equal(0, result.Code);
        Assert.False(keychain.Contains(_host.Cloud.CloudUrl));
        Assert.Equal("signed-out-locally", result.Json().GetProperty("result").GetString());
    }

    [Fact]
    public void AKeychainThatCannotBeCheckedIsReportedAndDoesNotFailTheSignOut()
    {
        _host.Keychain = new FakeCredentialStore(CredentialStoreKind.Keychain, "Test Keychain")
        {
            ProbeResult = KeychainProbe.Unavailable("keychain locked"),
            Fails = true,
        };

        CliResult result = _host.Run("logout", "--json");

        Assert.Equal(0, result.Code);
        Assert.Contains($"You are not signed in to {_host.Cloud.CloudUrl}.", result.Error, StringComparison.Ordinal);
        const string Warning = "Could not check the Test Keychain (keychain locked). A sign-in made where it is available, " +
            "such as a desktop session, may still be stored there; run `xping logout` there to remove it.";
        Assert.Contains(Warning, result.Error, StringComparison.Ordinal);
        Assert.Equal(Warning, result.Json().GetProperty("warning").GetString());
    }

    [Fact]
    public void ASignOutFromTheFileAlsoReportsAKeychainThatCannotBeChecked()
    {
        _host.SignIn();
        _host.Keychain = new FakeCredentialStore(CredentialStoreKind.Keychain, "Test Keychain")
        {
            ProbeResult = KeychainProbe.Unavailable("Secret Service not running"),
            Fails = true,
        };

        CliResult result = _host.Run("logout", "--json");

        Assert.Equal(0, result.Code);
        Assert.False(File.Exists(_host.CredentialsFile));
        Assert.Equal("signed-out", result.Json().GetProperty("result").GetString());
        Assert.StartsWith("Could not check the Test Keychain (Secret Service not running).",
            result.Json().GetProperty("warning").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOfflineSignOutReportsBothTheRevocationAndAnUncheckedKeychain()
    {
        _host.SignIn();
        _host.Cloud.Drop("/connect/revoke", 2);
        _host.Keychain = new FakeCredentialStore(CredentialStoreKind.Keychain, "Test Keychain")
        {
            ProbeResult = KeychainProbe.Unavailable("keychain locked"),
            Fails = true,
        };

        Task<CliResult> run = _host.Start("logout", "--json");
        await _host.WaitForTimerAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(true);
        _host.Time.Advance(TimeSpan.FromSeconds(2));
        CliResult result = await run.ConfigureAwait(true);

        Assert.Equal(0, result.Code);
        string warning = result.Json().GetProperty("warning").GetString()!;
        Assert.StartsWith($"Could not reach {_host.Cloud.CloudUrl} to revoke the session (", warning, StringComparison.Ordinal);
        Assert.Contains(" Could not check the Test Keychain (keychain locked).", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void LogoutTwiceIsNotSignedInTheSecondTime()
    {
        _host.SignIn();
        Assert.Equal(0, _host.Run("logout").Code);

        CliResult second = _host.Run("logout");

        Assert.Equal(0, second.Code);
        Assert.Contains("You are not signed in", second.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Logout_ApiKeyOnly()
    {
        _host.Environment["XPING_APIKEY"] = "xpk_team_key";

        CliResult result = _host.Run("logout");

        Assert.Equal(0, result.Code);
        Assert.Contains("You are not signed in", result.Error, StringComparison.Ordinal);
        Assert.Contains("Unset XPING_APIKEY", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("xpk_team_key", result.Error, StringComparison.Ordinal);
        Assert.Empty(_host.Cloud.Requests);
    }

    [Fact]
    public void AnUnreadableEntryIsRemovedWithoutARevocation()
    {
        _host.WriteCredentialsFile(
            $$"""{ "schemaVersion": 1, "credentials": { "{{_host.Cloud.CloudUrl}}": { "schemaVersion": 2 } } }""");

        CliResult result = _host.Run("logout", "--json");

        Assert.Equal(0, result.Code);
        Assert.Equal("signed-out-locally", result.Json().GetProperty("result").GetString());
        Assert.Empty(_host.Cloud.RequestsTo("/connect/revoke"));
        Assert.False(File.Exists(_host.CredentialsFile) && File.ReadAllText(_host.CredentialsFile).Contains(_host.Cloud.CloudUrl, StringComparison.Ordinal));
    }

    [Fact]
    public void ACredentialsFileThatDoesNotParseIsLeftAlone()
    {
        // It may hold another Cloud URL's sign-in written by a newer CLI (§7.7).
        _host.WriteCredentialsFile("not json");

        CliResult result = _host.Run("logout", "--json");

        Assert.Equal(0, result.Code);
        Assert.Equal("not-signed-in", result.Json().GetProperty("result").GetString());
        Assert.Equal("not json", File.ReadAllText(_host.CredentialsFile));
    }

    [Fact]
    public void TheProjectCacheOfThisCloudIsRemoved()
    {
        _host.SignIn();
        string projects = new XpingHome(_host.HomeDirectory).ProjectCacheDirectory(_host.Cloud.CloudUrl);
        Directory.CreateDirectory(projects);
        File.WriteAllText(Path.Combine(projects, FakeCloud.WorkspaceId + ".json"), "{}");

        Assert.Equal(0, _host.Run("logout").Code);

        Assert.False(Directory.Exists(projects));
    }
}
