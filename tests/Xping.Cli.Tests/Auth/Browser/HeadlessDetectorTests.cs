/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Auth.Browser;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Tests.Auth.Browser;

/// <summary>
/// Every row of cli-auth-cli-spec §5.2.
/// </summary>
public sealed class HeadlessDetectorTests
{
    [Theory]
    [InlineData("WSL_DISTRO_NAME")]
    [InlineData("WSL_INTEROP")]
    public void WslIsDetectedBeforeAnythingElse(string variable)
    {
        BrowserEnvironment result = Detect(HostOs.Linux, [variable, "SSH_TTY"]);

        Assert.Equal(BrowserEnvironmentKind.Wsl, result.Kind);
    }

    [Theory]
    [InlineData("SSH_CLIENT")]
    [InlineData("SSH_TTY")]
    [InlineData("SSH_CONNECTION")]
    public void AnSshSessionIsHeadless(string variable)
    {
        BrowserEnvironment result = Detect(HostOs.MacOS, [variable]);

        Assert.Equal(BrowserEnvironment.Headless("SSH session"), result);
    }

    [Fact]
    public void LinuxWithoutADisplayIsHeadless() =>
        Assert.Equal(BrowserEnvironment.Headless("no display"), Detect(HostOs.Linux, []));

    [Theory]
    [InlineData("DISPLAY")]
    [InlineData("WAYLAND_DISPLAY")]
    public void LinuxWithADisplayIsInteractive(string variable) =>
        Assert.Equal(BrowserEnvironment.Interactive, Detect(HostOs.Linux, [variable]));

    [Theory]
    [InlineData("MacOS")]
    [InlineData("Windows")]
    public void ADisplayIsOnlyRequiredOnLinux(string os) =>
        Assert.Equal(BrowserEnvironment.Interactive, Detect(Enum.Parse<HostOs>(os), []));

    [Theory]
    [InlineData("/.dockerenv")]
    [InlineData("/run/.containerenv")]
    public void AContainerMarkerFileIsHeadless(string file) =>
        Assert.Equal(BrowserEnvironment.Headless("container"), Detect(HostOs.Linux, ["DISPLAY"], [file]));

    [Theory]
    [InlineData("REMOTE_CONTAINERS")]
    [InlineData("CODESPACES")]
    [InlineData("DEVCONTAINER")]
    public void AContainerVariableIsHeadless(string variable) =>
        Assert.Equal(BrowserEnvironment.Headless("container"), Detect(HostOs.MacOS, [variable]));

    [Fact]
    public void AnEmptyVariableCountsAsUnset() =>
        Assert.Equal(BrowserEnvironment.Interactive, Detect(HostOs.MacOS, [], variables: new() { ["SSH_TTY"] = string.Empty }));

    private static BrowserEnvironment Detect(
        HostOs os, string[] set, string[]? files = null, Dictionary<string, string>? variables = null)
    {
        variables ??= [];
        foreach (string name in set)
            variables[name] = "1";

        return new HeadlessDetector(new Variables(variables), os, path => files?.Contains(path) == true).Detect();
    }

    private sealed class Variables(Dictionary<string, string> values) : IEnvironmentVariableProvider
    {
        public string? GetVariable(string name) => values.GetValueOrDefault(name);
    }
}
