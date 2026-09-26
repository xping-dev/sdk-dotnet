/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.MSTest.Tests;

using System;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Tests for XpingContext lifecycle management.
/// </summary>
public sealed class XpingContextTests : IAsyncLifetime
{
    // A store of its own, thrown away with the fixture. Without it the context under test resolves
    // the repository's .xping/ - the same one the run recording this suite writes to - and every
    // synthetic execution recorded below would land in the real history.
    private readonly string _scratchStore = Path.Combine(
        Path.GetTempPath(), "xping-tests", Guid.NewGuid().ToString("N"));

    public Task InitializeAsync()
    {
        // Ensure clean state before each test
        return XpingContext.ShutdownAsync().AsTask();
    }

    public async Task DisposeAsync()
    {
        // Clean up after each test
        await XpingContext.ShutdownAsync().ConfigureAwait(false);
        DeleteScratchStore();
    }

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

    [Fact]
    public void RecordTest_AfterInitialize_DoesNotThrow()
    {
        XpingContext.Initialize(ScratchConfiguration());
        var execution = CreateTestExecution();

        var exception = Record.Exception(() => XpingContext.RecordTest(execution));

        Assert.Null(exception);
    }

    [Fact]
    public async Task FlushAsync_AfterInitialize_DoesNotThrow()
    {
        XpingContext.Initialize();

        var exception = await Record.ExceptionAsync(async () => await XpingContext.FlushAsync().ConfigureAwait(true)).ConfigureAwait(true);

        Assert.Null(exception);
    }

    [Fact]
    public async Task ShutdownAsync_AfterInitialize_ResetsContext()
    {
        XpingContext.Initialize();
        Assert.True(XpingContext.IsInitialized);

        await XpingContext.ShutdownAsync().ConfigureAwait(true);

        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task ShutdownAsync_MultipleCallsSafe()
    {
        XpingContext.Initialize();

        await XpingContext.ShutdownAsync().ConfigureAwait(true);
        var exception = await Record.ExceptionAsync(async () => await XpingContext.ShutdownAsync().ConfigureAwait(true)).ConfigureAwait(true);

        Assert.Null(exception);
    }

    [Fact]
    public async Task ShutdownAsync_BeforeInitialize_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(async () => await XpingContext.ShutdownAsync().ConfigureAwait(true)).ConfigureAwait(true);

        Assert.Null(exception);
    }

    [Fact]
    public async Task FinalizeAsync_BeforeInitialize_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(async () => await XpingContext.FinalizeAsync().ConfigureAwait(true)).ConfigureAwait(true);

        Assert.Null(exception);
    }

    [Fact]
    public async Task FinalizeAsync_AfterInitialize_DoesNotThrow()
    {
        XpingContext.Initialize();

        var exception = await Record.ExceptionAsync(async () => await XpingContext.FinalizeAsync().ConfigureAwait(true)).ConfigureAwait(true);

        Assert.Null(exception);
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_AfterInitialize_ResetsContext()
    {
        XpingContext.Initialize();
        Assert.True(XpingContext.IsInitialized);

        await XpingContext.FinalizeAndShutdownAsync().ConfigureAwait(true);

        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public void FinalizeOnProcessExit_WithMaterializedSession_FinalizesAndResetsContext()
    {
        // The safety net for a session MSTest never handed to [AssemblyCleanup]: it must run the
        // whole finalize-and-shutdown sequence itself, so afterwards the context is gone.
        XpingContext.Initialize(ScratchConfiguration());
        XpingContext.RecordTest(CreateTestExecution()); // materializes the host
        Assert.True(XpingContext.IsInitialized);

        XpingContext.FinalizeOnProcessExit();

        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public void FinalizeOnProcessExit_WithoutMaterializedSession_LeavesContextAlone()
    {
        // Initialize() only registers the lazy wrapper. Nothing ran, so there is nothing to
        // finalize, and building a host on the way out of the process would be pure cost.
        XpingContext.Initialize(ScratchConfiguration());

        XpingContext.FinalizeOnProcessExit();

        Assert.True(XpingContext.IsInitialized);
    }

    [Fact]
    public void FinalizeOnProcessExit_BeforeInitialize_DoesNotThrow()
    {
        var exception = Record.Exception(XpingContext.FinalizeOnProcessExit);

        Assert.Null(exception);
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_BeforeInitialize_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(async () => await XpingContext.FinalizeAndShutdownAsync().ConfigureAwait(true)).ConfigureAwait(true);

        Assert.Null(exception);
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_StrictModeUploadFails_FailsFast()
    {
        XpingContext.Initialize(UnreachableCloudConfiguration(strictMode: true));
        XpingContext.RecordTest(CreateTestExecution());
        var calls = new List<Exception>();

        await XpingContext.FinalizeAndShutdownAsync((_, ex) => calls.Add(ex)).ConfigureAwait(true);

        Assert.IsType<XpingNetworkException>(Assert.Single(calls));
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_StrictModeUploadFails_StillShutsDown()
    {
        XpingContext.Initialize(UnreachableCloudConfiguration(strictMode: true));
        XpingContext.RecordTest(CreateTestExecution());

        await XpingContext.FinalizeAndShutdownAsync((_, _) => { }).ConfigureAwait(true);

        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_UploadFailsOutsideStrictMode_DoesNotFailFast()
    {
        XpingContext.Initialize(UnreachableCloudConfiguration(strictMode: false));
        XpingContext.RecordTest(CreateTestExecution());
        var calls = new List<Exception>();

        await XpingContext.FinalizeAndShutdownAsync((_, ex) => calls.Add(ex)).ConfigureAwait(true);

        Assert.Empty(calls);
        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_BeforeInitialize_DoesNotFailFast()
    {
        var exception = await Record.ExceptionAsync(
            () => XpingContext.FinalizeAndShutdownAsync((_, ex) => throw ex)).ConfigureAwait(true);

        Assert.Null(exception);
    }

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
        // Use the builder to create a properly constructed TestExecution
        return new TestExecutionBuilder()
            .WithTestName("TestMethod")
            .WithOutcome(TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(100))
            .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
            .WithEndTime(DateTime.UtcNow)
            .Build();
    }
}
