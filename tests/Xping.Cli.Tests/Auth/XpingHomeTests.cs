/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth;

namespace Xping.Cli.Tests.Auth;

public sealed class XpingHomeTests
{
    private static readonly string Profile = Path.Combine(Path.GetTempPath(), "xping-home-tests", "jane");

    [Fact]
    public void APathBelowTheProfileIsShownFromTilde() =>
        Assert.Equal(
            "~/.xping/credentials.json",
            XpingHome.Display(Path.Combine(Profile, ".xping", "credentials.json"), Profile));

    [Fact]
    public void APathOutsideTheProfileIsShownAsItIs()
    {
        string path = Path.Combine(Path.GetTempPath(), "elsewhere", "credentials.json");

        Assert.Equal(path, XpingHome.Display(path, Profile));
    }

    [Fact]
    public void ASiblingWhoseNameStartsWithDotsIsNotMistakenForAParent()
    {
        string sibling = Path.Combine(Profile, "..jane", "credentials.json");

        Assert.Equal("~/..jane/credentials.json", XpingHome.Display(sibling, Profile));
    }

    [Fact]
    public void WithoutAProfileThePathIsShownAsItIs()
    {
        // A container user with no HOME and no passwd entry. Before the fix, GetRelativePath threw
        // ArgumentException on the empty profile before the guard was reached.
        string path = Path.Combine(Profile, ".xping", "credentials.json");

        Assert.Equal(path, XpingHome.Display(path, string.Empty));
    }

    [Fact]
    public void TheCredentialsFileIsInTheRoot() =>
        Assert.Equal(Path.Combine(Profile, ".xping", "credentials.json"), new XpingHome(Path.Combine(Profile, ".xping")).CredentialsFile);
}
