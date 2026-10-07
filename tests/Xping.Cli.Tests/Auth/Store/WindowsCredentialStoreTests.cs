/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Runtime.Versioning;
using Xping.Cli.Auth.Store;
using static Xping.Cli.Tests.Auth.Store.CredentialTestData;

namespace Xping.Cli.Tests.Auth.Store;

/// <summary>
/// The real WindowsCredentialStore (cli-auth-cli-spec §7.3). Runs in the weekly credential store workflow, not on
/// pull requests (§17.2).
/// </summary>
[SupportedOSPlatform("windows")]
[Trait("Category", KeychainStoreScenarios.Category)]
public sealed class WindowsCredentialStoreTests : IAsyncLifetime
{
    private readonly WindowsCredentialStore _store = new WindowsCredentialStore(KeychainStoreScenarios.Service, Serializer);

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => _store.DeleteAsync(CloudUrl, CancellationToken.None);

    [WindowsFact]
    public Task NothingStoredReadsAsNoneAndProbesAsAvailable() => KeychainStoreScenarios.NothingStoredReadsAsNoneAndProbesAsAvailable(_store);

    [WindowsFact]
    public Task ARecordRoundTrips() => KeychainStoreScenarios.ARecordRoundTrips(_store);

    [WindowsFact]
    public Task AWriteReplacesTheEntry() => KeychainStoreScenarios.AWriteReplacesTheEntry(_store);

    [WindowsFact]
    public Task EntriesOfDifferentCloudUrlsAreSeparate() => KeychainStoreScenarios.EntriesOfDifferentCloudUrlsAreSeparate(_store);

    [WindowsFact]
    public Task DeleteRemovesTheEntryOnce() => KeychainStoreScenarios.DeleteRemovesTheEntryOnce(_store);

    [WindowsFact]
    public Task ACorruptEntryReadsAsNoneWithAWarningAndIsKept() => KeychainStoreScenarios.ACorruptEntryReadsAsNoneWithAWarningAndIsKept(_store);

    [WindowsFact]
    public Task ARecordOverTheBlobLimitIsStoredWithoutItsAccessToken() => KeychainStoreScenarios.ARecordOverTheLimitIsStoredWithoutItsAccessToken(_store);
}
