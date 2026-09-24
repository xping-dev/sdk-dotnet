/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Indexes;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Statistics;

namespace Xping.Cli.Tests.Report;

public sealed class SessionViewTests
{
    private static SessionView View(int failures, int passes)
    {
        List<TestExecution> executions =
        [
            .. Enumerable.Range(0, failures).Select(i =>
                TestSessionFactory.Execution($"Fail{i}", TestOutcome.Failed, errorMessage: "boom")),
            .. Enumerable.Range(0, passes).Select(i =>
                TestSessionFactory.Execution($"Pass{i}"))
        ];

        return SessionView.For(TestSessionFactory.Session(0, executions), index: 0);
    }

    /// <summary>
    /// A timed-out test has to count as a failure everywhere a failed one does. Every "did this go
    /// wrong" check used to compare against <c>Failed</c> alone, and a check left behind would report
    /// a session that ended with a hung test as having ended green.
    /// </summary>
    [Fact]
    public void ATimedOutTestCountsAsAFailureOfItsSession()
    {
        SessionView view = SessionView.For(
            TestSessionFactory.Session(
                0,
                [
                    TestSessionFactory.Execution("Hangs", TestOutcome.Timeout, errorMessage: "killed"),
                    TestSessionFactory.Execution("Passes")
                ]),
            index: 0);

        Assert.Equal(2, view.Tests);
        Assert.Equal(1, view.Failures);
    }

    [Fact]
    public void ASessionThatFailedWidelyAndOftenIsFlaggedEnvironmental()
    {
        // 12 of 30 tests: over the rate, over the count.
        SessionView view = View(failures: 12, passes: 18);

        Assert.True(view.IsLikelyEnvironmental);
        Assert.Equal(30, view.Tests);
        Assert.Equal(12, view.Failures);
    }

    [Fact]
    public void ExactlyAtBothThresholdsCounts()
    {
        // 10 of 33 is 0.303 — the first rate above 0.30 that also clears the minimum count.
        SessionView view = View(failures: 10, passes: 23);

        Assert.True(view.IsLikelyEnvironmental);
    }

    [Fact]
    public void BelowTheRateIsNotEnvironmentalHoweverManyFailed()
    {
        // 20 failures, but only a fifth of the suite. That is a bad day for twenty tests, not for
        // the machine.
        SessionView view = View(failures: 20, passes: 80);

        Assert.False(view.IsLikelyEnvironmental);
    }

    [Fact]
    public void BelowTheFailureCountIsNotEnvironmentalHoweverHighTheRate()
    {
        // The guard that stops a tiny suite being called an outage: 4 of 5 is 80% and means nothing.
        SessionView view = View(failures: 4, passes: 1);

        Assert.False(view.IsLikelyEnvironmental);
        Assert.Equal(0.8, view.FailureRate);
    }

    [Fact]
    public void OneFailureShortOfTheCountIsNotEnvironmental()
    {
        SessionView view = View(failures: 9, passes: 11);

        Assert.False(view.IsLikelyEnvironmental);
    }

    [Fact]
    public void ATestThatPassedOnRetryDoesNotCountAgainstItsSession()
    {
        // Judged on the last attempt, like every other question about how a session ended.
        TestSession session = TestSessionFactory.Session(
            0,
            [
                TestSessionFactory.Execution("Alpha", TestOutcome.Failed, attempt: 1, errorMessage: "boom"),
                TestSessionFactory.Execution("Alpha", TestOutcome.Passed, attempt: 2, passedOnRetry: true)
            ]);

        SessionView view = SessionView.For(session, index: 0);

        Assert.Equal(1, view.Tests);
        Assert.Equal(0, view.Failures);
    }

    [Fact]
    public void AnEmptySessionHasNoFailureRateRatherThanADivisionByZero()
    {
        SessionView view = SessionView.For(TestSessionFactory.Session(0, []), index: 0);

        Assert.Equal(0, view.FailureRate);
        Assert.False(view.IsLikelyEnvironmental);
    }

    [Fact]
    public void PartialityIsAPropertyOfTheWindowAndNotOfTheSession()
    {
        // The same run, measured twice. Six tests is the whole suite in one window and a tenth of it
        // in another, and only the window can say which — which is why SessionView.For leaves the
        // flag alone and AnalysisContext sets it.
        TestSession small = TestSessionFactory.Session(1, [.. Names(6)]);

        AnalysisContext alone = TestSessionFactory.Context(
            TestSessionFactory.Session(0, [.. Names(6)]), small);

        AnalysisContext beside = TestSessionFactory.Context(
            TestSessionFactory.Session(0, [.. Names(60)]), small);

        Assert.False(ViewOf(alone, small).IsPartial);
        Assert.Equal(0, alone.PartialSessionCount);

        Assert.True(ViewOf(beside, small).IsPartial);
        Assert.Equal(1, beside.PartialSessionCount);
    }

    [Fact]
    public void ARunCoveringExactlyHalfTheSuiteIsStillARunOfIt()
    {
        // The threshold is a floor the session has to fall below, not one it has to clear. Half is
        // the last size that still counts, so the boundary is pinned rather than left to a rounding
        // argument in a review.
        TestSession half = TestSessionFactory.Session(1, [.. Names(5)]);

        AnalysisContext context = TestSessionFactory.Context(
            TestSessionFactory.Session(0, [.. Names(10)]), half);

        Assert.False(ViewOf(context, half).IsPartial);
    }

    [Fact]
    public void AWindowInWhichNothingRanHasNoSuiteToBePartOf()
    {
        // Every session empty makes the anchor zero, and a share of zero is a division nobody wants
        // to be surprised by.
        AnalysisContext context = TestSessionFactory.Context(
            TestSessionFactory.Session(0, []), TestSessionFactory.Session(1, []));

        Assert.Equal(0, context.PartialSessionCount);
    }

    [Fact]
    public void AReportedFilterIsPartialHoweverMuchOfTheSuiteItRan()
    {
        // Sixteen of seventeen clears the half-suite threshold by a mile, and the count alone would
        // call it a run of the suite. The framework said it discovered seventeen and ran sixteen,
        // so the seventeenth was never asked about.
        TestSession filtered = TestSessionFactory.Reporting(
            TestSessionFactory.Session(1, [.. Names(16)]), discovered: 17, selected: 16);

        AnalysisContext context = TestSessionFactory.Context(
            TestSessionFactory.Session(0, [.. Names(17)]), filtered);

        Assert.True(ViewOf(context, filtered).IsPartial);
        Assert.Equal(1, context.PartialSessionCount);
    }

    [Fact]
    public void AReportedFullRunIsNotPartialHoweverSmallItIs()
    {
        // The deletion the threshold cannot see: one test left of seventeen. The framework
        // discovered one and ran one, so the sixteen are gone rather than unselected.
        TestSession afterDeletion = TestSessionFactory.Reporting(
            TestSessionFactory.Session(1, [.. Names(1)]), discovered: 1, selected: 1);

        AnalysisContext context = TestSessionFactory.Context(
            TestSessionFactory.Session(0, [.. Names(17)]), afterDeletion);

        Assert.False(ViewOf(context, afterDeletion).IsPartial);
        Assert.Equal(0, context.PartialSessionCount);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(17, null)]
    public void OneCountWithoutTheOtherFallsBackToTheThreshold(int? discovered, int? selected)
    {
        // An IDE running tests it discovered earlier reports a selection and no discovery. Half a
        // fact is no fact, so the session is judged as though it reported nothing.
        TestSession small = TestSessionFactory.Reporting(
            TestSessionFactory.Session(1, [.. Names(1)]), discovered, selected);

        AnalysisContext context = TestSessionFactory.Context(
            TestSessionFactory.Session(0, [.. Names(17)]), small);

        Assert.True(ViewOf(context, small).IsPartial);
    }

    [Fact]
    public void ABreakdownOfSeveralAssembliesIsNotReadAsAFact()
    {
        // Every session in a window is projected onto one assembly. One that was not carries
        // several entries, and which of them answers for the window is not something to guess.
        TestSession unprojected = TestSessionFactory.Session(0, [.. Names(1)]);
        unprojected = new TestSession
        {
            SessionId = unprojected.SessionId,
            StartedAt = unprojected.StartedAt,
            EnvironmentInfo = unprojected.EnvironmentInfo,
            Executions = unprojected.Executions,
            Assemblies = unprojected.Assemblies,
            StatisticsByAssembly = new Dictionary<string, AssemblyStatistics>
            {
                ["A.Tests"] = new() { DiscoveredTestCases = 1, SelectedTestCases = 1 },
                ["B.Tests"] = new() { DiscoveredTestCases = 9, SelectedTestCases = 1 }
            }
        };

        Assert.Null(SessionView.For(unprojected, index: 0).ReportedPartial);
    }

    private static IEnumerable<string> Names(int count) =>
        Enumerable.Range(0, count).Select(i => $"T{i:00}");

    private static SessionView ViewOf(AnalysisContext context, TestSession session) =>
        context.SessionViewFor(session.SessionId)!;
}
