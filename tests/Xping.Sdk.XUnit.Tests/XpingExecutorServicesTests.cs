/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit.Tests;

/// <summary>
/// Tests for <see cref="XpingExecutorServices"/> resolved via <see cref="XpingContext.GetExecutorServices"/>.
/// </summary>
[Collection("XpingContext")]
public sealed class XpingExecutorServicesTests : IAsyncLifetime
{
    public Task InitializeAsync() => XpingContext.ShutdownAsync().AsTask();

    public Task DisposeAsync() => XpingContext.ShutdownAsync().AsTask();

    // ---------------------------------------------------------------------------
    // Property non-null checks
    // ---------------------------------------------------------------------------

    [Fact]
    public void ExecutionTracker_ShouldNotBeNull()
    {
        XpingContext.Initialize();
        var services = XpingContext.GetExecutorServices();

        Assert.NotNull(services.ExecutionTracker);
    }

    [Fact]
    public void RetryDetector_ShouldNotBeNull()
    {
        XpingContext.Initialize();
        var services = XpingContext.GetExecutorServices();

        Assert.NotNull(services.RetryDetector);
    }

    [Fact]
    public void IdentityGenerator_ShouldNotBeNull()
    {
        XpingContext.Initialize();
        var services = XpingContext.GetExecutorServices();

        Assert.NotNull(services.IdentityGenerator);
    }

    [Fact]
    public void Logger_ShouldNotBeNull()
    {
        XpingContext.Initialize();
        var services = XpingContext.GetExecutorServices();

        Assert.NotNull(services.Logger);
    }

    // ---------------------------------------------------------------------------
    // Singleton consistency
    // ---------------------------------------------------------------------------

    [Fact]
    public void GetExecutorServices_CalledTwice_ReturnsSameServiceInstances()
    {
        XpingContext.Initialize();

        var s1 = XpingContext.GetExecutorServices();
        var s2 = XpingContext.GetExecutorServices();

        // All four services should be singleton-scoped within the host.
        Assert.Same(s1.ExecutionTracker, s2.ExecutionTracker);
        Assert.Same(s1.RetryDetector, s2.RetryDetector);
        Assert.Same(s1.IdentityGenerator, s2.IdentityGenerator);
        Assert.Same(s1.Logger, s2.Logger);
    }
}
