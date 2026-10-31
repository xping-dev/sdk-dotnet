/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Sdk.Core.Tests.LocalStore;

// One test sets XPING_LOCAL_STORE, which is process-wide state.
[Collection("Sequential")]
public sealed class JsonSessionStoreTests : IDisposable
{
    private readonly string _root;

    public JsonSessionStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-session-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

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

    private JsonSessionStore CreateStore(LocalStoreOptions? options = null) =>
        new(options ?? new LocalStoreOptions(), NullLogger.Instance, storePath: _root);

    private string SessionsDirectory => LocalStorePathResolver.GetSessionsDirectory(_root);

    private static TestExecution BuildExecution(
        string name,
        TestOutcome outcome = TestOutcome.Passed,
        string assembly = "MyApp.Tests")
    {
        TestIdentity identity = new TestIdentityBuilder()
            .WithTestFingerprint($"fingerprint-{name}")
            .WithFullyQualifiedName($"MyApp.Tests.SampleTests.{name}")
            .WithAssembly(assembly)
            .WithNamespace("MyApp.Tests")
            .WithClassName("SampleTests")
            .WithMethodName(name)
            .WithDisplayName(name)
            .WithSourceFile("SampleTests.cs")
            .WithSourceLineNumber(41)
            .Build();

        TestOrchestrationRecord orchestration = new TestOrchestrationBuilder()
            .WithThreadId("7")
            .WithWorkerId("collection-a")
            .WithCollectionName("collection-a")
            .WithParallelization(wasParallelized: true, concurrentTestCount: 4)
            .WithPositionInSuite(3)
            .WithGlobalPosition(9)
            .WithPreviousTest("fingerprint-Predecessor", "Predecessor", TestOutcome.Passed)
            .WithSuiteElapsedTime(TimeSpan.FromSeconds(12.5))
            .Build();

        RetryMetadata retry = new RetryMetadataBuilder()
            .WithAttemptNumber(2)
            .WithMaxRetries(3)
            .WithPassedOnRetry(true)
            .WithRetryAttributeName("RetryFact")
            .Build();

        return new TestExecutionBuilder()
            .WithExecutionId(Guid.NewGuid())
            .WithIdentity(identity)
            .WithTestName(name)
            .WithOutcome(outcome)
            .WithDuration(TimeSpan.FromMilliseconds(1234))
            .WithStartTime(new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc))
            .WithEndTime(new DateTime(2026, 8, 1, 9, 0, 1, DateTimeKind.Utc))
            .WithTestOrchestrationRecord(orchestration)
            .WithRetry(retry)
            .WithException(
                "System.InvalidOperationException",
                "Expected 3 but found 4",
                "   at MyApp.Tests.SampleTests.Failing()")
            .Build();
    }

    private static TestSession BuildSession(
        DateTime startedAt,
        string assembly = "MyApp.Tests",
        int testCount = 2,
        IDictionary<string, string>? customProperties = null)
    {
        EnvironmentInfo environment = new EnvironmentInfoBuilder()
            .WithMachineName("dev-box")
            .WithEnvironmentName("Local")
            .WithIsCIEnvironment(false)
            .AddCustomProperties(customProperties ?? new Dictionary<string, string>
            {
                ["Git.SHA"] = "a3f9c2e1d0b7a4f18e6c5d3b2a190f8e7d6c5b4a",
                ["Git.Branch"] = "main"
            })
            .Build();

        var executions = Enumerable
            .Range(0, testCount)
            .Select(i => BuildExecution($"Test{i}", assembly: assembly))
            .ToList();

        return new TestSessionBuilder()
            .WithSessionId(Guid.NewGuid())
            .WithStartedAt(startedAt)
            .WithEndedAt(startedAt.AddMinutes(1))
            .WithEnvironmentInfo(environment)
            .AddExecutions(executions)
            .WithSessionState(TestSessionState.Finalized)
            .Build();
    }

    private static TestSession WithId(TestSession session, string id) =>
        new TestSessionBuilder()
            .WithSessionId(Guid.Parse(id))
            .WithStartedAt(session.StartedAt)
            .WithEndedAt(session.EndedAt)
            .WithEnvironmentInfo(session.EnvironmentInfo)
            .AddExecutions(session.Executions)
            .WithSessionState(TestSessionState.Finalized)
            .Build();

    /// <summary>
    /// Builds one session recording several test assemblies, as a shared test host produces.
    /// </summary>
    private static TestSession BuildMixedSession(
        DateTime startedAt, params (string Assembly, int TestCount)[] assemblies)
    {
        var executions = new List<TestExecution>();

        foreach ((string assembly, int count) in assemblies)
        {
            for (int i = 0; i < count; i++)
                executions.Add(BuildExecution($"{assembly}Test{i}", assembly: assembly));
        }

        return new TestSessionBuilder()
            .WithSessionId(Guid.NewGuid())
            .WithStartedAt(startedAt)
            .WithEndedAt(startedAt.AddMinutes(1))
            .WithEnvironmentInfo(new EnvironmentInfoBuilder().WithMachineName("dev-box").Build())
            .AddExecutions(executions)
            .WithSessionState(TestSessionState.Finalized)
            .Build();
    }

    [Fact]
    public void ASessionLandsInTheRepositoryStoreWhenXpingLocalStoreIsSetAfterTheStoreIsBuilt()
    {
        // The SDK builds its store at Initialize but resolves the root on the first write, at the
        // end of the run. A test that points a tool at a fixture store in between must not take
        // the run's own session with it.
        string repo = Path.Combine(_root, "repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        string fixture = Path.Combine(_root, "fixture");
        var store = new JsonSessionStore(new LocalStoreOptions(), NullLogger.Instance, startDirectory: repo);

        System.Environment.SetEnvironmentVariable("XPING_LOCAL_STORE", fixture);
        try
        {
            Assert.True(store.Write(BuildSession(new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc))));
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("XPING_LOCAL_STORE", null);
        }

        Assert.Single(Directory.GetFiles(
            LocalStorePathResolver.GetSessionsDirectory(Path.Combine(repo, LocalStorePathResolver.StoreDirectoryName))));
        Assert.False(Directory.Exists(fixture));
    }

    [Fact]
    public void WriteThenReadPreservesEverythingAnalysisNeeds()
    {
        JsonSessionStore store = CreateStore();
        TestSession written = BuildSession(new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc));

        Assert.True(store.Write(written));

        LocalSessionReadResult result = store.ReadRecent(10);
        TestSession read = Assert.Single(result.Sessions);

        Assert.Equal(0, result.UnreadableCount);
        Assert.Equal(written.SessionId, read.SessionId);
        Assert.Equal(written.StartedAt, read.StartedAt);
        Assert.Equal(TestSessionState.Finalized, read.SessionState);
        Assert.Equal(written.Executions.Count, read.Executions.Count);

        TestExecution execution = read.Executions.First();

        // The whole point of this tier: the fields the slim run projection drops must survive.
        Assert.Equal("MyApp.Tests.SampleTests.Test0", execution.Identity.FullyQualifiedName);
        Assert.Equal("MyApp.Tests", execution.Identity.Assembly);
        Assert.Equal("SampleTests.cs", execution.Identity.SourceFile);
        Assert.Equal(41, execution.Identity.SourceLineNumber);
        Assert.Equal("System.InvalidOperationException", execution.ExceptionType);
        Assert.Equal("Expected 3 but found 4", execution.ErrorMessage);
        Assert.Equal("   at MyApp.Tests.SampleTests.Failing()", execution.StackTrace);
        Assert.Equal(TimeSpan.FromMilliseconds(1234), execution.Duration);
        Assert.Equal(new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc), execution.StartTimeUtc);
        Assert.True(execution.TestOrchestrationRecord.WasParallelized);
        Assert.Equal(4, execution.TestOrchestrationRecord.ConcurrentTestCount);
        Assert.Equal("fingerprint-Predecessor", execution.TestOrchestrationRecord.PreviousTestId);
        Assert.Equal(TimeSpan.FromSeconds(12.5), execution.TestOrchestrationRecord.SuiteElapsedTime);
        Assert.Equal(2, execution.Retry!.AttemptNumber);
        Assert.Equal(3, execution.Retry.MaxRetries);
        Assert.True(execution.Retry.PassedOnRetry);
        Assert.Equal("RetryFact", execution.Retry.RetryAttributeName);

        Assert.Equal(
            "a3f9c2e1d0b7a4f18e6c5d3b2a190f8e7d6c5b4a",
            read.EnvironmentInfo.CustomProperties["Git.SHA"]);
        Assert.Equal("main", read.EnvironmentInfo.CustomProperties["Git.Branch"]);
    }

    [Fact]
    public void ReadRecentReturnsNewestFirst()
    {
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 4; i++)
            store.Write(BuildSession(baseTime.AddMinutes(i)));

        LocalSessionReadResult result = store.ReadRecent(10);

        Assert.Equal(4, result.Sessions.Count);
        Assert.Equal(baseTime.AddMinutes(3), result.Sessions[0].StartedAt);
        Assert.Equal(baseTime, result.Sessions[3].StartedAt);
    }

    [Fact]
    public void ReadRecentHonoursTheAssemblyFilter()
    {
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        store.Write(BuildSession(baseTime, assembly: "Alpha.Tests"));
        store.Write(BuildSession(baseTime.AddMinutes(1), assembly: "Beta.Tests"));
        store.Write(BuildSession(baseTime.AddMinutes(2), assembly: "Alpha.Tests"));

        LocalSessionReadResult result = store.ReadRecent(10, "Alpha.Tests");

        Assert.Equal(2, result.Sessions.Count);
        Assert.All(
            result.Sessions,
            s => Assert.Equal("Alpha.Tests", s.Executions.First().Identity.Assembly));
    }

    [Fact]
    public void ReadRecentStopsAtTheRequestedCount()
    {
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 5; i++)
            store.Write(BuildSession(baseTime.AddMinutes(i)));

        Assert.Equal(2, store.ReadRecent(2).Sessions.Count);
        Assert.Empty(store.ReadRecent(0).Sessions);
    }

    [Fact]
    public void UnreadableFilesAreSkippedAndCounted()
    {
        JsonSessionStore store = CreateStore();
        store.Write(BuildSession(new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)));

        // Not gzip at all.
        File.WriteAllText(
            Path.Combine(SessionsDirectory, "session-0000000000000000001-aaaaaaaa.json.gz"),
            "this is not gzip");

        // Valid gzip holding a truncated JSON document. This is the failure a killed test host
        // produces, and the case a JSON Lines file would have accepted silently.
        string truncatedPath =
            Path.Combine(SessionsDirectory, "session-0000000000000000002-bbbbbbbb.json.gz");
        using (var file = new FileStream(truncatedPath, FileMode.Create))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            writer.Write("{\"sessionId\":\"" + Guid.NewGuid().ToString("D") + "\",\"executions\":[{");
        }

        // Empty file.
        File.WriteAllBytes(
            Path.Combine(SessionsDirectory, "session-0000000000000000003-cccccccc.json.gz"), []);

        LocalSessionReadResult result = store.ReadRecent(10);

        Assert.Single(result.Sessions);
        Assert.Equal(3, result.UnreadableCount);
    }

    [Fact]
    public void SessionsWithoutExecutionsAreSkippedButNotCountedAsUnreadable()
    {
        JsonSessionStore store = CreateStore();
        store.Write(BuildSession(new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)));

        TestSession empty = new TestSessionBuilder()
            .WithSessionId(Guid.NewGuid())
            .WithStartedAt(new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc))
            .WithSessionState(TestSessionState.Finalized)
            .Build();

        store.Write(empty);

        LocalSessionReadResult result = store.ReadRecent(10);

        Assert.Single(result.Sessions);
        Assert.Equal(0, result.UnreadableCount);
    }

    [Fact]
    public void RetentionPrunesOldestFirstAndNeverTheNewest()
    {
        JsonSessionStore store = CreateStore(new LocalStoreOptions { MaxRuns = 3 });
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 6; i++)
            store.Write(BuildSession(baseTime.AddMinutes(i)));

        LocalSessionReadResult result = store.ReadRecent(20);

        Assert.Equal(3, result.Sessions.Count);
        Assert.Equal(baseTime.AddMinutes(5), result.Sessions[0].StartedAt);
        Assert.Equal(baseTime.AddMinutes(3), result.Sessions[2].StartedAt);
    }

    [Fact]
    public void RetentionKeepsTheNewestEvenWhenItAloneExceedsTheSizeLimit()
    {
        JsonSessionStore store = CreateStore(new LocalStoreOptions { MaxBytes = 1 });

        store.Write(BuildSession(new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)));
        store.Write(BuildSession(new DateTime(2026, 8, 1, 9, 1, 0, DateTimeKind.Utc)));

        Assert.Single(store.ReadRecent(20).Sessions);
    }

    [Fact]
    public void RetentionCountsRunsPerAssembly()
    {
        // Three test projects sharing one store must each keep their own history: a store-wide count
        // would leave each with a third of it, and the report's default window could never fill.
        JsonSessionStore store = CreateStore();
        var baseTime = DateTime.UtcNow.AddHours(-6);
        string[] assemblies = ["Alpha.Tests", "Beta.Tests", "Gamma.Tests"];

        for (int i = 0; i < 40; i++)
        {
            foreach (string assembly in assemblies)
                store.Write(BuildSession(baseTime.AddMinutes(i), assembly: assembly));
        }

        Assert.Equal(120, store.ReadRecent(int.MaxValue).Sessions.Count);
        foreach (string assembly in assemblies)
            Assert.Equal(ReportWindow.DefaultRuns, store.ReadRecent(ReportWindow.DefaultRuns, assembly).Sessions.Count);
    }

    [Fact]
    public void RetentionPrunesTheBusiestAssemblyWithoutTouchingTheOthers()
    {
        JsonSessionStore store = CreateStore(new LocalStoreOptions { MaxRuns = 3 });
        var baseTime = DateTime.UtcNow.AddHours(-6);

        store.Write(BuildSession(baseTime, assembly: "Beta.Tests"));
        store.Write(BuildSession(baseTime.AddMinutes(1), assembly: "Beta.Tests"));

        for (int i = 0; i < 6; i++)
            store.Write(BuildSession(baseTime.AddMinutes(2 + i), assembly: "Alpha.Tests"));

        IReadOnlyList<TestSession> alpha = store.ReadRecent(20, "Alpha.Tests").Sessions;
        Assert.Equal(3, alpha.Count);
        Assert.Equal(baseTime.AddMinutes(5), alpha[2].StartedAt);
        Assert.Equal(2, store.ReadRecent(20, "Beta.Tests").Sessions.Count);
    }

    [Fact]
    public void RetentionKeepsAMixedRunWhileAnyOfItsAssembliesStillNeedsIt()
    {
        JsonSessionStore store = CreateStore(new LocalStoreOptions { MaxRuns = 2 });
        var baseTime = DateTime.UtcNow.AddHours(-6);

        store.Write(BuildMixedSession(baseTime, ("Alpha.Tests", 1), ("Beta.Tests", 1)));

        // Alpha moves past the mixed run; Beta has run nothing since, so it is still Beta's history.
        for (int i = 1; i <= 4; i++)
            store.Write(BuildSession(baseTime.AddMinutes(i), assembly: "Alpha.Tests"));

        Assert.Single(store.ReadRecent(20, "Beta.Tests").Sessions);
        Assert.Equal(3, store.ReadRecent(20, "Alpha.Tests").Sessions.Count);

        // Once Beta has two newer runs of its own, nothing needs the mixed run any more.
        store.Write(BuildSession(baseTime.AddMinutes(5), assembly: "Beta.Tests"));
        store.Write(BuildSession(baseTime.AddMinutes(6), assembly: "Beta.Tests"));

        Assert.Equal(2, store.ReadRecent(20, "Beta.Tests").Sessions.Count);
        Assert.Equal(2, store.ReadRecent(20, "Alpha.Tests").Sessions.Count);
    }

    [Fact]
    public void RetentionDeletesRunsOlderThanTheAgeLimit()
    {
        JsonSessionStore store = CreateStore(new LocalStoreOptions { MaxAge = TimeSpan.FromDays(1) });

        store.Write(BuildSession(DateTime.UtcNow.AddHours(-2)));
        string first = Assert.Single(Directory.GetFiles(SessionsDirectory));
        File.SetLastWriteTimeUtc(first, DateTime.UtcNow.AddDays(-2));

        store.Write(BuildSession(DateTime.UtcNow.AddHours(-1)));

        Assert.Single(store.ReadRecent(20).Sessions);
        Assert.False(File.Exists(first));
    }

    [Fact]
    public void RetentionBySizeSparesEveryAssemblysReportWindowFirst()
    {
        // Beta's runs are the oldest in the store. Oldest-first size pruning would delete them all
        // for Alpha's sake; Alpha's runs beyond its own window have to go first.
        var baseTime = DateTime.UtcNow.AddHours(-6);
        JsonSessionStore unbounded = CreateStore();

        for (int i = 0; i < ReportWindow.DefaultRuns; i++)
            unbounded.Write(BuildSession(baseTime.AddMinutes(i), assembly: "Beta.Tests"));

        for (int i = 0; i < 30; i++)
            unbounded.Write(BuildSession(baseTime.AddMinutes(100 + i), assembly: "Alpha.Tests"));

        FileInfo[] files = new DirectoryInfo(SessionsDirectory).GetFiles();
        long total = files.Sum(f => f.Length);
        long largest = files.Max(f => f.Length);

        JsonSessionStore bounded = CreateStore(new LocalStoreOptions { MaxBytes = total - (5 * largest) });
        bounded.Write(BuildSession(baseTime.AddMinutes(200), assembly: "Alpha.Tests"));

        Assert.Equal(ReportWindow.DefaultRuns, bounded.ReadRecent(100, "Beta.Tests").Sessions.Count);

        int alpha = bounded.ReadRecent(100, "Alpha.Tests").Sessions.Count;
        Assert.InRange(alpha, ReportWindow.DefaultRuns, 25);
    }

    [Fact]
    public void RetentionCountsAnUntaggedWideRunUnderEveryAssemblyItCovers()
    {
        // Too many assemblies to tag in its name, so retention reads them from the file's head.
        JsonSessionStore store = CreateStore(new LocalStoreOptions { MaxRuns = 1 });
        var baseTime = DateTime.UtcNow.AddHours(-6);
        var wide = Enumerable.Range(0, 9).Select(i => ($"Wide{i}.Tests", 1)).ToArray();

        store.Write(BuildMixedSession(baseTime, wide));
        Assert.Null(SessionFileName.ParseTags(Path.GetFileName(Directory.GetFiles(SessionsDirectory).Single())));

        // Wide0 moves on; the other eight still need the wide run.
        store.Write(BuildSession(baseTime.AddMinutes(1), assembly: "Wide0.Tests"));
        Assert.Single(store.ReadRecent(10, "Wide8.Tests").Sessions);

        // A second wide run replaces the first for all nine.
        store.Write(BuildMixedSession(baseTime.AddMinutes(2), wide));
        Assert.Equal(baseTime.AddMinutes(2), Assert.Single(store.ReadRecent(10, "Wide8.Tests").Sessions).StartedAt);
    }

    [Fact]
    public void ScopedReadSkipsAnotherAssemblysFileWithoutOpeningIt()
    {
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        store.Write(BuildSession(baseTime, assembly: "Alpha.Tests"));

        // Damaged, but named as Beta's: an Alpha read never opens it, so it is not reported.
        File.WriteAllBytes(
            Path.Combine(SessionsDirectory, SessionFileName.Build(baseTime.AddMinutes(1), Guid.NewGuid(), ["Beta.Tests"])),
            [0x1f, 0x8b, 0x08, 0x00]);

        LocalSessionReadResult alpha = store.ReadRecent(10, "Alpha.Tests");
        Assert.Single(alpha.Sessions);
        Assert.Equal(0, alpha.UnreadableCount);

        Assert.Equal(1, store.ReadRecent(10, "Beta.Tests").UnreadableCount);
    }

    [Fact]
    public void WriteNamesTheFileAfterTheAssembliesItsExecutionsCover()
    {
        // BuildMixedSession leaves TestSession.Assemblies empty: the store derives it.
        JsonSessionStore store = CreateStore();

        store.Write(BuildMixedSession(
            new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc), ("Beta.Tests", 1), ("Alpha.Tests", 1)));

        string file = Directory.GetFiles(SessionsDirectory).Single();
        Assert.Equal(
            [SessionFileName.Tag("Alpha.Tests"), SessionFileName.Tag("Beta.Tests")],
            SessionFileName.ParseTags(Path.GetFileName(file)));
        Assert.Equal(["Alpha.Tests", "Beta.Tests"], SessionFileHeader.ReadAssemblies(file));
    }

    [Fact]
    public void SummarizeCountsEveryRunByAssemblyAndReportsTheUnreadable()
    {
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        store.Write(BuildSession(baseTime, assembly: "Beta.Tests"));
        store.Write(BuildMixedSession(baseTime.AddMinutes(1), ("Alpha.Tests", 1), ("Beta.Tests", 1)));
        store.Write(BuildSession(baseTime.AddMinutes(2), assembly: "Alpha.Tests"));
        File.WriteAllBytes(
            Path.Combine(SessionsDirectory, "session-0000000000000000009-deadbeef.json.gz"), [0x00]);

        LocalStoreSummary summary = store.Summarize();

        Assert.Equal(["Alpha.Tests", "Beta.Tests"], summary.Assemblies.Select(a => a.Name));
        Assert.Equal(2, summary.Assemblies[0].Runs);
        Assert.Equal(baseTime.AddMinutes(2), summary.Assemblies[0].LastStartedAt);
        Assert.Equal(2, summary.Assemblies[1].Runs);
        Assert.Equal(baseTime.AddMinutes(1), summary.Assemblies[1].LastStartedAt);
        Assert.Equal(1, summary.UnattributedRuns);
    }

    [Fact]
    public void SummarizeIsEmptyBeforeAnyRunIsWritten()
    {
        LocalStoreSummary summary = CreateStore().Summarize();

        Assert.Empty(summary.Assemblies);
        Assert.Equal(0, summary.UnattributedRuns);
    }

    [Fact]
    public void SummarizeCountsAFileWhoseNameCarriesNoStartTimeAsUnattributed()
    {
        JsonSessionStore store = CreateStore();
        store.Write(BuildSession(new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)));

        string file = Directory.GetFiles(SessionsDirectory).Single();
        File.Move(file, Path.Combine(SessionsDirectory, "session-notaticks.json.gz"));

        LocalStoreSummary summary = store.Summarize();

        Assert.Empty(summary.Assemblies);
        Assert.Equal(1, summary.UnattributedRuns);
    }

    [Fact]
    public void SummarizeDegradesToEmptyWhenTheDirectoryCannotBeListed()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return; // chmod semantics differ; the behaviour is verified on Unix CI.

        JsonSessionStore store = CreateStore();
        store.Write(BuildSession(new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)));

        File.SetUnixFileMode(SessionsDirectory, UnixFileMode.None);

        try
        {
            Assert.Empty(store.Summarize().Assemblies);
        }
        finally
        {
            File.SetUnixFileMode(
                SessionsDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void ReadHeadersReturnsNewestFirstWithTheAssembliesEachRunCovers()
    {
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        store.Write(BuildSession(baseTime, assembly: "Beta.Tests"));
        store.Write(BuildMixedSession(baseTime.AddMinutes(1), ("Beta.Tests", 1), ("Alpha.Tests", 1)));
        store.Write(BuildSession(baseTime.AddMinutes(2), assembly: "Alpha.Tests"));

        List<LocalSessionHeader> headers = [.. store.ReadHeaders()];

        Assert.Equal(
            [baseTime.AddMinutes(2), baseTime.AddMinutes(1), baseTime],
            headers.Select(h => h.StartedAt));
        Assert.Equal(["Alpha.Tests"], headers[0].Assemblies);
        Assert.Equal(["Alpha.Tests", "Beta.Tests"], headers[1].Assemblies);
        Assert.Equal(["Beta.Tests"], headers[2].Assemblies);
    }

    [Fact]
    public void ReadHeadersBreaksSameTickTiesAsReadRecentDoes()
    {
        // Two suites starting in one tick is routine in a solution-wide run. File names sort the ids
        // the other way round, so walking them in reverse would name a different "newest" suite.
        JsonSessionStore store = CreateStore();
        var startedAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        store.Write(WithId(BuildSession(startedAt, assembly: "Alpha.Tests"), "22222222-0000-0000-0000-000000000000"));
        store.Write(WithId(BuildSession(startedAt, assembly: "Beta.Tests"), "11111111-0000-0000-0000-000000000000"));

        Assert.Equal(
            store.ReadRecent(10).Sessions.Select(s => s.Executions.First().Identity.Assembly),
            store.ReadHeaders().Select(h => h.Assemblies.Single()));
        Assert.Equal("Beta.Tests", store.ReadHeaders().First().Assemblies.Single());
    }

    [Fact]
    public void ReadHeadersReportsNoAssembliesForAnUnreadableHead()
    {
        JsonSessionStore store = CreateStore();
        store.Write(BuildSession(new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)));
        File.WriteAllBytes(
            Path.Combine(SessionsDirectory, "session-0000000000000000009-deadbeef.json.gz"), [0x00]);

        List<LocalSessionHeader> headers = [.. store.ReadHeaders()];

        Assert.Equal(2, headers.Count);
        Assert.Empty(headers[1].Assemblies);
    }

    [Fact]
    public void ReadEachReturnsEveryRunInReadRecentsOrderWithAnEntryPerUnreadableFile()
    {
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 4; i++)
            store.Write(BuildSession(baseTime.AddMinutes(i % 2), assembly: i < 2 ? "Alpha.Tests" : "Beta.Tests"));

        File.WriteAllBytes(
            Path.Combine(SessionsDirectory, "session-0000000000000000009-deadbeef.json.gz"), [0x00]);

        List<LocalSessionFile> files = [.. store.ReadEach()];

        Assert.Equal(
            store.ReadRecent(10).Sessions.Select(s => s.SessionId),
            files.Where(f => f.Session != null).Select(f => f.Session!.SessionId));
        Assert.Null(files[^1].Session);
    }

    [Fact]
    public void ReadEachNarrowsToAnAssemblyAsReadRecentDoes()
    {
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        store.Write(BuildSession(baseTime, assembly: "Beta.Tests"));
        store.Write(BuildMixedSession(baseTime.AddMinutes(1), ("Alpha.Tests", 1), ("Beta.Tests", 2)));
        store.Write(BuildSession(baseTime.AddMinutes(2), assembly: "Alpha.Tests"));

        List<TestSession> each = [.. store.ReadEach("Beta.Tests").Select(f => f.Session!)];
        IReadOnlyList<TestSession> recent = store.ReadRecent(10, "Beta.Tests").Sessions;

        Assert.Equal(recent.Select(s => s.SessionId), each.Select(s => s.SessionId));
        Assert.Equal(recent.Select(s => s.Executions.Count), each.Select(s => s.Executions.Count));
        Assert.All(each.SelectMany(s => s.Executions), e => Assert.Equal("Beta.Tests", e.Identity.Assembly));
    }

    [Fact]
    public void ReadEachIsEmptyBeforeAnyRunIsWritten() =>
        Assert.Empty(CreateStore().ReadEach());

    [Fact]
    public void ReadHeadersIsEmptyBeforeAnyRunIsWritten() =>
        Assert.Empty(CreateStore().ReadHeaders());

    [Fact]
    public void ReadHeadersDegradesToEmptyWhenTheDirectoryCannotBeListed()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return; // chmod semantics differ; the behaviour is verified on Unix CI.

        JsonSessionStore store = CreateStore();
        store.Write(BuildSession(new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)));

        File.SetUnixFileMode(SessionsDirectory, UnixFileMode.None);

        try
        {
            Assert.Empty(store.ReadHeaders());
        }
        finally
        {
            File.SetUnixFileMode(
                SessionsDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void RetentionOnAnEmptyDirectoryDoesNothing()
    {
        Directory.CreateDirectory(SessionsDirectory);

        StoreRetention.Apply(SessionsDirectory, new LocalStoreOptions(), _ => [], NullLogger.Instance);

        Assert.Empty(Directory.GetFiles(SessionsDirectory));
    }

    [Fact]
    public void DeleteScopedToAnAssemblyRenamesAMixedRunToTheAssembliesLeft()
    {
        JsonSessionStore store = CreateStore();

        store.Write(BuildMixedSession(
            new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc), ("Alpha.Tests", 1), ("Beta.Tests", 1)));

        Assert.Equal(1, store.Delete("Alpha.Tests"));

        string file = Directory.GetFiles(SessionsDirectory).Single();
        Assert.Equal([SessionFileName.Tag("Beta.Tests")], SessionFileName.ParseTags(Path.GetFileName(file)));
        Assert.Single(store.ReadRecent(10, "Beta.Tests").Sessions);
        Assert.Empty(store.ReadRecent(10, "Alpha.Tests").Sessions);
    }

    [Fact]
    public void DeleteScopedToAnAssemblyLeavesOtherAssembliesAlone()
    {
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        store.Write(BuildSession(baseTime, assembly: "Alpha.Tests"));
        store.Write(BuildSession(baseTime.AddMinutes(1), assembly: "Beta.Tests"));

        Assert.Equal(1, store.Delete("Alpha.Tests"));

        TestSession remaining = Assert.Single(store.ReadRecent(10).Sessions);
        Assert.Equal("Beta.Tests", remaining.Executions.First().Identity.Assembly);
    }

    [Fact]
    public void ARunCoveringSeveralAssembliesIsARunOfEachOfThem()
    {
        // `dotnet test` over a solution puts several test projects in one host, and the host writes
        // one session. Each project's history has to contain that run, or a report on one of them
        // silently loses every solution-wide run while another inherits its tests.
        JsonSessionStore store = CreateStore();

        store.Write(BuildMixedSession(
            new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
            ("Alpha.Tests", 2),
            ("Beta.Tests", 3)));

        Assert.Single(store.ReadRecent(10, "Alpha.Tests").Sessions);
        Assert.Single(store.ReadRecent(10, "Beta.Tests").Sessions);
    }

    [Fact]
    public void AScopedReadNarrowsAMixedRunToTheRequestedAssembly()
    {
        JsonSessionStore store = CreateStore();

        store.Write(BuildMixedSession(
            new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
            ("Alpha.Tests", 2),
            ("Beta.Tests", 3)));

        TestSession alpha = Assert.Single(store.ReadRecent(10, "Alpha.Tests").Sessions);
        TestSession beta = Assert.Single(store.ReadRecent(10, "Beta.Tests").Sessions);

        Assert.Equal(2, alpha.Executions.Count);
        Assert.Equal(3, beta.Executions.Count);
        Assert.All(alpha.Executions, e => Assert.Equal("Alpha.Tests", e.Identity.Assembly));
        Assert.All(beta.Executions, e => Assert.Equal("Beta.Tests", e.Identity.Assembly));
    }

    [Fact]
    public void AMixedRunCostsOneSlotOfTheRequestedMaximum()
    {
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 4; i++)
        {
            store.Write(BuildMixedSession(
                baseTime.AddMinutes(i), ("Alpha.Tests", 1), ("Beta.Tests", 1)));
        }

        Assert.Equal(2, store.ReadRecent(2, "Alpha.Tests").Sessions.Count);
    }

    [Fact]
    public void ScopedReadingIsUnaffectedByWhichAssemblyRanFirst()
    {
        // The rule this replaces took the first execution that named an assembly, so a run was
        // reachable under one of its assemblies and invisible under the rest.
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        store.Write(BuildMixedSession(baseTime, ("Alpha.Tests", 1), ("Beta.Tests", 1)));
        store.Write(BuildMixedSession(baseTime.AddMinutes(1), ("Beta.Tests", 1), ("Alpha.Tests", 1)));

        Assert.Equal(2, store.ReadRecent(10, "Alpha.Tests").Sessions.Count);
        Assert.Equal(2, store.ReadRecent(10, "Beta.Tests").Sessions.Count);
    }

    [Fact]
    public void DeleteScopedToAnAssemblyFindsRunsWhereItWasNotNamedFirst()
    {
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        store.Write(BuildMixedSession(baseTime, ("Alpha.Tests", 1), ("Beta.Tests", 1)));

        Assert.Equal(1, store.Delete("Beta.Tests"));
        Assert.Empty(store.ReadRecent(10, "Beta.Tests").Sessions);
    }

    [Fact]
    public void DeleteScopedToAnAssemblyKeepsTheRestOfASharedRun()
    {
        // The run held two suites. Clearing one of them must not clear the other, or `xping clear`
        // would destroy history its caller never named — the failure this scoping exists to prevent.
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        store.Write(BuildMixedSession(baseTime, ("Alpha.Tests", 2), ("Beta.Tests", 3)));

        Assert.Equal(1, store.Delete("Alpha.Tests"));

        TestSession remaining = Assert.Single(store.ReadRecent(10).Sessions);

        Assert.Equal(3, remaining.Executions.Count);
        Assert.All(remaining.Executions, e => Assert.Equal("Beta.Tests", e.Identity.Assembly));
    }

    [Fact]
    public void AStrippedRunKeepsItsIdentityAndStaysReadable()
    {
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        TestSession written = BuildMixedSession(baseTime, ("Alpha.Tests", 1), ("Beta.Tests", 1));
        store.Write(written);
        store.Delete("Alpha.Tests");

        TestSession remaining = Assert.Single(store.ReadRecent(10).Sessions);

        // Same run, rewritten in place: the filename encodes the ticks and session id, so a rewrite
        // that changed either would leave the old file behind beside the new one.
        Assert.Equal(written.SessionId, remaining.SessionId);
        Assert.Equal(written.StartedAt, remaining.StartedAt);
        Assert.Single(Directory.GetFiles(SessionsDirectory, "session-*.json.gz"));
        Assert.Empty(Directory.GetFiles(SessionsDirectory, "*.tmp"));
    }

    [Fact]
    public void ARewriteNeverLeavesTheStoreWithoutTheRunItIsReplacing()
    {
        // The destination is replaced in one step. Deleting it first and moving after would leave a
        // window with neither file present, and a process killed inside it loses the run — which on
        // a scoped clear means losing the suites the caller asked to keep.
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        store.Write(BuildMixedSession(baseTime, ("Alpha.Tests", 1), ("Beta.Tests", 1)));

        string path = Assert.Single(Directory.GetFiles(SessionsDirectory, "session-*.json.gz"));
        DateTime before = File.GetLastWriteTimeUtc(path);

        store.Delete("Alpha.Tests");

        // Still one file, actually rewritten rather than left alone, and renamed in place to the
        // assemblies it still covers — a rename, never a delete-then-write.
        string renamed = Assert.Single(Directory.GetFiles(SessionsDirectory, "session-*.json.gz"));
        Assert.Equal([SessionFileName.Tag("Beta.Tests")], SessionFileName.ParseTags(Path.GetFileName(renamed)));
        Assert.True(SessionFileName.TryParseStartedAt(Path.GetFileName(renamed), out DateTime startedAt));
        Assert.Equal(baseTime, startedAt);
        Assert.True(File.GetLastWriteTimeUtc(renamed) >= before);
        Assert.Single(store.ReadRecent(10, "Beta.Tests").Sessions);
    }

    [Fact]
    public void AStaleTempFileFromAnEarlierCrashDoesNotBlockAWriteOrCountAsARun()
    {
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        store.Write(BuildMixedSession(baseTime, ("Alpha.Tests", 1), ("Beta.Tests", 1)));

        string path = Assert.Single(Directory.GetFiles(SessionsDirectory, "session-*.json.gz"));
        File.WriteAllText(path + ".tmp", "leftover from a killed process");

        Assert.Equal(1, store.Delete("Alpha.Tests"));

        // The stale temp is overwritten by the rewrite, and never read as a session either way: the
        // store's search pattern ends at .json.gz, so a .tmp is invisible to every read.
        TestSession remaining = Assert.Single(store.ReadRecent(10).Sessions);
        Assert.Equal("Beta.Tests", remaining.Executions.First().Identity.Assembly);
    }

    [Fact]
    public void DeleteRemovesARunThatRecordedTheScopedAssemblyAndNothingElse()
    {
        JsonSessionStore store = CreateStore();
        var baseTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        store.Write(BuildSession(baseTime, assembly: "Alpha.Tests"));

        Assert.Equal(1, store.Delete("Alpha.Tests"));
        Assert.Empty(Directory.GetFiles(SessionsDirectory, "session-*.json.gz"));
    }

    [Fact]
    public void ARunThatNamedNoAssemblyIsUnreachableByAnyScope()
    {
        // It cannot be attributed, so it cannot be scoped to. The store itself does not refuse it -
        // the orchestrator is what stops writing one - so a file from before that guard still reads
        // back, and `xping where` still accounts for it, but no report can claim it belongs to a suite.
        JsonSessionStore store = CreateStore();

        store.Write(BuildSession(
            new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc), assembly: string.Empty));

        Assert.Single(store.ReadRecent(10).Sessions);
        Assert.Empty(store.ReadRecent(10, "Alpha.Tests").Sessions);
    }

    [Fact]
    public void ReadingBeforeAnythingIsWrittenYieldsAnEmptyResult()
    {
        // The sessions directory does not exist yet. A brand-new project hits this on its very
        // first `xping report`, so it has to read as "no history", never as a failure.
        JsonSessionStore store = CreateStore();
        LocalSessionReadResult result = store.ReadRecent(10);

        Assert.True(store.IsAvailable);
        Assert.Empty(result.Sessions);
        Assert.Equal(0, result.UnreadableCount);
    }
}
