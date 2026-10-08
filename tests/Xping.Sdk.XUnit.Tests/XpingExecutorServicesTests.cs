/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Configuration;

namespace Xping.Sdk.XUnit.Tests;

/// <summary>
/// Tests for <see cref="XpingExecutorServices"/> resolved via <see cref="XpingContext.GetExecutorServices"/>.
/// </summary>
[Collection("XpingContext")]
public sealed class XpingExecutorServicesTests : IAsyncLifetime
{
    private readonly string _scratchStore = Path.Combine(
        Path.GetTempPath(), "xping-tests", Guid.NewGuid().ToString("N"));

    public Task InitializeAsync() => XpingContext.ShutdownAsync().AsTask();

    public async Task DisposeAsync()
    {
        await XpingContext.ShutdownAsync().ConfigureAwait(false);

        if (Directory.Exists(_scratchStore))
            Directory.Delete(_scratchStore, recursive: true);
    }

    // ---------------------------------------------------------------------------
    // Property non-null checks
    // ---------------------------------------------------------------------------

    [Fact]
    public void ExecutionTracker_ShouldNotBeNull()
    {
        XpingContext.Initialize(ScratchConfiguration());
        var services = XpingContext.GetExecutorServices();

        Assert.NotNull(services.ExecutionTracker);
    }

    [Fact]
    public void RetryDetector_ShouldNotBeNull()
    {
        XpingContext.Initialize(ScratchConfiguration());
        var services = XpingContext.GetExecutorServices();

        Assert.NotNull(services.RetryDetector);
    }

    [Fact]
    public void IdentityGenerator_ShouldNotBeNull()
    {
        XpingContext.Initialize(ScratchConfiguration());
        var services = XpingContext.GetExecutorServices();

        Assert.NotNull(services.IdentityGenerator);
    }

    // ---------------------------------------------------------------------------
    // Singleton consistency
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetExecutorServices_CalledTwice_ReturnsSameServiceInstances()
    {
        XpingContext.Initialize(ScratchConfiguration());

        var s1 = XpingContext.GetExecutorServices();
        var s2 = XpingContext.GetExecutorServices();

        // All three services should be singleton-scoped within the host.
        Assert.Same(s1.ExecutionTracker, s2.ExecutionTracker);
        Assert.Same(s1.RetryDetector, s2.RetryDetector);
        Assert.Same(s1.IdentityGenerator, s2.IdentityGenerator);
    }

    // LocalOnly for the same reason as XpingContextTests.ScratchConfiguration (#260): building the
    // host from the ambient config picks up the pipeline's XPING_APIKEY and uploads to Xping Cloud.
    private XpingConfiguration ScratchConfiguration() =>
        new() { Mode = XpingMode.LocalOnly, LocalStorePath = _scratchStore };
}
