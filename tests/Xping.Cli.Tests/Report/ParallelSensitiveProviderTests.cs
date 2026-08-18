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

public sealed class ParallelSensitiveProviderTests
{
    private const string Subject = "Subject";

    // Ten sessions, five a side of the median, is the smallest window the finding can fire in:
    // ParallelSensitiveMinArmExecutions is 5 and both arms have to clear it.
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
    public void TheEvidenceCarriesBothArmsWithTheirDenominators()
    {
        ParallelSensitiveEvidence evidence = EvidenceFrom(Split(highFailures: 4, lowFailures: 0));

        Assert.Equal(4, evidence.High.Failures);
        Assert.Equal(5, evidence.High.Executions);
        Assert.Equal(5, evidence.High.Sessions);
        Assert.Equal(0.8, evidence.High.FailureRate);

        Assert.Equal(0, evidence.Low.Failures);
        Assert.Equal(5, evidence.Low.Executions);
        Assert.Equal(5, evidence.Low.Sessions);
        Assert.Equal(0, evidence.Low.FailureRate);
    }

    [Fact]
    public void TheSplitPointAndTheObservedRangeArePublished()
    {
        ParallelSensitiveEvidence evidence = EvidenceFrom(Split(highFailures: 5, lowFailures: 0));

        // Five executions at 2 and five at 8; the nearest-rank median of that is the low level.
        Assert.Equal(2, evidence.SplitAtConcurrency);
        Assert.Equal(2, evidence.Observed.Min);
        Assert.Equal(8, evidence.Observed.Max);
        Assert.Equal(2, evidence.Observed.DistinctLevels);

        Assert.Equal(8, evidence.High.MinConcurrency);
        Assert.Equal(8, evidence.High.MaxConcurrency);
        Assert.Equal(2, evidence.Low.MinConcurrency);
        Assert.Equal(2, evidence.Low.MaxConcurrency);
    }

    [Fact]
    public void UnreliabilityIsTheGapTheConditionMeasured()
    {
        FindingCandidate candidate = Single(Split(highFailures: 4, lowFailures: 1));

        // 0.8 in the crowded arm against 0.2 in the quiet one.
        Assert.Equal(0.6, candidate.Unreliability, 10);
    }

    // ---------------------------------------------------------------------------------------
    // Direction
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestThatFailsOnlyWhenItRunsNearlyAloneIsAlsoSensitive()
    {
        FindingCandidate candidate = Single(Split(highFailures: 0, lowFailures: 5));

        Assert.Equal(FindingKind.ParallelSensitive, candidate.Kind);
    }

    [Fact]
    public void TheDeltaIsSignedSoTheDirectionIsReadRatherThanDerived()
    {
        ParallelSensitiveEvidence crowded = EvidenceFrom(Split(highFailures: 5, lowFailures: 0));
        Assert.Equal(1.0, crowded.Delta.FailureRate);
        Assert.Equal(100.0, crowded.Delta.FailureRatePct);

        ParallelSensitiveEvidence alone = EvidenceFrom(Split(highFailures: 0, lowFailures: 5));
        Assert.Equal(-1.0, alone.Delta.FailureRate);
        Assert.Equal(-100.0, alone.Delta.FailureRatePct);
    }

    [Fact]
    public void ExemplarsComeFromTheArmHoldingTheFailuresWhicheverSideThatIs()
    {
        // The quiet arm is the failing one here, so the exemplars must be its concurrency-2 runs
        // rather than the crowded arm's clean ones.
        ParallelSensitiveEvidence evidence = EvidenceFrom(Split(highFailures: 0, lowFailures: 5));

        Assert.All(evidence.Exemplars, e => Assert.Equal(2, e.Concurrency));
        Assert.All(evidence.Exemplars, e => Assert.Equal("Failed", e.Outcome));
        Assert.Equal(8, evidence.Contrast?.Concurrency);
    }

    // ---------------------------------------------------------------------------------------
    // What does not qualify
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestWhoseConcurrencyNeverVariedIsNotAFinding()
    {
        // Failures aplenty, but every execution ran at the same level, so the high arm is empty and
        // there is nothing to compare. This is the common case in a real store.
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

    [Fact]
    public void ATestWithTooFewExecutionsOnOneSideIsNotCompared()
    {
        // Nine executions: four crowded, five quiet. The crowded arm is one short of the gate.
        Assert.Empty(Analyze(Split(highFailures: 4, lowFailures: 0, highSessions: 4)));
    }

    [Fact]
    public void AGapBelowTheThresholdIsNotAFinding()
    {
        // 0.4 against 0.2 is a gap of 0.2, under ParallelSensitivityDelta.
        Assert.Empty(Analyze(Split(highFailures: 2, lowFailures: 1)));
    }

    [Fact]
    public void ExecutionsWithNoOrchestrationDataAreExcludedRatherThanAssumedSerial()
    {
        // Every execution fails, and every one predates the concurrency field. Defaulting those to
        // "ran alone" would invent an arm and report a finding from nothing.
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
    public void AnOutageDoesNotManufactureAGapOutOfTheArmItLandedIn()
    {
        // The crowded arm's failures all come from sessions where the whole suite went down. Counted
        // naively that is a perfect 1.0-against-0.0 split; discounted, the arm empties out entirely.
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
            new ParallelSensitiveProvider().Analyze(context),
            c => Named(c) == Subject);
    }

    // ---------------------------------------------------------------------------------------
    // The parallel-versus-serial case the median subsumes
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ASuiteThatTurnedParallelMidWindowSplitsAtOneWithNoSpecialCase()
    {
        // Five genuinely serial executions and five at fourteen — the shape the original two-arm
        // condition described. The per-test median puts them either side on its own.
        ParallelSensitiveEvidence evidence = EvidenceFrom(
            Split(highFailures: 5, lowFailures: 0, lowConcurrency: 1, highConcurrency: 14));

        Assert.Equal(1, evidence.SplitAtConcurrency);
        Assert.Equal(1, evidence.Low.MaxConcurrency);
        Assert.Equal(14, evidence.High.MinConcurrency);
        Assert.Equal(1.0, evidence.Delta.FailureRate);
    }

    // ---------------------------------------------------------------------------------------
    // Output contract and determinism
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ExemplarsAreCappedAtThreeAndOrderedNewestFirst()
    {
        ParallelSensitiveEvidence evidence = EvidenceFrom(Split(highFailures: 5, lowFailures: 0));

        Assert.Equal(3, evidence.Exemplars.Count);

        List<DateTime> dates = [.. evidence.Exemplars.Select(e => e.StartedAt)];
        Assert.Equal(dates.OrderByDescending(d => d), dates);
    }

    [Fact]
    public void TheContrastPrefersAPassingExecutionFromTheOtherArm()
    {
        ParallelSensitiveEvidence evidence = EvidenceFrom(Split(highFailures: 5, lowFailures: 1));

        Assert.NotNull(evidence.Contrast);
        Assert.Equal("Passed", evidence.Contrast.Outcome);
        Assert.Equal(2, evidence.Contrast.Concurrency);
    }

    [Fact]
    public void TwoRunsOverTheSameWindowProduceByteIdenticalJson()
    {
        AnalysisContext context = TestSessionFactory.Context(
            [.. Split(highFailures: 4, lowFailures: 1)]);

        Assert.Equal(Serialize(context), Serialize(context));
    }

    [Fact]
    public void TwoIdenticallyBuiltWindowsProduceByteIdenticalJson()
    {
        string first = Serialize(TestSessionFactory.Context([.. Split(highFailures: 4, lowFailures: 1)]));
        string second = Serialize(TestSessionFactory.Context([.. Split(highFailures: 4, lowFailures: 1)]));

        Assert.Equal(first, second);
    }

    // ---------------------------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a window where the subject runs once per session, quiet in the older half and crowded
    /// in the newer, failing as many times as asked on each side.
    /// </summary>
    private static List<TestSession> Split(
        int highFailures,
        int lowFailures,
        int highSessions = 5,
        int lowSessions = 5,
        int lowConcurrency = 2,
        int highConcurrency = 8)
    {
        List<TestSession> sessions = [];

        for (int ordinal = 0; ordinal < lowSessions + highSessions; ordinal++)
        {
            bool crowded = ordinal >= lowSessions;
            int position = crowded ? ordinal - lowSessions : ordinal;
            bool failed = position < (crowded ? highFailures : lowFailures);

            sessions.Add(TestSessionFactory.Session(ordinal,
            [
                TestSessionFactory.Execution(
                    Subject,
                    failed ? TestOutcome.Failed : TestOutcome.Passed,
                    errorMessage: failed ? "boom" : null,

                    // Distinct per session so exemplars never collide on the id-based tiebreaker.
                    executionId: TestSessionFactory.ExecutionIdFor(Subject, ordinal + 1, TestOutcome.Failed),
                    concurrency: crowded ? highConcurrency : lowConcurrency)
            ]));
        }

        return sessions;
    }

    private static IReadOnlyList<FindingCandidate> Analyze(List<TestSession> sessions) =>
        [.. new ParallelSensitiveProvider().Analyze(TestSessionFactory.Context([.. sessions]))];

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
