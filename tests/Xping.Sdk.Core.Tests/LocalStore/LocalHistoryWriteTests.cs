/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;
using Xping.Sdk.Core.Services.Upload;

namespace Xping.Sdk.Core.Tests.LocalStore;

/// <summary>
/// Guards what a finished run leaves in the local store, and where.
/// </summary>
/// <remarks>
/// Runs the real orchestrator against a real store in a scratch directory rather than a mock:
/// the questions here — was a file written, and into which directory — are exactly the ones a
/// mock would answer by construction.
/// </remarks>
// Mutates the XPING_LOCAL_STORE environment variable, which is process-wide state.
[Collection("Sequential")]
public sealed class LocalHistoryWriteTests : IDisposable
{
    private sealed class Harness(IHost host) : XpingContextOrchestrator(host)
    {
        public void Record(TestExecution execution) => RecordTestExecution(execution);

        public Task<UploadResult> FinalizeAsync() => FinalizeSessionAsync(CancellationToken.None);
    }

    private readonly string _temp;

    public LocalHistoryWriteTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "xping-history-tests", Guid.NewGuid().ToString("N"));
        System.Environment.SetEnvironmentVariable(LocalStorePathResolver.EnvironmentVariableName, null);
    }

    public void Dispose()
    {
        System.Environment.SetEnvironmentVariable(LocalStorePathResolver.EnvironmentVariableName, null);

        try
        {
            if (Directory.Exists(_temp))
                Directory.Delete(_temp, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public async Task ARunWhoseExecutionsNameTheirAssemblyIsWrittenToTheConfiguredStore()
    {
        string store = Path.Combine(_temp, "configured");

        await using (Harness orchestrator = Create(store))
        {
            orchestrator.Record(Execution("MyApp.Tests", "Passes"));
            await orchestrator.FinalizeAsync();
        }

        Assert.Single(SessionFiles(store));
    }

    [Fact]
    public async Task ARunThatRecordedNothingIsNotWritten()
    {
        // A session that was initialized and finalized without a single test in between has no
        // history to keep. Writing an empty run would only give the CLI a blank to explain.
        string store = Path.Combine(_temp, "configured");

        await using (Harness orchestrator = Create(store))
        {
            await orchestrator.FinalizeAsync();
        }

        Assert.Empty(SessionFiles(store));
    }

    [Fact]
    public async Task ARunInWhichNoExecutionNamesAnAssemblyIsNotWritten()
    {
        // The shape every CLI report scopes by is missing, so the run could only ever be counted,
        // never shown. It is also the shape a hand-built TestExecution produces, which is how the
        // SDK's own test suites used to fill the repository's store with runs of nothing.
        string store = Path.Combine(_temp, "configured");

        await using (Harness orchestrator = Create(store))
        {
            orchestrator.Record(Execution(assembly: string.Empty, "Orphan"));
            orchestrator.Record(Execution(assembly: string.Empty, "AnotherOrphan"));
            await orchestrator.FinalizeAsync();
        }

        Assert.Empty(SessionFiles(store));
    }

    [Fact]
    public async Task AnExecutionWithoutAnAssemblyStillRidesAlongWithThoseThatHaveOne()
    {
        // The guard is on the run, not on the execution: one attributable execution is enough to
        // make the run worth keeping, orphans included, so nothing that was recorded goes missing.
        string store = Path.Combine(_temp, "configured");

        await using (Harness orchestrator = Create(store))
        {
            orchestrator.Record(Execution(assembly: string.Empty, "Orphan"));
            orchestrator.Record(Execution("MyApp.Tests", "Named"));
            await orchestrator.FinalizeAsync();
        }

        Assert.Single(SessionFiles(store));
    }

    [Fact]
    public async Task TheEnvironmentVariableOutranksTheConfiguredPath()
    {
        // Same precedence as every other XPING_* variable: what the pipeline exports beats what
        // the code configured. It is also the only way the CLI, which cannot see the SDK's
        // configuration, can be pointed at the same place.
        string configured = Path.Combine(_temp, "configured");
        string exported = Path.Combine(_temp, "exported");
        System.Environment.SetEnvironmentVariable(LocalStorePathResolver.EnvironmentVariableName, exported);

        await using (Harness orchestrator = Create(configured))
        {
            orchestrator.Record(Execution("MyApp.Tests", "Passes"));
            await orchestrator.FinalizeAsync();
        }

        Assert.Single(SessionFiles(exported));
        Assert.Empty(SessionFiles(configured));
    }

    [Fact]
    public async Task TheRunIsOnDiskBeforeTheFirstUploadStarts()
    {
        // The order matters when finalization runs from a process-exit safety net: under `dotnet
        // test`, vstest terminates the test host 100 ms after the run, which is less than one
        // network round trip. A file write fits in that window; the uploads must come after it
        // (issue #126). Batch size 1 forces several batch uploads ahead of the finalized one, and
        // every one of them must already find the run on disk.
        string store = Path.Combine(_temp, "configured");
        var filesSeenByUploads = new List<int>();

        var uploader = new Mock<IXpingUploader>();
        uploader
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .Callback<TestSession, CancellationToken>((_, _) => filesSeenByUploads.Add(SessionFiles(store).Length))
            .ReturnsAsync(new UploadResult { Success = true, TotalRecordsCount = 1 });

        await using (Harness orchestrator = CreateUploading(store, uploader.Object))
        {
            orchestrator.Record(Execution("MyApp.Tests", "First"));
            orchestrator.Record(Execution("MyApp.Tests", "Second"));
            await orchestrator.FinalizeAsync();
        }

        Assert.Equal([1, 1, 1], filesSeenByUploads);
        Assert.Single(SessionFiles(store));
    }

    [Fact]
    public async Task TheBatchesAreUploadedWhole_AndInOrder()
    {
        // Building every batch before uploading any is only safe if the batches do not share
        // state: the first must still carry its executions once the last has been built.
        string store = Path.Combine(_temp, "configured");
        var uploaded = new List<TestSession>();

        var uploader = new Mock<IXpingUploader>();
        uploader
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .Callback<TestSession, CancellationToken>((session, _) => uploaded.Add(session))
            .ReturnsAsync(new UploadResult { Success = true, TotalRecordsCount = 1 });

        await using (Harness orchestrator = CreateUploading(store, uploader.Object))
        {
            orchestrator.Record(Execution("MyApp.Tests", "First"));
            orchestrator.Record(Execution("MyApp.Tests", "Second"));
            await orchestrator.FinalizeAsync();
        }

        Assert.Equal(3, uploaded.Count);
        Assert.Equal(["First"], uploaded[0].Executions.Select(e => e.TestName));
        Assert.Equal(["Second"], uploaded[1].Executions.Select(e => e.TestName));
        Assert.Equal(
            [TestSessionState.Initial, TestSessionState.Partial, TestSessionState.Finalized],
            uploaded.Select(s => s.SessionState));
        Assert.Empty(uploaded[2].Executions);
    }

    [Fact]
    public async Task AFailedBatchStopsTheRemainingBatches_ButNotTheFinalizedUpload()
    {
        // Retries are Polly's job. Once it has given up on one batch, the rest would only repeat
        // the wait; they are already on disk. The finalized session is still sent, because it is
        // what closes the run on the platform and what strict mode judges.
        string store = Path.Combine(_temp, "configured");
        var uploaded = new List<TestSession>();

        var uploader = new Mock<IXpingUploader>();
        uploader
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .Callback<TestSession, CancellationToken>((session, _) => uploaded.Add(session))
            .ReturnsAsync((TestSession session, CancellationToken _) =>
                session.SessionState == TestSessionState.Finalized
                    ? new UploadResult { Success = true }
                    : new UploadResult { Success = false, ErrorMessage = "Simulated failure" });

        await using (Harness orchestrator = CreateUploading(store, uploader.Object))
        {
            orchestrator.Record(Execution("MyApp.Tests", "First"));
            orchestrator.Record(Execution("MyApp.Tests", "Second"));
            orchestrator.Record(Execution("MyApp.Tests", "Third"));
            UploadResult result = await orchestrator.FinalizeAsync();

            Assert.True(result.Success);
        }

        Assert.Equal(
            [TestSessionState.Initial, TestSessionState.Finalized],
            uploaded.Select(s => s.SessionState));
        Assert.Single(SessionFiles(store));
    }

    [Fact]
    public async Task AStoreThatThrowsDoesNotStopTheUpload()
    {
        // The local write runs ahead of the uploads with the session already marked finalized.
        // Whatever it throws — not only the storage failures the store itself swallows — must be
        // contained, or the run would be lost to the cloud and reported as a success.
        var store = new Mock<ILocalSessionStore>();
        store
            .Setup(s => s.Write(It.IsAny<TestSession>()))
            .Throws(new InvalidOperationException("Serializer misconfigured"));

        var uploader = new Mock<IXpingUploader>();
        uploader
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadResult { Success = true, TotalRecordsCount = 1 });

        await using (Harness orchestrator = CreateUploading(Path.Combine(_temp, "unused"), uploader.Object, store.Object))
        {
            orchestrator.Record(Execution("MyApp.Tests", "Passes"));
            UploadResult result = await orchestrator.FinalizeAsync();

            Assert.True(result.Success);
        }

        uploader.Verify(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AStoreThatDeclinesTheWriteStillLetsTheUploadProceed()
    {
        // JsonSessionStore answers false rather than throwing when the disk is not writable. The
        // run is then simply not stored; nothing about the upload changes.
        var store = new Mock<ILocalSessionStore>();
        store.Setup(s => s.Write(It.IsAny<TestSession>())).Returns(false);

        var uploader = new Mock<IXpingUploader>();
        uploader
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadResult { Success = true, TotalRecordsCount = 1 });

        await using (Harness orchestrator = CreateUploading(Path.Combine(_temp, "unused"), uploader.Object, store.Object))
        {
            orchestrator.Record(Execution("MyApp.Tests", "Passes"));
            UploadResult result = await orchestrator.FinalizeAsync();

            Assert.True(result.Success);
        }

        store.Verify(s => s.Write(It.IsAny<TestSession>()), Times.Once);
        uploader.Verify(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static Harness CreateUploading(string storePath, IXpingUploader uploader, ILocalSessionStore? store = null)
    {
        // Composed by hand rather than through AddXping: a zero FlushInterval fails validation
        // there, and it is what keeps the timer and the buffer-full flush out of this test, so
        // that the only uploads are the ones finalization itself issues.
        IHost host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<XpingConfiguration>(o =>
                {
                    o.Mode = XpingMode.Cloud;
                    o.ApiKey = "test-key";
                    o.BatchSize = 1;
                    o.FlushInterval = TimeSpan.Zero;
                    o.LocalStorePath = storePath;
                });
                services
                    .AddXpingInfrastructure()
                    .AddXpingSerialization()
                    .AddXpingEnvironment()
                    .AddXpingCollectors()
                    .AddXpingPullRequest()
                    .AddXpingStatistics()
                    .AddXpingLocalStore(XpingMode.Cloud)
                    .AddSingleton(uploader);

                // Registered after AddXpingLocalStore so it is the ILocalSessionStore the
                // orchestrator resolves.
                if (store != null)
                    services.AddSingleton(store);
            })
            .Build();

        return new Harness(host);
    }

    private static Harness Create(string storePath)
    {
        // LocalOnly, explicitly: an ambient XPING_APIKEY must not turn this into an upload.
        var configuration = new XpingConfiguration
        {
            Mode = XpingMode.LocalOnly,
            LocalStorePath = storePath
        };

        IHost host = new HostBuilder()
            .ConfigureServices(services => services.AddXping(configuration))
            .Build();

        return new Harness(host);
    }

    private static TestExecution Execution(string assembly, string name) =>
        new TestExecutionBuilder()
            .WithTestName(name)
            .WithOutcome(TestOutcome.Passed)
            .WithIdentity(new TestIdentity { Assembly = assembly, FullyQualifiedName = $"{assembly}.{name}" })
            .Build();

    private static string[] SessionFiles(string store)
    {
        string sessions = LocalStorePathResolver.GetSessionsDirectory(store);

        return Directory.Exists(sessions)
            ? Directory.GetFiles(sessions, "session-*.json.gz")
            : [];
    }
}
