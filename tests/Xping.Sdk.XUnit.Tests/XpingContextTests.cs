/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.XUnit.Tests;

/// <summary>
/// Tests for <see cref="XpingContext"/> lifecycle management.
/// </summary>
[Collection("XpingContext")]
public sealed class XpingContextTests : IAsyncLifetime
{
    public Task InitializeAsync() => XpingContext.ShutdownAsync().AsTask();

    public Task DisposeAsync() => XpingContext.ShutdownAsync().AsTask();

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
        XpingContext.Initialize();
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
    // Helpers
    // ---------------------------------------------------------------------------

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
