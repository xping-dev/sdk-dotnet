/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;
using Xping.Cli.Auth;

namespace Xping.Cli.Tests.Auth;

public sealed class PrivateFilesTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "xping-cli-private-files-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task ConcurrentWritersOfOneFileNeverDisturbEachOther()
    {
        string path = Path.Combine(_directory, "entry.json");

        // Before the temporary name was unique per write, one writer deleted another's temporary
        // file and that writer's move failed.
        await Task.WhenAll(Enumerable.Range(0, 32).Select(i => Task.Run(
            () => PrivateFiles.WriteAtomically(path, Encoding.UTF8.GetBytes($"writer {i}")))));

        Assert.StartsWith("writer ", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
        Assert.Equal([path], Directory.GetFiles(_directory));
    }

    [Fact]
    public void EveryMissingLevelIsCreatedPrivate()
    {
        if (OperatingSystem.IsWindows())
            return;

        string leaf = Path.Combine(_directory, "a", "b");

        PrivateFiles.EnsureDirectory(leaf);

        foreach (string level in (string[])[_directory, Path.Combine(_directory, "a"), leaf])
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(level));
    }
}
