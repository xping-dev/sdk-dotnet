/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Models;
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
    private readonly LocalStoreOptions _options;
    private readonly ILogger _logger;
    private readonly Lazy<string?> _storePath;

    /// <param name="options">Retention limits for the store.</param>
    /// <param name="logger">Receives the store's diagnostics.</param>
    /// <param name="startDirectory">
    /// Where the repository walk starts when no path is configured. Defaults to the location of
    /// the calling assembly.
    /// </param>
    /// <param name="storePath">
    /// The store root, when configured explicitly. Skips the repository walk altogether; the
    /// directory is created on first write like any resolved root.
    /// </param>
    public JsonSessionStore(
        LocalStoreOptions options, ILogger logger, string? startDirectory = null, string? storePath = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storePath = new Lazy<string?>(() =>
            string.IsNullOrWhiteSpace(storePath)
                ? LocalStorePathResolver.Resolve(startDirectory)
                : storePath);
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

            session = SessionAssemblies.WithDerivedAssemblies(session);
            string path = Path.Combine(
                directory,
                SessionFileName.Build(session.StartedAt, session.SessionId, session.Assemblies));

            WriteFile(path, session);
            StoreRetention.Apply(directory, _options, TagsOf, _logger);

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
                .GetFiles(directory, SessionFileName.SearchPattern)
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .ToList();

            var sessions = new List<TestSession>(Math.Min(maxSessions, files.Count));
            int unreadable = 0;
            string? tag = assembly == null ? null : SessionFileName.Tag(assembly);

            foreach (string file in files)
            {
                if (sessions.Count >= maxSessions)
                    break;

                // A tagged name that lacks this assembly's tag is another assembly's run, and is
                // skipped without being opened. An untagged one says nothing, so it is read.
                if (tag != null
                    && SessionFileName.ParseTags(Path.GetFileName(file)) is { } tags
                    && !tags.Contains(tag, StringComparer.Ordinal))
                {
                    continue;
                }

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

                // A run is not asked which assembly it is, but which assemblies it covers: one test
                // host can execute several test projects, and each of them owns its own slice of the
                // run. Narrowing here rather than downstream means the whole analysis pipeline —
                // window bounds, session counts, every provider — sees one assembly's history and
                // nothing else, without any of it having to know that scoping happened.
                if (assembly != null)
                {
                    TestSession? scoped = SessionAssemblies.Project(session, assembly);
                    if (scoped == null)
                        continue;

                    session = scoped;
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

    public LocalStoreSummary Summarize()
    {
        // Directory.Exists is false for a null path, so an unavailable store lands here too.
        string? directory = SessionsPath;
        if (!Directory.Exists(directory))
            return LocalStoreSummary.Empty;

        try
        {
            var runs = new Dictionary<string, (int Runs, DateTime Last)>(StringComparer.Ordinal);
            int unattributed = 0;

            // Names come from each file's head rather than its tags, which are hashes. The start time
            // comes from the name, so no session is loaded.
            // Oldest first, so the last run seen for an assembly is its newest.
            IEnumerable<string> files = Directory
                .GetFiles(directory!, SessionFileName.SearchPattern)
                .OrderBy(f => f, StringComparer.Ordinal);

            foreach (string file in files)
            {
                IReadOnlyList<string>? assemblies = SessionFileHeader.ReadAssemblies(file);

                if (assemblies is not { Count: > 0 }
                    || !SessionFileName.TryParseStartedAt(Path.GetFileName(file), out DateTime startedAt))
                {
                    unattributed++;
                    continue;
                }

                foreach (string assembly in assemblies)
                {
                    runs.TryGetValue(assembly, out var entry);
                    runs[assembly] = (entry.Runs + 1, startedAt);
                }
            }

            var summary = runs
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new LocalStoreAssembly(pair.Key, pair.Value.Runs, pair.Value.Last))
                .ToList();

            return new LocalStoreSummary(summary, unattributed);
        }
        catch (Exception ex) when (StoreRetention.IsStorageFailure(ex))
        {
            _logger.LogDebug("Local session store summary skipped: {Message}", ex.Message);
            return LocalStoreSummary.Empty;
        }
    }

    public IEnumerable<LocalSessionHeader> ReadHeaders() =>
        Headers(ListNewestFirst().Where(f => f.Dated));

    public IEnumerable<LocalSessionFile> ReadEach(string? assembly = null) =>
        Files(ListNewestFirst(), assembly);

    /// <summary>
    /// Lists the session files newest first, ordered as <see cref="CompareNewestFirst"/> orders
    /// sessions. Files whose name carries no start time come last, as ordinal order puts them.
    /// </summary>
    /// <remarks>
    /// Listed eagerly so the lazy readers built on it stay iterators: an iterator cannot yield inside
    /// a try that catches, and listing is the only step that can fail. Each file read degrades on
    /// its own.
    /// </remarks>
    private List<(DateTime StartedAt, bool Dated, string Path)> ListNewestFirst()
    {
        string? directory = SessionsPath;
        if (!Directory.Exists(directory))
            return [];

        try
        {
            var files = new List<(DateTime StartedAt, bool Dated, string Path)>();

            foreach (string file in Directory.GetFiles(directory!, SessionFileName.SearchPattern))
            {
                bool dated = SessionFileName.TryParseStartedAt(Path.GetFileName(file), out DateTime startedAt);
                files.Add((startedAt, dated, file));
            }

            // Not the reverse of the name order ReadRecent walks: that breaks same-tick ties by id
            // descending, where CompareNewestFirst sorts them ascending. The name's id is a prefix of
            // the full one, so ascending names within a tick give the same order.
            files.Sort((left, right) =>
            {
                int byTime = right.StartedAt.CompareTo(left.StartedAt);
                return byTime != 0 ? byTime : string.CompareOrdinal(left.Path, right.Path);
            });

            return files;
        }
        catch (Exception ex) when (StoreRetention.IsStorageFailure(ex))
        {
            _logger.LogDebug("Local session store listing skipped: {Message}", ex.Message);
            return [];
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

            int cleared = 0;

            foreach (string file in Directory.GetFiles(directory, SessionFileName.SearchPattern))
            {
                if (assembly == null)
                {
                    if (StoreRetention.TryDelete(new FileInfo(file), _logger))
                        cleared++;

                    continue;
                }

                // An unreadable session has no assembly to match, so a scoped delete leaves it alone.
                TestSession? session = TryReadFile(file);
                if (!SessionAssemblies.Covers(session, assembly))
                    continue;

                // A run can hold several test projects' history, because one test host can execute
                // several at once. Deleting the file would take the others with it, so the run is
                // stripped of the named assembly and written back carrying the rest. Only a run that
                // recorded nothing else is deleted outright.
                TestSession? remaining = SessionAssemblies.Excluding(session, assembly);

                bool removed = remaining == null
                    ? StoreRetention.TryDelete(new FileInfo(file), _logger)
                    : TryRewriteFile(file, remaining);

                if (removed)
                    cleared++;
            }

            return cleared;
        }
        catch (Exception ex) when (StoreRetention.IsStorageFailure(ex))
        {
            _logger.LogDebug("Local session store delete skipped: {Message}", ex.Message);
            return 0;
        }
    }

    private static IEnumerable<LocalSessionHeader> Headers(
        IEnumerable<(DateTime StartedAt, bool Dated, string Path)> files)
    {
        foreach ((DateTime startedAt, _, string path) in files)
            yield return new LocalSessionHeader(startedAt, SessionFileHeader.ReadAssemblies(path) ?? []);
    }

    /// <remarks>
    /// Skips and narrows exactly as <see cref="ReadRecent"/> does, so the two never disagree about
    /// which runs an assembly has.
    /// </remarks>
    private IEnumerable<LocalSessionFile> Files(
        List<(DateTime StartedAt, bool Dated, string Path)> files, string? assembly)
    {
        string? tag = assembly == null ? null : SessionFileName.Tag(assembly);

        foreach ((_, _, string path) in files)
        {
            if (tag != null
                && SessionFileName.ParseTags(Path.GetFileName(path)) is { } tags
                && !tags.Contains(tag, StringComparer.Ordinal))
            {
                continue;
            }

            TestSession? session = TryReadFile(path);
            if (session == null)
            {
                yield return new LocalSessionFile(null);
                continue;
            }

            if (session.Executions.Count == 0)
                continue;

            if (assembly != null)
            {
                session = SessionAssemblies.Project(session, assembly);
                if (session == null)
                    continue;
            }

            yield return new LocalSessionFile(session);
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

        // Put the file in place in one step rather than deleting the destination and moving after.
        // The two-step form leaves a window in which the old file is already gone and its
        // replacement is not yet in place, and a process killed inside that window loses the run
        // outright. Writing a new session never opened that window — the filename carries fresh
        // ticks and a fresh id, so there was nothing to delete — but rewriting one opens it every
        // time, because the destination always exists.
        //
        // Two calls rather than File.Move's overwrite overload, which netstandard2.0 does not have:
        // Replace needs the destination to exist and Move needs it not to. Both are atomic, and the
        // race between the check and the call throws IOException, which the callers already treat as
        // "the write did not happen".
        if (File.Exists(path))
            File.Replace(tempPath, path, destinationBackupFileName: null);
        else
            File.Move(tempPath, path);
    }

    /// <summary>
    /// Replaces a session file with a stripped copy of the run it holds.
    /// </summary>
    /// <remarks>
    /// Rewritten in place through <see cref="WriteFile"/>, which keeps the temp-file-then-move
    /// guarantee, then renamed to carry the tags of the assemblies it still covers. A crash between
    /// the two leaves the stripped run under its old tags, which only makes a scoped read open it and
    /// find nothing for the removed assembly: nothing is lost, and nothing wrong is read.
    /// </remarks>
    /// <returns><see langword="true"/> when the file was rewritten.</returns>
    private bool TryRewriteFile(string path, TestSession session)
    {
        try
        {
            WriteFile(path, session);

            string renamed = Path.Combine(
                Path.GetDirectoryName(path)!,
                SessionFileName.Build(session.StartedAt, session.SessionId, session.Assemblies));

            if (!string.Equals(renamed, path, StringComparison.Ordinal))
                File.Move(path, renamed);

            return true;
        }
        catch (Exception ex) when (StoreRetention.IsStorageFailure(ex))
        {
            // Same contract as every other path here: storage problems degrade to "did not happen",
            // never to an exception. The run is left whole, which is the safe direction — the caller
            // reports fewer runs cleared than it matched, and nothing is lost.
            _logger.LogDebug("Session file '{Path}' not rewritten: {Message}", path, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Returns the keys retention counts a session file under: its name's tags, or, for an untagged
    /// name, the tags of the assemblies at the head of the file.
    /// </summary>
    /// <remarks>
    /// An unreadable head covers none, and is counted with the other runs that name no assembly.
    /// </remarks>
    private static IReadOnlyList<string> TagsOf(FileInfo file) =>
        SessionFileName.ParseTags(file.Name)
        ?? (SessionFileHeader.ReadAssemblies(file.FullName) ?? []).Select(SessionFileName.Tag).ToList();

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
