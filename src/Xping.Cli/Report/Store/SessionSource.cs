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
    /// Returns the assembly of the most recently recorded session, or <see langword="null"/>.
    /// </summary>
    string? NewestAssembly();

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
    // A generous probe used only to discover which assemblies exist, never to bound analysis.
    private const int DiscoveryWindow = 200;

    public bool IsAvailable => store.IsAvailable;

    public string? StorePath => store.StorePath;

    public SessionReadResult Read(int maxSessions, string? assembly)
    {
        LocalSessionReadResult result = store.ReadRecent(maxSessions, assembly);
        return new SessionReadResult(result.Sessions, result.UnreadableCount);
    }

    public string? NewestAssembly()
    {
        LocalSessionReadResult newest = store.ReadRecent(1);
        return newest.Sessions.Count == 0 ? null : AssemblyOf(newest.Sessions[0]);
    }

    public IReadOnlyList<string> KnownAssemblies() =>
        store.ReadRecent(DiscoveryWindow).Sessions
            .Select(AssemblyOf)
            .Where(a => a is { Length: > 0 })
            .Select(a => a!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(a => a, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Returns the test assembly a session belongs to.
    /// </summary>
    /// <remarks>
    /// Taken from the first execution that names one: an execution recorded before identity
    /// generation completed carries an empty assembly, and treating that as the session's assembly
    /// would hide the whole session from a scoped report.
    /// </remarks>
    internal static string? AssemblyOf(TestSession session)
    {
        foreach (var execution in session.Executions)
        {
            if (execution.Identity.Assembly is { Length: > 0 } assembly)
                return assembly;
        }

        return null;
    }
}
