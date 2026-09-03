/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Scoring;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Report.Providers;

/// <summary>
/// The retry configuration as the attribute declared it, never as analysis interpreted it.
/// </summary>
/// <remarks>
/// Shared by all three retry kinds so that one mechanism is described one way. Every field here is
/// transcribed, and none of them decides anything: the conditions are decided on attempts observed.
/// </remarks>
/// <param name="AttributeName">
/// The retry mechanism in use, as the SDK named it, or <see langword="null"/> when the adapter
/// recorded no name for it. Never an empty string: an adapter that could not name the mechanism is
/// normalised to <see langword="null"/> on the way in, so a reader has one absent value to check
/// rather than two.
/// </param>
/// <param name="MaxRetriesAsDeclared">
/// The limit the retry attribute declared, verbatim — named so the ambiguity cannot be read away.
/// Adapters record it exactly as the attribute spells it and the attributes disagree about what it
/// counts: NUnit writes NUnit's <c>TryCount</c>, which is total attempts, while an xUnit retry
/// library writes whatever its own library calls its limit. Published as context so a reader can set
/// it beside the attempts actually observed; never compared against them by this report.
/// </param>
/// <param name="Reason">
/// The reason the attribute declared for retrying, verbatim, or <see langword="null"/> when it
/// declared none. Empty is absent: the MSTest adapter writes an empty string where xUnit and NUnit
/// leave null, and a blank reason is not a reason.
/// </param>
/// <param name="ConfiguredDelayMs">
/// The delay the attribute declared between attempts. Published so that configured waiting is never
/// read as time the test spent running. Not recorded at all by the NUnit adapter.
/// </param>
internal sealed record RetryConfiguration(
    string? AttributeName,
    int MaxRetriesAsDeclared,
    string? Reason,
    long ConfiguredDelayMs);

/// <summary>
/// One run, reduced to what its attempts cost and how it ended.
/// </summary>
/// <remarks>
/// <see cref="StartedAt"/> is the start of the run, not of any attempt in it, for the reason the
/// duration provider gives: the per-execution timestamp is reused across retry attempts on the xUnit
/// adapter, while the duration is per-attempt everywhere.
/// </remarks>
/// <param name="SessionId">The run it happened in.</param>
/// <param name="StartedAt">When that run started.</param>
/// <param name="Sha">The commit it ran at, or <see langword="null"/> when none was recorded.</param>
/// <param name="Attempts">Attempts the run recorded for this test.</param>
/// <param name="Outcome">How the deciding attempt ended.</param>
/// <param name="RetryWallClockMs">Measured time spent on attempts after the first.</param>
/// <param name="ErrorMessage">
/// What the last attempt before the deciding one said, elided to the budget. Null when the run
/// needed only one attempt, or when the adapter recorded no message.
/// </param>
internal sealed record RetryAttemptExemplar(
    string SessionId,
    DateTime StartedAt,
    string? Sha,
    int Attempts,
    string Outcome,
    long RetryWallClockMs,
    string? ErrorMessage);

/// <summary>
/// One occasion on which a failure was hidden by a retry.
/// </summary>
/// <remarks>
/// Carries both halves of the event: the attempt that passed, and what the attempt before it failed
/// with. A reader looking at a green build has no other way to see that either happened.
/// </remarks>
/// <param name="SessionId">The run the masking happened in.</param>
/// <param name="StartedAt">When that run started.</param>
/// <param name="Sha">The commit it ran at, or <see langword="null"/> when none was recorded.</param>
/// <param name="AttemptNumber">The attempt that finally passed.</param>
/// <param name="FailedAttempts">Attempts of this test that preceded it in the same run.</param>
/// <param name="DurationMs">How long the passing attempt took.</param>
/// <param name="ErrorMessage">What the last preceding failure said, elided to the budget.</param>
internal sealed record RetryMaskedExemplar(
    string SessionId,
    DateTime StartedAt,
    string? Sha,
    int AttemptNumber,
    int FailedAttempts,
    long DurationMs,
    string? ErrorMessage);

/// <summary>
/// Evidence that a test failed and passed on retry without ever failing a build.
/// </summary>
/// <param name="MaskedOccurrences">Executions that passed on an attempt after the first.</param>
/// <param name="Executions">Executions of the test across the whole window.</param>
/// <param name="Sessions">Sessions in the window.</param>
/// <param name="SessionsWithMasking">Sessions in which masking happened.</param>
/// <param name="MaskedRate">
/// <paramref name="MaskedOccurrences"/> over <paramref name="Executions"/>, at published precision.
/// </param>
/// <param name="MaxAttemptObserved">The highest attempt number a masked pass arrived on.</param>
/// <param name="Configuration">The retry configuration, as the attribute declared it.</param>
/// <param name="RetryWallClockMs">Measured wall-clock spent on attempts after the first.</param>
/// <param name="ConfiguredDelayTotalMs">
/// Declared waiting across the same attempts: the configured delay multiplied by the attempts after
/// the first. A declared figure scaled by an observed count, not a measurement — the report cannot
/// see whether the framework actually waited, and adding it to the wall clock would claim it could.
/// </param>
/// <param name="LastMaskedAt">When masking last happened.</param>
/// <param name="LastMaskedSha">The commit it last happened at, when known.</param>
/// <param name="Exemplars">Up to three occurrences, newest first.</param>
internal sealed record RetryMaskedEvidence(
    int MaskedOccurrences,
    int Executions,
    int Sessions,
    int SessionsWithMasking,
    double MaskedRate,
    int MaxAttemptObserved,
    RetryConfiguration Configuration,
    long RetryWallClockMs,
    long ConfiguredDelayTotalMs,
    DateTime LastMaskedAt,
    string? LastMaskedSha,
    IReadOnlyList<RetryMaskedExemplar> Exemplars) : FindingEvidence;

/// <summary>
/// One side of the deepening comparison, always carrying the runs it was computed from.
/// </summary>
/// <param name="TypicalAttempts">
/// Attempts a typical passing run needed, by nearest rank — a whole attempt some run actually took.
/// </param>
/// <param name="MaxAttempts">The deepest passing run in this arm.</param>
/// <param name="RunsSettledGreen">Runs the typical figure was computed over.</param>
/// <param name="RunsFailedFinally">
/// Runs of this test in this arm that ended red. Left out of the typical figure, because "attempts
/// to pass" is undefined for a run that never passed — and published, because a reader shown only
/// that figure would otherwise take every run in the arm for a green one.
/// </param>
/// <param name="Runs">
/// Runs of this test in this arm. Exceeds the two counts above by the runs whose deciding attempt
/// neither passed nor failed, which is what a skipped test looks like.
/// </param>
internal sealed record RetryDepthProfile(
    int TypicalAttempts,
    int MaxAttempts,
    int RunsSettledGreen,
    int RunsFailedFinally,
    int Runs);

/// <summary>
/// The change in attempts, signed so the direction is read rather than derived.
/// </summary>
/// <param name="Attempts">Recent typical attempts minus earlier typical attempts.</param>
/// <param name="AttemptsPct">The same change relative to the earlier figure, as a percentage.</param>
internal sealed record RetryDepthDelta(int Attempts, double AttemptsPct);

/// <summary>
/// Evidence that a test now needs more attempts to pass than it used to.
/// </summary>
/// <param name="Current">The recent runs.</param>
/// <param name="Baseline">The runs before them.</param>
/// <param name="Delta">The change, which the threshold was applied to.</param>
/// <param name="Configuration">The retry configuration, as the attribute declared it.</param>
/// <param name="RetryWallClockMs">
/// Measured time the recent passing runs spent on attempts after the first.
/// </param>
/// <param name="ConfiguredDelayTotalMs">Declared waiting across the same attempts.</param>
/// <param name="DiscountedRuns">Runs left out of both arms because the environment looked broken.</param>
/// <param name="FirstSeenAt">
/// The commit of the oldest recent run this test passed in — where the change crosses from the
/// baseline into "now". Null when that run recorded no commit, never fabricated.
/// </param>
/// <param name="Exemplars">Up to three recent passing runs, newest first.</param>
/// <param name="Contrast">One earlier run typical of what a pass used to cost.</param>
internal sealed record RetryDeepeningEvidence(
    RetryDepthProfile Current,
    RetryDepthProfile Baseline,
    RetryDepthDelta Delta,
    RetryConfiguration Configuration,
    long RetryWallClockMs,
    long ConfiguredDelayTotalMs,
    int DiscountedRuns,
    string? FirstSeenAt,
    IReadOnlyList<RetryAttemptExemplar> Exemplars,
    RetryAttemptExemplar? Contrast) : FindingEvidence;

/// <summary>
/// Evidence that a test's retries ran out and it failed anyway.
/// </summary>
/// <remarks>
/// Every count here is of runs rather than of executions. Exhaustion is a property of a run, and an
/// exhausted run contributes three or four executions — an execution-denominated rate would be
/// deflated by the very behaviour it is measuring, reading "4 of 20 runs" out as "4 of 56".
/// </remarks>
/// <param name="ExhaustedRuns">
/// Runs whose highest recorded attempt failed with an earlier attempt behind it.
/// </param>
/// <param name="RetriedRuns">Runs that recorded more than one attempt.</param>
/// <param name="RescuedRuns">
/// Retried runs that settled green — the occasions the retry attribute earned its keep.
/// </param>
/// <param name="RunsConsidered">Runs of this test, after environmental runs were set aside.</param>
/// <param name="Sessions">Sessions in the window.</param>
/// <param name="ExhaustedRate">
/// <paramref name="ExhaustedRuns"/> over <paramref name="RetriedRuns"/>, at published precision —
/// the share of the occasions retries were spent on which they did not help. This is the figure the
/// threshold was applied to; it is not how unreliable the test is, which is measured against every
/// run of it and is what the finding is ranked by.
/// </param>
/// <param name="MaxAttemptObserved">
/// The deepest attempt an exhausted run reached before giving up. Set beside
/// <see cref="RetryConfiguration.MaxRetriesAsDeclared"/> deliberately: the pair is the reading, and
/// the report publishes both rather than deriving either from the other.
/// </param>
/// <param name="RetryAttemptsSpent">
/// Attempts after the first, across the exhausted runs — the work the retry budget bought and did
/// not deliver.
/// </param>
/// <param name="Configuration">The retry configuration, as the attribute declared it.</param>
/// <param name="RetryWallClockMs">
/// Measured time the exhausted runs spent on attempts after the first.
/// </param>
/// <param name="ConfiguredDelayTotalMs">Declared waiting across the same attempts.</param>
/// <param name="DiscountedRuns">Runs left out because the environment looked broken.</param>
/// <param name="LastExhaustedAt">When the retries last ran out.</param>
/// <param name="LastExhaustedSha">The commit that last happened at, when known.</param>
/// <param name="Exemplars">Up to three exhausted runs, newest first.</param>
/// <param name="Contrast">
/// One retried run of the same test that did settle green, or <see langword="null"/> when none did.
/// The pair is what separates a test whose retries occasionally fail to save it from one they never
/// save.
/// </param>
internal sealed record RetryExhaustedEvidence(
    int ExhaustedRuns,
    int RetriedRuns,
    int RescuedRuns,
    int RunsConsidered,
    int Sessions,
    double ExhaustedRate,
    int MaxAttemptObserved,
    int RetryAttemptsSpent,
    RetryConfiguration Configuration,
    long RetryWallClockMs,
    long ConfiguredDelayTotalMs,
    int DiscountedRuns,
    DateTime LastExhaustedAt,
    string? LastExhaustedSha,
    IReadOnlyList<RetryAttemptExemplar> Exemplars,
    RetryAttemptExemplar? Contrast) : FindingEvidence;

/// <summary>
/// Reports what a suite's retries are costing it: failures they hide, failures they no longer
/// prevent, and tests that need more attempts than they used to.
/// </summary>
/// <remarks>
/// <para>
/// One provider owns all three kinds because they are one judgement about one mechanism, and a test
/// gets at most one of them. A test that has deepened is <i>necessarily</i> masked — its recent runs
/// pass on an attempt above the first — so reporting both would state one observation twice under
/// two names. Splitting them across providers would let each claim the same test, and providers may
/// not consult each other.
/// </para>
/// <para>
/// The order is out of retries, then deeper retries, then masked by retry: red beats worsening beats
/// standing. Exhaustion sits first because it is the only one of the three describing a build that
/// actually broke, and masking last because it is the weakest claim the three can make.
/// </para>
/// <para>
/// <b>Exhaustion is observed, never inferred from the configured limit.</b> A run is exhausted for a
/// test when the highest-numbered attempt it recorded for that test failed and at least one earlier
/// attempt exists in the same run. <c>MaxRetries</c> is recorded verbatim by every adapter and the
/// attributes disagree about what it counts — NUnit writes NUnit's <c>TryCount</c>, which is total
/// attempts, while an xUnit library writes whatever its own limit is called — so comparing an
/// attempt number against it would report one framework's test as out of retries and another's,
/// behaving identically, as fine. It is published as context under a name that says whose number it
/// is, and nothing here is decided by it.
/// </para>
/// <para>
/// Every kind here needs an adapter that records one execution per attempt, and all three now do,
/// within the limits set out in known-limitations.md. NUnit records each attempt with the number
/// NUnit itself reports. xUnit records each attempt for retry libraries exposing a single-attempt
/// hook — xRetry does — and one execution for the whole retry loop for libraries that do not.
/// MSTest records each attempt and derives the number by counting the executions already recorded
/// for the same fingerprint, so two identical <c>[DataRow]</c> values repeated inside one run are
/// indistinguishable from a retry. Where attempts are not recorded, silence here is
/// indistinguishable from "no retries happened", and that remains the honest result.
/// </para>
/// </remarks>
internal sealed class RetryProvider : IFindingProvider
{
    // Three, per the output contract's exemplar budget. A per-provider constant rather than a shared
    // threshold: the specification's constant table does not list it, and adding an entry there
    // would be a threshold this session invented.
    private const int MaxExemplars = 3;

    // Runs the test settled green in, either side of the deepening split, before its typical attempt
    // count is compared. Private for the same reason as the exemplar budget above, and counted in
    // runs rather than executions: a retried run contributes several executions of one attempt
    // sequence and would clear an execution floor on its own, which is the opposite of what a floor
    // is for.
    //
    // Five earlier runs, because below that the typical figure describes two or three runs and one
    // unlucky retry moves it by a whole attempt — the bar the duration provider sets on a baseline.
    // Two recent runs, because the current slice is three sessions and a test need not run in all of
    // them; demanding all three would silence the finding for anything running under a filter, while
    // one run is a coin toss. Two is also self-guarding under a nearest-rank median: from two runs,
    // [1, 3] reads as 1, so a single deep run cannot produce this finding on its own.
    private const int MinimumBaselineSettledRuns = 5;
    private const int MinimumCurrentSettledRuns = 2;

    /// <inheritdoc/>
    public string Name => "retry";

    /// <inheritdoc/>
    public IReadOnlyList<FindingKind> Kinds =>
        [FindingKind.RetryMasked, FindingKind.RetryDeepening, FindingKind.RetryExhausted];

    /// <inheritdoc/>
    /// <remarks>
    /// Every kind here is a count of attempts that happened, so no family is reported and nothing
    /// downstream corrects them for multiplicity.
    /// </remarks>
    public ProviderReport Analyze(AnalysisContext context) =>
        ProviderReport.Observations([.. Observed(context)]);

    /// <summary>
    /// Walks the window, yielding what it observed.
    /// </summary>
    /// <param name="context">The window, sessions and shared indexes.</param>
    /// <returns>Candidate findings, in any order.</returns>
    private static IEnumerable<FindingCandidate> Observed(AnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var currentSessions = new HashSet<Guid>(
            context.Window.CurrentSlice.Select(s => s.SessionId));

        foreach (string fingerprint in context.Tests.Fingerprints)
        {
            TestReference? test = context.Tests.ReferenceFor(fingerprint);
            if (test == null)
                continue;

            IReadOnlyList<ExecutionRef> executions = context.Tests.ExecutionsOf(fingerprint);
            List<RunAttempts> runs = RunsOf(context, executions);

            // One test, one finding. A test qualifying for two of these kinds has not done two
            // things: it has done one thing that two thresholds both noticed.
            FindingCandidate? candidate =
                Exhausted(context, test, runs) ??
                Deepening(context, test, runs, currentSessions) ??
                Masked(context, test, executions, runs);

            if (candidate != null)
                yield return candidate;
        }
    }

    // -------------------------------------------------------------------------------------------
    // The shared run reduction
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// One run of one test, reduced the way the shared index reduces it.
    /// </summary>
    /// <remarks>
    /// Every question the three kinds ask is a question about a run: how many attempts it needed and
    /// how it ended. Reducing once, here, is what keeps them from answering it three different ways —
    /// and from disagreeing with <see cref="SessionOutcomes"/>, which is how a report ends up calling
    /// a run green while flagging a test inside it as having blocked the build.
    /// </remarks>
    /// <param name="SessionId">The run.</param>
    /// <param name="SessionIndex">Its position in the window; 0 is the newest.</param>
    /// <param name="Attempts">The highest attempt number recorded in the run.</param>
    /// <param name="EarlierAttempts">Executions of the run below that number.</param>
    /// <param name="FailedFinally">Whether the deciding attempt failed.</param>
    /// <param name="Passed">
    /// Whether the deciding attempt passed. Strictly narrower than the negation of
    /// <paramref name="FailedFinally"/>: a run whose deciding attempt was skipped passed nothing,
    /// and "attempts to pass" is undefined for it.
    /// </param>
    /// <param name="Final">The execution that decided the run.</param>
    /// <param name="LastBefore">The last attempt before it — the message nobody saw.</param>
    /// <param name="RetryWallClockMs">Attempts after the first, summed.</param>
    /// <param name="RetriedExecutions">Attempts after the first, counted.</param>
    /// <param name="Discounted">Whether the run looked like an outage rather than a test failure.</param>
    private sealed record RunAttempts(
        Guid SessionId,
        int SessionIndex,
        int Attempts,
        int EarlierAttempts,
        bool FailedFinally,
        bool Passed,
        ExecutionRef Final,
        ExecutionRef? LastBefore,
        long RetryWallClockMs,
        int RetriedExecutions,
        bool Discounted)
    {
        /// <summary>Gets whether the run recorded more than one attempt.</summary>
        public bool Retried => EarlierAttempts > 0;

        /// <summary>
        /// Gets whether the run spent its retries and still went red.
        /// </summary>
        /// <remarks>
        /// Observational, and deliberately says nothing about <c>MaxRetries</c>. See the class
        /// remarks for why that field cannot be part of this condition.
        /// </remarks>
        public bool Exhausted => FailedFinally && EarlierAttempts > 0;
    }

    /// <summary>
    /// Reduces a test's executions to one record per run, newest run first.
    /// </summary>
    private static List<RunAttempts> RunsOf(
        AnalysisContext context, IReadOnlyList<ExecutionRef> executions)
    {
        // Executions arrive newest-session-first, so first-seen order is newest-run-first and the
        // resulting list needs no sort. One entry per session makes its order total on SessionIndex.
        var bySession = new Dictionary<Guid, List<ExecutionRef>>();
        var order = new List<Guid>();

        foreach (ExecutionRef reference in executions)
        {
            if (!bySession.TryGetValue(reference.Session.SessionId, out List<ExecutionRef>? run))
            {
                run = [];
                bySession[reference.Session.SessionId] = run;
                order.Add(reference.Session.SessionId);
            }

            run.Add(reference);
        }

        var runs = new List<RunAttempts>(order.Count);
        foreach (Guid sessionId in order)
            runs.Add(Reduce(context, bySession[sessionId]));

        return runs;
    }

    private static RunAttempts Reduce(AnalysisContext context, List<ExecutionRef> run)
    {
        // `>=` over the stored order, exactly as SessionOutcomes decides it: the last execution
        // recorded at the highest attempt is the one that settled the run. A `>` here would pick the
        // first execution at the top attempt instead, and the two would disagree about whether a run
        // ended red.
        ExecutionRef final = run[0];
        int attempts = AttemptOf(run[0]);

        foreach (ExecutionRef reference in run)
        {
            int attempt = AttemptOf(reference);
            if (attempt >= attempts)
            {
                attempts = attempt;
                final = reference;
            }
        }

        int earlier = 0;
        int retried = 0;
        long retryWallClockMs = 0;
        ExecutionRef? lastBefore = null;

        foreach (ExecutionRef reference in run)
        {
            int attempt = AttemptOf(reference);

            if (attempt < attempts)
            {
                earlier++;

                if (lastBefore == null || attempt >= AttemptOf(lastBefore))
                    lastBefore = reference;
            }

            if (attempt > 1)
            {
                retried++;
                retryWallClockMs += (long)reference.Execution.Duration.TotalMilliseconds;
            }
        }

        return new RunAttempts(
            final.Session.SessionId,
            final.SessionIndex,
            attempts,
            earlier,
            final.Execution.Outcome.IsFailure(),
            final.Execution.Outcome == TestOutcome.Passed,
            final,
            lastBefore,
            retryWallClockMs,
            retried,
            context.SessionViewFor(final.Session.SessionId)?.IsLikelyEnvironmental == true);
    }

    // -------------------------------------------------------------------------------------------
    // Out of retries
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Attempts the exhaustion finding, returning <see langword="null"/> when a gate declines it.
    /// </summary>
    /// <remarks>
    /// Measured over the whole window rather than either slice. Whether retries rescue a test is a
    /// standing property of the pair, not a change between two halves of its history, and splitting
    /// the window would only halve the evidence behind it.
    /// </remarks>
    private static FindingCandidate? Exhausted(
        AnalysisContext context, TestReference test, List<RunAttempts> runs)
    {
        List<RunAttempts> considered = [];
        int discounted = 0;

        foreach (RunAttempts run in runs)
        {
            if (run.Discounted)
                discounted++;
            else
                considered.Add(run);
        }

        if (considered.Count == 0)
            return null;

        List<RunAttempts> retried = [.. considered.Where(r => r.Retried)];
        List<RunAttempts> exhausted = [.. retried.Where(r => r.Exhausted)];

        if (exhausted.Count < LocalAnalysisConstants.RetryExhaustedMinRuns)
            return null;

        // Thresholded on the lower bound of the share rather than on the share itself. The claim is
        // that retries are not rescuing this test, which is a statement about the mechanism, and two
        // retried runs that both ran out is 1.00 of a denominator that cannot support it. The
        // published rate below stays the point estimate: a reader wants "7 of 8", not "0.88".
        double exhaustedShare = (double)exhausted.Count / retried.Count;
        if (WilsonInterval.LowerBound(exhausted.Count, retried.Count) <
            LocalAnalysisConstants.RetryExhaustedShareMin)
        {
            return null;
        }

        RunAttempts newest = exhausted[0];

        long retryWallClockMs = 0;
        int attemptsSpent = 0;
        foreach (RunAttempts run in exhausted)
        {
            retryWallClockMs += run.RetryWallClockMs;
            attemptsSpent += run.RetriedExecutions;
        }

        RetryConfiguration configuration = ConfigurationOf(newest);

        return new FindingCandidate(
            FindingKind.RetryExhausted,
            new FindingSubject.SingleTest(test),
            new RetryExhaustedEvidence(
                exhausted.Count,
                retried.Count,
                retried.Count - exhausted.Count,
                considered.Count,
                context.Window.SessionCount,
                FindingOrder.Round(exhaustedShare),
                exhausted.Max(r => r.Attempts),
                attemptsSpent,
                configuration,
                retryWallClockMs,
                configuration.ConfiguredDelayMs * attemptsSpent,
                discounted,
                newest.Final.Session.StartedAt,
                RevisionContext.ReadSha(newest.Final.Session),
                [.. exhausted.Take(MaxExemplars).Select(ToExemplar)],
                Rescued(retried)),

            // The share of every run of this test that ended red with its retries already spent —
            // not the share of retried runs, which is what the condition thresholds. The two answer
            // different questions: the condition asks whether the retry attribute is working, and
            // this asks how unreliable the test is, which is what the report ranks on.
            //
            // Bounded below rather than taken raw, so that this test ranks against the others by a
            // figure that grows with the runs behind it. Two exhausted runs in two is the same 1.00
            // as forty in forty, and ranking them alike put the least-evidenced findings on top.
            Unreliability: WilsonInterval.LowerBound(exhausted.Count, considered.Count),

            // Dated by the exhaustions themselves rather than by the test's last execution, so a
            // test that ran out of retries a fortnight ago and has been clean since decays.
            SessionsSinceLastOccurrence: exhausted.Min(r => r.SessionIndex),

            DrillDownCommand: DrillDown.ForTest(FindingKind.RetryExhausted, test));
    }

    /// <summary>
    /// Picks one retried run that did settle green, or <see langword="null"/> when none did.
    /// </summary>
    /// <remarks>
    /// Absent rather than null-filled when there is nothing to contrast against: a consumer cannot
    /// tell an empty field apart from analysis that looked and found nothing. Its absence is itself
    /// the reading — the retries have never once worked.
    /// </remarks>
    private static RetryAttemptExemplar? Rescued(List<RunAttempts> retried)
    {
        foreach (RunAttempts run in retried)
        {
            if (run.Passed)
                return ToExemplar(run);
        }

        return null;
    }

    // -------------------------------------------------------------------------------------------
    // Deeper retries
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Attempts the deepening finding, returning <see langword="null"/> when a gate declines it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike masking, this deliberately does not exclude a test that also failed a build somewhere
    /// in the window. Masking excludes such a test because its entire claim is that nobody has
    /// noticed; this claims only that a cost has grown, which is still true and still measurable over
    /// the runs the test did pass. What it must not do is let a reader take every run in an arm for a
    /// green one, which is why <see cref="RetryDepthProfile.RunsFailedFinally"/> is published.
    /// </para>
    /// <para>
    /// Environmental runs are set aside from both arms. This is a delta over a three-session "now",
    /// and one outage inside the current slice fabricates the delta outright.
    /// </para>
    /// </remarks>
    private static FindingCandidate? Deepening(
        AnalysisContext context,
        TestReference test,
        List<RunAttempts> runs,
        HashSet<Guid> currentSessions)
    {
        List<RunAttempts> current = [];
        List<RunAttempts> baseline = [];
        int discounted = 0;

        foreach (RunAttempts run in runs)
        {
            if (run.Discounted)
                discounted++;
            else if (currentSessions.Contains(run.SessionId))
                current.Add(run);
            else
                baseline.Add(run);
        }

        List<RunAttempts> currentGreen = [.. current.Where(r => r.Passed)];
        List<RunAttempts> baselineGreen = [.. baseline.Where(r => r.Passed)];

        // Nothing to compare against is not a deepening. A test added this week has history in the
        // window but no history of its own, and one recent run is describing one run.
        if (baselineGreen.Count < MinimumBaselineSettledRuns ||
            currentGreen.Count < MinimumCurrentSettledRuns)
        {
            return null;
        }

        int baselineTypical = TypicalAttempts(baselineGreen);
        int currentTypical = TypicalAttempts(currentGreen);

        // Attempt numbers start at one, so this cannot arise from recorded data. Guarded because the
        // division below would otherwise produce an infinity comparing greater than every threshold.
        if (baselineTypical <= 0)
            return null;

        int increase = currentTypical - baselineTypical;
        if (increase < LocalAnalysisConstants.RetryDeepeningMinAttempts)
            return null;

        double relative = (double)increase / baselineTypical;

        long retryWallClockMs = 0;
        int attemptsSpent = 0;
        foreach (RunAttempts run in currentGreen)
        {
            retryWallClockMs += run.RetryWallClockMs;
            attemptsSpent += run.RetriedExecutions;
        }

        RetryConfiguration configuration = ConfigurationOf(currentGreen[0]);

        return new FindingCandidate(
            FindingKind.RetryDeepening,
            new FindingSubject.SingleTest(test),
            new RetryDeepeningEvidence(
                Profile(current, currentGreen, currentTypical),
                Profile(baseline, baselineGreen, baselineTypical),
                new RetryDepthDelta(
                    increase, FindingOrder.RoundPercent(relative * 100)),
                configuration,
                retryWallClockMs,
                configuration.ConfiguredDelayMs * attemptsSpent,
                discounted,
                FirstSeenAt(currentGreen),
                [.. currentGreen.Take(MaxExemplars).Select(ToExemplar)],
                Contrast(baselineGreen, baselineTypical)),

            // Doubling is as unreliable as this measure gets, as it is for a duration regression.
            // Beyond that the test is simply retry-dependent, and ranking one that went from a single
            // attempt to four above one that went from one to two would crowd out every other kind on
            // the strength of one arithmetic accident.
            Unreliability: Math.Min(1.0, relative),

            SessionsSinceLastOccurrence: currentGreen.Min(r => r.SessionIndex),

            DrillDownCommand: DrillDown.ForTest(FindingKind.RetryDeepening, test),

            // Nothing has failed a build here. Left uncapped, the generic impact formula would rank a
            // frequently-run test that still goes green above one that is failing today, because
            // "runs constantly and got slightly more expensive" scores well on every term it reads.
            SeverityCeiling: Severity.Medium);
    }

    /// <summary>
    /// Reads the attempt count a typical run needed, by nearest rank.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nearest rank rather than the interpolating definition the duration provider reads a
    /// percentile with, because an attempt count is discrete where a duration is continuous: it
    /// returns a whole attempt some run actually needed, rather than the one-and-a-half attempts no
    /// run can produce and no reader can act on. <see cref="LocalAnalysisConstants.RetryDeepeningMinAttempts"/>
    /// is stated in whole attempts on the strength of it.
    /// </para>
    /// <para>
    /// Its preference for the lower of two central readings is relied on rather than tolerated: from
    /// two runs, <c>[1, 3]</c> reads as 1, so a single deep run cannot produce this finding on its
    /// own. Lower is the conservative direction for a claim that a test has become more expensive.
    /// </para>
    /// <para>
    /// Median rather than maximum, because one unlucky earlier run would mask a real deepening and
    /// one unlucky recent run would manufacture one.
    /// </para>
    /// </remarks>
    private static int TypicalAttempts(List<RunAttempts> settled)
    {
        var attempts = new List<int>(settled.Count);
        foreach (RunAttempts run in settled)
            attempts.Add(run.Attempts);

        attempts.Sort();

        return Quantile.NearestRank(attempts, 0.50);
    }

    private static RetryDepthProfile Profile(
        List<RunAttempts> arm, List<RunAttempts> settled, int typical)
    {
        int max = 0;
        foreach (RunAttempts run in settled)
        {
            if (run.Attempts > max)
                max = run.Attempts;
        }

        return new RetryDepthProfile(
            typical, max, settled.Count, arm.Count(r => r.FailedFinally), arm.Count);
    }

    /// <summary>
    /// Reads the commit the change crossed into "now" at.
    /// </summary>
    /// <remarks>
    /// Taken from the oldest recent run <i>this test passed in</i>, not simply the oldest recent run.
    /// A test need not execute in every run, and naming a commit that never ran it would attribute
    /// the change to the wrong place.
    /// </remarks>
    private static string? FirstSeenAt(List<RunAttempts> currentGreen)
    {
        RunAttempts oldest = currentGreen[0];

        foreach (RunAttempts run in currentGreen)
        {
            if (run.SessionIndex > oldest.SessionIndex)
                oldest = run;
        }

        return RevisionContext.ReadSha(oldest.Final.Session);
    }

    /// <summary>
    /// Picks one earlier run typical of what a pass used to cost.
    /// </summary>
    /// <remarks>
    /// The one nearest the earlier typical figure rather than the newest, because the pair only makes
    /// the change reasonable about if the "before" half is representative of before. Ties fall to the
    /// newest such run, which is total because one run per session.
    /// </remarks>
    private static RetryAttemptExemplar? Contrast(List<RunAttempts> baselineGreen, int typical)
    {
        RunAttempts? nearest = null;

        foreach (RunAttempts run in baselineGreen)
        {
            if (nearest == null ||
                Math.Abs(run.Attempts - typical) < Math.Abs(nearest.Attempts - typical))
            {
                nearest = run;
            }
        }

        return nearest == null ? null : ToExemplar(nearest);
    }

    // -------------------------------------------------------------------------------------------
    // Masked by retry
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Attempts the masking finding, returning <see langword="null"/> when a gate declines it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A test that failed and passed on retry is invisible in a green build, which is exactly what
    /// makes it worth surfacing: it is the cheapest genuine flakiness signal available, needing no
    /// history at all — one run that retried is enough to have observed one.
    /// </para>
    /// <para>
    /// The second half of the condition is what separates this from ordinary flakiness. A test that
    /// also fails a build somewhere in the window is not hidden; it is already costing someone their
    /// afternoon, and reporting it here as well would double-count it against a kind whose entire
    /// claim is that nobody has noticed.
    /// </para>
    /// <para>
    /// Deliberately not sliced, and deliberately not discounted for environmental runs. Masking is a
    /// standing property of the window rather than a change between two halves of it, and the
    /// population is the one this kind shipped with — narrowing it here would move the numbers of an
    /// already-published finding for a reason that belongs to the two kinds above it.
    /// </para>
    /// </remarks>
    private static FindingCandidate? Masked(
        AnalysisContext context,
        TestReference test,
        IReadOnlyList<ExecutionRef> executions,
        List<RunAttempts> runs)
    {
        List<ExecutionRef> masked = [.. executions.Where(IsMasked)];
        if (masked.Count == 0)
            return null;

        // Judged on the reduced runs, so this can never disagree with the shared index about whether
        // a session ended red.
        foreach (RunAttempts run in runs)
        {
            if (run.FailedFinally)
                return null;
        }

        // Executions arrive newest-session-first, so the head is the most recent masking.
        ExecutionRef newest = masked[0];

        return new FindingCandidate(
            FindingKind.RetryMasked,
            new FindingSubject.SingleTest(test),
            MaskedEvidence(context, executions, masked, newest),

            // The share of this test's runs that needed a retry to look green. A test masked on one
            // run in twenty is a different proposition from one masked on every run, and the ratio
            // says so without claiming to know why.
            //
            // Bounded below, because this kind emits on a single occurrence by design and a single
            // occurrence is exactly what a point estimate cannot rank honestly: one masked run in
            // one is 1.00. The bound puts it at 0.21 and leaves the well-evidenced cases above it.
            Unreliability: WilsonInterval.LowerBound(masked.Count, executions.Count),

            SessionsSinceLastOccurrence: newest.SessionIndex,

            DrillDownCommand: DrillDown.ForTest(FindingKind.RetryMasked, test));
    }

    private static bool IsMasked(ExecutionRef reference) =>
        reference.Execution.Retry is { AttemptNumber: > 1, PassedOnRetry: true };

    private static RetryMaskedEvidence MaskedEvidence(
        AnalysisContext context,
        IReadOnlyList<ExecutionRef> executions,
        List<ExecutionRef> masked,
        ExecutionRef newest)
    {
        var maskedSessions = new HashSet<Guid>(masked.Select(m => m.Session.SessionId));

        // Every attempt after the first, in the runs where masking happened: the time the suite spent
        // re-running a test to make it pass.
        long retryWallClockMs = 0;
        int attemptsSpent = 0;
        foreach (ExecutionRef reference in executions)
        {
            if (AttemptOf(reference) > 1 && maskedSessions.Contains(reference.Session.SessionId))
            {
                retryWallClockMs += (long)reference.Execution.Duration.TotalMilliseconds;
                attemptsSpent++;
            }
        }

        RetryConfiguration configuration = ConfigurationOf(newest);

        return new RetryMaskedEvidence(
            masked.Count,
            executions.Count,
            context.Window.SessionCount,
            maskedSessions.Count,
            FindingOrder.Round((double)masked.Count / executions.Count),
            masked.Max(AttemptOf),
            configuration,
            retryWallClockMs,
            configuration.ConfiguredDelayMs * attemptsSpent,
            newest.Session.StartedAt,
            RevisionContext.ReadSha(newest.Session),
            MaskedExemplars(executions, masked));
    }

    private static List<RetryMaskedExemplar> MaskedExemplars(
        IReadOnlyList<ExecutionRef> executions,
        List<ExecutionRef> masked)
    {
        // Newest first, so the exemplars answer "what does this look like now" rather than "what did
        // it look like a fortnight ago". Every key is total, so the same window always yields the
        // same three.
        IEnumerable<ExecutionRef> ordered = masked
            .OrderBy(m => m.SessionIndex)
            .ThenBy(AttemptOf)
            .ThenBy(m => m.Execution.ExecutionId.ToString("N", CultureInfo.InvariantCulture),
                StringComparer.Ordinal)
            .Take(MaxExemplars);

        var exemplars = new List<RetryMaskedExemplar>(MaxExemplars);

        foreach (ExecutionRef reference in ordered)
        {
            int attempt = AttemptOf(reference);

            List<ExecutionRef> preceding = [.. executions
                .Where(e => e.Session.SessionId == reference.Session.SessionId && AttemptOf(e) < attempt)
                .OrderBy(AttemptOf)];

            // What the retry hid. Read from the last failure before the pass, because that is the
            // attempt whose message the developer never saw.
            string? errorMessage = preceding
                .LastOrDefault(e => e.Execution.Outcome.IsFailure())?
                .Execution.ErrorMessage;

            exemplars.Add(new RetryMaskedExemplar(
                reference.Session.SessionId.ToString("D", CultureInfo.InvariantCulture),
                reference.Session.StartedAt,
                RevisionContext.ReadSha(reference.Session),
                attempt,
                preceding.Count,
                (long)reference.Execution.Duration.TotalMilliseconds,
                EvidenceText.Elide(errorMessage)));
        }

        return exemplars;
    }

    // -------------------------------------------------------------------------------------------
    // Shared
    // -------------------------------------------------------------------------------------------

    private static int AttemptOf(ExecutionRef reference) =>
        reference.Execution.Retry?.AttemptNumber ?? 1;

    /// <summary>
    /// Transcribes the retry configuration off the run that decided the finding.
    /// </summary>
    /// <remarks>
    /// Read from one run rather than reconciled across the window, for the reason the timeout finding
    /// reads its declared budget off the newest timed-out execution: a test's attribute can change
    /// across a window, and the current declaration is what a reader would find in the source today.
    /// </remarks>
    private static RetryConfiguration ConfigurationOf(RunAttempts run) =>
        ConfigurationOf(run.Final);

    private static RetryConfiguration ConfigurationOf(ExecutionRef reference)
    {
        RetryMetadata? retry = reference.Execution.Retry;

        if (retry == null)
            return new RetryConfiguration(null, 0, null, 0);

        return new RetryConfiguration(
            // Reported as the SDK recorded it, never inferred. An empty name is what an adapter
            // writes when it could not identify the mechanism, and publishing that verbatim would
            // put a blank where a reader expects a name; absent says the same thing honestly.
            string.IsNullOrEmpty(retry.RetryAttributeName) ? null : retry.RetryAttributeName,
            retry.MaxRetries,

            // Empty is absent. The MSTest adapter writes an empty string where the other two leave
            // null, and a blank reason is not a reason.
            string.IsNullOrEmpty(retry.RetryReason) ? null : retry.RetryReason,
            (long)retry.DelayBetweenRetries.TotalMilliseconds);
    }

    private static RetryAttemptExemplar ToExemplar(RunAttempts run) =>
        new(
            run.SessionId.ToString("D", CultureInfo.InvariantCulture),
            run.Final.Session.StartedAt,
            RevisionContext.ReadSha(run.Final.Session),
            run.Attempts,
            run.Final.Execution.Outcome.ToString(),
            run.RetryWallClockMs,
            EvidenceText.Elide(run.LastBefore?.Execution.ErrorMessage));
}
