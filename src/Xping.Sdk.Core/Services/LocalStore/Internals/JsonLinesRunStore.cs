/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Models.Local;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Sdk.Core.Services.LocalStore.Internals;

/// <summary>
/// Stores each run as its own gzipped JSON Lines file.
/// </summary>
/// <remarks>
/// <para>
/// One file per run is what makes the whole design cheap. Concurrent test assemblies in a single
/// <c>dotnet test</c> solution run write disjoint files, so there is no locking anywhere. A run
/// truncated by a killed test host costs exactly that run — the reader drops the incomplete trailing
/// line and every other run stays intact. Retention is file deletion.
/// </para>
/// <para>
/// The filename carries the sort key, so no shared index or manifest is needed. That removes the one
/// piece of mutable cross-process state such a store would otherwise require.
/// </para>
/// </remarks>
internal sealed class JsonLinesRunStore : ILocalRunStore
{
    private const string FilePrefix = "run-";
    private const string FileSuffix = ".jsonl.gz";

    private readonly LocalStoreOptions _options;
    private readonly ILogger _logger;
    private readonly Lazy<string?> _storePath;

    public JsonLinesRunStore(LocalStoreOptions options, ILogger logger, string? startDirectory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storePath = new Lazy<string?>(() => LocalStorePathResolver.Resolve(startDirectory));
    }

    public bool IsAvailable => _storePath.Value != null;

    public string? StorePath => _storePath.Value;

    public string? RunsPath =>
        _storePath.Value is { } root ? LocalStorePathResolver.GetRunsDirectory(root) : null;

    public int Delete(string? assembly = null)
    {
        string? root = _storePath.Value;
        if (root == null)
            return 0;

        try
        {
            string runsDirectory = LocalStorePathResolver.GetRunsDirectory(root);
            if (!Directory.Exists(runsDirectory))
                return 0;

            var targets = new List<string>();

            foreach (string file in Directory.GetFiles(runsDirectory, FilePrefix + "*" + FileSuffix))
            {
                if (assembly == null)
                {
                    targets.Add(file);
                    continue;
                }

                // Scoping by assembly means reading each header; the alternative would be encoding
                // the assembly into the filename, which would break older stores.
                LocalRun? run = TryReadFile(file);

                // An unreadable run has no assembly to match, so a scoped delete leaves it alone.
                if (run != null &&
                    string.Equals(run.Header.Assembly, assembly, StringComparison.Ordinal))
                {
                    targets.Add(file);
                }
            }

            int deleted = 0;
            foreach (string file in targets)
            {
                if (TryDelete(new FileInfo(file)))
                    deleted++;
            }

            return deleted;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            _logger.LogDebug("Local run store delete skipped: {Message}", ex.Message);
            return 0;
        }
    }

    public bool Write(LocalRun run)
    {
        if (run == null)
            throw new ArgumentNullException(nameof(run));

        string? root = _storePath.Value;
        if (root == null)
            return false;

        try
        {
            LocalStorePathResolver.EnsureCreated(root);

            string path = Path.Combine(
                LocalStorePathResolver.GetRunsDirectory(root),
                BuildFileName(run.Header));

            WriteFile(path, run);
            ApplyRetention(LocalStorePathResolver.GetRunsDirectory(root));

            return true;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            // Never surface storage problems to the test run.
            _logger.LogDebug("Local run store write skipped: {Message}", ex.Message);
            return false;
        }
    }

    public IReadOnlyList<LocalRun> ReadRecent(int maxRuns, string? assembly = null)
    {
        if (maxRuns <= 0)
            return [];

        string? root = _storePath.Value;
        if (root == null)
            return [];

        try
        {
            string runsDirectory = LocalStorePathResolver.GetRunsDirectory(root);
            if (!Directory.Exists(runsDirectory))
                return [];

            // Filenames are timestamp-prefixed, so ordinal ordering is chronological ordering.
            // Walk newest-first and stop once enough matching runs are found, so a store shared by
            // several test projects does not force reading every file.
            var files = Directory
                .GetFiles(runsDirectory, FilePrefix + "*" + FileSuffix)
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .ToList();

            var runs = new List<LocalRun>(Math.Min(maxRuns, files.Count));
            foreach (string file in files)
            {
                if (runs.Count >= maxRuns)
                    break;

                LocalRun? run = TryReadFile(file);
                if (run == null)
                    continue;

                if (assembly != null &&
                    !string.Equals(run.Header.Assembly, assembly, StringComparison.Ordinal))
                {
                    continue;
                }

                runs.Add(run);
            }

            // Collected newest-first; the caller and the sparkline both read oldest-first.
            runs.Reverse();
            return runs;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            _logger.LogDebug("Local run store read skipped: {Message}", ex.Message);
            return [];
        }
    }

    private static string BuildFileName(LocalRunHeader header)
    {
        string ticks = header.StartedAtUtc.Ticks.ToString("D19", CultureInfo.InvariantCulture);

        string suffix = header.SessionId.Length >= 8
            ? header.SessionId.Substring(0, 8)
            : Guid.NewGuid().ToString("N").Substring(0, 8);

        return FilePrefix + ticks + "-" + suffix + FileSuffix;
    }

    private static void WriteFile(string path, LocalRun run)
    {
        JsonSerializerOptions options = XpingSerializerOptions.FileOptions;

        // Write to a temporary file and move into place, so a crash mid-write cannot leave a
        // half-written file under a name the reader will trust.
        string tempPath = path + ".tmp";

        using (var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.WriteLine(JsonSerializer.Serialize(run.Header, options));

            foreach (LocalTestRecord record in run.Records)
                writer.WriteLine(JsonSerializer.Serialize(record, options));
        }

        if (File.Exists(path))
            File.Delete(path);

        File.Move(tempPath, path);
    }

    private LocalRun? TryReadFile(string path)
    {
        try
        {
            JsonSerializerOptions options = XpingSerializerOptions.FileOptions;

            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);

            string? headerLine = reader.ReadLine();
            if (headerLine == null)
                return null;

            LocalRunHeader? header = JsonSerializer.Deserialize<LocalRunHeader>(headerLine, options);
            if (header == null)
                return null;

            // Forward compatibility: a newer SDK may have written a schema this build cannot read.
            // Skipping the file costs some history; failing would break the report entirely.
            if (header.Version > LocalRunHeader.CurrentVersion)
            {
                _logger.LogDebug(
                    "Skipping run file with unsupported schema version {Version}.", header.Version);
                return null;
            }

            var records = new List<LocalTestRecord>();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0)
                    continue;

                LocalTestRecord? record = TryDeserializeRecord(line, options);

                // A truncated trailing line is the expected result of a killed test host. Stop at the
                // first unreadable record and keep everything before it.
                if (record == null)
                    break;

                records.Add(record);
            }

            return new LocalRun(header, records);
        }
        // JsonException covers a malformed header. Record deserialization is guarded separately,
        // but without this a single corrupt header would abort the whole read instead of costing
        // one run, which is the opposite of what the store contract promises.
        catch (Exception ex) when (
            IsStorageFailure(ex) || ex is InvalidDataException or System.Text.Json.JsonException)
        {
            _logger.LogDebug("Skipping unreadable run file '{Path}': {Message}", path, ex.Message);
            return null;
        }
    }

    private static LocalTestRecord? TryDeserializeRecord(string line, JsonSerializerOptions options)
    {
        try
        {
            return JsonSerializer.Deserialize<LocalTestRecord>(line, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Deletes runs, oldest first, until the count, size and age limits all hold.
    /// </summary>
    private void ApplyRetention(string runsDirectory)
    {
        // Oldest first: filenames are timestamp-prefixed, so ordinal order is chronological.
        var files = Directory
            .GetFiles(runsDirectory, FilePrefix + "*" + FileSuffix)
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();

        DateTime cutoff = DateTime.UtcNow - _options.MaxAge;
        long remainingBytes = files.Sum(SafeLength);
        int remainingCount = files.Count;

        // The newest run is never a deletion candidate. Every limit is evaluated after the run has
        // been written, so a short MaxAge, or a single run larger than MaxBytes, would otherwise
        // delete the run that was just recorded and leave the store empty. Retention exists to bound
        // history, not to discard the thing it was called to keep.
        foreach (FileInfo file in files.Take(files.Count - 1))
        {
            bool overCount = remainingCount > _options.MaxRuns;
            bool overBytes = remainingBytes > _options.MaxBytes;
            bool tooOld = SafeLastWriteUtc(file) < cutoff;

            // Files are ordered oldest first, so once the oldest survivor is within every limit,
            // everything newer is too.
            if (!overCount && !overBytes && !tooOld)
                break;

            long length = SafeLength(file);
            if (TryDelete(file))
            {
                remainingCount--;
                remainingBytes -= length;
            }
        }
    }

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

    private bool TryDelete(FileInfo file)
    {
        try
        {
            file.Delete();
            return true;
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            // Another test host may be pruning the same file concurrently, which is expected.
            _logger.LogDebug("Retention could not delete '{Path}': {Message}", file.FullName, ex.Message);
            return false;
        }
    }

    private static bool IsStorageFailure(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException
            or NotSupportedException
            or ArgumentException;
}
