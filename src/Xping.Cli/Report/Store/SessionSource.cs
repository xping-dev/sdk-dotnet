/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Report.Store;

/// <summary>
/// Sessions read from somewhere, with a count of what could not be read.
/// </summary>
/// <param name="Sessions">The sessions, newest first.</param>
/// <param name="UnreadableCount">Files skipped because they could not be read.</param>
internal sealed record SessionReadResult(IReadOnlyList<TestSession> Sessions, int UnreadableCount)
{
    /// <summary>Gets an empty result.</summary>
    public static SessionReadResult Empty { get; } = new([], 0);
}

/// <summary>
/// Where analysis gets its sessions.
/// </summary>
/// <remarks>
/// This is the only layer of local analysis that touches a disk. Everything above it — the window,
/// the index, every provider — receives sessions it was handed, which is what makes providers
/// testable without a filesystem and keeps a provider from quietly widening its own window.
/// </remarks>
internal interface ISessionSource
{
    /// <summary>Gets a value indicating whether a store was found.</summary>
    bool IsAvailable { get; }

    /// <summary>Gets the store location, for messages that tell the user where to look.</summary>
    string? StorePath { get; }

    /// <summary>
    /// Reads the most recent sessions.
    /// </summary>
    /// <param name="maxSessions">Upper bound on how many to return.</param>
    /// <param name="assembly">Test assembly to scope to, when one was chosen.</param>
    /// <returns>The sessions and the count of unreadable files.</returns>
    SessionReadResult Read(int maxSessions, string? assembly);

    /// <summary>
    /// Lists the stored runs newest first, without loading any of them.
    /// </summary>
    /// <returns>Each run's start time and assemblies, read as the sequence is enumerated.</returns>
    IEnumerable<LocalSessionHeader> ReadHeaders();

    /// <summary>
    /// Reads every session, newest first, one at a time.
    /// </summary>
    /// <param name="assembly">Test assembly to scope to, when one was chosen.</param>
    /// <returns>One entry per file read; an unreadable file is an entry with no session.</returns>
    IEnumerable<LocalSessionFile> ReadEach(string? assembly);

    /// <summary>
    /// Returns the distinct assemblies present in recent history, ordered.
    /// </summary>
    IReadOnlyList<string> KnownAssemblies();
}

/// <summary>
/// Reads sessions from the on-disk local store.
/// </summary>
internal sealed class LocalSessionSource(ILocalSessionStore store) : ISessionSource
{
    public bool IsAvailable => store.IsAvailable;

    public string? StorePath => store.StorePath;

    public SessionReadResult Read(int maxSessions, string? assembly)
    {
        LocalSessionReadResult result = store.ReadRecent(maxSessions, assembly);
        return new SessionReadResult(result.Sessions, result.UnreadableCount);
    }

    // Both passed through unmaterialised: a caller that stops at the first match must not pay for
    // the rest, and one that reads everything must not hold it all.
    public IEnumerable<LocalSessionHeader> ReadHeaders() => store.ReadHeaders();

    public IEnumerable<LocalSessionFile> ReadEach(string? assembly) => store.ReadEach(assembly);

    /// <remarks>
    /// Read from the store's summary, which covers every file: the scope notice reports how many
    /// other assemblies the store holds, and an assembly whose runs are all older than some probe
    /// would silently drop out of that count.
    /// </remarks>
    public IReadOnlyList<string> KnownAssemblies() =>
        [.. store.Summarize().Assemblies.Select(a => a.Name)];
}
