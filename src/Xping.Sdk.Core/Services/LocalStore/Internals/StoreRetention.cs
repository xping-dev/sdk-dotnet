/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;

namespace Xping.Sdk.Core.Services.LocalStore.Internals;

/// <summary>
/// Applies the count, size and age limits to a directory of store files.
/// </summary>
/// <remarks>
/// Shared by every store tier. The two tiers hold the same runs at very different fidelities, so
/// they prune independently — but the <i>rules</i> must not diverge, because a subtle difference
/// (which limit wins, whether the newest file is a candidate) would show up as one tier silently
/// holding more history than the other.
/// </remarks>
internal static class StoreRetention
{
    /// <summary>
    /// Deletes files, oldest first, until the count, size and age limits all hold.
    /// </summary>
    /// <param name="directory">The directory to prune.</param>
    /// <param name="searchPattern">Pattern selecting the tier's files.</param>
    /// <param name="options">The limits to enforce.</param>
    /// <param name="logger">Diagnostics sink; deletion failures are logged at debug level.</param>
    /// <remarks>
    /// Filenames are timestamp-prefixed, so ordinal name order is chronological order and no file
    /// timestamps need to be read to decide the deletion sequence.
    /// </remarks>
    public static void Apply(
        string directory, string searchPattern, LocalStoreOptions options, ILogger logger)
    {
        var files = Directory
            .GetFiles(directory, searchPattern)
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();

        DateTime cutoff = DateTime.UtcNow - options.MaxAge;
        long remainingBytes = files.Sum(SafeLength);
        int remainingCount = files.Count;

        // The newest file is never a deletion candidate. Every limit is evaluated after the file has
        // been written, so a short MaxAge, or a single file larger than MaxBytes, would otherwise
        // delete the thing that was just recorded and leave the store empty. Retention exists to
        // bound history, not to discard the thing it was called to keep.
        foreach (FileInfo file in files.Take(files.Count - 1))
        {
            bool overCount = remainingCount > options.MaxRuns;
            bool overBytes = remainingBytes > options.MaxBytes;
            bool tooOld = SafeLastWriteUtc(file) < cutoff;

            // Files are ordered oldest first, so once the oldest survivor is within every limit,
            // everything newer is too.
            if (!overCount && !overBytes && !tooOld)
                break;

            long length = SafeLength(file);
            if (TryDelete(file, logger))
            {
                remainingCount--;
                remainingBytes -= length;
            }
        }
    }

    /// <summary>
    /// Deletes a single file, reporting whether it went away.
    /// </summary>
    /// <param name="file">The file to delete.</param>
    /// <param name="logger">Diagnostics sink.</param>
    /// <returns><see langword="true"/> when the file was deleted.</returns>
    public static bool TryDelete(FileInfo file, ILogger logger)
    {
        try
        {
            file.Delete();
            return true;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            // Another test host may be pruning the same file concurrently, which is expected.
            logger.LogDebug("Retention could not delete '{Path}': {Message}", file.FullName, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Classifies the exceptions a store must swallow rather than propagate.
    /// </summary>
    /// <param name="ex">The exception to classify.</param>
    /// <returns><see langword="true"/> when the failure is a storage problem.</returns>
    /// <remarks>
    /// A read-only checkout, a full disk or a locked file has to degrade to "no local history".
    /// Failing a developer's test run because an observability side-channel could not write is never
    /// an acceptable trade.
    /// </remarks>
    public static bool IsStorageFailure(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException
            or NotSupportedException
            or ArgumentException;

    private static long SafeLength(FileInfo file)
    {
        try
        {
            return file.Exists ? file.Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static DateTime SafeLastWriteUtc(FileInfo file)
    {
        try
        {
            return file.LastWriteTimeUtc;
        }
        catch (IOException)
        {
            // Unreadable timestamp must not make the file look infinitely old and get deleted.
            return DateTime.UtcNow;
        }
    }
}
