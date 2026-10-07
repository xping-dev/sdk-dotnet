/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Threading.Channels;
using Microsoft.Extensions.Time.Testing;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that reports every timer created on it, so a test can move the
/// clock exactly to the wait the code under test started.
/// </summary>
/// <remarks>
/// Advancing the clock on a schedule of its own would race the real loopback requests in between:
/// how far the clock moved would depend on how fast the machine is.
/// </remarks>
internal sealed class TimerTrackingTimeProvider(DateTimeOffset start) : FakeTimeProvider(start)
{
    private readonly Channel<TimeSpan> _created = Channel.CreateUnbounded<TimeSpan>();

    /// <summary>
    /// Gets the due times of timers created and not yet taken, oldest first.
    /// </summary>
    public ChannelReader<TimeSpan> CreatedTimers => _created.Reader;

    /// <summary>
    /// Forgets the timers created so far, so a later drive does not advance by a wait that belonged
    /// to an earlier command.
    /// </summary>
    public void DiscardCreatedTimers()
    {
        while (_created.Reader.TryRead(out _))
        {
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ITimer timer = base.CreateTimer(callback, state, dueTime, period);
        _created.Writer.TryWrite(dueTime);
        return timer;
    }
}
