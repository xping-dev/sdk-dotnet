/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Services.LocalStore;

/// <summary>
/// Resolves which test assemblies a stored session covers, and narrows a session to one of them.
/// </summary>
/// <remarks>
/// <para>
/// A session records one test host process, not one test assembly. VSTest batches test projects that
/// share a target framework and architecture into a single host, so a solution-wide
/// <c>dotnet test</c> produces one session holding every assembly's executions. Treating such a
/// session as belonging to whichever assembly its first execution happened to name would label the
/// run arbitrarily: one assembly would inherit another's tests and run count, and the rest would
/// vanish from a scoped report entirely.
/// </para>
/// <para>
/// So a session is not asked which assembly it <i>is</i>. It is asked which assemblies it
/// <i>covers</i>, and is projected onto one of them when a report is scoped. A run that exercised
/// three assemblies is one run of each, carrying only that assembly's executions.
/// </para>
/// </remarks>
public static class SessionAssemblies
{
    /// <summary>
    /// Returns the distinct test assemblies a session covers.
    /// </summary>
    /// <param name="session">The session to inspect, or <see langword="null"/>.</param>
    /// <returns>
    /// The assembly names in ordinal order, or an empty list when the session names none.
    /// </returns>
    /// <remarks>
    /// Sorted rather than left in execution order so that two runs of the same solution list their
    /// assemblies identically however the host happened to interleave them — analysis output has to
    /// be byte-identical across runs.
    /// <para>
    /// An empty result is a real answer, not a failure. An execution recorded before identity
    /// generation completed carries no assembly, and a session made up entirely of those cannot be
    /// attributed to anything; it is reported as covering nothing rather than guessed at.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> Of(TestSession? session)
    {
        if (session == null)
            return [];

        var names = new SortedSet<string>(StringComparer.Ordinal);

        foreach (TestExecution execution in session.Executions)
        {
            string candidate = execution.Identity.Assembly;
            if (!string.IsNullOrEmpty(candidate))
                names.Add(candidate);
        }

        return names.Count == 0 ? [] : [.. names];
    }

    /// <summary>
    /// Returns whether a session recorded any execution belonging to a given test assembly.
    /// </summary>
    /// <param name="session">The session to inspect, or <see langword="null"/>.</param>
    /// <param name="assembly">The assembly name to look for.</param>
    /// <returns><see langword="true"/> when at least one execution names that assembly.</returns>
    public static bool Covers(TestSession? session, string assembly)
    {
        if (session == null || string.IsNullOrEmpty(assembly))
            return false;

        foreach (TestExecution execution in session.Executions)
        {
            if (string.Equals(execution.Identity.Assembly, assembly, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Narrows a session to the executions belonging to one test assembly.
    /// </summary>
    /// <param name="session">The session to narrow, or <see langword="null"/>.</param>
    /// <param name="assembly">The assembly to keep.</param>
    /// <returns>
    /// A copy carrying only that assembly's executions, or <see langword="null"/> when the session
    /// recorded none of them — which is how a caller tells "this run is not part of that assembly's
    /// history" apart from "this run of it executed nothing".
    /// </returns>
    /// <remarks>
    /// <para>
    /// <see cref="TestSession.SessionId"/> and <see cref="TestSession.StartedAt"/> are preserved, so
    /// a projection still sorts and de-duplicates as the run it came from. Two projections of one
    /// session share an id, which is safe because a window is always scoped to a single assembly and
    /// therefore never holds both.
    /// </para>
    /// <para>
    /// <see cref="TestSession.QuickStatistics"/> and <see cref="TestSession.TotalTestsExpected"/> are
    /// dropped. Both describe the whole host process rather than this slice of it, and a
    /// solution-wide count carried onto one assembly's history would be wrong in exactly the way
    /// this projection exists to prevent.
    /// </para>
    /// </remarks>
    public static TestSession? Project(TestSession? session, string assembly) =>
        Filter(session, assembly, keepMatches: true);

    /// <summary>
    /// Removes one test assembly's executions from a session, keeping every other assembly's.
    /// </summary>
    /// <param name="session">The session to strip, or <see langword="null"/>.</param>
    /// <param name="assembly">The assembly to remove.</param>
    /// <returns>
    /// A copy without that assembly's executions, or <see langword="null"/> when nothing is left —
    /// the session recorded that assembly and nothing else, so there is no run left to keep.
    /// </returns>
    /// <remarks>
    /// The complement of <see cref="Project"/>, and what makes a scoped delete safe: one run can
    /// hold several test projects' history, so deleting a run outright to clear one of them would
    /// take the others with it. Stripping lets the run survive carrying only what was not asked for.
    /// </remarks>
    public static TestSession? Excluding(TestSession? session, string assembly) =>
        Filter(session, assembly, keepMatches: false);

    /// <summary>
    /// Builds the session that remains when executions are kept or dropped by assembly.
    /// </summary>
    private static TestSession? Filter(TestSession? session, string assembly, bool keepMatches)
    {
        if (session == null || string.IsNullOrEmpty(assembly))
            return null;

        var executions = new List<TestExecution>(session.Executions.Count);

        foreach (TestExecution execution in session.Executions)
        {
            bool matches = string.Equals(
                execution.Identity.Assembly, assembly, StringComparison.Ordinal);

            if (matches == keepMatches)
                executions.Add(execution);
        }

        if (executions.Count == 0)
            return null;

        // Nothing was filtered out, so no copy is needed. The common case — a single-project
        // `dotnet test`, projected onto its one assembly — pays nothing.
        if (executions.Count == session.Executions.Count)
            return session;

        // Built by object initializer rather than TestSessionBuilder: the builder stamps SdkVersion
        // from the running assembly, which would relabel a session recorded by an older SDK with the
        // version of whatever is reading it.
        return new TestSession
        {
            SessionId = session.SessionId,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            EnvironmentInfo = session.EnvironmentInfo,
            Executions = executions.AsReadOnly(),
            SessionState = session.SessionState,
            PullRequestContext = session.PullRequestContext,
            SdkVersion = session.SdkVersion,
            TotalTestsExpected = null,
            QuickStatistics = null
        };
    }
}
