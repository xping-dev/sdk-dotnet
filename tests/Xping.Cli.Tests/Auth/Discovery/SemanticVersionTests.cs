/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth.Discovery;

namespace Xping.Cli.Tests.Auth.Discovery;

public sealed class SemanticVersionTests
{
    // The precedence chain of semver.org §11, plus the numeric cases that a string compare gets wrong.
    [Theory]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1")]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta")]
    [InlineData("1.0.0-alpha.beta", "1.0.0-beta")]
    [InlineData("1.0.0-beta", "1.0.0-beta.2")]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.11")]
    [InlineData("1.0.0-beta.11", "1.0.0-rc.1")]
    [InlineData("1.0.0-rc.1", "1.0.0")]
    [InlineData("1.0.0", "2.0.0")]
    [InlineData("2.0.0", "2.1.0")]
    [InlineData("2.1.0", "2.1.1")]
    [InlineData("1.9.0", "1.10.0")]
    public void PrecedenceFollowsTheSpecification(string lower, string higher)
    {
        Assert.True(SemanticVersion.TryParse(lower, out SemanticVersion? low));
        Assert.True(SemanticVersion.TryParse(higher, out SemanticVersion? high));

        Assert.True(low < high);
        Assert.True(high > low);
    }

    [Fact]
    public void BuildMetadataDoesNotAffectPrecedence()
    {
        Assert.True(SemanticVersion.TryParse("1.2.3+abc", out SemanticVersion? left));
        Assert.True(SemanticVersion.TryParse("1.2.3+def", out SemanticVersion? right));

        Assert.Equal(0, left.CompareTo(right));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("v1.2.3")]
    [InlineData("01.2.3")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-01")]
    [InlineData("1.2.3.4")]
    public void AnythingElseIsNotAVersion(string value)
    {
        Assert.False(SemanticVersion.TryParse(value, out _));
    }
}
