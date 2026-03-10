/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.PullRequest.Internals;

namespace Xping.Sdk.Core.Tests.Services.PullRequest;

public sealed class NoOpPullRequestContextDetectorTests
{
    // ---------------------------------------------------------------------------
    // Detect
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_Always_ReturnsNull()
    {
        // Arrange
        var detector = new NoOpPullRequestContextDetector();

        // Act
        var result = detector.Detect();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Detect_CalledMultipleTimes_AlwaysReturnsNull()
    {
        // Arrange
        var detector = new NoOpPullRequestContextDetector();

        // Act & Assert
        for (int i = 0; i < 5; i++)
        {
            Assert.Null(detector.Detect());
        }
    }

    [Fact]
    public void Detect_DoesNotThrow()
    {
        // Arrange
        var detector = new NoOpPullRequestContextDetector();

        // Act & Assert
        var ex = Record.Exception(() => detector.Detect());
        Assert.Null(ex);
    }
}
