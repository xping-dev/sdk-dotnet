/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Hosting;
using Moq;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Upload;
using Xping.Sdk.Core.Tests.Helpers;

namespace Xping.Sdk.Core.Tests.Orchestration;

public sealed class XpingContextOrchestratorTests
{
    // ---------------------------------------------------------------------------
    // Minimal concrete subclass for testing
    // ---------------------------------------------------------------------------

    private sealed class TestOrchestrator : XpingContextOrchestrator
    {
        private int _sessionFinalizingCount;
        private int _sessionFinalizedCount;
        private UploadResult? _lastFinalizedResult;

        public int SessionFinalizingCount => _sessionFinalizingCount;
        public int SessionFinalizedCount => _sessionFinalizedCount;
        public UploadResult? LastFinalizedResult => _lastFinalizedResult;
        public List<TestExecution> OnRecordedExecutions { get; } = [];
        public bool HealthStatus => IsHealthy;

        public TestOrchestrator(IHost host) : base(host) { }

        public Task<UploadResult> FlushAsync(CancellationToken ct = default)
            => FlushSessionAsync(ct);

        public Task<UploadResult> FinalizeAsync(CancellationToken ct = default)
            => FinalizeSessionAsync(ct);

        public void RecordExecution(TestExecution e) => RecordTestExecution(e);

        protected override Task OnSessionFinalizingAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _sessionFinalizingCount);
            return base.OnSessionFinalizingAsync(cancellationToken);
        }

        protected override Task OnSessionFinalizedAsync(UploadResult result, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _sessionFinalizedCount);
            _lastFinalizedResult = result;
            return base.OnSessionFinalizedAsync(result, cancellationToken);
        }

        protected override void OnTestExecutionRecorded(TestExecution execution)
            => OnRecordedExecutions.Add(execution);
    }

    // ---------------------------------------------------------------------------
    // Minimal subclass for statistics tests — does NOT override OnTestExecutionRecorded
    // so the base implementation feeds _statisticsAccumulator.Record(execution).
    // ---------------------------------------------------------------------------

    private sealed class StatsOrchestrator : XpingContextOrchestrator
    {
        public StatsOrchestrator(IHost host) : base(host) { }

        public Action<UploadResult>? OnFinalizedResult { get; set; }

        public Task<UploadResult> FlushAsync(CancellationToken ct = default)
            => FlushSessionAsync(ct);

        public Task<UploadResult> FinalizeAsync(CancellationToken ct = default)
            => FinalizeSessionAsync(ct);

        public void RecordExecution(TestExecution e) => RecordTestExecution(e);

        protected override Task OnSessionFinalizedAsync(UploadResult result, CancellationToken cancellationToken)
        {
            OnFinalizedResult?.Invoke(result);
            return base.OnSessionFinalizedAsync(result, cancellationToken);
        }
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static (TestOrchestrator orchestrator, Mock<IXpingUploader> uploaderMock)
        CreateOrchestrator(Action<XpingConfiguration>? configure = null)
    {
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        ServiceHelper.SetupDefaultMocks(uploaderMock, envDetectorMock);

        var host = ServiceHelper.BuildOrchestratorHost(uploaderMock, envDetectorMock, configure);
        return (new TestOrchestrator(host), uploaderMock);
    }

    private static (StatsOrchestrator orchestrator, Mock<IXpingUploader> uploaderMock)
        CreateStatsOrchestrator(Action<XpingConfiguration>? configure = null)
    {
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        ServiceHelper.SetupDefaultMocks(uploaderMock, envDetectorMock);

        var host = ServiceHelper.BuildOrchestratorHost(uploaderMock, envDetectorMock, configure);
        return (new StatsOrchestrator(host), uploaderMock);
    }

    private static TestExecution BuildExecution(string name = "Test")
        => new TestExecutionBuilder().WithTestName(name).WithOutcome(TestOutcome.Passed).Build();

    // ---------------------------------------------------------------------------
    // SessionId / StartedAt
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task SessionId_ShouldBeUniquePerInstance()
    {
        // Arrange
        var (o1, _) = CreateOrchestrator();
        var (o2, _) = CreateOrchestrator();

        // Assert
        Assert.NotEqual(o1.SessionId, o2.SessionId);

        await o1.DisposeAsync();
        await o2.DisposeAsync();
    }

    [Fact]
    public async Task StartedAt_ShouldBeSetDuringConstruction()
    {
        // Arrange
        var before = DateTime.UtcNow.AddSeconds(-1);
        var (orchestrator, _) = CreateOrchestrator();
        var after = DateTime.UtcNow.AddSeconds(1);

        // Assert
        Assert.InRange(orchestrator.StartedAt, before, after);

        await orchestrator.DisposeAsync();
    }

    // ---------------------------------------------------------------------------
    // RecordTestExecution
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RecordTestExecution_ShouldCallOnTestExecutionRecorded()
    {
        // Arrange
        var (orchestrator, _) = CreateOrchestrator();
        var execution = BuildExecution("Hook_Test");

        // Act
        orchestrator.RecordExecution(execution);

        // Assert
        Assert.Single(orchestrator.OnRecordedExecutions);
        Assert.Equal(execution.ExecutionId, orchestrator.OnRecordedExecutions[0].ExecutionId);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task RecordTestExecution_ShouldThrowArgumentNullException_ForNull()
    {
        // Arrange
        var (orchestrator, _) = CreateOrchestrator();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => orchestrator.RecordExecution(null!));

        await orchestrator.DisposeAsync();
    }

    // ---------------------------------------------------------------------------
    // FlushSessionAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FlushSessionAsync_ShouldCallUploader_WithDrainedExecutions()
    {
        // Arrange
        var (orchestrator, uploaderMock) = CreateOrchestrator();
        orchestrator.RecordExecution(BuildExecution("Flush_Test"));

        // Act
        var result = await orchestrator.FlushAsync();

        // Assert
        uploaderMock.Verify(
            u => u.UploadAsync(It.Is<TestSession>(s => s.Executions.Count == 1), It.IsAny<CancellationToken>()),
            Times.Once);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FlushSessionAsync_ShouldReturnSuccessResult()
    {
        // Arrange
        var (orchestrator, _) = CreateOrchestrator();
        orchestrator.RecordExecution(BuildExecution());

        // Act
        var result = await orchestrator.FlushAsync();

        // Assert
        Assert.True(result.Success);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FlushSessionAsync_ShouldIncludeSessionId_InUploadedSession()
    {
        // Arrange
        TestSession? capturedSession = null;
        var uploaderMock = new Mock<IXpingUploader>();
        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .Callback<TestSession, CancellationToken>((s, _) => capturedSession = s)
            .ReturnsAsync(new UploadResult { Success = true, TotalRecordsCount = 0 });

        var envDetectorMock = new Mock<IEnvironmentDetector>();
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());

        var host = ServiceHelper.BuildOrchestratorHost(uploaderMock, envDetectorMock);
        var orchestrator = new TestOrchestrator(host);

        // Act
        await orchestrator.FlushAsync();

        // Assert
        Assert.NotNull(capturedSession);
        Assert.Equal(orchestrator.SessionId, capturedSession.SessionId);

        await orchestrator.DisposeAsync();
    }

    // ---------------------------------------------------------------------------
    // FinalizeSessionAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FinalizeSessionAsync_ShouldCallOnSessionFinalizingAndFinalized()
    {
        // Arrange
        var (orchestrator, _) = CreateOrchestrator();
        orchestrator.RecordExecution(BuildExecution());

        // Act
        await orchestrator.FinalizeAsync();

        // Assert
        Assert.Equal(1, orchestrator.SessionFinalizingCount);
        Assert.Equal(1, orchestrator.SessionFinalizedCount);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FinalizeSessionAsync_ShouldBeIdempotent_CalledTwice()
    {
        // Arrange
        var (orchestrator, uploaderMock) = CreateOrchestrator();
        orchestrator.RecordExecution(BuildExecution());

        // Act — call twice
        await orchestrator.FinalizeAsync();
        await orchestrator.FinalizeAsync();

        // Assert — hooks fire only once
        Assert.Equal(1, orchestrator.SessionFinalizingCount);
        Assert.Equal(1, orchestrator.SessionFinalizedCount);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FinalizeSessionAsync_ShouldDrainAllExecutions()
    {
        // Arrange
        // Note: TestSessionBuilder.Build() uses _executions.AsReadOnly() (a view, not a copy).
        // Moq captures the TestSession reference, and after the builder's Reset() clears the
        // backing list the captured session appears empty. We capture the counts at invocation
        // time via Callback to work around this.
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        var capturedCounts = new List<int>();

        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());

        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .Callback<TestSession, CancellationToken>((s, _) => capturedCounts.Add(s.Executions.Count))
            .ReturnsAsync((TestSession s, CancellationToken _) =>
                new UploadResult { Success = true, TotalRecordsCount = s.Executions.Count });

        var host = ServiceHelper.BuildOrchestratorHost(uploaderMock, envDetectorMock, o => o.BatchSize = 100);
        var orchestrator = new TestOrchestrator(host);

        for (int i = 0; i < 5; i++)
            orchestrator.RecordExecution(BuildExecution($"Test{i}"));

        // Act
        await orchestrator.FinalizeAsync();

        // Assert — at least one flush contained executions
        Assert.Contains(capturedCounts, count => count > 0);

        await orchestrator.DisposeAsync();
    }

    // ---------------------------------------------------------------------------
    // DisposeAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_ShouldCallFinalizeOnce()
    {
        // Arrange
        var (orchestrator, uploaderMock) = CreateOrchestrator();
        orchestrator.RecordExecution(BuildExecution());

        // Act
        await orchestrator.DisposeAsync();

        // Assert — uploader was called during finalization
        uploaderMock.Verify(
            u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task DisposeAsync_ShouldBeIdempotent()
    {
        // Arrange
        var (orchestrator, _) = CreateOrchestrator();

        // Act & Assert — second dispose must not throw
        await orchestrator.DisposeAsync();
        await orchestrator.DisposeAsync();
    }

    // ---------------------------------------------------------------------------
    // GetCollectorStatsAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetCollectorStatsAsync_ShouldReflectRecordedExecutions()
    {
        // Arrange
        var (orchestrator, _) = CreateOrchestrator();

        for (int i = 0; i < 3; i++)
            orchestrator.RecordExecution(BuildExecution($"Test{i}"));

        // Act
        var stats = await orchestrator.GetCollectorStatsAsync();

        // Assert
        Assert.Equal(3, stats.TotalRecorded);

        await orchestrator.DisposeAsync();
    }

    // ---------------------------------------------------------------------------
    // Silent failure (invalid configuration)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Constructor_WithInvalidConfig_DoesNotThrow()
    {
        // Arrange
        var host = ServiceHelper.BuildInvalidConfigHost();

        // Act — if the constructor throws, the test fails; reaching the assertion means success
        var orchestrator = new TestOrchestrator(host);
        Assert.NotNull(orchestrator);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task Constructor_WithInvalidConfig_SetsIsHealthyFalse()
    {
        // Arrange
        var host = ServiceHelper.BuildInvalidConfigHost();
        var orchestrator = new TestOrchestrator(host);

        // Assert
        Assert.False(orchestrator.HealthStatus);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FlushSessionAsync_WhenUnhealthy_ReturnsSuccessWithZeroRecords()
    {
        // Arrange
        var host = ServiceHelper.BuildInvalidConfigHost();
        var orchestrator = new TestOrchestrator(host);
        orchestrator.RecordExecution(BuildExecution("SilentTest"));

        // Act
        var result = await orchestrator.FlushAsync();

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.TotalRecordsCount);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task RecordTestExecution_WhenUnhealthy_DoesNotBufferExecution()
    {
        // Arrange
        var host = ServiceHelper.BuildInvalidConfigHost();
        var orchestrator = new TestOrchestrator(host);

        // Act
        orchestrator.RecordExecution(BuildExecution("SilentTest"));

        // Assert — collector is no-op; stats show nothing recorded
        var stats = await orchestrator.GetCollectorStatsAsync();
        Assert.Equal(0, stats.TotalRecorded);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task Constructor_WithEnabledFalse_SetsIsHealthyFalse()
    {
        // Arrange
        var (orchestrator, _) = CreateOrchestrator(o => o.Enabled = false);

        // Assert
        Assert.False(orchestrator.HealthStatus);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenUnhealthy_DoesNotThrow()
    {
        // Arrange
        var host = ServiceHelper.BuildInvalidConfigHost();
        var orchestrator = new TestOrchestrator(host);

        // Act — if DisposeAsync throws, the test fails automatically
        await orchestrator.DisposeAsync();
    }

    // ---------------------------------------------------------------------------
    // Strict mode (invalid configuration) — IConfiguration path only.
    // Tests that set/clear the XPING_STRICTMODE environment variable have been
    // moved to XpingContextOrchestratorEnvVarTests which runs in the "Sequential"
    // xUnit collection to avoid races with other test classes.
    // ---------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithInvalidConfig_AndStrictModeViaIConfiguration_ThrowsXpingConfigurationException()
    {
        // Arrange — Xping:StrictMode is injected into IConfiguration via in-memory collection;
        // no env var is used, verifying the IConfiguration code path independently.
        var host = ServiceHelper.BuildInvalidConfigHostWithStrictMode();

        // Act & Assert
        Assert.Throws<XpingConfigurationException>(() => new TestOrchestrator(host));
    }

    // ---------------------------------------------------------------------------
    // TestSessionState progression
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FlushSessionAsync_FirstFlush_ShouldHaveInitialState()
    {
        // Arrange
        TestSession? capturedSession = null;
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());
        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .Callback<TestSession, CancellationToken>((s, _) => capturedSession = s)
            .ReturnsAsync((TestSession s, CancellationToken _) =>
                new UploadResult { Success = true, TotalRecordsCount = s.Executions.Count });

        var host = ServiceHelper.BuildOrchestratorHost(uploaderMock, envDetectorMock);
        var orchestrator = new TestOrchestrator(host);
        orchestrator.RecordExecution(BuildExecution("StateTest"));

        // Act — first flush
        await orchestrator.FlushAsync();

        // Assert
        Assert.NotNull(capturedSession);
        Assert.Equal(TestSessionState.Initial, capturedSession.SessionState);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FlushSessionAsync_SecondFlush_ShouldHavePartialState()
    {
        // Arrange
        var capturedStates = new List<TestSessionState>();
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());
        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .Callback<TestSession, CancellationToken>((s, _) => capturedStates.Add(s.SessionState))
            .ReturnsAsync((TestSession s, CancellationToken _) =>
                new UploadResult { Success = true, TotalRecordsCount = s.Executions.Count });

        var host = ServiceHelper.BuildOrchestratorHost(uploaderMock, envDetectorMock);
        var orchestrator = new TestOrchestrator(host);

        // Act — two flushes with at least one execution each
        orchestrator.RecordExecution(BuildExecution("First"));
        await orchestrator.FlushAsync();

        orchestrator.RecordExecution(BuildExecution("Second"));
        await orchestrator.FlushAsync();

        // Assert — first flush is Initial, second flush is Partial
        Assert.Equal(2, capturedStates.Count);
        Assert.Equal(TestSessionState.Initial, capturedStates[0]);
        Assert.Equal(TestSessionState.Partial, capturedStates[1]);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FinalizeSessionAsync_ShouldSendFinalizedSession()
    {
        // Arrange
        var capturedStates = new List<TestSessionState>();
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());
        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .Callback<TestSession, CancellationToken>((s, _) => capturedStates.Add(s.SessionState))
            .ReturnsAsync((TestSession s, CancellationToken _) =>
                new UploadResult { Success = true, TotalRecordsCount = s.Executions.Count });

        var host = ServiceHelper.BuildOrchestratorHost(uploaderMock, envDetectorMock);
        var orchestrator = new TestOrchestrator(host);
        orchestrator.RecordExecution(BuildExecution("FinalTest"));

        // Act
        await orchestrator.FinalizeAsync();

        // Assert — at least one upload must have Finalized state
        Assert.Contains(TestSessionState.Finalized, capturedStates);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FinalizeSessionAsync_WhenUploadFails_ExitsDrainLoopImmediately()
    {
        // Arrange
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());

        // Non-finalized uploads always fail
        uploaderMock
            .Setup(u => u.UploadAsync(
                It.Is<TestSession>(s => s.SessionState != TestSessionState.Finalized),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadResult { Success = false, ErrorMessage = "Simulated failure" });

        // Finalized upload (FinalFlushAsync) succeeds
        uploaderMock
            .Setup(u => u.UploadAsync(
                It.Is<TestSession>(s => s.SessionState == TestSessionState.Finalized),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadResult { Success = true, TotalRecordsCount = 0 });

        var host = ServiceHelper.BuildOrchestratorHost(uploaderMock, envDetectorMock);
        var orchestrator = new TestOrchestrator(host);
        orchestrator.RecordExecution(BuildExecution("FailTest"));

        // Act
        await orchestrator.FinalizeAsync();

        // Assert — the drain loop must have called UploadAsync exactly once for the
        // non-finalized session. A second call would mean the loop did not exit on failure,
        // which is the bug this test guards against.
        uploaderMock.Verify(
            u => u.UploadAsync(
                It.Is<TestSession>(s => s.SessionState != TestSessionState.Finalized),
                It.IsAny<CancellationToken>()),
            Times.Once);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FinalizeSessionAsync_ShouldIncludeQuickStatistics_InFinalizedSession()
    {
        // Arrange
        TestSession? finalizedSession = null;
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());
        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .Callback<TestSession, CancellationToken>((s, _) =>
            {
                if (s.SessionState == TestSessionState.Finalized)
                    finalizedSession = s;
            })
            .ReturnsAsync((TestSession s, CancellationToken _) =>
                new UploadResult { Success = true, TotalRecordsCount = s.Executions.Count });

        var host = ServiceHelper.BuildOrchestratorHost(uploaderMock, envDetectorMock);
        var orchestrator = new StatsOrchestrator(host);
        orchestrator.RecordExecution(BuildExecution("StatTest"));

        // Act
        await orchestrator.FinalizeAsync();

        // Assert
        Assert.NotNull(finalizedSession);
        Assert.NotNull(finalizedSession.QuickStatistics);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FinalizeSessionAsync_ShouldPopulateQuickStatistics_OnUploadResult()
    {
        // Arrange — verifies that the UploadResult returned by FinalizeSessionAsync
        // carries QuickStatistics so framework adapters can log the summary.
        UploadResult? capturedResult = null;
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());
        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestSession s, CancellationToken _) =>
                new UploadResult { Success = true, TotalRecordsCount = s.Executions.Count });

        var host = ServiceHelper.BuildOrchestratorHost(uploaderMock, envDetectorMock);
        var orchestrator = new StatsOrchestrator(host);
        orchestrator.RecordExecution(BuildExecution("StatTest"));

        // Capture the result returned by FinalizeSessionAsync via the hook
        orchestrator.OnFinalizedResult = r => capturedResult = r;

        // Act
        await orchestrator.FinalizeAsync();

        // Assert — the returned UploadResult must carry QuickStatistics
        Assert.NotNull(capturedResult);
        Assert.NotNull(capturedResult.QuickStatistics);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task RecordTestExecution_ShouldFeedStatisticsAccumulator()
    {
        // Arrange — use StatsOrchestrator so base OnTestExecutionRecorded runs
        // and feeds _statisticsAccumulator.
        TestSession? finalizedSession = null;
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());
        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .Callback<TestSession, CancellationToken>((s, _) =>
            {
                if (s.SessionState == TestSessionState.Finalized)
                    finalizedSession = s;
            })
            .ReturnsAsync((TestSession s, CancellationToken _) =>
                new UploadResult { Success = true, TotalRecordsCount = s.Executions.Count });

        var host = ServiceHelper.BuildOrchestratorHost(uploaderMock, envDetectorMock);
        var orchestrator = new StatsOrchestrator(host);

        const int executionCount = 3;
        for (int i = 0; i < executionCount; i++)
            orchestrator.RecordExecution(BuildExecution($"StatTest{i}"));

        // Act
        await orchestrator.FinalizeAsync();

        // Assert — QuickStatistics.Total must match the number of recorded executions
        Assert.NotNull(finalizedSession);
        Assert.NotNull(finalizedSession.QuickStatistics);
        Assert.Equal(executionCount, finalizedSession.QuickStatistics.Total);

        await orchestrator.DisposeAsync();
    }

    // ---------------------------------------------------------------------------
    // StrictMode: network error handling — IConfiguration path only.
    // Tests that set/clear XPING_STRICTMODE have been moved to
    // XpingContextOrchestratorEnvVarTests which runs in the "Sequential" collection.
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FinalizeSessionAsync_WithStrictModeViaIConfiguration_AndUploadFailure_ThrowsXpingNetworkException()
    {
        // Arrange — valid config but all uploads fail, strict mode via IConfiguration
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());
        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadResult { Success = false, ErrorMessage = "Simulated network failure" });

        var host = ServiceHelper.BuildOrchestratorHostWithStrictMode(uploaderMock, envDetectorMock);
        var orchestrator = new TestOrchestrator(host);
        orchestrator.RecordExecution(BuildExecution("StrictModeNetworkTest"));

        // Act & Assert
        await Assert.ThrowsAsync<XpingNetworkException>(() => orchestrator.FinalizeAsync());

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FinalizeSessionAsync_WithStrictModeViaIConfiguration_AndUploadSuccess_DoesNotThrow()
    {
        // Arrange — valid config, uploads succeed, strict mode via IConfiguration
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        ServiceHelper.SetupDefaultMocks(uploaderMock, envDetectorMock);

        var host = ServiceHelper.BuildOrchestratorHostWithStrictMode(uploaderMock, envDetectorMock);
        var orchestrator = new TestOrchestrator(host);
        orchestrator.RecordExecution(BuildExecution("StrictModeSuccessTest"));

        // Act & Assert — no exception expected
        var result = await orchestrator.FinalizeAsync();
        Assert.True(result.Success);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FinalizeSessionAsync_WithoutStrictMode_AndUploadFailure_DoesNotThrow()
    {
        // Arrange — valid config, uploads fail, strict mode NOT enabled
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());
        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadResult { Success = false, ErrorMessage = "Simulated network failure" });

        var host = ServiceHelper.BuildOrchestratorHost(uploaderMock, envDetectorMock);
        var orchestrator = new TestOrchestrator(host);
        orchestrator.RecordExecution(BuildExecution("ResilientModeNetworkTest"));

        // Act — no exception expected in resilient (non-strict) mode
        var result = await orchestrator.FinalizeAsync();
        Assert.False(result.Success);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FinalizeSessionAsync_WithStrictModeViaIConfiguration_AndUploadFailure_CallsOnSessionFinalizedBeforeThrowing()
    {
        // Arrange — verify OnSessionFinalizedAsync is called even when strict mode will throw
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());
        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadResult { Success = false, ErrorMessage = "Simulated network failure" });

        var host = ServiceHelper.BuildOrchestratorHostWithStrictMode(uploaderMock, envDetectorMock);
        var orchestrator = new TestOrchestrator(host);
        orchestrator.RecordExecution(BuildExecution("HookOrderTest"));

        // Act & Assert
        await Assert.ThrowsAsync<XpingNetworkException>(() => orchestrator.FinalizeAsync());

        // The hook must have been called before the throw
        Assert.Equal(1, orchestrator.SessionFinalizedCount);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FinalizeSessionAsync_WithStrictModeViaIConfiguration_AndUploadFailure_ExceptionContainsErrorMessage()
    {
        // Arrange
        const string simulatedError = "Connection refused by server";
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());
        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadResult { Success = false, ErrorMessage = simulatedError });

        var host = ServiceHelper.BuildOrchestratorHostWithStrictMode(uploaderMock, envDetectorMock);
        var orchestrator = new TestOrchestrator(host);
        orchestrator.RecordExecution(BuildExecution("ErrorMessageTest"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<XpingNetworkException>(() => orchestrator.FinalizeAsync());
        Assert.Contains(simulatedError, ex.Message, StringComparison.Ordinal);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FinalizeSessionAsync_WithStrictModeViaIConfiguration_AndUploadFailure_SecondCallReturnsFailed()
    {
        // Arrange — after the first finalization throws XpingNetworkException, a subsequent call
        // (e.g. from DisposeAsync during test teardown) should return the stored failure rather
        // than masking it with Success=true.
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());
        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadResult { Success = false, ErrorMessage = "Simulated failure" });

        var host = ServiceHelper.BuildOrchestratorHostWithStrictMode(uploaderMock, envDetectorMock);
        var orchestrator = new TestOrchestrator(host);
        orchestrator.RecordExecution(BuildExecution("SecondCallTest"));

        // First call throws
        await Assert.ThrowsAsync<XpingNetworkException>(() => orchestrator.FinalizeAsync());

        // Act — second call (simulates a redundant finalization attempt after the exception)
        var secondResult = await orchestrator.FinalizeAsync();

        // Assert — failure is surfaced, not masked with Success=true
        Assert.False(secondResult.Success);
        Assert.NotNull(secondResult.ErrorMessage);
        Assert.Contains("Simulated failure", secondResult.ErrorMessage, StringComparison.Ordinal);

        await orchestrator.DisposeAsync();
    }

    [Fact]
    public async Task FinalizeSessionAsync_WithStrictModeViaIConfiguration_AndNullErrorMessage_UsesDefaultFallback()
    {
        // Arrange — upload fails with null ErrorMessage; the exception should not end with ": "
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());
        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<TestSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UploadResult { Success = false, ErrorMessage = null });

        var host = ServiceHelper.BuildOrchestratorHostWithStrictMode(uploaderMock, envDetectorMock);
        var orchestrator = new TestOrchestrator(host);
        orchestrator.RecordExecution(BuildExecution("NullErrorMessageTest"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<XpingNetworkException>(() => orchestrator.FinalizeAsync());
        Assert.False(ex.Message.EndsWith(": ", StringComparison.Ordinal));
        Assert.Contains("Upload failed with no additional details", ex.Message, StringComparison.Ordinal);

        await orchestrator.DisposeAsync();
    }
}
