/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models.Executions;

/// <summary>
/// Questions about a <see cref="TestOutcome"/> that more than one caller needs to ask.
/// </summary>
public static class TestOutcomeExtensions
{
    /// <summary>
    /// Returns whether an outcome means the test did not succeed and should turn its run red.
    /// </summary>
    /// <param name="outcome">The outcome to classify.</param>
    /// <returns><see langword="true"/> for <see cref="TestOutcome.Failed"/> and
    /// <see cref="TestOutcome.Timeout"/>.</returns>
    /// <remarks>
    /// Every "did this go wrong" test goes through here rather than comparing against
    /// <see cref="TestOutcome.Failed"/>. Those comparisons were the whole reason a timed-out test
    /// could be counted as green: each new failing outcome would otherwise have to be remembered at
    /// a dozen call sites, and the ones that were forgotten would fail silently rather than loudly.
    /// </remarks>
    public static bool IsFailure(this TestOutcome outcome) =>
        outcome == TestOutcome.Failed || outcome == TestOutcome.Timeout;
}
