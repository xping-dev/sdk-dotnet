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
    }

    /// <summary>Gets the sessions under analysis and the boundaries that produced them.</summary>
    public AnalysisWindow Window { get; }

    /// <summary>Gets the shared derived index over those sessions.</summary>
    public TestIndex Tests { get; }

    /// <summary>
    /// Gets where the analysed sessions came from, or <see langword="null"/> when unknown.
    /// </summary>
    public RevisionContext? Revision { get; }

    /// <summary>Gets the analysed sessions, newest first.</summary>
    public IReadOnlyList<TestSession> Sessions => Window.Sessions;
}
