/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Store;
using Xping.Sdk.Core.Models;

namespace Xping.Cli.Report;

/// <summary>
/// Where the analysed sessions came from in source control.
/// </summary>
/// <remarks>
/// <para>
/// Recovered from the environment properties the SDK collects by reading <c>.git/HEAD</c> directly.
/// That collection runs for local runs only, so sessions recorded in CI carry no commit and simply
/// contribute nothing here.
/// </para>
/// <para>
/// There is deliberately no <c>dirty</c> flag. The SDK records whether there are <i>staged</i>
/// changes, which is a different question — a working tree full of unstaged edits reports clean —
/// and a field that is wrong in the common case is worse than a field that is absent.
/// </para>
/// </remarks>
/// <param name="Sha">The commit the newest analysed session ran at.</param>
/// <param name="Branch">The branch it ran on, when known.</param>
/// <param name="Assembly">The test assembly the report covers, when scoped to one.</param>
internal sealed record RevisionContext(string? Sha, string? Branch, string? Assembly)
{
    private const string ShaProperty = "Git.SHA";
    private const string BranchProperty = "Git.Branch";

    /// <summary>
    /// Builds the context from the newest session in a window.
    /// </summary>
    /// <param name="sessions">The analysed sessions, newest first.</param>
    /// <returns>
    /// The context, or <see langword="null"/> when nothing is known. It is never fabricated: a null
    /// context says "this report cannot tell you what was checked out", which is honest, where an
    /// invented one would be quietly wrong.
    /// </returns>
    public static RevisionContext? FromNewest(IReadOnlyList<TestSession> sessions)
    {
        if (sessions.Count == 0)
            return null;

        TestSession newest = sessions[0];

        string? sha = ReadSha(newest);
        string? branch = ReadProperty(newest, BranchProperty);
        string? assembly = LocalSessionSource.AssemblyOf(newest);

        if (sha == null && branch == null && assembly == null)
            return null;

        return new RevisionContext(sha, branch, assembly);
    }

    /// <summary>
    /// Reads the commit a session ran at, or <see langword="null"/> when it recorded none.
    /// </summary>
    /// <param name="session">The session to read.</param>
    /// <returns>The commit SHA.</returns>
    public static string? ReadSha(TestSession session) => ReadProperty(session, ShaProperty);

    private static string? ReadProperty(TestSession session, string key)
    {
        if (session.EnvironmentInfo?.CustomProperties == null)
            return null;

        return session.EnvironmentInfo.CustomProperties.TryGetValue(key, out string? value) &&
               !string.IsNullOrEmpty(value)
            ? value
            : null;
    }
}
