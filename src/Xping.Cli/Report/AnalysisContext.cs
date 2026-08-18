/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;

namespace Xping.Cli.Report;

/// <summary>
/// Everything a finding provider is allowed to see.
/// </summary>
/// <remarks>
/// <para>
/// Read-only by construction. A provider receives this, returns findings, and does nothing else — it
/// never reads the disk, never writes anything, and never calls another provider. That restriction
/// is what lets the coordinator run a provider that throws without the report dying with it, and
/// what lets every provider be tested against a hand-built window.
/// </para>
/// <para>
/// Anything more than one provider would derive belongs on <see cref="Tests"/> rather than being
/// recomputed per provider.
/// </para>
/// </remarks>
internal sealed class AnalysisContext
{
    private readonly Dictionary<Guid, SessionView> _viewsBySession;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalysisContext"/> class.
    /// </summary>
    /// <param name="window">The sessions under analysis and their boundaries.</param>
    /// <param name="revision">Where those sessions came from in source control.</param>
    public AnalysisContext(AnalysisWindow window, RevisionContext? revision)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
        Revision = revision;
        Tests = TestIndex.Build(window);
        Signatures = SignatureIndex.Build(window);

        var views = new List<SessionView>(window.Sessions.Count);
        for (int position = 0; position < window.Sessions.Count; position++)
            views.Add(SessionView.For(window.Sessions[position], position));

        SessionViews = views;
        _viewsBySession = views.ToDictionary(v => v.Session.SessionId);
        EnvironmentalSessionCount = views.Count(v => v.IsLikelyEnvironmental);
    }

    /// <summary>Gets the sessions under analysis and the boundaries that produced them.</summary>
    public AnalysisWindow Window { get; }

    /// <summary>Gets the shared derived index over those sessions.</summary>
    public TestIndex Tests { get; }

    /// <summary>Gets the failure signatures of every failure in the window.</summary>
    public SignatureIndex Signatures { get; }

    /// <summary>Gets the analysed sessions with their health, newest first.</summary>
    public IReadOnlyList<SessionView> SessionViews { get; }

    /// <summary>
    /// Gets how many analysed sessions look like the environment failed rather than the tests.
    /// </summary>
    /// <remarks>
    /// Reported in the summary so a reader can tell a quiet window from one whose numbers were
    /// deliberately discounted. Without that line the discounting would silently change every rate
    /// in the report with nothing on screen to explain it.
    /// </remarks>
    public int EnvironmentalSessionCount { get; }

    /// <summary>
    /// Gets the health of one analysed session.
    /// </summary>
    /// <param name="sessionId">The session to look up.</param>
    /// <returns>Its view, or <see langword="null"/> when it is not in the window.</returns>
    public SessionView? SessionViewFor(Guid sessionId) =>
        _viewsBySession.TryGetValue(sessionId, out SessionView? view) ? view : null;

    /// <summary>
    /// Gets where the analysed sessions came from, or <see langword="null"/> when unknown.
    /// </summary>
    public RevisionContext? Revision { get; }

    /// <summary>Gets the analysed sessions, newest first.</summary>
    public IReadOnlyList<TestSession> Sessions => Window.Sessions;
}
