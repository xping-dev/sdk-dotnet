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
    /// Returns the assembly to scope to when the caller named none, or <see langword="null"/>.
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

    /// <summary>
    /// Returns the assembly to scope to when the caller named none.
    /// </summary>
    /// <remarks>
    /// The newest run can cover several assemblies at once, and a report covers one. Taking the
    /// first in ordinal order makes the choice deterministic and repeatable, which matters more than
    /// which one wins: it is not a silent choice, because the report names the assembly it settled
    /// on and counts the ones it left out.
    /// </remarks>
    public string? NewestAssembly()
    {
        LocalSessionReadResult newest = store.ReadRecent(1);

        if (newest.Sessions.Count == 0)
            return null;

        IReadOnlyList<string> assemblies = SessionAssemblies.Of(newest.Sessions[0]);
        return assemblies.Count == 0 ? null : assemblies[0];
    }

    /// <remarks>
    /// Flattened across each session rather than one name per session: a solution-wide run records
    /// every test project it executed, and counting only the first would undercount what the store
    /// holds — which is the number the scope notice reports.
    /// </remarks>
    public IReadOnlyList<string> KnownAssemblies() =>
        store.ReadRecent(DiscoveryWindow).Sessions
            .SelectMany(SessionAssemblies.Of)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(a => a, StringComparer.Ordinal)
            .ToList();
}
