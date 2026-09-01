/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

public sealed class FailureModeProviderTests
{
    private const string SharedType = "System.Net.Sockets.SocketException";
    private const string SharedMessage = "Connection refused";

    private static readonly string[] ClusterFixtureTests =
        ["Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta"];

    private static TestExecution Passing(string name) => TestSessionFactory.Execution(name);

    /// <summary>A failure every test in a fixture can share, so it clusters.</summary>
    private static TestExecution SharedFailure(string name) =>
        TestSessionFactory.Execution(
            name, TestOutcome.Failed, exceptionType: SharedType, errorMessage: SharedMessage);

    /// <summary>The same shared failure, recorded in a named lifecycle member.</summary>
    private static TestExecution FixtureFailure(
        string name, FailureSite site = FailureSite.TestSetup, string? member = "SampleTests.Setup") =>
        TestSessionFactory.Execution(
            name,
            TestOutcome.Failed,
            exceptionType: SharedType,
            errorMessage: SharedMessage,
            failureSite: site,
            failureSiteMember: member);

    /// <summary>A second failure every test can share, distinct from <see cref="SharedFailure"/>.</summary>
    private static TestExecution OtherSharedFailure(string name) =>
        TestSessionFactory.Execution(
            name,
            TestOutcome.Failed,
            exceptionType: "System.TimeoutException",
            errorMessage: "The operation timed out");

    /// <summary>A failure whose signature is the test's own.</summary>
    private static TestExecution Failure(string name, string message = "unexpected null") =>
        TestSessionFactory.Execution(
            name,
            TestOutcome.Failed,
            exceptionType: "System.InvalidOperationException",
            errorMessage: message,
            stackTrace: $"   at MyApp.Tests.SampleTests.{name}()");

    private static List<FindingCandidate> Analyze(params TestSession[] sessions) =>
        [.. new FailureModeProvider().Analyze(TestSessionFactory.Context(sessions))];

    private static FindingCandidate Single(List<FindingCandidate> candidates, FindingKind kind) =>
        Assert.Single(candidates, c => c.Kind == kind);

    private static FindingCandidate For(List<FindingCandidate> candidates, string name) =>
        Assert.Single(
            candidates,
            c => c.Subject is FindingSubject.SingleTest test &&
                 test.Test.TestFingerprint == $"fp-{name}");

    /// <summary>
    /// Builds sessions in which one test fails in the newest <paramref name="failing"/> of them.
    /// </summary>
    private static TestSession[] Runs(int total, int failing, Func<string, TestExecution>? failure = null)
    {
        failure ??= n => Failure(n);

        return [.. Enumerable.Range(0, total).Select(ordinal =>
            TestSessionFactory.Session(
                ordinal,
                [ordinal >= total - failing ? failure("Subject") : Passing("Subject")]))];
    }

    /// <summary>A test the framework killed for overrunning the budget it declared.</summary>
    private static TestExecution TimedOut(string name, int budgetMs = 500, int durationMs = 505) =>
        TestSessionFactory.Execution(
            name,
            TestOutcome.Timeout,
            durationMs: durationMs,
            exceptionType: "Xunit.Sdk.TestTimeoutException",
            errorMessage: $"Test execution timed out after {budgetMs} milliseconds",
            timeoutBudgetMs: budgetMs);

    [Fact]
    public void ATestMostlyKilledForOverrunningItsBudgetIsTimingOut()
    {
        List<FindingCandidate> candidates = Analyze(Runs(total: 10, failing: 9, failure: n => TimedOut(n)));

        FindingCandidate candidate = Single(candidates, FindingKind.TimingOut);
        var evidence = Assert.IsType<TimingOutEvidence>(candidate.Evidence);

        Assert.Equal(9, evidence.Timeouts);
        Assert.Equal(9, evidence.Failures);
        Assert.Equal(10, evidence.Executions);
        Assert.Equal(1.0, evidence.TimeoutShareOfFailures);
        Assert.Equal(500, evidence.DeclaredBudgetMs);
        Assert.Equal(9, evidence.ObservedDurationsMs.Count);
        Assert.All(evidence.ObservedDurationsMs, ms => Assert.Equal(505, ms));
    }

    /// <summary>
    /// The whole reason the kind exists: without it these nine hangs would be reported as a broken
    /// test, with a failure signature built from a stack frame wherever the runner interrupted it.
    /// </summary>
    [Fact]
    public void ATimingOutTestIsNotAlsoReportedAsAlwaysFailingOrFlaky()
    {
        List<FindingCandidate> candidates = Analyze(Runs(total: 10, failing: 9, failure: n => TimedOut(n)));

        Assert.DoesNotContain(candidates, c => c.Kind == FindingKind.AlwaysFailing);
        Assert.DoesNotContain(candidates, c => c.Kind == FindingKind.Flaky);
    }

    /// <summary>
    /// A test that mostly disagrees with an assertion but hung once is still a failing test. Moving
    /// it into the timeout bucket would point its reader at a deadlock that is not the problem.
    /// </summary>
    [Fact]
    public void ATestFailingMostlyOnAssertionsStaysFlakyDespiteOneTimeout()
    {
        TestSession[] sessions = [.. Enumerable.Range(0, 10).Select(ordinal =>
            TestSessionFactory.Session(
                ordinal,
                [ordinal switch
                {
                    9 => TimedOut("Subject"),
                    >= 5 => Failure("Subject"),
                    _ => Passing("Subject"),
                }]))];

        List<FindingCandidate> candidates = Analyze(sessions);

        Assert.DoesNotContain(candidates, c => c.Kind == FindingKind.TimingOut);
        Single(candidates, FindingKind.Flaky);
    }

    /// <summary>
    /// A budget is only recorded when the test declared one. When it did not, the limit it hit came
    /// from a suite-wide or runner-level setting the session does not carry, and the finding says so
    /// by omitting the field rather than inventing a number.
    /// </summary>
    [Fact]
    public void ATimingOutTestWithNoDeclaredBudgetReportsNoBudget()
    {
        List<FindingCandidate> candidates = Analyze(Runs(
            total: 10,
            failing: 9,
            failure: n => TestSessionFactory.Execution(n, TestOutcome.Timeout, errorMessage: "killed")));

        var evidence = Assert.IsType<TimingOutEvidence>(Single(candidates, FindingKind.TimingOut).Evidence);

        Assert.Null(evidence.DeclaredBudgetMs);
    }

    /// <summary>
    /// The budget beside the observed duration is the reading the kind exists to publish, so it has
    /// to survive into the sentence a reader is actually shown.
    /// </summary>
    [Fact]
    public void TheTimingOutHeadlineNamesTheLimitTheTestWasKilledAt()
    {
        FindingCandidate candidate = Single(
            Analyze(Runs(total: 10, failing: 9, failure: n => TimedOut(n))), FindingKind.TimingOut);

        (string headline, IReadOnlyList<MetricDto> metrics) =
            EvidenceHeadline.For(candidate.Kind, candidate.Evidence);

        Assert.Equal("timed out 9 of 10 executions (90%) in 9 of 10 runs, killed at its 500ms limit", headline);
        Assert.Contains(metrics, m => m.Label == "declared limit" && m.Value == "500ms");

        // Only stated when the test also failed some other way; here every failure was a timeout.
        Assert.DoesNotContain(metrics, m => m.Label == "share of failures");
    }

    /// <summary>
    /// When a test both hangs and fails ordinarily, the split is the reason it was reported as
    /// timing out rather than as a plain failure, so the finding states it. It is omitted when every
    /// failure was a timeout, where "7 of 7" would be noise.
    /// </summary>
    [Fact]
    public void TheTimingOutMetricsStateTheSplitWhenTheTestAlsoFailsOtherWays()
    {
        // 8 timeouts and 1 assertion failure: over the share the condition asks of the bound, but
        // not all of them.
        TestSession[] sessions = [.. Enumerable.Range(0, 10).Select(ordinal =>
            TestSessionFactory.Session(
                ordinal,
                [ordinal switch
                {
                    >= 2 => TimedOut("Subject"),
                    >= 1 => Failure("Subject"),
                    _ => Passing("Subject"),
                }]))];

        FindingCandidate candidate = Single(Analyze(sessions), FindingKind.TimingOut);
        var evidence = Assert.IsType<TimingOutEvidence>(candidate.Evidence);

        Assert.Equal(8, evidence.Timeouts);
        Assert.Equal(9, evidence.Failures);

        (_, IReadOnlyList<MetricDto> metrics) = EvidenceHeadline.For(candidate.Kind, candidate.Evidence);

        Assert.Contains(metrics, m => m.Label == "share of failures" && m.Value == "8 of 9 (88.9%)");
    }

    [Fact]
    public void TheTimingOutHeadlineSaysSoWhenTheTestDeclaredNoLimit()
    {
        FindingCandidate candidate = Single(
            Analyze(Runs(
                total: 10,
                failing: 9,
                failure: n => TestSessionFactory.Execution(n, TestOutcome.Timeout, errorMessage: "killed"))),
            FindingKind.TimingOut);

        (string headline, IReadOnlyList<MetricDto> metrics) =
            EvidenceHeadline.For(candidate.Kind, candidate.Evidence);

        Assert.DoesNotContain("limit", headline, StringComparison.Ordinal);
        Assert.Contains(metrics, m => m.Label == "declared limit" && m.Value == "none declared by the test");
    }

    [Fact]
    public void ATestFailingAlmostAlwaysTheSameWayIsAlwaysFailing()
    {
        List<FindingCandidate> candidates = Analyze(Runs(total: 10, failing: 9));

        FindingCandidate candidate = Single(candidates, FindingKind.AlwaysFailing);
        var evidence = Assert.IsType<AlwaysFailingEvidence>(candidate.Evidence);

        Assert.Equal(9, evidence.Failures);
        Assert.Equal(10, evidence.Executions);

        // The published rate is the point estimate; the ranking term is its lower bound. Nine of
        // ten is 0.90 either way to a reader, and 0.60 against the other findings in the report.
        Assert.Equal(0.9, evidence.FailureRate);
        Assert.Equal(0.596, candidate.Unreliability, 3);
    }

    [Fact]
    public void OneRunShortOfTheThresholdIsFlakyInstead()
    {
        // 8 of 10. The band is where a broken test stops being a flaky one, and it has to be tested
        // from both sides or the comparison could be inverted and nothing would notice.
        List<FindingCandidate> candidates = Analyze(Runs(total: 10, failing: 8));

        FindingCandidate candidate = Single(candidates, FindingKind.Flaky);
        var evidence = Assert.IsType<FlakyEvidence>(candidate.Evidence);

        Assert.Equal(0.8, evidence.FailureRate);
        Assert.Equal(1, evidence.DistinctSignatureCount);
    }

    [Fact]
    public void ATestThatFailsEveryRunTheSameWayIsAlwaysFailing()
    {
        List<FindingCandidate> candidates = Analyze(Runs(total: 8, failing: 8));

        var evidence = Assert.IsType<AlwaysFailingEvidence>(
            Single(candidates, FindingKind.AlwaysFailing).Evidence);

        Assert.Equal(1.0, evidence.FailureRate);
        Assert.Null(evidence.Contrast);
    }

    [Fact]
    public void ATestThatFailsEveryRunADifferentWayIsFlaky()
    {
        // The entropy arm. The rate says broken; the varying failure mode says something else is
        // going on, and the specification calls that flaky whatever the rate.
        TestSession[] sessions = [.. Enumerable.Range(0, 8).Select(ordinal =>
            TestSessionFactory.Session(ordinal, [Failure("Subject", $"unexpected {(char)('a' + ordinal)}")]))];

        List<FindingCandidate> candidates = Analyze(sessions);

        var evidence = Assert.IsType<FlakyEvidence>(Single(candidates, FindingKind.Flaky).Evidence);

        Assert.Equal(1.0, evidence.FailureRate);
        Assert.Equal(8, evidence.DistinctSignatureCount);
    }

    [Fact]
    public void UnreliabilityPeaksAtAnEvenSplit()
    {
        // The tent is evaluated at the lower bound of the failure rate, so the peak is approached
        // rather than reached: an even split over ten runs is worth less than the same split over
        // forty, and neither claims the certainty the raw ratio used to hand both of them.
        double ten = Single(Analyze(Runs(total: 10, failing: 5)), FindingKind.Flaky).Unreliability;
        double forty = Single(Analyze(Runs(total: 40, failing: 20)), FindingKind.Flaky).Unreliability;
        double lopsided = Single(Analyze(Runs(total: 40, failing: 8)), FindingKind.Flaky).Unreliability;

        Assert.Equal(0.473, ten, 3);
        Assert.Equal(0.704, forty, 3);
        Assert.True(forty > lopsided, $"{forty} > {lopsided}");
    }

    [Fact]
    public void UnreliabilityRisesWithTheRunsBehindTheSameRate()
    {
        // The property the whole ranking rests on: two tests failing half their runs are not the
        // same finding when one of them ran five times and the other forty.
        double[] scores =
        [
            Single(Analyze(Runs(total: 10, failing: 5)), FindingKind.Flaky).Unreliability,
            Single(Analyze(Runs(total: 20, failing: 10)), FindingKind.Flaky).Unreliability,
            Single(Analyze(Runs(total: 40, failing: 20)), FindingKind.Flaky).Unreliability
        ];

        for (int i = 1; i < scores.Length; i++)
            Assert.True(scores[i] > scores[i - 1], $"{scores[i]} > {scores[i - 1]}");
    }

    /// <summary>Repeats one assertion message, so several failures share one signature.</summary>
    private static string[] Said(int times, string message) =>
        [.. Enumerable.Repeat(message, times)];

    /// <summary>
    /// Builds sessions in which one test passes <paramref name="passing"/> times and then fails
    /// once per entry in <paramref name="messages"/>, newest last.
    /// </summary>
    private static TestSession[] RunsFailingWith(int passing, params string[] messages) =>
    [
        .. Enumerable.Range(0, passing)
            .Select(ordinal => TestSessionFactory.Session(ordinal, [Passing("Subject")])),
        .. messages.Select((message, index) =>
            TestSessionFactory.Session(passing + index, [Failure("Subject", message)]))
    ];

    [Fact]
    public void ABrokenTestWhoseMessageNamesTheDataIsStillAlwaysFailing()
    {
        // The defect this pair of thresholds exists for. Signatures compare messages exactly, so an
        // assertion that prints the value it saw produces one mode per value; requiring a single
        // mode demoted a test failing 19 runs in 20 to flaky over a name in a string.
        TestSession[] sessions = RunsFailingWith(
            passing: 1,
            [
                .. Said(17, "Expected: \"Alice\" but was: \"Bob\""),
                .. Said(2, "Expected: \"Alice\" but was: \"Carol\"")
            ]);

        FindingCandidate candidate = Single(Analyze(sessions), FindingKind.AlwaysFailing);
        var evidence = Assert.IsType<AlwaysFailingEvidence>(candidate.Evidence);

        Assert.Equal(19, evidence.Failures);
        Assert.Equal(0.95, evidence.FailureRate);
        Assert.Equal(0.895, evidence.ModalSignatureShare);
        Assert.Equal(17, evidence.Signature.Occurrences);
        Assert.Equal(0.764, candidate.Unreliability, 3);
    }

    [Fact]
    public void ATestFailingHalfTheTimeInTenWaysIsStillFlaky()
    {
        // The other side of the same judgement. A dominant mode is what separates a broken test from
        // a flaky one, and ten modes across ten failures is not one.
        TestSession[] sessions = RunsFailingWith(
            passing: 10,
            [.. Enumerable.Range(0, 10).Select(i => $"unexpected {(char)('a' + i)}")]);

        var evidence = Assert.IsType<FlakyEvidence>(Single(Analyze(sessions), FindingKind.Flaky).Evidence);

        Assert.Equal(0.5, evidence.FailureRate);
        Assert.Equal(10, evidence.DistinctSignatureCount);
    }

    [Fact]
    public void AtTheDominantShareTheTestIsBrokenAndBelowItFlaky()
    {
        // The band is tested from both sides, as the rate is: 14 of 20 failures agreeing is the
        // threshold itself, 13 is one short of it, and a comparison written the wrong way round
        // would pass one of these and not the other.
        TestSession[] atThreshold = RunsFailingWith(
            passing: 0,
            [.. Said(14, "the cart was not empty"), .. Said(6, "the cart held a stale item")]);

        TestSession[] belowThreshold = RunsFailingWith(
            passing: 0,
            [.. Said(13, "the cart was not empty"), .. Said(7, "the cart held a stale item")]);

        var broken = Assert.IsType<AlwaysFailingEvidence>(
            Single(Analyze(atThreshold), FindingKind.AlwaysFailing).Evidence);

        Assert.Equal(0.7, broken.ModalSignatureShare);

        var flaky = Assert.IsType<FlakyEvidence>(
            Single(Analyze(belowThreshold), FindingKind.Flaky).Evidence);

        Assert.Equal(2, flaky.DistinctSignatureCount);
    }

    [Fact]
    public void ATestThatFailsOneWayOnlyPublishesTheWholeShare()
    {
        var evidence = Assert.IsType<AlwaysFailingEvidence>(
            Single(Analyze(Runs(total: 10, failing: 9)), FindingKind.AlwaysFailing).Evidence);

        Assert.Equal(1.0, evidence.ModalSignatureShare);
    }

    [Fact]
    public void UnreliabilityNeverFallsBelowTheFailureRate()
    {
        // The tent alone scored a test failing 19 of 20 runs at 0.10 against a coin flip's 1.00,
        // costing the most broken tests in a suite 0.34 of their impact for being more broken. Ten
        // distinct modes keep this one flaky, which is exactly where the collapse used to happen.
        TestSession[] sessions = RunsFailingWith(
            passing: 1,
            [
                .. Enumerable.Range(0, 10).Select(i => $"unexpected {(char)('a' + i)}"),
                .. Said(9, "unexpected a")
            ]);

        FindingCandidate candidate = Single(Analyze(sessions), FindingKind.Flaky);
        var evidence = Assert.IsType<FlakyEvidence>(candidate.Evidence);

        Assert.Equal(0.95, evidence.FailureRate);
        Assert.Equal(10, evidence.DistinctSignatureCount);

        // The floor still holds, now against the bounded rate: the tent alone would put the lower
        // bound of 19 of 20 at 0.47, and the floor keeps the term at the bound itself.
        Assert.Equal(0.764, candidate.Unreliability, 3);
    }

    [Fact]
    public void TheDominantSignatureIsTheOneMostFailuresCarried()
    {
        // Ordered on the counts that survived discounting rather than the index's window-wide ones,
        // because the head of this list is read as the mode the test meets most often — by the
        // classification above it and by whoever opens the report.
        TestSession[] sessions = RunsFailingWith(
            passing: 0,
            [.. Said(3, "the cart held a stale item"), .. Said(17, "the cart was not empty")]);

        var evidence = Assert.IsType<AlwaysFailingEvidence>(
            Single(Analyze(sessions), FindingKind.AlwaysFailing).Evidence);

        Assert.Equal(17, evidence.Signature.Occurrences);
        Assert.Equal("the cart was not empty", evidence.Signature.Message);
    }

    [Fact]
    public void ATestThatNeverFailsProducesNothing() =>
        Assert.Empty(Analyze(Runs(total: 6, failing: 0)));

    [Fact]
    public void ThreeTestsFailingAlikeInOneRunBecomeOneFinding()
    {
        // The behaviour the whole report is for: three failures, one cause.
        TestSession[] sessions =
        [
            TestSessionFactory.Session(0, [Passing("Alpha"), Passing("Beta"), Passing("Gamma")]),
            TestSessionFactory.Session(1, [Passing("Alpha"), Passing("Beta"), Passing("Gamma")]),
            TestSessionFactory.Session(2, [Passing("Alpha"), Passing("Beta"), Passing("Gamma")]),
            TestSessionFactory.Session(3, [Passing("Alpha"), Passing("Beta"), Passing("Gamma")]),
            TestSessionFactory.Session(
                4, [SharedFailure("Alpha"), SharedFailure("Beta"), SharedFailure("Gamma")])
        ];

        List<FindingCandidate> candidates = Analyze(sessions);

        FindingCandidate candidate = Single(candidates, FindingKind.SharedFailure);
        var evidence = Assert.IsType<SharedFailureEvidence>(candidate.Evidence);

        Assert.Equal(3, evidence.MemberCount);
        Assert.Equal(3, evidence.MaxTestsInOneSession);
        Assert.Equal(
            ["fp-Alpha", "fp-Beta", "fp-Gamma"],
            evidence.Members.Select(m => m.Fingerprint));
    }

    /// <summary>
    /// The point of the failure site: the same three failures, but every one of them recorded in the
    /// same setup method, so the report can name the member to fix instead of listing three tests.
    /// </summary>
    [Fact]
    public void ThreeTestsBlockedByOneLifecycleMemberNameIt()
    {
        TestSession[] sessions =
        [
            .. FourQuietRuns(),
            TestSessionFactory.Session(
                4, [FixtureFailure("Alpha"), FixtureFailure("Beta"), FixtureFailure("Gamma")])
        ];

        FindingCandidate candidate = Single(Analyze(sessions), FindingKind.BrokenFixture);
        var evidence = Assert.IsType<BrokenFixtureEvidence>(candidate.Evidence);

        Assert.Equal("SampleTests.Setup", evidence.Member);
        Assert.Equal(nameof(FailureSite.TestSetup), evidence.Site);
        Assert.Equal(3, evidence.TestsBlocked);
        Assert.Equal(
            ["fp-Alpha", "fp-Beta", "fp-Gamma"],
            evidence.Members.Select(m => m.Fingerprint));
    }

    /// <summary>
    /// A broken fixture replaces the shared failure rather than joining it. Reporting both would
    /// charge one cause twice — the thing clustering exists to prevent.
    /// </summary>
    [Fact]
    public void ABrokenFixtureIsNotAlsoReportedAsASharedFailure()
    {
        TestSession[] sessions =
        [
            .. FourQuietRuns(),
            TestSessionFactory.Session(
                4, [FixtureFailure("Alpha"), FixtureFailure("Beta"), FixtureFailure("Gamma")])
        ];

        List<FindingCandidate> candidates = Analyze(sessions);

        Assert.DoesNotContain(candidates, c => c.Kind == FindingKind.SharedFailure);
        Assert.Single(candidates);
    }

    /// <summary>
    /// Two different broken setup methods failing with one signature are two defects. Naming either
    /// would send the reader to the wrong one, so the finding claims only what it can support.
    /// </summary>
    [Fact]
    public void FailuresDisagreeingOnTheMemberStayASharedFailure()
    {
        TestSession[] sessions =
        [
            .. FourQuietRuns(),
            TestSessionFactory.Session(
                4,
                [
                    FixtureFailure("Alpha", member: "SampleTests.Setup"),
                    FixtureFailure("Beta", member: "OtherTests.Setup"),
                    FixtureFailure("Gamma", member: "SampleTests.Setup")
                ])
        ];

        List<FindingCandidate> candidates = Analyze(sessions);

        Assert.Single(candidates, c => c.Kind == FindingKind.SharedFailure);
        Assert.DoesNotContain(candidates, c => c.Kind == FindingKind.BrokenFixture);
    }

    /// <summary>
    /// One failure the adapter could not place is enough to withhold the claim. Sites are all-or
    /// nothing here because the finding points at a line of code, and a majority vote is not evidence
    /// that the minority came from the same place.
    /// </summary>
    [Fact]
    public void OneFailureWithoutASiteKeepsTheClusterASharedFailure()
    {
        TestSession[] sessions =
        [
            .. FourQuietRuns(),
            TestSessionFactory.Session(
                4, [FixtureFailure("Alpha"), FixtureFailure("Beta"), SharedFailure("Gamma")])
        ];

        List<FindingCandidate> candidates = Analyze(sessions);

        Assert.Single(candidates, c => c.Kind == FindingKind.SharedFailure);
        Assert.DoesNotContain(candidates, c => c.Kind == FindingKind.BrokenFixture);
    }

    /// <summary>
    /// A test body is not a fixture. Three tests failing alike inside their own bodies is a shared
    /// cause the report cannot name, which is exactly what a shared failure says.
    /// </summary>
    [Fact]
    public void FailuresInTheTestBodyAreNotABrokenFixture()
    {
        TestSession[] sessions =
        [
            .. FourQuietRuns(),
            TestSessionFactory.Session(
                4,
                [
                    FixtureFailure("Alpha", FailureSite.TestBody, member: null),
                    FixtureFailure("Beta", FailureSite.TestBody, member: null),
                    FixtureFailure("Gamma", FailureSite.TestBody, member: null)
                ])
        ];

        List<FindingCandidate> candidates = Analyze(sessions);

        Assert.Single(candidates, c => c.Kind == FindingKind.SharedFailure);
        Assert.DoesNotContain(candidates, c => c.Kind == FindingKind.BrokenFixture);
    }

    /// <summary>
    /// An unresolved site is an admission, not an observation, and must not license the claim.
    /// </summary>
    [Fact]
    public void FailuresWithAnUnknownSiteAreNotABrokenFixture()
    {
        TestSession[] sessions =
        [
            .. FourQuietRuns(),
            TestSessionFactory.Session(
                4,
                [
                    FixtureFailure("Alpha", FailureSite.Unknown, member: null),
                    FixtureFailure("Beta", FailureSite.Unknown, member: null),
                    FixtureFailure("Gamma", FailureSite.Unknown, member: null)
                ])
        ];

        Assert.Single(Analyze(sessions), c => c.Kind == FindingKind.SharedFailure);
    }

    /// <summary>
    /// The subject is the same cluster whichever kind describes it, so promoting it must not move the
    /// finding's id — otherwise every stored finding would move the first time an adapter learned to
    /// name a member.
    /// </summary>
    [Fact]
    public void PromotingAClusterDoesNotChangeItsSubject()
    {
        TestSession[] unnamed =
        [
            .. FourQuietRuns(),
            TestSessionFactory.Session(
                4, [SharedFailure("Alpha"), SharedFailure("Beta"), SharedFailure("Gamma")])
        ];

        TestSession[] named =
        [
            .. FourQuietRuns(),
            TestSessionFactory.Session(
                4, [FixtureFailure("Alpha"), FixtureFailure("Beta"), FixtureFailure("Gamma")])
        ];

        FindingCandidate shared = Single(Analyze(unnamed), FindingKind.SharedFailure);
        FindingCandidate fixture = Single(Analyze(named), FindingKind.BrokenFixture);

        Assert.Equal(shared.Subject.SortKey, fixture.Subject.SortKey);
    }

    /// <summary>Four runs in which nothing fails, so a cluster in the fifth is the only finding.</summary>
    private static TestSession[] FourQuietRuns() =>
        [.. Enumerable.Range(0, 4).Select(ordinal =>
            TestSessionFactory.Session(
                ordinal, [Passing("Alpha"), Passing("Beta"), Passing("Gamma")]))];

    [Fact]
    public void ClusterMembersDoNotAlsoAppearIndividually()
    {
        // The replacement rule. Without it, "47 failures" stays 47 findings and the cause is the
        // forty-eighth line nobody reads.
        TestSession[] sessions =
        [
            .. Enumerable.Range(0, 4).Select(ordinal =>
                TestSessionFactory.Session(
                    ordinal, [Passing("Alpha"), Passing("Beta"), Passing("Gamma")])),
            TestSessionFactory.Session(
                4, [SharedFailure("Alpha"), SharedFailure("Beta"), SharedFailure("Gamma")])
        ];

        List<FindingCandidate> candidates = Analyze(sessions);

        Assert.Single(candidates);
        Assert.Equal(FindingKind.SharedFailure, candidates[0].Kind);
    }

    [Fact]
    public void AMembersOtherFailuresStillCountAgainstItIndividually()
    {
        // "Failures of a member test outside the cluster still count normally."
        TestSession[] sessions =
        [
            .. Enumerable.Range(0, 3).Select(ordinal =>
                TestSessionFactory.Session(
                    ordinal, [Passing("Alpha"), Passing("Beta"), Passing("Gamma")])),
            TestSessionFactory.Session(
                3, [Failure("Alpha", "its own problem"), Passing("Beta"), Passing("Gamma")]),
            TestSessionFactory.Session(
                4, [SharedFailure("Alpha"), SharedFailure("Beta"), SharedFailure("Gamma")])
        ];

        List<FindingCandidate> candidates = Analyze(sessions);

        Assert.Single(candidates, c => c.Kind == FindingKind.SharedFailure);

        var evidence = Assert.IsType<FlakyEvidence>(For(candidates, "Alpha").Evidence);

        // One failure of its own, counted against the four runs that were not the cluster's.
        Assert.Equal(1, evidence.Failures);
        Assert.Equal(4, evidence.Executions);
        Assert.Equal(1, evidence.DiscountedExecutions);
        Assert.Equal(1, evidence.DistinctSignatureCount);
    }

    [Fact]
    public void TwoTestsFailingAlikeAreACoincidenceAndReportedSeparately()
    {
        TestSession[] sessions =
        [
            .. Enumerable.Range(0, 4).Select(ordinal =>
                TestSessionFactory.Session(ordinal, [Passing("Alpha"), Passing("Beta")])),
            TestSessionFactory.Session(4, [SharedFailure("Alpha"), SharedFailure("Beta")])
        ];

        List<FindingCandidate> candidates = Analyze(sessions);

        Assert.DoesNotContain(candidates, c => c.Kind == FindingKind.SharedFailure);
        Assert.Equal(2, candidates.Count(c => c.Kind == FindingKind.Flaky));
    }

    [Fact]
    public void ThreeTestsFailingAlikeInThreeDifferentRunsIsNotACluster()
    {
        // Spread across runs the claim is much weaker: nothing links the three failures except that
        // they look alike, and counting that would be the report inventing a cause.
        TestSession[] sessions =
        [
            TestSessionFactory.Session(0, [Passing("Alpha"), Passing("Beta"), Passing("Gamma")]),
            TestSessionFactory.Session(1, [Passing("Alpha"), Passing("Beta"), Passing("Gamma")]),
            TestSessionFactory.Session(2, [SharedFailure("Alpha"), Passing("Beta"), Passing("Gamma")]),
            TestSessionFactory.Session(3, [Passing("Alpha"), SharedFailure("Beta"), Passing("Gamma")]),
            TestSessionFactory.Session(4, [Passing("Alpha"), Passing("Beta"), SharedFailure("Gamma")])
        ];

        List<FindingCandidate> candidates = Analyze(sessions);

        Assert.DoesNotContain(candidates, c => c.Kind == FindingKind.SharedFailure);
        Assert.Equal(3, candidates.Count(c => c.Kind == FindingKind.Flaky));
    }

    [Fact]
    public void AnEnvironmentalRunIsLeftOutOfATestsOwnFailureRate()
    {
        // One broken dependency knocks over a third of the suite. Counting that run against each
        // test individually would poison every rate in the report for as long as it stays in view.
        List<TestExecution> outage =
        [
            .. Enumerable.Range(0, 12).Select(i => Failure($"Broken{i}")),
            .. Enumerable.Range(0, 18).Select(i => Passing($"Fine{i}"))
        ];

        TestSession[] sessions =
        [
            .. Enumerable.Range(0, 5).Select(ordinal => TestSessionFactory.Session(
                ordinal,
                [
                    .. Enumerable.Range(0, 12).Select(i => Passing($"Broken{i}")),
                    .. Enumerable.Range(0, 18).Select(i => Passing($"Fine{i}"))
                ])),
            TestSessionFactory.Session(5, outage)
        ];

        AnalysisContext context = TestSessionFactory.Context(sessions);

        Assert.Equal(1, context.EnvironmentalSessionCount);
        Assert.True(context.SessionViews[0].IsLikelyEnvironmental);
        Assert.Empty(new FailureModeProvider().Analyze(context));
    }

    [Fact]
    public void AnEnvironmentalRunIsStillWhereASharedCauseIsFound()
    {
        // The exception to the discounting rule, and the reason it exists: the run everything failed
        // in is precisely where the one cause is visible.
        List<TestExecution> outage =
        [
            .. Enumerable.Range(0, 12).Select(i => SharedFailure($"Broken{i}")),
            .. Enumerable.Range(0, 18).Select(i => Passing($"Fine{i}"))
        ];

        TestSession[] sessions =
        [
            .. Enumerable.Range(0, 5).Select(ordinal => TestSessionFactory.Session(
                ordinal,
                [
                    .. Enumerable.Range(0, 12).Select(i => Passing($"Broken{i}")),
                    .. Enumerable.Range(0, 18).Select(i => Passing($"Fine{i}"))
                ])),
            TestSessionFactory.Session(5, outage)
        ];

        List<FindingCandidate> candidates = Analyze(sessions);

        var evidence = Assert.IsType<SharedFailureEvidence>(
            Single(candidates, FindingKind.SharedFailure).Evidence);

        Assert.Equal(12, evidence.MemberCount);
    }

    [Fact]
    public void FailuresWithNothingRecordedNeverClusterTogether()
    {
        // The MSTest shape. Every one of these failures carries the same absence of detail, and a
        // signature built from that alone would report the entire suite as one shared cause.
        TestExecution Blank(string name) => TestSessionFactory.Execution(name, TestOutcome.Failed);

        TestSession[] sessions =
        [
            .. Enumerable.Range(0, 4).Select(ordinal =>
                TestSessionFactory.Session(
                    ordinal, [Passing("Alpha"), Passing("Beta"), Passing("Gamma")])),
            TestSessionFactory.Session(4, [Blank("Alpha"), Blank("Beta"), Blank("Gamma")])
        ];

        List<FindingCandidate> candidates = Analyze(sessions);

        Assert.DoesNotContain(candidates, c => c.Kind == FindingKind.SharedFailure);
        Assert.Equal(3, candidates.Count(c => c.Kind == FindingKind.Flaky));

        var evidence = Assert.IsType<FlakyEvidence>(For(candidates, "Alpha").Evidence);

        Assert.Equal(1, evidence.DistinctSignatureCount);
        Assert.True(evidence.DistinctSignatures[0].Unavailable);
    }

    [Fact]
    public void ATestThatAlwaysFailsBlanklyIsStillAlwaysFailing()
    {
        // The sentinel has to stay one signature per test, or a suite whose adapter records nothing
        // would read as though every test varied its failure mode.
        TestSession[] sessions = Runs(
            total: 10,
            failing: 10,
            failure: n => TestSessionFactory.Execution(n, TestOutcome.Failed));

        var evidence = Assert.IsType<AlwaysFailingEvidence>(
            Single(Analyze(sessions), FindingKind.AlwaysFailing).Evidence);

        Assert.True(evidence.Signature.Unavailable);
    }

    [Fact]
    public void AFailureWhoseStackTraceWasOmittedIsStillClassified()
    {
        TestSession[] sessions = Runs(
            total: 10,
            failing: 10,
            failure: n => TestSessionFactory.Execution(
                n,
                TestOutcome.Failed,
                exceptionType: "System.TimeoutException",
                errorMessage: "the operation timed out",
                stackTraceOmitted: true));

        var evidence = Assert.IsType<AlwaysFailingEvidence>(
            Single(Analyze(sessions), FindingKind.AlwaysFailing).Evidence);

        Assert.False(evidence.Signature.Unavailable);
        Assert.True(evidence.Signature.Degraded);
        Assert.Empty(evidence.Signature.Frames);
    }

    [Fact]
    public void AFailureWithNoUserFramesIsClassifiedAndFlaggedDegraded()
    {
        TestSession[] sessions = Runs(
            total: 10,
            failing: 10,
            failure: n => TestSessionFactory.Execution(
                n,
                TestOutcome.Failed,
                exceptionType: "Xunit.Sdk.TrueException",
                errorMessage: "assertion failed",
                stackTrace: RealFailureSamples.FrameworkOnlyStackTrace));

        var evidence = Assert.IsType<AlwaysFailingEvidence>(
            Single(Analyze(sessions), FindingKind.AlwaysFailing).Evidence);

        Assert.True(evidence.Signature.Degraded);
        Assert.NotEmpty(evidence.Signature.Frames);
    }

    [Fact]
    public void EveryFailedAttemptContributesItsOwnSignature()
    {
        // A test that fails one way, is retried and fails another way has varied its failure mode.
        // Reading only the last attempt would throw away the observation entirely.
        TestSession[] sessions =
        [
            .. Enumerable.Range(0, 4).Select(ordinal =>
                TestSessionFactory.Session(ordinal, [Passing("Subject")])),
            TestSessionFactory.Session(
                4,
                [
                    TestSessionFactory.Execution(
                        "Subject", TestOutcome.Failed, attempt: 1,
                        exceptionType: "System.TimeoutException", errorMessage: "timed out"),
                    TestSessionFactory.Execution(
                        "Subject", TestOutcome.Failed, attempt: 2,
                        exceptionType: "System.InvalidOperationException", errorMessage: "bad state")
                ])
        ];

        var evidence = Assert.IsType<FlakyEvidence>(
            Single(Analyze(sessions), FindingKind.Flaky).Evidence);

        Assert.Equal(2, evidence.Failures);
        Assert.Equal(2, evidence.DistinctSignatureCount);
    }

    [Fact]
    public void EveryRateArrivesWithItsDenominators()
    {
        var evidence = Assert.IsType<FlakyEvidence>(
            Single(Analyze(Runs(total: 10, failing: 3)), FindingKind.Flaky).Evidence);

        Assert.Equal(0.3, evidence.FailureRate);
        Assert.Equal(3, evidence.Failures);
        Assert.Equal(10, evidence.Executions);
        Assert.Equal(10, evidence.Sessions);
        Assert.Equal(3, evidence.SessionsWithFailures);
    }

    [Fact]
    public void NoMoreThanThreeExemplarsAreCarried()
    {
        var evidence = Assert.IsType<FlakyEvidence>(
            Single(Analyze(Runs(total: 10, failing: 7)), FindingKind.Flaky).Evidence);

        Assert.Equal(3, evidence.Exemplars.Count);
    }

    [Fact]
    public void ExemplarsCoverDistinctFailureModesBeforeRepeatingOne()
    {
        // Three exemplars of the same failure answer a question nobody asked.
        TestSession[] sessions = [.. Enumerable.Range(0, 9).Select(ordinal =>
            TestSessionFactory.Session(
                ordinal, [Failure("Subject", $"mode {(char)('a' + (ordinal % 3))}")]))];

        var evidence = Assert.IsType<FlakyEvidence>(
            Single(Analyze(sessions), FindingKind.Flaky).Evidence);

        Assert.Equal(3, evidence.Exemplars.Count);
        Assert.Equal(3, evidence.Exemplars.Select(e => e.SignatureHash).Distinct().Count());
    }

    [Fact]
    public void ALongMessageIsCutToTheBudgetAndMarked()
    {
        string huge = new('x', LocalAnalysisConstants.ExemplarCharBudget + 200);

        TestSession[] sessions = Runs(total: 6, failing: 6, failure: n => Failure(n, huge));

        var evidence = Assert.IsType<AlwaysFailingEvidence>(
            Single(Analyze(sessions), FindingKind.AlwaysFailing).Evidence);

        string? message = evidence.Exemplars[0].ErrorMessage;

        Assert.NotNull(message);
        Assert.StartsWith(new string('x', LocalAnalysisConstants.ExemplarCharBudget), message, StringComparison.Ordinal);
        Assert.EndsWith("(truncated)", message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExemplarCarriesTheExtractedFramesRatherThanTheRawTrace()
    {
        TestSession[] sessions = Runs(
            total: 6,
            failing: 6,
            failure: n => TestSessionFactory.Execution(
                n,
                TestOutcome.Failed,
                exceptionType: "Xunit.Sdk.TrueException",
                errorMessage: RealFailureSamples.XunitWatchdogFirstRun,
                stackTrace: RealFailureSamples.XunitWatchdogStackTrace));

        var evidence = Assert.IsType<AlwaysFailingEvidence>(
            Single(Analyze(sessions), FindingKind.AlwaysFailing).Evidence);

        Assert.Equal(
            ["SampleApp.XUnit.SampleTests.FlakyTest_EnvironmentState_FailsBasedOnSystemState()"],
            evidence.Exemplars[0].StackTrace);
    }

    [Fact]
    public void AFlakyTestCarriesOnePassingContrast()
    {
        var evidence = Assert.IsType<FlakyEvidence>(
            Single(Analyze(Runs(total: 10, failing: 4)), FindingKind.Flaky).Evidence);

        Assert.NotNull(evidence.Contrast);
        Assert.Equal("Passed", evidence.Contrast.Outcome);
    }

    [Fact]
    public void ATestThatNeverPassedCarriesNoContrastRatherThanAnEmptyOne()
    {
        var evidence = Assert.IsType<AlwaysFailingEvidence>(
            Single(Analyze(Runs(total: 6, failing: 6)), FindingKind.AlwaysFailing).Evidence);

        Assert.Null(evidence.Contrast);
    }

    [Fact]
    public void ASignatureSeenOnlyInTheNewestRunIsMarkedNew()
    {
        List<FindingCandidate> candidates = Analyze(Runs(total: 10, failing: 1));

        var evidence = Assert.IsType<FlakyEvidence>(Single(candidates, FindingKind.Flaky).Evidence);
        SignatureView signature = evidence.DistinctSignatures[0];

        Assert.True(signature.FirstSeenInLatestSession);
        Assert.True(signature.FirstSeenAfterWindowStart);
        Assert.Equal(0, signature.FirstSeenSessionsAgo);
    }

    [Fact]
    public void ASignaturePresentSinceTheOldestRunIsNotMarkedNew()
    {
        List<FindingCandidate> candidates = Analyze(Runs(total: 10, failing: 10));

        var evidence = Assert.IsType<AlwaysFailingEvidence>(
            Single(candidates, FindingKind.AlwaysFailing).Evidence);

        Assert.False(evidence.Signature.FirstSeenInLatestSession);
        Assert.False(evidence.Signature.FirstSeenAfterWindowStart);
        Assert.Equal(9, evidence.Signature.FirstSeenSessionsAgo);
    }

    [Fact]
    public void ClustersAreOrderedWidestFirstAndTiesBreakStably()
    {
        TestSession[] sessions =
        [
            .. Enumerable.Range(0, 4).Select(ordinal => TestSessionFactory.Session(
                ordinal,
                [.. ClusterFixtureTests.Select(Passing)])),
            TestSessionFactory.Session(
                4,
                [
                    SharedFailure("Alpha"),
                    SharedFailure("Beta"),
                    SharedFailure("Gamma"),
                    OtherSharedFailure("Delta"),
                    OtherSharedFailure("Epsilon"),
                    OtherSharedFailure("Zeta")
                ])
        ];

        // Both clusters have three members, so only the hash separates them — and it has to separate
        // them the same way on every run.
        List<FindingCandidate> first = Analyze(sessions);
        List<FindingCandidate> second = Analyze(sessions);

        Assert.Equal(2, first.Count(c => c.Kind == FindingKind.SharedFailure));
        Assert.Equal(
            first.Select(c => c.Subject.SortKey),
            second.Select(c => c.Subject.SortKey));
    }

    [Fact]
    public void TheSameWindowProducesTheSameCandidatesInTheSameOrder()
    {
        TestSession[] sessions = Runs(total: 12, failing: 5);

        Assert.Equal(Serialize(Analyze(sessions)), Serialize(Analyze(sessions)));
    }

    /// <summary>
    /// Renders candidates the way the report would, so the comparison is over published bytes.
    /// </summary>
    /// <remarks>
    /// Comparing the records directly would compare their evidence lists by reference and pass
    /// whatever the contents were.
    /// </remarks>
    private static string Serialize(List<FindingCandidate> candidates) =>
        string.Join(
            "\n",
            candidates.Select(c => string.Concat(
                c.Kind.ToString(),
                c.Subject.SortKey,
                JsonSerializer.Serialize(c.Evidence, c.Evidence.GetType(), ReportJsonOptions.Default))));

    [Fact]
    public void TwoIdenticallyBuiltWindowsProduceByteIdenticalJson()
    {
        // Rebuilt from scratch rather than reused, so anything leaking in from allocation order or
        // dictionary enumeration shows up here and not only in the run-twice case.
        Assert.Equal(Serialize(MixedFixture()), Serialize(MixedFixture()));
    }

    [Fact]
    public void TheReportingFloorKeepsAThinWindowOutOfTheFindings()
    {
        // Four sessions is below the floor. The command still renders a report; it simply has
        // nothing it is willing to claim.
        AnalysisResult result = Run(TestSessionFactory.Context(Runs(total: 4, failing: 4)));

        Assert.Empty(result.Findings);
        Assert.Equal(1, result.ExcludedLowEvidence);
    }

    [Fact]
    public void AFindingSurvivesTheFloorOnceThereIsEnoughOfIt()
    {
        AnalysisResult result = Run(TestSessionFactory.Context(Runs(total: 5, failing: 5)));

        Finding finding = Assert.Single(result.Findings);

        Assert.Equal(FindingKind.AlwaysFailing, finding.Kind);
        Assert.Equal(EvidenceLevel.Low, finding.EvidenceLevel);
    }

    [Fact]
    public void EnvironmentalSessionsAreCountedInTheSummary()
    {
        List<TestExecution> outage =
        [
            .. Enumerable.Range(0, 12).Select(i => Failure($"Broken{i}")),
            .. Enumerable.Range(0, 18).Select(i => Passing($"Fine{i}"))
        ];

        AnalysisContext context = TestSessionFactory.Context(
            [
                .. Enumerable.Range(0, 5).Select(ordinal => TestSessionFactory.Session(
                    ordinal,
                    [.. Enumerable.Range(0, 30).Select(i => Passing($"Test{i}"))])),
                TestSessionFactory.Session(5, outage)
            ]);

        ReportEnvelope envelope = EnvelopeBuilder.Build(
            context, Run(context), incompleteSessions: 0, unreadableSessions: 0, top: null);

        // Without this line the discounting silently changes every rate in the report, with nothing
        // on screen to explain it.
        Assert.Equal(1, envelope.Summary.EnvironmentalSessions);
    }

    private static AnalysisResult Run(AnalysisContext context) =>
        new FindingCoordinator([new FailureModeProvider()])
            .Run(context, kinds: null, TextWriter.Null);

    private static string Serialize(TestSession[] sessions)
    {
        AnalysisContext context = TestSessionFactory.Context(sessions);

        ReportEnvelope envelope = EnvelopeBuilder.Build(
            context, Run(context), incompleteSessions: 0, unreadableSessions: 0, top: null);

        return JsonSerializer.Serialize(envelope, ReportJsonOptions.Default);
    }

    /// <summary>
    /// A window carrying one of everything: a cluster, a broken test and an unstable one.
    /// </summary>
    private static TestSession[] MixedFixture() =>
        [
            .. Enumerable.Range(0, 6).Select(ordinal => TestSessionFactory.Session(
                ordinal,
                [
                    Passing("Alpha"),
                    Passing("Beta"),
                    Passing("Gamma"),
                    Failure("Broken"),
                    ordinal % 2 == 0 ? Passing("Unstable") : Failure("Unstable", "sometimes")
                ],
                sha: $"a3f9c2e{ordinal}")),
            TestSessionFactory.Session(
                6,
                [
                    SharedFailure("Alpha"),
                    SharedFailure("Beta"),
                    SharedFailure("Gamma"),
                    Failure("Broken"),
                    Failure("Unstable", "sometimes")
                ],
                sha: "a3f9c2e6")
        ];

    [Fact]
    public void ATestWithNoFingerprintIsSkippedRatherThanCrashingTheProvider()
    {
        // Defensive: the index drops fingerprint-less executions, and the provider must not then
        // find a candidate it cannot name.
        TestSession[] sessions = Runs(total: 6, failing: 6);

        Assert.NotEmpty(Analyze(sessions));
    }
}
