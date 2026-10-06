/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Xping.Cli.Hosting;

namespace Xping.Cli.Tests.Hosting;

public sealed class VerboseLoggerProviderTests : IDisposable
{
    private readonly StringWriter _error = new();
    private readonly GlobalOptions _options = new();
    private readonly VerboseLoggerProvider _provider;

    public VerboseLoggerProviderTests()
    {
        _provider = new VerboseLoggerProvider(
            new ConsoleIO(TextWriter.Null, _error, TextReader.Null, isTerminal: false), _options);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _error.Dispose();
    }

    [Fact]
    public void NothingIsWrittenWithoutVerbose()
    {
        _provider.CreateLogger("Xping.Cli.Auth").LogInformation("hello");

        Assert.Empty(_error.ToString());
    }

    [Fact]
    public void TheCliCategoriesAreWrittenToStderrWithVerbose()
    {
        _options.Verbose = true;

        _provider.CreateLogger("Xping.Cli.Auth").LogInformation("hello");

        Assert.Equal("hello" + Environment.NewLine, _error.ToString());
    }

    [Fact]
    public void OtherCategoriesStaySilentWithVerbose()
    {
        _options.Verbose = true;

        _provider.CreateLogger("System.Net.Http.HttpClient").LogInformation("GET https://example");
        _provider.CreateLogger("Xping.Sdk.Core.Uploader").LogInformation("upload");

        Assert.Empty(_error.ToString());
    }

    [Fact]
    public void DebugMessagesStaySilentWithVerbose()
    {
        _options.Verbose = true;

        _provider.CreateLogger("Xping.Cli.Auth").LogDebug("detail");

        Assert.Empty(_error.ToString());
    }

    [Fact]
    public void MessagesAndExceptionsAreScrubbed()
    {
        _options.Verbose = true;

        _provider.CreateLogger("Xping.Cli.Auth").LogWarning(
            new InvalidOperationException("refresh_token=abc failed"), "sent code={Code}", "xyz");

        string written = _error.ToString();
        Assert.Contains("sent code=[redacted]", written, StringComparison.Ordinal);
        Assert.Contains("refresh_token=[redacted] failed", written, StringComparison.Ordinal);
        Assert.DoesNotContain("xyz", written, StringComparison.Ordinal);
        Assert.DoesNotContain("abc", written, StringComparison.Ordinal);
    }
}
