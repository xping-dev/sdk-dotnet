/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Hosting;

/// <summary>
/// Decides what Ctrl+C does: stop the running command cleanly, or end the process.
/// </summary>
/// <remarks>
/// <para>
/// Only a command that watches its cancellation token can stop cleanly. <c>login</c> must close
/// its listener and say "Cancelled."; <c>report</c>, <c>where</c> and <c>clear</c> never look at the
/// token, so swallowing the signal for them would leave Ctrl+C doing nothing at all, including at
/// the <c>clear</c> prompt. For those, and for a second press when a cooperative command does not
/// stop, the process ends as it always has.
/// </para>
/// </remarks>
internal sealed class CancelKeyHandler(CancellationTokenSource cancellation)
{
    private int _cooperative;

    /// <summary>
    /// Gets the token cancelled by Ctrl+C while a cooperative command runs.
    /// </summary>
    public CancellationToken Token => cancellation.Token;

    /// <summary>
    /// Marks a command that stops on <see cref="Token"/> as running, until the result is disposed.
    /// </summary>
    /// <returns>Ends the cooperative section.</returns>
    public IDisposable EnterCooperative()
    {
        Interlocked.Increment(ref _cooperative);
        return new Exit(this);
    }

    /// <summary>
    /// Handles one Ctrl+C.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> to keep the process alive because the running command will stop on
    /// its own; <see langword="false"/> to let it end.
    /// </returns>
    public bool OnCancelKeyPress()
    {
        if (Volatile.Read(ref _cooperative) == 0 || cancellation.IsCancellationRequested)
            return false;

        cancellation.Cancel();
        return true;
    }

    private sealed class Exit(CancelKeyHandler owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                Interlocked.Decrement(ref owner._cooperative);
        }
    }
}
