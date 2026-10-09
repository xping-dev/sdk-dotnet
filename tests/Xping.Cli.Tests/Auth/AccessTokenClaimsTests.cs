/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Buffers.Text;
using System.Text;
using Xping.Cli.Auth;

namespace Xping.Cli.Tests.Auth;

public sealed class AccessTokenClaimsTests
{
    [Fact]
    public void TheDisplayClaimsAreReadFromThePayload()
    {
        string token = Jwt("""{"sub":"u1","sid":"s1","email":"jane@example.com","workspace_id":"w1","exp":1}""");

        Assert.Equal(new AccessTokenClaims("u1", "s1", "jane@example.com", "w1"), AccessTokenClaims.Read(token));
    }

    [Fact]
    public void MissingOrNonStringClaimsAreNull()
    {
        string token = Jwt("""{"sub":42,"email":null}""");

        Assert.Equal(AccessTokenClaims.None, AccessTokenClaims.Read(token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("opaque-token")]
    [InlineData("a.b")]
    [InlineData("a.!!!.c")]
    [InlineData("a.bm90IGpzb24.c")]
    [InlineData("a.WzFd.c")]
    public void AnUnreadableTokenGivesNoClaimsInsteadOfAnError(string? token) =>
        Assert.Equal(AccessTokenClaims.None, AccessTokenClaims.Read(token));

    private static string Jwt(string payload) =>
        $"{Base64Url.EncodeToString("""{"alg":"none"}"""u8)}.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload))}.sig";
}
