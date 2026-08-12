/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Signatures;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Report.Providers;

/// <summary>
/// One distinct way a test failed, and when it was first seen.
/// </summary>
/// <param name="Hash">The grouping key. Only useful for joining, never for reading.</param>
/// <param name="ExceptionType">The type the adapter recorded, or null when it recorded none.</param>
/// <param name="Message">The message with run-varying detail replaced by tokens.</param>
/// <param name="Frames">The frames the signature was built from, method signature only.</param>
/// <param name="Degraded">Whether framework frames were used because no user frame was found.</param>
/// <param name="Unavailable">
/// Whether the adapter recorded nothing to build a signature from. True for every failure from an
/// adapter that does not capture failure detail, and the reason such failures never group.
/// </param>
/// <param name="Occurrences">Failures carrying this signature.</param>
/// <param name="FirstSeenAt">Start of the oldest analysed session it appeared in.</param>
/// <param name="FirstSeenSha">Commit that session ran at, when one was recorded.</param>
/// <param name="FirstSeenSessionsAgo">How many sessions back that was; 0 is the newest.</param>
/// <param name="FirstSeenInLatestSession">Whether it has only ever appeared in the newest session.</param>
/// <param name="FirstSeenAfterWindowStart">
/// Whether it was absent from the oldest analysed session. An observation about this window, not a
/// claim that the signature has never occurred before — analysis cannot see past its own boundary.
/// </param>
internal sealed record SignatureView(
    string Hash,
    string? ExceptionType,
    string Message,
    IReadOnlyList<string> Frames,
    bool Degraded,
    bool Unavailable,
    int Occurrences,
    DateTime FirstSeenAt,
    string? FirstSeenSha,
    int FirstSeenSessionsAgo,
    bool FirstSeenInLatestSession,
    bool FirstSeenAfterWindowStart);

/// <summary>
/// One failure, published raw so a reader can see what the signature was derived from.
/// </summary>
/// <param name="SessionId">The run it happened in.</param>
/// <param name="StartedAt">When that run started.</param>
/// <param name="Sha">The commit it ran at, or null when none was recorded.</param>
/// <param name="AttemptNumber">Which attempt this was.</param>
/// <param name="DurationMs">How long it took.</param>
/// <param name="ExceptionType">The type the adapter recorded.</param>
/// <param name="ErrorMessage">The message as recorded, elided to the budget.</param>
/// <param name="StackTrace">The extracted frames — not the raw blob, which is mostly runner noise.</param>
/// <param name="SignatureHash">Which signature this failure carries.</param>
internal sealed record FailureExemplar(
    string SessionId,
    DateTime StartedAt,
    string? Sha,
    int AttemptNumber,
    long DurationMs,
    string? ExceptionType,
    string? ErrorMessage,
    IReadOnlyList<string> StackTrace,
    string SignatureHash);

/// <summary>
/// One execution of the same test that did not fail.
/// </summary>
/// <remarks>
/// The pair is what makes a flaky test reasonable about. A page of failures says the test is broken;
/// a page of failures next to a pass from the same window says it is not.
/// </remarks>
/// <param name="SessionId">The run it happened in.</param>
/// <param name="StartedAt">When that run started.</param>
/// <param name="Sha">The commit it ran at, or null when none was recorded.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="DurationMs">How long it took.</param>
internal sealed record ContrastExecution(
    string SessionId,
    DateTime StartedAt,
    string? Sha,
    string Outcome,
    long DurationMs);

/// <summary>
/// One test inside a shared-failure cluster.
/// </summary>
/// <param name="Fingerprint">Its stable identity.</param>
/// <param name="FullyQualifiedName">Namespace, class and method.</param>
/// <param name="Failures">Failures of this test carrying the cluster's signature.</param>
internal sealed record ClusterMember(string Fingerprint, string FullyQualifiedName, int Failures);

/// <summary>
/// Evidence that a test fails sometimes, or fails in more than one way.
/// </summary>
/// <param name="Failures">Failures counted, after any discounting.</param>
/// <param name="Executions">Executions they were counted against.</param>
/// <param name="Sessions">Sessions in the window.</param>
/// <param name="SessionsWithFailures">Sessions this test failed in.</param>
/// <param name="FailureRate"><paramref name="Failures"/> over <paramref name="Executions"/>.</param>
/// <param name="DiscountedExecutions">
/// Executions left out of the counts above: those from environmental sessions, and those belonging
/// to a shared-failure cluster reported separately.
/// </param>
/// <param name="DistinctSignatureCount">Distinct ways it failed.</param>
/// <param name="DistinctSignatures">Those ways, most frequent first.</param>
/// <param name="Exemplars">Up to three failures, raw.</param>
/// <param name="Contrast">One execution that did not fail, or null when it never passed.</param>
internal sealed record FlakyEvidence(
    int Failures,
    int Executions,
    int Sessions,
    int SessionsWithFailures,
    double FailureRate,
    int DiscountedExecutions,
    int DistinctSignatureCount,
    IReadOnlyList<SignatureView> DistinctSignatures,
    IReadOnlyList<FailureExemplar> Exemplars,
    ContrastExecution? Contrast) : FindingEvidence;

/// <summary>
/// Evidence that a test fails almost every time, always the same way.
/// </summary>
/// <param name="Failures">Failures counted, after any discounting.</param>
/// <param name="Executions">Executions they were counted against.</param>
/// <param name="Sessions">Sessions in the window.</param>
/// <param name="SessionsWithFailures">Sessions this test failed in.</param>
/// <param name="FailureRate"><paramref name="Failures"/> over <paramref name="Executions"/>.</param>
/// <param name="DiscountedExecutions">Executions left out of the counts above.</param>
/// <param name="Signature">The single way it fails.</param>
/// <param name="Exemplars">Up to three failures, raw.</param>
/// <param name="Contrast">The execution that did not fail, when there was one.</param>
internal sealed record AlwaysFailingEvidence(
    int Failures,
    int Executions,
    int Sessions,
    int SessionsWithFailures,
    double FailureRate,
    int DiscountedExecutions,
    SignatureView Signature,
    IReadOnlyList<FailureExemplar> Exemplars,
    ContrastExecution? Contrast) : FindingEvidence;

/// <summary>
/// Evidence that several tests fail the same way.
/// </summary>
/// <param name="Signature">The way they fail.</param>
/// <param name="MemberCount">Distinct tests carrying it.</param>
/// <param name="Members">Those tests, in ordinal order, with their failure counts.</param>
/// <param name="Failures">Failures across all members.</param>
/// <param name="SessionsAffected">Sessions in which at least one member failed this way.</param>
/// <param name="Sessions">Sessions in the window.</param>
/// <param name="MaxTestsInOneSession">The most members it hit within a single session.</param>
/// <param name="LastSeenAt">Start of the newest session it appeared in.</param>
/// <param name="LastSeenSha">Commit that session ran at, when one was recorded.</param>
/// <param name="Exemplars">Up to three failures, raw, from different members where possible.</param>
internal sealed record SharedFailureEvidence(
    SignatureView Signature,
    int MemberCount,
    IReadOnlyList<ClusterMember> Members,
    int Failures,
    int SessionsAffected,
    int Sessions,
    int MaxTestsInOneSession,
    DateTime LastSeenAt,
    string? LastSeenSha,
    IReadOnlyList<FailureExemplar> Exemplars) : FindingEvidence;

/// <summary>
/// Reports how tests fail: sometimes, always, or together with other tests.
/// </summary>
/// <remarks>
/// <para>
/// One provider owns all three kinds because they are one judgement. Whether a failing test is flaky
/// or simply broken cannot be decided without knowing how many distinct ways it fails, and neither
/// can be decided until the failures that belong to a shared cluster have been taken out — otherwise
/// forty tests knocked over by one broken dependency are reported as forty broken tests. Splitting
/// this across providers would let each claim the same failures, and providers may not consult each
/// other.
/// </para>
/// <para>
/// Every classification here is a statement about which side of a published threshold a measurement
/// fell on. None of them is a claim about cause: the report says three distinct failure signatures
/// were observed, never that a test shares static state. Naming a cause anchors the reader — human
/// or model — onto a guess and stops the investigation there.
/// </para>
/// </remarks>
internal sealed class FailureModeProvider : IFindingProvider
{
    // Three, per the output contract's exemplar budget. A per-provider constant rather than a shared
    // threshold: the specification's constant table does not list it, and adding an entry there
    // would be a threshold this session invented.
    private const int MaxExemplars = 3;

    /// <inheritdoc/>
    public string Name => "failure-mode";

    /// <inheritdoc/>
    public IReadOnlyList<FindingKind> Kinds =>
        [FindingKind.Flaky, FindingKind.AlwaysFailing, FindingKind.SharedFailure];

    /// <inheritdoc/>
    public IEnumerable<FindingCandidate> Analyze(AnalysisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<SignatureGroup> clusters = FindClusters(context);

        var clustered = new HashSet<string>(
            clusters.Select(c => c.Signature.Hash), StringComparer.Ordinal);

        foreach (SignatureGroup cluster in clusters)
            yield return SharedFailure(context, cluster);

        foreach (string fingerprint in context.Tests.Fingerprints)
        {
            FindingCandidate? candidate = Individual(context, fingerprint, clustered);
            if (candidate != null)
                yield return candidate;
        }
    }

    /// <summary>
    /// Finds the signatures that knocked over enough tests at once to be worth reporting as one.
    /// </summary>
    /// <remarks>
    /// Environmental sessions are deliberately kept. A session where the environment fell over is
    /// exactly where a shared cause shows itself; discounting it here would suppress the one finding
    /// that explains the rest of the report.
    /// </remarks>
    private static List<SignatureGroup> FindClusters(AnalysisContext context)
    {
        List<SignatureGroup> clusters = [];

        foreach (string hash in context.Signatures.Hashes)
        {
            SignatureGroup? group = context.Signatures.GroupFor(hash);
            if (group == null)
                continue;

            // A signature the adapter could not build is keyed on the test it came from, so it can
            // never reach the threshold. Skipped explicitly all the same, because relying on that
            // would make a change to the sentinel silently produce a suite-wide false cluster.
            if (group.Signature.Unavailable)
                continue;

            if (group.MaxTestsInOneSession < LocalAnalysisConstants.SharedFailureMinTests)
                continue;

            clusters.Add(group);
        }

        // Widest first. Ties fall back to the hash, which is total and stable, so two runs over one
        // store emit the same clusters in the same order.
        clusters.Sort((left, right) =>
        {
            int byMembers = right.Fingerprints.Count.CompareTo(left.Fingerprints.Count);
            return byMembers != 0
                ? byMembers
                : string.CompareOrdinal(left.Signature.Hash, right.Signature.Hash);
        });

        return clusters;
    }

    private static FindingCandidate SharedFailure(AnalysisContext context, SignatureGroup cluster)
    {
        var members = new List<ClusterMember>(cluster.Fingerprints.Count);
        var references = new List<TestReference>(cluster.Fingerprints.Count);

        double unreliability = 0;

        foreach (string fingerprint in cluster.Fingerprints)
        {
            TestReference? reference = context.Tests.ReferenceFor(fingerprint);
            if (reference == null)
                continue;

            references.Add(reference);

            int failures = cluster.Failures.Count(
                f => string.Equals(
                    f.Execution.Identity.TestFingerprint, fingerprint, StringComparison.Ordinal));

            members.Add(new ClusterMember(fingerprint, reference.FullyQualifiedName, failures));

            // The cluster takes its worst member's unreliability rather than their average. One
            // member failing constantly makes the whole cluster worth opening, and an average would
            // let a dozen occasional members hide it.
            int executions = context.Tests.ExecutionsOf(fingerprint).Count;
            if (executions > 0)
                unreliability = Math.Max(unreliability, (double)failures / executions);
        }

        string groupId = string.Create(CultureInfo.InvariantCulture, $"sig_{cluster.Signature.Hash}");

        return new FindingCandidate(
            FindingKind.SharedFailure,
            new FindingSubject.Group(groupId, references),
            BuildSharedEvidence(context, cluster, members),
            unreliability,
            cluster.NewestSessionIndex,
            DrillDown.ForGroup(FindingKind.SharedFailure, references.Count > 0 ? references[0].Assembly : null));
    }

    private static SharedFailureEvidence BuildSharedEvidence(
        AnalysisContext context, SignatureGroup cluster, List<ClusterMember> members)
    {
        // Newest first, and one per member before any member gets a second, so three exemplars
        // describe three tests rather than three runs of the same one.
        List<ExecutionRef> exemplars = Spread(
            cluster.Failures, f => f.Execution.Identity.TestFingerprint);

        return new SharedFailureEvidence(
            ToView(context, cluster.Signature, cluster.Failures.Count,
                cluster.OldestSessionIndex, cluster.FirstSeenAt, cluster.FirstSeenSha),
            members.Count,
            members,
            cluster.Failures.Count,
            cluster.SessionCount,
            context.Window.SessionCount,
            cluster.MaxTestsInOneSession,
            cluster.Failures[0].Session.StartedAt,
            RevisionContext.ReadSha(cluster.Failures[0].Session),
            [.. exemplars.Select(e => ToExemplar(context, e))]);
    }

    /// <summary>
    /// Classifies one test's own failures, once the shared and environmental ones are set aside.
    /// </summary>
    private static FindingCandidate? Individual(
        AnalysisContext context, string fingerprint, HashSet<string> clustered)
    {
        IReadOnlyList<ExecutionRef> all = context.Tests.ExecutionsOf(fingerprint);

        List<ExecutionRef> considered = [];
        int discounted = 0;

        foreach (ExecutionRef reference in all)
        {
            if (IsDiscounted(context, reference, clustered))
                discounted++;
            else
                considered.Add(reference);
        }

        if (considered.Count == 0)
            return null;

        List<ExecutionRef> failures = [.. considered.Where(e => e.Failed)];
        if (failures.Count == 0)
            return null;

        TestReference? test = context.Tests.ReferenceFor(fingerprint);
        if (test == null)
            return null;

        double failureRate = (double)failures.Count / considered.Count;

        List<SignatureView> signatures = DistinctSignatures(context, fingerprint, failures);

        int sessionsWithFailures = failures.Select(f => f.Session.SessionId).Distinct().Count();
        List<FailureExemplar> exemplars = [.. Spread(failures, SignatureHashOf(context))
            .Select(e => ToExemplar(context, e))];

        ContrastExecution? contrast = Contrast(considered);
        int sessionsSinceLast = failures.Min(f => f.SessionIndex);

        // A single failure mode occurring on almost every run is a broken test, not a flaky one, and
        // is reported apart because the remedy is entirely different — and because leaving it in the
        // flaky bucket is how a real regression gets ignored. Both arms are classifications against
        // the published threshold; neither says anything about why the test fails.
        if (signatures.Count == 1 && failureRate >= LocalAnalysisConstants.AlwaysFailingRate)
        {
            return new FindingCandidate(
                FindingKind.AlwaysFailing,
                new FindingSubject.SingleTest(test),
                new AlwaysFailingEvidence(
                    failures.Count,
                    considered.Count,
                    context.Window.SessionCount,
                    sessionsWithFailures,
                    FindingOrder.Round(failureRate),
                    discounted,
                    signatures[0],
                    exemplars,
                    contrast),
                failureRate,
                sessionsSinceLast,
                DrillDown.ForTest(FindingKind.AlwaysFailing, test));
        }

        // Everything else that failed at all. Either the failure mode varies between runs, or one
        // mode occurs inconsistently — two observations that a developer investigates the same way
        // and that the specification names alike.
        return new FindingCandidate(
            FindingKind.Flaky,
            new FindingSubject.SingleTest(test),
            new FlakyEvidence(
                failures.Count,
                considered.Count,
                context.Window.SessionCount,
                sessionsWithFailures,
                FindingOrder.Round(failureRate),
                discounted,
                signatures.Count,
                signatures,
                exemplars,
                contrast),

            // Peaks at a failure rate of one half, which is the most disruptive thing a test can do:
            // it neither passes nor fails, so nobody can act on either result.
            1 - Math.Abs((2 * failureRate) - 1),

            sessionsSinceLast,
            DrillDown.ForTest(FindingKind.Flaky, test));
    }

    /// <summary>
    /// Returns whether an execution is left out of a test's own failure rate.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two exclusions, both removing the execution from the numerator and the denominator together.
    /// Removing it from only the numerator would understate the rate instead of setting the run
    /// aside, which is a subtler way of being wrong.
    /// </para>
    /// <para>
    /// A session where a third of the suite failed says something about the machine, not about this
    /// test. A failure belonging to a shared cluster is already reported once, against the cluster;
    /// counting it again here is what turns one cause into forty findings.
    /// </para>
    /// </remarks>
    private static bool IsDiscounted(
        AnalysisContext context, ExecutionRef reference, HashSet<string> clustered)
    {
        if (context.SessionViewFor(reference.Session.SessionId)?.IsLikelyEnvironmental == true)
            return true;

        if (!reference.Failed)
            return false;

        FailureSignature? signature = context.Signatures.Of(reference);

        return signature != null && clustered.Contains(signature.Hash);
    }

    private static List<SignatureView> DistinctSignatures(
        AnalysisContext context, string fingerprint, List<ExecutionRef> failures)
    {
        // Counted over the failures that survived discounting rather than read straight off the
        // index, so a test whose only varying failures were clustered is not still called varying.
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (ExecutionRef reference in failures)
        {
            FailureSignature? signature = context.Signatures.Of(reference);
            if (signature == null)
                continue;

            counts[signature.Hash] = counts.TryGetValue(signature.Hash, out int count) ? count + 1 : 1;
        }

        List<SignatureView> views = [];

        foreach (SignatureOccurrence occurrence in context.Signatures.SignaturesOf(fingerprint))
        {
            if (!counts.TryGetValue(occurrence.Signature.Hash, out int occurrences))
                continue;

            views.Add(ToView(
                context,
                occurrence.Signature,
                occurrences,
                occurrence.OldestSessionIndex,
                occurrence.FirstSeenAt,
                occurrence.FirstSeenSha));
        }

        return views;
    }

    private static SignatureView ToView(
        AnalysisContext context,
        FailureSignature signature,
        int occurrences,
        int oldestSessionIndex,
        DateTime firstSeenAt,
        string? firstSeenSha) =>
        new(
            signature.Hash,
            signature.ExceptionType,
            signature.NormalisedMessage,
            signature.Frames,
            signature.Degraded,
            signature.Unavailable,
            occurrences,
            firstSeenAt,
            firstSeenSha,
            oldestSessionIndex,

            // Only ever seen in the newest run.
            oldestSessionIndex == 0,

            // Absent from the oldest analysed session, so it arrived while the window was open. Not
            // a claim that it has never happened before — analysis cannot see past its own boundary,
            // and saying otherwise would be the report inventing history it does not have.
            oldestSessionIndex < context.Window.SessionCount - 1);

    private static Func<ExecutionRef, string> SignatureHashOf(AnalysisContext context) =>
        reference => context.Signatures.Of(reference)?.Hash ?? string.Empty;

    /// <summary>
    /// Picks up to three failures, newest first, covering as many distinct values of a key as
    /// possible before repeating one.
    /// </summary>
    /// <remarks>
    /// Three exemplars of the same thing answer a question nobody asked. Spread across signatures,
    /// they show what "fails in three ways" actually looks like; spread across cluster members, they
    /// show that the same failure really is the same.
    /// </remarks>
    private static List<ExecutionRef> Spread(
        IReadOnlyList<ExecutionRef> failures, Func<ExecutionRef, string> key)
    {
        List<ExecutionRef> ordered = [.. failures
            .OrderBy(f => f.SessionIndex)
            .ThenBy(f => f.Execution.Retry?.AttemptNumber ?? 1)
            .ThenBy(f => f.Execution.Identity.TestFingerprint, StringComparer.Ordinal)
            .ThenBy(f => f.Execution.ExecutionId.ToString("N", CultureInfo.InvariantCulture),
                StringComparer.Ordinal)];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        List<ExecutionRef> chosen = [];

        foreach (ExecutionRef reference in ordered)
        {
            if (chosen.Count == MaxExemplars)
                break;

            if (seen.Add(key(reference)))
                chosen.Add(reference);
        }

        // Then top up in the same order, so a test with one failure mode still gets three exemplars.
        foreach (ExecutionRef reference in ordered)
        {
            if (chosen.Count == MaxExemplars)
                break;

            if (!chosen.Contains(reference))
                chosen.Add(reference);
        }

        return chosen;
    }

    private static FailureExemplar ToExemplar(AnalysisContext context, ExecutionRef reference)
    {
        FailureSignature? signature = context.Signatures.Of(reference);

        return new FailureExemplar(
            reference.Session.SessionId.ToString("D", CultureInfo.InvariantCulture),
            reference.Session.StartedAt,
            RevisionContext.ReadSha(reference.Session),
            reference.Execution.Retry?.AttemptNumber ?? 1,
            (long)reference.Execution.Duration.TotalMilliseconds,
            reference.Execution.ExceptionType,
            EvidenceText.Elide(reference.Execution.ErrorMessage),

            // The extracted frames, not the raw trace. The rest is runner scaffolding that pushes
            // the one useful line out of view.
            signature?.Frames ?? [],

            signature?.Hash ?? string.Empty);
    }

    /// <summary>
    /// Picks the most recent execution of the same test that did not fail.
    /// </summary>
    private static ContrastExecution? Contrast(IReadOnlyList<ExecutionRef> considered)
    {
        ExecutionRef? passing = considered
            .Where(e => e.Execution.Outcome == TestOutcome.Passed)
            .OrderBy(e => e.SessionIndex)
            .ThenBy(e => e.Execution.ExecutionId.ToString("N", CultureInfo.InvariantCulture),
                StringComparer.Ordinal)
            .FirstOrDefault();

        // Absent rather than null-filled when the test never passed in the window. A finding never
        // carries an empty field for analysis it could not do — a consumer cannot tell that apart
        // from "looked, found nothing".
        return passing == null
            ? null
            : new ContrastExecution(
                passing.Session.SessionId.ToString("D", CultureInfo.InvariantCulture),
                passing.Session.StartedAt,
                RevisionContext.ReadSha(passing.Session),
                passing.Execution.Outcome.ToString(),
                (long)passing.Execution.Duration.TotalMilliseconds);
    }
}
