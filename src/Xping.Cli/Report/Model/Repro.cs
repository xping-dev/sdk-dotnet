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
/// <param name="RunsFailed">Runs of the rerun test that ended red, by their deciding attempt.</param>
/// <param name="RunsJudged">Its runs with a verdict, which they were counted out of.</param>
internal sealed record Repro(string Command, int RunsNeeded, int RunsFailed, int RunsJudged)
{
    /// <summary>
    /// Gets how many times to run <see cref="Command"/>, or null when that is more than
    /// <see cref="LocalAnalysisConstants.ReproMaxRuns"/>.
    /// </summary>
    public int? Runs => RunsNeeded <= LocalAnalysisConstants.ReproMaxRuns ? RunsNeeded : null;

    /// <summary>
    /// Gets the per-run failure rate the counts were sized from.
    /// </summary>
    public double Rate => (double)RunsFailed / RunsJudged;
}
