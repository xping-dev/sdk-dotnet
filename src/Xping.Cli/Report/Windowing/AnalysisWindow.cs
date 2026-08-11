/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Sdk.Core.Models;

namespace Xping.Cli.Report.Windowing;

/// <summary>
/// How the analysed set of sessions was chosen.
/// </summary>
internal enum WindowResolution
{
    /// <summary>No selection flag given; the default bounds applied.</summary>
    Default,

    /// <summary>A session count was requested with <c>--runs</c>.</summary>
    Runs,

    /// <summary>A starting commit was requested with <c>--since</c>.</summary>
    SinceSha,

    /// <summary>A starting date was requested with <c>--since</c>.</summary>
    SinceDate
}

/// <summary>
/// The sessions under analysis, together with the boundaries that produced them.
/// </summary>
/// <remarks>
/// <para>
/// The window is data, not a filter that has already been applied and forgotten. Providers, the
/// human report and the JSON envelope all read these values verbatim, so a reader never has to infer
/// what "recent" meant on the machine that produced the report.
/// </para>
/// <para>
/// <paramref name="Sessions"/> is ordered newest first. The current slice is its head and the
/// baseline slice its tail, so a delta finding compares "now" against "before" without re-deriving
/// either.
/// </para>
/// </remarks>
/// <param name="Sessions">The analysed sessions, newest first.</param>
/// <param name="From">Start of the oldest session in the window.</param>
/// <param name="To">Start of the newest session in the window.</param>
/// <param name="Resolution">How the window was chosen.</param>
/// <param name="ResolutionArgument">The flag value that produced it, when there was one.</param>
/// <param name="CurrentSlice">The most recent sessions — the "now" side of a delta.</param>
/// <param name="BaselineSlice">The remaining sessions — the "before" side of a delta.</param>
internal sealed record AnalysisWindow(
    IReadOnlyList<TestSession> Sessions,
    DateTime From,
    DateTime To,
    WindowResolution Resolution,
    string? ResolutionArgument,
    IReadOnlyList<TestSession> CurrentSlice,
    IReadOnlyList<TestSession> BaselineSlice)
{
    /// <summary>Gets the number of sessions in the window.</summary>
    public int SessionCount => Sessions.Count;

    /// <summary>Gets the number of sessions forming the current slice.</summary>
    public int CurrentSliceSize => CurrentSlice.Count;

    /// <summary>
    /// Gets a value indicating whether the window is large enough for findings to be emitted.
    /// </summary>
    /// <remarks>
    /// A window below the floor still produces a report — the counts, the context and the window
    /// itself are all meaningful, and telling a developer with three runs that they have "no data"
    /// is both wrong and discouraging. It simply produces no findings.
    /// </remarks>
    public bool MeetsReportingFloor =>
        SessionCount >= LocalAnalysisConstants.MinimumSessionsToReport;

    /// <summary>
    /// Gets a stable identity for this window, used to make finding ids window-scoped.
    /// </summary>
    /// <remarks>
    /// Built from the session ids rather than the timestamps or the count: two windows holding the
    /// same sessions are the same window regardless of which flag selected them, and a finding about
    /// them should keep its id.
    /// </remarks>
    public string Key
    {
        get
        {
            // Sessions are already in a total order, so concatenating in sequence is deterministic.
            var builder = new System.Text.StringBuilder(Sessions.Count * 33);
            foreach (TestSession session in Sessions)
                builder.Append(session.SessionId.ToString("N", CultureInfo.InvariantCulture)).Append('|');

            return builder.ToString();
        }
    }

    /// <summary>
    /// Splits an ordered session list into its current and baseline slices.
    /// </summary>
    /// <param name="sessions">Sessions, newest first.</param>
    /// <returns>The window with both slices resolved.</returns>
    /// <param name="from">Start of the oldest session.</param>
    /// <param name="to">Start of the newest session.</param>
    /// <param name="resolution">How the window was chosen.</param>
    /// <param name="argument">The flag value that produced it.</param>
    public static AnalysisWindow Create(
        IReadOnlyList<TestSession> sessions,
        DateTime from,
        DateTime to,
        WindowResolution resolution,
        string? argument)
    {
        // In a small window three sessions would be most of the history, leaving a baseline too thin
        // to compare against. One session is a worse "now" but leaves a usable "before".
        int sliceSize = sessions.Count < LocalAnalysisConstants.SmallWindowSessionCount
            ? 1
            : LocalAnalysisConstants.CurrentSliceSize;

        sliceSize = Math.Min(sliceSize, sessions.Count);

        return new AnalysisWindow(
            sessions,
            from,
            to,
            resolution,
            argument,
            [.. sessions.Take(sliceSize)],
            [.. sessions.Skip(sliceSize)]);
    }
}
