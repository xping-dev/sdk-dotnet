/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// A printed command runs as printed, whatever the caller typed into its scope.
/// </summary>
public sealed class DrillDownTests
{
    [Theory]
    [InlineData("../svc", "../svc")]
    [InlineData("/work/My Repo", "'/work/My Repo'")]
    [InlineData("/work/r&d/app", "'/work/r&d/app'")]
    [InlineData("/work/$HOME;rm", "'/work/$HOME;rm'")]
    [InlineData("/work/it's", "'/work/it'\\''s'")]
    [InlineData(@"C:\My Repo\", @"'C:\My Repo\'")]
    public void AValueTheShellWouldReadIsSingleQuoted(string directory, string printed)
    {
        Assert.Equal(
            $"xping report --all --directory {printed}",
            DrillDown.ForFullReport(new ReportScope("MyApp.Tests", Directory: directory)));
    }

    [Fact]
    public void AnOrdinaryScopeReadsAsTyped()
    {
        Assert.Equal(
            "xping report --id f_2a91c0de --assembly MyApp.Tests --since 2026-08-01",
            DrillDown.ForFinding("f_2a91c0de", new ReportScope("MyApp.Tests", Since: "2026-08-01")));
    }
}
