/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth.Store;
using static Xping.Cli.Tests.Auth.Store.CredentialTestData;

namespace Xping.Cli.Tests.Auth.Store;

/// <summary>
/// What every OS keychain backend must do, run against the real keychain by each backend's tests
/// (cli-auth-cli-spec §17.2).
/// </summary>
/// <remarks>
/// The entries use the service <see cref="Service"/> and a Cloud URL under <c>.invalid</c>, so a
/// developer's real sign-in is never read, replaced or deleted.
/// </remarks>
internal static class KeychainStoreScenarios
{
    public const string Category = "CredentialStore";

    public const string Service = "xping-cli-tests";

    public static async Task NothingStoredReadsAsNoneAndProbesAsAvailable(IKeychainCredentialStore store)
    {
        await store.DeleteAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(KeychainProbe.Ok, store.Probe(CloudUrl));
        Assert.Equal(CredentialReadResult.None, await store.ReadAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false));
    }

    public static async Task ARecordRoundTrips(IKeychainCredentialStore store)
    {
        await store.WriteAsync(Record(), CancellationToken.None).ConfigureAwait(false);

        CredentialReadResult result = await store.ReadAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(new CredentialReadResult(Record(), null), result);
        Assert.Equal(KeychainProbe.Ok, store.Probe(CloudUrl));
    }

    public static async Task AWriteReplacesTheEntry(IKeychainCredentialStore store)
    {
        await store.WriteAsync(Record(refreshToken: "first-refresh-token-0123"), CancellationToken.None).ConfigureAwait(false);
        await store.WriteAsync(Record(refreshToken: "second-refresh-token-012"), CancellationToken.None).ConfigureAwait(false);

        CredentialReadResult result = await store.ReadAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false);

        Assert.Equal("second-refresh-token-012", result.Record?.RefreshToken);
    }

    public static async Task EntriesOfDifferentCloudUrlsAreSeparate(IKeychainCredentialStore store)
    {
        try
        {
            await store.WriteAsync(Record(), CancellationToken.None).ConfigureAwait(false);
            await store.WriteAsync(Record(OtherCloudUrl, "other-refresh-token-0123"), CancellationToken.None).ConfigureAwait(false);

            Assert.True(await store.DeleteAsync(OtherCloudUrl, CancellationToken.None).ConfigureAwait(false));
            Assert.Equal(RefreshToken, (await store.ReadAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false)).Record?.RefreshToken);
        }
        finally
        {
            await store.DeleteAsync(OtherCloudUrl, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public static async Task DeleteRemovesTheEntryOnce(IKeychainCredentialStore store)
    {
        await store.WriteAsync(Record(), CancellationToken.None).ConfigureAwait(false);

        Assert.True(await store.DeleteAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false));
        Assert.False(await store.DeleteAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false));
        Assert.Equal(CredentialReadResult.None, await store.ReadAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false));
    }

    public static async Task ACorruptEntryReadsAsNoneWithAWarningAndIsKept(IKeychainCredentialStore store)
    {
        // Written by a "newer CLI": nothing is deleted automatically (§7.7).
        await store.WriteAsync(Record() with { SchemaVersion = 2 }, CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(CredentialReadResult.Corrupt(CloudUrl), await store.ReadAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false));
        Assert.True(await store.DeleteAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false));
    }

    public static async Task ARecordOverTheLimitIsStoredWithoutItsAccessToken(IKeychainCredentialStore store)
    {
        // A maximum-length refresh token plus an access token exceeds a Windows credential (§7.3).
        CredentialRecord large = Record(refreshToken: new string('r', 2048)) with { AccessToken = LargeAccessToken };

        await store.WriteAsync(large, CancellationToken.None).ConfigureAwait(false);

        CredentialRecord? stored = (await store.ReadAsync(CloudUrl, CancellationToken.None).ConfigureAwait(false)).Record;
        Assert.Equal(large with { AccessToken = null, AccessTokenExpiresAt = null }, stored);
    }
}
