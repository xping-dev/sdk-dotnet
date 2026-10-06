/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Configuration;

namespace Xping.Cli.Tests.Configuration;

public sealed class CloudUrlTests
{
    [Theory]
    [InlineData("https://app.xping.io", "https://app.xping.io")]
    [InlineData("https://app.xping.io/", "https://app.xping.io")]
    [InlineData("HTTPS://App.Xping.IO/", "https://app.xping.io")]
    [InlineData("https://app.xping.io:443", "https://app.xping.io")]
    [InlineData("https://app.xping.io:8443/", "https://app.xping.io:8443")]
    [InlineData("  https://app.xping.io  ", "https://app.xping.io")]
    [InlineData("http://localhost:5000", "http://localhost:5000")]
    [InlineData("http://127.0.0.1:5000/", "http://127.0.0.1:5000")]
    [InlineData("http://LOCALHOST", "http://localhost")]
    [InlineData("https://[::1]:5001", "https://[::1]:5001")]
    public void TwoSpellingsOfOneServerNormalizeToOneKey(string value, string expected)
    {
        Assert.True(CloudUrl.TryNormalize(value, "--cloud-url", out string? normalized, out string? error), error);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("app.xping.io")]
    [InlineData("/relative")]
    [InlineData("")]
    [InlineData("ftp://app.xping.io")]
    [InlineData("http://app.xping.io")]
    [InlineData("http://[::1]:5000")]
    [InlineData("http://0.0.0.0")]
    [InlineData("https://user:pass@app.xping.io")]
    [InlineData("https://app.xping.io/?")]
    [InlineData("https://app.xping.io/?a=b")]
    [InlineData("https://app.xping.io/#top")]
    [InlineData("https://app.xping.io/portal")]
    [InlineData("https://app.xping.io//")]
    public void AnUnusableUrlIsRefused(string value)
    {
        Assert.False(CloudUrl.TryNormalize(value, "--cloud-url", out _, out string? error));
        Assert.StartsWith("--cloud-url ", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRefusalNamesWhereTheValueCameFrom()
    {
        Assert.False(CloudUrl.TryNormalize("http://example.com", "XPING_CLOUDURL", out _, out string? error));
        Assert.StartsWith("XPING_CLOUDURL must use https", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://user:s3cret@example.com")]
    [InlineData("ftp://user:s3cret@example.com")]
    [InlineData("https://app.xping.io/?token=s3cret")]
    [InlineData("https://app.xping.io/#s3cret")]
    [InlineData("http://example.com/?s3cret")]
    [InlineData("not a url s3cret")]
    public void TheRefusalNeverQuotesASecretFromTheValue(string value)
    {
        Assert.False(CloudUrl.TryNormalize(value, "--cloud-url", out _, out string? error));
        Assert.DoesNotContain("s3cret", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRefusalQuotesTheHarmlessPartsToHelpFixATypo()
    {
        Assert.False(CloudUrl.TryNormalize("https://App.Xping.io/portal", "--cloud-url", out _, out string? error));
        Assert.Equal("--cloud-url must not contain a path, got 'https://app.xping.io/portal'.", error);
    }

    [Fact]
    public void TheDefaultIsAlreadyNormalized()
    {
        Assert.True(CloudUrl.TryNormalize(CloudUrl.Default, "default", out string? normalized, out _));
        Assert.Equal(CloudUrl.Default, normalized);
    }
}
