/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth.Loopback;

namespace Xping.Cli.Tests.Auth.Loopback;

public sealed class PkceTests
{
    private const string Base64UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    [Fact]
    public void TheChallengeMatchesTheRfc7636AppendixBVector() =>
        Assert.Equal(
            "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            Pkce.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));

    [Fact]
    public void TheVerifierAndStateAre43Base64UrlCharacters()
    {
        using var pkce = Pkce.Create();

        Assert.Equal(43, pkce.CodeVerifier.Length);
        Assert.Equal(43, pkce.State.Length);
        Assert.All(pkce.CodeVerifier + pkce.State, c => Assert.Contains(c, Base64UrlAlphabet));
        Assert.Equal(Pkce.Challenge(pkce.CodeVerifier), pkce.CodeChallenge);
    }

    [Fact]
    public void EverySignInGetsFreshValues()
    {
        using var first = Pkce.Create();
        using var second = Pkce.Create();

        Assert.NotEqual(first.CodeVerifier, second.CodeVerifier);
        Assert.NotEqual(first.State, second.State);
    }

    [Fact]
    public void OnlyTheExactStateMatches()
    {
        using var pkce = Pkce.Create();

        Assert.True(pkce.StateMatches(pkce.State));
        Assert.False(pkce.StateMatches(null));
        Assert.False(pkce.StateMatches(string.Empty));
        Assert.False(pkce.StateMatches(pkce.State[..^1]));
        Assert.False(pkce.StateMatches(pkce.State + "x"));
        Assert.False(pkce.StateMatches((pkce.State[0] == 'A' ? 'B' : 'A') + pkce.State[1..]));
    }

    [Fact]
    public void DisposingClearsTheBytesAndNothingMatchesAfterwards()
    {
        var pkce = Pkce.Create();
        string state = pkce.State;

        pkce.Dispose();

        Assert.True(pkce.IsCleared);
        Assert.False(pkce.StateMatches(state));
    }
}
