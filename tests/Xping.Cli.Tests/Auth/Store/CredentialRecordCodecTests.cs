/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;
using Xping.Cli.Auth.Store;
using static Xping.Cli.Tests.Auth.Store.CredentialTestData;

namespace Xping.Cli.Tests.Auth.Store;

public sealed class CredentialRecordCodecTests
{
    // CRED_MAX_CREDENTIAL_BLOB_SIZE, the only limit a backend declares today.
    private const int WindowsLimit = 2560;

    [Fact]
    public void ARecordThatFitsKeepsItsAccessToken()
    {
        byte[] secret = CredentialRecordCodec.Encode(Record(), maxBytes: 4096, Serializer, "Test Keychain");

        CredentialRecord? record = CredentialRecordCodec.Decode(secret, CloudUrl, Serializer).Record;

        Assert.Equal(Record(), record);
    }

    [Fact]
    public void WithoutALimitTheWholeRecordIsKept()
    {
        CredentialRecord large = Record(refreshToken: new string('r', 2048)) with { AccessToken = LargeAccessToken };

        byte[] secret = CredentialRecordCodec.Encode(large, maxBytes: null, Serializer, "Test Keychain");

        Assert.Equal(LargeAccessToken, CredentialRecordCodec.Decode(secret, CloudUrl, Serializer).Record?.AccessToken);
    }

    [Fact]
    public void ARecordOverTheLimitDropsItsAccessTokenAndKeepsTheRest()
    {
        // A maximum-length refresh token and an access token do not fit a Windows credential (§7.3).
        CredentialRecord large = Record(refreshToken: new string('r', 2048)) with { AccessToken = LargeAccessToken };

        byte[] secret = CredentialRecordCodec.Encode(large, WindowsLimit, Serializer, "Test Keychain");

        Assert.True(secret.Length <= WindowsLimit);
        CredentialRecord? record = CredentialRecordCodec.Decode(secret, CloudUrl, Serializer).Record;
        Assert.Equal(large with { AccessToken = null, AccessTokenExpiresAt = null }, record);
    }

    [Fact]
    public void ARecordThatDoesNotFitEvenWithoutItsAccessTokenIsRefused()
    {
        CredentialRecord huge = Record(refreshToken: new string('r', 4096));

        var ex = Assert.Throws<CredentialStoreException>(
            () => CredentialRecordCodec.Encode(huge, WindowsLimit, Serializer, "Test Keychain"));

        Assert.Contains("too large for Test Keychain", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("rrrr", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void AnUnreadableSecretIsCorrupt(string secret)
    {
        CredentialReadResult result = CredentialRecordCodec.Decode(Encoding.UTF8.GetBytes(secret), CloudUrl, Serializer);

        Assert.Null(result.Record);
        Assert.Equal(CredentialReadResult.Corrupt(CloudUrl), result);
    }

    [Fact]
    public void ARecordOfAnotherSchemaVersionIsCorrupt()
    {
        byte[] secret = Serializer.SerializeToUtf8Bytes(Record() with { SchemaVersion = 2 });

        Assert.Equal(CredentialReadResult.Corrupt(CloudUrl), CredentialRecordCodec.Decode(secret, CloudUrl, Serializer));
    }

    [Fact]
    public void ARecordStoredUnderAnotherCloudUrlIsCorrupt()
    {
        byte[] secret = Serializer.SerializeToUtf8Bytes(Record(OtherCloudUrl));

        Assert.Equal(CredentialReadResult.Corrupt(CloudUrl), CredentialRecordCodec.Decode(secret, CloudUrl, Serializer));
    }
}
