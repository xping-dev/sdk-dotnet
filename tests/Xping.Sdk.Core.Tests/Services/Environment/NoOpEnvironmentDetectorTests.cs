/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.Environment.Internals;

namespace Xping.Sdk.Core.Tests.Services.Environment;

public sealed class NoOpEnvironmentDetectorTests
{
    // ---------------------------------------------------------------------------
    // Individual properties
    // ---------------------------------------------------------------------------

    [Fact]
    public void IsCiEnvironment_ReturnsFalse()
    {
        var detector = new NoOpEnvironmentDetector();
        Assert.False(detector.IsCiEnvironment);
    }

    [Fact]
    public void IsContainer_ReturnsFalse()
    {
        var detector = new NoOpEnvironmentDetector();
        Assert.False(detector.IsContainer);
    }

    [Fact]
    public void EnvironmentName_ReturnsLocal()
    {
        var detector = new NoOpEnvironmentDetector();
        Assert.Equal("Local", detector.EnvironmentName);
    }

    [Fact]
    public void MachineName_ReturnsNonEmpty()
    {
        var detector = new NoOpEnvironmentDetector();
        Assert.False(string.IsNullOrWhiteSpace(detector.MachineName));
    }

    [Fact]
    public void OperatingSystem_ReturnsNonEmpty()
    {
        var detector = new NoOpEnvironmentDetector();
        Assert.False(string.IsNullOrWhiteSpace(detector.OperatingSystem));
    }

    [Fact]
    public void RuntimeVersion_ReturnsNonEmpty()
    {
        var detector = new NoOpEnvironmentDetector();
        Assert.False(string.IsNullOrWhiteSpace(detector.RuntimeVersion));
    }

    [Fact]
    public void Framework_ReturnsNonEmpty()
    {
        var detector = new NoOpEnvironmentDetector();
        Assert.False(string.IsNullOrWhiteSpace(detector.Framework));
    }

    [Fact]
    public void CustomProperties_ReturnsEmptyDictionary()
    {
        var detector = new NoOpEnvironmentDetector();
        Assert.Empty(detector.CustomProperties);
    }

    // ---------------------------------------------------------------------------
    // BuildEnvironmentInfoAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task BuildEnvironmentInfoAsync_DoesNotThrow()
    {
        // Arrange
        var detector = new NoOpEnvironmentDetector();

        // Act & Assert
        var ex = await Record.ExceptionAsync(() => detector.BuildEnvironmentInfoAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_ReturnsInfoConsistentWithProperties()
    {
        // Arrange
        var detector = new NoOpEnvironmentDetector();

        // Act
        var info = await detector.BuildEnvironmentInfoAsync();

        // Assert
        Assert.Equal(detector.MachineName, info.MachineName);
        Assert.Equal(detector.OperatingSystem, info.OperatingSystem);
        Assert.Equal(detector.RuntimeVersion, info.RuntimeVersion);
        Assert.Equal(detector.Framework, info.Framework);
        Assert.Equal(detector.IsCiEnvironment, info.IsCIEnvironment);
        Assert.Equal(detector.EnvironmentName, info.EnvironmentName);
    }

    [Fact]
    public async Task BuildEnvironmentInfoAsync_WithCancelledToken_DoesNotThrow()
    {
        // Arrange
        var detector = new NoOpEnvironmentDetector();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert — no-op never observes the cancellation token
        var ex = await Record.ExceptionAsync(() => detector.BuildEnvironmentInfoAsync(cts.Token));
        Assert.Null(ex);
    }
}
