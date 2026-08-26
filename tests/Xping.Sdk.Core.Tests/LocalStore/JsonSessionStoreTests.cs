/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Sdk.Core.Tests.LocalStore;

// Mutates the XPING_LOCAL_STORE environment variable, which is process-wide state.
[Collection("Sequential")]
public sealed class JsonSessionStoreTests : IDisposable
{
    private readonly string _root;

    public JsonSessionStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-session-tests", Guid.NewGuid().ToString("N"));
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

    private static JsonSessionStore CreateStore(LocalStoreOptions? options = null) =>
        new(options ?? new LocalStoreOptions(), NullLogger.Instance);

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
        // It cannot be attributed, so it cannot be scoped to. It stays in the store — `xping where`
        // still accounts for it — but no report can claim it belongs to a suite.
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
