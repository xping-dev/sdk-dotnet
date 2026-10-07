/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Runtime.Versioning;
using Xping.Cli.Auth.Store;
using static Xping.Cli.Tests.Auth.Store.CredentialTestData;

namespace Xping.Cli.Tests.Auth.Store;

/// <summary>
/// The real MacOsKeychainStore (cli-auth-cli-spec §7.3). Runs in the weekly credential store workflow, not on
/// pull requests (§17.2).
/// </summary>
[SupportedOSPlatform("macos")]
[Trait("Category", KeychainStoreScenarios.Category)]
public sealed class MacOsKeychainStoreTests : IAsyncLifetime
{
    private readonly MacOsKeychainStore _store = new MacOsKeychainStore(KeychainStoreScenarios.Service, Serializer);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _store.DeleteAsync(CloudUrl, CancellationToken.None);

    [MacOsFact]
    public Task NothingStoredReadsAsNoneAndProbesAsAvailable() => KeychainStoreScenarios.NothingStoredReadsAsNoneAndProbesAsAvailable(_store);

    [MacOsFact]
    public Task AReadAfterAWriteSeesTheWriteNotTheProbe() => KeychainStoreScenarios.AReadAfterAWriteSeesTheWriteNotTheProbe(_store);

    [MacOsFact]
    public Task AReadAfterADeleteSeesTheDeleteNotTheProbe() => KeychainStoreScenarios.AReadAfterADeleteSeesTheDeleteNotTheProbe(_store);

    [MacOsFact]
    public Task ARecordRoundTrips() => KeychainStoreScenarios.ARecordRoundTrips(_store);

    [MacOsFact]
    public Task AWriteReplacesTheEntry() => KeychainStoreScenarios.AWriteReplacesTheEntry(_store);

    [MacOsFact]
    public Task EntriesOfDifferentCloudUrlsAreSeparate() => KeychainStoreScenarios.EntriesOfDifferentCloudUrlsAreSeparate(_store);

    [MacOsFact]
    public Task DeleteRemovesTheEntryOnce() => KeychainStoreScenarios.DeleteRemovesTheEntryOnce(_store);

    [MacOsFact]
    public Task ACorruptEntryReadsAsNoneWithAWarningAndIsKept() => KeychainStoreScenarios.ACorruptEntryReadsAsNoneWithAWarningAndIsKept(_store);
}
