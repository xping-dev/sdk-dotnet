/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Windowing;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// A printed command runs as printed, whatever the caller typed into its scope.
/// </summary>
public sealed class DrillDownTests
{
    [Theory]
    [InlineData("../svc", "../svc")]
    [InlineData("/work/My Repo", "'/work/My Repo'")]
    public void AValueTheShellWouldReadIsSingleQuoted(string directory, string printed)
    {
        Assert.Equal(
            $"xping report --all --assembly MyApp.Tests --directory {printed}",
            DrillDown.ForFullReport(new ReportScope("MyApp.Tests", WindowResolution.Default, 20, ShellDialect.Posix, Directory: directory)));
    }

    /// <summary>Amendment 8: every value of the command is quoted for the scope's shell.</summary>
    [Fact]
    public void InPowerShellAnEmbeddedQuoteIsDoubled()
    {
        var scope = new ReportScope("It's.Tests", WindowResolution.SinceDate, 20, ShellDialect.PowerShell, Since: "a,b", Directory: "/work/it's");

        Assert.Equal(
            "xping report --id f_2a91c0de --assembly 'It''s.Tests' --since 'a,b' --directory '/work/it''s'",
            DrillDown.ForFinding("f_2a91c0de", scope));
        Assert.Equal(
            "xping report --test 3a597eb2 --assembly 'It''s.Tests' --directory '/work/it''s'",
            DrillDown.ForTest("3a597eb2", scope));
    }

    [Fact]
    public void AnOrdinaryScopeReadsAsTyped()
    {
        Assert.Equal(
            "xping report --id f_2a91c0de --assembly MyApp.Tests --since 2026-08-01",
            DrillDown.ForFinding("f_2a91c0de", new ReportScope("MyApp.Tests", WindowResolution.SinceDate, 20, ShellDialect.Posix, Since: "2026-08-01")));
    }

    [Fact]
    public void AWholeStoreWindowIsPinnedToItsSize()
    {
        var scope = new ReportScope("MyApp.Tests", WindowResolution.Store, 48, ShellDialect.Posix);

        Assert.Equal("xping report --id f_2a91c0de --assembly MyApp.Tests --runs 48", DrillDown.ForFinding("f_2a91c0de", scope));
        Assert.Equal("xping report --all --assembly MyApp.Tests --runs 48", DrillDown.ForFullReport(scope));
    }

    [Fact]
    public void TheRunsTheCallerTypedWinOverTheWindowSize()
    {
        var scope = new ReportScope("MyApp.Tests", WindowResolution.Runs, 5, ShellDialect.Posix, Runs: 5);

        Assert.Equal("xping report --all --assembly MyApp.Tests --runs 5", DrillDown.ForFullReport(scope));
    }

    [Fact]
    public void ADefaultWindowIsNotPinned()
    {
        Assert.Equal(
            "xping report --all --assembly MyApp.Tests",
            DrillDown.ForFullReport(new ReportScope("MyApp.Tests", WindowResolution.Default, 20, ShellDialect.Posix)));
    }

    /// <summary>
    /// Report-format Amendment 6H: a bare <c>--all</c> over a store that gains a second assembly opens
    /// the overview, so the command names the assembly even when the caller never typed it.
    /// </summary>
    [Fact]
    public void TheFullReportCommandNamesTheAssemblyTheCallerNeverTyped()
    {
        Assert.Equal(
            "xping report --all --assembly 'My App.Tests'",
            DrillDown.ForFullReport(new ReportScope("My App.Tests", WindowResolution.Default, 20, ShellDialect.Posix)));
    }

    [Fact]
    public void AnAssemblysReportCommandCarriesTheOverviewsWindow()
    {
        var scope = new ReportScope(
            "My App.Tests", WindowResolution.Runs, 5, ShellDialect.Posix, Runs: 5, Directory: "/work/My Repo");

        Assert.Equal(
            "xping report --assembly 'My App.Tests' --runs 5 --directory '/work/My Repo'",
            DrillDown.ForAssembly(scope));
    }

    /// <summary>
    /// D11: the history's own window is what a reader following the link wants, so none is repeated.
    /// </summary>
    [Fact]
    public void AHistoryNamesTheAssemblyAndDirectoryAndNoWindow()
    {
        var scope = new ReportScope(
            "MyApp.Tests", WindowResolution.Store, 48, ShellDialect.Posix, Runs: 5, Since: "2026-08-01", Directory: "/work/My Repo");

        Assert.Equal(
            "xping report --test 3a597eb2 --assembly MyApp.Tests --directory '/work/My Repo'",
            DrillDown.ForTest("3a597eb2", scope));
    }
}
