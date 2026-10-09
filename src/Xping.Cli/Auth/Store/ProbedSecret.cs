/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Auth.Store;

/// <summary>
/// Holds what a keychain probe read, so the first read of the same entry does not ask the
/// keychain again.
/// </summary>
/// <remarks>
/// The probe has to read the secret itself: a locked macOS keychain lists items but refuses their
/// content. Reading it twice meant two "xping wants to use your confidential information" dialogs
/// after an upgrade for a user who answers "Allow", and two D-Bus round trips with libsecret.
/// The secret is handed out once and cleared on any change, so it cannot go stale.
/// </remarks>
internal sealed class ProbedSecret
{
    private Entry? _entry;

    /// <summary>
    /// Keeps what the probe found for <paramref name="cloudUrl"/>: the secret, or
    /// <see langword="null"/> when there was no entry.
    /// </summary>
    public void Keep(string cloudUrl, byte[]? secret) => Clear(Interlocked.Exchange(ref _entry, new Entry(cloudUrl, secret)));

    /// <summary>
    /// Takes what the probe found for <paramref name="cloudUrl"/>, once.
    /// </summary>
    /// <returns>Whether the probe read that entry; <paramref name="secret"/> is then its secret, or <see langword="null"/>.</returns>
    public bool TryTake(string cloudUrl, out byte[]? secret)
    {
        Entry? entry = Interlocked.Exchange(ref _entry, null);
        if (entry is not null && string.Equals(entry.CloudUrl, cloudUrl, StringComparison.Ordinal))
        {
            secret = entry.Secret;
            return true;
        }

        Clear(entry);
        secret = null;
        return false;
    }

    /// <summary>
    /// Drops what the probe found; called on every write and delete.
    /// </summary>
    public void Forget() => Clear(Interlocked.Exchange(ref _entry, null));

    private static void Clear(Entry? entry)
    {
        if (entry?.Secret is { } secret)
            Array.Clear(secret);
    }

    private sealed record Entry(string CloudUrl, byte[]? Secret);
}
