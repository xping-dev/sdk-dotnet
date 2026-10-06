/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging.Abstractions;
using Xping.Cli.Auth.Browser;

namespace Xping.Cli.Tests.Auth.Browser;

/// <summary>
/// The launcher commands per operating system (cli-auth-cli-spec §5.1).
/// </summary>
public sealed class BrowserLauncherTests
{
    private static readonly Uri Url = new("https://app.xping.io/connect/authorize?response_type=code&client_id=xping-cli&state=abc");

    [Fact]
    public void WindowsHandsTheUrlToTheShell()
    {
        var process = new FakeProcess();

        Assert.True(Launch(HostOs.Windows, process));

        AssertRan(process, "shell", Url.AbsoluteUri);
    }

    [Fact]
    public void MacOsUsesOpenWithTheUrlAsOneArgument()
    {
        var process = new FakeProcess();

        Assert.True(Launch(HostOs.MacOS, process));

        AssertRan(process, "open", Url.AbsoluteUri);
    }

    [Fact]
    public void LinuxPrefersXdgOpen()
    {
        var process = new FakeProcess("xdg-open", "gio", "sensible-browser");

        Assert.True(Launch(HostOs.Linux, process));

        AssertRan(process, "xdg-open", Url.AbsoluteUri);
    }

    [Fact]
    public void LinuxFallsBackToGioThenSensibleBrowser()
    {
        var gio = new FakeProcess("gio", "sensible-browser");
        Assert.True(Launch(HostOs.Linux, gio));
        AssertRan(gio, "gio", "open", Url.AbsoluteUri);

        var sensible = new FakeProcess("sensible-browser");
        Assert.True(Launch(HostOs.Linux, sensible));
        AssertRan(sensible, "sensible-browser", Url.AbsoluteUri);
    }

    [Fact]
    public void LinuxWithNoLauncherGivesUp()
    {
        var process = new FakeProcess();

        Assert.False(Launch(HostOs.Linux, process));
        Assert.Empty(process.Runs);
    }

    [Fact]
    public void WslPrefersWslview()
    {
        var process = new FakeProcess("wslview");

        Assert.True(Launch(HostOs.Linux, process, BrowserEnvironment.Wsl));

        AssertRan(process, "wslview", Url.AbsoluteUri);
    }

    [Fact]
    public void WslWithoutWslviewUsesRundll32()
    {
        var process = new FakeProcess();

        Assert.True(Launch(HostOs.Linux, process, BrowserEnvironment.Wsl));

        AssertRan(process, BrowserLauncher.WslRundll32, "url.dll,FileProtocolHandler", Url.AbsoluteUri);
    }

    [Fact]
    public void ALauncherThatFailsReportsNotOpened()
    {
        var process = new FakeProcess { Succeeds = false };

        Assert.False(Launch(HostOs.MacOS, process));
    }

    [Fact]
    public void ALauncherThatThrowsReportsNotOpened()
    {
        var process = new FakeProcess { Throws = true };

        Assert.False(Launch(HostOs.MacOS, process));
    }

    [Fact]
    public void TheWaitIsCappedAtTwoSeconds()
    {
        var process = new FakeProcess();

        Launch(HostOs.MacOS, process);

        Assert.Equal(TimeSpan.FromSeconds(2), process.LastWait);
    }

    private static void AssertRan(FakeProcess process, string file, params string[] arguments)
    {
        (string ranFile, IReadOnlyList<string> ranArguments) = Assert.Single(process.Runs);
        Assert.Equal(file, ranFile);
        Assert.Equal(arguments, ranArguments);
    }

    private static bool Launch(HostOs os, FakeProcess process, BrowserEnvironment? environment = null) =>
        new BrowserLauncher(process, os, NullLogger<BrowserLauncher>.Instance)
            .TryOpen(Url, environment ?? BrowserEnvironment.Interactive);

    private sealed class FakeProcess(params string[] onPath) : ILauncherProcess
    {
        public List<(string File, IReadOnlyList<string> Arguments)> Runs { get; } = [];

        public bool Succeeds { get; init; } = true;

        public bool Throws { get; init; }

        public TimeSpan? LastWait { get; private set; }

        public bool IsOnPath(string name) => onPath.Contains(name);

        public bool TryRun(string fileName, IReadOnlyList<string> arguments, TimeSpan wait)
        {
            if (Throws)
                throw new InvalidOperationException("No such file.");

            Runs.Add((fileName, [.. arguments]));
            LastWait = wait;
            return Succeeds;
        }

        public bool TryShellOpen(string url)
        {
            Runs.Add(("shell", [url]));
            return Succeeds;
        }
    }
}
