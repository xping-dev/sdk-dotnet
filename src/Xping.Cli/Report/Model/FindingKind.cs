/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Model;

/// <summary>
/// What a finding claims about its subject.
/// </summary>
/// <remarks>
/// <para>
/// Every kind here is implemented by a provider and rests on an input that varies between runs. That
/// is the bar for declaring one at all: a member the data can never answer reads as a promise in the
/// JSON contract and in <c>--kind</c>, and leaves a reader waiting for a finding that cannot arrive.
/// </para>
/// <para>
/// Declaration order is a tiebreaker in the finding sort, so it must not be reordered casually. It
/// runs roughly from the cheapest, most actionable signal to the most speculative.
/// </para>
/// </remarks>
internal enum FindingKind
{
    /// <summary>
    /// The test failed and passed on retry, never contributing to a session's final outcome.
    /// </summary>
    /// <remarks>
    /// Invisible in a green build, and the only genuine flakiness signal that needs no history at
    /// all — which is why it sorts first.
    /// </remarks>
    RetryMasked,

    /// <summary>
    /// The test needs more attempts to pass than it used to.
    /// </summary>
    /// <remarks>
    /// Masking plus a direction. Sorted immediately after <see cref="RetryMasked"/> because it says
    /// the same thing and more, but costs a baseline to compute where masking costs nothing — and a
    /// test that has deepened is necessarily masked, so the two are decided together.
    /// </remarks>
    RetryDeepening,

    /// <summary>
    /// The test's retries ran out and it failed the run anyway.
    /// </summary>
    /// <remarks>
    /// Needs no history — one run that spent its retries is one observation — and every attempt's
    /// failure text is in the evidence, so it is fully actionable. Sorted after
    /// <see cref="RetryDeepening"/> because a deepening is a cost nobody can see any other way,
    /// whereas an exhausted run already went red in the runner output; and ahead of
    /// <see cref="Flaky"/> because it is strictly more informative about that same red run.
    /// </remarks>
    RetryExhausted,

    /// <summary>The test both passes and fails, or fails in varying ways.</summary>
    Flaky,

    /// <summary>The test fails almost always and in one dominant way. Broken, not flaky.</summary>
    AlwaysFailing,

    /// <summary>The test is mostly killed for overrunning its timeout rather than failing outright.</summary>
    /// <remarks>
    /// Reported apart from <see cref="AlwaysFailing"/> and <see cref="Flaky"/> because a hang is a
    /// different defect with a different remedy, and because the evidence differs: a timed-out test
    /// leaves no assertion message and no stack frame worth grouping on, so pooling it with ordinary
    /// failures produces signatures that describe nothing.
    /// </remarks>
    TimingOut,

    /// <summary>
    /// Several tests fail alike because one shared lifecycle member is broken, and that member is
    /// named.
    /// </summary>
    /// <remarks>
    /// A <see cref="SharedFailure"/> whose cause is known. Sorted ahead of it because naming the
    /// member to fix is strictly more actionable than reporting that several tests fail the same way,
    /// and the two never contend: a cluster becomes this only when every failure in it agrees on the
    /// member, and stays a <see cref="SharedFailure"/> otherwise.
    /// </remarks>
    BrokenFixture,

    /// <summary>Several tests fail with one signature in one session — one cause, not many.</summary>
    SharedFailure,

    /// <summary>The test's median duration has increased against its own baseline.</summary>
    DurationRegression,

    /// <summary>The test's duration varies too much for a regression to be measurable.</summary>
    DurationUnstable,

    /// <summary>The test's failure rate moves with how many tests ran alongside it.</summary>
    ParallelSensitive,

    /// <summary>The test's failures cluster at one time of day, day group, or UTC offset.</summary>
    /// <remarks>
    /// Sorted after <see cref="ParallelSensitive"/> because it says the same sort of thing — the
    /// test's failures track a condition of its surroundings rather than anything in the test — and
    /// because it is the weaker of the two. Concurrency is a condition the suite imposed; a clock
    /// reading is a condition the suite merely ran under, and correlating with one is a lead rather
    /// than a cause.
    /// </remarks>
    TimeSensitive,

    /// <summary>The test appeared throughout the baseline and has stopped running.</summary>
    Vanished
}
