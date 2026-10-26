/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.XUnit.V3.Tests;

/// <summary>
/// Tests for <see cref="XpingContext"/> lifecycle management.
/// </summary>
[Collection(XpingContextCollection.Name)]
public sealed class XpingContextTests : IAsyncLifetime
{
    // A store of its own, thrown away with the fixture, so nothing recorded here lands in the
    // repository's .xping/.
    private readonly string _scratchStore = Path.Combine(
        Path.GetTempPath(), "xping-tests", Guid.NewGuid().ToString("N"));

    public ValueTask InitializeAsync() => XpingContext.ShutdownAsync();

    public async ValueTask DisposeAsync()
    {
        await XpingContext.ShutdownAsync().ConfigureAwait(true);

        if (Directory.Exists(_scratchStore))
            Directory.Delete(_scratchStore, recursive: true);
    }

    [Fact]
    public void IsInitialized_BeforeInitialize_ReturnsFalse()
    {
        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public void Initialize_SecondCall_IsIdempotent()
    {
        XpingContext.Initialize(new XpingConfiguration { Mode = XpingMode.LocalOnly, LocalStorePath = _scratchStore });
        XpingContext.Initialize();

        Assert.True(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_BeforeInitialize_ReturnsNoError()
    {
        Assert.Null(await XpingContext.FinalizeAndShutdownAsync());
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_StrictModeUploadFails_ReturnsTheNetworkError()
    {
        XpingContext.Initialize(UnreachableCloudConfiguration(strictMode: true));
        XpingContext.RecordTest(CreateTestExecution());

        XpingNetworkException? runFailure = await XpingContext.FinalizeAndShutdownAsync();

        Assert.NotNull(runFailure);
        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_UploadFailsOutsideStrictMode_ReturnsNoError()
    {
        XpingContext.Initialize(UnreachableCloudConfiguration(strictMode: false));
        XpingContext.RecordTest(CreateTestExecution());

        Assert.Null(await XpingContext.FinalizeAndShutdownAsync());
        Assert.False(XpingContext.IsInitialized);
    }

    // Port 9 (discard) is closed on loopback, so the upload is refused immediately.
    private XpingConfiguration UnreachableCloudConfiguration(bool strictMode) => new()
    {
        Mode = XpingMode.Cloud,
        ApiKey = "test-key",
        ProjectId = "test-project",
        ApiEndpoint = "http://127.0.0.1:9/v1",
        MaxRetries = 0,
        StrictMode = strictMode,
        LocalStorePath = _scratchStore,
    };

    private static TestExecution CreateTestExecution() => new TestExecutionBuilder()
        .WithTestName("TestMethod")
        .WithOutcome(TestOutcome.Passed)
        .WithDuration(TimeSpan.FromMilliseconds(100))
        .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
        .WithEndTime(DateTime.UtcNow)
        .Build();
}
