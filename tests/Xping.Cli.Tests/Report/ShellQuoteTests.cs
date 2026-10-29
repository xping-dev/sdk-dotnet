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
    [InlineData("/work/it\u2019s", "'/work/it\u2019s'")]
    [InlineData(@"C:\My Repo\", @"'C:\My Repo\'")]
    [InlineData(@"FullyQualifiedName=Ns.C.Divide\(10,0\)", @"'FullyQualifiedName=Ns.C.Divide\(10,0\)'")]
    [InlineData("A|B", "'A|B'")]
    [InlineData("a,b", "'a,b'")]
    [InlineData("@x", "'@x'")]
    public void InAPosixShellAValueTheShellWouldReadIsSingleQuoted(string value, string printed)
    {
        Assert.Equal(printed, ShellQuote.Of(value, ShellDialect.Posix));
    }

    /// <summary>
    /// PowerShell reads a bare <c>a,b</c> as an array and a leading <c>@</c> as a splat, and closes a
    /// single-quoted string on a typographic quote as well as on <c>'</c>.
    /// </summary>
    [Theory]
    [InlineData("../svc", "../svc")]
    [InlineData("FullyQualifiedName=Ns.Outer+Inner.Test", "FullyQualifiedName=Ns.Outer+Inner.Test")]
    [InlineData("/work/My Repo", "'/work/My Repo'")]
    [InlineData("/work/$HOME;rm", "'/work/$HOME;rm'")]
    [InlineData("/work/it's", "'/work/it''s'")]
    [InlineData("/work/it\u2019s", "'/work/it\u2019\u2019s'")]
    [InlineData("\u2018a\u201Ab\u201B", "'\u2018\u2018a\u201A\u201Ab\u201B\u201B'")]
    [InlineData(@"C:\My Repo\", @"'C:\My Repo\'")]
    [InlineData("a,b", "'a,b'")]
    [InlineData("@x", "'@x'")]
    public void InPowerShellAnEmbeddedQuoteIsDoubled(string value, string printed)
    {
        Assert.Equal(printed, ShellQuote.Of(value, ShellDialect.PowerShell));
    }

    [Fact]
    public void AnEmptyValueIsQuotedSoItStillCountsAsAnArgument()
    {
        Assert.Equal("''", ShellQuote.Of(string.Empty, ShellDialect.Posix));
        Assert.Equal("''", ShellQuote.Of(string.Empty, ShellDialect.PowerShell));
    }
}
