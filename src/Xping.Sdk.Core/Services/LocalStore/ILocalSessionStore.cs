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
/// <para>
/// The unreadable files travel with the sessions because a report has to state how much history it
/// could not see. Returning only the sessions would make a store that is half corrupt
/// indistinguishable from a store that is half empty, and the difference matters: the first is a
/// problem to investigate, the second is a normal new project.
/// </para>
/// <para>
/// Named rather than counted, so a caller combining several scoped reads can count a file once. A
/// file that names no assembly in its name is opened by every scoped read, and summing their counts
/// would report one damaged file as several.
/// </para>
/// </remarks>
public sealed class LocalSessionReadResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalSessionReadResult"/> class.
    /// </summary>
    /// <param name="sessions">The sessions that were read.</param>
    /// <param name="unreadableFiles">The names of files skipped because they could not be read.</param>
    public LocalSessionReadResult(IReadOnlyList<TestSession> sessions, IReadOnlyList<string> unreadableFiles)
    {
        Sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        UnreadableFiles = unreadableFiles ?? throw new ArgumentNullException(nameof(unreadableFiles));
    }

    /// <summary>Gets an empty result.</summary>
    public static LocalSessionReadResult Empty { get; } = new(new List<TestSession>(), new List<string>());

    /// <summary>
    /// Gets the sessions read, ordered newest first by <see cref="TestSession.StartedAt"/> with
    /// <see cref="TestSession.SessionId"/> breaking ties.
    /// </summary>
    public IReadOnlyList<TestSession> Sessions { get; }

    /// <summary>Gets the names of the files skipped because they could not be read.</summary>
    public IReadOnlyList<string> UnreadableFiles { get; }

    /// <summary>Gets the number of files skipped because they could not be read.</summary>
    public int UnreadableCount => UnreadableFiles.Count;
}

/// <summary>
/// One stored run as its file's name and header describe it, without the run itself.
/// </summary>
public sealed class LocalSessionHeader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalSessionHeader"/> class.
    /// </summary>
    /// <param name="startedAt">When the run started, in UTC.</param>
    /// <param name="assemblies">The assemblies the run covered, ordered by name.</param>
    public LocalSessionHeader(DateTime startedAt, IReadOnlyList<string> assemblies)
    {
        StartedAt = startedAt;
        Assemblies = assemblies ?? throw new ArgumentNullException(nameof(assemblies));
    }

    /// <summary>Gets when the run started, in UTC.</summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// Gets the assemblies the run covered, ordered by name. Empty when it names none or its header
    /// could not be read.
    /// </summary>
    public IReadOnlyList<string> Assemblies { get; }
}

/// <summary>
/// One test assembly's history in the local store.
/// </summary>
public sealed class LocalStoreAssembly
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalStoreAssembly"/> class.
    /// </summary>
    /// <param name="name">The assembly name.</param>
    /// <param name="runs">The number of stored runs that covered it.</param>
    /// <param name="lastStartedAt">When the newest of those runs started, in UTC.</param>
    public LocalStoreAssembly(string name, int runs, DateTime lastStartedAt)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Runs = runs;
        LastStartedAt = lastStartedAt;
    }

    /// <summary>Gets the assembly name.</summary>
    public string Name { get; }

    /// <summary>Gets the number of stored runs that covered this assembly.</summary>
    public int Runs { get; }

    /// <summary>Gets when the newest of those runs started, in UTC.</summary>
    public DateTime LastStartedAt { get; }
}

/// <summary>
/// What the local store holds, by assembly, read without loading any session.
/// </summary>
public sealed class LocalStoreSummary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalStoreSummary"/> class.
    /// </summary>
    /// <param name="assemblies">Every assembly with at least one stored run, ordered by name.</param>
    /// <param name="unattributedRuns">Stored runs that name no assembly or could not be read.</param>
    public LocalStoreSummary(IReadOnlyList<LocalStoreAssembly> assemblies, int unattributedRuns)
    {
        Assemblies = assemblies ?? throw new ArgumentNullException(nameof(assemblies));
        UnattributedRuns = unattributedRuns;
    }

    /// <summary>Gets an empty summary.</summary>
    public static LocalStoreSummary Empty { get; } = new(new List<LocalStoreAssembly>(), 0);

    /// <summary>Gets every assembly with at least one stored run, ordered by name.</summary>
    /// <remarks>
    /// A run that covered several assemblies is counted under each, so the run counts can add up to
    /// more than the number of files in the store.
    /// </remarks>
    public IReadOnlyList<LocalStoreAssembly> Assemblies { get; }

    /// <summary>Gets the number of stored runs that name no assembly or could not be read.</summary>
    public int UnattributedRuns { get; }
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
    /// Summarizes the whole store by assembly, without loading any session.
    /// </summary>
    /// <returns>Every assembly's run count and newest run, and the runs that name none.</returns>
    /// <remarks>
    /// Covers every file, however many the store holds, which <see cref="ReadRecent"/> with a
    /// bound cannot promise. Cheap enough to call whenever the full list of assemblies is needed.
    /// </remarks>
    LocalStoreSummary Summarize();

    /// <summary>
    /// Lists the stored runs newest first, as their file names and headers describe them.
    /// </summary>
    /// <returns>
    /// One header per file, read only as the sequence is enumerated, so a caller looking for the
    /// newest run with some property reads as few headers as it needs.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Loads no session. A header is the head of the file, so a damaged run whose head is intact is
    /// listed here although <see cref="ReadRecent"/> reports it unreadable.
    /// </para>
    /// <para>
    /// Ties in start time break the same way <see cref="ReadRecent"/> breaks them, so "the newest
    /// run" means the same run through either method.
    /// </para>
    /// </remarks>
    IEnumerable<LocalSessionHeader> ReadHeaders();

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
