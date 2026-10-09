/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth.Store;
using static Xping.Cli.Tests.Auth.Store.CredentialTestData;

namespace Xping.Cli.Tests.Auth.Store;

public sealed class ProbedSecretTests
{
    private readonly ProbedSecret _probed = new();

    [Fact]
    public void TheProbedSecretIsHandedToTheFirstReadOfTheSameEntry()
    {
        // Before the fix every command read the keychain twice: two access dialogs on macOS.
        byte[] secret = [1, 2, 3];
        _probed.Keep(CloudUrl, secret);

        Assert.True(_probed.TryTake(CloudUrl, out byte[]? taken));
        Assert.Same(secret, taken);
    }

    [Fact]
    public void ItIsHandedOutOnlyOnce()
    {
        _probed.Keep(CloudUrl, [1, 2, 3]);
        _probed.TryTake(CloudUrl, out _);

        Assert.False(_probed.TryTake(CloudUrl, out _));
    }

    [Fact]
    public void AProbeThatFoundNoEntryIsReusedAsNoEntry()
    {
        _probed.Keep(CloudUrl, null);

        Assert.True(_probed.TryTake(CloudUrl, out byte[]? taken));
        Assert.Null(taken);
    }

    [Fact]
    public void AnotherCloudUrlIsNotServedAndTheSecretIsCleared()
    {
        byte[] secret = [1, 2, 3];
        _probed.Keep(CloudUrl, secret);

        Assert.False(_probed.TryTake(OtherCloudUrl, out _));
        Assert.Equal(new byte[3], secret);
        Assert.False(_probed.TryTake(CloudUrl, out _));
    }

    [Fact]
    public void ForgettingClearsTheSecret()
    {
        // Every write and delete forgets, so a read after a change never sees the probe's result.
        byte[] secret = [1, 2, 3];
        _probed.Keep(CloudUrl, secret);

        _probed.Forget();

        Assert.Equal(new byte[3], secret);
        Assert.False(_probed.TryTake(CloudUrl, out _));
    }
}
