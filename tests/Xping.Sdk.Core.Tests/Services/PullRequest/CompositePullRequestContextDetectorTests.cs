/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.PullRequests;
using Xping.Sdk.Core.Services.PullRequest.Internals;

namespace Xping.Sdk.Core.Tests.Services.PullRequest;

public sealed class CompositePullRequestContextDetectorTests
{
    // ---------------------------------------------------------------------------
    // Hand-rolled test doubles
    // IPlatformPullRequestDetector is internal; Moq/Castle cannot proxy it without
    // [InternalsVisibleTo("DynamicProxyGenAssembly2")]. Concrete fakes avoid that constraint.
    // ---------------------------------------------------------------------------

    /// <summary>Fake that always returns the context supplied at construction time.</summary>
    private sealed class StubDetector : IPlatformPullRequestDetector
    {
        private readonly PullRequestContext? _context;
        public int CallCount { get; private set; }

        public StubDetector(PullRequestContext? context) => _context = context;

        public PullRequestContext? Detect()
        {
            CallCount++;
            return _context;
        }
    }

    // ---------------------------------------------------------------------------
    // Detect — empty detector list
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_WithNoDetectors_ReturnsNull()
    {
        // Arrange
        var composite = new CompositePullRequestContextDetector(
            Enumerable.Empty<IPlatformPullRequestDetector>());

        // Act
        var result = composite.Detect();

        // Assert
        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // Detect — all detectors return null
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_AllDetectorsReturnNull_ReturnsNull()
    {
        // Arrange
        var d1 = new StubDetector(null);
        var d2 = new StubDetector(null);
        var composite = new CompositePullRequestContextDetector(
            new IPlatformPullRequestDetector[] { d1, d2 });

        // Act
        var result = composite.Detect();

        // Assert
        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // Detect — first detector succeeds
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_FirstDetectorReturnsContext_ReturnsImmediately()
    {
        // Arrange
        var context = new PullRequestContext();
        var first = new StubDetector(context);
        var second = new StubDetector(null);
        var composite = new CompositePullRequestContextDetector(
            new IPlatformPullRequestDetector[] { first, second });

        // Act
        var result = composite.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Same(context, result);
        // Second detector should never be called because first returned a result
        Assert.Equal(0, second.CallCount);
    }

    // ---------------------------------------------------------------------------
    // Detect — second detector succeeds when first returns null
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_FirstDetectorReturnsNull_SecondDetectorReturnsContext_ReturnsContext()
    {
        // Arrange
        var context = new PullRequestContext();
        var first = new StubDetector(null);
        var second = new StubDetector(context);
        var composite = new CompositePullRequestContextDetector(
            new IPlatformPullRequestDetector[] { first, second });

        // Act
        var result = composite.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Same(context, result);
    }

    // ---------------------------------------------------------------------------
    // Detect — all detectors are called in order until a result is found
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_ThirdDetectorSucceeds_FirstAndSecondAreCalled()
    {
        // Arrange
        var context = new PullRequestContext();
        var first = new StubDetector(null);
        var second = new StubDetector(null);
        var third = new StubDetector(context);
        var composite = new CompositePullRequestContextDetector(
            new IPlatformPullRequestDetector[] { first, second, third });

        // Act
        var result = composite.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, first.CallCount);
        Assert.Equal(1, second.CallCount);
        Assert.Equal(1, third.CallCount);
    }

    // ---------------------------------------------------------------------------
    // Detect — single detector that returns a context
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_SingleDetectorReturnsContext_ReturnsThatContext()
    {
        // Arrange
        var context = new PullRequestContext();
        var detector = new StubDetector(context);
        var composite = new CompositePullRequestContextDetector(
            new IPlatformPullRequestDetector[] { detector });

        // Act
        var result = composite.Detect();

        // Assert
        Assert.NotNull(result);
        Assert.Same(context, result);
    }

    // ---------------------------------------------------------------------------
    // Detect — first detector short-circuits remaining detectors
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_FirstDetectorSucceeds_RemainingDetectorsNotCalled()
    {
        // Arrange
        var context = new PullRequestContext();
        var first = new StubDetector(context);
        var second = new StubDetector(new PullRequestContext());
        var third = new StubDetector(new PullRequestContext());
        var composite = new CompositePullRequestContextDetector(
            new IPlatformPullRequestDetector[] { first, second, third });

        // Act
        composite.Detect();

        // Assert
        Assert.Equal(1, first.CallCount);
        Assert.Equal(0, second.CallCount);
        Assert.Equal(0, third.CallCount);
    }

    // ---------------------------------------------------------------------------
    // Detect — each detector is called exactly once per Detect() invocation
    // ---------------------------------------------------------------------------

    [Fact]
    public void Detect_WithAllNullDetectors_EachDetectorCalledExactlyOnce()
    {
        // Arrange
        var d1 = new StubDetector(null);
        var d2 = new StubDetector(null);
        var d3 = new StubDetector(null);
        var composite = new CompositePullRequestContextDetector(
            new IPlatformPullRequestDetector[] { d1, d2, d3 });

        // Act
        composite.Detect();

        // Assert
        Assert.Equal(1, d1.CallCount);
        Assert.Equal(1, d2.CallCount);
        Assert.Equal(1, d3.CallCount);
    }
}
