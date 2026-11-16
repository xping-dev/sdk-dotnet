/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging.Abstractions;
using Xping.Cli.Commands;
using Xping.Cli.Hosting;
using Xping.Cli.Services;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Tests.Commands;

public sealed class WhereCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xping-where-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void TheSizeOnDiskIncludesTempFilesButTheRunCountDoesNot()
    {
        var environment = new StoreVariable(_root);
        var factory = new LocalSessionStoreFactory(NullLoggerFactory.Instance, environment);

        string sessions = factory.Create().SessionsPath!;
        Directory.CreateDirectory(sessions);
        string run = Path.Combine(sessions, "session-0-0.json.gz");
        File.WriteAllBytes(run, new byte[1024]);
        File.WriteAllBytes(run + ".tmp", new byte[2048]);

        using var output = new StringWriter();
        var io = new ConsoleIO(output, TextWriter.Null, TextReader.Null, Terminals.All(false));
        var command = new WhereCommand(factory, io, environment);

        Assert.Equal(0, command.Run(directory: null));
        Assert.Contains("  1 run | 3.0 KB on disk", output.ToString(), StringComparison.Ordinal);
    }

    private sealed class StoreVariable(string root) : IEnvironmentVariableProvider
    {
        public string? GetVariable(string name) => name == LocalSessionStoreFactory.StoreVariable ? root : null;
    }
}
