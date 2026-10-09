/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth;
using Xping.Cli.Auth.Store;
using static Xping.Cli.Tests.Auth.Store.CredentialTestData;

namespace Xping.Cli.Tests.Auth.Store;

public sealed class CredentialStoreSelectorTests
{
    private readonly FileCredentialStore _file = new(new XpingHome(Path.Combine(Path.GetTempPath(), "unused")), Serializer);
    private readonly FakeCredentialStore _keychain = new(CredentialStoreKind.Keychain, "Test Keychain");

    [Fact]
    public void AnAvailableKeychainIsSelectedAndReadBeforeTheFile()
    {
        CredentialStores stores = new CredentialStoreSelector(_file, _keychain).Select(CloudUrl);

        Assert.Same(_keychain, stores.Selected);
        Assert.Equal<ICredentialStore>([_keychain, _file], stores.ReadOrder);
        Assert.Null(stores.FallbackReason);
    }

    [Fact]
    public void TheProbeLooksUpTheEntryOfTheCloudUrlInUse()
    {
        new CredentialStoreSelector(_file, _keychain).Select(CloudUrl);

        Assert.Equal([CloudUrl], _keychain.Probed);
    }

    [Theory]
    [InlineData("keychain locked")]
    [InlineData("keychain unavailable")]
    [InlineData("libsecret not installed")]
    [InlineData("Secret Service not running")]
    [InlineData("Credential Manager error")]
    public void AnUnavailableKeychainFallsBackToTheFileWithItsReason(string reason)
    {
        _keychain.ProbeResult = KeychainProbe.Unavailable(reason);

        CredentialStores stores = new CredentialStoreSelector(_file, _keychain).Select(CloudUrl);

        Assert.Same(_file, stores.Selected);
        Assert.Equal(reason, stores.FallbackReason);
    }

    [Fact]
    public void AnUnavailableKeychainIsLeftOutOfTheReadOrder()
    {
        // Reading it would fail the same way, and turn "not signed in" into a store failure (§7.4).
        _keychain.ProbeResult = KeychainProbe.Unavailable("keychain locked");

        CredentialStores stores = new CredentialStoreSelector(_file, _keychain).Select(CloudUrl);

        Assert.Equal<ICredentialStore>([_file], stores.ReadOrder);
    }

    [Fact]
    public void AnUnavailableKeychainIsStillOfferedForSigningOut()
    {
        _keychain.ProbeResult = KeychainProbe.Unavailable("keychain locked");

        CredentialStores stores = new CredentialStoreSelector(_file, _keychain).Select(CloudUrl);

        Assert.Equal<ICredentialStore>([_keychain], stores.Unreachable);
    }

    [Fact]
    public void AKeychainThatCannotHoldAnythingIsNotOfferedForSigningOut()
    {
        _keychain.ProbeResult = KeychainProbe.Unavailable("libsecret not installed", mayHoldSignIns: false);

        CredentialStores stores = new CredentialStoreSelector(_file, _keychain).Select(CloudUrl);

        Assert.Empty(stores.Unreachable);
    }

    [Fact]
    public void WithoutAKeychainTheFileIsSelectedWithoutAReason()
    {
        CredentialStores stores = new CredentialStoreSelector(_file, keychain: null).Select(CloudUrl);

        Assert.Same(_file, stores.Selected);
        Assert.Equal<ICredentialStore>([_file], stores.ReadOrder);
        Assert.Null(stores.FallbackReason);
    }

    [Fact]
    public void TheKeychainIsProbedOncePerProcess()
    {
        var selector = new CredentialStoreSelector(_file, _keychain);

        CredentialStores first = selector.Select(CloudUrl);
        CredentialStores second = selector.Select(CloudUrl);

        Assert.Same(first, second);
        Assert.Single(_keychain.Probed);
    }
}
