/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// A printed value reads in the shell as it was meant, and a plain one reads as typed.
/// </summary>
public sealed class ShellQuoteTests
{
    [Theory]
    [InlineData("../svc", "../svc")]
    [InlineData("f_2a91c0de", "f_2a91c0de")]
    [InlineData("FullyQualifiedName=Ns.Outer+Inner.Test", "FullyQualifiedName=Ns.Outer+Inner.Test")]
    [InlineData("/work/My Repo", "'/work/My Repo'")]
    [InlineData("/work/r&d/app", "'/work/r&d/app'")]
    [InlineData("/work/$HOME;rm", "'/work/$HOME;rm'")]
    [InlineData("/work/it's", "'/work/it'\\''s'")]
    [InlineData(@"C:\My Repo\", @"'C:\My Repo\'")]
    [InlineData(@"FullyQualifiedName=Ns.C.Divide\(10,0\)", @"'FullyQualifiedName=Ns.C.Divide\(10,0\)'")]
    [InlineData("A|B", "'A|B'")]
    public void AValueTheShellWouldReadIsSingleQuoted(string value, string printed)
    {
        Assert.Equal(printed, ShellQuote.Of(value));
    }

    [Fact]
    public void AnEmptyValueIsQuotedSoItStillCountsAsAnArgument()
    {
        Assert.Equal("''", ShellQuote.Of(string.Empty));
    }
}
