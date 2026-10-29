/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Sdk.Core.Services.LocalStore;

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
/// <para>
/// The command follows the runner the SDK recorded. A vstest project runs under <c>dotnet test</c>;
/// a Microsoft.Testing.Platform project runs as its own executable through <c>dotnet run</c>, which
/// works whether the suite is usually started that way or by <c>dotnet test</c> in the SDK's
/// platform mode — and in that mode <c>-e</c> does not reach the test process, so the repro runs
/// would be recorded. With no runner recorded there is no command: a guessed runner prints one that
/// fails.
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
    /// <param name="target">The runner and project the window's newest session recorded.</param>
    /// <param name="shell">The shell the command is quoted for.</param>
    /// <returns>
    /// The repro, or null when rerunning cannot show this kind of failure or the runner is unknown.
    /// </returns>
    public static Repro? For(Finding finding, ReproTarget target, ShellDialect shell)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(target);

        Runner? runner = target.Runner switch
        {
            LocalSessionProperties.VSTestRunner => Runner.VSTest,
            LocalSessionProperties.TestingPlatformRunner => Runner.TestingPlatform,
            _ => null
        };

        if (runner is not { } known)
            return null;

        // Every rate is over the runs the rerun tests ran in with a verdict, failed by their deciding
        // attempt. The window's count is larger for a test that joined it late or was skipped, a
        // cluster's is larger than any subset of it, and a run a retry turned green comes back green;
        // each puts the count off in a direction the reader cannot see.
        (string[] names, int failing, int judged, bool once) = finding.Evidence switch
        {
            FlakyEvidence e => (Single(finding), e.RunsFailed, e.RunsJudged, false),

            // Broken, not intermittent: one run shows it, whatever the odd pass in the window says.
            AlwaysFailingEvidence e => (Single(finding), e.RunsFailed, e.RunsJudged, true),

            TimingOutEvidence e => (Single(finding), e.RunsFailed, e.RunsJudged, false),

            // A broken fixture fails every test that uses it, so one member shows it.
            BrokenFixtureEvidence e => Members(e.Members, 1),

            SharedFailureEvidence e => Members(e.Members, SharedFailureMembers),

            _ => ([], 0, 0, false)
        };

        if (names.Length == 0 || failing <= 0 || judged <= 0)
            return null;

        return new Repro(
            Command(names, known, target.ProjectPath, shell),
            once ? 1 : RunsNeeded((double)failing / judged),
            failing,
            judged);
    }

    /// <summary>
    /// The runners a command can be built for.
    /// </summary>
    private enum Runner
    {
        VSTest,
        TestingPlatform
    }

    private static string[] Single(Finding finding) =>
        finding.Subject is FindingSubject.SingleTest single ? [single.Test.FullyQualifiedName] : [];

    /// <summary>
    /// Picks the members most likely to fail on a rerun, and the rate to size their runs from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rate is the highest of theirs, not the chance that any of them fails, which the counts
    /// cannot give: their failing runs may overlap or not. The highest is a lower bound on that
    /// chance, so the count errs towards more runs. OrderBy is stable, so a full tie keeps the
    /// members' ordinal order and the command is the same on every run of the report.
    /// </para>
    /// <para>
    /// One per name. MSTest and xUnit data rows share an FQN, and the filter runs every row of it, so
    /// a second row would repeat a clause and crowd out another test.
    /// </para>
    /// </remarks>
    private static (string[] Names, int Failing, int Judged, bool Once) Members(
        IReadOnlyList<ClusterMember> members, int count)
    {
        ClusterMember[] chosen =
        [
            .. members
                .Where(member => member.RunsJudged > 0)
                .OrderByDescending(member => (double)member.RunsFailed / member.RunsJudged)
                .ThenByDescending(member => member.Failures)
                .DistinctBy(member => member.FullyQualifiedName, StringComparer.Ordinal)
                .Take(count)
        ];

        if (chosen.Length == 0)
            return ([], 0, 0, false);

        // Chosen in descending rate order, so the first carries the highest.
        return ([.. chosen.Select(member => member.FullyQualifiedName)], chosen[0].RunsFailed, chosen[0].RunsJudged, false);
    }

    private static string Command(string[] names, Runner runner, string? projectPath, ShellDialect shell)
    {
        bool vstest = runner == Runner.VSTest;
        var builder = new StringBuilder(vstest ? "dotnet test" : "dotnet run");

        if (projectPath != null)
            builder.Append(vstest ? " " : " --project ").Append(ShellQuote.Of(projectPath, shell));

        string filter = string.Join("|", names.Select(name => "FullyQualifiedName=" + Escape(name, vstest)));

        return builder
            .Append(" -e XPING_ENABLED=false ")
            .Append(vstest ? "--filter " : "-- --filter ")
            .Append(ShellQuote.Of(filter, shell))
            .ToString();
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
    /// Escapes a name for a filter value.
    /// </summary>
    /// <remarks>
    /// The filter syntax reserves <c>\ ( ) &amp; | = ! ~</c> on both runners. <c>dotnet test</c> on
    /// vstest also hands the filter to MSBuild as a property, which fails on a bare <c>"</c> and
    /// decodes <c>%XX</c>, so those two are escaped for MSBuild too; NUnit's stored names quote their
    /// string arguments. <c>dotnet run</c> passes the filter to the test app as it is, and there an
    /// escaped quote does not parse.
    /// </remarks>
    private static string Escape(string name, bool throughMSBuild)
    {
        var builder = new StringBuilder(name.Length + 8);

        foreach (char c in name)
        {
            if (c is '\\' or '(' or ')' or '&' or '|' or '=' or '!' or '~')
                builder.Append('\\').Append(c);
            else if (throughMSBuild && c == '"')
                builder.Append("\\\"");
            else if (throughMSBuild && c == '%')
                builder.Append("%25");
            else
                builder.Append(c);
        }

        return builder.ToString();
    }
}
