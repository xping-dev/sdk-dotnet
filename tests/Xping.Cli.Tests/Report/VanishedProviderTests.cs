/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Sdk.Core.Models;

namespace Xping.Cli.Tests.Report;

public sealed class VanishedProviderTests
{
    /// <summary>
    /// Builds sessions in which the newest ones stop running a test.
    /// </summary>
    /// <param name="total">Sessions to build.</param>
    /// <param name="presentIn">How many of the oldest sessions include the vanishing test.</param>
    private static AnalysisContext Context(int total, int presentIn)
    {
        var sessions = new List<TestSession>();

        for (int i = 0; i < total; i++)
        {
            sessions.Add(i < presentIn
                ? TestSessionFactory.Session(i, "Stable", "Removed")
                : TestSessionFactory.Session(i, "Stable"));
        }

        return TestSessionFactory.Context([.. sessions]);
    }

    private static IReadOnlyList<FindingCandidate> Analyze(AnalysisContext context) =>
        [.. new VanishedProvider().Analyze(context)];

    [Fact]
    public void ATestThatStopsRunningIsReported()
    {
        // Eight sessions gives a current slice of three; the test ran in the oldest five.
        IReadOnlyList<FindingCandidate> candidates = Analyze(Context(total: 8, presentIn: 5));

        FindingCandidate candidate = Assert.Single(candidates);
        Assert.Equal(FindingKind.Vanished, candidate.Kind);

        var subject = Assert.IsType<FindingSubject.SingleTest>(candidate.Subject);
        Assert.Equal("fp-Removed", subject.Test.TestFingerprint);
    }

    [Fact]
    public void EvidenceCarriesTheDenominatorsBehindTheClaim()
    {
        IReadOnlyList<FindingCandidate> candidates = Analyze(Context(total: 8, presentIn: 5));

        var evidence = Assert.IsType<VanishedEvidence>(Assert.Single(candidates).Evidence);

        Assert.Equal(5, evidence.BaselineSessions);
        Assert.Equal(5, evidence.BaselineSessionCount);
        Assert.Equal(3, evidence.CurrentSessionCount);
        Assert.Equal(5, evidence.Executions);
    }

    [Fact]
    public void EvidenceCarriesTheRunRateAndThePValueBehindTheClaim()
    {
        // Both, and not just the counts. "ran in 12 of 17 earlier runs" is the same sentence whether
        // the test was a habit or an occasional visitor, and the p-value is what tells them apart.
        // Five baseline appearances of five, none in the current three: one deal in fifty-six.
        var evidence = Assert.IsType<VanishedEvidence>(
            Assert.Single(Analyze(Context(total: 8, presentIn: 5))).Evidence);

        Assert.Equal(1.0, evidence.BaselineRunRate);
        Assert.Equal(0.0179, evidence.PValue);

        // Twelve of seventeen on a default window, which is where the bar actually sits.
        var wider = Assert.IsType<VanishedEvidence>(
            Assert.Single(Analyze(Context(total: 20, presentIn: 12))).Evidence);

        Assert.Equal(0.706, wider.BaselineRunRate);
        Assert.Equal(0.0491, wider.PValue);
    }

    [Theory]
    [InlineData(3, false)]      // 3 of 17: more likely than not to miss the last three anyway
    [InlineData(8, false)]      // 8 of 17: p 0.19, still the sort of thing that happens
    [InlineData(12, true)]      // 12 of 17: p 0.049, the first table on this window that carries
    [InlineData(17, true)]      // ran in every one of them and then stopped
    public void AbsenceIsReportedOnlyWhereItWasNotTheLikelyThingToHappen(int presentIn, bool reported)
    {
        // The defect this gate replaced was a count: three appearances qualified whether they were
        // three sessions out of three or three out of seventeen. On a default twenty-session window
        // the second is absent from the current slice 56% of the time with nothing having changed.
        IReadOnlyList<FindingCandidate> candidates = Analyze(Context(total: 20, presentIn));

        Assert.Equal(reported ? 1 : 0, candidates.Count);
    }

    [Fact]
    public void TheCandidateHandsThePValueToTheCoordinatorUnrounded()
    {
        // The number #160's Benjamini-Hochberg pass sorts on. The evidence publishes a copy rounded
        // to three significant digits; this one must not be it.
        FindingCandidate candidate = Assert.Single(Analyze(Context(total: 20, presentIn: 12)));

        Assert.NotNull(candidate.PValue);
        Assert.Equal(0.0491228, candidate.PValue!.Value, 7);
    }

    [Fact]
    public void AWindowTooShortForAThreeSessionSliceReportsNothingOfThisKind()
    {
        // Below SmallWindowSessionCount the current slice narrows to one session, and one session's
        // absence is not evidence that anything stopped: six baseline appearances of six missing a
        // single run is one deal in seven. Deliberate, and the reason the kind is silent on a short
        // history rather than loud on it.
        Assert.Empty(Analyze(Context(total: 7, presentIn: 6)));
    }

    [Fact]
    public void ATestStillRunningIsNotReported()
    {
        IReadOnlyList<FindingCandidate> candidates = Analyze(Context(total: 8, presentIn: 8));

        Assert.Empty(candidates);
    }

    [Fact]
    public void ATestSeenTooFewTimesToHaveBeenEstablishedIsNotReported()
    {
        // Two baseline appearances is below the minimum: a test seen once or twice and never again
        // was probably never really there, and calling that a change would be noise. The p-value gate
        // refuses it as well — two appearances split that way is better than one deal in three —
        // which is why the minimum is a guard rather than a decision.
        IReadOnlyList<FindingCandidate> candidates = Analyze(Context(total: 8, presentIn: 2));

        Assert.Empty(candidates);
    }

    [Fact]
    public void AWindowWithNoBaselineProducesNothing()
    {
        // With one session everything is in the current slice, so no test can be absent from it.
        AnalysisContext context = TestSessionFactory.Context(
            TestSessionFactory.Session(0, "Stable", "Removed"));

        Assert.Empty(Analyze(context));
    }

    [Fact]
    public void UnreliabilityRisesWithHowEstablishedTheTestWas()
    {
        // Ran in every baseline session, then stopped — a starker change than one that missed a run
        // here and there before it went. Both clear the gate; this is only the ranking between them.
        FindingCandidate everySession = Assert.Single(Analyze(Context(total: 8, presentIn: 5)));
        FindingCandidate someSessions = Assert.Single(Analyze(Context(total: 10, presentIn: 6)));

        // Bounded rather than taken raw, so five of five is 0.57 and not the certainty a ratio of
        // one would claim. The comparison the finding rests on is unaffected.
        Assert.Equal(0.566, everySession.Unreliability, 3);
        Assert.Equal(0.487, someSessions.Unreliability, 3);
    }

    [Fact]
    public void CandidatesComeOutInFingerprintOrder()
    {
        var sessions = new List<TestSession>();
        for (int i = 0; i < 8; i++)
        {
            sessions.Add(i < 5
                ? TestSessionFactory.Session(i, "Stable", "Zulu", "Alpha", "Mike")
                : TestSessionFactory.Session(i, "Stable"));
        }

        IReadOnlyList<FindingCandidate> candidates =
            Analyze(TestSessionFactory.Context([.. sessions]));

        string[] fingerprints =
            [.. candidates.Select(c => ((FindingSubject.SingleTest)c.Subject).Test.TestFingerprint)];

        Assert.Equal(fingerprints.OrderBy(f => f, StringComparer.Ordinal), fingerprints);
    }

    [Fact]
    public void VanishedIsReportedQuietlyEvenWhenItScoresHighly()
    {
        // A test that ran in every session and then stopped scores highly on every term the generic
        // impact formula measures. It is still usually a deliberate deletion, so it must not sort
        // above a genuinely failing test.
        var coordinator = new FindingCoordinator([new VanishedProvider()]);

        using var warnings = new StringWriter();
        Finding finding = Assert.Single(
            coordinator.Run(Context(total: 8, presentIn: 5), null, warnings).Findings);

        Assert.Equal(Severity.Low, finding.Severity);
        Assert.True(finding.Impact > LocalAnalysisConstants.SeverityMediumThreshold);
    }

    [Theory]
    [InlineData(5, false)]      // p 0.083 — declined here, though the coordinator would have taken it
    [InlineData(6, true)]       // p 0.033
    public void ThePValueGateBindsBeforeEitherSessionFloor(int presentIn, bool reported)
    {
        // Which of the three gates actually decides, pinned so that the constants' remarks cannot
        // quietly stop being true. Five baseline appearances of seven is exactly
        // MinimumSessionsPerTestToReport and comfortably above VanishedMinBaselineSessions, so both
        // floors would admit it; the provider declines it anyway, because five appearances landing in
        // seven baseline runs and none in the current three is one deal in twelve. No appearance
        // count below five clears the p-value gate at any baseline size, so neither floor can ever
        // be the binding one.
        var coordinator = new FindingCoordinator([new VanishedProvider()]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(total: 10, presentIn), null, warnings);

        Assert.Equal(reported ? 1 : 0, result.Findings.Count);

        // And it is the provider that decided, not the floor applied after it.
        Assert.Equal(reported ? 1 : 0, Analyze(Context(total: 10, presentIn)).Count);
    }

    [Fact]
    public void TheProviderReachesTheReportEndToEnd()
    {
        var coordinator = new FindingCoordinator([new VanishedProvider()]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(total: 8, presentIn: 5), null, warnings);

        Finding finding = Assert.Single(result.Findings);

        Assert.Equal(FindingKind.Vanished, finding.Kind);
        Assert.Empty(result.FailedProviders);
        Assert.StartsWith("f_", finding.Id, StringComparison.Ordinal);
        Assert.Contains("--kind Vanished", finding.DrillDownCommand, StringComparison.Ordinal);
    }
}
