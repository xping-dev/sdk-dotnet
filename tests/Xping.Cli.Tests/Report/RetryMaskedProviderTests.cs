/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

public sealed class RetryMaskedProviderTests
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
        [.. new RetryMaskedProvider().Analyze(context)];

    private static RetryMaskedEvidence EvidenceFrom(AnalysisContext context) =>
        Assert.IsType<RetryMaskedEvidence>(Assert.Single(Analyze(context)).Evidence);

    private static AnalysisResult Run(AnalysisContext context)
    {
        using var warnings = new StringWriter();
        return new FindingCoordinator([new RetryMaskedProvider()]).Run(context, null, warnings);
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
    public void AFailureRetriedUnsuccessfullyLeavesTheTestUnreported()
    {
        // Attempt one fails, attempt two fails as well: the session's final outcome for this test is
        // a failure, so nothing was masked.
        AnalysisContext context = WithSubjectExecutions(
            sessions: 6,
            _ =>
            [
                TestSessionFactory.Execution(
                    Subject, TestOutcome.Failed, attempt: 1, retryAttributeName: RetryAttribute),
                TestSessionFactory.Execution(
                    Subject, TestOutcome.Failed, attempt: 2, retryAttributeName: RetryAttribute)
            ]);

        Assert.Empty(Analyze(context));
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

        Assert.Equal(RetryAttribute, evidence.RetryAttributeName);
        Assert.Equal(ConfiguredRetries, evidence.MaxRetriesConfigured);
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

        Assert.Null(evidence.RetryAttributeName);
        Assert.Equal(0, evidence.MaxRetriesConfigured);
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
    public void UnreliabilityIsTheShareOfRunsThatNeededARetry()
    {
        FindingCandidate candidate = Assert.Single(Analyze(Context(sessions: 6, maskedSessions: 3)));

        Assert.Equal(0.5, candidate.Unreliability);
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
    public void TheSessionFloorDecidesWhetherAFindingSurvives(int sessions, bool reported)
    {
        // Three masked sessions gives six executions throughout, so only the session count moves.
        AnalysisResult result = Run(Context(sessions, maskedSessions: 3));

        Assert.Equal(reported ? 1 : 0, result.Findings.Count);
        Assert.Equal(reported ? 0 : 1, result.ExcludedLowEvidence);
    }

    [Theory]
    [InlineData(2, 0, 4, false)]
    [InlineData(2, 1, 5, true)]
    [InlineData(3, 0, 6, true)]
    public void TheExecutionFloorDecidesWhetherAFindingSurvives(
        int maskedSessions, int padding, int expectedExecutions, bool reported)
    {
        AnalysisContext context = Context(sessions: 6, maskedSessions, padding);

        Assert.Equal(expectedExecutions, EvidenceFrom(context).Executions);

        AnalysisResult result = Run(context);

        Assert.Equal(reported ? 1 : 0, result.Findings.Count);
        Assert.Equal(reported ? 0 : 1, result.ExcludedLowEvidence);
    }

    [Theory]
    [InlineData(7, 0, 14, "Low")]
    [InlineData(7, 1, 15, "Moderate")]
    [InlineData(8, 0, 16, "Moderate")]
    [InlineData(20, 0, 40, "Moderate")]
    [InlineData(20, 1, 41, "High")]
    public void EvidenceIsBandedByExecutionsOfTheSubject(
        int maskedSessions, int padding, int expectedExecutions, string expected)
    {
        AnalysisContext context = Context(sessions: 24, maskedSessions, padding);

        Assert.Equal(expectedExecutions, EvidenceFrom(context).Executions);
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
