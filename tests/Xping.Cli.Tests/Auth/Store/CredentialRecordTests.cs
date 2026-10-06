/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Auth.Store;
using static Xping.Cli.Tests.Auth.Store.CredentialTestData;

namespace Xping.Cli.Tests.Auth.Store;

public sealed class CredentialRecordTests
{
    [Fact]
    public void SerializesToTheStoredShape()
    {
        using JsonDocument json = JsonDocument.Parse(Serializer.Serialize(Record()));

        string[] names = [.. json.RootElement.EnumerateObject().Select(p => p.Name)];

        Assert.Equal(
            [
                "schemaVersion", "cloudUrl", "refreshToken", "accessToken", "accessTokenExpiresAt", "workspaceId",
                "sid", "sub", "email", "dataGatewayUri", "storedAt"
            ],
            names);
        Assert.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void RoundTripsThroughTheSerializer()
    {
        CredentialRecord record = Record();

        Assert.Equal(record, Serializer.Deserialize<CredentialRecord>(Serializer.Serialize(record)));
    }

    [Fact]
    public void ToStringLeavesTheTokensOut()
    {
        string text = Record().ToString();

        Assert.DoesNotContain(RefreshToken, text, StringComparison.Ordinal);
        Assert.DoesNotContain(AccessToken, text, StringComparison.Ordinal);
        Assert.Contains(CloudUrl, text, StringComparison.Ordinal);
    }

    [Fact]
    public void AWellFormedRecordIsValidForItsOwnCloudUrl() =>
        Assert.True(Record().IsValidFor(CloudUrl));

    [Fact]
    public void ARecordNamingAnotherCloudUrlIsNotValid() =>
        Assert.False(Record().IsValidFor(OtherCloudUrl));

    [Fact]
    public void ARecordOfAnotherSchemaVersionIsNotValid() =>
        Assert.False((Record() with { SchemaVersion = 2 }).IsValidFor(CloudUrl));

    [Theory]
    [InlineData("""{"schemaVersion":1,"cloudUrl":"https://tests.invalid","dataGatewayUri":"https://api.tests.invalid","storedAt":"2026-09-29T10:00:00Z"}""")]
    [InlineData("""{"schemaVersion":1,"cloudUrl":"https://tests.invalid","refreshToken":" ","dataGatewayUri":"https://api.tests.invalid","storedAt":"2026-09-29T10:00:00Z"}""")]
    [InlineData("""{"schemaVersion":1,"cloudUrl":"https://tests.invalid","refreshToken":"refresh-token-0123456789","storedAt":"2026-09-29T10:00:00Z"}""")]
    [InlineData("""{"schemaVersion":1,"refreshToken":"refresh-token-0123456789","dataGatewayUri":"https://api.tests.invalid","storedAt":"2026-09-29T10:00:00Z"}""")]
    public void ARecordMissingARequiredMemberIsNotValid(string json)
    {
        CredentialRecord? record = Serializer.Deserialize<CredentialRecord>(json);

        Assert.NotNull(record);
        Assert.False(record.IsValidFor(CloudUrl));
    }
}
