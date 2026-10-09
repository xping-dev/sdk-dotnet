/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth.Store;
using Xping.Cli.Commands.Auth;

namespace Xping.Cli.Tests.Commands.Auth;

/// <summary>
/// The auth JSON contract (cli-auth-cli-spec §3).
/// </summary>
public sealed class AuthJsonTests
{
    [Fact]
    public void NullsAreWrittenAndNamesAreCamelCase()
    {
        string json = Write(new LogoutDocument(AuthJson.SchemaVersion, "signed-out", "https://app.xping.io", Revoked: true, Warning: null));

        Assert.Contains("\"schemaVersion\": \"1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"cloudUrl\": \"https://app.xping.io\"", json, StringComparison.Ordinal);
        Assert.Contains("\"warning\": null", json, StringComparison.Ordinal);
    }

    [Fact]
    public void TheServersOAuthCodeIsWrittenAsOauthError()
    {
        string json = Write(new AuthFailedDocument("1", "failed", "https://app.xping.io", "oauth_error", "m", "invalid_scope"));

        Assert.Contains("\"oauthError\": \"invalid_scope\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void TimestampsAreUtcToTheSecond() =>
        Assert.Equal(
            "2026-09-29T10:15:00Z",
            AuthJson.Timestamp(new DateTimeOffset(2026, 9, 29, 12, 15, 0, 789, TimeSpan.FromHours(2))));

    [Fact]
    public void StoreKindsAreLowercaseWords()
    {
        Assert.Equal("keychain", AuthJson.StoreName(CredentialStoreKind.Keychain));
        Assert.Equal("file", AuthJson.StoreName(CredentialStoreKind.File));
    }

    [Fact]
    public void NoDocumentTypeHasAMemberThatCouldCarryASecret()
    {
        string[] forbidden = ["token", "code", "verifier", "state", "secret", "key"];
        Type[] documents = [typeof(LoginSucceededDocument), typeof(AuthFailedDocument), typeof(LogoutDocument), typeof(AuthStatusDocument)];

        foreach (Type document in documents)
        {
            foreach (string property in document.GetProperties().Select(p => p.Name))
            {
                // "accessTokenExpiresAt" names when a token expires, not the token; "fallbackApiKey"
                // names where a key is configured ("env"), never its value.
                if (property is "AccessTokenExpiresAt" or "FallbackApiKey")
                    continue;

                Assert.DoesNotContain(forbidden, word => property.Contains(word, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    private static string Write<T>(T document)
    {
        using var writer = new StringWriter();
        AuthJson.Write(writer, document);
        return writer.ToString();
    }
}
