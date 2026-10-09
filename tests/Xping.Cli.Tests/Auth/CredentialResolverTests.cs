/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth;
using Xping.Cli.Auth.Store;
using Xping.Cli.Configuration;
using Xping.Cli.Tests.Auth.Store;
using static Xping.Cli.Tests.Auth.Store.CredentialTestData;

namespace Xping.Cli.Tests.Auth;

public sealed class CredentialResolverTests : IDisposable
{
    private const string FlagKey = "flag-api-key-0123456789";
    private const string EnvKey = "env-api-key-0123456789";
    private const string ConfigKey = "config-api-key-0123456789";

    private readonly string _scratch =
        Path.Combine(Path.GetTempPath(), "xping-cli-resolver-tests", Guid.NewGuid().ToString("N"));

    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal)
    {
        ["XPING_CLOUDURL"] = CredentialTestData.CloudUrl
    };

    private readonly XpingHome _home;
    private readonly FileCredentialStore _store;
    private readonly CredentialResolver _resolver;

    public CredentialResolverTests()
    {
        _home = new XpingHome(Path.Combine(_scratch, ".xping"));
        _store = new FileCredentialStore(_home, Serializer);
        _resolver = new CredentialResolver(new CredentialStoreSelector(_store, keychain: null));
        Directory.CreateDirectory(ProjectDirectory);
    }

    private string ProjectDirectory => Path.Combine(_scratch, "project");

    public void Dispose()
    {
        if (Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    [Fact]
    public async Task WithNothingConfiguredThereIsNoCredential()
    {
        ResolvedCredential credential = await ResolveAsync();

        Assert.Equal(CredentialSource.None, credential.Source);
        Assert.Null(credential.ApiKey);
        Assert.Null(credential.Login);
        Assert.Empty(credential.Warnings);
    }

    [Fact]
    public async Task TheFlagWinsOverAStoredLoginWhichIsReportedAsShadowed()
    {
        await SignInAsync();
        _environment["XPING_APIKEY"] = EnvKey;

        ResolvedCredential credential = await ResolveAsync(apiKeyFlag: FlagKey);

        Assert.Equal(CredentialSource.ApiKeyFlag, credential.Source);
        Assert.Equal(FlagKey, credential.ApiKey?.Value);
        Assert.Null(credential.Login);
        Assert.True(credential.ShadowedLogin);
        Assert.Null(credential.FallbackApiKey);
    }

    [Fact]
    public async Task TheFlagWithoutAStoredLoginShadowsNothing()
    {
        ResolvedCredential credential = await ResolveAsync(apiKeyFlag: FlagKey);

        Assert.Equal(CredentialSource.ApiKeyFlag, credential.Source);
        Assert.False(credential.ShadowedLogin);
    }

    [Fact]
    public async Task AStoredLoginWinsOverTheEnvironmentKeyWhichIsKeptAsTheFallback()
    {
        await SignInAsync();
        _environment["XPING_APIKEY"] = EnvKey;

        ResolvedCredential credential = await ResolveAsync();

        Assert.Equal(CredentialSource.StoredLogin, credential.Source);
        Assert.Equal(Record(), credential.Login?.Record);
        Assert.Same(_store, credential.Login?.Store);
        Assert.Null(credential.ApiKey);
        Assert.Equal(EnvKey, credential.FallbackApiKey?.Value);
        Assert.False(credential.ShadowedLogin);
    }

    [Fact]
    public async Task AStoredLoginWinsOverTheSettingsFileKey()
    {
        await SignInAsync();
        WriteAppSettings(ConfigKey);

        ResolvedCredential credential = await ResolveAsync();

        Assert.Equal(CredentialSource.StoredLogin, credential.Source);
        Assert.Equal(ConfigurationSource.AppSettings, credential.FallbackApiKey?.Source);
    }

    [Fact]
    public async Task AStoredLoginForAnotherCloudUrlIsNotUsed()
    {
        await _store.WriteAsync(Record(OtherCloudUrl), CancellationToken.None);
        _environment["XPING_APIKEY"] = EnvKey;

        ResolvedCredential credential = await ResolveAsync();

        Assert.Equal(CredentialSource.ApiKeyEnv, credential.Source);
    }

    [Fact]
    public async Task WithoutALoginTheEnvironmentKeyIsUsed()
    {
        _environment["XPING_APIKEY"] = EnvKey;
        WriteAppSettings(ConfigKey);

        ResolvedCredential credential = await ResolveAsync();

        Assert.Equal(CredentialSource.ApiKeyEnv, credential.Source);
        Assert.Equal(EnvKey, credential.ApiKey?.Value);
        Assert.Equal("XPING_APIKEY", credential.ApiKey?.Origin);
    }

    [Fact]
    public async Task WithoutALoginOrEnvironmentKeyTheSettingsFileKeyIsUsed()
    {
        WriteAppSettings(ConfigKey);

        ResolvedCredential credential = await ResolveAsync();

        Assert.Equal(CredentialSource.ApiKeyConfig, credential.Source);
        Assert.Equal(ConfigKey, credential.ApiKey?.Value);
    }

    [Fact]
    public async Task ACorruptLoginFallsThroughToTheKeyWithItsWarning()
    {
        Directory.CreateDirectory(_home.Root);
        await File.WriteAllTextAsync(_home.CredentialsFile, "not json");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_home.CredentialsFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        _environment["XPING_APIKEY"] = EnvKey;

        ResolvedCredential credential = await ResolveAsync();

        Assert.Equal(CredentialSource.ApiKeyEnv, credential.Source);
        Assert.Equal(
            ["Stored credentials for https://tests.invalid are unreadable and will be replaced at the next `xping login`."],
            credential.Warnings);
    }

    [Fact]
    public async Task ARefusedLoginFileMeansNoCredentialWithTheRefusal()
    {
        if (OperatingSystem.IsWindows())
            return;

        await SignInAsync();
        File.SetUnixFileMode(_home.CredentialsFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);

        ResolvedCredential credential = await ResolveAsync();

        Assert.Equal(CredentialSource.None, credential.Source);
        Assert.StartsWith("Refusing to read", Assert.Single(credential.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStoreThatCannotBeReadDoesNotStopTheFlag()
    {
        if (OperatingSystem.IsWindows())
            return;

        // A directory where the file should be: the read fails with an I/O error, not a refusal.
        Directory.CreateDirectory(_home.CredentialsFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        ResolvedCredential credential = await ResolveAsync(apiKeyFlag: FlagKey);

        Assert.Equal(CredentialSource.ApiKeyFlag, credential.Source);
        Assert.Contains("credentials.json", Assert.Single(credential.StoreFailures), StringComparison.Ordinal);
        Assert.Empty(credential.Warnings);
    }

    [Fact]
    public async Task AStoreThatCannotBeReadIsAFailureNotAWarning()
    {
        if (OperatingSystem.IsWindows())
            return;

        // auth status tells "nothing stored" (exit 10) from "store unreadable" (exit 17) by this.
        Directory.CreateDirectory(_home.CredentialsFile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        ResolvedCredential credential = await ResolveAsync();

        Assert.Equal(CredentialSource.None, credential.Source);
        Assert.Single(credential.StoreFailures);
        Assert.Empty(credential.Warnings);
    }

    [Fact]
    public async Task AnInvalidLoginFallsBackToTheAmbientKey()
    {
        await SignInAsync();
        _environment["XPING_APIKEY"] = EnvKey;

        ResolvedCredential fallback = (await ResolveAsync()).FallbackToApiKey();

        Assert.Equal(CredentialSource.ApiKeyEnv, fallback.Source);
        Assert.Equal(EnvKey, fallback.ApiKey?.Value);
        Assert.Null(fallback.Login);
    }

    [Fact]
    public async Task AnInvalidLoginWithoutAKeyFallsBackToNothing()
    {
        await SignInAsync();

        ResolvedCredential fallback = (await ResolveAsync()).FallbackToApiKey();

        Assert.Equal(CredentialSource.None, fallback.Source);
    }

    [Fact]
    public async Task OnlyALoginFallsBack()
    {
        _environment["XPING_APIKEY"] = EnvKey;

        ResolvedCredential credential = await ResolveAsync();

        Assert.Throws<InvalidOperationException>(credential.FallbackToApiKey);
    }

    [Fact]
    public async Task TheKeyIsRegisteredForRedactionAndLeftOutOfToString()
    {
        const string key = "redacted-api-key-0123456789";
        _environment["XPING_APIKEY"] = key;

        ResolvedCredential credential = await ResolveAsync();

        Assert.DoesNotContain(key, credential.ToString(), StringComparison.Ordinal);
        Assert.Equal(Redaction.Placeholder, Redaction.Scrub(key));
    }

    private Task SignInAsync() => _store.WriteAsync(Record(), CancellationToken.None);

    private void WriteAppSettings(string apiKey) =>
        File.WriteAllText(
            Path.Combine(ProjectDirectory, "appsettings.json"),
            $$"""{ "Xping": { "ApiKey": "{{apiKey}}" } }""");

    private Task<ResolvedCredential> ResolveAsync(string? apiKeyFlag = null) =>
        _resolver.ResolveAsync(
            CliConfiguration.Load(cloudUrlFlag: null, apiKeyFlag, projectFlag: null, ProjectDirectory, name => _environment.GetValueOrDefault(name)),
            CancellationToken.None);
}
