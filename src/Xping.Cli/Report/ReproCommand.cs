/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;

namespace Xping.Cli.Report;

/// <summary>
/// Builds the command that makes a finding's failure happen again, and says how many runs it takes.
/// </summary>
/// <remarks>
/// <para>
/// Printed, never run. <c>report</c> stays read-only over the store: running tests would write
/// sessions into the window it just analysed, and nothing in the store says which project,
/// configuration or environment the suite needs.
/// </para>
/// <para>
/// The count is the point. One rerun of a test that fails in 30% of runs passes 70% of the time,
/// and the reader concludes the report is wrong. The rate is per run, not per execution, because one
/// <c>dotnet test</c> is one run and the reader judges each by its result; an execution rate counts
/// retry attempts and would understate the runs needed.
/// </para>
/// <para>
/// Only kinds whose failure a plain rerun can show get a command. A retried test stays green while
/// its retry attribute is on, and nothing on the command line turns that off; a duration, a
/// concurrency or a time-of-day effect does not show up as pass or fail; a vanished test has nothing
/// to run. A command that cannot show the failure discredits the report more than no command.
/// </para>
/// </remarks>
internal static class ReproCommand
{
    /// <summary>
    /// Shared-failure members a command names. Enough to see the shared cause, few enough to keep the
    /// command short.
    /// </summary>
    private const int SharedFailureMembers = 3;

    /// <summary>
    /// Builds the repro for a finding.
    /// </summary>
    /// <param name="finding">The finding.</param>
    /// <returns>The repro, or null when rerunning cannot show this kind of failure.</returns>
    public static Repro? For(Finding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        return finding.Evidence switch
        {
            FlakyEvidence e => Sized(Single(finding), e.SessionsWithFailures, e.SessionsConsidered),

            // Broken, not intermittent: one run shows it, whatever the odd pass in the window says.
            AlwaysFailingEvidence e => Build(Single(finding), 1, 1, e.SessionsWithFailures, e.SessionsConsidered),

            TimingOutEvidence e => Sized(Single(finding), e.SessionsWithTimeouts, e.SessionsConsidered),

            // A broken fixture fails every test that uses it, so one member shows it.
            BrokenFixtureEvidence e => Sized(MostFailing(e.Members, 1), e.SessionsAffected, e.Sessions),

            SharedFailureEvidence e => Sized(MostFailing(e.Members, SharedFailureMembers), e.SessionsAffected, e.Sessions),

            _ => null
        };
    }

    private static string[] Single(Finding finding) =>
        finding.Subject is FindingSubject.SingleTest single ? [single.Test.FullyQualifiedName] : [];

    // OrderByDescending is stable, so a tie keeps the members' ordinal order and the command is
    // the same on every run of the report.
    private static string[] MostFailing(IReadOnlyList<ClusterMember> members, int count) =>
        [.. members.OrderByDescending(member => member.Failures).Take(count).Select(member => member.FullyQualifiedName)];

    private static Repro? Sized(string[] names, int failing, int sessions)
    {
        if (failing <= 0 || sessions <= 0)
            return null;

        int needed = RunsNeeded((double)failing / sessions);

        return Build(names, needed <= LocalAnalysisConstants.ReproMaxRuns ? needed : null, needed, failing, sessions);
    }

    private static Repro? Build(string[] names, int? runs, int needed, int failing, int sessions)
    {
        if (names.Length == 0 || sessions <= 0)
            return null;

        string filter = string.Join("|", names.Select(name => "FullyQualifiedName=" + Escape(name)));

        return new Repro(
            "dotnet test --no-build -e XPING_ENABLED=false --filter " + ShellQuote.Of(filter),
            runs,
            needed,
            failing,
            sessions);
    }

    /// <summary>
    /// The runs that show at least one failure with probability
    /// <see cref="LocalAnalysisConstants.ReproConfidence"/>, at per-run rate <paramref name="rate"/>.
    /// </summary>
    private static int RunsNeeded(double rate)
    {
        if (rate >= 1)
            return 1;

        double runs = Math.Ceiling(Math.Log(1 - LocalAnalysisConstants.ReproConfidence) / Math.Log(1 - rate));

        return runs >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)runs);
    }

    /// <summary>
    /// Escapes a name for a VSTest filter value.
    /// </summary>
    /// <remarks>
    /// VSTest reserves <c>\ ( ) &amp; | = ! ~</c>. <c>"</c> is not VSTest's but MSBuild's:
    /// <c>dotnet test</c> hands the filter to MSBuild as a property, and a bare quote there fails
    /// before the runner starts. NUnit's stored names quote their string arguments.
    /// </remarks>
    private static string Escape(string name)
    {
        var builder = new StringBuilder(name.Length + 8);

        foreach (char c in name)
        {
            if (c is '\\' or '(' or ')' or '&' or '|' or '=' or '!' or '~' or '"')
                builder.Append('\\');

            builder.Append(c);
        }

        return builder.ToString();
    }
}
