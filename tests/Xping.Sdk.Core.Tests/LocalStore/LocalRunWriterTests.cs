/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging.Abstractions;
using Xping.Sdk.Core.Models.Local;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Sdk.Core.Tests.LocalStore;

// Mutates the XPING_LOCAL_STORE environment variable, which is process-wide state.
[Collection("Sequential")]
public sealed class LocalRunWriterTests : IDisposable
{
    private readonly string _root;

    public LocalRunWriterTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-writer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        System.Environment.SetEnvironmentVariable(
            LocalStorePathResolver.EnvironmentVariableName, _root);
    }

    public void Dispose()
    {
        System.Environment.SetEnvironmentVariable(
            LocalStorePathResolver.EnvironmentVariableName, null);

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static LocalRun BuildRun() =>
        new(
            new LocalRunHeader { SessionId = Guid.NewGuid().ToString("N"), StartedAtUtc = DateTime.UtcNow },
            [new LocalTestRecord { Fingerprint = "fp", Name = "T", Outcome = OutcomeCodes.Passed }]);

    [Fact]
    public void WritePersistsTheRun()
    {
        // Arrange
        var store = LocalRunStore.Create();
        var writer = new LocalRunWriter(store, NullLogger<LocalRunWriter>.Instance);

        // Act
        bool written = writer.Write(BuildRun());

        // Assert
        Assert.True(written);
        Assert.Single(store.ReadRecent(10));
    }

    [Fact]
    public void WriteThrowsOnNullRun()
    {
        var writer = new LocalRunWriter(LocalRunStore.Create(), NullLogger<LocalRunWriter>.Instance);

        Assert.Throws<ArgumentNullException>(() => writer.Write(null!));
    }

    [Fact]
    public void WriteSwallowsStorageFailures()
    {
        // Arrange — the local store is a side channel; a failing store must never surface as an
        // exception into a developer's test run.
        var writer = new LocalRunWriter(new ThrowingStore(), NullLogger<LocalRunWriter>.Instance);

        // Act
        bool written = writer.Write(BuildRun());

        // Assert
        Assert.False(written);
    }

    [Fact]
    public void FactoryCreatesAUsableStore()
    {
        // The factory is the supported entry point for reading the store from outside the SDK,
        // which is how the CLI gets at it.
        ILocalRunStore store = LocalRunStore.Create();

        Assert.True(store.IsAvailable);
        Assert.Equal(_root, store.StorePath);
    }

    private sealed class ThrowingStore : ILocalRunStore
    {
        public bool IsAvailable => true;

        public string? StorePath => "/nonexistent";

        public string? RunsPath => "/nonexistent/runs";

        public int Delete(string? assembly = null) => 0;

        public bool Write(LocalRun run) => throw new IOException("disk on fire");

        public IReadOnlyList<LocalRun> ReadRecent(int maxRuns, string? assembly = null) => [];
    }
}
