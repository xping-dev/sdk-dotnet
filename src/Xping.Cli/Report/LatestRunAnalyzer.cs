/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;

namespace Xping.Cli.Report;

/// <summary>
/// Answers what just broke, as distinct from what is chronically unreliable.
/// </summary>
/// <remarks>
/// <para>
/// The findings answer a pattern question and need evidence gates to answer it honestly: a test
/// that passed nineteen times and failed on the newest run is below every one of them and produces
/// nothing. This reads the newest session directly and contrasts it with the sessions before it,
/// which is the one thing <c>dotnet test</c> cannot do, and it needs no gate because it claims no
/// pattern. Lowering the gates to admit it would corrupt the question they guard.
/// </para>
/// <para>
/// Not a provider. The coordinator corrects every provider's hypotheses for multiplicity and ranks
/// what survives by severity; these rows are not hypotheses and carry no severity. It runs beside
/// the coordinator, reads the same context and the findings the coordinator produced, and returns a
/// value. Nothing here reads a disk, a clock or a setting.
/// </para>
/// <para>
/// <b>No row without a contrast.</b> A row's worth is entirely the history beside it, so the
/// section is suppressed on a window of one session, and a test with no verdict in the newest
/// session — skipped, or rescued by a retry — is not a row: the first has no outcome to contrast,
/// the second has the outcome the session actually ended on.
/// </para>
/// </remarks>
internal static class LatestRunAnalyzer
{
    /// <summary>
    /// Reads the newest session against the rest of the window.
    /// </summary>
    /// <param name="context">The window under analysis.</param>
    /// <param name="findings">
    /// The findings the report will show, most severe first. A test one of them names is accounted
    /// for by it and produces no row.
    /// </param>
    /// <param name="showAll">
    /// Whether to lift the row cap. <c>--all</c> means <i>everything the report withheld</i>, and
    /// that is one meaning whether the withholding was the finding cap or this one.
    /// </param>
    /// <returns>The analysis, or <see langword="null"/> when the window holds no session.</returns>
    public static LatestRunAnalysis? Analyze(
        AnalysisContext context,
        IReadOnlyList<Finding> findings,
        bool showAll = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(findings);

        if (context.SessionViews.Count == 0)
            return null;

        SessionView newest = context.SessionViews[0];

        // Counted from the judged runs rather than copied from the view, because the view counts
        // every test the session recorded — skipped ones included — and this section's numbers
        // are verdicts. The failure count cannot differ between the two: a failing deciding attempt
        // always has a verdict.
        int executed = 0;
        int failed = 0;
        var failing = new List<(string Fingerprint, IReadOnlyList<ExecutionRef> Runs)>();

        foreach (string fingerprint in context.Tests.Fingerprints)
        {
            IReadOnlyList<ExecutionRef> runs = context.Tests.VerdictRunsOf(fingerprint);
            if (runs.Count == 0 || runs[0].SessionIndex != newest.Index)
                continue;

            executed++;

            if (!runs[0].Failed)
                continue;

            failed++;
            failing.Add((fingerprint, runs));
        }

        // One session is no history. Every row would say "first seen this run, failed", which the
        // test runner said thirty seconds ago; the counts are still published so a consumer can
        // tell suppressed from clean.
        if (context.SessionViews.Count == 1)
            return new LatestRunAnalysis(newest, true, executed, failed, 0, 0, [], [], 0);

        // The environmental heuristic exists to stop one broken dependency from poisoning every
        // test's history. A section that itemised the 187 tests it took down would defeat it, so
        // the run is described and not listed.
        if (newest.IsLikelyEnvironmental)
            return new LatestRunAnalysis(newest, false, executed, failed, 0, 0, [], [], 0);

        var explaining = new List<Finding>();
        var explainingIds = new HashSet<string>(StringComparer.Ordinal);
        var explainedFingerprints = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<LatestRunFailure>();

        foreach ((string fingerprint, IReadOnlyList<ExecutionRef> runs) in failing)
        {
            // A finding already says what this test does over the window, and says it with more
            // evidence than one session can. A row beside it would be the same test twice, with the
            // weaker statement on top.
            if (FindingsNaming(findings, fingerprint, out IReadOnlyList<Finding> named))
            {
                explainedFingerprints.Add(fingerprint);
                foreach (Finding finding in named)
                {
                    if (explainingIds.Add(finding.Id))
                        explaining.Add(finding);
                }

                continue;
            }

            int priorSessions = runs.Count - 1;
            int priorFailures = 0;
            for (int index = 1; index < runs.Count; index++)
            {
                if (runs[index].Failed)
                    priorFailures++;
            }

            LatestRunStatus status = priorSessions == 0
                ? LatestRunStatus.NewTest
                : priorFailures == 0
                    ? LatestRunStatus.New
                    : LatestRunStatus.SeenBefore;

            rows.Add(new LatestRunFailure(
                status,
                context.Tests.ReferenceFor(fingerprint)!,
                runs[0].Execution,
                priorSessions,
                priorFailures));
        }

        rows.Sort(CompareRows);

        int cap = showAll ? rows.Count : LocalAnalysisConstants.LatestRunMaxRows;
        int newFailures = rows.Count(row => row.Status != LatestRunStatus.SeenBefore);

        return new LatestRunAnalysis(
            newest,
            false,
            executed,
            failed,
            newFailures,
            explainedFingerprints.Count,
            explaining,
            rows.Count > cap ? rows.GetRange(0, cap) : rows,
            rows.Count);
    }

    /// <summary>
    /// Orders rows for reading: the regression signal first, then what is genuinely new, then what
    /// was already known to fail. Within each, the strongest contrast first.
    /// </summary>
    /// <remarks>
    /// A test that passed nineteen times and just failed outranks one that passed six; a test that
    /// failed five of eight outranks one that failed two. New tests have no count to rank on and
    /// sort by name. The fingerprint breaks every tie, so two runs over one store produce identical
    /// output whatever order the sessions were read in.
    /// </remarks>
    private static int CompareRows(LatestRunFailure left, LatestRunFailure right)
    {
        int byStatus = left.Status.CompareTo(right.Status);
        if (byStatus != 0)
            return byStatus;

        int byContrast = left.Status switch
        {
            LatestRunStatus.New => right.PriorSessions.CompareTo(left.PriorSessions),
            LatestRunStatus.SeenBefore => right.PriorFailures.CompareTo(left.PriorFailures),
            _ => string.CompareOrdinal(ShortName(left.Test), ShortName(right.Test))
        };

        return byContrast != 0
            ? byContrast
            : string.CompareOrdinal(left.Test.TestFingerprint, right.Test.TestFingerprint);
    }

    /// <summary>
    /// The name a row will print, resolved here only to sort by. The envelope resolves it again
    /// for display, through the same function, so the two cannot disagree.
    /// </summary>
    private static string ShortName(TestReference test) =>
        SubjectNames.ShortName(test.FullyQualifiedName, test.DisplayName);

    /// <summary>
    /// Finds every finding whose subject includes a test, in the order the findings were given.
    /// </summary>
    /// <param name="findings">The findings to search.</param>
    /// <param name="fingerprint">The test to look for.</param>
    /// <param name="named">The findings naming it; empty when none does.</param>
    /// <returns>Whether any finding names it.</returns>
    /// <remarks>
    /// Walked per failing test rather than indexed once, because a session that failed enough tests
    /// to make that matter is one the environmental collapse has already declined to itemise.
    /// </remarks>
    private static bool FindingsNaming(
        IReadOnlyList<Finding> findings,
        string fingerprint,
        out IReadOnlyList<Finding> named)
    {
        List<Finding>? matches = null;

        foreach (Finding finding in findings)
        {
            foreach (TestReference test in finding.Subject.Tests)
            {
                if (!string.Equals(test.TestFingerprint, fingerprint, StringComparison.Ordinal))
                    continue;

                (matches ??= []).Add(finding);
                break;
            }
        }

        named = matches ?? [];
        return matches != null;
    }
}
