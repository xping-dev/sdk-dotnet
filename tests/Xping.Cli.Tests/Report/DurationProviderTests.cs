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
        Assert.Equal(3, evidence.Current.NormalisedExecutions);
        Assert.Equal(3, evidence.Current.NormalisedSessions);

        Assert.Equal(200, evidence.Baseline.P50Ms);
        Assert.Equal(200, evidence.Baseline.P95Ms);
        Assert.Equal(7, evidence.Baseline.Executions);
        Assert.Equal(7, evidence.Baseline.Sessions);
        Assert.Equal(7, evidence.Baseline.NormalisedExecutions);
        Assert.Equal(7, evidence.Baseline.NormalisedSessions);
    }

    [Fact]
    public void TheDeltaIsPublishedInBothRawAndNormalisedTerms()
    {
        DurationRegressionEvidence evidence = RegressionFrom(Regressing());

        // 200ms to 800ms against an unchanged suite: the same change either way of measuring it.
        // The normalised millisecond figure is the increase in units of the run median — six —
        // multiplied by the window's reference speed, which every fixture here holds at 100ms.
        Assert.Equal(300.0, evidence.Delta.P50Pct);
        Assert.Equal(600, evidence.Delta.P50Ms);
        Assert.Equal(300.0, evidence.NormalisedDelta.P50Pct);
        Assert.Equal(600, evidence.NormalisedDelta.P50Ms);
        Assert.Equal(0.0, evidence.BaselineDispersion);
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
    public void UnreliabilityIsHalfTheNormalisedIncreaseCappedAtOne()
    {
        // Baseline 400, current 600: a normalised increase of 0.5, so half of it is 0.25.
        FindingCandidate candidate = Single(Regressing(baselineMs: 400, currentMs: 600));

        Assert.Equal(0.25, candidate.Unreliability);

        // Beyond a doubling the measure saturates rather than letting one arithmetic accident
        // crowd out every other kind in the ranking.
        Assert.Equal(1.0, Single(Regressing()).Unreliability);
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
        Assert.Equal(350.0, evidence.NormalisedDelta.P50Pct);
        Assert.Equal(700, evidence.NormalisedDelta.P50Ms);
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
        // Eight `--filter`ed runs of three fast tests, then eight full ones the subject ran in.
        // A window-wide anchor is the median of all sixteen run medians, which the filtered
        // majority puts at 2ms, and every millisecond gate would then be read fifty times too
        // small: the subject's doubling would clear the relative gate and be declined as a 12ms
        // change. The runs the subject was actually measured in all had a median of 100ms.
        AnalysisContext context = Build(
            sessions: 16,
            subjectMs: o => o < 13 ? 600 : 1200,
            companionMs: o => o < 8 ? 2 : CompanionMs,
            subjectRuns: o => o >= 8);

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

    [Theory]
    [InlineData(58, false)]     // a baseline dispersion of 0.483
    [InlineData(57, true)]      // 0.474, just inside
    [InlineData(55, true)]      // 0.457
    public void AnUnsteadyBaselineIsNotSomethingARegressionCanBeClaimedAgainst(
        int spread, bool reported)
    {
        // A test that has always swung between fast and slow has not "regressed" when it happens
        // to run slow; it has done what it always does.
        IReadOnlyList<FindingCandidate> candidates = Regressions(Build(
            sessions: 11,
            subjectMs: o => o >= 8 ? 900 : o % 2 == 0 ? 200 - spread : 200 + spread));

        Assert.Equal(reported, candidates.Count == 1);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    public void ABaselineTooThinToHaveAMedianProducesNoRegression(int baselineRuns, bool reported)
    {
        IReadOnlyList<FindingCandidate> candidates = Regressions(Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 800,

            // Runs in the three recent sessions, and in only the newest few baseline ones.
            subjectRuns: o => o >= 7 || o >= 7 - baselineRuns));

        Assert.Equal(reported, candidates.Count == 1);
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(2, true)]
    public void ABaselineWhoseRunsCannotBeNormalisedProducesNoRegression(
        int unusableRuns, bool reported)
    {
        // The gate counts the runs the comparison can use, not the runs that happened. A run whose
        // own median was zero cannot divide anything, so it contributes to neither median being
        // compared nor to the dispersion between them — and counting it would hold the claim to a
        // bar its evidence never reached. Seven baseline runs either way; three unusable leaves
        // four, which is under the floor, and two leaves five, which is not.
        IReadOnlyList<FindingCandidate> candidates = Regressions(Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 800,
            companionMs: o => o < unusableRuns ? 0 : CompanionMs));

        Assert.Equal(reported, candidates.Count == 1);
    }

    [Fact]
    public void RetryingWithinOneRunIsOneOccasionOfEvidenceRatherThanThree()
    {
        // Six baseline executions from two afternoons. Attempts of one test within a run are
        // correlated — same machine, same state, same minute — so counting them as six independent
        // observations buys a regression claim evidence it does not have.
        AnalysisContext retried = Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 800,
            subjectRuns: o => o >= 7 || o == 5 || o == 6,
            subjectAttempts: o => o < 7 ? 3 : 1);

        Assert.Empty(Regressions(retried));

        // The same slowdown seen once in each of the five separate runs the floor asks for, to
        // show it is the count of runs that declined the case above and not the shape of the
        // fixture or the size of the step.
        AnalysisContext spread = Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 800,
            subjectRuns: o => o >= 7 || o >= 2);

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
        // A baseline already swinging between 100ms and 250ms — dispersion 0.412, inside the
        // stability gate — that then steps to 800ms. The step lifts the whole window above the
        // instability threshold while the baseline stays measurable, so both kinds are earned and
        // reporting both would state one observation twice under two names.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o >= 7 ? 800 : o == 6 ? 250 : o % 2 == 0 ? 100 : 200);

        Assert.True(
            WholeWindowDispersion(context) >= LocalAnalysisConstants.DurationUnstableDispersionMin);

        Assert.Equal(FindingKind.DurationRegression, Single(Analyze(context)).Kind);
    }

    [Fact]
    public void ATestTooUnsteadyToMeasureButNotUnsteadyEnoughToReportProducesNothing()
    {
        // The gap between the two thresholds. This test swings enough that the shift it would take
        // to call it slower sits inside its own noise, and not enough for the noise itself to be
        // worth a developer's morning. Silence is the answer; one number for both gates could not
        // express it.
        AnalysisContext context = Varying(high: 270, low: 130);

        double dispersion = WholeWindowDispersion(context);

        Assert.InRange(
            dispersion,
            LocalAnalysisConstants.DurationStableDispersionMax,
            LocalAnalysisConstants.DurationUnstableDispersionMin);

        Assert.Empty(Analyze(context));
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
    public void ATestWithTwoSpeedsIsNotABaselineARegressionCanBeClaimedAgainst()
    {
        // The same blind spot on the other gate, where it costs more. A baseline of four runs at
        // 200ms and three at 600ms reads a median absolute deviation of exactly zero, so a
        // dispersion built on that alone would call it perfectly steady and let the step to 2s
        // through as a regression — against a test that was already swinging threefold.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o >= 7 ? 2000 : o < 4 ? 200 : 600);

        Assert.Empty(Regressions(context));
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
    public void ATestWithNoSpreadAtAllIsNeitherUnstableNorProtectedFromARegression()
    {
        // Zero is what the measure returns when there is nothing to measure, and it has to fall on
        // the reporting side of both gates: it clears the stability gate, so a regression against a
        // perfectly steady baseline is still claimable, and it fails the instability gate, so a
        // window with no spread never produces a finding of its own.
        Assert.Equal(0.0, WholeWindowDispersion(Build(sessions: 10, subjectMs: _ => 200)));

        Assert.Empty(Analyze(Build(sessions: 10, subjectMs: _ => 200)));
        Assert.Equal(0.0, RegressionFrom(Regressing()).BaselineDispersion);
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
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 800,
            companions: o => o == 9 ? 0 : Companions);

        Assert.Equal(FindingKind.DurationRegression, Single(Analyze(context)).Kind);
    }

    [Fact]
    public void ASessionWhoseMedianIsZeroIsLeftOutOfTheNormalisationRatherThanDividedBy()
    {
        // The xUnit adapter reports a zero duration for failures raised outside the timed
        // invocation. A run made mostly of those has a zero median, which cannot be a divisor.
        AnalysisContext context = Build(
            sessions: 10,
            subjectMs: o => o < 7 ? 200 : 800,
            companionMs: o => o == 3 ? 0 : CompanionMs);

        FindingCandidate candidate = Single(Analyze(context));
        var evidence = Assert.IsType<DurationRegressionEvidence>(candidate.Evidence);

        // The unusable run still contributes its raw milliseconds — seven baseline executions, all
        // of them counted — while contributing nothing to the normalised comparison. Both counts
        // are published, because the percentiles rest on one and the normalised median, the
        // dispersion and every gate rest on the other.
        Assert.Equal(7, evidence.Baseline.Executions);
        Assert.Equal(7, evidence.Baseline.Sessions);
        Assert.Equal(6, evidence.Baseline.NormalisedExecutions);
        Assert.Equal(6, evidence.Baseline.NormalisedSessions);

        Assert.False(double.IsNaN(evidence.NormalisedDelta.P50Pct));
        Assert.False(double.IsInfinity(evidence.NormalisedDelta.P50Pct));
    }

    [Fact]
    public void AWindowWithNoUsableRunMedianProducesNoDurationFindingOfEitherKind()
    {
        // Every run is mostly zero-duration executions, so no run has a divisor and nothing in the
        // window can be normalised. Both kinds are decided on normalised durations, so both
        // decline — the regression for having no runs it can count, the instability for having no
        // spread it can measure. Neither invents an answer from the raw milliseconds.
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
}
