/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Xping.Cli.Auth;
using Xping.Cli.Hosting;
using Xping.Cli.Services;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Commands.Auth;

/// <summary>
/// The command surface phase 0 of the auth spec adds: global options, the auth commands, Ctrl+C and
/// the scrubbing of escaping exceptions.
/// </summary>
public sealed class AuthCommandShellTests
{
    private static (int Code, string Out, string Err) Run(
        string[] args,
        Action<IServiceCollection>? configureServices = null,
        CancellationToken cancellationToken = default)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(
            args, output, error, input: null, isTerminal: true, configureServices, cancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    [Fact]
    public void RootHelpListsTheAuthCommandsAndGlobalOptions()
    {
        var (code, output, _) = Run(["--help"]);

        Assert.Equal(0, code);
        Assert.Contains("login", output, StringComparison.Ordinal);
        Assert.Contains("logout", output, StringComparison.Ordinal);
        Assert.Contains("auth", output, StringComparison.Ordinal);
        Assert.Contains("--cloud-url", output, StringComparison.Ordinal);
        Assert.Contains("--api-key", output, StringComparison.Ordinal);
        Assert.Contains("--verbose", output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheApiKeyHelpSteersTowardsTheEnvironmentVariable()
    {
        var (_, output, _) = Run(["--help"]);

        Assert.Contains("prefer XPING_APIKEY", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("login")]
    [InlineData("logout")]
    [InlineData("auth", "status")]
    public void SubcommandHelpSucceeds(params string[] verb)
    {
        var (code, output, _) = Run([.. verb, "--help"]);

        Assert.Equal(0, code);
        Assert.Contains("Usage:", output, StringComparison.Ordinal);
        Assert.Contains("--json", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("login")]
    [InlineData("logout")]
    [InlineData("auth", "status")]
    public void AnUnimplementedCommandSaysSoOnStderrAndFails(params string[] verb)
    {
        var (code, output, error) = Run(verb);

        Assert.Equal(AuthExitCodes.LoginFailed, code);
        Assert.Empty(output);
        Assert.Contains("not implemented", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AnInvalidCloudUrlIsAParseError()
    {
        var (code, _, error) = Run(["login", "--cloud-url", "http://example.com"]);

        Assert.Equal(2, code);
        Assert.Contains("--cloud-url must use https", error, StringComparison.Ordinal);
        Assert.Contains("Run `xping login --help` for usage.", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--cloud-url", "https://app.xping.io", "where")]
    [InlineData("where", "--cloud-url", "https://app.xping.io")]
    [InlineData("where", "--api-key", "xpg_test_key_123")]
    [InlineData("where", "--verbose")]
    public void GlobalOptionsAreAcceptedBeforeAndAfterTheCommand(params string[] args)
    {
        var (code, _, error) = Run(args, services => services.AddSingleton<ILocalSessionStoreFactory>(new UnavailableStoreFactory()));

        // `where` with no store exits 1; a parse error would be 2.
        Assert.Equal(1, code);
        Assert.Empty(error);
    }

    [Fact]
    public void GlobalOptionsReachTheCommand()
    {
        GlobalOptions? seen = null;

        Run(
            ["--cloud-url", "HTTPS://App.Xping.io/", "--api-key", "xpg_test_key_456", "--verbose", "where"],
            services => services.AddSingleton<ILocalSessionStoreFactory>(sp =>
            {
                seen = sp.GetRequiredService<GlobalOptions>();
                return new UnavailableStoreFactory();
            }));

        Assert.NotNull(seen);
        Assert.Equal("https://app.xping.io", seen.CloudUrl);
        Assert.Equal("xpg_test_key_456", seen.ApiKey);
        Assert.True(seen.Verbose);
    }

    [Fact]
    public void CtrlCStopsTheCommandWithExitCode130()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var (code, output, error) = Run(["login"], cancellationToken: cancellation.Token);

        Assert.Equal(AuthExitCodes.Cancelled, code);
        Assert.Empty(output);
        Assert.Equal("Cancelled." + Environment.NewLine, error);
    }

    [Fact]
    public void AnEscapingExceptionIsReportedScrubbedWithoutAStackTrace()
    {
        string apiKey = "xpg_" + Guid.NewGuid().ToString("N");

        var (code, output, error) = Run(
            ["where", "--api-key", apiKey],
            services => services.AddSingleton<ILocalSessionStoreFactory>(
                new ThrowingStoreFactory($"rejected key {apiKey} with Authorization: Bearer abc")));

        Assert.Equal(1, code);
        Assert.Empty(output);
        Assert.Equal("xping: rejected key [redacted] with Authorization: [redacted]" + Environment.NewLine, error);
    }

    [Fact]
    public void VerboseAddsTheScrubbedStackTrace()
    {
        var (code, _, error) = Run(
            ["where", "--verbose"],
            services => services.AddSingleton<ILocalSessionStoreFactory>(
                new ThrowingStoreFactory("failed with refresh_token=abc")));

        Assert.Equal(1, code);
        Assert.Contains($"{nameof(ThrowingStoreFactory)}.{nameof(ThrowingStoreFactory.Create)}", error, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh_token=abc", error, StringComparison.Ordinal);
    }

    private sealed class UnavailableStoreFactory : ILocalSessionStoreFactory
    {
        public ILocalSessionStore Create(LocalStoreOptions? options = null, string? startDirectory = null) =>
            new UnavailableStore();
    }

    private sealed class UnavailableStore : ILocalSessionStore
    {
        public bool IsAvailable => false;

        public string? StorePath => null;

        public string? SessionsPath => null;

        public bool Write(TestSession session) => false;

        public LocalSessionReadResult ReadRecent(int maxSessions, string? assembly = null) =>
            LocalSessionReadResult.Empty;

        public int Delete(string? assembly = null) => 0;
    }

    private sealed class ThrowingStoreFactory(string message) : ILocalSessionStoreFactory
    {
        public ILocalSessionStore Create(LocalStoreOptions? options = null, string? startDirectory = null) =>
            throw new InvalidOperationException(message);
    }
}
