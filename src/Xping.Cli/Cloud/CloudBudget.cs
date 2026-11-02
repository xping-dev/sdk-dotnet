/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Cloud;

/// <summary>
/// The wall-clock limit on one report's Cloud work (cloud-data-in-cli-cli-spec §3.2, §3.3).
/// </summary>
/// <remarks>
/// Starts when the enrichment starts, after the local report exists, so local work is never
/// delayed. Everything Cloud does for a report fits in <see cref="Total"/>: a slow Cloud costs its
/// data, never a noticeably slower report (contract §7.3).
/// </remarks>
internal sealed class CloudBudget : IDisposable
{
    /// <summary>
    /// The whole Cloud layer, from the first credential lookup to the last answer (4 s).
    /// </summary>
    public static readonly TimeSpan Total = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Resolving the credential: settings, the keychain probe, choosing one (1 s). A token refresh is
    /// not part of it; it runs inside the first request (Amendment 1A).
    /// </summary>
    public static readonly TimeSpan Credential = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _timeProvider;
    private readonly long _started;
    private readonly CancellationTokenSource _total;
    private readonly CancellationTokenSource _linkedTotal;
    private readonly CancellationTokenSource _credential;
    private readonly CancellationTokenSource _linkedCredential;

    /// <summary>
    /// Starts the budget now.
    /// </summary>
    /// <param name="timeProvider">The clock every deadline and elapsed time is measured on.</param>
    /// <param name="cancellationToken">The caller's cancellation, linked into both tokens.</param>
    public CloudBudget(TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
        _started = timeProvider.GetTimestamp();

        // Created disarmed and armed with CancelAfter, which reuses the provider's timer: these are
        // deadlines, not waits, and a test clock that jumps to each timer as it is created
        // (ClockDriver) must not expire the budget before Cloud has answered. Polly arms its
        // attempt timeout the same way.
        _total = new CancellationTokenSource(Timeout.InfiniteTimeSpan, timeProvider);
        _total.CancelAfter(Total);
        _linkedTotal = CancellationTokenSource.CreateLinkedTokenSource(_total.Token, cancellationToken);
        _credential = new CancellationTokenSource(Timeout.InfiniteTimeSpan, timeProvider);
        _credential.CancelAfter(Credential);
        _linkedCredential = CancellationTokenSource.CreateLinkedTokenSource(_credential.Token, _linkedTotal.Token);
    }

    /// <summary>
    /// Gets the token cancelled when <see cref="Total"/> has passed or the caller cancels.
    /// </summary>
    public CancellationToken Token => _linkedTotal.Token;

    /// <summary>
    /// Gets the token for resolving the credential, cancelled after <see cref="Credential"/> as well.
    /// </summary>
    public CancellationToken CredentialToken => _linkedCredential.Token;

    /// <summary>
    /// Gets whether the credential slice ran out, as opposed to the total or the caller.
    /// </summary>
    public bool CredentialExpired => _credential.IsCancellationRequested;

    /// <summary>
    /// Gets how long the budget has run.
    /// </summary>
    public TimeSpan Elapsed => _timeProvider.GetElapsedTime(_started);

    /// <summary>
    /// Gets what is left of <see cref="Total"/>, never negative.
    /// </summary>
    public TimeSpan Remaining => Total - Elapsed is { Ticks: > 0 } left ? left : TimeSpan.Zero;

    /// <summary>
    /// Returns whether at least <paramref name="time"/> is left: something that cannot finish in
    /// what remains is skipped rather than started and cut short.
    /// </summary>
    public bool HasAtLeast(TimeSpan time) => Remaining >= time;

    public void Dispose()
    {
        _linkedCredential.Dispose();
        _credential.Dispose();
        _linkedTotal.Dispose();
        _total.Dispose();
    }
}
