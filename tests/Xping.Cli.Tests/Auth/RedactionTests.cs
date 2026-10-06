/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth;

namespace Xping.Cli.Tests.Auth;

public sealed class RedactionTests
{
    [Theory]
    [InlineData("Authorization: Bearer abc123", "Authorization: [redacted]")]
    [InlineData("authorization:Bearer abc123", "authorization:[redacted]")]
    [InlineData("X-API-Key: xpg_live_1234", "X-API-Key: [redacted]")]
    [InlineData("sent\nX-Api-Key: xpg_live_1234\nnext", "sent\nX-Api-Key: [redacted]\nnext")]
    public void HeaderValuesAreRemoved(string text, string expected)
    {
        Assert.Equal(expected, Redaction.Scrub(text));
    }

    [Theory]
    [InlineData("code=abc&state=xyz", "code=[redacted]&state=[redacted]")]
    [InlineData("grant_type=refresh_token&refresh_token=r1 more", "grant_type=refresh_token&refresh_token=[redacted] more")]
    [InlineData("device_code=d1", "device_code=[redacted]")]
    [InlineData("code_verifier=v1", "code_verifier=[redacted]")]
    [InlineData("token=t1&token_type_hint=refresh_token", "token=[redacted]&token_type_hint=refresh_token")]
    [InlineData("/callback?code=abc", "/callback?code=[redacted]")]
    [InlineData("#access_token=opaque123&expires_in=900", "#access_token=[redacted]&expires_in=900")]
    [InlineData("id_token=i1", "id_token=[redacted]")]
    [InlineData("client_id=xping-cli&client_secret=s1", "client_id=xping-cli&client_secret=[redacted]")]
    public void FormFieldsAreRemoved(string text, string expected)
    {
        Assert.Equal(expected, Redaction.Scrub(text));
    }

    [Theory]
    [InlineData("error_code=42")]
    [InlineData("access_denied: user declined")]
    [InlineData("grant_type=authorization_code")]
    [InlineData("statement=ok")]
    public void FieldsThatOnlyResembleASecretAreKept(string text)
    {
        Assert.Equal(text, Redaction.Scrub(text));
    }

    [Fact]
    public void TokenValuesInAJsonBodyAreRemoved()
    {
        const string body = """{"access_token":"opaque-a","expires_in":900,"refresh_token" : "opaque\"r","scope":"user:read"}""";

        Assert.Equal(
            """{"access_token":"[redacted]","expires_in":900,"refresh_token" : "[redacted]","scope":"user:read"}""",
            Redaction.Scrub(body));
    }

    [Fact]
    public void TokenValuesInTheStoredRecordAreRemoved()
    {
        const string record = """{"refreshToken":"r1","accessToken":"a1","email":"jane@example.com"}""";

        Assert.Equal(
            """{"refreshToken":"[redacted]","accessToken":"[redacted]","email":"jane@example.com"}""",
            Redaction.Scrub(record));
    }

    [Fact]
    public void AJwtShapedStringIsRemoved()
    {
        const string jwt = "eyJhbGciOiJFUzI1NiJ9.eyJzdWIiOiIwMUo4SzJWNlhOIn0.c2lnbmF0dXJlLXZhbHVl";

        Assert.Equal("token was [redacted].", Redaction.Scrub($"token was {jwt}."));
    }

    [Theory]
    [InlineData("Xping.Cli.Auth failed in System.Net.Http")]
    [InlineData("at Microsoft.Extensions.Configuration.FileConfigurationProvider.HandleException()")]
    [InlineData("at System.Runtime.CompilerServices.TaskAwaiter.ThrowForNonSuccess(Task task)")]
    public void ADottedTypeNameIsKept(string text)
    {
        Assert.Equal(text, Redaction.Scrub(text));
    }

    [Fact]
    public void ARegisteredSecretIsRemovedWhereverItAppears()
    {
        string secret = "secret-" + Guid.NewGuid().ToString("N");
        Redaction.AddSecret(secret);

        Assert.Equal("key [redacted] was rejected", Redaction.Scrub($"key {secret} was rejected"));
    }

    [Fact]
    public void AShortValueIsNotRegistered()
    {
        Redaction.AddSecret("abc");

        Assert.Equal("abc is fine", Redaction.Scrub("abc is fine"));
    }

    [Fact]
    public void NullScrubsToEmpty()
    {
        Assert.Equal(string.Empty, Redaction.Scrub(null));
    }
}
