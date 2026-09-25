/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Moq;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xunit.Abstractions;

namespace Xping.Sdk.XUnit.Tests;

/// <summary>
/// Tests for <see cref="XpingContext"/> lifecycle management.
/// </summary>
[Collection("XpingContext")]
public sealed class XpingContextTests : IAsyncLifetime
{
    // A store of its own, thrown away with the fixture. Without it the context under test resolves
    // the repository's .xping/ - the same one the run recording this suite writes to - and every
    // synthetic execution recorded below would land in the real history.
    private readonly string _scratchStore = Path.Combine(
        Path.GetTempPath(), "xping-tests", Guid.NewGuid().ToString("N"));

    public Task InitializeAsync() => XpingContext.ShutdownAsync().AsTask();

    public async Task DisposeAsync()
    {
        await XpingContext.ShutdownAsync().ConfigureAwait(false);
        DeleteScratchStore();
    }

    // ---------------------------------------------------------------------------
    // IsInitialized
    // ---------------------------------------------------------------------------

    [Fact]
    public void IsInitialized_BeforeInitialize_ReturnsFalse()
    {
        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public void IsInitialized_AfterInitialize_ReturnsTrue()
    {
        XpingContext.Initialize();

        Assert.True(XpingContext.IsInitialized);
    }

    // ---------------------------------------------------------------------------
    // Initialize
    // ---------------------------------------------------------------------------

    [Fact]
    public void Initialize_FirstCall_SetsIsInitializedTrue()
    {
        XpingContext.Initialize();

        Assert.True(XpingContext.IsInitialized);
    }

    [Fact]
    public void Initialize_SecondCall_IsIdempotent()
    {
        XpingContext.Initialize();
        XpingContext.Initialize(); // second call is a no-op

        Assert.True(XpingContext.IsInitialized);
    }

    [Fact]
    public void Initialize_WithCustomConfiguration_SetsIsInitializedTrue()
    {
        var config = new XpingConfiguration
        {
            ApiKey = "test-key",
            ProjectId = "test-project"
        };

        XpingContext.Initialize(config);

        Assert.True(XpingContext.IsInitialized);
    }

    [Fact]
    public void Initialize_WithConfiguration_SecondCall_IsIdempotent()
    {
        var config = new XpingConfiguration
        {
            ApiKey = "test-key",
            ProjectId = "test-project"
        };

        XpingContext.Initialize(config);
        XpingContext.Initialize(config); // second call is a no-op

        Assert.True(XpingContext.IsInitialized);
    }

    // ---------------------------------------------------------------------------
    // GetExecutorServices
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetExecutorServices_BeforeInitialize_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => XpingContext.GetExecutorServices());
    }

    [Fact]
    public void GetExecutorServices_AfterInitialize_ReturnsNonNull()
    {
        XpingContext.Initialize();

        var services = XpingContext.GetExecutorServices();

        Assert.NotNull(services);
    }

    // ---------------------------------------------------------------------------
    // RecordTest
    // ---------------------------------------------------------------------------

    [Fact]
    public void RecordTest_AfterInitialize_DoesNotThrow()
    {
        XpingContext.Initialize(ScratchConfiguration());
        var execution = CreateTestExecution();

        var exception = Record.Exception(() => XpingContext.RecordTest(execution));

        Assert.Null(exception);
    }

    // ---------------------------------------------------------------------------
    // FlushAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FlushAsync_AfterInitialize_DoesNotThrow()
    {
        XpingContext.Initialize();

        var exception = await Record.ExceptionAsync(() => XpingContext.FlushAsync());

        Assert.Null(exception);
    }

    // ---------------------------------------------------------------------------
    // FinalizeAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FinalizeAsync_BeforeInitialize_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() => XpingContext.FinalizeAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task FinalizeAsync_AfterInitialize_DoesNotThrow()
    {
        XpingContext.Initialize();

        var exception = await Record.ExceptionAsync(() => XpingContext.FinalizeAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task FinalizeAsync_WhenHostNotYetBuilt_ReturnsCompletedTask()
    {
        // Initialize registers the Lazy but doesn't build the host.
        // FinalizeAsync short-circuits when !IsValueCreated.
        XpingContext.Initialize();

        var exception = await Record.ExceptionAsync(() => XpingContext.FinalizeAsync());

        Assert.Null(exception);
    }

    // ---------------------------------------------------------------------------
    // ShutdownAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ShutdownAsync_AfterInitialize_ResetsIsInitialized()
    {
        XpingContext.Initialize();
        Assert.True(XpingContext.IsInitialized);

        await XpingContext.ShutdownAsync();

        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task ShutdownAsync_BeforeInitialize_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(() => XpingContext.ShutdownAsync().AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task ShutdownAsync_MultipleCallsSafe()
    {
        XpingContext.Initialize();

        await XpingContext.ShutdownAsync();
        var exception = await Record.ExceptionAsync(() => XpingContext.ShutdownAsync().AsTask());

        Assert.Null(exception);
    }

    [Fact]
    public async Task ShutdownAsync_AllowsReinitialize()
    {
        XpingContext.Initialize();
        await XpingContext.ShutdownAsync();

        // Should be able to initialize again after shutdown
        XpingContext.Initialize();

        Assert.True(XpingContext.IsInitialized);
    }

    // ---------------------------------------------------------------------------
    // FinalizeAndShutdownAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task FinalizeAndShutdownAsync_StrictModeUploadFails_FailsFast()
    {
        XpingContext.Initialize(UnreachableCloudConfiguration(strictMode: true));
        XpingContext.RecordTest(CreateTestExecution());
        var calls = new List<Exception>();

        await XpingContext.FinalizeAndShutdownAsync((_, ex) => calls.Add(ex));

        Assert.IsType<XpingNetworkException>(Assert.Single(calls));
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_StrictModeUploadFails_StillShutsDown()
    {
        XpingContext.Initialize(UnreachableCloudConfiguration(strictMode: true));
        XpingContext.RecordTest(CreateTestExecution());

        await XpingContext.FinalizeAndShutdownAsync((_, _) => { });

        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_UploadFailsOutsideStrictMode_DoesNotFailFast()
    {
        XpingContext.Initialize(UnreachableCloudConfiguration(strictMode: false));
        XpingContext.RecordTest(CreateTestExecution());
        var calls = new List<Exception>();

        await XpingContext.FinalizeAndShutdownAsync((_, ex) => calls.Add(ex));

        Assert.Empty(calls);
        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_BeforeInitialize_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(
            () => XpingContext.FinalizeAndShutdownAsync((_, ex) => throw ex));

        Assert.Null(exception);
    }

    [Fact]
    public void AssemblyFinished_EndsSessionBeforeRunnerIsTold()
    {
        // Once the runner sees the assembly finish it wraps up the run, and under `dotnet test` the
        // test host exits with the session half written. The session has to be over by then.
        XpingContext.Initialize(ScratchConfiguration());
        XpingContext.GetExecutorServices(); // builds the host, as the framework constructor does

        bool? initializedWhenForwarded = null;
        var innerSink = new Mock<IMessageSink>();
        innerSink
            .Setup(s => s.OnMessage(It.IsAny<ITestAssemblyFinished>()))
            .Callback(() => initializedWhenForwarded = XpingContext.IsInitialized)
            .Returns(true);

        IMessageSink sink = new XpingMessageSink(
            innerSink.Object,
            Mock.Of<IExecutionTracker>(),
            Mock.Of<IRetryDetector<ITest>>(),
            Mock.Of<ITestIdentityGenerator>(),
            captureStackTraces: false,
            assemblyName: "Xping.Sdk.XUnit.Tests");

        sink.OnMessage(Mock.Of<ITestAssemblyFinished>());

        Assert.False(initializedWhenForwarded);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private XpingConfiguration ScratchConfiguration() =>
        new() { LocalStorePath = _scratchStore };

    // Port 9 (discard) is closed on loopback, so the upload is refused immediately.
    private XpingConfiguration UnreachableCloudConfiguration(bool strictMode) => new()
    {
        Mode = XpingMode.Cloud,
        ApiKey = "test-key",
        ProjectId = "test-project",
        ApiEndpoint = "http://127.0.0.1:9/v1",
        MaxRetries = 1,
        RetryDelay = TimeSpan.FromMilliseconds(1),
        StrictMode = strictMode,
        LocalStorePath = _scratchStore,
    };

    private void DeleteScratchStore()
    {
        if (Directory.Exists(_scratchStore))
            Directory.Delete(_scratchStore, recursive: true);
    }

    private static TestExecution CreateTestExecution()
    {
        return new TestExecutionBuilder()
            .WithTestName("TestMethod")
            .WithOutcome(TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(100))
            .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
            .WithEndTime(DateTime.UtcNow)
            .Build();
    }
}
