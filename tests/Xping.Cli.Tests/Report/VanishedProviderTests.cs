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
    public void ATestStillRunningIsNotReported()
    {
        IReadOnlyList<FindingCandidate> candidates = Analyze(Context(total: 8, presentIn: 8));

        Assert.Empty(candidates);
    }

    [Fact]
    public void ATestSeenTooFewTimesToHaveBeenEstablishedIsNotReported()
    {
        // Two baseline appearances is below the minimum: a test seen once or twice and never again
        // was probably never really there, and calling that a change would be noise.
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
        // Ran in every baseline session, then stopped — a starker change than one that ran in only
        // some of them.
        FindingCandidate everySession = Assert.Single(Analyze(Context(total: 8, presentIn: 5)));
        FindingCandidate someSessions = Assert.Single(Analyze(Context(total: 10, presentIn: 4)));

        // Bounded rather than taken raw, so five of five is 0.57 and not the certainty a ratio of
        // one would claim. The comparison the finding rests on is unaffected.
        Assert.Equal(0.566, everySession.Unreliability, 3);
        Assert.True(someSessions.Unreliability < everySession.Unreliability);
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
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    public void ThePerTestSessionFloorDominatesTheProvidersOwnBaselineGate(int presentIn, bool reported)
    {
        // VanishedMinBaselineSessions is 3, but a vanished test is absent from the current slice by
        // definition, so the sessions it ran in are exactly its baseline appearances and the
        // coordinator's floor of five decides. The provider still gates at three — it must not
        // depend on a floor applied elsewhere — and this pins which of the two actually binds, so
        // the constant's remark cannot quietly stop being true.
        var coordinator = new FindingCoordinator([new VanishedProvider()]);

        using var warnings = new StringWriter();
        AnalysisResult result = coordinator.Run(Context(total: 10, presentIn), null, warnings);

        Assert.Equal(reported ? 1 : 0, result.Findings.Count);

        // The provider offered the candidate in every case; the floor is what removed it.
        Assert.Single(Analyze(Context(total: 10, presentIn)));
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
