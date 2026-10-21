/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;

namespace Xping.Sdk.Core.Services.LocalStore.Internals;

/// <summary>
/// Applies the age, count and size limits to a directory of session files.
/// </summary>
/// <remarks>
/// Age and size are limits on the store, which is one directory on one disk. The count is a limit on
/// each test assembly's history: several test projects write to one store, and a store-wide count
/// would let the busiest of them push every other project's history out.
/// </remarks>
internal static class StoreRetention
{
    /// <summary>
    /// Deletes files, oldest first, until the age, count and size limits all hold.
    /// </summary>
    /// <param name="directory">The directory to prune.</param>
    /// <param name="searchPattern">Pattern selecting the session files.</param>
    /// <param name="options">The limits to enforce.</param>
    /// <param name="assembliesOf">
    /// Returns the assemblies a file covers. Only called when the store holds more files than
    /// <see cref="LocalStoreOptions.MaxRuns"/>, so a store below the limit is never opened.
    /// </param>
    /// <param name="logger">Diagnostics sink; deletion failures are logged at debug level.</param>
    /// <remarks>
    /// Filenames are timestamp-prefixed, so ordinal name order is chronological order and no file
    /// timestamps need to be read to decide the deletion sequence.
    /// </remarks>
    public static void Apply(
        string directory,
        string searchPattern,
        LocalStoreOptions options,
        Func<string, IReadOnlyList<string>> assembliesOf,
        ILogger logger)
    {
        var files = Directory
            .GetFiles(directory, searchPattern)
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
            return;

        // The newest file is never a deletion candidate. Every limit is evaluated after the file has
        // been written, so a short MaxAge, or a single file larger than MaxBytes, would otherwise
        // delete the thing that was just recorded and leave the store empty. Retention exists to
        // bound history, not to discard the thing it was called to keep.
        FileInfo newest = files[files.Count - 1];
        var survivors = new List<FileInfo>(files.Count);

        DateTime cutoff = DateTime.UtcNow - options.MaxAge;

        foreach (FileInfo file in files)
        {
            bool tooOld = file != newest && SafeLastWriteUtc(file) < cutoff;
            if (!tooOld || !TryDelete(file, logger))
                survivors.Add(file);
        }

        if (survivors.Count > options.MaxRuns)
            survivors = ApplyCount(survivors, options.MaxRuns, assembliesOf, logger);

        long remainingBytes = survivors.Sum(SafeLength);

        foreach (FileInfo file in survivors)
        {
            if (remainingBytes <= options.MaxBytes || file == newest)
                break;

            long length = SafeLength(file);
            if (TryDelete(file, logger))
                remainingBytes -= length;
        }
    }

    /// <summary>
    /// Keeps the newest <paramref name="maxRuns"/> files of every assembly and deletes the rest.
    /// </summary>
    /// <param name="files">Files oldest first; the last one is the newest and always kept.</param>
    /// <param name="maxRuns">Runs to keep for each assembly.</param>
    /// <param name="assembliesOf">Returns the assemblies a file covers.</param>
    /// <param name="logger">Diagnostics sink.</param>
    /// <returns>The files still on disk, oldest first.</returns>
    /// <remarks>
    /// A run covering several assemblies counts towards each of them and is kept while any of them
    /// still needs it, so deleting it can never shorten an assembly's history below the limit. A
    /// file naming no assembly — unreadable, or recorded before identities resolved — is counted
    /// under a bucket of its own, so it still ages out of the store instead of living forever.
    /// </remarks>
    private static List<FileInfo> ApplyCount(
        List<FileInfo> files,
        int maxRuns,
        Func<string, IReadOnlyList<string>> assembliesOf,
        ILogger logger)
    {
        IReadOnlyList<string> unattributed = [string.Empty];
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var kept = new List<FileInfo>(files.Count);

        for (int i = files.Count - 1; i >= 0; i--)
        {
            FileInfo file = files[i];
            IReadOnlyList<string> assemblies = assembliesOf(file.FullName);
            if (assemblies.Count == 0)
                assemblies = unattributed;

            bool isNewest = i == files.Count - 1;
            bool needed = isNewest || assemblies.Any(a => !seen.TryGetValue(a, out int count) || count < maxRuns);

            if (needed || !TryDelete(file, logger))
            {
                kept.Add(file);

                foreach (string assembly in assemblies)
                    seen[assembly] = seen.TryGetValue(assembly, out int count) ? count + 1 : 1;
            }
        }

        kept.Reverse();
        return kept;
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
