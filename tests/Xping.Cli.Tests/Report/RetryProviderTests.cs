/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Indexes;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

public sealed class RetryProviderTests
{
    private const string Subject = "Masked";
    private const string RetryAttribute = "Retry";
    private const int ConfiguredRetries = 2;

    /// <summary>
    /// Builds the two executions NUnit records when a test fails and passes on retry.
    /// </summary>
    /// <param name="failedMs">Duration of the attempt that failed.</param>
    /// <param name="passedMs">Duration of the attempt that passed.</param>
    /// <param name="errorMessage">What the failed attempt reported.</param>
    private static IEnumerable<TestExecution> MaskedPair(
        int failedMs = 40, int passedMs = 60, string? errorMessage = "Expected 3 but was 2") =>
    [
        TestSessionFactory.Execution(
            Subject,
            TestOutcome.Failed,
            durationMs: failedMs,
            attempt: 1,
            maxRetries: ConfiguredRetries,
            retryAttributeName: RetryAttribute,
            errorMessage: errorMessage),

        TestSessionFactory.Execution(
            Subject,
            TestOutcome.Passed,
            durationMs: passedMs,
            attempt: 2,
            passedOnRetry: true,
            maxRetries: ConfiguredRetries,
            retryAttributeName: RetryAttribute)
    ];

    /// <summary>
    /// Builds a window in which the newest sessions mask a failure behind a retry.
    /// </summary>
    /// <param name="sessions">Sessions to build.</param>
    /// <param name="maskedSessions">How many of the newest sessions retry the subject.</param>
    /// <param name="padding">
    /// Extra plain passes of the subject, one per session, filling from the oldest. Used to move the
    /// execution count across a threshold without changing how often it was masked.
    /// </param>
    /// <param name="sha">Commit every session records, or null for none.</param>
    private static AnalysisContext Context(
        int sessions, int maskedSessions, int padding = 0, string? sha = null)
    {
        var built = new List<TestSession>();
        int remainingPadding = padding;

        for (int ordinal = 0; ordinal < sessions; ordinal++)
        {
            // Ordinal zero is the oldest session, so masking sits in the newest ones.
            bool masked = ordinal >= sessions - maskedSessions;

            var executions = new List<TestExecution> { TestSessionFactory.Execution("Stable") };

            if (masked)
            {
                executions.AddRange(MaskedPair());
            }
            else if (remainingPadding > 0)
            {
                executions.Add(TestSessionFactory.Execution(Subject));
                remainingPadding--;
            }

            built.Add(TestSessionFactory.Session(ordinal, executions, sha: sha));
        }

        return TestSessionFactory.Context([.. built]);
    }

    private static IReadOnlyList<FindingCandidate> Analyze(AnalysisContext context) =>
        [.. new RetryProvider().Analyze(context)];

    private static RetryMaskedEvidence EvidenceFrom(AnalysisContext context) =>
        Assert.IsType<RetryMaskedEvidence>(Assert.Single(Analyze(context)).Evidence);

    private static AnalysisResult Run(AnalysisContext context)
    {
        using var warnings = new StringWriter();
        return new FindingCoordinator([new RetryProvider()]).Run(context, null, warnings);
    }

    // ---------------------------------------------------------------------------------------
    // The condition
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestThatPassesOnRetryIsReported()
    {
        FindingCandidate candidate = Assert.Single(Analyze(Context(sessions: 6, maskedSessions: 3)));

        Assert.Equal(FindingKind.RetryMasked, candidate.Kind);

        var subject = Assert.IsType<FindingSubject.SingleTest>(candidate.Subject);
        Assert.Equal($"fp-{Subject}", subject.Test.TestFingerprint);
    }

    [Fact]
    public void ATestThatNeverRetriedIsNotReported()
    {
        Assert.Empty(Analyze(Context(sessions: 6, maskedSessions: 0, padding: 6)));
    }

    [Fact]
    public void APassOnTheFirstAttemptIsNotMasking()
    {
        // Attempt one carries retry metadata whenever the attribute is present, so the attempt
        // number alone has to be what distinguishes a retry from an ordinary run.
        AnalysisContext context = WithSubjectExecutions(
            sessions: 6,
            _ => [TestSessionFactory.Execution(
                Subject, TestOutcome.Passed, attempt: 1, maxRetries: ConfiguredRetries,
                retryAttributeName: RetryAttribute)]);

        Assert.Empty(Analyze(context));
    }

    [Fact]
    public void ALaterAttemptThatDidNotFollowAFailureIsNotMasking()
    {
        // Attempt above one but PassedOnRetry false: whatever produced the repeat, the SDK is not
        // claiming a failure was hidden, and this finding may not claim it either.
        AnalysisContext context = WithSubjectExecutions(
            sessions: 6,
            _ => [TestSessionFactory.Execution(
                Subject, TestOutcome.Passed, attempt: 2, passedOnRetry: false,
                maxRetries: ConfiguredRetries, retryAttributeName: RetryAttribute)]);

        Assert.Empty(Analyze(context));
    }

    [Fact]
    public void ExecutionsWithoutRetryMetadataAreNotMasking()
    {
        // The common case in a real store: no retry attribute, so the adapter attaches nothing.
        AnalysisContext context = WithSubjectExecutions(
            sessions: 6,
            _ => [TestSessionFactory.Execution(Subject, retry: false)]);

        Assert.Empty(Analyze(context));
    }

    [Fact]
    public void ATestThatAlsoFailedABuildIsNotReported()
    {
        // Masked in the newest sessions, but the oldest one ended with it failing outright. It is
        // already costing someone their afternoon, so it is not hidden.
        var built = new List<TestSession>
        {
            TestSessionFactory.Session(0, [TestSessionFactory.Execution(Subject, TestOutcome.Failed)])
        };

        for (int ordinal = 1; ordinal < 6; ordinal++)
            built.Add(TestSessionFactory.Session(ordinal, [.. MaskedPair()]));

        Assert.Empty(Analyze(TestSessionFactory.Context([.. built])));
    }

    [Fact]
    public void AFailureRetriedUnsuccessfullyIsNotMaskedButExhausted()
    {
        // Attempt one fails, attempt two fails as well: the session's final outcome for this test is
        // a failure, so nothing was masked. It is the other retry finding — the retries were spent
        // and the build went red anyway — and the provider must say so rather than fall silent.
        AnalysisContext context = WithSubjectExecutions(
            sessions: 6,
            _ =>
            [
                TestSessionFactory.Execution(
                    Subject, TestOutcome.Failed, attempt: 1, retryAttributeName: RetryAttribute),
                TestSessionFactory.Execution(
                    Subject, TestOutcome.Failed, attempt: 2, retryAttributeName: RetryAttribute)
            ]);

        FindingCandidate candidate = Assert.Single(Analyze(context));

        Assert.Equal(FindingKind.RetryExhausted, candidate.Kind);
    }

    [Fact]
    public void MaskingInOneSessionIsEnoughToReport()
    {
        // No history is required beyond the reporting floor: one run that retried is one observation.
        RetryMaskedEvidence evidence = EvidenceFrom(Context(sessions: 6, maskedSessions: 1, padding: 3));

        Assert.Equal(1, evidence.MaskedOccurrences);
        Assert.Equal(1, evidence.SessionsWithMasking);
    }

    // ---------------------------------------------------------------------------------------
    // Evidence
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void EvidenceCarriesTheDenominatorsBehindTheRate()
    {
        RetryMaskedEvidence evidence = EvidenceFrom(Context(sessions: 6, maskedSessions: 3));

        Assert.Equal(3, evidence.MaskedOccurrences);
        Assert.Equal(6, evidence.Executions);
        Assert.Equal(6, evidence.Sessions);
        Assert.Equal(3, evidence.SessionsWithMasking);
        Assert.Equal(0.5, evidence.MaskedRate);
    }

    [Fact]
    public void TheRateIsRoundedToThePublishedPrecision()
    {
        // Two masked occurrences in seven executions is 0.2857…, which must not reach the output at
        // full width — the report publishes rates to three decimals.
        RetryMaskedEvidence evidence = EvidenceFrom(Context(sessions: 6, maskedSessions: 2, padding: 3));

        Assert.Equal(0.286, evidence.MaskedRate);
    }

    [Fact]
    public void TheRetryConfigurationIsReportedAsRecorded()
    {
        RetryMaskedEvidence evidence = EvidenceFrom(Context(sessions: 6, maskedSessions: 3));

        Assert.Equal(RetryAttribute, evidence.Configuration.AttributeName);
        Assert.Equal(ConfiguredRetries, evidence.Configuration.MaxRetriesAsDeclared);
        Assert.Equal(2, evidence.MaxAttemptObserved);
    }

    [Fact]
    public void AnUnnamedRetryMechanismIsNullRatherThanEmpty()
    {
        AnalysisContext context = WithSubjectExecutions(
            sessions: 6,
            _ =>
            [
                TestSessionFactory.Execution(Subject, TestOutcome.Failed, attempt: 1),
                TestSessionFactory.Execution(Subject, attempt: 2, passedOnRetry: true)
            ]);

        RetryMaskedEvidence evidence = EvidenceFrom(context);

        Assert.Null(evidence.Configuration.AttributeName);
        Assert.Equal(0, evidence.Configuration.MaxRetriesAsDeclared);
    }

    [Fact]
    public void RetryWallClockCountsOnlyAttemptsAfterTheFirst()
    {
        // Three masked sessions, each spending 60ms on the attempt that passed. The 40ms first
        // attempts are the failures, not the retries.
        RetryMaskedEvidence evidence = EvidenceFrom(Context(sessions: 6, maskedSessions: 3));

        Assert.Equal(180, evidence.RetryWallClockMs);
    }

    [Fact]
    public void MaskingIsDatedFromTheMostRecentOccurrence()
    {
        AnalysisContext context = Context(sessions: 6, maskedSessions: 3, sha: "a3f9c2e");

        RetryMaskedEvidence evidence = EvidenceFrom(context);

        // Ordinal five is the newest session the fixture builds.
        Assert.Equal(TestSessionFactory.Epoch.AddMinutes(5), evidence.LastMaskedAt);
        Assert.Equal("a3f9c2e", evidence.LastMaskedSha);
    }

    [Fact]
    public void AMissingCommitIsNullRatherThanFabricated()
    {
        RetryMaskedEvidence evidence = EvidenceFrom(Context(sessions: 6, maskedSessions: 3));

        Assert.Null(evidence.LastMaskedSha);
        Assert.All(evidence.Exemplars, exemplar => Assert.Null(exemplar.Sha));
    }

    [Fact]
    public void ExemplarsAreCappedAtThreeAndOrderedNewestFirst()
    {
        RetryMaskedEvidence evidence = EvidenceFrom(Context(sessions: 8, maskedSessions: 5));

        Assert.Equal(5, evidence.MaskedOccurrences);
        Assert.Equal(3, evidence.Exemplars.Count);

        // Sessions seven, six and five: the three most recent of the five that masked.
        Assert.Equal(
            [
                TestSessionFactory.SessionIdFor(7).ToString("D"),
                TestSessionFactory.SessionIdFor(6).ToString("D"),
                TestSessionFactory.SessionIdFor(5).ToString("D")
            ],
            evidence.Exemplars.Select(e => e.SessionId));
    }

    [Fact]
    public void AnExemplarCarriesWhatTheRetryHid()
    {
        RetryMaskedEvidence evidence = EvidenceFrom(Context(sessions: 6, maskedSessions: 3));

        RetryMaskedExemplar exemplar = evidence.Exemplars[0];

        Assert.Equal(2, exemplar.AttemptNumber);
        Assert.Equal(1, exemplar.FailedAttempts);
        Assert.Equal(60, exemplar.DurationMs);
        Assert.Equal("Expected 3 but was 2", exemplar.ErrorMessage);
    }

    [Fact]
    public void AnErrorMessageExactlyAtTheBudgetIsNotElided()
    {
        string message = new('x', LocalAnalysisConstants.ExemplarCharBudget);

        RetryMaskedEvidence evidence = EvidenceFrom(WithSubjectExecutions(
            sessions: 6, _ => MaskedPair(errorMessage: message)));

        Assert.Equal(message, evidence.Exemplars[0].ErrorMessage);
    }

    [Fact]
    public void AnErrorMessageOverTheBudgetIsElidedWithAMarker()
    {
        string message = new('x', LocalAnalysisConstants.ExemplarCharBudget + 1);

        RetryMaskedEvidence evidence = EvidenceFrom(WithSubjectExecutions(
            sessions: 6, _ => MaskedPair(errorMessage: message)));

        string? published = evidence.Exemplars[0].ErrorMessage;

        Assert.NotNull(published);
        Assert.StartsWith(new string('x', LocalAnalysisConstants.ExemplarCharBudget), published, StringComparison.Ordinal);
        Assert.EndsWith("(truncated)", published, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAbsentErrorMessageIsNullRatherThanEmpty()
    {
        RetryMaskedEvidence evidence = EvidenceFrom(WithSubjectExecutions(
            sessions: 6, _ => MaskedPair(errorMessage: null)));

        Assert.Null(evidence.Exemplars[0].ErrorMessage);
    }

    // ---------------------------------------------------------------------------------------
    // Scoring inputs
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void UnreliabilityIsTheBoundedShareOfRunsThatNeededARetry()
    {
        // Three masked of six executions is a share of one half; the term is the lower bound of
        // that share, because this kind reports on a single occurrence and a single occurrence
        // would otherwise rank as a certainty.
        FindingCandidate candidate = Assert.Single(Analyze(Context(sessions: 6, maskedSessions: 3)));

        Assert.Equal(0.188, candidate.Unreliability, 3);
    }

    [Fact]
    public void RecencyIsMeasuredFromTheNewestMasking()
    {
        // Masked in the three oldest of eight sessions, so the last occurrence is five sessions back.
        var built = new List<TestSession>();
        for (int ordinal = 0; ordinal < 8; ordinal++)
        {
            built.Add(ordinal < 3
                ? TestSessionFactory.Session(ordinal, [.. MaskedPair()])
                : TestSessionFactory.Session(ordinal, [TestSessionFactory.Execution(Subject)]));
        }

        FindingCandidate candidate = Assert.Single(Analyze(TestSessionFactory.Context([.. built])));

        Assert.Equal(5, candidate.SessionsSinceLastOccurrence);
    }

    [Fact]
    public void TheKindDeclaresNoSeverityCeiling()
    {
        // Unlike a vanished test, masking is never capped: a hidden failure is worth the top of the
        // report when the impact score puts it there.
        FindingCandidate candidate = Assert.Single(Analyze(Context(sessions: 6, maskedSessions: 3)));

        Assert.Null(candidate.SeverityCeiling);
    }

    [Fact]
    public void TheDrillDownNamesTheKindAndAssembly()
    {
        FindingCandidate candidate = Assert.Single(Analyze(Context(sessions: 6, maskedSessions: 3)));

        Assert.Equal(
            $"xping report --kind RetryMasked --format json --assembly {TestSessionFactory.DefaultAssembly}",
            candidate.DrillDownCommand);
    }

    // ---------------------------------------------------------------------------------------
    // Thresholds, at and either side of the boundary
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void TheWindowSessionFloorDecidesWhetherAFindingSurvives(int sessions, bool reported)
    {
        // The subject runs in every session, so it clears the per-test floor wherever the window
        // does and only the window's own session count moves.
        AnalysisResult result = Run(Context(sessions, maskedSessions: 3, padding: sessions - 3));

        Assert.Equal(reported ? 1 : 0, result.Findings.Count);
        Assert.Equal(reported ? 0 : 1, result.ExcludedLowEvidence);
    }

    [Theory]
    [InlineData(2, 0, 2, 4, false)]
    [InlineData(2, 1, 3, 5, false)]
    [InlineData(4, 0, 4, 8, false)]
    [InlineData(4, 1, 5, 9, true)]
    [InlineData(5, 0, 5, 10, true)]
    public void ThePerTestFloorCountsSessionsRatherThanAttempts(
        int maskedSessions, int padding, int expectedSessions, int expectedExecutions, bool reported)
    {
        // A masked session contributes two executions of the subject and a padded one contributes
        // one, so the two denominators come apart. Five executions from two masked sessions and a
        // pad used to clear a floor of five; it is one retried afternoon, and it no longer does.
        AnalysisContext context = Context(sessions: 8, maskedSessions, padding);

        Assert.Equal(expectedExecutions, EvidenceFrom(context).Executions);
        Assert.Equal(expectedSessions, context.Tests.SessionsRunIn($"fp-{Subject}"));

        AnalysisResult result = Run(context);

        Assert.Equal(reported ? 1 : 0, result.Findings.Count);
        Assert.Equal(reported ? 0 : 1, result.ExcludedLowEvidence);
    }

    [Theory]
    [InlineData(7, 0, 7, "Low")]
    [InlineData(7, 1, 8, "Moderate")]
    [InlineData(8, 0, 8, "Moderate")]
    [InlineData(15, 0, 15, "Moderate")]
    [InlineData(15, 1, 16, "High")]
    public void EvidenceIsBandedBySessionsOfTheSubject(
        int maskedSessions, int padding, int expectedSessions, string expected)
    {
        // Sessions, not executions: seven masked sessions are fourteen executions, and banding those
        // would call one week of a twice-retrying test better evidenced than a fortnight of a clean
        // one.
        AnalysisContext context = Context(sessions: 24, maskedSessions, padding);

        Assert.Equal(expectedSessions, context.Tests.SessionsRunIn($"fp-{Subject}"));
        Assert.Equal(expected, Assert.Single(Run(context).Findings).EvidenceLevel.ToString());
    }

    // ---------------------------------------------------------------------------------------
    // Edge cases
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AnEmptyWindowProducesNothing()
    {
        AnalysisWindow window = AnalysisWindow.Create(
            [], DateTime.UnixEpoch, DateTime.UnixEpoch, WindowResolution.Default, null);

        Assert.Empty(Analyze(new AnalysisContext(window, null)));
    }

    [Fact]
    public void ASingleSessionIsExcludedRatherThanReported()
    {
        // A report over one session is still a valid report; what it may not do is call a test
        // unreliable on the strength of it.
        AnalysisResult result = Run(Context(sessions: 1, maskedSessions: 1));

        Assert.Empty(result.Findings);
        Assert.Equal(1, result.ExcludedLowEvidence);
    }

    [Fact]
    public void ATestAbsentFromTheCurrentSliceIsStillEligible()
    {
        // Masking is a property of the whole window, not a delta between its halves, so a test that
        // has stopped running keeps the observations it earned.
        var built = new List<TestSession>();
        for (int ordinal = 0; ordinal < 8; ordinal++)
        {
            built.Add(ordinal < 5
                ? TestSessionFactory.Session(ordinal, [.. MaskedPair()])
                : TestSessionFactory.Session(ordinal, [TestSessionFactory.Execution("Stable")]));
        }

        FindingCandidate candidate = Assert.Single(Analyze(TestSessionFactory.Context([.. built])));

        Assert.Equal(FindingKind.RetryMasked, candidate.Kind);
    }

    [Fact]
    public void ATestNewInTheCurrentSliceIsEligibleOnItsOwnExecutions()
    {
        var built = new List<TestSession>();
        for (int ordinal = 0; ordinal < 8; ordinal++)
        {
            built.Add(ordinal >= 5
                ? TestSessionFactory.Session(ordinal, [.. MaskedPair()])
                : TestSessionFactory.Session(ordinal, [TestSessionFactory.Execution("Stable")]));
        }

        RetryMaskedEvidence evidence = EvidenceFrom(TestSessionFactory.Context([.. built]));

        Assert.Equal(3, evidence.MaskedOccurrences);
        Assert.Equal(6, evidence.Executions);
        Assert.Equal(8, evidence.Sessions);
    }

    [Fact]
    public void RepeatedExecutionsAtTheSameAttemptDoNotContradictTheSharedIndex()
    {
        // An adapter that re-runs a whole test without incrementing the attempt number produces two
        // attempt-one executions. The last of them decides the session's outcome, here and in the
        // shared index alike, so the two can never disagree about whether the session failed.
        AnalysisContext context = WithSubjectExecutions(
            sessions: 6,
            ordinal =>
            [
                TestSessionFactory.Execution(
                    Subject, TestOutcome.Failed, attempt: 1, retryAttributeName: RetryAttribute,
                    executionId: TestSessionFactory.ExecutionIdFor($"{Subject}-first", ordinal, TestOutcome.Failed)),
                TestSessionFactory.Execution(
                    Subject, TestOutcome.Passed, attempt: 1, retryAttributeName: RetryAttribute),
                TestSessionFactory.Execution(
                    Subject, TestOutcome.Passed, attempt: 2, passedOnRetry: true,
                    retryAttributeName: RetryAttribute)
            ]);

        RetryMaskedEvidence evidence = EvidenceFrom(context);

        Assert.Equal(6, evidence.MaskedOccurrences);
        Assert.Equal(18, evidence.Executions);
    }

    // ---------------------------------------------------------------------------------------
    // Determinism
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TwoRunsOverTheSameWindowProduceByteIdenticalJson()
    {
        AnalysisContext context = Context(sessions: 8, maskedSessions: 5, sha: "a3f9c2e");

        Assert.Equal(Serialize(context), Serialize(context));
    }

    [Fact]
    public void TwoIdenticallyBuiltWindowsProduceByteIdenticalJson()
    {
        // Rebuilt from scratch rather than reused, so anything leaking in from allocation order or
        // dictionary enumeration would show up here and not in the run-twice case.
        string first = Serialize(Context(sessions: 8, maskedSessions: 5, sha: "a3f9c2e"));
        string second = Serialize(Context(sessions: 8, maskedSessions: 5, sha: "a3f9c2e"));

        Assert.Equal(first, second);
    }

    private static string Serialize(AnalysisContext context)
    {
        ReportEnvelope envelope = EnvelopeBuilder.Build(
            context, Run(context), incompleteSessions: 0, unreadableSessions: 0, top: null);

        return JsonSerializer.Serialize(envelope, ReportJsonOptions.Default);
    }

    // ===========================================================================================
    // Out of retries
    // ===========================================================================================

    // ---------------------------------------------------------------------------------------
    // The condition
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ARunWhoseLastAttemptFailedAfterAnEarlierAttemptIsReported()
    {
        FindingCandidate candidate = Assert.Single(Analyze(Retrying(sessions: 8, exhausted: 4)));

        Assert.Equal(FindingKind.RetryExhausted, candidate.Kind);

        var subject = Assert.IsType<FindingSubject.SingleTest>(candidate.Subject);
        Assert.Equal($"fp-{Subject}", subject.Test.TestFingerprint);
    }

    [Fact]
    public void ARunThatFailedOnItsOnlyAttemptIsNotExhaustion()
    {
        // A retry attribute is present and the test failed, but nothing was ever retried. Reporting
        // that as retries running out would claim a budget was spent that never was.
        AnalysisContext context = WithSubjectExecutions(
            sessions: 8,
            _ => [TestSessionFactory.Execution(
                Subject, TestOutcome.Failed, attempt: 1, maxRetries: ConfiguredRetries,
                retryAttributeName: RetryAttribute)]);

        Assert.Empty(Analyze(context));
    }

    [Fact]
    public void AnAttemptRecordedAboveOneWithNothingBeneathItIsNotExhaustion()
    {
        // The adapter lost the earlier attempts, or a retry helper published an attempt number for a
        // loop the SDK never saw. Either way the run holds no evidence that anything was retried,
        // and the condition says "at least one earlier attempt exists" for exactly this reason.
        AnalysisContext context = WithSubjectExecutions(
            sessions: 8,
            _ => [TestSessionFactory.Execution(
                Subject, TestOutcome.Failed, attempt: 3, maxRetries: ConfiguredRetries,
                retryAttributeName: RetryAttribute)]);

        Assert.Empty(Analyze(context));
    }

    [Fact]
    public void ATestWhoseRetriesAlwaysRescueItIsNotExhausted()
    {
        // Every retried run settled green. The retry attribute is doing its job, and the test is
        // masked rather than out of retries.
        FindingCandidate candidate = Assert.Single(
            Analyze(Retrying(sessions: 8, exhausted: 0, rescued: 5)));

        Assert.Equal(FindingKind.RetryMasked, candidate.Kind);
    }

    [Theory]
    [InlineData(0, 3, nameof(FindingKind.RetryMasked))]
    [InlineData(2, 4, null)]
    [InlineData(3, 3, null)]
    [InlineData(4, 0, nameof(FindingKind.RetryExhausted))]
    [InlineData(7, 1, nameof(FindingKind.RetryExhausted))]
    public void TheShareOfRetriedRunsThatGaveUpDecidesTheKind(
        int exhausted, int rescued, string? kind)
    {
        // The half the condition asks for is asked of the share's lower bound, so the denominator
        // decides as much as the ratio does. Two of six is 0.33 and falls short on the ratio alone;
        // three of six is exactly the bar and still falls short, because six runs cannot carry it.
        // Four of four and seven of eight are the two smallest shapes that can. Below the bar the
        // retries rescue the test more often than not, and the test is neither masked - it failed
        // builds - nor out of retries.
        //
        // Named as a string because FindingKind is internal: a public theory parameter may not be
        // less accessible than the method carrying it.
        IReadOnlyList<FindingCandidate> candidates =
            Analyze(Retrying(sessions: 10, exhausted, rescued));

        if (kind == null)
            Assert.Empty(candidates);
        else
            Assert.Equal(kind, Assert.Single(candidates).Kind.ToString());
    }

    [Fact]
    public void OneExhaustedRunIsAnIncidentRatherThanAPattern()
    {
        // The whole of the test's retrying gave up, so the share is 1.0 - and it happened once.
        Assert.Empty(Analyze(Retrying(sessions: 8, exhausted: 1)));
    }

    [Fact]
    public void TwoExhaustedRunsInTwoAreNotEnoughToReport()
    {
        // Both gates the count knows about are cleared - two exhausted runs, a share of 1.00 - and
        // the finding is still declined, because a denominator of two cannot carry a claim about
        // whether retries rescue this test. The Wilson lower bound of two in two is 0.34.
        Assert.Empty(Analyze(Retrying(sessions: 8, exhausted: 2)));
    }

    [Fact]
    public void FourExhaustedRunsInFourAreTheSmallestReportableShape()
    {
        // Bound 0.51, the first configuration to clear the half the threshold asks for.
        FindingCandidate candidate = Assert.Single(Analyze(Retrying(sessions: 8, exhausted: 4)));

        Assert.Equal(FindingKind.RetryExhausted, candidate.Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(99)]
    public void ExhaustionIsNotReadFromTheDeclaredRetryLimit(int maxRetries)
    {
        // One unchanged attempt sequence against four different declared limits. The frameworks
        // disagree about whether that number counts total attempts or retries after the first, so
        // every case has to report identically: the finding is decided on attempts observed.
        AnalysisContext context = Retrying(sessions: 8, exhausted: 4, maxRetries: maxRetries);

        RetryExhaustedEvidence evidence = ExhaustedFrom(context);

        Assert.Equal(4, evidence.ExhaustedRuns);
        Assert.Equal(2, evidence.MaxAttemptObserved);
        Assert.Equal(maxRetries, evidence.Configuration.MaxRetriesAsDeclared);
    }

    [Fact]
    public void ATimedOutFinalAttemptIsExhaustion()
    {
        // A hang is a failure to the shared index, so it has to be one here too. Deciding it
        // otherwise would let the report call a run red and this finding call the same run settled.
        AnalysisContext context = Retrying(
            sessions: 8, exhausted: 4, settled: TestOutcome.Timeout);

        Assert.Equal(4, ExhaustedFrom(context).ExhaustedRuns);
    }

    // ---------------------------------------------------------------------------------------
    // Evidence
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ExhaustionEvidenceCarriesTheDenominatorsBehindTheRate()
    {
        RetryExhaustedEvidence evidence = ExhaustedFrom(Retrying(sessions: 12, exhausted: 7, rescued: 1));

        Assert.Equal(7, evidence.ExhaustedRuns);
        Assert.Equal(8, evidence.RetriedRuns);
        Assert.Equal(1, evidence.RescuedRuns);
        Assert.Equal(12, evidence.RunsConsidered);
        Assert.Equal(12, evidence.Sessions);
    }

    [Fact]
    public void TheRateIsCountedInRunsRatherThanExecutions()
    {
        // Three attempts in each of four exhausted runs is twelve executions. An execution-
        // denominated rate would read that as a smaller share of a larger number and describe the
        // test as five times less serious than it is.
        RetryExhaustedEvidence evidence =
            ExhaustedFrom(Retrying(sessions: 10, exhausted: 4, attempts: 3));

        Assert.Equal(4, evidence.ExhaustedRuns);
        Assert.Equal(4, evidence.RetriedRuns);
        Assert.Equal(1.0, evidence.ExhaustedRate);
        Assert.Equal(8, evidence.RetryAttemptsSpent);
    }

    [Fact]
    public void TheExhaustionRateIsRoundedToThePublishedPrecision()
    {
        // Eight exhausted of nine retried is 0.8888..., which must not reach the output at full
        // width - the report publishes rates to three decimals.
        RetryExhaustedEvidence evidence =
            ExhaustedFrom(Retrying(sessions: 12, exhausted: 8, rescued: 1));

        Assert.Equal(0.889, evidence.ExhaustedRate);
    }

    [Fact]
    public void TheDeepestAttemptIsPublishedBesideTheDeclaredLimit()
    {
        // Five attempts observed against a declared limit of two. Both are published and neither is
        // derived from the other, because they were written down by different parties.
        RetryExhaustedEvidence evidence = ExhaustedFrom(
            Retrying(sessions: 8, exhausted: 4, attempts: 5, maxRetries: 2));

        Assert.Equal(5, evidence.MaxAttemptObserved);
        Assert.Equal(2, evidence.Configuration.MaxRetriesAsDeclared);
    }

    [Fact]
    public void RetryWallClockCountsOnlyAttemptsAfterTheFirstOfTheExhaustedRuns()
    {
        // Three attempts of 100ms in each of four exhausted runs. The first attempt of each is the
        // failure, not the retry, so eight attempts are counted rather than twelve.
        RetryExhaustedEvidence evidence =
            ExhaustedFrom(Retrying(sessions: 8, exhausted: 4, attempts: 3));

        Assert.Equal(800, evidence.RetryWallClockMs);
    }

    [Fact]
    public void ConfiguredWaitingIsCountedApartFromMeasuredAttemptTime()
    {
        // Whether the framework actually waited is not in the session, so the declared delay is
        // scaled by the attempts it applied to and published on its own line. Summing the two would
        // present a figure nothing measured.
        RetryExhaustedEvidence evidence = ExhaustedFrom(
            Retrying(sessions: 8, exhausted: 4, attempts: 3, retryDelayMs: 250));

        Assert.Equal(800, evidence.RetryWallClockMs);
        Assert.Equal(2000, evidence.ConfiguredDelayTotalMs);
    }

    [Fact]
    public void ARetryReasonIsPublishedVerbatim()
    {
        RetryExhaustedEvidence evidence = ExhaustedFrom(
            Retrying(sessions: 8, exhausted: 4, retryReason: "NetworkError"));

        Assert.Equal("NetworkError", evidence.Configuration.Reason);
    }

    [Fact]
    public void AnEmptyRetryReasonIsAbsentRatherThanPublished()
    {
        // What the MSTest adapter writes where the other two leave null. A blank reason is not a
        // reason, and publishing it would put an empty pair in front of a reader.
        RetryExhaustedEvidence evidence = ExhaustedFrom(
            Retrying(sessions: 8, exhausted: 4, retryReason: string.Empty));

        Assert.Null(evidence.Configuration.Reason);
    }

    [Fact]
    public void ExhaustionIsDatedFromTheMostRecentOccurrence()
    {
        AnalysisContext context = Retrying(sessions: 8, exhausted: 4, sha: "a3f9c2e");

        RetryExhaustedEvidence evidence = ExhaustedFrom(context);

        // Ordinal seven is the newest session the fixture builds.
        Assert.Equal(TestSessionFactory.Epoch.AddMinutes(7), evidence.LastExhaustedAt);
        Assert.Equal("a3f9c2e", evidence.LastExhaustedSha);
    }

    [Fact]
    public void TheContrastIsARetriedRunThatDidSettleGreen()
    {
        RetryExhaustedEvidence evidence =
            ExhaustedFrom(Retrying(sessions: 12, exhausted: 7, rescued: 1));

        Assert.NotNull(evidence.Contrast);
        Assert.Equal(nameof(TestOutcome.Passed), evidence.Contrast.Outcome);
    }

    [Fact]
    public void AContrastIsAbsentWhenTheRetriesNeverOnceWorked()
    {
        // Absent rather than null-filled: its absence is itself the reading.
        RetryExhaustedEvidence evidence = ExhaustedFrom(Retrying(sessions: 8, exhausted: 4));

        Assert.Null(evidence.Contrast);
    }

    [Fact]
    public void ExhaustedExemplarsAreCappedAtThreeAndOrderedNewestFirst()
    {
        RetryExhaustedEvidence evidence = ExhaustedFrom(Retrying(sessions: 10, exhausted: 5));

        Assert.Equal(5, evidence.ExhaustedRuns);
        Assert.Equal(3, evidence.Exemplars.Count);

        Assert.Equal(
            [
                TestSessionFactory.SessionIdFor(9).ToString("D"),
                TestSessionFactory.SessionIdFor(8).ToString("D"),
                TestSessionFactory.SessionIdFor(7).ToString("D")
            ],
            evidence.Exemplars.Select(e => e.SessionId));
    }

    [Fact]
    public void AnExhaustedExemplarCarriesWhatTheAttemptBeforeTheLastSaid()
    {
        RetryExhaustedEvidence evidence =
            ExhaustedFrom(Retrying(sessions: 8, exhausted: 4, attempts: 3));

        RetryAttemptExemplar exemplar = evidence.Exemplars[0];

        Assert.Equal(3, exemplar.Attempts);
        Assert.Equal(nameof(TestOutcome.Failed), exemplar.Outcome);
        Assert.Equal(200, exemplar.RetryWallClockMs);
        Assert.Equal("Expected 3 but was 2", exemplar.ErrorMessage);
    }

    // ---------------------------------------------------------------------------------------
    // Scoring inputs
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ExhaustionUnreliabilityIsMeasuredAgainstEveryRunOfTheTest()
    {
        // Four exhausted runs of ten, a share of 0.40 bounded at 0.17. The condition thresholds the
        // share of *retried* runs, which is a question about the retry attribute; this ranks the
        // test, which is a different question and keeps its own denominator.
        FindingCandidate candidate = Assert.Single(Analyze(Retrying(sessions: 10, exhausted: 4)));

        Assert.Equal(0.168, candidate.Unreliability, 3);
    }

    [Fact]
    public void ExhaustionRecencyIsMeasuredFromTheNewestOccurrence()
    {
        // Exhausted in the four oldest of ten sessions, so the last occurrence is six back.
        var built = new List<TestSession>();
        for (int ordinal = 0; ordinal < 10; ordinal++)
        {
            built.Add(ordinal < 4
                ? TestSessionFactory.Session(ordinal, [.. AttemptSequence(2, TestOutcome.Failed)])
                : TestSessionFactory.Session(ordinal, [TestSessionFactory.Execution(Subject)]));
        }

        FindingCandidate candidate = Assert.Single(Analyze(TestSessionFactory.Context([.. built])));

        Assert.Equal(FindingKind.RetryExhausted, candidate.Kind);
        Assert.Equal(6, candidate.SessionsSinceLastOccurrence);
    }

    [Fact]
    public void ExhaustionDeclaresNoSeverityCeiling()
    {
        // This test broke a build. Nothing here is worth capping below what the impact score says.
        FindingCandidate candidate = Assert.Single(Analyze(Retrying(sessions: 8, exhausted: 4)));

        Assert.Null(candidate.SeverityCeiling);
    }

    [Fact]
    public void TheExhaustionDrillDownNamesTheKindAndAssembly()
    {
        FindingCandidate candidate = Assert.Single(Analyze(Retrying(sessions: 8, exhausted: 4)));

        Assert.Equal(
            $"xping report --kind RetryExhausted --format json --assembly {TestSessionFactory.DefaultAssembly}",
            candidate.DrillDownCommand);
    }

    // ---------------------------------------------------------------------------------------
    // Edge cases
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AnEnvironmentalRunIsDiscountedFromExhaustionAndCounted()
    {
        // One run where the environment fell over, and its exhaustion must not count towards a claim
        // about the test.
        var built = new List<TestSession>
        {
            Outage(0)
        };

        for (int ordinal = 1; ordinal < 9; ordinal++)
        {
            built.Add(ordinal <= 4
                ? TestSessionFactory.Session(ordinal, [.. AttemptSequence(2, TestOutcome.Failed)])
                : TestSessionFactory.Session(ordinal, [TestSessionFactory.Execution(Subject)]));
        }

        RetryExhaustedEvidence evidence = ExhaustedFrom(TestSessionFactory.Context([.. built]));

        Assert.Equal(1, evidence.DiscountedRuns);
        Assert.Equal(4, evidence.ExhaustedRuns);
        Assert.Equal(8, evidence.RunsConsidered);
    }

    // ===========================================================================================
    // Deeper retries
    // ===========================================================================================

    // ---------------------------------------------------------------------------------------
    // The condition
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestThatUsedToPassFirstTimeAndNowNeedsThreeIsReported()
    {
        FindingCandidate candidate = Assert.Single(
            Analyze(Depths(sessions: 12, baselineAttempts: 1, currentAttempts: 3)));

        Assert.Equal(FindingKind.RetryDeepening, candidate.Kind);
    }

    [Fact]
    public void ATestWhoseAttemptCountHasNotMovedIsNotDeepening()
    {
        FindingCandidate candidate = Assert.Single(
            Analyze(Depths(sessions: 12, baselineAttempts: 2, currentAttempts: 2)));

        // Still masked - it passes on retry throughout - but nothing about it has changed.
        Assert.Equal(FindingKind.RetryMasked, candidate.Kind);
    }

    [Fact]
    public void ATestThatNeedsFewerAttemptsThanItDidIsNotDeepening()
    {
        FindingCandidate candidate = Assert.Single(
            Analyze(Depths(sessions: 12, baselineAttempts: 3, currentAttempts: 1)));

        Assert.Equal(FindingKind.RetryMasked, candidate.Kind);
    }

    [Fact]
    public void OnlyRunsThatPassedCountTowardsTheTypicalAttemptCount()
    {
        // The earlier runs settle red at four attempts. "Attempts to pass" is undefined for a run
        // that never passed, so those must not raise the baseline and hide the change.
        AnalysisContext context = Depths(
            sessions: 12, baselineAttempts: 1, currentAttempts: 3, redBaselineRuns: 2);

        RetryDeepeningEvidence evidence = DeepeningFrom(context);

        Assert.Equal(1, evidence.Baseline.TypicalAttempts);
        Assert.Equal(2, evidence.Baseline.RunsFailedFinally);
    }

    [Fact]
    public void ASingleDeepRunAmongTheRecentOnesDoesNotMoveTheTypicalCount()
    {
        // Two recent passing runs, one of them at three attempts and one at one. Under a nearest-rank
        // median that reads as one, so a single deep run cannot produce this finding by itself.
        AnalysisContext context = Depths(
            sessions: 12, baselineAttempts: 1, currentAttempts: 3, currentRuns: 1, shallowCurrentRuns: 1);

        FindingCandidate candidate = Assert.Single(Analyze(context));

        Assert.Equal(FindingKind.RetryMasked, candidate.Kind);
    }

    // ---------------------------------------------------------------------------------------
    // Evidence
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void DeepeningEvidenceCarriesBothArmsAndTheCountsBehindThem()
    {
        RetryDeepeningEvidence evidence =
            DeepeningFrom(Depths(sessions: 12, baselineAttempts: 1, currentAttempts: 3));

        Assert.Equal(3, evidence.Current.TypicalAttempts);
        Assert.Equal(3, evidence.Current.RunsSettledGreen);
        Assert.Equal(1, evidence.Baseline.TypicalAttempts);
        Assert.Equal(9, evidence.Baseline.RunsSettledGreen);
    }

    [Fact]
    public void TheChangeIsPublishedAbsolutelyAndRelatively()
    {
        RetryDeepeningEvidence evidence =
            DeepeningFrom(Depths(sessions: 12, baselineAttempts: 1, currentAttempts: 3));

        Assert.Equal(2, evidence.Delta.Attempts);
        Assert.Equal(200, evidence.Delta.AttemptsPct);
    }

    [Fact]
    public void TheTypicalAttemptCountIsAWholeAttemptSomeRunActuallyNeeded()
    {
        // Two recent runs at three attempts and one at one gives a nearest-rank median of one; the
        // arithmetic mean would be 2.33, which no run ever took and no reader could act on.
        AnalysisContext context = Depths(
            sessions: 12, baselineAttempts: 1, currentAttempts: 3, currentRuns: 2, shallowCurrentRuns: 1);

        RetryDeepeningEvidence evidence = DeepeningFrom(context);

        Assert.Equal(3, evidence.Current.TypicalAttempts);
        Assert.Equal(3, evidence.Current.MaxAttempts);
    }

    [Fact]
    public void TheChangeIsDatedFromTheOldestRecentRunTheTestPassedIn()
    {
        AnalysisContext context = Depths(
            sessions: 12, baselineAttempts: 1, currentAttempts: 3, sha: "a3f9c2e");

        Assert.Equal("a3f9c2e", DeepeningFrom(context).FirstSeenAt);
    }

    [Fact]
    public void TheContrastIsAnEarlierRunTypicalOfWhatAPassUsedToCost()
    {
        RetryDeepeningEvidence evidence =
            DeepeningFrom(Depths(sessions: 12, baselineAttempts: 1, currentAttempts: 3));

        Assert.NotNull(evidence.Contrast);
        Assert.Equal(1, evidence.Contrast.Attempts);
    }

    [Fact]
    public void DeepeningPublishesTheRetryConfigurationAsRecorded()
    {
        RetryDeepeningEvidence evidence = DeepeningFrom(Depths(
            sessions: 12, baselineAttempts: 1, currentAttempts: 3,
            retryReason: "NetworkError", retryDelayMs: 250));

        Assert.Equal(RetryAttribute, evidence.Configuration.AttributeName);
        Assert.Equal(ConfiguredRetries, evidence.Configuration.MaxRetriesAsDeclared);
        Assert.Equal("NetworkError", evidence.Configuration.Reason);
        Assert.Equal(250, evidence.Configuration.ConfiguredDelayMs);

        // Two attempts after the first in each of three recent runs.
        Assert.Equal(600, evidence.RetryWallClockMs);
        Assert.Equal(1500, evidence.ConfiguredDelayTotalMs);
    }

    // ---------------------------------------------------------------------------------------
    // Scoring inputs
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, 2, 1.0)]
    [InlineData(2, 3, 0.5)]
    [InlineData(2, 4, 1.0)]
    [InlineData(3, 4, 0.333)]
    public void DeepeningUnreliabilityIsTheRelativeIncreaseCappedAtADoubling(
        int baselineAttempts, int currentAttempts, double expected)
    {
        FindingCandidate candidate = Assert.Single(
            Analyze(Depths(sessions: 12, baselineAttempts, currentAttempts)));

        Assert.Equal(expected, Math.Round(candidate.Unreliability, 3));
    }

    [Fact]
    public void DeepeningIsCappedAtMediumSeverity()
    {
        // Nothing has failed a build. Left uncapped, a frequently-run test that still goes green
        // would outrank one that is failing today.
        FindingCandidate candidate = Assert.Single(
            Analyze(Depths(sessions: 12, baselineAttempts: 1, currentAttempts: 3)));

        Assert.Equal(Severity.Medium, candidate.SeverityCeiling);
    }

    [Fact]
    public void TheDeepeningDrillDownNamesTheKindAndAssembly()
    {
        FindingCandidate candidate = Assert.Single(
            Analyze(Depths(sessions: 12, baselineAttempts: 1, currentAttempts: 3)));

        Assert.Equal(
            $"xping report --kind RetryDeepening --format json --assembly {TestSessionFactory.DefaultAssembly}",
            candidate.DrillDownCommand);
    }

    // ---------------------------------------------------------------------------------------
    // Thresholds, at and either side of the boundary
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void TheEarlierRunFloorDecidesWhetherADeepeningIsMeasured(
        int baselineRuns, bool deepening)
    {
        AnalysisContext context = Depths(
            sessions: 12, baselineAttempts: 1, currentAttempts: 3, baselineRuns: baselineRuns);

        FindingCandidate candidate = Assert.Single(Analyze(context));

        Assert.Equal(
            deepening ? FindingKind.RetryDeepening : FindingKind.RetryMasked, candidate.Kind);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void TheRecentRunFloorDecidesWhetherADeepeningIsMeasured(int currentRuns, bool deepening)
    {
        AnalysisContext context = Depths(
            sessions: 12, baselineAttempts: 1, currentAttempts: 3, currentRuns: currentRuns);

        FindingCandidate candidate = Assert.Single(Analyze(context));

        Assert.Equal(
            deepening ? FindingKind.RetryDeepening : FindingKind.RetryMasked, candidate.Kind);
    }

    // ---------------------------------------------------------------------------------------
    // Edge cases
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AWindowTooSmallForAThreeRunSliceProducesNoDeepening()
    {
        // Seven sessions narrows the current slice to one, which is below the recent-run floor. A
        // window that short has no baseline worth comparing against either.
        FindingCandidate candidate = Assert.Single(
            Analyze(Depths(sessions: 7, baselineAttempts: 1, currentAttempts: 3)));

        Assert.Equal(FindingKind.RetryMasked, candidate.Kind);
    }

    [Fact]
    public void AnEnvironmentalRunIsLeftOutOfBothArmsAndCounted()
    {
        AnalysisContext context = Depths(
            sessions: 12, baselineAttempts: 1, currentAttempts: 3, outageOrdinal: 0);

        RetryDeepeningEvidence evidence = DeepeningFrom(context);

        Assert.Equal(1, evidence.DiscountedRuns);
        Assert.Equal(8, evidence.Baseline.Runs);
    }

    // ===========================================================================================
    // One judgement, one finding
    // ===========================================================================================

    [Fact]
    public void AtMostOneCandidateIsYieldedPerTest()
    {
        // A window that satisfies masking and deepening at once. The subject may appear only once.
        IReadOnlyList<FindingCandidate> candidates =
            Analyze(Depths(sessions: 12, baselineAttempts: 1, currentAttempts: 3));

        Assert.Single(candidates);
    }

    [Fact]
    public void ADeepenedTestIsNotAlsoReportedAsMasked()
    {
        AnalysisContext context = Depths(sessions: 12, baselineAttempts: 1, currentAttempts: 3);

        IReadOnlyList<ExecutionRef> executions = context.Tests.ExecutionsOf($"fp-{Subject}");

        // The window genuinely satisfies masking as well: it holds passes on an attempt above the
        // first, and no run of the subject ended red. Without the suppression both kinds would fire,
        // so asserting the single candidate below would prove nothing on its own.
        Assert.Contains(
            executions,
            e => e.Execution.Retry is { AttemptNumber: > 1, PassedOnRetry: true });
        Assert.DoesNotContain(executions, e => e.Execution.Outcome.IsFailure() && IsFinalAttempt(e));

        FindingCandidate candidate = Assert.Single(Analyze(context));

        Assert.Equal(FindingKind.RetryDeepening, candidate.Kind);
    }

    [Fact]
    public void AnExhaustedTestIsNeverAlsoMasked()
    {
        // Disjoint by construction rather than by ordering: masking excludes any test that ended a
        // run red, and every exhausted run did.
        AnalysisContext context = Retrying(sessions: 10, exhausted: 7, rescued: 1);

        FindingCandidate candidate = Assert.Single(Analyze(context));

        Assert.Equal(FindingKind.RetryExhausted, candidate.Kind);
    }

    [Fact]
    public void ATestMayBeBothOutOfRetriesAndFlaky()
    {
        // Two providers, two claims about one test, two ids. Providers may not consult each other,
        // and the kinds say different things about the same red run.
        AnalysisContext context = Retrying(sessions: 10, exhausted: 4);

        using var warnings = new StringWriter();
        AnalysisResult result = new FindingCoordinator(
            [new RetryProvider(), new FailureModeProvider()]).Run(context, null, warnings);

        Assert.Contains(result.Findings, f => f.Kind == FindingKind.RetryExhausted);
        Assert.Contains(result.Findings, f => f.Kind is FindingKind.Flaky or FindingKind.AlwaysFailing);
        Assert.Equal(result.Findings.Select(f => f.Id).Distinct().Count(), result.Findings.Count);
    }

    // ---------------------------------------------------------------------------------------
    // Determinism
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AWindowOfEveryRetryKindSerializesIdenticallyTwice()
    {
        Assert.Equal(
            Serialize(Retrying(sessions: 12, exhausted: 7, rescued: 1, sha: "a3f9c2e")),
            Serialize(Retrying(sessions: 12, exhausted: 7, rescued: 1, sha: "a3f9c2e")));

        Assert.Equal(
            Serialize(Depths(sessions: 12, baselineAttempts: 1, currentAttempts: 3, sha: "a3f9c2e")),
            Serialize(Depths(sessions: 12, baselineAttempts: 1, currentAttempts: 3, sha: "a3f9c2e")));
    }

    // ---------------------------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Returns whether no later attempt of this execution's test exists in the same run.
    /// </summary>
    private static bool IsFinalAttempt(ExecutionRef reference)
    {
        int attempt = reference.Execution.Retry?.AttemptNumber ?? 1;

        return !reference.Session.Executions.Any(e =>
            string.Equals(
                e.Identity.TestFingerprint,
                reference.Execution.Identity.TestFingerprint,
                StringComparison.Ordinal) &&
            (e.Retry?.AttemptNumber ?? 1) > attempt);
    }

    private static RetryExhaustedEvidence ExhaustedFrom(AnalysisContext context) =>
        Assert.IsType<RetryExhaustedEvidence>(Assert.Single(Analyze(context)).Evidence);

    private static RetryDeepeningEvidence DeepeningFrom(AnalysisContext context) =>
        Assert.IsType<RetryDeepeningEvidence>(Assert.Single(Analyze(context)).Evidence);

    /// <summary>
    /// Builds the executions an adapter records when one run takes several attempts to settle.
    /// </summary>
    /// <param name="attempts">Attempts the run recorded.</param>
    /// <param name="settled">How the last attempt ended; the ones before it always failed.</param>
    /// <param name="maxRetries">The limit the attribute declared, recorded verbatim.</param>
    /// <param name="retryReason">The reason the attribute declared, or null for none.</param>
    /// <param name="retryDelayMs">The delay the attribute declared between attempts.</param>
    private static IEnumerable<TestExecution> AttemptSequence(
        int attempts,
        TestOutcome settled = TestOutcome.Passed,
        int maxRetries = ConfiguredRetries,
        string? retryReason = null,
        int retryDelayMs = 0)
    {
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            bool last = attempt == attempts;
            bool passed = last && settled == TestOutcome.Passed;

            yield return TestSessionFactory.Execution(
                Subject,
                last ? settled : TestOutcome.Failed,
                durationMs: 100,
                attempt: attempt,
                passedOnRetry: passed && attempt > 1,
                maxRetries: maxRetries,
                retryAttributeName: RetryAttribute,
                retryReason: retryReason,
                retryDelayMs: retryDelayMs,
                errorMessage: passed ? null : "Expected 3 but was 2");
        }
    }

    /// <summary>
    /// Builds a session in which the environment fell over rather than the tests.
    /// </summary>
    /// <remarks>
    /// Both bounds of the environmental rule have to be cleared: enough failures, and a high enough
    /// share of the session. Ten failing tests out of ten does both.
    /// </remarks>
    private static TestSession Outage(int ordinal)
    {
        var executions = new List<TestExecution>();
        executions.AddRange(AttemptSequence(2, TestOutcome.Failed));

        for (int index = 0; index < 10; index++)
            executions.Add(TestSessionFactory.Execution($"Wide{index}", TestOutcome.Failed));

        return TestSessionFactory.Session(ordinal, executions);
    }

    /// <summary>
    /// Builds a window in which the subject retries, sometimes giving up and sometimes rescued.
    /// </summary>
    /// <param name="sessions">Sessions to build.</param>
    /// <param name="exhausted">How many of the newest sessions retry and still fail.</param>
    /// <param name="rescued">
    /// How many of the <i>oldest</i> sessions retry and pass. Deliberately the far end of the
    /// window: rescued runs sitting in the newest three would land inside the current slice and make
    /// the fixture a deepening as well, which is a different finding and not what these tests mean.
    /// </param>
    /// <param name="attempts">Attempts each retrying run records.</param>
    /// <param name="maxRetries">The limit the attribute declares, recorded verbatim.</param>
    /// <param name="retryReason">The reason the attribute declares, or null for none.</param>
    /// <param name="retryDelayMs">The delay the attribute declares between attempts.</param>
    /// <param name="settled">How the last attempt of an exhausted run ended.</param>
    /// <param name="sha">Commit every session records, or null for none.</param>
    private static AnalysisContext Retrying(
        int sessions,
        int exhausted,
        int rescued = 0,
        int attempts = 2,
        int maxRetries = ConfiguredRetries,
        string? retryReason = null,
        int retryDelayMs = 0,
        TestOutcome settled = TestOutcome.Failed,
        string? sha = null)
    {
        var built = new List<TestSession>();

        for (int ordinal = 0; ordinal < sessions; ordinal++)
        {
            int fromNewest = sessions - 1 - ordinal;

            var executions = new List<TestExecution> { TestSessionFactory.Execution("Stable") };

            if (fromNewest < exhausted)
            {
                executions.AddRange(
                    AttemptSequence(attempts, settled, maxRetries, retryReason, retryDelayMs));
            }
            else if (ordinal < rescued)
            {
                executions.AddRange(AttemptSequence(
                    attempts, TestOutcome.Passed, maxRetries, retryReason, retryDelayMs));
            }
            else
            {
                // A plain single-attempt pass, so the test has runs that neither retried nor failed.
                executions.Add(TestSessionFactory.Execution(
                    Subject, maxRetries: maxRetries, retryAttributeName: RetryAttribute));
            }

            built.Add(TestSessionFactory.Session(ordinal, executions, sha: sha));
        }

        return TestSessionFactory.Context([.. built]);
    }

    /// <summary>
    /// Builds a window in which the subject's recent runs take a different number of attempts to
    /// pass than its earlier ones.
    /// </summary>
    /// <param name="sessions">Sessions to build.</param>
    /// <param name="baselineAttempts">Attempts an earlier passing run takes.</param>
    /// <param name="currentAttempts">Attempts a recent passing run takes.</param>
    /// <param name="currentRuns">Recent sessions the subject passes in, filling from the oldest.</param>
    /// <param name="shallowCurrentRuns">
    /// Recent sessions the subject passes in on a single attempt, filling after the deep ones. Used
    /// to move the recent median without changing how many recent runs there are.
    /// </param>
    /// <param name="baselineRuns">Earlier sessions the subject passes in.</param>
    /// <param name="redBaselineRuns">Earlier sessions the subject ends red in, at four attempts.</param>
    /// <param name="outageOrdinal">A session to build as an outage, or null for none.</param>
    /// <param name="retryReason">The reason the attribute declares, or null for none.</param>
    /// <param name="retryDelayMs">The delay the attribute declares between attempts.</param>
    /// <param name="sha">Commit every session records, or null for none.</param>
    private static AnalysisContext Depths(
        int sessions,
        int baselineAttempts,
        int currentAttempts,
        int currentRuns = 3,
        int shallowCurrentRuns = 0,
        int baselineRuns = int.MaxValue,
        int redBaselineRuns = 0,
        int? outageOrdinal = null,
        string? retryReason = null,
        int retryDelayMs = 0,
        string? sha = null)
    {
        var built = new List<TestSession>();

        int deepSeen = 0;
        int shallowSeen = 0;
        int greenSeen = 0;
        int redSeen = 0;

        for (int ordinal = 0; ordinal < sessions; ordinal++)
        {
            if (ordinal == outageOrdinal)
            {
                built.Add(Outage(ordinal));
                continue;
            }

            // Mirrors AnalysisWindow.Create: the current slice narrows to a single session in a
            // window below SmallWindowSessionCount, and a fixture that kept three would put runs in
            // an arm the analysis reads as the baseline.
            int sliceSize = sessions < LocalAnalysisConstants.SmallWindowSessionCount
                ? 1
                : LocalAnalysisConstants.CurrentSliceSize;

            bool current = ordinal >= sessions - sliceSize;

            var executions = new List<TestExecution> { TestSessionFactory.Execution("Stable") };

            if (current)
            {
                if (deepSeen < currentRuns)
                {
                    executions.AddRange(AttemptSequence(
                        currentAttempts, TestOutcome.Passed, ConfiguredRetries, retryReason, retryDelayMs));
                    deepSeen++;
                }
                else if (shallowSeen < shallowCurrentRuns)
                {
                    executions.AddRange(AttemptSequence(1));
                    shallowSeen++;
                }
            }
            else if (redSeen < redBaselineRuns)
            {
                executions.AddRange(AttemptSequence(4, TestOutcome.Failed));
                redSeen++;
            }
            else if (greenSeen < baselineRuns)
            {
                executions.AddRange(AttemptSequence(baselineAttempts));
                greenSeen++;
            }

            built.Add(TestSessionFactory.Session(ordinal, executions, sha: sha));
        }

        return TestSessionFactory.Context([.. built]);
    }

    /// <summary>
    /// Builds a window where every session runs the same given executions of the subject.
    /// </summary>
    /// <param name="sessions">Sessions to build.</param>
    /// <param name="subjectExecutions">The subject's executions, given the session ordinal.</param>
    private static AnalysisContext WithSubjectExecutions(
        int sessions, Func<int, IEnumerable<TestExecution>> subjectExecutions)
    {
        var built = new List<TestSession>();

        for (int ordinal = 0; ordinal < sessions; ordinal++)
        {
            var executions = new List<TestExecution> { TestSessionFactory.Execution("Stable") };
            executions.AddRange(subjectExecutions(ordinal));

            built.Add(TestSessionFactory.Session(ordinal, executions));
        }

        return TestSessionFactory.Context([.. built]);
    }
}
