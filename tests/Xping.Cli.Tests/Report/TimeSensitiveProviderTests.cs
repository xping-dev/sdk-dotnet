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
    public void TheEvidenceCarriesBothArmsWithTheirDenominators()
    {
        TimeSensitiveEvidence evidence =
            EvidenceFrom(TimeOfDay(eveningFailures: 5, morningFailures: 0));

        Assert.Equal(5, evidence.Worse.Failures);
        Assert.Equal(6, evidence.Worse.Executions);
        Assert.Equal(6, evidence.Worse.Sessions);
        Assert.Equal(0.833, evidence.Worse.FailureRate);

        Assert.Equal(0, evidence.Other.Failures);
        Assert.Equal(6, evidence.Other.Executions);
        Assert.Equal(6, evidence.Other.Sessions);
        Assert.Equal(0, evidence.Other.FailureRate);
    }

    [Fact]
    public void TheEvidenceCountsTheDaysTheFailuresFellOnRatherThanTheArmsSpan()
    {
        // The evening arm runs on six days; only three of them failed. The published figure is the
        // one the three-day gate was applied to, so a reader can check the gate rather than infer it.
        TimeSensitiveEvidence evidence =
            EvidenceFrom(TimeOfDay(eveningFailures: 3, morningFailures: 0));

        Assert.Equal(6, evidence.Worse.Executions);
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
        // six executions a side support down to 0.28. The condition still thresholds the observed
        // gap, so what is emitted has not changed; what changed is where it ranks against findings
        // measured over a whole window.
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
    public void WhenTwoAxesQualifyOnlyTheWiderGapIsReported()
    {
        IReadOnlyList<FindingCandidate> candidates = Analyze(BothAxes());

        // Weekend against weekday is a gap of 0.64; evening against the rest of the day is 0.90.
        FindingCandidate candidate = Assert.Single(candidates);
        var evidence = Assert.IsType<TimeSensitiveEvidence>(candidate.Evidence);

        // The axis is still chosen on the observed gaps; the term the report ranks on is the gap
        // the winning split's arms support.
        Assert.Equal("LocalTimeOfDay", evidence.Axis);
        Assert.Equal(0.9, evidence.Delta.FailureRate);
        Assert.Equal(0.405, candidate.Unreliability, 3);
    }

    [Fact]
    public void TheBetterEvidencedAxisWinsEvenWhenAnotherShowsAWiderGap()
    {
        // Five evening runs of which one failed, against thirty mornings split into fifteen weekend
        // runs that all failed and fifteen weekday runs of which nine did. The time-of-day axis
        // shows the wider gap - 0.60 against the day group's 0.50 - and five executions support
        // almost none of it: 0.14, against 0.21 for the split measured over fifteen a side.
        //
        // Choosing on the observed gap published the thin axis and then ranked the finding on the
        // thin axis's bound, throwing away the better evidenced split entirely. It also published
        // "the rest of the day" as the failing side, which is exactly the label the tie-break above
        // exists to avoid.
        FindingCandidate candidate = Single(WiderButThinner());
        var evidence = Assert.IsType<TimeSensitiveEvidence>(candidate.Evidence);

        Assert.Equal("LocalDayGroup", evidence.Axis);
        Assert.Equal("weekend", evidence.Worse.Label);
        Assert.Equal(0.5, evidence.Delta.FailureRate);
        Assert.Equal(0.214, candidate.Unreliability, 3);
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
        // Two of six evenings against none of six mornings is a gap of 0.33, over the threshold, on
        // arms of six. It fails only on the day count — and one day more, below, qualifies.
        Assert.Empty(Analyze(TimeOfDay(eveningFailures: 2, morningFailures: 0)));

        Assert.Equal(
            FindingKind.TimeSensitive,
            Single(TimeOfDay(eveningFailures: 3, morningFailures: 0)).Kind);
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
            new TimeSensitiveProvider().Analyze(context),
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

        Assert.Equal(0, candidate.SessionsSinceLastOccurrence);
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

        Assert.Equal(Serialize(context), Serialize(context));
    }

    [Fact]
    public void TwoIdenticallyBuiltWindowsProduceByteIdenticalJson()
    {
        string first = Serialize(Context(TimeOfDay(eveningFailures: 6, morningFailures: 1)));
        string second = Serialize(Context(TimeOfDay(eveningFailures: 6, morningFailures: 1)));

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
    /// <returns>The sessions.</returns>
    /// <remarks>
    /// One run per day, so the failing side spans as many local dates as it has failures. That keeps
    /// the distinct-day gate clear of every case except the one written to violate it.
    /// </remarks>
    private static List<TestSession> TimeOfDay(
        int eveningFailures,
        int morningFailures,
        int evenings = 6,
        int mornings = 6,
        bool recordOffset = true)
    {
        TimeSpan? recorded = recordOffset ? Offset : null;

        List<TestSession> sessions = [];

        for (int i = 0; i < evenings; i++)
            sessions.Add(Session(i, Local(3 + i, 20), [Execution(i, i < eveningFailures)], recorded));

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

    private static AnalysisContext Context(List<TestSession> sessions) =>
        TestSessionFactory.Context([.. sessions]);

    /// <summary>
    /// Builds a window in which the time-of-day axis shows a wider gap than the day group over far
    /// fewer executions, so the two axes disagree about which split the evidence supports.
    /// </summary>
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

        // Five weekday evenings, one of them red.
        for (int i = 0; i < 5; i++)
            sessions.Add(RunAt(ordinal++, Local(weekdayDays[i], 20), failed: i == 0));

        return sessions;
    }

    private static IReadOnlyList<FindingCandidate> Analyze(List<TestSession> sessions) =>
        [.. new TimeSensitiveProvider().Analyze(Context(sessions))];

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
