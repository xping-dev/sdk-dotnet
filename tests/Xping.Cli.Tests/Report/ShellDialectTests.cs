/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth.Browser;
using Xping.Cli.Report;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// <c>XPING_SHELL</c> names the shell when it is set, and the OS stands in when it is not.
/// </summary>
public sealed class ShellDialectTests
{
    [Fact]
    public void UnsetWindowsGetsPowerShellAndEveryOtherOsPosix()
    {
        Assert.Equal((ShellDialect.PowerShell, null), ShellDialects.Resolve(null, HostOs.Windows));
        Assert.Equal((ShellDialect.Posix, null), ShellDialects.Resolve(null, HostOs.Linux));
        Assert.Equal((ShellDialect.Posix, null), ShellDialects.Resolve(null, HostOs.MacOS));
        Assert.Equal((ShellDialect.Posix, null), ShellDialects.Resolve(null, HostOs.Other));
        Assert.Equal((ShellDialect.PowerShell, null), ShellDialects.Resolve("  ", HostOs.Windows));
    }

    /// <summary>Git Bash on Windows is the case the OS gets wrong, and the one the variable is for.</summary>
    [Theory]
    [InlineData("posix")]
    [InlineData("POSIX")]
    [InlineData(" Posix ")]
    public void PosixWinsOverTheOs(string setting)
    {
        Assert.Equal((ShellDialect.Posix, null), ShellDialects.Resolve(setting, HostOs.Windows));
    }

    [Theory]
    [InlineData("powershell")]
    [InlineData("PowerShell")]
    public void PowerShellWinsOverTheOs(string setting)
    {
        Assert.Equal((ShellDialect.PowerShell, null), ShellDialects.Resolve(setting, HostOs.Linux));
    }

    [Fact]
    public void AnUnknownValueIsWarnedAboutAndTheOsDecides()
    {
        Assert.Equal(
            (ShellDialect.PowerShell, "xping: XPING_SHELL=\"bash\" is not posix or powershell; commands are quoted for powershell."),
            ShellDialects.Resolve("bash", HostOs.Windows));
        Assert.Equal(
            (ShellDialect.Posix, "xping: XPING_SHELL=\"pwsh\" is not posix or powershell; commands are quoted for posix."),
            ShellDialects.Resolve("pwsh", HostOs.Linux));
    }
}
