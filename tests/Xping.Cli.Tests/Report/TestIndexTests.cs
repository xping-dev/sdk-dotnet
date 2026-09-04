/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

public sealed class TestIndexTests
{
    private const string Subject = "Alpha";
    private const string SubjectFingerprint = "fp-Alpha";

    /// <summary>
    /// Builds a window in which <see cref="Subject"/> ran in the newest <paramref name="presentIn"/>
    /// sessions, each run taking <paramref name="attempts"/> attempts to finish.
    /// </summary>
    /// <remarks>
    /// A second test runs in every session so that the window never shrinks to the sessions the
    /// subject appeared in — the denominator has to stay the whole window for the ratio to mean
    /// anything.
    /// </remarks>
    private static AnalysisWindow Window(int total, int presentIn, int attempts)
    {
        var sessions = new List<TestSession>();

        for (int ordinal = 0; ordinal < total; ordinal++)
        {
            var executions = new List<TestExecution> { TestSessionFactory.Execution("Stable") };

            // Highest ordinal is newest, so the subject's runs are the most recent sessions.
            if (ordinal >= total - presentIn)
            {
                for (int attempt = 1; attempt <= attempts; attempt++)
                {
                    executions.Add(TestSessionFactory.Execution(
                        Subject,
                        outcome: attempt == attempts ? TestOutcome.Passed : TestOutcome.Failed,
                        attempt: attempt,
                        passedOnRetry: attempt == attempts && attempts > 1,
                        maxRetries: attempts - 1,
                        errorMessage: attempt == attempts ? null : "boom"));
                }
            }

            sessions.Add(TestSessionFactory.Session(ordinal, executions));
        }

        return TestSessionFactory.Window([.. sessions]);
    }

    [Fact]
    public void RunFrequencyCountsSessionsRatherThanAttempts()
    {
        // The bug this pins: three attempts in half the sessions is thirty executions over twenty
        // sessions, which read as a test that runs on every build.
        TestIndex index = TestIndex.Build(Window(total: 20, presentIn: 10, attempts: 3));

        Assert.Equal(30, index.ExecutionsOf(SubjectFingerprint).Count);
        Assert.Equal(0.50, index.RunFrequencyOf(SubjectFingerprint));
    }

    [Fact]
    public void RunFrequencyIsOneForATestThatRunsInEverySession()
    {
        TestIndex index = TestIndex.Build(Window(total: 20, presentIn: 20, attempts: 1));

        Assert.Equal(1.0, index.RunFrequencyOf(SubjectFingerprint));
    }

    [Fact]
    public void RunFrequencyIsStillOneWhenEverySessionRetries()
    {
        // Retries must not be what pushes the value to one: a test that genuinely runs everywhere
        // has to be indistinguishable from itself with retries switched on.
        TestIndex index = TestIndex.Build(Window(total: 20, presentIn: 20, attempts: 3));

        Assert.Equal(1.0, index.RunFrequencyOf(SubjectFingerprint));
    }

    [Fact]
    public void ATestThatRetriesFourTimesInAQuarterOfSessionsDoesNotOutrankOneThatRunsEverywhere()
    {
        // The ranking consequence, stated as the issue states it: before the fix both read 1.0.
        TestIndex index = TestIndex.Build(Window(total: 20, presentIn: 5, attempts: 4));

        Assert.Equal(0.25, index.RunFrequencyOf(SubjectFingerprint));
        Assert.Equal(1.0, index.RunFrequencyOf("fp-Stable"));
    }

    [Fact]
    public void RunFrequencyIsZeroForAFingerprintTheWindowNeverSaw()
    {
        TestIndex index = TestIndex.Build(Window(total: 20, presentIn: 10, attempts: 1));

        Assert.Equal(0, index.RunFrequencyOf("fp-NeverRan"));
    }

    [Fact]
    public void RunFrequencyIsZeroOverAnEmptyWindow()
    {
        AnalysisWindow empty = AnalysisWindow.Create(
            [], TestSessionFactory.Epoch, TestSessionFactory.Epoch, WindowResolution.Default, null);

        Assert.Equal(0, TestIndex.Build(empty).RunFrequencyOf(SubjectFingerprint));
    }

    // ---------------------------------------------------------------------------------------
    // Runs and the per-test session count
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void SessionsRunInCountsSessionsRatherThanAttempts()
    {
        TestIndex index = TestIndex.Build(Window(total: 20, presentIn: 10, attempts: 3));

        Assert.Equal(30, index.ExecutionsOf(SubjectFingerprint).Count);
        Assert.Equal(10, index.SessionsRunIn(SubjectFingerprint));
    }

    [Fact]
    public void SessionsRunInIsZeroForAFingerprintTheWindowNeverSaw()
    {
        TestIndex index = TestIndex.Build(Window(total: 20, presentIn: 10, attempts: 1));

        Assert.Equal(0, index.SessionsRunIn("fp-NeverRan"));
    }

    [Fact]
    public void RunsOfCollapsesEverySessionToOneEntry()
    {
        // The distinction the arm gates and the evidence floor now rest on: thirty executions are
        // ten occasions, and a gate handed the first number is claiming a sample it has not got.
        TestIndex index = TestIndex.Build(Window(total: 20, presentIn: 10, attempts: 3));

        IReadOnlyList<ExecutionRef> runs = index.RunsOf(SubjectFingerprint);

        Assert.Equal(10, runs.Count);
        Assert.Equal(10, runs.Select(r => r.SessionIndex).Distinct().Count());
    }

    [Fact]
    public void ARunIsRepresentedByItsDecidingAttempt()
    {
        // The fixture fails twice and passes on the third, so the run passed. Reading any earlier
        // attempt would make every retried run look like a failure and invert the whole report.
        TestIndex index = TestIndex.Build(Window(total: 6, presentIn: 6, attempts: 3));

        foreach (ExecutionRef run in index.RunsOf(SubjectFingerprint))
        {
            Assert.Equal(3, run.Execution.Retry?.AttemptNumber);
            Assert.Equal(TestOutcome.Passed, run.Execution.Outcome);
            Assert.False(run.Failed);
        }
    }

    [Fact]
    public void ARunAgreesWithSessionOutcomesWhenAttemptsArriveOutOfOrder()
    {
        // Attempt order within a session is not guaranteed. The two must not disagree, or the report
        // would call a session green while flagging a test inside it as having blocked the build.
        TestSession session = TestSessionFactory.Session(
            0,
            [
                TestSessionFactory.Execution(
                    Subject, TestOutcome.Failed, attempt: 2, maxRetries: 1, errorMessage: "boom"),
                TestSessionFactory.Execution(Subject, TestOutcome.Passed, attempt: 1)
            ]);

        ExecutionRef run = Assert.Single(
            TestIndex.Build(TestSessionFactory.Window(session)).RunsOf(SubjectFingerprint));

        Assert.True(run.Failed);
        Assert.True(SessionOutcomes.HasFinalFailure(session));
    }

    [Fact]
    public void RunsOfIsEmptyForAFingerprintTheWindowNeverSaw()
    {
        TestIndex index = TestIndex.Build(Window(total: 20, presentIn: 10, attempts: 1));

        Assert.Empty(index.RunsOf("fp-NeverRan"));
    }

    [Fact]
    public void RunsAreOrderedNewestSessionFirstLikeExecutions()
    {
        TestIndex index = TestIndex.Build(Window(total: 8, presentIn: 8, attempts: 2));

        IReadOnlyList<ExecutionRef> runs = index.RunsOf(SubjectFingerprint);

        Assert.Equal(Enumerable.Range(0, 8), runs.Select(r => r.SessionIndex));
    }

    // ---------------------------------------------------------------------------------------
    // Recency
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a window of <paramref name="total"/> sessions spaced <paramref name="apart"/> apart.
    /// </summary>
    /// <param name="total">How many sessions to build.</param>
    /// <param name="apart">The gap between one session's start and the next.</param>
    /// <returns>The sessions, for a caller to window and index.</returns>
    private static List<TestSession> Spaced(int total, TimeSpan apart)
    {
        var sessions = new List<TestSession>();

        for (int ordinal = 0; ordinal < total; ordinal++)
        {
            sessions.Add(TestSessionFactory.Session(
                ordinal,
                [TestSessionFactory.Execution(Subject)],
                startedAt: TestSessionFactory.Epoch + (apart * ordinal)));
        }

        return sessions;
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 0.794)]
    [InlineData(3, 0.5)]
    [InlineData(7, 0.198)]
    [InlineData(14, 0.039)]
    public void RecencyHalvesEveryThreeDays(int days, double expected)
    {
        double actual = TestIndex.Recency(
            TimeSpan.FromDays(days), TimeSpan.FromDays(30), sessionsSinceLastOccurrence: 0);

        Assert.Equal(expected, actual, 3);
    }

    [Fact]
    public void TwentySessionsInsideOneAfternoonAreAllFresh()
    {
        // The tight inner loop: `dotnet watch test` fills a window before the afternoon is out.
        // Nothing in it is stale, so the term should barely separate the ends of it — where the
        // session index separated them by 0.93, scoring the oldest of these at 0.07 as though the
        // morning had been a fortnight ago.
        TestIndex index = TestIndex.Build(
            TestSessionFactory.Window([.. Spaced(20, TimeSpan.FromMinutes(20))]));

        IReadOnlyList<TestSession> sessions = index.Window.Sessions;

        Assert.Equal(1.0, Recency(index, sessions[0]), 3);
        Assert.Equal(0.941, Recency(index, sessions[^1]), 3);

        foreach (TestSession session in sessions)
            Assert.True(Recency(index, session) > 0.93, $"{session.StartedAt:o}");
    }

    [Fact]
    public void TwentySessionsAcrossThreeWeeksDecayTheOldestToNearlyNothing()
    {
        // The same twenty sessions, on a CI cadence. Identical indices, and the oldest of them is
        // three weeks old rather than six hours.
        TestIndex index = TestIndex.Build(
            TestSessionFactory.Window([.. Spaced(20, TimeSpan.FromDays(1.1))]));

        IReadOnlyList<TestSession> sessions = index.Window.Sessions;

        Assert.Equal(1.0, Recency(index, sessions[0]), 3);
        Assert.True(Recency(index, sessions[^1]) < 0.01);
    }

    [Fact]
    public void AnOccurrenceEightDaysBackDecaysPastWhatItsSessionIndexWouldRead()
    {
        // The regression test for the floor this deliberately does not apply. Session index five
        // reads 0.50; eight days reads 0.16. Taking the greater of the two — the belt-and-braces
        // #172 floated — would reinstate the over-weighting in the sparse direction that the whole
        // change exists to remove, so the time reading has to stand alone.
        TestIndex index = TestIndex.Build(
            TestSessionFactory.Window([.. Spaced(20, TimeSpan.FromDays(1.6))]));

        double actual = Recency(index, index.Window.Sessions[5]);

        Assert.Equal(0.157, actual, 3);
        Assert.True(actual < 0.5, $"{actual} is not below the session-index reading");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(30)]
    public void AStampTheWindowContradictsFallsBackToTheSessionIndex(int elapsedDays)
    {
        // Negative, or older than the window that contains it. Either way the store's clock has
        // said something the window's own boundaries deny, and index is the only reading left.
        double actual = TestIndex.Recency(
            TimeSpan.FromDays(elapsedDays), TimeSpan.FromDays(14), sessionsSinceLastOccurrence: 5);

        Assert.Equal(0.5, actual, 3);
    }

    [Fact]
    public void ASessionTheWindowNeverSawDoesNotDecayUpwards()
    {
        // PositionOf answers -1 for a stranger, which unclamped is a negative exponent and a score
        // above 1.0 — better than the newest run in the window. The scorer's Clamp would hide it.
        double actual = TestIndex.Recency(
            TimeSpan.FromDays(-1), TimeSpan.FromDays(14), sessionsSinceLastOccurrence: -1);

        Assert.Equal(1.0, actual);
    }

    [Fact]
    public void RecencyIsDatedAgainstTheWindowAndNeverAgainstTheClock()
    {
        // The fixture epoch is a fixed instant, so it recedes further into the past with every day
        // that passes. Read against a clock the newest session here would decay to nothing; read
        // against the window it is what "now" means, and two runs over an unchanged store agree.
        TestIndex index = TestIndex.Build(
            TestSessionFactory.Window([.. Spaced(5, TimeSpan.FromDays(1))]));

        Assert.Equal(1.0, Recency(index, index.Window.Sessions[0]));
    }

    /// <summary>
    /// Scores one session the way <c>ImpactScorer</c> does.
    /// </summary>
    /// <param name="index">The index the session belongs to.</param>
    /// <param name="session">The session a finding was last seen in.</param>
    /// <returns>Its recency.</returns>
    private static double Recency(TestIndex index, TestSession session) =>
        TestIndex.Recency(
            index.Window.To - session.StartedAt,
            index.Window.To - index.Window.From,
            index.PositionOf(session.SessionId));
}
