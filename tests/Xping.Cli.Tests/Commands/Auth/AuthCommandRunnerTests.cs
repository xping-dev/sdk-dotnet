/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Tests.Cloud;

namespace Xping.Cli.Tests.Commands.Auth;

/// <summary>
/// What every auth command shares: a configuration that does not load is still a JSON document.
/// </summary>
public sealed class AuthCommandRunnerTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly CliFlowHost _host = new();

    public Task InitializeAsync() => Task.CompletedTask;

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Theory]
    [InlineData("login")]
    [InlineData("logout")]
    [InlineData("auth", "status")]
    public void AnInvalidConfiguredCloudUrlIsAFailureDocumentUnderJson(params string[] verb)
    {
        _host.PassCloudUrl = false;
        _host.Environment["XPING_CLOUDURL"] = "ftp://app.xping.io";

        CliResult result = _host.Run([.. verb, "--json"]);

        Assert.Equal(2, result.Code);
        JsonElement json = result.Json();
        Assert.Equal("1", json.GetProperty("schemaVersion").GetString());
        Assert.Equal("failed", json.GetProperty("result").GetString());
        Assert.Equal("configuration", json.GetProperty("error").GetString());
        Assert.Contains("XPING_CLOUDURL", json.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("XPING_CLOUDURL", result.Error, StringComparison.Ordinal);
        Assert.Empty(_host.Cloud.Requests);
    }
}
