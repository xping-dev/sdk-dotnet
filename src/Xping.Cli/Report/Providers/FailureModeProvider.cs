/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Scoring;
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
/// <param name="Site">
/// Where in the lifecycle it failed, as the adapter recorded it, or null when the adapter recorded
/// none. Published on every exemplar rather than only on a cluster, because a lone test whose own
/// setup is broken never reaches a cluster and would otherwise read as a broken test.
/// </param>
/// <param name="SiteMember">The lifecycle member that failed, when the framework named one.</param>
internal sealed record FailureExemplar(
    string SessionId,
    DateTime StartedAt,
    string? Sha,
    int AttemptNumber,
    long DurationMs,
    string? ExceptionType,
    string? ErrorMessage,
    IReadOnlyList<string> StackTrace,
    string SignatureHash,
    string? Site,
    string? SiteMember);

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
/// Evidence that a test fails almost every time, and mostly in one way.
/// </summary>
/// <param name="Failures">Failures counted, after any discounting.</param>
/// <param name="Executions">Executions they were counted against.</param>
/// <param name="Sessions">Sessions in the window.</param>
/// <param name="SessionsWithFailures">Sessions this test failed in.</param>
/// <param name="FailureRate"><paramref name="Failures"/> over <paramref name="Executions"/>.</param>
/// <param name="DiscountedExecutions">Executions left out of the counts above.</param>
/// <param name="Signature">The dominant way it fails.</param>
/// <param name="ModalSignatureShare">
/// The share of <paramref name="Failures"/> that failed the way <paramref name="Signature"/>
/// describes. Published rather than implied: below 1.0 the failures were not identical, and a
/// reader comparing two exemplars needs to know that before concluding the report misread them.
/// </param>
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
    double ModalSignatureShare,
    IReadOnlyList<FailureExemplar> Exemplars,
    ContrastExecution? Contrast) : FindingEvidence;

/// <summary>
/// Evidence that a test is being killed for running too long rather than failing outright.
/// </summary>
/// <param name="Timeouts">Executions that timed out, after any discounting.</param>
/// <param name="Failures">All failures counted, timeouts included.</param>
/// <param name="Executions">Executions they were counted against.</param>
/// <param name="Sessions">Sessions in the window.</param>
/// <param name="SessionsWithTimeouts">Sessions this test timed out in.</param>
/// <param name="TimeoutRate"><paramref name="Timeouts"/> over <paramref name="Executions"/>.</param>
/// <param name="TimeoutShareOfFailures"><paramref name="Timeouts"/> over <paramref name="Failures"/>.</param>
/// <param name="DiscountedExecutions">Executions left out of the counts above.</param>
/// <param name="DeclaredBudgetMs">
/// The timeout the test declared for itself, or null when it declared none — which means the limit
/// it hit came from a suite-wide or runner-level setting this report cannot see.
/// </param>
/// <param name="ObservedDurationsMs">
/// How long the timed-out runs lasted, newest first. Published beside the budget because the pair is
/// the whole point: a duration sitting on its declared ceiling is what distinguishes a test that was
/// killed from one that disagreed with an assertion.
/// </param>
/// <param name="Exemplars">Up to three timed-out runs, raw.</param>
/// <param name="Contrast">One execution that did not fail, or null when it never passed.</param>
internal sealed record TimingOutEvidence(
    int Timeouts,
    int Failures,
    int Executions,
    int Sessions,
    int SessionsWithTimeouts,
    double TimeoutRate,
    double TimeoutShareOfFailures,
    int DiscountedExecutions,
    long? DeclaredBudgetMs,
    IReadOnlyList<long> ObservedDurationsMs,
    IReadOnlyList<FailureExemplar> Exemplars,
    ContrastExecution? Contrast) : FindingEvidence;

/// <summary>
/// Evidence that one shared lifecycle member is broken, and every test that used it failed.
/// </summary>
/// <param name="Site">Where in the lifecycle it failed, as every member of the cluster recorded it.</param>
/// <param name="Member">
/// The lifecycle member the failures agree on, or null when the frameworks named none. The whole
/// point of the finding: the one place to go and fix.
/// </param>
/// <param name="Signature">The way they fail.</param>
/// <param name="TestsBlocked">Distinct tests this member took down.</param>
/// <param name="Members">Those tests, in ordinal order, with their failure counts.</param>
/// <param name="Failures">Failures across all of them.</param>
/// <param name="SessionsAffected">Sessions in which at least one of them failed this way.</param>
/// <param name="Sessions">Sessions in the window.</param>
/// <param name="MaxTestsInOneSession">The most tests it blocked within a single session.</param>
/// <param name="LastSeenAt">Start of the newest session it appeared in.</param>
/// <param name="LastSeenSha">Commit that session ran at, when one was recorded.</param>
/// <param name="Exemplars">Up to three failures, raw, from different tests where possible.</param>
internal sealed record BrokenFixtureEvidence(
    string Site,
    string? Member,
    SignatureView Signature,
    int TestsBlocked,
    IReadOnlyList<ClusterMember> Members,
    int Failures,
    int SessionsAffected,
    int Sessions,
    int MaxTestsInOneSession,
    DateTime LastSeenAt,
    string? LastSeenSha,
    IReadOnlyList<FailureExemplar> Exemplars) : FindingEvidence;

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
    [
        FindingKind.Flaky,
        FindingKind.AlwaysFailing,
        FindingKind.TimingOut,
        FindingKind.BrokenFixture,
        FindingKind.SharedFailure
    ];

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

    /// <summary>
    /// Builds the finding for one cluster, naming the lifecycle member when the failures agree on one.
    /// </summary>
    /// <remarks>
    /// Both kinds describe the same measurement and differ only in what can be said about its cause.
    /// A cluster whose every failure was recorded in the same lifecycle member is not "forty tests
    /// failing alike" — it is one broken member reported forty times, and the finding says so. A
    /// cluster that disagrees, or whose adapter recorded no site, stays a shared failure, which claims
    /// only what was measured.
    /// </remarks>
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
            // let a dozen occasional members hide it. Worst is measured on the lower bound of each
            // member's failure rate, so the member that wins is the one with the evidence behind it
            // rather than whichever happened to run fewest times.
            int executions = context.Tests.ExecutionsOf(fingerprint).Count;
            unreliability = Math.Max(unreliability, WilsonInterval.LowerBound(failures, executions));
        }

        // Unchanged by the promotion below: the id identifies the claim's subject, and the subject is
        // the same cluster whichever kind describes it. Recomputing it from the member would move
        // every finding's id the first time an adapter learned to name one.
        string groupId = string.Create(CultureInfo.InvariantCulture, $"sig_{cluster.Signature.Hash}");

        FailureSite? site = AgreedSite(cluster);
        FindingKind kind = site == null ? FindingKind.SharedFailure : FindingKind.BrokenFixture;
        string? assembly = references.Count > 0 ? references[0].Assembly : null;

        FindingEvidence evidence = site == null
            ? BuildSharedEvidence(context, cluster, members)
            : BuildBrokenFixtureEvidence(context, cluster, members, site.Value);

        return new FindingCandidate(
            kind,
            new FindingSubject.Group(groupId, references),
            evidence,
            unreliability,
            cluster.NewestSessionIndex,
            DrillDown.ForGroup(kind, assembly));
    }

    /// <summary>
    /// Returns the lifecycle site every failure in a cluster agrees on, or null when they do not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Agreement has to be total, and deliberately is not a tunable threshold. Every other constant in
    /// <see cref="LocalAnalysisConstants"/> classifies a measurement against a line; this decides
    /// whether the report may name a specific member as the defect, and a finding that points at a
    /// line of code should not be emitted on a majority vote. The member is part of the agreement, not
    /// just the site: two different broken setup methods failing with one signature are two defects,
    /// and naming either of them would send the reader to the wrong one.
    /// </para>
    /// <para>
    /// A cluster containing an execution recorded before the adapter knew about sites, or by an
    /// adapter that could not resolve one, has a null or unknown site among its failures and stays a
    /// shared failure. That is the intended behaviour rather than a gap: the report claims a cause
    /// only when every failure it is speaking for supports it.
    /// </para>
    /// </remarks>
    private static FailureSite? AgreedSite(SignatureGroup cluster)
    {
        FailureSite? site = null;
        string? member = null;
        bool first = true;

        foreach (ExecutionRef failure in cluster.Failures)
        {
            FailureSite? candidate = failure.Execution.Site;

            if (candidate == null || !candidate.Value.IsLifecycle())
                return null;

            if (first)
            {
                site = candidate;
                member = failure.Execution.FailureSiteMember;
                first = false;
                continue;
            }

            if (candidate != site ||
                !string.Equals(member, failure.Execution.FailureSiteMember, StringComparison.Ordinal))
            {
                return null;
            }
        }

        return site;
    }

    private static BrokenFixtureEvidence BuildBrokenFixtureEvidence(
        AnalysisContext context, SignatureGroup cluster, List<ClusterMember> members, FailureSite site)
    {
        List<ExecutionRef> exemplars = Spread(
            cluster.Failures, f => f.Execution.Identity.TestFingerprint);

        return new BrokenFixtureEvidence(
            site.ToString(),
            cluster.Failures[0].Execution.FailureSiteMember,
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

        // Timeouts first, because a hang is a different defect from a disagreement and the branches
        // below cannot describe it. Their evidence is built from failure signatures, and a killed
        // test leaves none worth grouping: no assertion message, and a stack frame pointing wherever
        // the runner happened to interrupt it. Classified here, the finding can instead publish the
        // one comparison that does explain it — how long the test ran against the budget it declared.
        List<ExecutionRef> timeouts =
            [.. failures.Where(e => e.Execution.Outcome == TestOutcome.Timeout)];

        // Thresholded on the lower bound of that share. Three failures of which three were kills is
        // 1.00 on a denominator of three, and moving a test into a bucket whose whole point is to
        // hand the reader a different diagnosis is not a decision three observations should make.
        // The published share stays the point estimate.
        if (timeouts.Count > 0 &&
            WilsonInterval.LowerBound(timeouts.Count, failures.Count) >=
                LocalAnalysisConstants.TimingOutShareMin)
        {
            return TimingOut(context, test, considered, failures, timeouts, discounted);
        }

        // Modal rather than sole. Failure modes are compared by exact hash over the exception type,
        // the normalised message and five frames, so one broken assertion counts as two modes as
        // soon as its message names the data that differed. Demanding a single mode let a name in an
        // error message decide the most severe classification the report makes.
        //
        // The denominator is every counted failure rather than the signatures' own total, so the
        // share stays a share of what the test did over the window even if a failure ever reaches
        // here unsigned.
        SignatureView? modal = signatures.Count > 0 ? signatures[0] : null;
        double modalShare = modal == null ? 0 : (double)modal.Occurrences / failures.Count;

        // A dominant failure mode occurring on almost every run is a broken test, not a flaky one,
        // and is reported apart because the remedy is entirely different — and because leaving it in
        // the flaky bucket is how a real regression gets ignored. Both arms are classifications
        // against the published thresholds; neither says anything about why the test fails.
        if (modal != null &&
            failureRate >= LocalAnalysisConstants.AlwaysFailingRate &&
            modalShare >= LocalAnalysisConstants.AlwaysFailingModalShareMin)
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
                    modal,
                    FindingOrder.Round(modalShare),
                    exemplars,
                    contrast),

                // The classification above is made on the rate itself; the ranking is made on its
                // lower bound. The two differ deliberately. A test failing five of five is broken
                // and the report should say so, but it should not outrank one failing forty of
                // forty, and only the bound distinguishes them.
                WilsonInterval.LowerBound(failures.Count, considered.Count),

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
            // it neither passes nor fails, so nobody can act on either result. Floored at the rate
            // itself so the term never scores a nearly-always-broken test below a milder one: the
            // tent alone put a test failing 19 runs in 20 at 0.10, which is 0.34 of impact lost for
            // being more broken, and it fell on exactly the tests that most need reading.
            //
            // Then discounted by how much of the observed rate the evidence supports, rather than
            // evaluated at the bound directly. The shape has to be applied to the rate the test
            // actually showed: the tent falls away above its peak, so feeding it a bound that climbs
            // towards the rate as runs accumulate makes the score fall as the evidence grows. A test
            // failing four of five scored 0.75 and the same test at thirty-two of forty scored 0.69,
            // which is the inversion this change exists to remove, reintroduced one line further on.
            //
            // The discount is monotone in the run count at every rate, and leaves the term at the
            // bound itself wherever the floor is what applies. An even split scores 0.47 over ten
            // executions, 0.60 over twenty and 0.70 over forty, converging on 1.00 rather than
            // claiming it.
            FlakyUnreliability(failureRate, failures.Count, considered.Count),

            sessionsSinceLast,
            DrillDown.ForTest(FindingKind.Flaky, test));
    }

    /// <summary>
    /// Scores flakiness: a tent peaking at one half, floored at the rate, discounted by evidence.
    /// </summary>
    /// <param name="rate">The observed failure rate.</param>
    /// <param name="failures">Failures the rate was computed from.</param>
    /// <param name="executions">Executions they were counted against.</param>
    /// <returns>The unreliability term, in [0,1].</returns>
    /// <remarks>
    /// The discount is the share of the observed rate its lower bound supports, which is 0 when a
    /// single failure could have been luck and approaches 1 as the runs accumulate. Applying it to
    /// the shape rather than substituting it into the shape is what keeps the term rising with the
    /// evidence: the tent is not monotone in the rate, by design, so it cannot be handed a moving
    /// estimate of one.
    /// </remarks>
    private static double FlakyUnreliability(double rate, int failures, int executions)
    {
        if (rate <= 0)
            return 0;

        double shape = Math.Max(rate, 1 - Math.Abs((2 * rate) - 1));

        return shape * (WilsonInterval.LowerBound(failures, executions) / rate);
    }

    /// <summary>
    /// Builds the finding for a test whose failures are mostly the framework killing it.
    /// </summary>
    private static FindingCandidate TimingOut(
        AnalysisContext context,
        TestReference test,
        List<ExecutionRef> considered,
        List<ExecutionRef> failures,
        List<ExecutionRef> timeouts,
        int discounted)
    {
        double timeoutRate = (double)timeouts.Count / considered.Count;

        int sessionsWithTimeouts = timeouts.Select(t => t.Session.SessionId).Distinct().Count();

        List<ExecutionRef> ordered = [.. timeouts
            .OrderBy(t => t.SessionIndex)
            .ThenBy(t => t.Execution.Retry?.AttemptNumber ?? 1)
            .ThenBy(t => t.Execution.ExecutionId.ToString("N", CultureInfo.InvariantCulture),
                StringComparer.Ordinal)];

        // Taken from the newest timed-out run rather than from any of them: a test's declared budget
        // can change between sessions, and the current one is what the reader would find in the
        // source today. Absent when the test declares none — the limit it hit then came from a
        // suite-wide or runner-level setting the session record does not carry, and inventing a
        // number for it would be worse than saying nothing.
        long? declaredBudgetMs = ordered[0].Execution.TimeoutBudget is TimeSpan budget
            ? (long)budget.TotalMilliseconds
            : null;

        return new FindingCandidate(
            FindingKind.TimingOut,
            new FindingSubject.SingleTest(test),
            new TimingOutEvidence(
                timeouts.Count,
                failures.Count,
                considered.Count,
                context.Window.SessionCount,
                sessionsWithTimeouts,
                FindingOrder.Round(timeoutRate),
                FindingOrder.Round((double)timeouts.Count / failures.Count),
                discounted,
                declaredBudgetMs,
                [.. ordered.Select(t => (long)t.Execution.Duration.TotalMilliseconds)],
                [.. ordered.Take(MaxExemplars).Select(t => ToExemplar(context, t))],
                Contrast(considered)),

            // The share of every run that ended in a kill, bounded below — not the share of failures
            // that were kills, which is what the condition thresholds. The condition asks which
            // diagnosis fits; this asks how unreliable the test is, which is what the report ranks
            // on, and the bound is what keeps a test killed twice in five behind one killed twenty
            // times in forty.
            WilsonInterval.LowerBound(timeouts.Count, considered.Count),

            timeouts.Min(t => t.SessionIndex),
            DrillDown.ForTest(FindingKind.TimingOut, test));
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

        // Re-sorted on the counts above. The index orders a test's signatures by how often each was
        // seen across the whole window, which is not the same order once environmental sessions and
        // clustered failures have been taken out — and the head of this list is read as the failure
        // mode this test meets most often, both by the reader and by the classification above it.
        // OrderByDescending is stable, so signatures tied on count keep the index's own tie-break
        // and the published evidence still does not shuffle between runs.
        return [.. views.OrderByDescending(v => v.Occurrences)];
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

            signature?.Hash ?? string.Empty,
            reference.Execution.Site?.ToString(),
            reference.Execution.FailureSiteMember);
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
