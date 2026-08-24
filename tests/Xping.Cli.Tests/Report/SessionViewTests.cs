/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Indexes;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

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
}
