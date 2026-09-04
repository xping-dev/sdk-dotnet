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

public sealed class TimeSensitiveProviderTests
{
    private const string Subject = "Subject";

    // Windows per cell in the null simulation below. Enough that the standard error is a quarter of
    // a percentage point, which separates the rate this branch produces from the level it claims,
    // and cheap enough to run seven times in a test suite: each window is at most six Fisher tests
    // over forty runs, and most are declined before the first of them.
    private const int NullDraws = 4_000;

    /// <summary>The zone every fixture session agrees on unless it is testing disagreement.</summary>
    private const string Zone = "Europe/Berlin";

    /// <summary>The offset every fixture applies unless it is testing a shift.</summary>
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    // August 2026 opens on a Saturday, so the 1st, 2nd, 8th, 9th, 15th and 16th are weekend days and
    // the 3rd to the 7th are a full working week. Fixtures below name days of that month directly
    // rather than counting from an epoch, because a temporal test that hides which day it means is a
    // test nobody can check.

    // ---------------------------------------------------------------------------------------
    // The condition
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestThatFailsOnlyInTheEveningIsSensitive()
    {
        FindingCandidate candidate = Single(TimeOfDay(eveningFailures: 6, morningFailures: 0));

        Assert.Equal(FindingKind.TimeSensitive, candidate.Kind);
        Assert.Equal(Subject, Named(candidate));
    }

    [Fact]
    public void RetriesWithinASessionDoNotFillAnArm()
    {
        // Three evening runs, each failing twice, are six evening executions and used to clear an
        // arm gate of five. They are three occasions on one clock: every attempt of a run shares its
        // session's local hour, so they were never separate observations of the evening at all.
        Assert.Empty(Analyze(RetriedEvenings(eveningSessions: 3, attempts: 2)));
    }

    [Fact]
    public void AnArmOfFiveMeansFiveDistinctSessions()
    {
        // The same shape with five evening runs qualifies, and the evidence publishes what the gate
        // was applied to — five runs, not the ten executions they took.
        TimeSensitiveEvidence evidence =
            EvidenceFrom(RetriedEvenings(eveningSessions: 5, attempts: 2));

        Assert.Equal(5, evidence.Worse.Sessions);
        Assert.Equal(5, evidence.Worse.Failures);
        Assert.Equal(1, evidence.Worse.FailureRate);
        Assert.Equal(6, evidence.Other.Sessions);
        Assert.Equal(0, evidence.Other.Failures);
    }

    [Fact]
    public void ARunIsJudgedOnItsFinalAttemptRatherThanOnEveryAttempt()
    {
        // A run that failed twice and passed on the third did not fail. Counting attempts made it
        // two failures in three, which inflated the arm's rate with the very behaviour that rescued
        // it — the argument RetryProvider has always made about runs, applied here.
        List<TestSession> sessions = [];

        for (int i = 0; i < 6; i++)
        {
            sessions.Add(Session(
                i,
                Local(3 + i, 20),
                [
                    TestSessionFactory.Execution(
                        Subject, TestOutcome.Failed, attempt: 1, maxRetries: 1, errorMessage: "boom",
                        executionId: TestSessionFactory.ExecutionIdFor(Subject, i, TestOutcome.Failed)),
                    TestSessionFactory.Execution(
                        Subject, TestOutcome.Passed, attempt: 2, maxRetries: 1, passedOnRetry: true,
                        executionId: TestSessionFactory.ExecutionIdFor(Subject, i, TestOutcome.Passed))
                ],
                Offset));
        }

        for (int i = 0; i < 6; i++)
        {
            int ordinal = 6 + i;
            sessions.Add(Session(ordinal, Local(3 + i, 9), [Execution(ordinal, failed: false)], Offset));
        }

        Assert.Empty(Analyze(sessions));
    }

    [Fact]
    public void TheEvidenceCarriesBothArmsWithTheirDenominators()
    {
        TimeSensitiveEvidence evidence =
            EvidenceFrom(TimeOfDay(eveningFailures: 5, morningFailures: 0));

        Assert.Equal(5, evidence.Worse.Failures);
        Assert.Equal(6, evidence.Worse.Sessions);
        Assert.Equal(0.833, evidence.Worse.FailureRate);

        Assert.Equal(0, evidence.Other.Failures);
        Assert.Equal(6, evidence.Other.Sessions);
        Assert.Equal(0, evidence.Other.FailureRate);
    }

    [Fact]
    public void TheEvidenceCountsTheDaysTheFailuresFellOnRatherThanTheArmsSpan()
    {
        // Eight evening runs across five local days, six of them red and those six falling on three
        // of the five. The published figure is the one the three-day gate was applied to, so a
        // reader can check the gate rather than infer it.
        //
        // The two counts have to differ for this test to mean anything. Its earlier fixture ran one
        // failure per day, which made the failure count and the failure-day count the same number —
        // so it would have passed just as readily against code that published the wrong one.
        TimeSensitiveEvidence evidence =
            EvidenceFrom(TimeOfDay(eveningFailures: 6, morningFailures: 0, evenings: 8, failureDays: 3));

        Assert.Equal(8, evidence.Worse.Sessions);
        Assert.Equal(3, evidence.Worse.DistinctFailureDates);

        // Nothing failed on the other side, so it spans no failure days at all.
        Assert.Equal(0, evidence.Other.DistinctFailureDates);
    }

    [Fact]
    public void TheArmsAreLabelledAndTheZoneTheyWereReadInIsPublished()
    {
        TimeSensitiveEvidence evidence =
            EvidenceFrom(TimeOfDay(eveningFailures: 6, morningFailures: 0));

        Assert.Equal("18:00-24:00 local", evidence.Worse.Label);
        Assert.Equal("the rest of the day", evidence.Other.Label);
        Assert.Equal(Zone, evidence.TimeZoneId);
    }

    [Fact]
    public void UnreliabilityIsTheLeastGapTheArmsSupport()
    {
        // Six of six in the evening against one of six in the morning: an observed gap of 0.83 that
        // six runs a side support down to 0.28. What the report ranks on is the second figure, so
        // this finding sits below one measured over a whole window rather than above it on the
        // strength of the smallest arms the provider allows.
        FindingCandidate candidate = Single(TimeOfDay(eveningFailures: 6, morningFailures: 1));

        Assert.Equal(0.277, candidate.Unreliability, 3);
    }

    // ---------------------------------------------------------------------------------------
    // The axes
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestThatFailsOnlyAtWeekendsSplitsOnTheDayGroup()
    {
        TimeSensitiveEvidence evidence = EvidenceFrom(DayGroup());

        Assert.Equal("LocalDayGroup", evidence.Axis);
        Assert.Equal("weekend", evidence.Worse.Label);
        Assert.Equal("weekday", evidence.Other.Label);
        Assert.Equal(1.0, evidence.Delta.FailureRate);
    }

    [Fact]
    public void AWindowStraddlingAnOffsetChangeSplitsOnTheOffset()
    {
        // The shape a daylight-saving transition leaves behind: one zone, two offsets, and the
        // failures all on one side of the change.
        TimeSensitiveEvidence evidence = EvidenceFrom(OffsetShift());

        Assert.Equal("UtcOffsetShift", evidence.Axis);
        Assert.Equal("UTC+01:00", evidence.Worse.Label);
        Assert.Equal("UTC+02:00", evidence.Other.Label);
    }

    [Fact]
    public void AMachineThatChangedZoneIsNotReadAsADaylightSavingShift()
    {
        // Two offsets again, but the zone moved with them. Nothing here is a transition, and a local
        // hour drawn from two zones describes neither of them.
        Assert.Empty(Analyze(OffsetShift(laterZone: "America/New_York")));
    }

    [Fact]
    public void TheWinnerOfATwoAxisSearchIsChargedForBothOfThem()
    {
        IReadOnlyList<FindingCandidate> candidates = Analyze(BothAxes());

        // Weekend against weekday is a gap of 0.64; evening against the rest of the day is 0.90.
        FindingCandidate candidate = Assert.Single(candidates);
        var evidence = Assert.IsType<TimeSensitiveEvidence>(candidate.Evidence);

        Assert.Equal("LocalTimeOfDay", evidence.Axis);
        Assert.Equal(0.9, evidence.Delta.FailureRate);
        Assert.Equal(0.405, candidate.Unreliability, 3);

        // Two divisions were available and both were charged for, which is the whole of what this
        // fixture is now for. The winner's raw 0.00087 becomes 0.0017 and still qualifies. The day
        // group's raw 0.034 becomes 0.067 and no longer does — so the search charge does not merely
        // reorder the two axes here, it removes one of them.
        Assert.Equal(2, evidence.Significance.ComparisonsTried);
        Assert.Equal(0.00175, evidence.Significance.PValue);
    }

    [Fact]
    public void TheLeastProbableAxisWinsEvenWhenAnotherShowsAWiderAndBetterSupportedGap()
    {
        // Five clean evening runs against thirty mornings split into fifteen weekend runs that all
        // failed and fifteen weekday runs of which nine did.
        //
        // The two axes disagree about everything. Time of day shows the wider gap - 0.80 against the
        // day group's 0.55 - and, over thirty runs against five, even the larger supported one:
        // 0.33 against 0.26. The day group is nonetheless the less probable division of the two,
        // 0.00055 against 0.0014, because twenty-four failures in thirty mornings against none in
        // five evenings is a thing five runs can easily miss, and fifteen weekends all red against
        // nine weekdays in twenty is not.
        //
        // So the winner is chosen on the p-value, and the two quantities beneath it in the tie-break
        // both point the other way. Choosing on either of them would publish the time-of-day axis.
        FindingCandidate candidate = Single(WiderButThinner());
        var evidence = Assert.IsType<TimeSensitiveEvidence>(candidate.Evidence);

        Assert.Equal("LocalDayGroup", evidence.Axis);
        Assert.Equal("weekend", evidence.Worse.Label);
        Assert.Equal(0.55, evidence.Delta.FailureRate);

        // Ranked on the winner's supported gap, which is the smaller of the two on offer. The rank
        // follows the split the evidence chose rather than the widest one available.
        Assert.Equal(0.259, candidate.Unreliability, 3);

        // Two genuinely different divisions of these runs, so both were charged for.
        Assert.Equal(2, evidence.Significance.ComparisonsTried);
        Assert.Equal(0.0011, evidence.Significance.PValue, 4);
    }

    [Fact]
    public void TheFailingSideIsNamedRatherThanDescribedAsTheRemainder()
    {
        // Failures in the evening and passes in the morning divide the executions the same way twice
        // over: the evening quarter against the rest, and the morning quarter against the rest. Both
        // gaps are 1.0, and only one of them tells a reader when to look.
        TimeSensitiveEvidence evidence =
            EvidenceFrom(TimeOfDay(eveningFailures: 6, morningFailures: 0));

        Assert.Equal("18:00-24:00 local", evidence.Worse.Label);
    }

    // ---------------------------------------------------------------------------------------
    // Direction
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestThatFailsOnlyInTheMorningIsAlsoSensitive()
    {
        TimeSensitiveEvidence evidence =
            EvidenceFrom(TimeOfDay(eveningFailures: 0, morningFailures: 6));

        Assert.Equal("06:00-12:00 local", evidence.Worse.Label);
        Assert.Equal(6, evidence.Worse.Failures);
        Assert.Equal(0, evidence.Other.Failures);
    }

    [Fact]
    public void ExemplarsComeFromTheArmHoldingTheFailures()
    {
        TimeSensitiveEvidence evidence =
            EvidenceFrom(TimeOfDay(eveningFailures: 6, morningFailures: 0));

        Assert.All(evidence.Exemplars, e => Assert.Equal("Failed", e.Outcome));
        Assert.All(evidence.Exemplars, e => Assert.EndsWith("20:00", e.LocalStartedAt, StringComparison.Ordinal));

        Assert.NotNull(evidence.Contrast);
        Assert.Equal("Passed", evidence.Contrast.Outcome);
        Assert.EndsWith("09:00", evidence.Contrast.LocalStartedAt, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // What does not qualify
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ExecutionsWithNoRecordedOffsetAreExcludedRatherThanAssumedToBeOnUtc()
    {
        // The same perfect split, from sessions written before the offset was recorded. Reading them
        // as UTC would invent the very measurement the finding is about, and on a machine two hours
        // ahead it would file every evening run as an afternoon one.
        Assert.Empty(Analyze(TimeOfDay(eveningFailures: 6, morningFailures: 0, recordOffset: false)));
    }

    [Fact]
    public void ASideWithTooFewExecutionsIsNotCompared()
    {
        // Ten executions, but only four of them in the evening: one short of the gate either way.
        Assert.Empty(Analyze(TimeOfDay(eveningFailures: 4, morningFailures: 0, evenings: 4)));
    }

    [Fact]
    public void AGapBelowTheThresholdIsNotAFinding()
    {
        // 0.5 in the evening against 0.33 in the morning is a gap of 0.17, under the threshold.
        Assert.Empty(Analyze(TimeOfDay(eveningFailures: 3, morningFailures: 2)));
    }

    [Fact]
    public void FailuresConfinedToASingleLocalDayAreAnIncidentRatherThanAPattern()
    {
        // A perfect 1.0-against-0.0 split over twelve executions — and every failure inside one
        // evening. This is the case the other two gates wave through and the report must not: it is
        // one bad session wearing the language of a recurring pattern.
        Assert.Empty(Analyze(SingleEvening()));
    }

    [Fact]
    public void FailuresOnTwoDaysAreOneShortOfAPattern()
    {
        // Six red evenings against six clean mornings either way, so the gap, the arms and the
        // p-value are identical in both halves and the day count is the only thing that moves. It
        // fails on two days — and one day more, below, qualifies.
        Assert.Empty(Analyze(TimeOfDay(eveningFailures: 6, morningFailures: 0, failureDays: 2)));

        Assert.Equal(
            FindingKind.TimeSensitive,
            Single(TimeOfDay(eveningFailures: 6, morningFailures: 0, failureDays: 3)).Kind);
    }

    // ---------------------------------------------------------------------------------------
    // The search, and what it costs
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ThreeRedEveningsOutOfSixIsNotAPattern()
    {
        // The case #161 is about. Three of six evenings red against six clean mornings is a gap of
        // 0.50, clears the delta bar twice over, spans three separate local days, and is the single
        // commonest way six failures and six passes fall when nothing is happening: p = 0.18. The
        // report used to call it an evening pattern.
        Assert.Empty(Analyze(TimeOfDay(eveningFailures: 3, morningFailures: 0)));

        // Two more red evenings is the same shape and a different claim, at p = 0.0022.
        Assert.Equal(
            FindingKind.TimeSensitive,
            Single(TimeOfDay(eveningFailures: 5, morningFailures: 0)).Kind);
    }

    [Fact]
    public void ASensitiveTestPublishesThePValueThatAdmittedIt()
    {
        FindingCandidate candidate = Single(TimeOfDay(eveningFailures: 6, morningFailures: 0));
        var evidence = Assert.IsType<TimeSensitiveEvidence>(candidate.Evidence);

        // Twelve runs split six and six can deal six failures 924 ways, and exactly one of them puts
        // all six in the evening. Its mirror image is equally probable and equally extreme, so the
        // two-sided answer is 2/924 — and one comparison was charged for, so that is also the
        // published figure, to the three digits it is written down with.
        Assert.Equal(0.00216, evidence.Significance.PValue);

        // The same number reaches the coordinator, unrounded, which is the only place a correction
        // for the number of tests compared can be applied.
        Assert.NotNull(candidate.PValue);
        Assert.Equal(2.0 / 924, candidate.PValue.Value, 12);
    }

    [Fact]
    public void TheSameGapSurvivesOneComparisonAndNotTwo()
    {
        // Five red evenings of ten against ten clean mornings, twice over. The 2x2 table is
        // identical in both windows and so is its own p-value, 0.0325. All that differs is how many
        // ways the runs could be divided: in the first the weekend is too thin to be an arm, in the
        // second the same runs are dealt so that it is not.
        //
        // One comparison reports it. Two charge it 0.065 and it falls silent — which is the whole of
        // what "a wider search is expensive" means, and why the docs cannot say five failures is
        // enough without saying how wide the search was.
        TimeSensitiveEvidence narrow =
            EvidenceFrom(TimeOfDay(eveningFailures: 5, morningFailures: 0, evenings: 10, mornings: 10));

        Assert.Equal(1, narrow.Significance.ComparisonsTried);
        Assert.Equal(0.0325, narrow.Significance.PValue, 4);

        Assert.Empty(Analyze(WeekendEvenings(eveningFailures: 5)));

        // And silent for that reason and no other: one more red evening is 0.0108, which survives
        // being charged twice, and the same window then reports — so what the assertion above sees
        // is the correction and not some earlier gate quietly declining the fixture.
        TimeSensitiveEvidence wide = EvidenceFrom(WeekendEvenings(eveningFailures: 6));

        Assert.Equal(2, wide.Significance.ComparisonsTried);
        Assert.Equal(0.0217, wide.Significance.PValue, 4);
    }

    [Fact]
    public void ALongWindowPublishesASmallProbabilityRatherThanACertainOne()
    {
        // Thirteen red evenings against thirteen clean mornings. Twenty-six runs is an ordinary
        // window, and a perfect split of it is 2/C(26,13) = 1.9e-07 — which the six decimal places
        // the duration provider publishes with would have written down as zero.
        //
        // Six decimals are safe there and not here. A duration regression compares a fixed three
        // recent runs against at most forty, so its p-value cannot fall below 1/12341 however long
        // the history; this one is the probability of the observed table and falls away as the
        // window grows. A published zero is a claim of certainty, and this measurement never makes
        // one.
        TimeSensitiveEvidence evidence =
            EvidenceFrom(TimeOfDay(eveningFailures: 13, morningFailures: 0, evenings: 13, mornings: 13));

        // Two divisions qualify on a window this long — the weekend has runs enough of its own — so
        // the charged figure is twice the split's own 1.9e-07.
        Assert.Equal(2, evidence.Significance.ComparisonsTried);
        Assert.Equal(3.85e-7, evidence.Significance.PValue, 9);

        // Written down as a probability rather than as a zero, which is the whole of the claim.
        var (_, metrics) = EvidenceHeadline.For(FindingKind.TimeSensitive, evidence);
        Assert.Contains(metrics, m => m.Value == "p 3.85e-07 two-sided, 2 splits compared");
    }

    [Theory]
    [InlineData(13, "3.85e-07")]
    [InlineData(20, "2.9e-11")]
    [InlineData(28, "5.23e-16")]
    public void NoWindowLengthRoundsAProbabilityDownToCertainty(int arm, string expected)
    {
        // The rounding step has to survive every window the arithmetic behind it survives, and it is
        // its own opportunity to publish a zero. Deriving a count of decimal places from the
        // magnitude and handing it to `Math.Round` does not: that count is capped at fifteen, which
        // twenty-eight runs a side already exceeds, so 2.6e-16 came back as 0 from a function whose
        // input was not zero and whose own remarks promise it never says certain.
        //
        // `FisherExactTests` guards the test itself to five hundred runs a side. This guards the
        // publication of what it returns, which is the half that was wrong. It stops at
        // twenty-eight because these fixtures deal one run per local day and August has thirty-one
        // of them -- and twenty-eight a side is already four places past what the cap allowed.
        TimeSensitiveEvidence evidence = EvidenceFrom(
            TimeOfDay(eveningFailures: arm, morningFailures: 0, evenings: arm, mornings: arm));

        // Asserted as it is written down rather than as a double. A tolerance here would have to be
        // expressed in decimal places, which is the very thing that cannot describe these
        // magnitudes, and what the defect produced was a published figure rather than an arithmetic
        // one.
        Assert.True(evidence.Significance.PValue > 0);

        var (_, metrics) = EvidenceHeadline.For(FindingKind.TimeSensitive, evidence);
        Assert.Contains(metrics, m => m.Value.StartsWith($"p {expected} ", StringComparison.Ordinal));
    }

    [Fact]
    public void TwoQuartersOfTheDayAreOneComparisonRatherThanTwo()
    {
        // Every run is either an evening or a morning, so "the evening against the rest of the day"
        // and "the morning against the rest of the day" put the same runs on the same two sides.
        // That is one comparison offered twice, and charging two for it would halve the level on the
        // commonest shape a real window takes.
        TimeSensitiveEvidence evidence =
            EvidenceFrom(TimeOfDay(eveningFailures: 6, morningFailures: 0));

        Assert.Equal(1, evidence.Significance.ComparisonsTried);
    }

    [Fact]
    public void AWideSearchIsChargedForEveryDivisionItActuallyMade()
    {
        // Runs spread over three local quarters and both day groups, dividing the window four
        // genuinely different ways rather than the two a clean evening-and-morning window offers.
        //
        // Three of those four still qualify on the gap alone — the failures sit in the evening, so
        // every division that separates the evening from anything shows one — and their p-values are
        // 0.055, 0.055 and 0.114. Read singly the first two are nearly reportable; charged for the
        // four attempts that produced them they are 0.22 and correctly are not.
        TimeSensitiveEvidence evidence = EvidenceFrom(WideSearch());

        Assert.Equal(4, evidence.Significance.ComparisonsTried);

        // The winner is the perfect five-against-fifteen split, which survives the charge easily.
        Assert.Equal("18:00-24:00 local", evidence.Worse.Label);
        Assert.Equal(0.000258, evidence.Significance.PValue);
    }

    [Fact]
    public void ATestWhoseRunsNeverVariedInTimeIsNotAFinding()
    {
        // Failures aplenty, but every run started at the same local hour on a weekday at one offset,
        // so no axis has two sides. This is the common case in a real store.
        List<TestSession> sessions = [];
        for (int i = 0; i < 12; i++)
            sessions.Add(RunAt(i, Local(day: 3 + (i % 5), hour: 20).AddMinutes(i), failed: i < 6));

        Assert.Empty(Analyze(sessions));
    }

    // ---------------------------------------------------------------------------------------
    // Discounting, §6
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AnOutageDoesNotManufactureAGapOutOfTheBinItLandedIn()
    {
        // Every evening run failed, and every evening run also took twelve other tests down with it.
        // Counted naively that is a perfect split; discounted, the evening arm empties entirely.
        List<TestSession> sessions = [];

        for (int i = 0; i < 6; i++)
        {
            List<TestExecution> executions =
            [
                Execution(ordinal: i, failed: true),
            ];

            // Enough collateral failures to clear both environmental bounds: a rate at or above 0.30
            // and at least ten failing tests.
            for (int companion = 0; companion < 12; companion++)
            {
                executions.Add(TestSessionFactory.Execution(
                    $"Companion{companion}", TestOutcome.Failed, errorMessage: "infra"));
            }

            sessions.Add(Session(i, Local(day: 3 + i, hour: 20), executions));
        }

        for (int i = 0; i < 6; i++)
            sessions.Add(RunAt(6 + i, Local(day: 3 + i, hour: 9), failed: false));

        AnalysisContext context = TestSessionFactory.Context([.. sessions]);
        Assert.Equal(6, context.EnvironmentalSessionCount);

        Assert.DoesNotContain(
            new TimeSensitiveProvider().Analyze(context).Candidates,
            c => Named(c) == Subject);
    }

    // ---------------------------------------------------------------------------------------
    // Scoring inputs
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TheFindingIsDatedByTheFailuresThatDroveIt()
    {
        // The newest run in the fixture is an evening one, and evenings are where the failures are.
        FindingCandidate candidate = Single(TimeOfDay(eveningFailures: 6, morningFailures: 0));

        Assert.Equal(TestSessionFactory.SessionIdFor(5), candidate.LastOccurrenceIn.SessionId);
    }

    [Fact]
    public void TimeSensitivityIsReportedAsALeadEvenWhenItScoresHighly()
    {
        // A frequently-run test that fails every evening scores at the top of every term the generic
        // impact formula measures. It is still a correlation found by screening three axes, and it
        // must not sort above a test that simply fails.
        var coordinator = new FindingCoordinator([new TimeSensitiveProvider()]);

        using var warnings = new StringWriter();
        Finding finding = Assert.Single(
            coordinator.Run(Context(TimeOfDay(eveningFailures: 6, morningFailures: 0)), null, warnings)
                .Findings);

        Assert.Equal(Severity.Medium, finding.Severity);
        Assert.True(finding.Impact > LocalAnalysisConstants.SeverityHighThreshold);
    }

    // ---------------------------------------------------------------------------------------
    // What it costs under the null
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(10, 0.30)]
    [InlineData(14, 0.30)]
    [InlineData(20, 0.30)]
    [InlineData(30, 0.30)]
    [InlineData(40, 0.30)]
    [InlineData(20, 0.10)]
    [InlineData(20, 0.50)]
    public void NoTrueTimeEffectIsReportedAsSensitiveMoreThanOnceInTwenty(int sessions, double rate)
    {
        // The issue's own measurement, re-run against this branch. One run per local day, each
        // starting at an hour drawn the way a development store actually looks — mostly inside
        // office hours, occasionally not — with the subject failing independently of when it ran.
        // Every gate the provider ships, end to end, because that product is what a developer sees.
        //
        // Shipped, this reaches 28% at a failure rate of 0.3, which #160 would then multiply by the
        // number of tests in the suite. Here the ceiling is the level the search is charged at and
        // nothing can lift it above that; the delta and distinct-day bars beside it only push the
        // rate further down, and Fisher's conditioning on both margins pushes it down again.
        //
        // Measured at these seven cells: 0.003, 0.009, 0.015, 0.022 and 0.019 as the window grows
        // from ten runs to forty at a failure rate of 0.3, and 0.004 and 0.018 at rates of 0.1 and
        // 0.5. The band below is the claim rather than the measurement — a rate of one in twenty is
        // what #161 asks for and what the level promises — because tightening it to the measured
        // figures would make an unrelated change to an arm gate look like a regression here.
        ulong state = 20260902UL + (ulong)sessions + (ulong)(rate * 100);
        int reported = 0;

        for (int draw = 0; draw < NullDraws; draw++)
        {
            List<TestSession> window = [];

            for (int session = 0; session < sessions; session++)
            {
                // Four fifths of runs between 09:00 and 19:00, the rest anywhere. A uniform hour
                // would spread the runs evenly over the four quarters, which both widens the search
                // and thins every arm — flattering to the correction on one count and to the arm
                // gate on the other.
                int hour = Uniform(ref state) < 0.8
                    ? 9 + (int)(Uniform(ref state) * 10)
                    : (int)(Uniform(ref state) * 24);

                window.Add(RunAt(
                    session, LocalOn(session, hour), failed: Uniform(ref state) < rate));
            }

            if (Analyze(window).Count > 0)
                reported++;
        }

        Assert.InRange((double)reported / NullDraws, 0, 0.05);
    }

    // ---------------------------------------------------------------------------------------
    // Output contract and determinism
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ExemplarsAreCappedAtThreeAndOrderedNewestFirst()
    {
        TimeSensitiveEvidence evidence =
            EvidenceFrom(TimeOfDay(eveningFailures: 6, morningFailures: 0));

        Assert.Equal(3, evidence.Exemplars.Count);

        List<DateTime> dates = [.. evidence.Exemplars.Select(e => e.StartedAt)];
        Assert.Equal(dates.OrderByDescending(d => d), dates);
    }

    [Fact]
    public void TheProviderReachesTheReportEndToEnd()
    {
        var coordinator = new FindingCoordinator([new TimeSensitiveProvider()]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(
            Context(TimeOfDay(eveningFailures: 6, morningFailures: 0)), null, warnings);

        Finding finding = Assert.Single(result.Findings);

        Assert.Equal(FindingKind.TimeSensitive, finding.Kind);
        Assert.Empty(result.FailedProviders);
        Assert.StartsWith("f_", finding.Id, StringComparison.Ordinal);
        Assert.Contains("--kind TimeSensitive", finding.DrillDownCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoRunsOverTheSameWindowProduceByteIdenticalJson()
    {
        AnalysisContext context = Context(TimeOfDay(eveningFailures: 6, morningFailures: 1));
        string report = Serialize(context);

        // Two empty reports are byte-identical too. The window has to still produce a finding for
        // this to be a claim about determinism rather than about silence, and a tightening gate is
        // exactly the change that would quietly empty it.
        Assert.Contains("TimeSensitive", report, StringComparison.Ordinal);

        Assert.Equal(report, Serialize(context));
    }

    [Fact]
    public void TwoIdenticallyBuiltWindowsProduceByteIdenticalJson()
    {
        string first = Serialize(Context(TimeOfDay(eveningFailures: 6, morningFailures: 1)));
        string second = Serialize(Context(TimeOfDay(eveningFailures: 6, morningFailures: 1)));

        Assert.Contains("TimeSensitive", first, StringComparison.Ordinal);
        Assert.Equal(first, second);
    }

    // ---------------------------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Names a local instant in August 2026.
    /// </summary>
    /// <param name="day">Day of the month.</param>
    /// <param name="hour">Hour on the machine's own clock.</param>
    /// <returns>The local instant.</returns>
    private static DateTime Local(int day, int hour) =>
        new(2026, 8, day, hour, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Names a local instant a whole number of days after the 3rd of August 2026.
    /// </summary>
    /// <param name="day">Days after the 3rd.</param>
    /// <param name="hour">Hour on the machine's own clock.</param>
    /// <returns>The local instant.</returns>
    /// <remarks>
    /// What <see cref="Local"/> cannot do. The fixtures above name days of one month directly,
    /// because a temporal test that hides which day it means is a test nobody can check; a window of
    /// forty runs leaves the month and needs arithmetic instead.
    /// </remarks>
    private static DateTime LocalOn(int day, int hour) =>
        new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc).AddDays(day).AddHours(hour);

    /// <summary>
    /// Draws from the unit interval.
    /// </summary>
    /// <param name="state">The generator's state, advanced by the call.</param>
    /// <returns>A value in [0,1).</returns>
    /// <remarks>
    /// splitmix64, hand-written rather than <see cref="Random"/> for the reason the duration and
    /// dispersion suites give: a seeded <see cref="Random"/> sequence is not guaranteed stable
    /// across runtime versions, and a simulation whose answer moves with the runtime is not an
    /// assertion.
    /// </remarks>
    private static double Uniform(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (z >> 11) * (1.0 / 9007199254740992.0);
    }

    /// <summary>
    /// Builds the subject's execution for one session.
    /// </summary>
    /// <param name="ordinal">The session's ordinal, which seeds a distinct execution id.</param>
    /// <param name="failed">Whether the execution failed.</param>
    /// <returns>The execution.</returns>
    /// <remarks>
    /// The id is derived from the ordinal so that exemplars never collide on the id-based tiebreaker,
    /// which is what the concurrency fixtures do and for the same reason.
    /// </remarks>
    private static TestExecution Execution(int ordinal, bool failed) =>
        TestSessionFactory.Execution(
            Subject,
            failed ? TestOutcome.Failed : TestOutcome.Passed,
            errorMessage: failed ? "boom" : null,
            executionId: TestSessionFactory.ExecutionIdFor(Subject, ordinal + 1, TestOutcome.Failed));

    /// <summary>
    /// Builds one session that started at a given local instant.
    /// </summary>
    /// <param name="ordinal">The session's position; only its identity depends on this.</param>
    /// <param name="localStart">When the run started on the machine's own clock.</param>
    /// <param name="executions">What it ran.</param>
    /// <param name="offset">The offset to record, or <see langword="null"/> to record none.</param>
    /// <param name="zone">The zone to record.</param>
    /// <returns>The session.</returns>
    private static TestSession Session(
        int ordinal,
        DateTime localStart,
        IReadOnlyList<TestExecution> executions,
        TimeSpan? offset = null,
        string zone = Zone)
    {
        TimeSpan applied = offset ?? Offset;

        return TestSessionFactory.Session(
            ordinal,
            executions,
            startedAt: localStart - applied,
            utcOffset: offset,
            timeZoneId: zone);
    }

    /// <summary>
    /// Builds one session running only the subject, at a given local instant.
    /// </summary>
    /// <param name="ordinal">The session's position.</param>
    /// <param name="localStart">When the run started on the machine's own clock.</param>
    /// <param name="failed">Whether the subject failed in it.</param>
    /// <param name="offset">The offset to record, or <see langword="null"/> to record none.</param>
    /// <param name="zone">The zone to record.</param>
    /// <returns>The session.</returns>
    private static TestSession RunAt(
        int ordinal,
        DateTime localStart,
        bool failed,
        TimeSpan? offset = null,
        string zone = Zone) =>
        Session(ordinal, localStart, [Execution(ordinal, failed)], offset ?? Offset, zone);

    /// <summary>
    /// Builds a window whose runs alternate between a local evening and a local morning.
    /// </summary>
    /// <param name="eveningFailures">How many of the evening runs failed.</param>
    /// <param name="morningFailures">How many of the morning runs failed.</param>
    /// <param name="evenings">How many evening runs to build.</param>
    /// <param name="mornings">How many morning runs to build.</param>
    /// <param name="recordOffset">
    /// Whether the sessions record an offset at all. The runs happen at the same local times either
    /// way, so a fixture built with this off differs from one built with it on in exactly one
    /// respect: whether the reader can tell when they happened.
    /// </param>
    /// <param name="failureDays">
    /// How many local days to deal the failing evening runs over, or 0 to give each evening run its
    /// own day. Naming a number packs several failures onto one date, which is the only way to move
    /// the distinct-day count without also moving the 2x2 table the significance test reads — a
    /// fixture that varied both could not say which gate it was exercising.
    /// </param>
    /// <returns>The sessions.</returns>
    /// <remarks>
    /// One run per day by default, so the failing side spans as many local dates as it has failures.
    /// That keeps the distinct-day gate clear of every case except the one written to violate it.
    /// </remarks>
    private static List<TestSession> TimeOfDay(
        int eveningFailures,
        int morningFailures,
        int evenings = 6,
        int mornings = 6,
        bool recordOffset = true,
        int failureDays = 0)
    {
        TimeSpan? recorded = recordOffset ? Offset : null;

        List<TestSession> sessions = [];

        for (int i = 0; i < evenings; i++)
        {
            bool failed = i < eveningFailures;

            // Minutes keep the instants distinct where two runs share a date, so the exemplar
            // ordering is still a total one and nothing falls back to the id tie-break.
            DateTime start = failureDays <= 0
                ? Local(3 + i, 20)
                : failed
                    ? Local(3 + (i % failureDays), 20).AddMinutes(i)
                    : Local(3 + failureDays + i - eveningFailures, 20).AddMinutes(i);

            sessions.Add(Session(i, start, [Execution(i, failed)], recorded));
        }

        for (int i = 0; i < mornings; i++)
        {
            int ordinal = evenings + i;
            sessions.Add(
                Session(ordinal, Local(3 + i, 9), [Execution(ordinal, i < morningFailures)], recorded));
        }

        return sessions;
    }

    /// <summary>
    /// Builds a window whose weekend runs all fail and whose weekday runs all pass.
    /// </summary>
    /// <returns>The sessions.</returns>
    /// <remarks>
    /// Every run starts at the same local hour, so the time-of-day axis has nothing to divide and the
    /// day group is the only split with two sides.
    /// </remarks>
    private static List<TestSession> DayGroup()
    {
        int[] weekend = [1, 2, 8, 9, 15, 16];
        int[] weekday = [3, 4, 5, 6, 7, 10];

        List<TestSession> sessions = [];

        for (int i = 0; i < weekend.Length; i++)
            sessions.Add(RunAt(i, Local(weekend[i], 20), failed: true));

        for (int i = 0; i < weekday.Length; i++)
            sessions.Add(RunAt(weekend.Length + i, Local(weekday[i], 20), failed: false));

        return sessions;
    }

    /// <summary>
    /// Builds a window that crosses a change in the machine's UTC offset.
    /// </summary>
    /// <param name="laterZone">
    /// The zone the later sessions record. Defaults to the earlier one, which is what a
    /// daylight-saving transition looks like; naming a different zone reproduces a machine that
    /// moved instead.
    /// </param>
    /// <returns>The sessions.</returns>
    private static List<TestSession> OffsetShift(string laterZone = Zone)
    {
        TimeSpan winter = TimeSpan.FromHours(1);

        List<TestSession> sessions = [];

        for (int i = 0; i < 5; i++)
            sessions.Add(RunAt(i, Local(3 + i, 20), failed: true, winter));

        for (int i = 0; i < 5; i++)
            sessions.Add(RunAt(5 + i, Local(10 + i, 20), failed: false, Offset, laterZone));

        return sessions;
    }

    /// <summary>
    /// Builds a window in which the evening quarter and the weekend both qualify.
    /// </summary>
    /// <returns>The sessions.</returns>
    private static List<TestSession> BothAxes()
    {
        int[] weekendEvenings = [1, 2, 8, 9, 15];
        int[] weekdayEvenings = [3, 4, 5, 6, 7];
        int[] weekdayMornings = [10, 11, 12, 13, 14, 17];

        List<TestSession> sessions = [];
        int ordinal = 0;

        foreach (int day in weekendEvenings)
            sessions.Add(RunAt(ordinal++, Local(day, 20), failed: true));

        // Four of the five weekday evenings fail, which is what pulls the day-group gap below the
        // time-of-day one instead of tying with it.
        for (int i = 0; i < weekdayEvenings.Length; i++)
            sessions.Add(RunAt(ordinal++, Local(weekdayEvenings[i], 20), failed: i < 4));

        foreach (int day in weekdayMornings)
            sessions.Add(RunAt(ordinal++, Local(day, 9), failed: false));

        return sessions;
    }

    /// <summary>
    /// Builds the same evening-against-morning split as <c>TimeOfDay</c>, dealt so the weekend is an
    /// arm of its own.
    /// </summary>
    /// <remarks>
    /// Ten evenings against ten clean mornings — at five failures, the identical 2x2 table to
    /// <c>TimeOfDay(5, 0, evenings: 10, mornings: 10)</c> and the identical p-value. Only the dates
    /// differ: enough runs fall at a weekend for the day group to clear the arm gate, so the window
    /// divides two ways rather than one and is charged twice for the same observation.
    /// </remarks>
    /// <param name="eveningFailures">How many of the ten evening runs failed.</param>
    /// <returns>The sessions.</returns>
    private static List<TestSession> WeekendEvenings(int eveningFailures)
    {
        // Six weekend days in August 2026 and four weekdays, so the weekend holds ten of the twenty
        // runs. The five red evenings are spread over five separate dates, as the day gate needs.
        int[] eveningDays = [1, 2, 8, 9, 15, 3, 4, 5, 6, 7];
        int[] morningDays = [16, 22, 23, 29, 30, 10, 11, 12, 13, 14];

        List<TestSession> sessions = [];
        int ordinal = 0;

        for (int i = 0; i < eveningDays.Length; i++)
            sessions.Add(RunAt(ordinal++, Local(eveningDays[i], 20), failed: i < eveningFailures));

        foreach (int day in morningDays)
            sessions.Add(RunAt(ordinal++, Local(day, 9), failed: false));

        return sessions;
    }

    /// <summary>
    /// Builds a window that divides four different ways rather than the usual one or two.
    /// </summary>
    /// <returns>The sessions.</returns>
    /// <remarks>
    /// Twenty runs over three local quarters, arranged so that the weekend is not any one of them:
    /// five evenings, eight mornings and seven afternoons, with the weekend spread across the last
    /// two. Every quarter has five runs and fifteen elsewhere, so all three divisions clear the arm
    /// gate, and none of them is the day group's. Only the evenings fail, and they fail on five
    /// separate days.
    /// </remarks>
    private static List<TestSession> WideSearch()
    {
        (int Day, int Hour, bool Failed)[] runs =
        [
            // Five weekday evenings, all red.
            (3, 20, true), (4, 20, true), (5, 20, true), (6, 20, true), (7, 20, true),

            // Eight mornings, five of them at the weekend.
            (1, 9, false), (2, 9, false), (8, 9, false), (9, 9, false), (15, 9, false),
            (10, 9, false), (11, 9, false), (12, 9, false),

            // Seven afternoons, three of them at the weekend.
            (16, 14, false), (22, 14, false), (23, 14, false),
            (17, 14, false), (18, 14, false), (19, 14, false), (20, 14, false)
        ];

        List<TestSession> sessions = [];

        for (int i = 0; i < runs.Length; i++)
            sessions.Add(RunAt(i, Local(runs[i].Day, runs[i].Hour), runs[i].Failed));

        return sessions;
    }

    /// <summary>
    /// Builds a window whose every failure falls inside one local evening.
    /// </summary>
    /// <returns>The sessions.</returns>
    private static List<TestSession> SingleEvening()
    {
        List<TestSession> sessions = [];

        for (int i = 0; i < 6; i++)
            sessions.Add(RunAt(i, Local(3, 20).AddMinutes(i), failed: true));

        for (int i = 0; i < 6; i++)
            sessions.Add(RunAt(6 + i, Local(3, 9).AddMinutes(i), failed: false));

        return sessions;
    }

    /// <summary>
    /// Builds a window whose evening runs all fail after exhausting their retries, and whose six
    /// morning runs all pass first time.
    /// </summary>
    /// <param name="eveningSessions">How many evening runs to build, one per local day.</param>
    /// <param name="attempts">Attempts each evening run takes before giving up.</param>
    /// <returns>The sessions.</returns>
    /// <remarks>
    /// The evening arm holds <c>eveningSessions * attempts</c> executions but only
    /// <c>eveningSessions</c> occasions, which is the whole point of the fixture: the two numbers
    /// come apart and the gate has to be applied to the second.
    /// </remarks>
    private static List<TestSession> RetriedEvenings(int eveningSessions, int attempts)
    {
        List<TestSession> sessions = [];

        for (int i = 0; i < eveningSessions; i++)
        {
            List<TestExecution> executions = [];

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                executions.Add(TestSessionFactory.Execution(
                    Subject,
                    TestOutcome.Failed,
                    attempt: attempt,
                    maxRetries: attempts - 1,
                    errorMessage: "boom",
                    executionId: TestSessionFactory.ExecutionIdFor(
                        Subject, (i * attempts) + attempt, TestOutcome.Failed)));
            }

            sessions.Add(Session(i, Local(3 + i, 20), executions, Offset));
        }

        for (int i = 0; i < 6; i++)
        {
            int ordinal = eveningSessions + i;
            sessions.Add(Session(ordinal, Local(3 + i, 9), [Execution(ordinal, failed: false)], Offset));
        }

        return sessions;
    }

    private static AnalysisContext Context(List<TestSession> sessions) =>
        TestSessionFactory.Context([.. sessions]);

    /// <summary>
    /// Builds a window in which the time-of-day axis shows a wider gap than the day group over far
    /// fewer runs, so the two axes disagree about which split the evidence supports.
    /// </summary>
    /// <remarks>
    /// The disagreement is three-way on purpose, and it is what makes the fixture worth having: the
    /// time-of-day axis has the wider observed gap (0.80 against 0.55) <b>and</b> the larger
    /// supported one (0.33 against 0.26), while the day group is the less probable division
    /// (0.00055 against 0.0014). A fixture in which the three agreed could not tell which of them
    /// the provider selects on.
    /// </remarks>
    private static List<TestSession> WiderButThinner()
    {
        int[] weekendDays = [1, 2, 8, 9, 15, 16, 22, 23, 29, 30];
        int[] weekdayDays = [3, 4, 5, 6, 7, 10, 11, 12, 13, 14, 17, 18, 19, 20, 21];

        List<TestSession> sessions = [];
        int ordinal = 0;

        // Fifteen weekend mornings, every one of them red.
        for (int i = 0; i < 15; i++)
            sessions.Add(RunAt(ordinal++, Local(weekendDays[i % weekendDays.Length], 9), failed: true));

        // Fifteen weekday mornings, nine of them red.
        for (int i = 0; i < 15; i++)
            sessions.Add(RunAt(ordinal++, Local(weekdayDays[i], 9), failed: i < 9));

        // Five weekday evenings, all green.
        for (int i = 0; i < 5; i++)
            sessions.Add(RunAt(ordinal++, Local(weekdayDays[i], 20), failed: false));

        return sessions;
    }

    private static IReadOnlyList<FindingCandidate> Analyze(List<TestSession> sessions) =>
        new TimeSensitiveProvider().Analyze(Context(sessions)).Candidates;

    private static FindingCandidate Single(List<TestSession> sessions) =>
        Assert.Single(Analyze(sessions));

    private static TimeSensitiveEvidence EvidenceFrom(List<TestSession> sessions) =>
        Assert.IsType<TimeSensitiveEvidence>(Single(sessions).Evidence);

    private static string Named(FindingCandidate candidate) =>
        Assert.IsType<FindingSubject.SingleTest>(candidate.Subject).Test.DisplayName;

    /// <summary>
    /// Renders the whole report the way the command would.
    /// </summary>
    /// <param name="context">The window to analyse.</param>
    /// <returns>The report JSON.</returns>
    /// <remarks>
    /// Determinism is asserted on the JSON rather than on the candidates, because the requirement in
    /// §10 is about the bytes: the evidence records hold lists, which records compare by reference,
    /// so two identical analyses would compare unequal while serialising the same.
    /// </remarks>
    private static string Serialize(AnalysisContext context)
    {
        using var warnings = new StringWriter();

        AnalysisResult result =
            new FindingCoordinator([new TimeSensitiveProvider()]).Run(context, null, warnings);

        ReportEnvelope envelope = EnvelopeBuilder.Build(
            context, result, incompleteSessions: 0, unreadableSessions: 0, top: null);

        return JsonSerializer.Serialize(envelope, ReportJsonOptions.Default);
    }
}
