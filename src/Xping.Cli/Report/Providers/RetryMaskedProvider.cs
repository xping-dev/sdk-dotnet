/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Report.Providers;

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
/// <param name="RetryAttributeName">The retry mechanism in use, as the SDK named it.</param>
/// <param name="MaxRetriesConfigured">Retries the attribute allows, as configured.</param>
/// <param name="RetryWallClockMs">Wall-clock spent on attempts after the first.</param>
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
    string? RetryAttributeName,
    int MaxRetriesConfigured,
    long RetryWallClockMs,
    DateTime LastMaskedAt,
    string? LastMaskedSha,
    IReadOnlyList<RetryMaskedExemplar> Exemplars) : FindingEvidence;

/// <summary>
/// Reports tests that failed, passed on retry, and never failed a build.
/// </summary>
/// <remarks>
/// <para>
/// These tests are invisible in a green build, which is exactly what makes them worth surfacing
/// first: they are the cheapest genuine flakiness signal available, needing no history at all — one
/// run that retried is enough to have observed one.
/// </para>
/// <para>
/// The second half of the condition is what separates this from ordinary flakiness. A test that also
/// fails a build somewhere in the window is not hidden; it is already costing someone their
/// afternoon, and reporting it here as well would double-count it against a kind whose entire claim
/// is that nobody has noticed.
/// </para>
/// <para>
/// Populated only where an adapter records retry attempts. Verified against the SDK: NUnit records
/// one execution per attempt with a correct attempt number, while the xUnit and MSTest adapters
/// cannot currently produce an attempt number above one. Silence for those frameworks is
/// indistinguishable from "no retries happened" and is the honest result until they can.
/// </para>
/// </remarks>
internal sealed class RetryMaskedProvider : IFindingProvider
{
    // Three, per the output contract's exemplar budget. A per-provider constant rather than a shared
    // threshold: the specification's constant table does not list it, and adding an entry there
    // would be a threshold this session invented.
    private const int MaxExemplars = 3;

    /// <inheritdoc/>
    public string Name => "retry-masked";

    /// <inheritdoc/>
    public IReadOnlyList<FindingKind> Kinds => [FindingKind.RetryMasked];

    /// <inheritdoc/>
    public IEnumerable<FindingCandidate> Analyze(AnalysisContext context)
    {
        // Deliberately not sliced. Masking is a standing property of the window, not a change
        // between two halves of it, so a current/baseline split would only shrink what can be seen.
        foreach (string fingerprint in context.Tests.Fingerprints)
        {
            IReadOnlyList<ExecutionRef> executions = context.Tests.ExecutionsOf(fingerprint);

            List<ExecutionRef> masked = [.. executions.Where(IsMasked)];
            if (masked.Count == 0)
                continue;

            if (EverFailedFinally(executions))
                continue;

            TestReference? reference = context.Tests.ReferenceFor(fingerprint);
            if (reference == null)
                continue;

            // Executions arrive newest-session-first, so the head is the most recent masking.
            ExecutionRef newest = masked[0];

            yield return new FindingCandidate(
                FindingKind.RetryMasked,
                new FindingSubject.SingleTest(reference),
                BuildEvidence(context, executions, masked, newest),

                // The share of this test's runs that needed a retry to look green. A test masked on
                // one run in twenty is a different proposition from one masked on every run, and the
                // ratio says so without claiming to know why.
                Unreliability: Math.Min(1.0, (double)masked.Count / executions.Count),

                SessionsSinceLastOccurrence: newest.SessionIndex,

                DrillDownCommand: DrillDown.ForTest(FindingKind.RetryMasked, reference));
        }
    }

    private static bool IsMasked(ExecutionRef reference) =>
        reference.Execution.Retry is { AttemptNumber: > 1, PassedOnRetry: true };

    private static int AttemptOf(ExecutionRef reference) =>
        reference.Execution.Retry?.AttemptNumber ?? 1;

    /// <summary>
    /// Returns whether this test ended a session as a failure at any point in the window.
    /// </summary>
    /// <remarks>
    /// Judged on the highest attempt number within each session, matching how
    /// <see cref="TestIndex"/> decides whether a session failed. Deciding it differently here would
    /// let the report call a session green while flagging a test in it as blocking, or the reverse.
    /// </remarks>
    private static bool EverFailedFinally(IReadOnlyList<ExecutionRef> executions)
    {
        var finalOutcomes = new Dictionary<Guid, (int Attempt, TestOutcome Outcome)>();

        foreach (ExecutionRef reference in executions)
        {
            Guid session = reference.Session.SessionId;
            int attempt = AttemptOf(reference);

            if (!finalOutcomes.TryGetValue(session, out var existing) || attempt >= existing.Attempt)
                finalOutcomes[session] = (attempt, reference.Execution.Outcome);
        }

        foreach (var outcome in finalOutcomes.Values)
        {
            if (outcome.Outcome == TestOutcome.Failed)
                return true;
        }

        return false;
    }

    private static RetryMaskedEvidence BuildEvidence(
        AnalysisContext context,
        IReadOnlyList<ExecutionRef> executions,
        List<ExecutionRef> masked,
        ExecutionRef newest)
    {
        var maskedSessions = new HashSet<Guid>(masked.Select(m => m.Session.SessionId));

        // Every attempt after the first, in the runs where masking happened: the time the suite
        // spent re-running a test to make it pass.
        long retryWallClockMs = 0;
        foreach (ExecutionRef reference in executions)
        {
            if (AttemptOf(reference) > 1 && maskedSessions.Contains(reference.Session.SessionId))
                retryWallClockMs += (long)reference.Execution.Duration.TotalMilliseconds;
        }

        RetryMetadata newestRetry = newest.Execution.Retry!;

        return new RetryMaskedEvidence(
            masked.Count,
            executions.Count,
            context.Window.SessionCount,
            maskedSessions.Count,
            FindingOrder.Round((double)masked.Count / executions.Count),
            masked.Max(AttemptOf),

            // Reported as the SDK recorded it, never inferred. An empty name means the adapter did
            // not identify the mechanism, which is not the same as it having no name.
            string.IsNullOrEmpty(newestRetry.RetryAttributeName) ? null : newestRetry.RetryAttributeName,
            newestRetry.MaxRetries,
            retryWallClockMs,
            newest.Session.StartedAt,
            RevisionContext.ReadSha(newest.Session),
            BuildExemplars(executions, masked));
    }

    private static List<RetryMaskedExemplar> BuildExemplars(
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
                .LastOrDefault(e => e.Execution.Outcome == TestOutcome.Failed)?
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
}
