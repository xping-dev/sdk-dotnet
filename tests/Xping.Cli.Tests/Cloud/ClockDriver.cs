/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// Runs a task to completion on a <see cref="TimerTrackingTimeProvider"/>, moving the clock past
/// each wait it starts.
/// </summary>
internal static class ClockDriver
{
    /// <summary>
    /// Waits for <paramref name="task"/>, advancing the clock by each timer it creates.
    /// </summary>
    /// <remarks>
    /// The clock moves only when the code under test has started a timer, and by exactly that
    /// timer's due time, so gaps measured on the fake clock are exact whatever the machine's speed.
    /// A task that neither finishes nor starts a timer fails the test rather than hang it.
    /// </remarks>
    public static async Task DriveAsync(TimerTrackingTimeProvider time, Task task)
    {
        TimeSpan patience = TimeSpan.FromSeconds(30);

        while (!task.IsCompleted)
        {
            // Waiting does not take the timer, so a wait abandoned because the task finished
            // first leaves it for the next call.
            Task<bool> timerCreated = time.CreatedTimers.WaitToReadAsync().AsTask();
            await Task.WhenAny(task, timerCreated).WaitAsync(patience).ConfigureAwait(false);

            // A timer created with an infinite due time is armed later through Change, which is not
            // tracked, and must not be fired: Polly creates its per-attempt timeout that way.
            if (time.CreatedTimers.TryRead(out TimeSpan dueTime) && dueTime >= TimeSpan.Zero)
                time.Advance(dueTime);
        }

        await task.ConfigureAwait(false);
    }

    public static async Task<T> DriveAsync<T>(TimerTrackingTimeProvider time, Task<T> task)
    {
        await DriveAsync(time, (Task)task).ConfigureAwait(false);
        return await task.ConfigureAwait(false);
    }
}
