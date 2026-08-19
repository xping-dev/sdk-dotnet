/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Serialization;

namespace Xping.Sdk.Core.Services.LocalStore.Internals;

/// <summary>
/// Stores each session as its own gzipped JSON document holding one whole <see cref="TestSession"/>.
/// </summary>
/// <remarks>
/// <para>
/// One file per session is what makes the design cheap: concurrent test assemblies write disjoint
/// files so there is no locking anywhere, retention is file deletion, and the filename carries the
/// sort key so no shared index is needed.
/// </para>
/// <para>
/// A session is a single JSON document rather than JSON Lines, which is what makes a truncated file
/// <i>detectable</i>: a partial document fails to parse and is reported as unreadable, where a
/// partial JSON Lines file parses cleanly and silently under-reports its executions. Analysis that
/// counts executions cannot tolerate the second failure mode.
/// </para>
/// </remarks>
internal sealed class JsonSessionStore : ILocalSessionStore
{
    private const string FilePrefix = "session-";
    private const string FileSuffix = ".json.gz";
    private const string SearchPattern = FilePrefix + "*" + FileSuffix;

    private readonly LocalStoreOptions _options;
    private readonly ILogger _logger;
    private readonly Lazy<string?> _storePath;

    public JsonSessionStore(LocalStoreOptions options, ILogger logger, string? startDirectory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storePath = new Lazy<string?>(() => LocalStorePathResolver.Resolve(startDirectory));
    }

    public bool IsAvailable => _storePath.Value != null;

    public string? StorePath => _storePath.Value;

    public string? SessionsPath =>
        _storePath.Value is { } root ? LocalStorePathResolver.GetSessionsDirectory(root) : null;

    public bool Write(TestSession session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        string? root = _storePath.Value;
        if (root == null)
            return false;

        try
        {
            LocalStorePathResolver.EnsureCreated(root);

            string directory = LocalStorePathResolver.GetSessionsDirectory(root);
            string path = Path.Combine(directory, BuildFileName(session));

            WriteFile(path, session);
            StoreRetention.Apply(directory, SearchPattern, _options, _logger);

            return true;
        }
        catch (Exception ex) when (StoreRetention.IsStorageFailure(ex))
        {
            // Never surface storage problems to the test run.
            _logger.LogDebug("Local session store write skipped: {Message}", ex.Message);
            return false;
        }
    }

    public LocalSessionReadResult ReadRecent(int maxSessions, string? assembly = null)
    {
        if (maxSessions <= 0)
            return LocalSessionReadResult.Empty;

        string? root = _storePath.Value;
        if (root == null)
            return LocalSessionReadResult.Empty;

        try
        {
            string directory = LocalStorePathResolver.GetSessionsDirectory(root);
            if (!Directory.Exists(directory))
                return LocalSessionReadResult.Empty;

            // Filenames are timestamp-prefixed, so ordinal ordering is chronological ordering. Walk
            // newest-first and stop once enough matching sessions are found, so a store shared by
            // several test projects does not force reading every file.
            var files = Directory
                .GetFiles(directory, SearchPattern)
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .ToList();

            var sessions = new List<TestSession>(Math.Min(maxSessions, files.Count));
            int unreadable = 0;

            foreach (string file in files)
            {
                if (sessions.Count >= maxSessions)
                    break;

                TestSession? session = TryReadFile(file);
                if (session == null)
                {
                    // Only files we actually attempted are counted. Files beyond the window were
                    // never opened, so claiming them as unreadable would overstate the damage.
                    unreadable++;
                    continue;
                }

                // A session with no executions carries no analysable history. It is not corrupt —
                // counting it as unreadable would report damage that did not happen — but letting it
                // occupy a window slot would dilute every rate computed against the session count.
                if (session.Executions.Count == 0)
                    continue;

                if (assembly != null &&
                    !string.Equals(GetAssembly(session), assembly, StringComparison.Ordinal))
                {
                    continue;
                }

                sessions.Add(session);
            }

            // Filename order is close to the required order but not identical: it breaks ties by
            // session id descending, where §2.3 requires ascending. Sort explicitly rather than lean
            // on the filename, so the ordering contract holds no matter how files came to be named.
            sessions.Sort(CompareNewestFirst);

            return new LocalSessionReadResult(sessions, unreadable);
        }
        catch (Exception ex) when (StoreRetention.IsStorageFailure(ex))
        {
            _logger.LogDebug("Local session store read skipped: {Message}", ex.Message);
            return LocalSessionReadResult.Empty;
        }
    }

    public int Delete(string? assembly = null)
    {
        string? root = _storePath.Value;
        if (root == null)
            return 0;

        try
        {
            string directory = LocalStorePathResolver.GetSessionsDirectory(root);
            if (!Directory.Exists(directory))
                return 0;

            var targets = new List<string>();

            foreach (string file in Directory.GetFiles(directory, SearchPattern))
            {
                if (assembly == null)
                {
                    targets.Add(file);
                    continue;
                }

                TestSession? session = TryReadFile(file);

                // An unreadable session has no assembly to match, so a scoped delete leaves it alone.
                if (session != null &&
                    string.Equals(GetAssembly(session), assembly, StringComparison.Ordinal))
                {
                    targets.Add(file);
                }
            }

            int deleted = 0;
            foreach (string file in targets)
            {
                if (StoreRetention.TryDelete(new FileInfo(file), _logger))
                    deleted++;
            }

            return deleted;
        }
        catch (Exception ex) when (StoreRetention.IsStorageFailure(ex))
        {
            _logger.LogDebug("Local session store delete skipped: {Message}", ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// Orders sessions newest first, by start time with session id breaking ties.
    /// </summary>
    /// <remarks>
    /// The tiebreak is what makes the order <i>total</i>: two assemblies in one solution routinely
    /// start within the same tick, and analysis output has to be byte-identical across runs.
    /// </remarks>
    internal static int CompareNewestFirst(TestSession left, TestSession right)
    {
        int byTime = right.StartedAt.CompareTo(left.StartedAt);
        if (byTime != 0)
            return byTime;

        return string.CompareOrdinal(
            left.SessionId.ToString("N"), right.SessionId.ToString("N"));
    }

    /// <summary>
    /// Returns the test assembly a session belongs to, or <see langword="null"/> when unknown.
    /// </summary>
    /// <remarks>
    /// Taken from the first execution that names one rather than from the first execution outright:
    /// an execution recorded before identity generation completed carries an empty assembly, and
    /// treating that as the session's assembly would hide the whole session from a scoped report.
    /// </remarks>
    private static string? GetAssembly(TestSession session)
    {
        foreach (TestExecution execution in session.Executions)
        {
            string candidate = execution.Identity.Assembly;
            if (!string.IsNullOrEmpty(candidate))
                return candidate;
        }

        return null;
    }

    private static string BuildFileName(TestSession session)
    {
        string ticks = session.StartedAt.Ticks.ToString("D19", CultureInfo.InvariantCulture);
        string id = session.SessionId.ToString("N");

        string suffix = id.Length >= 8
            ? id.Substring(0, 8)
            : Guid.NewGuid().ToString("N").Substring(0, 8);

        return FilePrefix + ticks + "-" + suffix + FileSuffix;
    }

    private static void WriteFile(string path, TestSession session)
    {
        JsonSerializerOptions options = XpingSerializerOptions.FileOptions;

        // Write to a temporary file and move into place, so a crash mid-write cannot leave a
        // half-written file under a name the reader will trust.
        string tempPath = path + ".tmp";

        using (var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var gzip = new GZipStream(file, CompressionLevel.Optimal))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.Write(JsonSerializer.Serialize(session, options));
        }

        if (File.Exists(path))
            File.Delete(path);

        File.Move(tempPath, path);
    }

    private TestSession? TryReadFile(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);

            string content = reader.ReadToEnd();
            if (content.Length == 0)
                return null;

            return JsonSerializer.Deserialize<TestSession>(
                content, XpingSerializerOptions.FileOptions);
        }
        // JsonException covers a truncated or malformed document, InvalidDataException a corrupt
        // gzip container. Without these a single damaged file would abort the whole read instead of
        // costing one session, which is the opposite of what the store contract promises.
        catch (Exception ex) when (
            StoreRetention.IsStorageFailure(ex) || ex is InvalidDataException or JsonException)
        {
            _logger.LogDebug("Skipping unreadable session file '{Path}': {Message}", path, ex.Message);
            return null;
        }
    }
}
