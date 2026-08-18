/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Store;
using Xping.Sdk.Core.Models;

namespace Xping.Cli.Report.Windowing;

/// <summary>
/// What the caller asked the window to cover.
/// </summary>
/// <param name="Runs">Session count requested with <c>--runs</c>.</param>
/// <param name="Since">Commit or date requested with <c>--since</c>.</param>
/// <param name="Assembly">Test assembly to scope to, when one was chosen.</param>
internal sealed record WindowRequest(int? Runs, string? Since, string? Assembly);

/// <summary>
/// Why a window could not be resolved.
/// </summary>
internal enum WindowFailure
{
    /// <summary>No local store exists at the resolved location.</summary>
    NoStore,

    /// <summary>The store exists but holds no readable sessions.</summary>
    EmptyStore,

    /// <summary>A <c>--since</c> commit was given that no session in the store carries.</summary>
    ShaNotFound,

    /// <summary>A <c>--since</c> value was given that is neither a date nor a commit.</summary>
    SinceNotUnderstood
}

/// <summary>
/// The outcome of resolving a window.
/// </summary>
/// <param name="Window">The resolved window, or <see langword="null"/> when resolution failed.</param>
/// <param name="Failure">Why resolution failed, when it did.</param>
/// <param name="FailureDetail">Human-readable detail for the failure message.</param>
/// <param name="UnreadableSessions">Files that could not be read while gathering the window.</param>
internal sealed record WindowResult(
    AnalysisWindow? Window,
    WindowFailure? Failure,
    string? FailureDetail,
    int UnreadableSessions)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="window">The resolved window.</param>
    /// <param name="unreadable">Files skipped while gathering it.</param>
    /// <returns>The result.</returns>
    public static WindowResult Success(AnalysisWindow window, int unreadable) =>
        new(window, null, null, unreadable);

    /// <summary>Creates a failed result.</summary>
    /// <param name="failure">Why resolution failed.</param>
    /// <param name="detail">Human-readable detail.</param>
    /// <param name="unreadable">Files skipped before the failure was reached.</param>
    /// <returns>The result.</returns>
    public static WindowResult Failed(
        WindowFailure failure, string? detail = null, int unreadable = 0) =>
        new(null, failure, detail, unreadable);
}

/// <summary>
/// Chooses which sessions a report covers.
/// </summary>
internal interface IWindowResolver
{
    /// <summary>
    /// Resolves the window for one report.
    /// </summary>
    /// <param name="source">Where sessions are read from.</param>
    /// <param name="request">What the caller asked for.</param>
    /// <returns>The resolved window, or the reason none could be resolved.</returns>
    WindowResult Resolve(ISessionSource source, WindowRequest request);
}

/// <summary>
/// Default <see cref="IWindowResolver"/>.
/// </summary>
/// <remarks>
/// Takes a <see cref="TimeProvider"/> because <c>--since &lt;date&gt;</c> is the one place analysis
/// is allowed to read a clock. The resolved boundary is then carried on the window as data, so
/// nothing downstream reads the time again and two runs over an unchanged store agree.
/// </remarks>
internal sealed class WindowResolver(TimeProvider timeProvider) : IWindowResolver
{
    // Bounds the read when --since selects by date or commit: we do not know how many sessions fall
    // inside the boundary until we have looked, but retention caps the store well below this.
    private const int UnboundedReadCeiling = 1000;

    public WindowResult Resolve(ISessionSource source, WindowRequest request)
    {
        if (!source.IsAvailable)
            return WindowResult.Failed(WindowFailure.NoStore);

        return request switch
        {
            { Since: { Length: > 0 } since } => ResolveSince(source, request, since),
            { Runs: { } runs } => ResolveCount(source, request, runs, WindowResolution.Runs, runs.ToString(CultureInfo.InvariantCulture)),
            _ => ResolveDefault(source, request)
        };
    }

    /// <summary>
    /// Applies the default bounds: the most recent N sessions, or everything inside the default age,
    /// whichever yields fewer.
    /// </summary>
    private WindowResult ResolveDefault(ISessionSource source, WindowRequest request)
    {
        SessionReadResult read = source.Read(
            LocalAnalysisConstants.DefaultWindowSessions, request.Assembly);

        if (read.Sessions.Count == 0)
            return WindowResult.Failed(WindowFailure.EmptyStore, unreadable: read.UnreadableCount);

        DateTime cutoff = timeProvider.GetUtcNow().UtcDateTime
            - TimeSpan.FromDays(LocalAnalysisConstants.DefaultWindowDays);

        var withinAge = read.Sessions.Where(s => s.StartedAt >= cutoff).ToList();

        // "Whichever yields fewer" — but never zero. A developer returning after a fortnight would
        // otherwise be told they have no history at all, when what they have is old history, which
        // the window bounds already declare.
        IReadOnlyList<TestSession> selected = withinAge.Count > 0 ? withinAge : read.Sessions;

        return Build(selected, WindowResolution.Default, null, read.UnreadableCount);
    }

    /// <summary>
    /// Takes the most recent <paramref name="count"/> sessions.
    /// </summary>
    private static WindowResult ResolveCount(
        ISessionSource source,
        WindowRequest request,
        int count,
        WindowResolution resolution,
        string? argument)
    {
        SessionReadResult read = source.Read(count, request.Assembly);

        if (read.Sessions.Count == 0)
            return WindowResult.Failed(WindowFailure.EmptyStore, unreadable: read.UnreadableCount);

        return Build(read.Sessions, resolution, argument, read.UnreadableCount);
    }

    /// <summary>
    /// Resolves <c>--since</c>, which accepts either a date or a commit.
    /// </summary>
    /// <remarks>
    /// Dates are tried first: no commit hash parses as a date, and no date matches the hex pattern,
    /// so the two cannot be confused. A value that is neither is a mistake worth reporting rather
    /// than guessing at.
    /// </remarks>
    private static WindowResult ResolveSince(
        ISessionSource source, WindowRequest request, string since)
    {
        if (TryParseDate(since, out DateTime boundary))
            return ResolveSinceDate(source, request, since, boundary);

        if (IsCommitLike(since))
            return ResolveSinceSha(source, request, since);

        return WindowResult.Failed(
            WindowFailure.SinceNotUnderstood,
            $"'{since}' is neither a date (yyyy-MM-dd) nor a commit SHA.");
    }

    private static WindowResult ResolveSinceDate(
        ISessionSource source, WindowRequest request, string argument, DateTime boundary)
    {
        SessionReadResult read = source.Read(UnboundedReadCeiling, request.Assembly);

        var selected = read.Sessions.Where(s => s.StartedAt >= boundary).ToList();

        if (selected.Count == 0)
        {
            return WindowResult.Failed(
                WindowFailure.EmptyStore,
                $"No sessions recorded on or after {boundary:yyyy-MM-dd}.",
                read.UnreadableCount);
        }

        return Build(selected, WindowResolution.SinceDate, argument, read.UnreadableCount);
    }

    /// <summary>
    /// Takes every session from the oldest one carrying the requested commit onwards.
    /// </summary>
    /// <remarks>
    /// The <i>oldest</i> match anchors the window, not the newest: a commit that was tested several
    /// times should include all of those runs, and anchoring on the newest would silently discard
    /// the earlier ones.
    /// </remarks>
    private static WindowResult ResolveSinceSha(
        ISessionSource source, WindowRequest request, string sha)
    {
        SessionReadResult read = source.Read(UnboundedReadCeiling, request.Assembly);

        // Sessions arrive newest first, so the last match is the oldest one.
        int anchor = -1;
        for (int i = 0; i < read.Sessions.Count; i++)
        {
            if (MatchesSha(read.Sessions[i], sha))
                anchor = i;
        }

        if (anchor < 0)
        {
            return WindowResult.Failed(
                WindowFailure.ShaNotFound,
                $"No recorded session carries commit '{sha}'. " +
                "Commits are recorded for local runs only; runs recorded in CI carry none.",
                read.UnreadableCount);
        }

        var selected = read.Sessions.Take(anchor + 1).ToList();

        return Build(selected, WindowResolution.SinceSha, sha, read.UnreadableCount);
    }

    private static WindowResult Build(
        IReadOnlyList<TestSession> sessions,
        WindowResolution resolution,
        string? argument,
        int unreadable)
    {
        // Sessions are newest first, so the bounds come from the ends of the list.
        DateTime to = sessions[0].StartedAt;
        DateTime from = sessions[sessions.Count - 1].StartedAt;

        return WindowResult.Success(
            AnalysisWindow.Create(sessions, from, to, resolution, argument), unreadable);
    }

    private static bool MatchesSha(TestSession session, string prefix) =>
        RevisionContext.ReadSha(session) is { } sha &&
        sha.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Recognises the values a commit hash can take, at any abbreviation git itself accepts.
    /// </summary>
    private static bool IsCommitLike(string value)
    {
        const int MinimumAbbreviation = 4;
        const int FullShaLength = 40;

        if (value.Length is < MinimumAbbreviation or > FullShaLength)
            return false;

        foreach (char c in value)
        {
            if (!Uri.IsHexDigit(c))
                return false;
        }

        return true;
    }

    private static bool TryParseDate(string value, out DateTime boundary)
    {
        // Invariant culture and an explicit UTC assumption: the same command must select the same
        // sessions regardless of the machine's locale, and sessions are stamped in UTC.
        bool parsed = DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out boundary);

        // A bare hex run like "20260810" parses as a date on some paths but is far more likely to be
        // a commit. Requiring a separator keeps the two apart.
        return parsed && value.IndexOfAny(['-', '/', ':']) >= 0;
    }
}
