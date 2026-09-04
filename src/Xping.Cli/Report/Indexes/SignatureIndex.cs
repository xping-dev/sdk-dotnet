/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Signatures;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Report.Indexes;

/// <summary>
/// One failure signature as it appeared for one test.
/// </summary>
/// <param name="Signature">The signature, readable components and all.</param>
/// <param name="Occurrences">Failures of this test carrying it.</param>
/// <param name="NewestSessionIndex">Newest session it appeared in; 0 is the newest in the window.</param>
/// <param name="OldestSessionIndex">Oldest session it appeared in.</param>
/// <param name="FirstSeenAt">Start of the oldest session it appeared in.</param>
/// <param name="FirstSeenSha">Commit that session ran at, when one was recorded.</param>
internal sealed record SignatureOccurrence(
    FailureSignature Signature,
    int Occurrences,
    int NewestSessionIndex,
    int OldestSessionIndex,
    DateTime FirstSeenAt,
    string? FirstSeenSha);

/// <summary>
/// One failure signature as it appeared across every test in the window.
/// </summary>
/// <param name="Signature">The signature, readable components and all.</param>
/// <param name="Fingerprints">Distinct tests that failed with it, in ordinal order.</param>
/// <param name="Failures">Every failure carrying it, newest first.</param>
/// <param name="MaxTestsInOneSession">
/// The most distinct tests it hit within a single session — the measurement the shared-failure
/// threshold is applied to.
/// </param>
/// <param name="SessionCount">Distinct sessions it appeared in.</param>
/// <param name="OldestSessionIndex">Oldest session it appeared in.</param>
/// <param name="FirstSeenAt">Start of the oldest session it appeared in.</param>
/// <param name="FirstSeenSha">Commit that session ran at, when one was recorded.</param>
internal sealed record SignatureGroup(
    FailureSignature Signature,
    IReadOnlyList<string> Fingerprints,
    IReadOnlyList<ExecutionRef> Failures,
    int MaxTestsInOneSession,
    int SessionCount,
    int OldestSessionIndex,
    DateTime FirstSeenAt,
    string? FirstSeenSha);

/// <summary>
/// Every failure in the window, reduced to signatures and indexed both ways.
/// </summary>
/// <remarks>
/// <para>
/// Signatures are computed once here and reused, because computing them is the expensive part of
/// this analysis — a regular expression pipeline and a SHA-256 over every failed execution — and
/// because two providers computing them separately could disagree about what "the same failure"
/// means.
/// </para>
/// <para>
/// Both directions are needed. Per test answers "does this test fail the same way every time",
/// which separates a broken test from a flaky one. Per signature answers "how many tests fail this
/// way", which is what turns a page of failures into a handful of causes.
/// </para>
/// <para>
/// Every list this exposes is sorted. Dictionaries back the lookups, but their enumeration order is
/// not stable across processes and the report has to be byte-identical between runs.
/// </para>
/// </remarks>
internal sealed class SignatureIndex
{
    private readonly Dictionary<(Guid Session, Guid Execution), FailureSignature> _byExecution;
    private readonly Dictionary<string, List<SignatureOccurrence>> _byFingerprint;
    private readonly Dictionary<string, SignatureGroup> _byHash;

    private SignatureIndex(
        Dictionary<(Guid Session, Guid Execution), FailureSignature> byExecution,
        Dictionary<string, List<SignatureOccurrence>> byFingerprint,
        Dictionary<string, SignatureGroup> byHash,
        IReadOnlyList<string> hashes)
    {
        _byExecution = byExecution;
        _byFingerprint = byFingerprint;
        _byHash = byHash;
        Hashes = hashes;
    }

    /// <summary>Gets every signature hash in the window, in ordinal order.</summary>
    public IReadOnlyList<string> Hashes { get; }

    /// <summary>
    /// Gets the signature of one failed execution.
    /// </summary>
    /// <param name="reference">The execution to look up, with the session it ran in.</param>
    /// <returns>Its signature, or <see langword="null"/> when the execution did not fail.</returns>
    /// <remarks>
    /// Keyed on the session as well as the execution. An execution id is meant to be unique, but a
    /// lookup that silently returns another execution's signature when it is not would corrupt every
    /// count built on top of it, and the session makes that impossible for the price of a tuple.
    /// </remarks>
    public FailureSignature? Of(ExecutionRef reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return _byExecution.TryGetValue(
            (reference.Session.SessionId, reference.Execution.ExecutionId),
            out FailureSignature? signature)
            ? signature
            : null;
    }

    /// <summary>
    /// Gets the distinct signatures one test failed with, most frequent first.
    /// </summary>
    /// <param name="fingerprint">The test to look up.</param>
    /// <returns>Its signatures, or an empty list when it never failed in the window.</returns>
    public IReadOnlyList<SignatureOccurrence> SignaturesOf(string fingerprint) =>
        _byFingerprint.TryGetValue(fingerprint, out List<SignatureOccurrence>? occurrences)
            ? occurrences
            : [];

    /// <summary>
    /// Gets everything known about one signature across the window.
    /// </summary>
    /// <param name="hash">The signature to look up.</param>
    /// <returns>Its group, or <see langword="null"/> when no failure carried it.</returns>
    public SignatureGroup? GroupFor(string hash) =>
        _byHash.TryGetValue(hash, out SignatureGroup? group) ? group : null;

    /// <summary>
    /// Builds the index for a window.
    /// </summary>
    /// <param name="window">The sessions under analysis.</param>
    /// <returns>The index.</returns>
    public static SignatureIndex Build(AnalysisWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var byExecution = new Dictionary<(Guid Session, Guid Execution), FailureSignature>();
        var perTest = new Dictionary<(string Fingerprint, string Hash), List<ExecutionRef>>();
        var perSignature = new Dictionary<string, List<ExecutionRef>>(StringComparer.Ordinal);
        var signatures = new Dictionary<string, FailureSignature>(StringComparer.Ordinal);

        for (int position = 0; position < window.Sessions.Count; position++)
        {
            TestSession session = window.Sessions[position];

            foreach (TestExecution execution in session.Executions)
            {
                // Only failures are signed. A skipped execution carries its skip reason in
                // ErrorMessage, which would otherwise be normalised and grouped as though the test
                // had failed that way.
                if (!execution.Outcome.IsFailure())
                    continue;

                string fingerprint = execution.Identity.TestFingerprint;
                if (string.IsNullOrEmpty(fingerprint))
                    continue;

                FailureSignature signature = FailureSignatureFactory.Create(execution, fingerprint);
                byExecution[(session.SessionId, execution.ExecutionId)] = signature;
                signatures[signature.Hash] = signature;

                var reference = new ExecutionRef(session, position, execution);

                Add(perTest, (fingerprint, signature.Hash), reference);
                Add(perSignature, signature.Hash, reference);
            }
        }

        return new SignatureIndex(
            byExecution,
            BuildPerTest(perTest, signatures),
            BuildPerSignature(perSignature, signatures),
            [.. perSignature.Keys.OrderBy(h => h, StringComparer.Ordinal)]);
    }

    private static void Add<TKey>(
        Dictionary<TKey, List<ExecutionRef>> target, TKey key, ExecutionRef reference)
        where TKey : notnull
    {
        if (!target.TryGetValue(key, out List<ExecutionRef>? references))
        {
            references = [];
            target[key] = references;
        }

        references.Add(reference);
    }

    private static Dictionary<string, List<SignatureOccurrence>> BuildPerTest(
        Dictionary<(string Fingerprint, string Hash), List<ExecutionRef>> perTest,
        Dictionary<string, FailureSignature> signatures)
    {
        var result = new Dictionary<string, List<SignatureOccurrence>>(StringComparer.Ordinal);

        foreach (var entry in perTest)
        {
            List<ExecutionRef> failures = entry.Value;

            var occurrence = new SignatureOccurrence(
                signatures[entry.Key.Hash],
                failures.Count,
                failures.Min(f => f.SessionIndex),
                failures.Max(f => f.SessionIndex),
                Oldest(failures).Session.StartedAt,
                RevisionContext.ReadSha(Oldest(failures).Session));

            if (!result.TryGetValue(entry.Key.Fingerprint, out List<SignatureOccurrence>? list))
            {
                list = [];
                result[entry.Key.Fingerprint] = list;
            }

            list.Add(occurrence);
        }

        // Most frequent first, then most recent, then by hash. The head is the failure mode a
        // developer looking at this test will meet most often, and the tail order is fixed so the
        // published evidence does not shuffle between runs.
        foreach (List<SignatureOccurrence> list in result.Values)
        {
            list.Sort((left, right) =>
            {
                int byCount = right.Occurrences.CompareTo(left.Occurrences);
                if (byCount != 0)
                    return byCount;

                int byRecency = left.NewestSessionIndex.CompareTo(right.NewestSessionIndex);
                return byRecency != 0
                    ? byRecency
                    : string.CompareOrdinal(left.Signature.Hash, right.Signature.Hash);
            });
        }

        return result;
    }

    private static Dictionary<string, SignatureGroup> BuildPerSignature(
        Dictionary<string, List<ExecutionRef>> perSignature,
        Dictionary<string, FailureSignature> signatures)
    {
        var result = new Dictionary<string, SignatureGroup>(StringComparer.Ordinal);

        foreach (var entry in perSignature)
        {
            List<ExecutionRef> failures = [.. entry.Value.OrderBy(f => f.SessionIndex)
                .ThenBy(f => f.Execution.Identity.TestFingerprint, StringComparer.Ordinal)
                .ThenBy(f => f.Execution.ExecutionId.ToString("N", CultureInfo.InvariantCulture),
                    StringComparer.Ordinal)];

            var fingerprints = failures
                .Select(f => f.Execution.Identity.TestFingerprint)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            // The shared-failure threshold is about one run: three tests failing the same way in the
            // same session share something. Three tests failing the same way in three different
            // sessions a week apart is a much weaker claim, and counting it would be how the report
            // starts inventing causes.
            int maxTestsInOneSession = failures
                .GroupBy(f => f.Session.SessionId)
                .Select(g => g.Select(f => f.Execution.Identity.TestFingerprint)
                    .Distinct(StringComparer.Ordinal).Count())
                .DefaultIfEmpty(0)
                .Max();

            int sessionCount = failures.Select(f => f.Session.SessionId).Distinct().Count();

            result[entry.Key] = new SignatureGroup(
                signatures[entry.Key],
                fingerprints,
                failures,
                maxTestsInOneSession,
                sessionCount,
                failures.Max(f => f.SessionIndex),
                Oldest(failures).Session.StartedAt,
                RevisionContext.ReadSha(Oldest(failures).Session));
        }

        return result;
    }

    /// <summary>
    /// Returns the failure from the oldest session, which is where a signature was first seen.
    /// </summary>
    /// <remarks>
    /// Sessions are ordered newest first, so the oldest carries the highest index.
    /// </remarks>
    private static ExecutionRef Oldest(List<ExecutionRef> failures)
    {
        ExecutionRef oldest = failures[0];

        foreach (ExecutionRef reference in failures)
        {
            if (reference.SessionIndex > oldest.SessionIndex)
                oldest = reference;
        }

        return oldest;
    }
}
