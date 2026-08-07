/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Sdk.Core.Models.Local;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Sdk.Core.Tests.LocalStore;

// Mutates the XPING_LOCAL_STORE environment variable, which is process-wide state.
[Collection("Sequential")]
public sealed class JsonLinesRunStoreTests : IDisposable
{
    private readonly string _root;

    public JsonLinesRunStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-store-tests", Guid.NewGuid().ToString("N"));
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

    private static JsonLinesRunStore CreateStore(LocalStoreOptions? options = null) =>
        new(options ?? new LocalStoreOptions(), NullLogger.Instance);

    private static LocalRun BuildRun(DateTime startedAt, int testCount = 2, string prefix = "T")
    {
        var records = Enumerable.Range(0, testCount).Select(i => new LocalTestRecord
        {
            Fingerprint = $"fingerprint-{prefix}{i}",
            Name = $"{prefix}{i}",
            Outcome = i % 2 == 0 ? OutcomeCodes.Passed : OutcomeCodes.Failed,
            DurationMs = 100 + i,
            Attempt = 1
        }).ToList();

        var header = new LocalRunHeader
        {
            SessionId = Guid.NewGuid().ToString("N"),
            StartedAtUtc = startedAt,
            DurationMs = 5000,
            Environment = "Local"
        };

        return new LocalRun(header, records);
    }

    [Fact]
    public void WriteThenReadRoundTripsRecords()
    {
        // Arrange
        var store = CreateStore();
        var run = BuildRun(DateTime.UtcNow, testCount: 3);

        // Act
        Assert.True(store.Write(run));
        var read = store.ReadRecent(10);

        // Assert
        var single = Assert.Single(read);
        Assert.Equal(3, single.Records.Count);
        Assert.Equal(run.Header.SessionId, single.Header.SessionId);
        Assert.Equal("fingerprint-T0", single.Records[0].Fingerprint);
        Assert.Equal(OutcomeCodes.Passed, single.Records[0].Outcome);
        Assert.Equal("Local", single.Header.Environment);
    }

    [Fact]
    public void PreservesFullFingerprint()
    {
        // Arrange — the full 64-character fingerprint is what lets local history join against
        // cloud identity, so it must survive a round trip untruncated.
        string fingerprint = new('a', 64);
        var store = CreateStore();
        var run = new LocalRun(
            new LocalRunHeader { SessionId = "s", StartedAtUtc = DateTime.UtcNow },
            [new LocalTestRecord { Fingerprint = fingerprint, Name = "T", Outcome = OutcomeCodes.Passed }]);

        // Act
        store.Write(run);

        // Assert
        Assert.Equal(fingerprint, store.ReadRecent(1)[0].Records[0].Fingerprint);
    }

    [Fact]
    public void ReadsRunsOldestFirst()
    {
        // Arrange
        var store = CreateStore();
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        store.Write(BuildRun(baseTime.AddMinutes(2), prefix: "second"));
        store.Write(BuildRun(baseTime.AddMinutes(1), prefix: "first"));
        store.Write(BuildRun(baseTime.AddMinutes(3), prefix: "third"));

        // Act
        var runs = store.ReadRecent(10);

        // Assert — chronological order matters: the sparkline reads left to right as time.
        Assert.Equal(3, runs.Count);
        Assert.StartsWith("first", runs[0].Records[0].Name, StringComparison.Ordinal);
        Assert.StartsWith("second", runs[1].Records[0].Name, StringComparison.Ordinal);
        Assert.StartsWith("third", runs[2].Records[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadRecentReturnsTheNewestRunsWhenLimited()
    {
        // Arrange
        var store = CreateStore();
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 5; i++)
            store.Write(BuildRun(baseTime.AddMinutes(i), prefix: $"run{i}_"));

        // Act
        var runs = store.ReadRecent(2);

        // Assert
        Assert.Equal(2, runs.Count);
        Assert.StartsWith("run3_", runs[0].Records[0].Name, StringComparison.Ordinal);
        Assert.StartsWith("run4_", runs[1].Records[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void EnforcesMaxRunsRetention()
    {
        // Arrange
        var store = CreateStore(new LocalStoreOptions { MaxRuns = 3 });
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        for (int i = 0; i < 8; i++)
            store.Write(BuildRun(baseTime.AddMinutes(i), prefix: $"run{i}_"));

        // Assert — oldest pruned, newest kept.
        var runs = store.ReadRecent(100);
        Assert.Equal(3, runs.Count);
        Assert.StartsWith("run5_", runs[0].Records[0].Name, StringComparison.Ordinal);
        Assert.StartsWith("run7_", runs[2].Records[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void EnforcesMaxAgeRetention()
    {
        // Arrange
        var store = CreateStore(new LocalStoreOptions { MaxAge = TimeSpan.FromMilliseconds(1) });
        store.Write(BuildRun(DateTime.UtcNow.AddDays(-1), prefix: "old"));

        // Age is judged by file write time, so let the first file fall outside the window.
        Thread.Sleep(30);

        // Act
        store.Write(BuildRun(DateTime.UtcNow, prefix: "new"));

        // Assert
        var runs = store.ReadRecent(100);
        Assert.Single(runs);
        Assert.StartsWith("new", runs[0].Records[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void EnforcesMaxBytesRetention()
    {
        // Arrange — a byte budget too small for two runs.
        var store = CreateStore(new LocalStoreOptions { MaxBytes = 1 });
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        store.Write(BuildRun(baseTime, prefix: "first"));
        store.Write(BuildRun(baseTime.AddMinutes(1), prefix: "second"));

        // Assert — at least the newest run always survives.
        var runs = store.ReadRecent(100);
        Assert.True(runs.Count <= 1);
    }

    [Fact]
    public void CreatesSelfIgnoringGitIgnore()
    {
        // Arrange
        var store = CreateStore();

        // Act
        store.Write(BuildRun(DateTime.UtcNow));

        // Assert — a nested .gitignore keeps the store out of git without touching the
        // repository's own .gitignore.
        string path = Path.Combine(_root, ".gitignore");
        Assert.True(File.Exists(path));
        Assert.Contains("*", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void RecoversPartialRecordsFromATruncatedFile()
    {
        // Arrange — simulate a test host killed mid-write: valid header, two valid records, then a
        // torn line. Everything before the tear must survive.
        var store = CreateStore();
        LocalStorePathResolver.EnsureCreated(_root);

        string path = Path.Combine(
            LocalStorePathResolver.GetRunsDirectory(_root),
            "run-0638000000000000000-abcdef01.jsonl.gz");

        using (var file = new FileStream(path, FileMode.Create))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            writer.WriteLine("{\"v\":1,\"sid\":\"abc\",\"ts\":\"2026-01-01T00:00:00Z\"}");
            writer.WriteLine("{\"f\":\"fp1\",\"n\":\"A\",\"o\":\"P\",\"d\":1,\"r\":1}");
            writer.WriteLine("{\"f\":\"fp2\",\"n\":\"B\",\"o\":\"F\",\"d\":2,\"r\":1}");
            writer.Write("{\"f\":\"fp3\",\"n\":\"C\",\"o\":");  // torn
        }

        // Act
        var runs = store.ReadRecent(10);

        // Assert
        var run = Assert.Single(runs);
        Assert.Equal(2, run.Records.Count);
        Assert.Equal("A", run.Records[0].Name);
        Assert.Equal("B", run.Records[1].Name);
    }

    [Fact]
    public void SkipsCorruptFilesWithoutFailing()
    {
        // Arrange
        var store = CreateStore();
        store.Write(BuildRun(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), prefix: "good"));

        LocalStorePathResolver.EnsureCreated(_root);
        File.WriteAllText(
            Path.Combine(
                LocalStorePathResolver.GetRunsDirectory(_root),
                "run-0638999999999999999-deadbeef.jsonl.gz"),
            "this is not gzip");

        // Act — one unreadable file must cost that file, not the whole report.
        var runs = store.ReadRecent(10);

        // Assert
        var run = Assert.Single(runs);
        Assert.StartsWith("good", run.Records[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsRunsWrittenByANewerSchema()
    {
        // Arrange — forward compatibility: a newer SDK's file must be ignored, not crash the reader.
        var store = CreateStore();
        LocalStorePathResolver.EnsureCreated(_root);

        string path = Path.Combine(
            LocalStorePathResolver.GetRunsDirectory(_root),
            "run-0638000000000000001-abcdef02.jsonl.gz");

        using (var file = new FileStream(path, FileMode.Create))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            writer.WriteLine("{\"v\":999,\"sid\":\"future\",\"ts\":\"2026-01-01T00:00:00Z\"}");
            writer.WriteLine("{\"f\":\"fp\",\"n\":\"A\",\"o\":\"P\"}");
        }

        // Act
        var runs = store.ReadRecent(10);

        // Assert
        Assert.Empty(runs);
    }

    [Fact]
    public void ReadRecentReturnsEmptyWhenNothingStored()
    {
        Assert.Empty(CreateStore().ReadRecent(10));
    }

    [Fact]
    public void ReadRecentReturnsEmptyForNonPositiveCount()
    {
        var store = CreateStore();
        store.Write(BuildRun(DateTime.UtcNow));

        Assert.Empty(store.ReadRecent(0));
        Assert.Empty(store.ReadRecent(-1));
    }

    [Fact]
    public void ReadRecentScopesResultsToTheRequestedAssembly()
    {
        // Arrange — a solution with three test projects all sharing one store. Without scoping, a
        // project's report would show other projects' tests and count their runs as its own.
        var store = CreateStore();
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 4; i++)
        {
            foreach (string assembly in new[] { "Alpha.Tests", "Beta.Tests", "Gamma.Tests" })
            {
                var run = BuildRun(baseTime.AddSeconds((i * 3) + assembly.Length), prefix: assembly);
                run.Header.Assembly = assembly;
                store.Write(run);
            }
        }

        // Act
        var alpha = store.ReadRecent(12, "Alpha.Tests");
        var unfiltered = store.ReadRecent(12);

        // Assert
        Assert.Equal(4, alpha.Count);
        Assert.All(alpha, r => Assert.Equal("Alpha.Tests", r.Header.Assembly));
        Assert.Equal(12, unfiltered.Count);
    }

    [Fact]
    public void ReadRecentReturnsNewestMatchesWhenTheAssemblyIsInterleaved()
    {
        // Arrange
        var store = CreateStore();
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 10; i++)
        {
            var run = BuildRun(baseTime.AddMinutes(i), prefix: $"r{i}_");
            run.Header.Assembly = i % 2 == 0 ? "Even.Tests" : "Odd.Tests";
            store.Write(run);
        }

        // Act
        var even = store.ReadRecent(2, "Even.Tests");

        // Assert — newest two of the matching assembly, oldest first.
        Assert.Equal(2, even.Count);
        Assert.StartsWith("r6_", even[0].Records[0].Name, StringComparison.Ordinal);
        Assert.StartsWith("r8_", even[1].Records[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void ConcurrentWritersDoNotCorruptEachOther()
    {
        // Arrange — several test assemblies in one solution run write at the same time. One file per
        // run is what makes this safe, so prove it.
        var options = new LocalStoreOptions { MaxRuns = 100 };
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        Parallel.For(0, 20, i =>
        {
            var store = CreateStore(options);
            store.Write(BuildRun(baseTime.AddSeconds(i), testCount: 5, prefix: $"p{i}_"));
        });

        // Assert
        var runs = CreateStore(options).ReadRecent(100);
        Assert.Equal(20, runs.Count);
        Assert.All(runs, r => Assert.Equal(5, r.Records.Count));
    }
}
