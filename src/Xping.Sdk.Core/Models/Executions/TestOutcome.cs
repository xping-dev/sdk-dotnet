/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models.Executions;

/// <summary>
/// Represents the outcome of a test execution.
/// </summary>
public enum TestOutcome
{
    /// <summary>
    /// The test passed successfully.
    /// </summary>
    Passed = 0,

    /// <summary>
    /// The test failed due to an assertion failure or unexpected exception.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// The test was skipped and not executed.
    /// </summary>
    Skipped = 2,

    /// <summary>
    /// The test completed but the result was inconclusive.
    /// </summary>
    Inconclusive = 3,

    /// <summary>
    /// The test was not executed.
    /// </summary>
    NotExecuted = 4,

    /// <summary>
    /// The test was killed by its test framework for exceeding a timeout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Distinct from <see cref="Failed"/> because the two describe different defects. A failed test
    /// ran to completion and disagreed with an assertion; a timed-out test never reached one. The
    /// cause is usually a deadlock, an unbounded wait, or a dependency that stopped answering, and
    /// the evidence a failed test leaves behind — an assertion message, a stack trace pointing at the
    /// disagreement — is absent or meaningless here.
    /// </para>
    /// <para>
    /// Counts as a failure for the purposes of whether a run ended red; use
    /// <see cref="TestOutcomeExtensions.IsFailure"/> rather than comparing against
    /// <see cref="Failed"/> directly.
    /// </para>
    /// </remarks>
    Timeout = 5
}
