/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Runtime.Versioning;
using Xping.Cli.Auth;
using Xping.Cli.Auth.Store;
using Xping.Cli.Hosting;
using Xping.Sdk.Core.Services.Environment;
using static Xping.Cli.Tests.Auth.Store.CredentialTestData;

namespace Xping.Cli.Tests.Auth.Store;

/// <summary>
/// The real <see cref="LibSecretStore"/> (cli-auth-cli-spec §7.3, §7.4).
/// </summary>
/// <remarks>
/// The fallback tests run on every Linux machine with libsecret, keyring or not. The
/// <c>CredentialStore</c> tests need an unlocked keyring on the session bus and run in the weekly
/// credential store workflow (§17.2).
/// </remarks>
[SupportedOSPlatform("linux")]
public sealed class LibSecretStoreTests : IAsyncLifetime
{
    private readonly LibSecretStore _store = new(
        KeychainStoreScenarios.Service, Serializer, new ProcessEnvironment(), File.Exists);

    public Task InitializeAsync() => Task.CompletedTask;

    // Only the keyring tests write; the others never reach the service, so there is nothing to undo.
    public async Task DisposeAsync()
    {
        if (_store.Probe(CloudUrl).Available)
            await _store.DeleteAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false);
    }

    [Fact]
    public void WithoutTheLibraryTheReasonIsThatLibsecretIsNotInstalled()
    {
        var store = new LibSecretStore(
            KeychainStoreScenarios.Service, Serializer, Environment(("DBUS_SESSION_BUS_ADDRESS", "unix:path=/tmp/bus")), _ => true,
            libraryName: "libxping-not-installed.so.0");

        Assert.Equal(KeychainProbe.Unavailable("libsecret not installed", mayHoldSignIns: false), store.Probe(CloudUrl));
    }

    [LibSecretFact]
    public void WithoutASessionBusTheReasonIsThatTheSecretServiceIsNotRunning()
    {
        var store = new LibSecretStore(KeychainStoreScenarios.Service, Serializer, Environment(), _ => false);

        Assert.Equal(KeychainProbe.Unavailable("Secret Service not running"), store.Probe(CloudUrl));
    }

    [LibSecretFact]
    public async Task WithoutASessionBusNoOperationReachesLibsecret()
    {
        // Logout still tries a keychain whose probe failed; it must fail fast, not autolaunch a bus.
        var store = new LibSecretStore(KeychainStoreScenarios.Service, Serializer, Environment(), _ => false);

        CredentialStoreException ex = await Assert.ThrowsAsync<CredentialStoreException>(
            () => store.DeleteAsync(CloudUrl, CancellationToken.None)).ConfigureAwait(false);

        Assert.Equal("Could not use the Secret Service: no session bus.", ex.Message);
    }

    [LibSecretFact]
    public void WithoutASessionBusSignInsGoToTheFile()
    {
        var store = new LibSecretStore(KeychainStoreScenarios.Service, Serializer, Environment(), _ => false);
        var file = new FileCredentialStore(new XpingHome(Path.Combine(Path.GetTempPath(), "unused")), Serializer);

        CredentialStores stores = new CredentialStoreSelector(file, store).Select(CloudUrl);

        Assert.Same(file, stores.Selected);
        Assert.Equal<ICredentialStore>([file], stores.ReadOrder);
        Assert.Equal("Secret Service not running", stores.FallbackReason);
    }

    [LibSecretFact]
    [Trait("Category", KeychainStoreScenarios.Category)]
    public Task NothingStoredReadsAsNoneAndProbesAsAvailable() => KeychainStoreScenarios.NothingStoredReadsAsNoneAndProbesAsAvailable(_store);

    [LibSecretFact]
    [Trait("Category", KeychainStoreScenarios.Category)]
    public Task AReadAfterAWriteSeesTheWriteNotTheProbe() => KeychainStoreScenarios.AReadAfterAWriteSeesTheWriteNotTheProbe(_store);

    [LibSecretFact]
    [Trait("Category", KeychainStoreScenarios.Category)]
    public Task AReadAfterADeleteSeesTheDeleteNotTheProbe() => KeychainStoreScenarios.AReadAfterADeleteSeesTheDeleteNotTheProbe(_store);

    [LibSecretFact]
    [Trait("Category", KeychainStoreScenarios.Category)]
    public Task ARecordRoundTrips() => KeychainStoreScenarios.ARecordRoundTrips(_store);

    [LibSecretFact]
    [Trait("Category", KeychainStoreScenarios.Category)]
    public Task AWriteReplacesTheEntry() => KeychainStoreScenarios.AWriteReplacesTheEntry(_store);

    [LibSecretFact]
    [Trait("Category", KeychainStoreScenarios.Category)]
    public Task EntriesOfDifferentCloudUrlsAreSeparate() => KeychainStoreScenarios.EntriesOfDifferentCloudUrlsAreSeparate(_store);

    [LibSecretFact]
    [Trait("Category", KeychainStoreScenarios.Category)]
    public Task DeleteRemovesTheEntryOnce() => KeychainStoreScenarios.DeleteRemovesTheEntryOnce(_store);

    [LibSecretFact]
    [Trait("Category", KeychainStoreScenarios.Category)]
    public Task ACorruptEntryReadsAsNoneWithAWarningAndIsKept() => KeychainStoreScenarios.ACorruptEntryReadsAsNoneWithAWarningAndIsKept(_store);

    private static DictionaryEnvironment Environment(params (string Name, string Value)[] variables) =>
        new(variables.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal));

    private sealed class DictionaryEnvironment(Dictionary<string, string> variables) : IEnvironmentVariableProvider
    {
        public string? GetVariable(string name) => variables.GetValueOrDefault(name);
    }
}
