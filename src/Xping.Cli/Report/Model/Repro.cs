/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Model;

/// <summary>
/// How to make a finding's failure happen again: a command, and how many times to run it.
/// </summary>
/// <param name="Command">One <c>dotnet</c> invocation, on one line.</param>
/// <param name="RunsNeeded">
/// Runs that show at least one failure with probability
/// <see cref="LocalAnalysisConstants.ReproConfidence"/>, uncapped. Carried so a finding too rare to
/// rerun can say how rare.
/// </param>
/// <param name="FailingSessions">Runs in the window that showed the failure.</param>
/// <param name="Sessions">Runs they were counted out of.</param>
internal sealed record Repro(string Command, int RunsNeeded, int FailingSessions, int Sessions)
{
    /// <summary>
    /// Gets how many times to run <see cref="Command"/>, or null when that is more than
    /// <see cref="LocalAnalysisConstants.ReproMaxRuns"/>.
    /// </summary>
    public int? Runs => RunsNeeded <= LocalAnalysisConstants.ReproMaxRuns ? RunsNeeded : null;

    /// <summary>
    /// Gets the per-run failure rate the counts were sized from.
    /// </summary>
    public double Rate => (double)FailingSessions / Sessions;
}
