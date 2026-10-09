/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth.Browser;

namespace Xping.Cli.Tests.Auth.Browser;

/// <summary>
/// Real processes through the Unix wrapper that detaches the launcher's streams.
/// </summary>
public sealed class LauncherProcessTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public void ALauncherThatExitsZeroCountsAsOpened()
    {
        if (OperatingSystem.IsWindows())
            return;

        Assert.True(new LauncherProcess().TryRun("true", [], Wait));
    }

    [Fact]
    public void ALauncherThatExitsNonZeroCountsAsNotOpened()
    {
        if (OperatingSystem.IsWindows())
            return;

        Assert.False(new LauncherProcess().TryRun("false", [], Wait));
    }

    [Fact]
    public void AMissingLauncherCountsAsNotOpened()
    {
        if (OperatingSystem.IsWindows())
            return;

        Assert.False(new LauncherProcess().TryRun("xping-no-such-launcher", [], Wait));
    }

    [Fact]
    public void ArgumentsArriveVerbatimAndAreNeverParsedByTheShell()
    {
        if (OperatingSystem.IsWindows())
            return;

        // test exits 0 only when its three arguments are exactly x, =, and the URL with its & and ;
        // intact; a shell that parsed the URL would have split it or run the part after ';'.
        const string Url = "https://app.xping.io/a?b=1&c=$(false);false";

        Assert.True(new LauncherProcess().TryRun("test", ["x", "=", "x"], Wait));
        Assert.True(new LauncherProcess().TryRun("test", [Url, "=", Url], Wait));
        Assert.False(new LauncherProcess().TryRun("test", [Url, "=", "other"], Wait));
    }

    [Fact]
    public void TheLaunchersOutputGoesNowhere()
    {
        if (OperatingSystem.IsWindows())
            return;

        // Would block or reach the test runner's output if the streams were inherited or piped.
        Assert.True(new LauncherProcess().TryRun("echo", ["noise"], Wait));
    }
}
