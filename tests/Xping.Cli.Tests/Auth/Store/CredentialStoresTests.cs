/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth;
using Xping.Cli.Auth.Store;
using static Xping.Cli.Tests.Auth.Store.CredentialTestData;

namespace Xping.Cli.Tests.Auth.Store;

public sealed class CredentialStoresTests
{
    private readonly FakeCredentialStore _keychain = new(CredentialStoreKind.Keychain, "Test Keychain");
    private readonly FakeCredentialStore _file = new(CredentialStoreKind.File, "~/.xping/credentials.json");

    [Fact]
    public async Task TheKeychainIsReadBeforeTheFileEvenWhenTheFileIsSelected()
    {
        // Signed in over SSH (file), later run in a desktop session: both exist, the keychain wins.
        _keychain.Add(Record(refreshToken: "keychain-refresh-token-01"));
        _file.Add(Record(refreshToken: "file-refresh-token-0123456"));
        var stores = new CredentialStores(_file, [_keychain, _file], "keychain locked");

        StoredLoginLookup lookup = await stores.ReadAsync(CloudUrl, CancellationToken.None);

        Assert.Same(_keychain, lookup.Login?.Store);
        Assert.Equal("keychain-refresh-token-01", lookup.Login?.Record.RefreshToken);
    }

    [Fact]
    public async Task AnEmptyKeychainFallsThroughToTheFile()
    {
        _file.Add(Record());
        var stores = new CredentialStores(_keychain, [_keychain, _file], null);

        StoredLoginLookup lookup = await stores.ReadAsync(CloudUrl, CancellationToken.None);

        Assert.Same(_file, lookup.Login?.Store);
    }

    [Fact]
    public async Task WarningsOfEveryStoreVisitedAreCollected()
    {
        _keychain.Warning = "keychain entry corrupt";
        _file.Warning = "file refused";
        var stores = new CredentialStores(_keychain, [_keychain, _file], null);

        StoredLoginLookup lookup = await stores.ReadAsync(CloudUrl, CancellationToken.None);

        Assert.Null(lookup.Login);
        Assert.Equal(["keychain entry corrupt", "file refused"], lookup.Warnings);
    }

    [Fact]
    public async Task WritingToTheKeychainRemovesTheFileEntryForTheSameCloudUrl()
    {
        _file.Add(Record());
        var stores = new CredentialStores(_keychain, [_keychain, _file], null);

        await stores.WriteAsync(Record(), CancellationToken.None);

        Assert.True(_keychain.Contains(CloudUrl));
        Assert.False(_file.Contains(CloudUrl));
    }

    [Fact]
    public async Task WritingToTheFileLeavesTheKeychainAlone()
    {
        var stores = new CredentialStores(_file, [_keychain, _file], "keychain locked");

        await stores.WriteAsync(Record(), CancellationToken.None);

        Assert.True(_file.Contains(CloudUrl));
        Assert.Empty(_keychain.Deleted);
    }

    [Fact]
    public async Task SigningOutDeletesFromEveryStore()
    {
        _file.Add(Record());
        var stores = new CredentialStores(_keychain, [_keychain, _file], null);

        Assert.True(await stores.DeleteAllAsync(CloudUrl, CancellationToken.None));

        Assert.Equal([CloudUrl], _keychain.Deleted);
        Assert.Equal([CloudUrl], _file.Deleted);
        Assert.False(await stores.DeleteAllAsync(CloudUrl, CancellationToken.None));
    }

    [Fact]
    public void TheSelectedStoreMustBeRead() =>
        Assert.Throws<ArgumentException>(() => new CredentialStores(_keychain, [_file], null));

    [Fact]
    public async Task AKeychainThatCannotBeReadDoesNotHideAFileLogin()
    {
        // Before the fix, the keychain's failure ended the lookup and the file was never read.
        _keychain.Fails = true;
        _file.Add(Record());
        var stores = new CredentialStores(_file, [_keychain, _file], "keychain locked");

        StoredLoginLookup lookup = await stores.ReadAsync(CloudUrl, CancellationToken.None);

        Assert.Same(_file, lookup.Login?.Store);
        Assert.Equal(["Could not read Test Keychain."], lookup.Failures);
        Assert.Empty(lookup.Warnings);
    }

    [Fact]
    public async Task SigningOutStillDeletesFromTheFileWhenTheKeychainFails()
    {
        _keychain.Fails = true;
        _file.Add(Record());
        var stores = new CredentialStores(_keychain, [_keychain, _file], null);

        CredentialStoreException failure = await Assert.ThrowsAsync<CredentialStoreException>(
            () => stores.DeleteAllAsync(CloudUrl, CancellationToken.None));

        Assert.Equal("Could not delete from Test Keychain.", failure.Message);
        Assert.False(_file.Contains(CloudUrl));
    }

    [Fact]
    public async Task EveryStoreThatFailsToDeleteIsNamed()
    {
        _keychain.Fails = true;
        _file.Fails = true;
        var stores = new CredentialStores(_keychain, [_keychain, _file], null);

        CredentialStoreException failure = await Assert.ThrowsAsync<CredentialStoreException>(
            () => stores.DeleteAllAsync(CloudUrl, CancellationToken.None));

        Assert.Equal(
            "Could not delete from Test Keychain. Could not delete from ~/.xping/credentials.json.",
            failure.Message);
    }
}
