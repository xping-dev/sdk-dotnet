/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models;

namespace Xping.Sdk.Core.Services.LocalStore;

/// <summary>
/// The result of reading sessions from the local store.
/// </summary>
/// <remarks>
/// The unreadable count travels with the sessions because a report has to state how much history it
/// could not see. Returning only the sessions would make a store that is half corrupt
/// indistinguishable from a store that is half empty, and the difference matters: the first is a
/// problem to investigate, the second is a normal new project.
/// </remarks>
public sealed class LocalSessionReadResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalSessionReadResult"/> class.
    /// </summary>
    /// <param name="sessions">The sessions that were read.</param>
    /// <param name="unreadableCount">The number of files skipped because they could not be read.</param>
    public LocalSessionReadResult(IReadOnlyList<TestSession> sessions, int unreadableCount)
    {
        Sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        UnreadableCount = unreadableCount;
    }

    /// <summary>Gets an empty result.</summary>
    public static LocalSessionReadResult Empty { get; } = new(new List<TestSession>(), 0);

    /// <summary>
    /// Gets the sessions read, ordered newest first by <see cref="TestSession.StartedAt"/> with
    /// <see cref="TestSession.SessionId"/> breaking ties.
    /// </summary>
    public IReadOnlyList<TestSession> Sessions { get; }

    /// <summary>Gets the number of files skipped because they could not be read.</summary>
    public int UnreadableCount { get; }
}

/// <summary>
/// Persists and retrieves complete test sessions on the developer's machine.
/// </summary>
/// <remarks>
/// <para>
/// This is the substrate for local analysis. It stores the whole <see cref="TestSession"/>: error
/// text, stack traces, source locations, orchestration and network data. Analysis that has to
/// explain <i>why</i> a test is unreliable cannot work from a summary projection.
/// </para>
/// <para>
/// Implementations must never throw as a result of storage problems. A read-only checkout, a full
/// disk or a locked file has to degrade to "no local history" — failing a developer's test run
/// because an observability side-channel could not write is never an acceptable trade.
/// </para>
/// </remarks>
public interface ILocalSessionStore
{
    /// <summary>
    /// Gets a value indicating whether the store resolved to a usable location.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the store root directory, or <see langword="null"/> when unavailable.
    /// </summary>
    string? StorePath { get; }

    /// <summary>
    /// Gets the directory holding session files, or <see langword="null"/> when unavailable.
    /// </summary>
    string? SessionsPath { get; }

    /// <summary>
    /// Writes a session and applies the retention policy.
    /// </summary>
    /// <param name="session">The session to persist.</param>
    /// <returns>
    /// <see langword="true"/> when the session was written; otherwise <see langword="false"/>.
    /// </returns>
    bool Write(TestSession session);

    /// <summary>
    /// Reads the most recent sessions, newest first.
    /// </summary>
    /// <param name="maxSessions">Maximum number of sessions to return.</param>
    /// <param name="assembly">
    /// When supplied, only sessions that recorded this test assembly are returned, each narrowed to
    /// that assembly's executions.
    /// </param>
    /// <returns>The sessions and the count of files that could not be read.</returns>
    /// <remarks>
    /// <para>
    /// The assembly filter matters whenever a solution has more than one test project. All of them
    /// share one store, so without it a report would mix unrelated suites: an assembly's history
    /// would contain other assemblies' tests, and its session count would be the solution-wide total.
    /// </para>
    /// <para>
    /// A session is narrowed rather than selected, because one session records a test host process
    /// and a host can run several test projects at once. A solution-wide <c>dotnet test</c> is one
    /// run of each assembly it covered, and each of them reads back carrying only its own
    /// executions. See <see cref="SessionAssemblies"/>.
    /// </para>
    /// </remarks>
    LocalSessionReadResult ReadRecent(int maxSessions, string? assembly = null);

    /// <summary>
    /// Deletes stored sessions.
    /// </summary>
    /// <param name="assembly">
    /// When supplied, only this test assembly's history is deleted; otherwise every session is.
    /// </param>
    /// <returns>The number of sessions whose history for that assembly was removed.</returns>
    /// <remarks>
    /// Deletion is scoped the same way <see cref="ReadRecent"/> is, because one session can record
    /// several test projects. A run that covered the named assembly and nothing else is deleted; a
    /// run that also covered others is stripped of it and kept, so clearing one suite's history
    /// never takes another's with it.
    /// </remarks>
    int Delete(string? assembly = null);
}
