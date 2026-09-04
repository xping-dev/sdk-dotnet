/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Cli.Report.Scoring;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

public sealed class ParallelSensitiveProviderTests
{
    private const string Subject = "Subject";

    // Ten sessions, five at each of two levels, is a comfortable window rather than the smallest one:
    // perfectly separated, four runs a side is the least the trend test can say anything about at all.
    private const int Sessions = 10;

    // ---------------------------------------------------------------------------------------
    // The condition
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestThatFailsOnlyWhenTheSuiteIsCrowdedIsSensitive()
    {
        FindingCandidate candidate = Single(Split(highFailures: 5, lowFailures: 0));

        Assert.Equal(FindingKind.ParallelSensitive, candidate.Kind);
        Assert.Equal(Subject, Named(candidate));
    }

    [Fact]
    public void APinnedSuiteWithOccasionalSerialRunsIsAnalysable()
    {
        // The shape the median split could never report: fifteen runs at the suite's fixed thread
        // count and five that happened to run serially. Every tied observation fell into the low arm
        // and the high arm starved, so the finding was structurally unreachable however
        // concurrency-sensitive the test really was. There is no arm to starve now.
        ParallelSensitiveEvidence evidence = EvidenceFrom(
            Split(highFailures: 11, lowFailures: 0, highSessions: 15, lowSessions: 5,
                lowConcurrency: 1, highConcurrency: 8));

        Assert.Equal(nameof(ConcurrencyDirection.WithConcurrency), evidence.Trend.Direction);
        Assert.Equal(0.638, evidence.Trend.Tau, 3);
        Assert.Equal(0.0336, evidence.Trend.PValue, 4);
    }

    [Fact]
    public void AGradualRiseAcrossTheWholeRangeIsSeenWhereTheMedianSplitOfItIsNot()
    {
        // Forty-two runs across all fourteen levels, the failure rate climbing steadily from a
        // twentieth to a bit over a half. Read as a trend that is p = 0.0084 at τ_b 0.35.
        List<TestSession> window = Gradual();
        ParallelSensitiveEvidence evidence = EvidenceFrom(window);

        Assert.Equal(0.00836, evidence.Trend.PValue, 5);
        Assert.Equal(0.354, evidence.Trend.Tau, 3);

        // The same window split at its own median, which is what this used to do. Both arms are
        // enormous and it still sees nothing, because the cut throws away the ordering that is the
        // whole signal: a gap of 0.286 — under the 0.30 the old condition required — and a division
        // of the failures that chance produces about one time in twelve.
        (double probability, double delta) = MedianSplit(window);

        Assert.Equal(0.0855, probability, 4);
        Assert.Equal(0.286, delta, 3);
    }

    [Fact]
    public void ATestWhoseConcurrencyNeverVariedIsNotAFinding()
    {
        // Failures aplenty, but every execution ran at the same level, so there is no exposure to
        // trend against. This is the common case in a real store.
        List<TestSession> sessions = [];
        for (int ordinal = 0; ordinal < Sessions; ordinal++)
        {
            sessions.Add(TestSessionFactory.Session(ordinal,
            [
                TestSessionFactory.Execution(
                    Subject,
                    ordinal < 5 ? TestOutcome.Failed : TestOutcome.Passed,
                    errorMessage: ordinal < 5 ? "boom" : null,
                    concurrency: 8)
            ]));
        }

        Assert.Empty(Analyze(sessions));
    }

    // ---------------------------------------------------------------------------------------
    // What the runs behind a trend are worth
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AHeavilyRetriedSessionCannotManufactureATrend()
    {
        // #180's fixture. Ten runs, each running the subject twice at concurrency 2 and passing.
        // Run zero additionally retries eight times at concurrency 8, failing every one; runs one to
        // five additionally run once at 8 and pass. Counted as independent attempts that is a
        // perfect four-sigma trend built out of a single afternoon.
        Assert.Empty(Analyze(RetriedBurst()));
    }

    [Fact]
    public void RetryingMoreWithinTheSameSessionsDoesNotBuyConfidence()
    {
        // The rank must not move on attempts. Both windows are ten runs taking the same concurrency
        // readings with the same outcomes; the second simply retries twice as hard. Anything computed
        // over the raw execution counts would call the second twice as well evidenced, and would put
        // a test that retries above one measured over as many separate builds.
        double four = Single(RetriedWithin(sessions: 10, attemptsPerSession: 4)).Unreliability;
        double eight = Single(RetriedWithin(sessions: 10, attemptsPerSession: 8)).Unreliability;

        // What is left is the continuity correction relaxing, which it should: the half-step the
        // levels put the statistic on is a fixed distance, and it is genuinely a smaller share of a
        // statistic computed over eighty readings than of one computed over forty. A variance taken
        // over attempts would have raised the rank by four fifths instead of by a fiftieth.
        Assert.Equal(0.158, four, 3);
        Assert.Equal(0.180, eight, 3);
    }

    [Fact]
    public void MoreSessionsStillBuyConfidence()
    {
        // The other half of the invariant: counting occasions must not flatten the bound altogether.
        // Both windows are a perfect dose-response and read τ_b 1.00; the second rests on four times
        // the runs and has to rank above the first.
        double ten = Single(Split(highFailures: 5, lowFailures: 0)).Unreliability;
        double forty = Single(
            Split(highFailures: 20, lowFailures: 0, highSessions: 20, lowSessions: 20)).Unreliability;

        Assert.True(forty > ten, $"{forty} > {ten}");
    }

    [Fact]
    public void UnreliabilityDiscountsTheCorrelationByHowPreciselyItWasMeasured()
    {
        // Five clean runs at 2 against five failing runs at 8 is τ_b 1.00 — the strongest correlation
        // there is — and it is the smallest window that comfortably clears the bar. Ranking on the
        // estimate would put it above every well-evidenced finding in the report; discounting it by
        // the trend statistic's margin over its own threshold puts it near the bottom, where a
        // reader can still find it.
        FindingCandidate candidate = Single(Split(highFailures: 5, lowFailures: 0));
        ParallelSensitiveEvidence evidence =
            Assert.IsType<ParallelSensitiveEvidence>(candidate.Evidence);

        Assert.Equal(1.0, evidence.Trend.Tau);
        Assert.Equal(0.183, candidate.Unreliability, 3);
    }

    // ---------------------------------------------------------------------------------------
    // What the evidence carries
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TheObservedRangeAndEveryLevelArePublished()
    {
        ParallelSensitiveEvidence evidence = EvidenceFrom(Split(highFailures: 5, lowFailures: 0));

        Assert.Equal(2, evidence.Observed.Min);
        Assert.Equal(8, evidence.Observed.Max);
        Assert.Equal(2, evidence.Observed.DistinctLevels);

        Assert.Equal([2, 8], evidence.Levels.Select(l => l.Concurrency));

        Assert.Equal(0, evidence.Levels[0].Failures);
        Assert.Equal(5, evidence.Levels[0].Executions);
        Assert.Equal(5, evidence.Levels[0].Sessions);
        Assert.Equal(0, evidence.Levels[0].FailureRate);

        Assert.Equal(5, evidence.Levels[1].Failures);
        Assert.Equal(5, evidence.Levels[1].Executions);
        Assert.Equal(5, evidence.Levels[1].Sessions);
        Assert.Equal(1.0, evidence.Levels[1].FailureRate);
    }

    [Fact]
    public void ALevelKeepsItsExecutionDenominatorAndItsRunCountApart()
    {
        // The concurrency readings are real and distinct, so a level's rate stays over executions.
        // What it publishes beside them is how many separate occasions those executions came from.
        ParallelSensitiveEvidence evidence = EvidenceFrom(RetriedWithin(sessions: 10, attemptsPerSession: 4));

        Assert.Equal([2, 8], evidence.Levels.Select(l => l.Concurrency));
        Assert.All(evidence.Levels, l => Assert.Equal(20, l.Executions));
        Assert.All(evidence.Levels, l => Assert.Equal(10, l.Sessions));
    }

    [Fact]
    public void TheTrendCarriesItsUnroundedProbabilityToTheCoordinator()
    {
        FindingCandidate candidate = Single(Split(highFailures: 5, lowFailures: 0));
        ParallelSensitiveEvidence evidence =
            Assert.IsType<ParallelSensitiveEvidence>(candidate.Evidence);

        // What is published is rounded to three significant digits; what the multiplicity correction
        // in #160 will sort on is not.
        Assert.Equal(0.0164, evidence.Trend.PValue, 6);
        Assert.NotNull(candidate.PValue);
        Assert.NotEqual(evidence.Trend.PValue, candidate.PValue!.Value);
        Assert.Equal(0.01639533, candidate.PValue!.Value, 6);
    }

    [Fact]
    public void ASmallProbabilityIsNeverPublishedAsZero()
    {
        // Forty runs perfectly separated. A fixed count of decimal places would publish this as
        // 0.000000, which is a claim of certainty no window makes.
        ParallelSensitiveEvidence evidence = EvidenceFrom(
            Split(highFailures: 20, lowFailures: 0, highSessions: 20, lowSessions: 20));

        Assert.True(evidence.Trend.PValue > 0, $"{evidence.Trend.PValue}");
        Assert.True(evidence.Trend.PValue < 1e-6, $"{evidence.Trend.PValue}");
    }

    // ---------------------------------------------------------------------------------------
    // Direction
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestThatFailsOnlyWhenItRunsNearlyAloneIsAlsoSensitive()
    {
        ParallelSensitiveEvidence evidence = EvidenceFrom(Split(highFailures: 0, lowFailures: 5));

        Assert.Equal(nameof(ConcurrencyDirection.AgainstConcurrency), evidence.Trend.Direction);
        Assert.True(evidence.Trend.Tau < 0, $"{evidence.Trend.Tau}");
        Assert.True(evidence.Trend.Z < 0, $"{evidence.Trend.Z}");
    }

    [Fact]
    public void TheDirectionAlwaysAgreesWithTheSignOfTheCorrelation()
    {
        ParallelSensitiveEvidence crowded = EvidenceFrom(Split(highFailures: 5, lowFailures: 0));
        Assert.Equal(nameof(ConcurrencyDirection.WithConcurrency), crowded.Trend.Direction);
        Assert.Equal(1.0, crowded.Trend.Tau);

        ParallelSensitiveEvidence alone = EvidenceFrom(Split(highFailures: 0, lowFailures: 5));
        Assert.Equal(nameof(ConcurrencyDirection.AgainstConcurrency), alone.Trend.Direction);
        Assert.Equal(-1.0, alone.Trend.Tau);
    }

    [Fact]
    public void AFailureThatArguesAgainstTheTrendDoesNotKeepTheFindingFresh()
    {
        // A rising trend whose crowded failures are all old, plus one failure last night at the
        // quiet end of the range. That failure is a counterexample: it is the observation the
        // statistic subtracted rather than added. Dating the finding by it would hold a stale
        // concurrency problem at the top of the report on the strength of the one run that argues
        // against it.
        List<IReadOnlyList<(int, bool)>> schedule = [];

        for (int ordinal = 0; ordinal < 20; ordinal++)
            schedule.Add([(8, ordinal < 15)]);

        for (int ordinal = 0; ordinal < 8; ordinal++)
            schedule.Add([(1, false)]);

        schedule.Add([(1, true)]);

        FindingCandidate candidate = Single(Window(schedule));
        ParallelSensitiveEvidence evidence =
            Assert.IsType<ParallelSensitiveEvidence>(candidate.Evidence);

        // Twenty-nine runs, the newest of them the counterexample; the newest crowded failure is
        // the fifteenth-oldest, fourteen runs back.
        Assert.Equal(TestSessionFactory.SessionIdFor(14), candidate.LastOccurrenceIn.SessionId);
        Assert.All(evidence.Exemplars, e => Assert.Equal(8, e.Concurrency));
    }

    [Fact]
    public void ExemplarsComeFromTheEndOfTheRangeTheTrendPointsAt()
    {
        // The quiet end is the failing one here, so the exemplars must be its concurrency-2 runs
        // rather than the crowded end's clean ones.
        ParallelSensitiveEvidence evidence = EvidenceFrom(Split(highFailures: 0, lowFailures: 5));

        Assert.All(evidence.Exemplars, e => Assert.Equal(2, e.Concurrency));
        Assert.All(evidence.Exemplars, e => Assert.Equal("Failed", e.Outcome));
        Assert.Equal(8, evidence.Contrast?.Concurrency);
    }

    // ---------------------------------------------------------------------------------------
    // What does not qualify
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATrendChanceWouldProduceIsNotAFinding()
    {
        // Two failures in five crowded runs against one in five quiet ones. τ_b is 0.22 and the
        // trend does not clear the half-step the levels put it on at all.
        Assert.Empty(Analyze(Split(highFailures: 2, lowFailures: 1)));
    }

    [Fact]
    public void ATrendTooWeakToActOnIsNotAFinding()
    {
        // Two hundred executions across all fourteen levels, the failure rate drifting from a tenth
        // to a sixth. There is enough of it to be sure it is there; τ_b is 0.06 and there is nothing
        // a developer could do with it.
        List<TestSession> window = Drift();

        Assert.True(Math.Abs(KendallTau.TauB(PointsFrom(window))) < 0.10);
        Assert.Empty(Analyze(window));
    }

    [Fact]
    public void ATestThatFailedEveryExecutionIsNotATrend()
    {
        // A test that always fails has no covariance with anything, whatever its concurrency did.
        // It is an AlwaysFailing finding, and this provider must not also claim it.
        Assert.Empty(Analyze(Split(highFailures: 5, lowFailures: 5)));
    }

    [Fact]
    public void ExecutionsWithNoOrchestrationDataAreExcludedRatherThanAssumedSerial()
    {
        // Every execution fails, and every one predates the concurrency field. Defaulting those to
        // "ran alone" would invent a level and report a finding from nothing.
        List<TestSession> sessions = [];
        for (int ordinal = 0; ordinal < Sessions; ordinal++)
        {
            sessions.Add(TestSessionFactory.Session(ordinal,
            [
                TestSessionFactory.Execution(Subject, TestOutcome.Failed, errorMessage: "boom")
            ]));
        }

        Assert.Empty(Analyze(sessions));
    }

    // ---------------------------------------------------------------------------------------
    // Discounting, §6
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AnOutageDoesNotManufactureATrendOutOfTheLevelItLandedIn()
    {
        // Every failure at the crowded level comes from a session where the whole suite went down.
        // Counted naively that is a perfect dose-response; discounted, the level holds no failures.
        List<TestSession> sessions = [];

        for (int ordinal = 0; ordinal < Sessions; ordinal++)
        {
            bool crowded = ordinal >= 5;
            List<TestExecution> executions =
            [
                TestSessionFactory.Execution(
                    Subject,
                    crowded ? TestOutcome.Failed : TestOutcome.Passed,
                    errorMessage: crowded ? "boom" : null,
                    concurrency: crowded ? 8 : 2)
            ];

            // Enough collateral failures to clear both environmental bounds: a rate at or above 0.30
            // and at least ten failing tests.
            if (crowded)
            {
                for (int companion = 0; companion < 12; companion++)
                {
                    executions.Add(TestSessionFactory.Execution(
                        $"Companion{companion}", TestOutcome.Failed, errorMessage: "infra", concurrency: 8));
                }
            }

            sessions.Add(TestSessionFactory.Session(ordinal, executions));
        }

        AnalysisContext context = TestSessionFactory.Context([.. sessions]);
        Assert.Equal(5, context.EnvironmentalSessionCount);

        Assert.DoesNotContain(
            new ParallelSensitiveProvider().Analyze(context).Candidates,
            c => Named(c) == Subject);
    }

    // ---------------------------------------------------------------------------------------
    // The parallel-versus-serial case the trend subsumes
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ASuiteThatTurnedParallelMidWindowIsOneTrendAcrossTwoLevels()
    {
        // Five genuinely serial executions and five at fourteen — the shape the original two-arm
        // condition described. It arrives as two levels with no special case.
        ParallelSensitiveEvidence evidence = EvidenceFrom(
            Split(highFailures: 5, lowFailures: 0, lowConcurrency: 1, highConcurrency: 14));

        Assert.Equal([1, 14], evidence.Levels.Select(l => l.Concurrency));
        Assert.Equal(nameof(ConcurrencyDirection.WithConcurrency), evidence.Trend.Direction);
        Assert.Equal(1.0, evidence.Trend.Tau);
    }

    // ---------------------------------------------------------------------------------------
    // Output contract and determinism
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ExemplarsAreCappedAtThreeAndDrawnFromTheCrowdedEndNewestFirst()
    {
        ParallelSensitiveEvidence evidence = EvidenceFrom(Split(highFailures: 5, lowFailures: 0));

        Assert.Equal(3, evidence.Exemplars.Count);
        Assert.All(evidence.Exemplars, e => Assert.Equal(8, e.Concurrency));

        List<DateTime> dates = [.. evidence.Exemplars.Select(e => e.StartedAt)];
        Assert.Equal(dates.OrderByDescending(d => d), dates);
    }

    [Fact]
    public void TheContrastPrefersAPassingExecutionFromTheOppositeEnd()
    {
        ParallelSensitiveEvidence evidence = EvidenceFrom(
            Split(highFailures: 7, lowFailures: 1, highSessions: 7, lowSessions: 7));

        Assert.NotNull(evidence.Contrast);
        Assert.Equal("Passed", evidence.Contrast.Outcome);
        Assert.Equal(2, evidence.Contrast.Concurrency);
    }

    [Fact]
    public void TwoRunsOverTheSameWindowProduceByteIdenticalJson()
    {
        AnalysisContext context = TestSessionFactory.Context(
            [.. Split(highFailures: 5, lowFailures: 0)]);

        Assert.Equal(Serialize(context), Serialize(context));
    }

    [Fact]
    public void TwoIdenticallyBuiltWindowsProduceByteIdenticalJson()
    {
        string first = Serialize(TestSessionFactory.Context([.. Split(highFailures: 5, lowFailures: 0)]));
        string second = Serialize(TestSessionFactory.Context([.. Split(highFailures: 5, lowFailures: 0)]));

        Assert.Equal(first, second);
    }

    // ---------------------------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a window from an explicit schedule: one entry per run, each holding the concurrency
    /// and outcome of every execution that run recorded.
    /// </summary>
    private static List<TestSession> Window(List<IReadOnlyList<(int, bool)>> schedule)
    {
        List<TestSession> sessions = [];
        int serial = 0;

        for (int ordinal = 0; ordinal < schedule.Count; ordinal++)
        {
            List<TestExecution> executions = [];
            IReadOnlyList<(int Concurrency, bool Failed)> run = schedule[ordinal];

            for (int attempt = 1; attempt <= run.Count; attempt++)
            {
                (int concurrency, bool failed) = run[attempt - 1];

                executions.Add(TestSessionFactory.Execution(
                    Subject,
                    failed ? TestOutcome.Failed : TestOutcome.Passed,
                    attempt: attempt,
                    maxRetries: run.Count - 1,
                    passedOnRetry: !failed && attempt > 1,
                    errorMessage: failed ? "boom" : null,

                    // Distinct across the whole window so exemplars never collide on the id-based
                    // tiebreaker.
                    executionId: TestSessionFactory.ExecutionIdFor(Subject, ++serial, TestOutcome.Failed),
                    concurrency: concurrency));
            }

            sessions.Add(TestSessionFactory.Session(ordinal, executions));
        }

        return sessions;
    }

    /// <summary>
    /// Builds a window where the subject runs once per session, quiet in the older half and crowded
    /// in the newer, failing as many times as asked at each level.
    /// </summary>
    private static List<TestSession> Split(
        int highFailures,
        int lowFailures,
        int highSessions = 5,
        int lowSessions = 5,
        int lowConcurrency = 2,
        int highConcurrency = 8)
    {
        List<IReadOnlyList<(int, bool)>> schedule = [];

        for (int ordinal = 0; ordinal < lowSessions + highSessions; ordinal++)
        {
            bool crowded = ordinal >= lowSessions;
            int position = crowded ? ordinal - lowSessions : ordinal;
            bool failed = position < (crowded ? highFailures : lowFailures);

            schedule.Add([(crowded ? highConcurrency : lowConcurrency, failed)]);
        }

        return Window(schedule);
    }

    /// <summary>
    /// Builds a window in which the subject retries within each session, taking a fresh concurrency
    /// reading on every attempt: crowded on the earlier attempts and quiet on the later ones.
    /// </summary>
    /// <remarks>
    /// The concurrency genuinely differs between attempts, so the readings are real; what they are
    /// not is independent occasions. Only <paramref name="attemptsPerSession"/> moves the execution
    /// count, which is what lets a test vary that alone and watch what the bound does.
    /// </remarks>
    private static List<TestSession> RetriedWithin(int sessions, int attemptsPerSession)
    {
        List<IReadOnlyList<(int, bool)>> schedule = [];

        for (int ordinal = 0; ordinal < sessions; ordinal++)
        {
            List<(int, bool)> run = [];

            for (int attempt = 1; attempt <= attemptsPerSession; attempt++)
            {
                bool crowded = attempt <= attemptsPerSession / 2;
                bool failed = crowded || (attempt - (attemptsPerSession / 2)) % 2 == 1;

                run.Add((crowded ? 8 : 2, failed));
            }

            schedule.Add(run);
        }

        return Window(schedule);
    }

    /// <summary>
    /// Builds #180's window: one run supplies every failure, out of a burst of retries.
    /// </summary>
    private static List<TestSession> RetriedBurst()
    {
        List<IReadOnlyList<(int, bool)>> schedule = [];

        for (int ordinal = 0; ordinal < Sessions; ordinal++)
        {
            List<(int, bool)> run = [(2, false), (2, false)];

            if (ordinal == 0)
            {
                for (int retry = 0; retry < 8; retry++)
                    run.Add((8, true));
            }
            else if (ordinal <= 5)
            {
                run.Add((8, false));
            }

            schedule.Add(run);
        }

        return Window(schedule);
    }

    /// <summary>
    /// Builds a window spread across every level from 1 to 14, three runs each, with the failure
    /// rate climbing steadily from a twentieth to a bit over a half.
    /// </summary>
    private static List<TestSession> Gradual() => Spread(3, 0.05, 0.55);

    /// <summary>
    /// Builds a long window across every level from 1 to 14 with a drift too small to act on.
    /// </summary>
    private static List<TestSession> Drift() => Spread(14, 0.10, 0.1667);

    private static List<TestSession> Spread(int runsPerLevel, double lowest, double highest)
    {
        List<IReadOnlyList<(int, bool)>> schedule = [];

        for (int level = 1; level <= 14; level++)
        {
            double rate = lowest + ((highest - lowest) * (level - 1) / 13.0);
            int failures = (int)Math.Round(runsPerLevel * rate, MidpointRounding.AwayFromZero);

            for (int run = 0; run < runsPerLevel; run++)
                schedule.Add([(level, run < failures)]);
        }

        return Window(schedule);
    }

    /// <summary>
    /// Divides a window at its own nearest-rank median concurrency, the way this provider used to.
    /// </summary>
    /// <returns>Fisher's exact probability of that division, and the gap between the two arms.</returns>
    private static (double Probability, double Delta) MedianSplit(List<TestSession> window)
    {
        List<TrendPoint> points = PointsFrom(window);
        int median = Quantile.NearestRank([.. points.Select(p => p.Level).Order()], 0.50);

        List<TrendPoint> high = [.. points.Where(p => p.Level > median)];
        List<TrendPoint> low = [.. points.Where(p => p.Level <= median)];

        return (
            FisherExact.TwoSidedPValue(
                high.Count(p => p.Occurred), high.Count, low.Count(p => p.Occurred), low.Count),
            ((double)high.Count(p => p.Occurred) / high.Count) -
                ((double)low.Count(p => p.Occurred) / low.Count));
    }

    /// <summary>
    /// Reads a window as the trend statistics read it.
    /// </summary>
    private static List<TrendPoint> PointsFrom(List<TestSession> sessions)
    {
        AnalysisContext context = TestSessionFactory.Context([.. sessions]);

        return
        [
            .. context.Tests.ExecutionsOf($"fp-{Subject}")
                .Where(r => r.Execution.TestOrchestrationRecord is { ConcurrentTestCount: >= 1 })
                .Select(r => new TrendPoint(
                    r.Execution.TestOrchestrationRecord!.ConcurrentTestCount,
                    r.Failed,
                    r.SessionIndex))
        ];
    }

    private static IReadOnlyList<FindingCandidate> Analyze(List<TestSession> sessions) =>
        new ParallelSensitiveProvider().Analyze(TestSessionFactory.Context([.. sessions])).Candidates;

    private static FindingCandidate Single(List<TestSession> sessions) =>
        Assert.Single(Analyze(sessions));

    private static ParallelSensitiveEvidence EvidenceFrom(List<TestSession> sessions) =>
        Assert.IsType<ParallelSensitiveEvidence>(Single(sessions).Evidence);

    private static string Named(FindingCandidate candidate) =>
        Assert.IsType<FindingSubject.SingleTest>(candidate.Subject).Test.DisplayName;

    /// <summary>
    /// Renders the whole report the way the command would.
    /// </summary>
    /// <remarks>
    /// Determinism is asserted on the JSON rather than on the candidates, because the requirement in
    /// §10 is about the bytes: the evidence records hold lists, which records compare by reference,
    /// so two identical analyses would compare unequal while serialising the same.
    /// </remarks>
    private static string Serialize(AnalysisContext context)
    {
        using var warnings = new StringWriter();

        AnalysisResult result =
            new FindingCoordinator([new ParallelSensitiveProvider()]).Run(context, null, warnings);

        ReportEnvelope envelope = EnvelopeBuilder.Build(
            context, result, incompleteSessions: 0, unreadableSessions: 0, top: null);

        return JsonSerializer.Serialize(envelope, ReportJsonOptions.Default);
    }
}
