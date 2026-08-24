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
/// Every kind is declared from the outset, including those no provider implements yet. The enum is
/// part of the JSON contract, so a consumer written against it should not need updating each time a
/// provider ships; and <c>--kind</c> should reject a typo rather than a not-yet-implemented value.
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

    /// <summary>The test both passes and fails, or fails in varying ways.</summary>
    Flaky,

    /// <summary>The test fails almost always and in one consistent way. Broken, not flaky.</summary>
    AlwaysFailing,

    /// <summary>The test is mostly killed for overrunning its timeout rather than failing outright.</summary>
    /// <remarks>
    /// Reported apart from <see cref="AlwaysFailing"/> and <see cref="Flaky"/> because a hang is a
    /// different defect with a different remedy, and because the evidence differs: a timed-out test
    /// leaves no assertion message and no stack frame worth grouping on, so pooling it with ordinary
    /// failures produces signatures that describe nothing.
    /// </remarks>
    TimingOut,

    /// <summary>Several tests fail with one signature in one session — one cause, not many.</summary>
    SharedFailure,

    /// <summary>The test's median duration has increased against its own baseline.</summary>
    DurationRegression,

    /// <summary>The test's duration varies too much for a regression to be measurable.</summary>
    DurationUnstable,

    /// <summary>The test fails when it runs after one particular predecessor.</summary>
    OrderDependent,

    /// <summary>The test's failure rate differs between parallel and serial execution.</summary>
    ParallelSensitive,

    /// <summary>The test's failures cluster in sessions with degraded or absent network.</summary>
    NetworkDependent,

    /// <summary>The test appeared throughout the baseline and has stopped running.</summary>
    Vanished,

    /// <summary>The test was expected but never executed.</summary>
    NeverRun
}
