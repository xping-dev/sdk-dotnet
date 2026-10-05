/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.PullRequest.Internals;

namespace Xping.Sdk.Core.Tests.Services.PullRequest;

public sealed class PullRequestEnvironmentTests
{
    [Theory]
    [InlineData("https://github.com", "https://github.com")]
    [InlineData("https://GitHub.COM/", "https://github.com")]
    [InlineData("HTTPS://ghes.acme.com", "https://ghes.acme.com")]
    [InlineData("https://ghes.acme.com:443", "https://ghes.acme.com")]
    [InlineData("http://gitlab.acme.local:8080", "http://gitlab.acme.local:8080")]
    [InlineData("https://example.com/gitlab/", "https://example.com/gitlab")]
    [InlineData("https://ghes.acme.com/?tab=1#top", "https://ghes.acme.com")]
    [InlineData("https://user:secret@ghes.acme.com", "https://ghes.acme.com")]
    [InlineData("  https://github.com  ", "https://github.com")]
    public void TryNormalizeServerUrl_AbsoluteHttpUrl_IsNormalized(string raw, string expected)
    {
        // Act
        bool ok = PullRequestEnvironment.TryNormalizeServerUrl(raw, out string? serverUrl);

        // Assert
        Assert.True(ok);
        Assert.Equal(expected, serverUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("github.com")]
    [InlineData("/acme/api")]
    [InlineData("ftp://ghes.acme.com")]
    [InlineData("git@github.com:acme/api.git")]
    public void TryNormalizeServerUrl_NotAnAbsoluteHttpUrl_Fails(string? raw)
    {
        // Act & Assert
        Assert.False(PullRequestEnvironment.TryNormalizeServerUrl(raw, out _));
    }

    [Theory]
    [InlineData("https://github.com/acme/api.git", "https://github.com")]
    [InlineData("https://fabrikam@dev.azure.com/fabrikam/Payments/_git/api", "https://dev.azure.com")]
    [InlineData("https://gitlab.acme.com:8443/group/api", "https://gitlab.acme.com:8443")]
    public void TryGetServerRoot_RepositoryUrl_KeepsOnlySchemeHostAndPort(string raw, string expected)
    {
        // Act
        bool ok = PullRequestEnvironment.TryGetServerRoot(raw, out string? serverUrl);

        // Assert
        Assert.True(ok);
        Assert.Equal(expected, serverUrl);
    }
}
