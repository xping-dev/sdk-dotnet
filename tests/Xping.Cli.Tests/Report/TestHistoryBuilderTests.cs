/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// One test's history lists every run of the window and says what the test did in each.
/// </summary>
public sealed class TestHistoryBuilderTests
{
    private const string Target = "Target";

    private static readonly TestMatch Match = new(
        $"fp-{Target}", $"fp-{Target}", TestSessionFactory.DefaultAssembly,
        $"MyApp.Tests.SampleTests.{Target}", TestSessionFactory.Epoch, 4);

    /// <summary>
    /// Five runs, newest last: passed, absent, failed then passed on retry, timed out, skipped.
    /// </summary>
    /// <remarks>
    /// Every run carries the same four other tests, so the absent run is as large as the rest and
    /// the window's partial fallback marks none of them.
    /// </remarks>
    private static AnalysisContext Mixed() =>
        TestSessionFactory.Context(
            Run(0, [TestSessionFactory.Execution(Target, durationMs: 10)], sha: "c670fc3d1e2f"),
            Run(1, []),
            Run(
                2,
                [
                    // Recorded out of order: the history lists attempts by number, not by arrival.
                    TestSessionFactory.Execution(Target, attempt: 2, passedOnRetry: true, durationMs: 12),
                    TestSessionFactory.Execution(Target, TestOutcome.Failed, attempt: 1, durationMs: 20)
                ]),
            Run(3, [TestSessionFactory.Execution(Target, TestOutcome.Timeout, durationMs: 30)]),
            Run(4, [TestSessionFactory.Execution(Target, TestOutcome.Skipped, durationMs: 0)]));

    private static TestSession Run(int ordinal, List<TestExecution> target, string? sha = null) =>
        TestSessionFactory.Session(
            ordinal,
            [.. target, .. Enumerable.Range(0, 4).Select(i => TestSessionFactory.Execution($"Other{i}"))],
            sha: sha);

    [Fact]
    public void EverySessionOfTheWindowIsListedNewestFirstWithAbsencesMarked()
    {
        TestHistoryDto history = TestHistoryBuilder.Found(Mixed(), Target, Match);

        Assert.Equal(
            [
                TestSessionFactory.SessionIdFor(4), TestSessionFactory.SessionIdFor(3), TestSessionFactory.SessionIdFor(2),
                TestSessionFactory.SessionIdFor(1), TestSessionFactory.SessionIdFor(0)
            ],
            history.Sessions.Select(s => s.SessionId));
        Assert.Equal([true, true, true, false, true], history.Sessions.Select(s => s.Appeared));

        TestSessionDto absent = history.Sessions[3];
        Assert.Null(absent.Outcome);
        Assert.Null(absent.DecidingAttempt);
        Assert.Null(absent.DurationMs);
        Assert.Empty(absent.Attempts);
        Assert.All(history.Sessions, s => Assert.False(s.Partial));
    }

    [Fact]
    public void ARetriedRunListsEveryAttemptAndIsDecidedByTheLast()
    {
        TestSessionDto retried = TestHistoryBuilder.Found(Mixed(), Target, Match).Sessions[2];

        Assert.Equal(TestOutcome.Passed, retried.Outcome);
        Assert.Equal(2, retried.DecidingAttempt);
        Assert.Equal(12, retried.DurationMs);
        Assert.Equal(
            [new TestAttemptDto(1, TestOutcome.Failed, 20), new TestAttemptDto(2, TestOutcome.Passed, 12)],
            retried.Attempts);
    }

    [Fact]
    public void ShaIsReadFromTheSessionAndNeverInvented()
    {
        TestHistoryDto history = TestHistoryBuilder.Found(Mixed(), Target, Match);

        Assert.Equal("c670fc3d1e2f", history.Sessions[4].Sha);
        Assert.Null(history.Sessions[0].Sha);
    }

    /// <summary>
    /// A skipped run counts as ran and as other, never as an absence (OQ-1); a timeout is a failure.
    /// </summary>
    [Fact]
    public void RunsAreCountedByTheirDecidingAttempt()
    {
        TestHistoryDto history = TestHistoryBuilder.Found(Mixed(), Target, Match);

        Assert.Equal(new TestRunCountsDto(4, 5, 1, 1, 1, 1), history.Counts);
    }

    [Fact]
    public void DurationIsTakenOverTheDecidingAttemptOfRunsWithAVerdict()
    {
        TestDurationDto duration = Assert.IsType<TestDurationDto>(
            TestHistoryBuilder.Found(Mixed(), Target, Match).Duration);

        // 10, 12 and 30: not the failed first attempt's 20, and not the skipped run's 0.
        Assert.Equal(3, duration.Samples);
        Assert.Equal(12, duration.P50Ms);
        Assert.Equal(28, duration.P95Ms);
        Assert.Equal(10, duration.MinMs);
        Assert.Equal(30, duration.MaxMs);
    }

    [Fact]
    public void ATestThatNeverReachedAVerdictHasNoDuration()
    {
        AnalysisContext context = TestSessionFactory.Context(
            Run(0, [TestSessionFactory.Execution(Target, TestOutcome.Skipped)]),
            Run(1, [TestSessionFactory.Execution(Target, TestOutcome.NotExecuted)]));

        TestHistoryDto history = TestHistoryBuilder.Found(context, Target, Match);

        Assert.Null(history.Duration);
        Assert.Equal(new TestRunCountsDto(2, 2, 0, 0, 0, 2), history.Counts);
    }

    [Fact]
    public void ThePartialMarkFollowsTheWindow()
    {
        AnalysisContext context = TestSessionFactory.Context(
            Run(0, [TestSessionFactory.Execution(Target)]),
            TestSessionFactory.Reporting(
                TestSessionFactory.Session(1, [TestSessionFactory.Execution(Target)]), discovered: 5, selected: 1));

        TestHistoryDto history = TestHistoryBuilder.Found(context, Target, Match);

        Assert.Equal([true, false], history.Sessions.Select(s => s.Partial));
    }

    [Fact]
    public void TheTestIsTheSubjectAFindingAboutItWouldCarry()
    {
        SubjectDto subject = Assert.IsType<SubjectDto>(TestHistoryBuilder.Found(Mixed(), Target, Match).Test);

        Assert.Equal("test", subject.Type);
        Assert.Equal($"fp-{Target}", subject.Fingerprint);
        Assert.Equal($"MyApp.Tests.SampleTests.{Target}", subject.FullyQualifiedName);
        Assert.Equal("SampleTests.cs", subject.SourceFile);
        Assert.Equal(TestSessionFactory.DefaultAssembly, subject.Assembly);
    }

    [Fact]
    public void FindingRowsAndTheWithheldReasonAreLeftToTheEnvelope()
    {
        TestHistoryDto history = TestHistoryBuilder.Found(Mixed(), Target, Match);

        Assert.Empty(history.FindingRows);
        Assert.Null(history.FindingsWithheld);
        Assert.Empty(history.Candidates);
        Assert.Equal(Target, history.Query);
    }

    [Fact]
    public void ATestThatDidNotRunInTheWindowIsRefused()
    {
        AnalysisContext context = TestSessionFactory.Context(Run(0, []));

        Assert.Throws<ArgumentException>(() => TestHistoryBuilder.Found(context, Target, Match));
    }

    [Fact]
    public void EachCandidateNamesItsOwnAssemblyInItsCommand()
    {
        DateTime seen = TestSessionFactory.Epoch;
        var resolution = new TestResolution(
            TestResolutionKind.Ambiguous,
            "Add",
            null,
            [
                new TestMatch("3a597eb2ffff", "3a597eb2", "SampleApp.MSTest", "SampleApp.MSTest.P.Add(1,2)", seen, 3),
                new TestMatch("c4d2aa10ffff", "c4d2aa10", "SampleApp.NUnit", "SampleApp.NUnit.C.Add", seen, 2)
            ],
            2,
            5);

        TestHistoryDto history = TestHistoryBuilder.Ambiguous(
            resolution,
            new ReportScope(null, WindowResolution.Store, 5, Runs: 3, Directory: "../svc"));

        Assert.Equal(
            [
                new TestCandidateDto(
                    "3a597eb2", "SampleApp.MSTest", "SampleApp.MSTest.P.Add(1,2)", seen,
                    "xping report --test 3a597eb2 --assembly SampleApp.MSTest --directory ../svc"),
                new TestCandidateDto(
                    "c4d2aa10", "SampleApp.NUnit", "SampleApp.NUnit.C.Add", seen,
                    "xping report --test c4d2aa10 --assembly SampleApp.NUnit --directory ../svc")
            ],
            history.Candidates);

        Assert.Null(history.Test);
        Assert.Empty(history.Sessions);
        Assert.Null(history.Counts);
        Assert.Null(history.Duration);
    }

    [Fact]
    public void NotFoundCarriesTheQueryAndNothingElse()
    {
        TestHistoryDto history = TestHistoryBuilder.NotFound("Foo");

        Assert.Equal("Foo", history.Query);
        Assert.Null(history.Test);
        Assert.Empty(history.Sessions);
        Assert.Null(history.Counts);
        Assert.Null(history.Duration);
        Assert.Null(history.FindingsWithheld);
        Assert.Empty(history.FindingRows);
        Assert.Empty(history.Candidates);
    }
}
