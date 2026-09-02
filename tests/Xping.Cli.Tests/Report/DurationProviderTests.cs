/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

public sealed class DurationProviderTests
{
    private const string Subject = "Subject";

    // Three companions per session at a fixed duration. With three identical companions and at most
    // one subject execution, the session's median is always the companion duration whichever side
    // of it the subject falls — which is what lets a test state the normalisation divisor outright
    // instead of inferring it.
    private const int Companions = 3;
    private const int CompanionMs = 100;

    // Windows drawn per cell in the simulations. Each one builds twenty sessions and runs the whole
    // provider over them, so this is the point where the rate is measured to a tenth of a percent
    // and the suite still finishes.
    private const int NullDraws = 4_000;

    // ---------------------------------------------------------------------------------------
    // The condition
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestThatSlowedAgainstASteadyBaselineHasRegressed()
    {
        FindingCandidate candidate = Single(Regressing());

        Assert.Equal(FindingKind.DurationRegression, candidate.Kind);
        Assert.Equal(Subject, Named(candidate));
    }

    [Fact]
    public void TheEvidenceCarriesBothSidesOfTheComparisonWithTheirDenominators()
    {
        DurationRegressionEvidence evidence = RegressionFrom(Regressing(sha: o => $"sha{o}"));

        Assert.Equal(800, evidence.Current.P50Ms);
        Assert.Equal(800, evidence.Current.P95Ms);
        Assert.Equal(3, evidence.Current.Executions);
        Assert.Equal(3, evidence.Current.Sessions);
        Assert.Equal(3, evidence.Current.ComparedSessions);

        Assert.Equal(200, evidence.Baseline.P50Ms);
        Assert.Equal(200, evidence.Baseline.P95Ms);
        Assert.Equal(7, evidence.Baseline.Executions);
        Assert.Equal(7, evidence.Baseline.Sessions);
        Assert.Equal(7, evidence.Baseline.ComparedSessions);
    }

    [Fact]
    public void TheChangeIsPublishedBothOnTheClockAndAsAMeasuredRatio()
    {
        DurationRegressionEvidence evidence = RegressionFrom(Regressing());

        // 200ms to 800ms against an unchanged suite: the same change either way of measuring it.
        // Every one of the twenty-one pairs a recent run makes with a baseline run is 8 over 2, so
        // the ratio is exactly four and the interval around it is a point — which is honest, not a
        // rounding artefact: with no spread on either side there is nothing for an interval to
        // express. The millisecond figure is that ratio applied to the baseline's normalised level
        // of two run medians, at the reference speed every fixture here holds at 100ms.
        Assert.Equal(300.0, evidence.Delta.P50Pct);
        Assert.Equal(600, evidence.Delta.P50Ms);

        Assert.Equal(4.0, evidence.Shift.Ratio);
        Assert.Equal(4.0, evidence.Shift.RatioLow);
        Assert.Equal(4.0, evidence.Shift.RatioHigh);
        Assert.Equal(300.0, evidence.Shift.Pct);
        Assert.Equal(600, evidence.Shift.Ms);
    }

    [Fact]
    public void AMedianOverAnEvenNumberOfReadingsSitsBetweenTheTwoMiddleOnes()
    {
        // Eight baseline runs, four at 200ms and four at 300ms, against three recent ones at 900ms.
        // The baseline median is 250 — between the two central readings — where a nearest-rank
        // median would answer 200, the lower of them, and overstate the change by ninety points.
        DurationRegressionEvidence evidence = RegressionFrom(
            Build(sessions: 11, subjectMs: o => o < 8 ? (o < 4 ? 200 : 300) : 900));

        Assert.Equal(250, evidence.Baseline.P50Ms);
        Assert.Equal(8, evidence.Baseline.ComparedSessions);

        Assert.Equal(900, evidence.Current.P50Ms);

        // 900 against 250, not 900 against 200.
        Assert.Equal(260.0, evidence.Delta.P50Pct);
        Assert.Equal(650, evidence.Delta.P50Ms);
    }

    [Fact]
    public void ARegressionPublishesThePValueThatAdmittedIt()
    {
        FindingCandidate candidate = Single(Regressing());
        DurationRegressionEvidence evidence = RegressionFrom(Regressing());

        // Seven baseline runs and three recent ones can be dealt C(10,3) = 120 ways, and exactly
        // one of them puts all three slow runs in the recent arm. That is the whole of what three
        // runs can establish, and it is the floor no size of slowdown gets below.
        Assert.Equal(0.008333, evidence.Shift.PValue);

        // The same number reaches the coordinator, which is the only place a correction for the
        // number of tests compared can be applied.
        Assert.NotNull(candidate.PValue);
        Assert.Equal(1.0 / 120, candidate.PValue.Value, 12);
    }

    [Fact]
    public void ARegressionCarriesRecentExemplarsAndOneContrastFromBefore()
    {
        DurationRegressionEvidence evidence = RegressionFrom(Regressing(sha: o => $"sha{o}"));

        Assert.Equal(3, evidence.Exemplars.Count);
        Assert.All(evidence.Exemplars, e => Assert.Equal(800, e.DurationMs));
        Assert.All(evidence.Exemplars, e => Assert.Equal("Passed", e.Outcome));

        // Required of any finding about a change: without the prior behaviour beside it, the delta
        // is a number the reader has to take on trust.
        Assert.NotNull(evidence.Contrast);
        Assert.Equal(200, evidence.Contrast.DurationMs);
    }

    [Fact]
    public void ExemplarsAreOrderedNewestFirst()
    {
        DurationRegressionEvidence evidence = RegressionFrom(Regressing(sha: o => $"sha{o}"));

        Assert.Equal(["sha9", "sha8", "sha7"], evidence.Exemplars.Select(e => e.Sha));
    }

    [Fact]
    public void UnreliabilityIsHalfTheIncreaseTheIntervalSupportsCappedAtOne()
    {
        // Baseline 400, current 600, with no spread on either side: the interval collapses onto the
        // estimate, an increase of 0.5, so half of it is 0.25.
        FindingCandidate candidate = Single(Regressing(baselineMs: 400, currentMs: 600));

        Assert.Equal(0.25, candidate.Unreliability);

        // Beyond a doubling the measure saturates rather than letting one arithmetic accident
        // crowd out every other kind in the ranking.
        Assert.Equal(1.0, Single(Regressing()).Unreliability);
    }

    [Fact]
    public void UnreliabilityFollowsTheIntervalRatherThanTheEstimate()
    {
        // Two windows whose estimated slowdown is the same number and whose evidence for it is not.
        // Both step to 400ms; one had held at 150ms and the other had alternated between 150ms and
        // 250ms. The median pairwise ratio is 2.667 either way, so ranking on the estimate would
        // call these one finding — but a third of the second window's pairs say 1.6, and its
        // interval reaches down to say so.
        DurationRegressionEvidence steady = RegressionFrom(
            Regressing(baselineMs: 150, currentMs: 400));

        DurationRegressionEvidence spread = RegressionFrom(Build(
            sessions: 10,
            subjectMs: o => o >= 7 ? 400 : o % 2 == 0 ? 150 : 250));

        Assert.Equal(2.667, steady.Shift.Ratio);
        Assert.Equal(2.667, spread.Shift.Ratio);

        Assert.Equal(2.667, steady.Shift.RatioLow);
        Assert.Equal(1.6, spread.Shift.RatioLow);

        // And the ranking follows the bound, so the finding resting on the weaker evidence sorts
        // below the one resting on the stronger.
        Assert.Equal(
            0.833,
            Math.Round(Single(Regressing(baselineMs: 150, currentMs: 400)).Unreliability, 3));

        Assert.Equal(
            0.3,
            Math.Round(
                Single(Build(
                    sessions: 10,
                    subjectMs: o => o >= 7 ? 400 : o % 2 == 0 ? 150 : 250)).Unreliability,
                3));
    }

    [Fact]
    public void ATestThatVariesWildlyIsUnstable()
    {
        FindingCandidate candidate = Single(Analyze(Varying(high: 300, low: 100)));

        Assert.Equal(FindingKind.DurationUnstable, candidate.Kind);

        var evidence = Assert.IsType<DurationUnstableEvidence>(candidate.Evidence);

        Assert.Equal(10, evidence.Executions);
        Assert.Equal(10, evidence.Sessions);
        Assert.Equal(100, evidence.MinMs);
        Assert.Equal(300, evidence.MaxMs);

        // What the dispersion beside them was actually computed over, and the median the
        // trivial-duration floor read. The second is the baseline's — 300ms, where the window's
        // is 100ms — because that is the figure the gate was applied to, and a published number
        // that is not the one the decision used cannot be checked against the decision.
        Assert.Equal(10, evidence.NormalisedExecutions);
        Assert.Equal(300, evidence.NormalisedP50Ms);
        Assert.Equal(0.799, evidence.Dispersion);
        Assert.Equal(0.799, candidate.Unreliability, 3);
    }

    [Fact]
    public void UnstableExemplarsSpanTheSpreadRatherThanRepeatingIt()
    {
        var evidence = Assert.IsType<DurationUnstableEvidence>(Single(Analyze(Stepped())).Evidence);

        // The slowest, the most typical and the fastest — newest first, as every kind's exemplars
        // are. Three clustered at the median would describe a steady test, which is the opposite of
        // what this finding claims.
        Assert.Equal(100, evidence.MinMs);
        Assert.Equal(200, evidence.P50Ms);
        Assert.Equal(400, evidence.MaxMs);
        Assert.Equal([100, 200, 400], evidence.Exemplars.Select(e => e.DurationMs));
    }

    [Fact]
    public void ExemplarsCollapseRatherThanRepeatOneExecutionToFillTheBudget()
    {
        // With only two duration levels the median coincides with the faster of them, so the
        // typical and the fastest are the same run. Publishing it twice would pad the budget with
        // a second copy of a point the reader has already seen.
        var evidence = Assert.IsType<DurationUnstableEvidence>(
            Single(Analyze(Varying(high: 300, low: 100))).Evidence);

        Assert.Equal([100, 300], evidence.Exemplars.Select(e => e.DurationMs));
    }

    // ---------------------------------------------------------------------------------------
    // Normalisation
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AWholeSuiteRunningSlowerIsNotARegressionInAnyTest()
    {
        // Every test in the recent runs took five times as long — a busy machine, not a code
        // change. In raw milliseconds the subject went from 200ms to 1000ms, which clears both
        // regression gates comfortably; normalised against its own run it did not move at all.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 1000,
            companionMs: o => o < 7 ? CompanionMs : CompanionMs * 5);

        Assert.Empty(Analyze(context));
    }

    [Fact]
    public void TheSameShapeWithOnlyTheSubjectSlowingIsStillReported()
    {
        // The companion to the test above: identical subject durations, but the suite around it
        // held steady. Without this pair, a fixture that reported nothing would prove nothing.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 1000);

        Assert.Equal(FindingKind.DurationRegression, Single(Analyze(context)).Kind);
    }

    // ---------------------------------------------------------------------------------------
    // Machine speed, where the two scales disagree
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ASlowdownVisibleOnlyOnceMachineSpeedIsDividedOutIsStillReported()
    {
        // The mirror of AWholeSuiteRunningSlowerIsNotARegressionInAnyTest, and the case a floor
        // read off the clock silently drops. The recent runs happened on a machine five times
        // faster, so the subject's raw median *fell* from 200ms to 180ms while what it costs
        // relative to the suite around it went from twice the run median to nine times.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 180,
            companionMs: o => o < 7 ? CompanionMs : CompanionMs / 5);

        var evidence = Assert.IsType<DurationRegressionEvidence>(Single(Analyze(context)).Evidence);

        // Both figures are published and they point in opposite directions, which is the whole
        // reason the report carries two of them. A raw millisecond floor compares the first
        // against a positive threshold and declines every regression measured the second way.
        Assert.Equal(-10.0, evidence.Delta.P50Pct);
        Assert.Equal(-20, evidence.Delta.P50Ms);

        // Seven run medians above the reference speed and three below leaves the reference at
        // 100ms, so seven run medians of increase is 700ms at that speed.
        Assert.Equal(4.5, evidence.Shift.Ratio);
        Assert.Equal(350.0, evidence.Shift.Pct);
        Assert.Equal(700, evidence.Shift.Ms);
    }

    [Theory]
    [InlineData(31, false)]     // 95ms at the reference speed
    [InlineData(32, true)]      // exactly 100ms
    [InlineData(33, true)]      // 105ms
    public void TheAbsoluteFloorIsReadAtTheReferenceSpeedRatherThanOffTheClock(
        int currentMs, bool reported)
    {
        // The floor still bites once it is on the right scale — this is not a gate being dropped.
        // A 60ms subject on 100ms runs is 0.6 of its run; the recent runs are five times faster,
        // so 32ms there is 1.6 of its run, and the increase of one run median is 100ms at the
        // window's reference speed of 100ms. Every case is a large relative increase, so the
        // relative gate is satisfied throughout, and every case is a raw *decrease*, so all three
        // are declined by a floor that reads a stopwatch.
        IReadOnlyList<FindingCandidate> candidates = Regressions(Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 60 : currentMs,
            companionMs: o => o < 7 ? CompanionMs : CompanionMs / 5));

        Assert.Equal(reported, candidates.Count == 1);
    }

    [Fact]
    public void ARawMedianDraggedUnderTheFloorByFastRunsDoesNotSilenceTheFinding()
    {
        // A test that costs a whole run median in six of its runs and a tenth of one in the other
        // four. Three of the six happened on a machine ten times faster, so the clock reads 10ms
        // for those as well as for the cheap four, and the raw median lands on 10ms — under the
        // floor, and silent. What the test does in a typical run is a whole run median, which at
        // the speed its own runs typically went at is 100ms.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o < 3 ? CompanionMs / 10 : o < 6 ? CompanionMs : CompanionMs / 10,
            companionMs: o => o < 3 ? CompanionMs / 10 : CompanionMs);

        FindingCandidate candidate = Single(Analyze(context));
        var evidence = Assert.IsType<DurationUnstableEvidence>(candidate.Evidence);

        Assert.Equal(FindingKind.DurationUnstable, candidate.Kind);
        Assert.Equal(10, evidence.P50Ms);
        Assert.Equal(100, evidence.NormalisedP50Ms);
    }

    [Fact]
    public void RunsTheTestNeverAppearedInDoNotSetTheScaleItIsMeasuredOn()
    {
        // Ten `--filter`ed runs of three fast tests, then ten full ones the subject ran in. A
        // window-wide anchor is the median of all twenty run medians, which the filtered half puts
        // at 2ms, and every millisecond gate would then be read fifty times too small: the
        // subject's doubling would clear the relative gate and be declined as a 12ms change. The
        // runs the subject was actually measured in all had a median of 100ms.
        AnalysisContext context = Build(
            sessions: 20,
            subjectMs: o => o < 17 ? 600 : 1200,
            companionMs: o => o < 10 ? 2 : CompanionMs,
            subjectRuns: o => o >= 10);

        Assert.Equal(FindingKind.DurationRegression, Single(Analyze(context)).Kind);
    }

    // ---------------------------------------------------------------------------------------
    // Thresholds, at and either side of the boundary
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(599, false)]    // a 49.75% increase
    [InlineData(600, true)]     // exactly 50%
    [InlineData(601, true)]     // 50.25%
    public void TheNormalisedIncreaseDecidesWhetherASlowdownIsARegression(int currentMs, bool reported)
    {
        // A 400ms baseline keeps the absolute increase well clear of its own gate in all three
        // cases, so only the relative one is under test.
        IReadOnlyList<FindingCandidate> candidates =
            Regressions(Regressing(baselineMs: 400, currentMs: currentMs));

        Assert.Equal(reported, candidates.Count == 1);
    }

    [Theory]
    [InlineData(159, false)]    // 99ms slower
    [InlineData(160, true)]     // exactly 100ms
    [InlineData(161, true)]     // 101ms
    public void TheAbsoluteIncreaseDecidesWhetherASmallSlowdownIsWorthReporting(
        int currentMs, bool reported)
    {
        // From a 60ms baseline every case here is a large relative increase, so the relative gate
        // is satisfied throughout and the millisecond floor is what decides. Every session here
        // runs at the same speed as the window's reference, so the corrected milliseconds the
        // floor reads and the raw ones a stopwatch would read are the same number.
        IReadOnlyList<FindingCandidate> candidates =
            Regressions(Regressing(baselineMs: 60, currentMs: currentMs));

        Assert.Equal(reported, candidates.Count == 1);
    }

    [Fact]
    public void ASlowdownTheTestSOwnHistoryAlreadyContainsIsNotARegression()
    {
        // Eight baseline runs alternating between 200ms and 900ms, and three recent runs at 900ms.
        // Half the pairwise ratios are 4.5 and half are 1.0, so the estimated slowdown is 2.1 times
        // and clears the practical gate comfortably — and it is not a finding, because the recent
        // runs did nothing this test has not done in four of its last eleven. Thirty-five of the
        // hundred and sixty-five ways the runs could have been dealt put three 900ms runs in the
        // recent arm, which is a p-value of 0.21.
        //
        // This is the case the deleted baseline-dispersion gate was a stand-in for, decided now on
        // both arms rather than on the shape of one.
        AnalysisContext context = Build(
            sessions: 11,
            subjectMs: o => o >= 8 ? 900 : o % 2 == 0 ? 200 : 900);

        Assert.Empty(Regressions(context));
    }

    [Fact]
    public void AStepBeyondEverythingATestEverDidIsARegressionHoweverWideItsBaseline()
    {
        // The other half of the pair, and a deliberate change of behaviour. A baseline of four runs
        // at 200ms and three at 600ms used to disqualify itself for being unsteady, whatever the
        // recent runs did; a step to 2s is outside everything this test has ever done and there is
        // now a test that can say so — separation over ten runs, a p-value of 1/120.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o >= 7 ? 2000 : o < 4 ? 200 : 600);

        var evidence = Assert.IsType<DurationRegressionEvidence>(Single(Analyze(context)).Evidence);

        Assert.Equal(10.0, evidence.Shift.Ratio);

        // The interval is what carries the honesty here: the estimate rests on a baseline holding
        // two speeds, and the bound says so rather than leaving the reader with a bare "10x".
        Assert.Equal(3.333, evidence.Shift.RatioLow);
        Assert.Equal(10.0, evidence.Shift.RatioHigh);
    }

    [Theory]
    [InlineData(6, false)]
    [InlineData(7, true)]
    public void ABaselineTooThinForAnyArrangementToClearTheBarProducesNoRegression(
        int baselineRuns, bool reported)
    {
        // Six baseline runs against three can be dealt eighty-four ways, so the best a perfect
        // separation can say is 0.012 — above the bar, whatever the durations were. Seven can be
        // dealt a hundred and twenty ways and reaches 0.008. The floor is that arithmetic and not
        // a taste about how much history is enough.
        IReadOnlyList<FindingCandidate> candidates = Regressions(Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 800,

            // Runs in the three recent sessions, and in only the newest few baseline ones.
            subjectRuns: o => o >= 7 || o >= 7 - baselineRuns));

        Assert.Equal(reported, candidates.Count == 1);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(3, true)]
    public void ABaselineWhoseRunsCannotBeNormalisedProducesNoRegression(
        int unusableRuns, bool reported)
    {
        // The gate counts the runs the comparison can use, not the runs that happened. A run whose
        // own median was zero cannot divide anything, so it contributes no reading to either arm —
        // and counting it would hold the claim to a bar its evidence never reached. Ten baseline
        // runs either way; four unusable leaves six, which is under the floor, and three leaves
        // seven, which is not.
        IReadOnlyList<FindingCandidate> candidates = Regressions(Build(
            sessions: 13,
            subjectMs: o => o < 10 ? 200 : 800,
            companionMs: o => o < unusableRuns ? 0 : CompanionMs));

        Assert.Equal(reported, candidates.Count == 1);
    }

    [Fact]
    public void RetryingWithinOneRunIsOneOccasionOfEvidenceRatherThanThree()
    {
        // Eight baseline executions from two afternoons. Attempts of one test within a run are
        // correlated — same machine, same state, same minute — so counting them as six independent
        // observations buys a regression claim evidence it does not have. They are collapsed to one
        // reading per run before anything is computed, which leaves two occasions of evidence and
        // not six; the two-sample test the comparison now rests on assumes its readings are
        // independent, and three attempts of one test in one minute are not.
        AnalysisContext retried = Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 800,
            subjectRuns: o => o >= 7 || o == 5 || o == 6,
            subjectAttempts: o => o < 7 ? 4 : 1);

        Assert.Empty(Regressions(retried));

        // A comparable number of baseline executions, seen once in each of the seven separate runs
        // the floor asks for, to show it is the count of runs that declined the case above and not
        // the shape of the fixture or the size of the step.
        AnalysisContext spread = Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 800);

        Assert.Equal(FindingKind.DurationRegression, Single(Analyze(spread)).Kind);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void TooFewRecentRunsProduceNoRegression(int currentRuns, bool reported)
    {
        IReadOnlyList<FindingCandidate> candidates = Regressions(Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 800,
            subjectRuns: o => o < 7 || o >= 10 - currentRuns));

        Assert.Equal(reported, candidates.Count == 1);
    }

    [Theory]
    [InlineData(281, 119, false)]   // a dispersion of 0.647
    [InlineData(282, 118, true)]    // 0.655, just over
    [InlineData(300, 100, true)]    // 0.799
    public void TheDispersionDecidesWhetherATestIsUnstable(
        int high, int low, bool reported)
    {
        IReadOnlyList<FindingCandidate> candidates = Unstables(Varying(high, low));

        Assert.Equal(reported, candidates.Count == 1);
    }

    [Theory]
    [InlineData(49, 16, false)]
    [InlineData(50, 16, true)]
    [InlineData(52, 17, true)]
    public void ATrivallyFastTestIsNotReportedAsUnstable(int high, int low, bool reported)
    {
        // Below a few tens of milliseconds the dispersion measures the scheduler rather than the
        // test, so it is not evidence of anything. Every case here is above the instability
        // threshold; only the duration floor changes the answer.
        IReadOnlyList<FindingCandidate> candidates = Unstables(Varying(high, low));

        Assert.Equal(reported, candidates.Count == 1);
    }

    // ---------------------------------------------------------------------------------------
    // One judgement, one finding
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ARegressingTestIsNotAlsoReportedAsUnstable()
    {
        // A baseline already swinging between 100ms and 250ms that then steps to 800ms, clear of
        // everything before it. The step lifts the whole window above the instability threshold
        // while the recent runs separate cleanly from the baseline, so both kinds are earned and
        // reporting both would state one observation twice under two names.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o >= 7 ? 800 : o == 6 ? 250 : o % 2 == 0 ? 100 : 200);

        Assert.True(
            WholeWindowDispersion(context) >= LocalAnalysisConstants.DurationUnstableDispersionMin);

        Assert.Equal(FindingKind.DurationRegression, Single(Analyze(context)).Kind);
    }

    [Fact]
    public void ATestWithTwoSpeedsIsReportedRatherThanReadAsSteady()
    {
        // Six runs at 300ms and four at 50ms. The median absolute deviation alone reads exactly
        // zero on this — the commoner mode is the median, so the typical run sits on top of it —
        // and the test would be silently steady. The quartile estimate reads the gap between the
        // two speeds, which is what a developer would call the test's timing.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o < 6 ? 300 : 50);

        FindingCandidate candidate = Single(Analyze(context));

        Assert.Equal(FindingKind.DurationUnstable, candidate.Kind);
        Assert.Equal(0.666, Assert.IsType<DurationUnstableEvidence>(candidate.Evidence).Dispersion);
    }

    [Fact]
    public void OneOutlyingRunDoesNotMakeATestUnstable()
    {
        // Nineteen runs at 200ms and one at 1000ms. A coefficient of variation reads 0.73 on that
        // and calls the test unstable; what the test actually does, in nineteen runs out of twenty,
        // is take 200ms. A dispersion half the sample has to move before it does says so.
        AnalysisContext context = Build(
            sessions: 20,
            subjectMs: o => o == 4 ? 1000 : 200);

        Assert.Empty(Analyze(context));
    }

    [Fact]
    public void ATestWithNoSpreadAtAllIsNotUnstable()
    {
        // Zero is what the measure returns when there is nothing to measure, and it has to fall on
        // the quiet side of the gate: a window with no spread never produces a finding of its own.
        Assert.Equal(0.0, WholeWindowDispersion(Build(sessions: 10, subjectMs: _ => 200)));

        Assert.Empty(Analyze(Build(sessions: 10, subjectMs: _ => 200)));
    }

    // ---------------------------------------------------------------------------------------
    // The commit the change arrived at
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TheChangeIsDatedFromTheOldestRecentRunTheTestAppearedIn()
    {
        // Sessions 7, 8 and 9 are the recent slice; 7 is where the regression crosses into "now".
        DurationRegressionEvidence evidence = RegressionFrom(Regressing(sha: o => $"sha{o}"));

        Assert.Equal("sha7", evidence.FirstSeenAt);
    }

    [Fact]
    public void AMissingCommitIsNullRatherThanFabricated()
    {
        // Sessions recorded on a CI agent carry no commit, and inventing one would be worse than
        // admitting the report cannot say.
        DurationRegressionEvidence evidence = RegressionFrom(Regressing());

        Assert.Null(evidence.FirstSeenAt);
        Assert.All(evidence.Exemplars, e => Assert.Null(e.Sha));
    }

    // ---------------------------------------------------------------------------------------
    // Edge cases
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AnEmptyWindowProducesNothing()
    {
        var window = AnalysisWindow.Create(
            [], DateTime.UnixEpoch, DateTime.UnixEpoch, WindowResolution.Default, null);

        Assert.Empty(Analyze(new AnalysisContext(window, null)));
    }

    [Fact]
    public void ASingleSessionHasNoBeforeToCompareAgainst()
    {
        // The one session becomes the recent slice and leaves the baseline empty, so there is
        // nothing a delta could be computed against.
        Assert.Empty(Analyze(Build(sessions: 1, subjectMs: _ => 800)));
    }

    [Fact]
    public void ANewTestIsNotReportedAsHavingRegressed()
    {
        // Absent from the baseline entirely. A test added this week has history in the window but
        // no history of its own, and calling that a regression would flag every new test.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o == 7 ? 300 : o == 8 ? 900 : 100,
            subjectRuns: o => o >= 7);

        Assert.Empty(Regressions(context));
    }

    [Fact]
    public void ANewTestMayStillBeReportedAsUnstable()
    {
        // The same window as above. Instability needs no baseline — it is a standing property of
        // the executions there are, not a change between two halves of them.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o == 7 ? 300 : o == 8 ? 900 : 100,
            subjectRuns: o => o >= 7);

        Assert.Equal(FindingKind.DurationUnstable, Single(Analyze(context)).Kind);
    }

    [Fact]
    public void ATestThatStoppedRunningIsLeftToVanished()
    {
        // Present throughout the baseline and absent from every recent run. Its disappearance is
        // what is interesting about it, and claiming it here as well would report one event twice.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o % 2 == 0 ? 300 : 100,
            subjectRuns: o => o < 7);

        Assert.Empty(Analyze(context));
    }

    [Fact]
    public void ASessionRunningOnlyOneTestNormalisesItToItself()
    {
        // Its own duration is its session's median, so its normalised value is exactly one. The
        // interesting part is that nothing divides by zero on the way there.
        //
        // Placed in the baseline rather than the recent slice, where its reading of exactly one
        // would land below the six baseline readings of two and leave the recent arm holding
        // {8, 8, 1}. Fifty of the hundred and twenty arrangements are at least that favourable, so
        // the finding would be declined and this test would be measuring the wrong thing.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 800,
            companions: o => o == 0 ? 0 : Companions);

        Assert.Equal(FindingKind.DurationRegression, Single(Analyze(context)).Kind);
    }

    [Fact]
    public void ASessionWhoseMedianIsZeroIsLeftOutOfTheNormalisationRatherThanDividedBy()
    {
        // The xUnit adapter reports a zero duration for failures raised outside the timed
        // invocation. A run made mostly of those has a zero median, which cannot be a divisor.
        AnalysisContext context = Build(
            sessions: 11,
            subjectMs: o => o < 8 ? 200 : 800,
            companionMs: o => o == 3 ? 0 : CompanionMs);

        FindingCandidate candidate = Single(Analyze(context));
        var evidence = Assert.IsType<DurationRegressionEvidence>(candidate.Evidence);

        // The unusable run still contributes its raw milliseconds — eight baseline executions, all
        // of them counted — while contributing nothing to the comparison. Both counts are
        // published, because the percentiles rest on one and every gate rests on the other.
        Assert.Equal(8, evidence.Baseline.Executions);
        Assert.Equal(8, evidence.Baseline.Sessions);
        Assert.Equal(7, evidence.Baseline.ComparedSessions);

        Assert.False(double.IsNaN(evidence.Shift.Ratio));
        Assert.False(double.IsInfinity(evidence.Shift.Ratio));

        // Seven baseline runs against three deal C(10,3) = 120 ways rather than the 165 the eighth
        // would have given, so the strongest claim the evidence can make is weaker than it would be
        // with the run intact — and it says so rather than quietly reporting the number it would
        // have had.
        Assert.Equal(0.008333, evidence.Shift.PValue);
    }

    [Fact]
    public void AWindowWithNoUsableRunMedianProducesNoDurationFindingOfEitherKind()
    {
        // Every run is mostly zero-duration executions, so no run has a divisor and nothing in the
        // window can be normalised. Both kinds are decided on normalised durations, so both
        // decline — the regression for having no readings it can compare, the instability for
        // having no spread it can measure. Neither invents an answer from the raw milliseconds.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 800,
            companionMs: _ => 0);

        Assert.Empty(Analyze(context));
    }

    [Fact]
    public void AnInstantBaselineProducesNoRegressionRatherThanAnInfinity()
    {
        // A mocked test that took no measurable time has no meaningful relative increase; the
        // division would produce an infinity that then compares greater than every threshold.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 0 : 800);

        Assert.Empty(Regressions(context));
    }

    // ---------------------------------------------------------------------------------------
    // What three recent runs can establish, and what they cost
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AtTheArmFloorOneOverlappingBaselineRunIsTheDifferenceBetweenAClaimAndSilence()
    {
        // Seven baseline runs and three recent ones can be dealt a hundred and twenty ways, so
        // nothing short of every recent run being slower than every run before it reaches the bar.
        // Here one baseline run at 900ms sits above the recent slice, four arrangements are at
        // least this favourable, and the answer is 0.033 — three times the bar, and silence.
        //
        // The estimated slowdown is still fourfold and the millisecond floor is cleared six times
        // over, so this case is declined by the test and by nothing else. That is the honest cost
        // of a three-run slice rather than a defect: the finding it would have made rests on seven
        // readings, one of which could as easily have fallen the other way.
        Assert.Empty(Regressions(Build(
            sessions: 10,
            subjectMs: o => o >= 7 ? 800 : o == 6 ? 900 : 200)));
    }

    [Fact]
    public void AFindingAtTheArmFloorPublishesHowLittleItsRunsPinDown()
    {
        // The same shape without the overlap, which is reported — and the issue's question of what
        // a three-run finding should look like, answered by publishing rather than by suppressing.
        // The estimate is a fourfold slowdown and the interval reaches down to 2.7, which is the
        // whole of what ten runs can settle. A reader who wants to know how much of this to believe
        // can see it in the number rather than having to know the arm sizes.
        DurationRegressionEvidence evidence = RegressionFrom(Build(
            sessions: 10,
            subjectMs: o => o >= 7 ? 800 : o >= 4 ? 300 : 200));

        Assert.Equal(7, evidence.Baseline.ComparedSessions);
        Assert.Equal(3, evidence.Current.ComparedSessions);

        Assert.Equal(4.0, evidence.Shift.Ratio);
        Assert.Equal(2.667, evidence.Shift.RatioLow);
        Assert.Equal(4.0, evidence.Shift.RatioHigh);

        // One arrangement in a hundred and twenty, which is the smallest p-value these arm sizes
        // admit of and the reason seven is the floor.
        Assert.Equal(0.008333, evidence.Shift.PValue);
    }

    [Fact]
    public void AVeryLongBaselineIsReadFromItsMostRecentRunsAndSaysSo()
    {
        // Forty-three baseline runs, of which the comparison reads the forty most recent. The cap
        // exists because the exact test enumerates every way the pooled runs could have been split,
        // which is C(n + 3, 3) and grows as a cube, and `--runs` has no upper bound. What it costs
        // is the difference between the fortieth-oldest run and the forty-third as evidence, which
        // is nothing next to the three runs on the other side.
        //
        // The published count is the one the comparison read, not the one that happened, so a
        // reader is never told the claim rests on more history than it does.
        DurationRegressionEvidence evidence = RegressionFrom(Build(
            sessions: 46,
            subjectMs: o => o < 43 ? 200 : 800));

        Assert.Equal(43, evidence.Baseline.Sessions);
        Assert.Equal(43, evidence.Baseline.Executions);
        Assert.Equal(40, evidence.Baseline.ComparedSessions);

        // One arrangement in C(43,3) = 12341. Three decimals would publish that as zero, which is
        // a certainty this measurement never has.
        Assert.Equal(0.000081, evidence.Shift.PValue);
    }

    [Fact]
    public void TheEnumerationTheExactTestPerformsStaysBounded()
    {
        // The provider truncates either arm to its forty most recent runs so that the exact test's
        // enumeration stays affordable. That safety property is not a property of forty: the count
        // is C(40 + k, k) for a recent slice of k, and it grows as k rises — 12,341 at the shipped
        // slice of three, 1.2 million at five, and past anything a report could finish at ten.
        //
        // So the two constants have to move together, and this is the test that says so. Raising
        // CurrentSliceSize without lowering the truncation turns `xping report` into a hang, which
        // is the kind of thing that should fail here rather than on a developer's machine.
        long arrangements = 1;
        for (int i = 0; i < LocalAnalysisConstants.CurrentSliceSize; i++)
            arrangements = arrangements * (40 + LocalAnalysisConstants.CurrentSliceSize - i) / (i + 1);

        Assert.InRange(arrangements, 1, 20_000);
    }

    [Theory]
    [InlineData(0.20, 200)]
    [InlineData(0.35, 200)]
    [InlineData(0.50, 200)]
    [InlineData(0.70, 200)]
    [InlineData(0.50, 1000)]
    [InlineData(0.70, 1000)]
    public void NoTrueShiftIsReportedAsARegressionMoreThanOnceInAHundred(
        double coefficient, int median)
    {
        // The issue's own measurement, re-run against this branch. Twenty runs of lognormal
        // durations with nothing planted in them, seventeen baseline against three recent, through
        // every gate the provider ships. What is being asked is not whether the test is exact —
        // BrunnerMunzelTests asks that — but what the exact test plus the practical floor beside it
        // amount to end to end, because that product is what a developer sees.
        //
        // Shipped, this reaches 6.3%, which #160 would then multiply by the number of tests in the
        // suite. Here the ceiling is the level the test is read at and nothing can lift it above
        // that; the practical floor beside it only pushes the rate further down. Over forty
        // thousand windows per cell the measured rates are 0.0004, 0.0062, 0.0089 and 0.0096 as the
        // dispersion climbs, and the same again at the larger median. The band below allows two
        // standard errors of Monte-Carlo noise at the four thousand a test suite can afford, which
        // is what separates 0.0096 from 0.01 — the claim is the ceiling, and this is its check.
        ulong state = 20260902UL + (ulong)(coefficient * 100) + (ulong)median;

        double sigma = Math.Sqrt(Math.Log(1 + (coefficient * coefficient)));
        int reported = 0;

        for (int draw = 0; draw < NullDraws; draw++)
        {
            int[] durations = new int[20];
            for (int session = 0; session < durations.Length; session++)
                durations[session] = (int)Math.Round(median * Math.Exp(sigma * Gaussian(ref state)));

            if (Regressions(Build(sessions: 20, subjectMs: o => durations[o])).Count > 0)
                reported++;
        }

        Assert.InRange((double)reported / NullDraws, 0, 0.012);
    }

    [Theory]
    [InlineData(0.8, 0.2, 0.0, 0.005)]      // the baseline is the wilder arm: far under the level
    [InlineData(0.2, 0.8, 0.04, 0.09)]      // the recent slice is: above it
    public void AWiderRecentSliceIsWhereTheOneInAHundredStops(
        double baselineSigma, double currentSigma, double low, double high)
    {
        // The same limit as BrunnerMunzelTests measures on the statistic, carried through every gate
        // the provider ships so the number is the one a developer would actually meet. Both arms are
        // centred on 200ms and differ only in spread, so nothing has slowed.
        //
        // Where the recent three runs are the wilder arm the rate is several times the level, and
        // the finding a reader gets says "slower" about a test whose typical duration has not moved.
        // It is not nothing — the test's variability really did change — but `DurationUnstable` is
        // the kind that claim belongs to, and a regression suppresses it. #187 owns the gap; this
        // pins its size.
        ulong state = 20260902UL + (ulong)(baselineSigma * 100) + (ulong)(currentSigma * 1000);

        int reported = 0;

        for (int draw = 0; draw < NullDraws; draw++)
        {
            int[] durations = new int[20];
            for (int session = 0; session < durations.Length; session++)
            {
                double sigma = session >= 17 ? currentSigma : baselineSigma;
                durations[session] = (int)Math.Round(200 * Math.Exp(sigma * Gaussian(ref state)));
            }

            if (Regressions(Build(sessions: 20, subjectMs: o => durations[o])).Count > 0)
                reported++;
        }

        Assert.InRange((double)reported / NullDraws, low, high);
    }

    [Fact]
    public void APlantedDoublingOnASteadyTestIsStillReported()
    {
        // The other side of the bargain, and the issue's second acceptance criterion. The criterion
        // asks for five recent executions and the window cannot supply them — AnalysisWindow fixes
        // the recent slice at three runs — so this is the same claim at the size the report
        // actually works with, which is the harder case.
        ulong state = 20260902UL;

        double sigma = Math.Sqrt(Math.Log(1 + (0.20 * 0.20)));
        int reported = 0;

        for (int draw = 0; draw < NullDraws; draw++)
        {
            int[] durations = new int[20];
            for (int session = 0; session < durations.Length; session++)
            {
                double factor = session >= 17 ? 2 : 1;
                durations[session] =
                    (int)Math.Round(200 * factor * Math.Exp(sigma * Gaussian(ref state)));
            }

            if (Regressions(Build(sessions: 20, subjectMs: o => durations[o])).Count > 0)
                reported++;
        }

        Assert.InRange((double)reported / NullDraws, 0.90, 1.0);
    }

    // ---------------------------------------------------------------------------------------
    // Determinism
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TwoRunsOverTheSameWindowProduceByteIdenticalJson()
    {
        AnalysisContext context = Regressing(sha: o => $"sha{o}");

        Assert.Equal(Serialize(context), Serialize(context));
    }

    [Fact]
    public void TwoIdenticallyBuiltWindowsProduceByteIdenticalJson()
    {
        // Rebuilt from scratch rather than reused, so anything leaking in from allocation order or
        // dictionary enumeration would show up here and not in the run-twice case.
        string first = Serialize(Regressing(sha: o => $"sha{o}"));
        string second = Serialize(Regressing(sha: o => $"sha{o}"));

        Assert.Equal(first, second);
    }

    [Fact]
    public void TheSameWindowProducesTheSameCandidatesInTheSameOrder()
    {
        AnalysisContext context = Varying(high: 300, low: 100);

        Assert.Equal(
            Analyze(context).Select(c => c.Kind),
            Analyze(context).Select(c => c.Kind));
    }

    // ---------------------------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Builds a window whose subject steps from one duration to another at the slice boundary.
    /// </summary>
    /// <param name="baselineMs">What the subject took before.</param>
    /// <param name="currentMs">What it takes now.</param>
    /// <param name="sha">The commit each session ran at, given its ordinal.</param>
    private static AnalysisContext Regressing(
        int baselineMs = 200, int currentMs = 800, Func<int, string?>? sha = null) =>
        Build(sessions: 10, subjectMs: o => o < 7 ? baselineMs : currentMs, sha: sha);

    /// <summary>
    /// Builds a window whose subject alternates between two durations.
    /// </summary>
    /// <remarks>
    /// The slower half is the older half, so the recent runs are never the slow ones — otherwise
    /// the window would read as a regression and the instability finding would be suppressed
    /// before the threshold under test was reached.
    /// </remarks>
    /// <param name="high">The slower duration.</param>
    /// <param name="low">The faster duration.</param>
    private static AnalysisContext Varying(int high, int low) =>
        Build(sessions: 10, subjectMs: o => o < 5 ? high : low);

    /// <summary>
    /// Builds a window whose subject settles at three distinct durations, slowest first.
    /// </summary>
    /// <remarks>
    /// Three levels rather than two so that the fastest, the median and the slowest are three
    /// different runs — which is the only arrangement in which spanning exemplars have anything to
    /// span. Ordered slowest-to-fastest so the window cannot read as a regression.
    /// </remarks>
    private static AnalysisContext Stepped() =>
        Build(sessions: 10, subjectMs: o => o < 4 ? 400 : o < 7 ? 200 : 100);

    /// <summary>
    /// Builds a window of sessions, each running the subject alongside fixed-duration companions.
    /// </summary>
    /// <param name="sessions">Sessions to build; ordinal 0 is the oldest.</param>
    /// <param name="subjectMs">What the subject took, given the session ordinal.</param>
    /// <param name="companionMs">What each companion took, given the session ordinal.</param>
    /// <param name="companions">How many companions ran, given the session ordinal.</param>
    /// <param name="subjectRuns">Whether the subject ran at all, given the session ordinal.</param>
    /// <param name="subjectAttempts">How many attempts the subject took, given the ordinal.</param>
    /// <param name="sha">The commit the session ran at, given its ordinal.</param>
    private static AnalysisContext Build(
        int sessions,
        Func<int, int> subjectMs,
        Func<int, int>? companionMs = null,
        Func<int, int>? companions = null,
        Func<int, bool>? subjectRuns = null,
        Func<int, int>? subjectAttempts = null,
        Func<int, string?>? sha = null)
    {
        var built = new List<TestSession>(sessions);

        for (int ordinal = 0; ordinal < sessions; ordinal++)
        {
            var executions = new List<TestExecution>();

            if (subjectRuns?.Invoke(ordinal) ?? true)
            {
                int attempts = subjectAttempts?.Invoke(ordinal) ?? 1;

                for (int attempt = 1; attempt <= attempts; attempt++)
                    executions.Add(Execution(Subject, ordinal, subjectMs(ordinal), attempt));
            }

            for (int companion = 0; companion < (companions?.Invoke(ordinal) ?? Companions); companion++)
            {
                executions.Add(Execution(
                    $"Companion{companion}", ordinal, companionMs?.Invoke(ordinal) ?? CompanionMs));
            }

            built.Add(TestSessionFactory.Session(ordinal, executions, sha: sha?.Invoke(ordinal)));
        }

        return TestSessionFactory.Context([.. built]);
    }

    /// <summary>
    /// Builds one execution with an id unique to its test and session.
    /// </summary>
    /// <remarks>
    /// The factory's default id is derived from the name, attempt and outcome alone, so the same
    /// test passing in ten sessions would carry one id ten times — and exemplar selection, which
    /// deduplicates and breaks ties on that id, would be testing a fiction.
    /// </remarks>
    private static TestExecution Execution(string name, int ordinal, int durationMs, int attempt = 1) =>
        TestSessionFactory.Execution(
            name,
            durationMs: durationMs,
            attempt: attempt,

            // Attempts past the first are folded into the name the id is derived from rather than
            // into the ordinal, which would collide with the first attempt of a later session.
            executionId: TestSessionFactory.ExecutionIdFor(
                attempt == 1 ? name : $"{name}#{attempt}", ordinal, TestOutcome.Passed),
            retry: attempt > 1);

    private static IReadOnlyList<FindingCandidate> Analyze(AnalysisContext context) =>
        [.. new DurationProvider().Analyze(context)];

    private static IReadOnlyList<FindingCandidate> Regressions(AnalysisContext context) =>
        [.. Analyze(context).Where(c => c.Kind == FindingKind.DurationRegression)];

    private static IReadOnlyList<FindingCandidate> Unstables(AnalysisContext context) =>
        [.. Analyze(context).Where(c => c.Kind == FindingKind.DurationUnstable)];

    /// <summary>
    /// Asserts that exactly one candidate was produced, and returns it.
    /// </summary>
    /// <remarks>
    /// The companions are deliberately identical in every session, so any candidate beyond the
    /// subject's is the fixture leaking rather than the provider working.
    /// </remarks>
    private static FindingCandidate Single(IReadOnlyList<FindingCandidate> candidates) =>
        Assert.Single(candidates);

    private static FindingCandidate Single(AnalysisContext context) => Single(Analyze(context));

    private static DurationRegressionEvidence RegressionFrom(AnalysisContext context) =>
        Assert.IsType<DurationRegressionEvidence>(Single(Analyze(context)).Evidence);

    private static string Named(FindingCandidate candidate) =>
        Assert.IsType<FindingSubject.SingleTest>(candidate.Subject).Test.DisplayName;

    /// <summary>
    /// Recomputes the window's dispersion for the subject, to show a gate really decided something
    /// rather than the threshold never having been reached.
    /// </summary>
    /// <remarks>
    /// Deliberately a second implementation rather than a call to <c>RobustDispersion</c>: a test
    /// that asks the code under test what the answer is cannot show that the answer was needed.
    /// </remarks>
    private static double WholeWindowDispersion(AnalysisContext context)
    {
        List<double> normalised = [];

        foreach (TestSession session in context.Window.Sessions)
        {
            List<double> durations = [.. session.Executions.Select(e => e.Duration.TotalMilliseconds)];
            durations.Sort();

            double median = durations[(int)Math.Ceiling(0.5 * durations.Count) - 1];

            normalised.AddRange(session.Executions
                .Where(e => e.TestName == Subject)
                .Select(e => e.Duration.TotalMilliseconds / median));
        }

        normalised.Sort();

        double centre = Middle(normalised);
        List<double> deviations = [.. normalised.Select(v => Math.Abs(v - centre))];
        deviations.Sort();

        // The same median-unbiasing factors the helper carries, spelled out for the counts these
        // fixtures build so a change to that table cannot silently agree with itself.
        double correction = normalised.Count switch
        {
            3 => 1.4136,
            10 => 1.0778,
            20 => 1.0262,
            _ => throw new InvalidOperationException($"no factor for {normalised.Count} executions")
        };

        double spread = Math.Max(
            1.4826 * Middle(deviations),
            (Quartile(normalised, 0.75) - Quartile(normalised, 0.25)) / 1.349);

        return correction * spread / centre;
    }

    /// <summary>
    /// Reads the median of a sorted list, averaging the two central values at an even count.
    /// </summary>
    private static double Middle(List<double> sorted) =>
        sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2;

    /// <summary>
    /// Reads a quantile of a sorted list by linear interpolation between the two nearest readings.
    /// </summary>
    private static double Quartile(List<double> sorted, double quantile)
    {
        double position = (sorted.Count - 1) * quantile;
        int lower = (int)Math.Floor(position);
        int upper = Math.Min(lower + 1, sorted.Count - 1);

        return sorted[lower] + ((position - lower) * (sorted[upper] - sorted[lower]));
    }

    private static string Serialize(AnalysisContext context)
    {
        using var warnings = new StringWriter();

        AnalysisResult result =
            new FindingCoordinator([new DurationProvider()]).Run(context, null, warnings);

        ReportEnvelope envelope = EnvelopeBuilder.Build(
            context, result, incompleteSessions: 0, unreadableSessions: 0, top: null);

        return JsonSerializer.Serialize(envelope, ReportJsonOptions.Default);
    }

    /// <summary>
    /// Draws one standard normal value by the Box–Muller transform.
    /// </summary>
    /// <remarks>
    /// Hand-written rather than <see cref="Random"/> for the reason <c>RobustDispersionTests</c>
    /// gives: a seeded <see cref="Random"/> sequence is not guaranteed stable across runtime
    /// versions, and a simulation whose answer moves with the runtime is not an assertion.
    /// </remarks>
    private static double Gaussian(ref ulong state) =>
        Math.Sqrt(-2 * Math.Log(1 - Uniform(ref state))) *
        Math.Cos(2 * Math.PI * Uniform(ref state));

    /// <summary>
    /// Draws one value in [0,1) from a splitmix64 generator.
    /// </summary>
    private static double Uniform(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;

        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;

        return (z >> 11) * (1.0 / 9007199254740992.0);
    }
}
