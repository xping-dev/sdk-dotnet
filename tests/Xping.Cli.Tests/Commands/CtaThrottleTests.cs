/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Time.Testing;
using Xping.Cli.Commands;
using Xping.Sdk.Core.Services.Environment;

namespace Xping.Cli.Tests.Commands;

public sealed class CtaThrottleTests : IDisposable
{
    private readonly string _root;
    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));
    private readonly CtaThrottle _throttle;

    public CtaThrottleTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-cta-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _throttle = new CtaThrottle(new DictionaryEnvironment(_environment), _time);
    }

    public void Dispose()
    {
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
        bool first = _throttle.ShouldShow(_root, hasFindings: true, isConnected: false);
        bool second = _throttle.ShouldShow(_root, hasFindings: true, isConnected: false);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void StaysQuietUntilTheFullIntervalHasElapsed()
    {
        Assert.True(_throttle.ShouldShow(_root, hasFindings: true, isConnected: false));

        _time.Advance(TimeSpan.FromHours(24) - TimeSpan.FromMinutes(1));

        Assert.False(_throttle.ShouldShow(_root, hasFindings: true, isConnected: false));
    }

    [Fact]
    public void ShowsAgainOnceTheIntervalHasElapsed()
    {
        Assert.True(_throttle.ShouldShow(_root, hasFindings: true, isConnected: false));

        _time.Advance(TimeSpan.FromHours(24));

        Assert.True(_throttle.ShouldShow(_root, hasFindings: true, isConnected: false));
    }

    [Fact]
    public void IsSuppressedForConnectedProjects()
    {
        // They already signed up.
        Assert.False(_throttle.ShouldShow(_root, hasFindings: true, isConnected: true));
    }

    [Fact]
    public void IsSuppressedAfterACleanReport()
    {
        // Never pitch when nothing was found; the ask has to be tied to a problem just shown.
        Assert.False(_throttle.ShouldShow(_root, hasFindings: false, isConnected: false));
    }

    [Fact]
    public void IsSuppressedByTheInjectedEnvironment()
    {
        // Through the command's environment, not the process's: the process variable is never set
        // here, and a throttle that read it would show the invitation.
        _environment[CtaThrottle.SuppressBannerVariable] = "1";

        Assert.False(_throttle.ShouldShow(_root, hasFindings: true, isConnected: false));
    }

    [Fact]
    public void StaysQuietWhenTheThrottleCannotBeRecorded()
    {
        // Without somewhere to record the timestamp there is no way to rate-limit, so the safe
        // failure is silence rather than pitching on every invocation.
        Assert.False(_throttle.ShouldShow(null, hasFindings: true, isConnected: false));
    }

    [Fact]
    public void TreatsAnUnparsableStateFileAsNeverShown()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_root, CtaThrottle.StateFileName), "{ not json");

        // Act & Assert — a corrupt throttle file must not permanently silence the invitation.
        Assert.True(_throttle.ShouldShow(_root, hasFindings: true, isConnected: false));
    }

    private sealed class DictionaryEnvironment(Dictionary<string, string> variables) : IEnvironmentVariableProvider
    {
        public string? GetVariable(string name) => variables.GetValueOrDefault(name);
    }
}
