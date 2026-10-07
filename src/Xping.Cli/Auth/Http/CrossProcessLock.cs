/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;

namespace Xping.Cli.Auth.Http;

/// <summary>
/// Keeps two <c>xping</c> processes from refreshing the same sign-in at once (cli-auth-cli-spec §9.4).
/// </summary>
/// <remarks>
/// <para>
/// A lock file per Cloud URL opened with <see cref="FileShare.None"/>, which also holds between
/// two handles of one process (<see cref="PrivateFiles.OpenExclusive"/>). The file is never
/// deleted; deleting it would let a third process lock a new file while the second still holds the
/// old one.
/// </para>
/// <para>
/// The lock is an optimisation, not the safety net. When it cannot be had within
/// <see cref="Patience"/>, the refresh proceeds without it: the server's 30 s reuse leeway
/// (contract §6.6) still covers a token presented by two processes.
/// </para>
/// </remarks>
internal sealed class CrossProcessLock(XpingHome home, TimeProvider timeProvider, ILogger<CrossProcessLock> logger)
{
    /// <summary>
    /// How long to wait for another process to finish its refresh.
    /// </summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How often to try again while another process holds the lock.
    /// </summary>
    public static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Takes the lock for <paramref name="cloudUrl"/>.
    /// </summary>
    /// <param name="cloudUrl">The normalized Cloud URL.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A handle that releases the lock; a no-op one when the lock could not be had.</returns>
    public async Task<IDisposable> AcquireAsync(string cloudUrl, CancellationToken cancellationToken)
    {
        string path = home.LockFile(cloudUrl);
        DateTimeOffset deadline = timeProvider.GetUtcNow() + Patience;

        try
        {
            home.EnsurePrivate();
            PrivateFiles.EnsureDirectory(Path.GetDirectoryName(path)!);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogInformation("Refreshing without the cross-process lock: its directory could not be created ({Error})", ex.GetType().Name);
            return NoLock.Instance;
        }

        while (true)
        {
            try
            {
                return PrivateFiles.OpenExclusive(path);
            }
            catch (UnauthorizedAccessException)
            {
                logger.LogInformation("Refreshing without the cross-process lock: {Path} cannot be opened", path);
                return NoLock.Instance;
            }
            catch (IOException) when (timeProvider.GetUtcNow() < deadline)
            {
                // Held by another refresh; it takes one request, so it is over soon.
            }
            catch (IOException)
            {
                logger.LogInformation("Refreshing without the cross-process lock: still held after {Seconds} s", Patience.TotalSeconds);
                return NoLock.Instance;
            }

            await Task.Delay(RetryInterval, timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class NoLock : IDisposable
    {
        public static readonly NoLock Instance = new();

        public void Dispose()
        {
        }
    }
}
