/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Report.Indexes;

/// <summary>
/// How a session ended, judged one test at a time.
/// </summary>
/// <remarks>
/// A test that failed and then passed on retry did not fail the session, so every question about
/// what a session ended up doing is answered on the highest attempt number recorded for each
/// fingerprint. Answering it differently in two places would let the report call a session green
/// while flagging a test inside it as having blocked the build.
/// </remarks>
internal static class SessionOutcomes
{
    /// <summary>
    /// Counts the distinct tests a session ran and how many of them ended it as a failure.
    /// </summary>
    /// <param name="session">The session to tally.</param>
    /// <returns>The distinct test count and the failing test count.</returns>
    public static (int Tests, int Failures) Tally(TestSession session)
    {
        int tests = 0;
        int failures = 0;

        foreach (var outcome in FinalOutcomes(session).Values)
        {
            tests++;
            if (outcome.Outcome.IsFailure())
                failures++;
        }

        return (tests, failures);
    }

    /// <summary>
    /// Returns whether a session ended with at least one test failing on its final attempt.
    /// </summary>
    /// <param name="session">The session to inspect.</param>
    /// <returns><see langword="true"/> when the session ended red.</returns>
    public static bool HasFinalFailure(TestSession session)
    {
        foreach (var outcome in FinalOutcomes(session).Values)
        {
            if (outcome.Outcome.IsFailure())
                return true;
        }

        return false;
    }

    /// <summary>
    /// Reduces a session to one outcome per test, taken from its last attempt.
    /// </summary>
    /// <param name="session">The session to reduce.</param>
    /// <returns>The final attempt and outcome, keyed by fingerprint.</returns>
    private static Dictionary<string, (int Attempt, TestOutcome Outcome)> FinalOutcomes(
        TestSession session)
    {
        var finalOutcomes = new Dictionary<string, (int Attempt, TestOutcome Outcome)>(
            StringComparer.Ordinal);

        foreach (TestExecution execution in session.Executions)
        {
            string fingerprint = execution.Identity.TestFingerprint;
            if (string.IsNullOrEmpty(fingerprint))
                continue;

            int attempt = execution.Retry?.AttemptNumber ?? 1;

            if (!finalOutcomes.TryGetValue(fingerprint, out var existing) || attempt >= existing.Attempt)
                finalOutcomes[fingerprint] = (attempt, execution.Outcome);
        }

        return finalOutcomes;
    }
}
