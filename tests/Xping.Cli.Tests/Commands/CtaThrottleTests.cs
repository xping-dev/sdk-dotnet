/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Commands;

namespace Xping.Cli.Tests.Commands;

// Mutates XPING_NO_BANNER, which is process-wide state.
[Collection("Sequential")]
public sealed class CtaThrottleTests : IDisposable
{
    private readonly string _root;

    public CtaThrottleTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-cta-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, null);

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void ShowsAtMostOncePerDay()
    {
        // A tool that pitches on every invocation gets uninstalled.
        bool first = CtaThrottle.ShouldShow(_root, hasFindings: true, isConnected: false);
        bool second = CtaThrottle.ShouldShow(_root, hasFindings: true, isConnected: false);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void ShowsAgainOnceTheIntervalHasElapsed()
    {
        // Arrange — record a timestamp older than the 24 hour window.
        File.WriteAllText(
            Path.Combine(_root, CtaThrottle.StateFileName),
            $"{{\"ctaLastShownUtc\":\"{DateTime.UtcNow.AddDays(-2):O}\"}}");

        // Act & Assert
        Assert.True(CtaThrottle.ShouldShow(_root, hasFindings: true, isConnected: false));
    }

    [Fact]
    public void IsSuppressedForConnectedProjects()
    {
        // They already signed up.
        Assert.False(CtaThrottle.ShouldShow(_root, hasFindings: true, isConnected: true));
    }

    [Fact]
    public void IsSuppressedAfterACleanReport()
    {
        // Never pitch when nothing was found; the ask has to be tied to a problem just shown.
        Assert.False(CtaThrottle.ShouldShow(_root, hasFindings: false, isConnected: false));
    }

    [Fact]
    public void IsSuppressedByEnvironmentVariable()
    {
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, "1");

        Assert.False(CtaThrottle.ShouldShow(_root, hasFindings: true, isConnected: false));
    }

    [Fact]
    public void StaysQuietWhenTheThrottleCannotBeRecorded()
    {
        // Without somewhere to record the timestamp there is no way to rate-limit, so the safe
        // failure is silence rather than pitching on every invocation.
        Assert.False(CtaThrottle.ShouldShow(null, hasFindings: true, isConnected: false));
    }

    [Fact]
    public void TreatsAnUnparsableStateFileAsNeverShown()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_root, CtaThrottle.StateFileName), "{ not json");

        // Act & Assert — a corrupt throttle file must not permanently silence the invitation.
        Assert.True(CtaThrottle.ShouldShow(_root, hasFindings: true, isConnected: false));
    }
}
