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
/// <para>
/// Three passes, each over what the last one left:
/// </para>
/// <list type="number">
/// <item><description>Age: runs older than <see cref="LocalStoreOptions.MaxAge"/> go.</description></item>
/// <item><description>
/// Count: each assembly keeps its newest <see cref="LocalStoreOptions.MaxRuns"/> runs. Counted per
/// assembly because several test projects share one store, and a store-wide count would let the
/// busiest of them push every other project's history out.
/// </description></item>
/// <item><description>
/// Size: oldest first until the store fits <see cref="LocalStoreOptions.MaxBytes"/>, taking runs
/// outside every assembly's default report window before any inside one, so one large suite cannot
/// empty another's report.
/// </description></item>
/// </list>
/// <para>
/// The newest file is never deleted. Every limit is evaluated after it has been written, so a short
/// MaxAge, or a single file larger than MaxBytes, would otherwise delete the thing that was just
/// recorded and leave the store empty.
/// </para>
/// </remarks>
internal static class StoreRetention
{
    /// <summary>
    /// Deletes files until the age, count and size limits all hold.
    /// </summary>
    /// <param name="directory">The directory to prune.</param>
    /// <param name="options">The limits to enforce.</param>
    /// <param name="assembliesOf">
    /// Returns the keys a file is counted under, one per assembly it covers, or none when it names no
    /// assembly. Any stable key works; the store passes file name tags.
    /// </param>
    /// <param name="logger">Diagnostics sink; deletion failures are logged at debug level.</param>
    /// <remarks>
    /// Filenames are timestamp-prefixed, so ordinal name order is chronological order and no file
    /// timestamps need to be read to decide the deletion sequence.
    /// </remarks>
    public static void Apply(
        string directory,
        LocalStoreOptions options,
        Func<FileInfo, IReadOnlyList<string>> assembliesOf,
        ILogger logger)
    {
        var files = Directory
            .GetFiles(directory, SessionFileName.SearchPattern)
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
            return;

        FileInfo newest = files[files.Count - 1];
        DateTime cutoff = DateTime.UtcNow - options.MaxAge;

        var survivors = new List<FileInfo>(files.Count);
        foreach (FileInfo file in files)
        {
            bool tooOld = file != newest && SafeLastWriteUtc(file) < cutoff;
            if (!tooOld || !TryDelete(file, logger))
                survivors.Add(file);
        }

        List<Ranked> ranked = ApplyCount(survivors, options.MaxRuns, assembliesOf, logger);

        long remainingBytes = ranked.Sum(r => SafeLength(r.File));

        // Runs outside every window first, then — only if the store is still over — the windows
        // themselves, so MaxBytes stays a real limit even when the windows alone exceed it.
        foreach (bool inWindow in new[] { false, true })
        {
            foreach (Ranked run in ranked)
            {
                if (remainingBytes <= options.MaxBytes)
                    return;

                if (run.InWindow != inWindow || run.File == newest)
                    continue;

                long length = SafeLength(run.File);
                if (TryDelete(run.File, logger))
                    remainingBytes -= length;
            }
        }
    }

    /// <summary>
    /// A file that survived the count, and whether it is in some assembly's default report window.
    /// </summary>
    private readonly record struct Ranked(FileInfo File, bool InWindow);

    /// <summary>
    /// Keeps the newest <paramref name="maxRuns"/> files of every assembly and deletes the rest.
    /// </summary>
    /// <param name="files">Files oldest first; the last one is the newest and always kept.</param>
    /// <param name="maxRuns">Runs to keep for each assembly.</param>
    /// <param name="assembliesOf">Returns the keys a file is counted under.</param>
    /// <param name="logger">Diagnostics sink.</param>
    /// <returns>The files still on disk, oldest first.</returns>
    /// <remarks>
    /// A run covering several assemblies counts towards each of them and is kept while any of them
    /// still needs it, so deleting it can never shorten an assembly's history below the limit. A
    /// file naming no assembly is counted under a key of its own, so it still leaves the store
    /// instead of living forever.
    /// </remarks>
    private static List<Ranked> ApplyCount(
        List<FileInfo> files,
        int maxRuns,
        Func<FileInfo, IReadOnlyList<string>> assembliesOf,
        ILogger logger)
    {
        IReadOnlyList<string> unattributed = [string.Empty];
        int windowRuns = Math.Min(maxRuns, ReportWindow.DefaultRuns);

        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var kept = new List<Ranked>(files.Count);

        for (int i = files.Count - 1; i >= 0; i--)
        {
            FileInfo file = files[i];
            IReadOnlyList<string> keys = assembliesOf(file);
            if (keys.Count == 0)
                keys = unattributed;

            bool isNewest = i == files.Count - 1;
            bool needed = isNewest || keys.Any(k => SeenCount(seen, k) < maxRuns);

            if (!needed && TryDelete(file, logger))
                continue;

            kept.Add(new Ranked(file, isNewest || keys.Any(k => SeenCount(seen, k) < windowRuns)));

            foreach (string key in keys)
                seen[key] = SeenCount(seen, key) + 1;
        }

        kept.Reverse();
        return kept;
    }

    private static int SeenCount(Dictionary<string, int> seen, string key) =>
        seen.TryGetValue(key, out int count) ? count : 0;

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
