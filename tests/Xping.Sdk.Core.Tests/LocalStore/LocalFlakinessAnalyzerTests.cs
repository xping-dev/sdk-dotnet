/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Local;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Sdk.Core.Tests.LocalStore;

public sealed class LocalFlakinessAnalyzerTests
{
    private static LocalRun Run(params (string Name, string Outcome)[] tests) =>
        Run(passedOnRetry: null, tests);

    private static LocalRun Run(string? passedOnRetry, params (string Name, string Outcome)[] tests)
    {
        var records = tests.Select(t => new LocalTestRecord
        {
            Fingerprint = "fp-" + t.Name,
            Name = t.Name,
            Outcome = t.Outcome,
            DurationMs = 10,
            Attempt = string.Equals(t.Name, passedOnRetry, StringComparison.Ordinal) ? 2 : 1,
            PassedOnRetry = string.Equals(t.Name, passedOnRetry, StringComparison.Ordinal)
        }).ToList();

        return new LocalRun(new LocalRunHeader { StartedAtUtc = DateTime.UtcNow }, records);
    }

    [Fact]
    public void AnalyzeReturnsEmptyForNoRuns()
    {
        Assert.Same(LocalAnalysis.Empty, LocalFlakinessAnalyzer.Analyze([]));
    }

    [Fact]
    public void DetectsFlakinessAcrossRuns()
    {
        // Arrange — passes and fails interleaved across the window.
        var runs = new[]
        {
            Run(("A", OutcomeCodes.Passed)),
            Run(("A", OutcomeCodes.Failed)),
            Run(("A", OutcomeCodes.Passed))
        };

        // Act
        var analysis = LocalFlakinessAnalyzer.Analyze(runs);

        // Assert
        var finding = Assert.Single(analysis.UnstableTests);
        Assert.Equal(InstabilityKind.FlakyAcrossRuns, finding.Kind);
        Assert.Equal(2, finding.PassCount);
        Assert.Equal(3, finding.RunCount);
        Assert.Equal([true, false, true], finding.History);
    }

    [Fact]
    public void DetectsIntraRunFlakeOnTheVeryFirstRun()
    {
        // Arrange — this is the signal that keeps a developer's first report from being empty,
        // before any cross-run history exists.
        var runs = new[] { Run(passedOnRetry: "A", ("A", OutcomeCodes.Passed)) };

        // Act
        var analysis = LocalFlakinessAnalyzer.Analyze(runs);

        // Assert
        var finding = Assert.Single(analysis.UnstableTests);
        Assert.Equal(InstabilityKind.FlakedInRun, finding.Kind);
        Assert.Equal(2, finding.PassedOnAttempt);
        Assert.False(analysis.HasSufficientHistory);
    }

    [Fact]
    public void DetectsNewlyFailing()
    {
        // Arrange — an unbroken run of passes, then a failure now.
        var runs = new[]
        {
            Run(("A", OutcomeCodes.Passed)),
            Run(("A", OutcomeCodes.Passed)),
            Run(("A", OutcomeCodes.Passed)),
            Run(("A", OutcomeCodes.Failed))
        };

        // Act
        var analysis = LocalFlakinessAnalyzer.Analyze(runs);

        // Assert
        var finding = Assert.Single(analysis.UnstableTests);
        Assert.Equal(InstabilityKind.NewlyFailing, finding.Kind);
    }

    [Fact]
    public void SeparatesConsistentFailuresFromFlakiness()
    {
        // Arrange — never passed. This is a bug, not flakiness, and saying so is the point.
        var runs = new[]
        {
            Run(("A", OutcomeCodes.Failed)),
            Run(("A", OutcomeCodes.Failed)),
            Run(("A", OutcomeCodes.Failed))
        };

        // Act
        var analysis = LocalFlakinessAnalyzer.Analyze(runs);

        // Assert
        Assert.Empty(analysis.UnstableTests);
        var finding = Assert.Single(analysis.ConsistentFailures);
        Assert.Equal(InstabilityKind.ConsistentlyFailing, finding.Kind);
    }

    [Fact]
    public void IgnoresAlwaysPassingTests()
    {
        var runs = new[]
        {
            Run(("A", OutcomeCodes.Passed)),
            Run(("A", OutcomeCodes.Passed)),
            Run(("A", OutcomeCodes.Passed))
        };

        var analysis = LocalFlakinessAnalyzer.Analyze(runs);

        Assert.False(analysis.HasFindings);
    }

    [Fact]
    public void IgnoresSkippedAndNotExecutedOutcomes()
    {
        // Arrange — a skipped test carries no reliability signal, so alternating
        // skipped/passed must not read as flakiness.
        var runs = new[]
        {
            Run(("A", OutcomeCodes.Passed)),
            Run(("A", OutcomeCodes.Skipped)),
            Run(("A", OutcomeCodes.NotExecuted)),
            Run(("A", OutcomeCodes.Passed))
        };

        // Act
        var analysis = LocalFlakinessAnalyzer.Analyze(runs);

        // Assert
        Assert.False(analysis.HasFindings);
    }

    [Fact]
    public void RequiresMinimumHistoryBeforeCrossRunClaims()
    {
        // Arrange — two runs is not enough to call anything flaky.
        var runs = new[]
        {
            Run(("A", OutcomeCodes.Passed)),
            Run(("A", OutcomeCodes.Failed))
        };

        // Act
        var analysis = LocalFlakinessAnalyzer.Analyze(runs);

        // Assert
        Assert.False(analysis.HasFindings);
        Assert.False(analysis.HasSufficientHistory);
        Assert.Equal(2, analysis.RunsAnalysed);
    }

    [Fact]
    public void CollapsesRetryAttemptsWithinASingleRun()
    {
        // Arrange — one run containing a failed attempt 1 and a passing attempt 2 of the same
        // test must count as a single run, not as two conflicting data points.
        var records = new List<LocalTestRecord>
        {
            new() { Fingerprint = "fp", Name = "A", Outcome = OutcomeCodes.Failed, Attempt = 1 },
            new() { Fingerprint = "fp", Name = "A", Outcome = OutcomeCodes.Passed, Attempt = 2, PassedOnRetry = true }
        };
        var runs = new[] { new LocalRun(new LocalRunHeader(), records) };

        // Act
        var analysis = LocalFlakinessAnalyzer.Analyze(runs);

        // Assert
        var finding = Assert.Single(analysis.UnstableTests);
        Assert.Equal(InstabilityKind.FlakedInRun, finding.Kind);
        Assert.Single(finding.History);
    }

    [Fact]
    public void CapsTheNumberOfReportedTests()
    {
        // Arrange — more unstable tests than the report can usefully show.
        var passRun = Enumerable.Range(0, 20).Select(i => ($"T{i}", OutcomeCodes.Passed)).ToArray();
        var failRun = Enumerable.Range(0, 20).Select(i => ($"T{i}", OutcomeCodes.Failed)).ToArray();

        var runs = new[] { Run(passRun), Run(failRun), Run(passRun) };

        // Act
        var analysis = LocalFlakinessAnalyzer.Analyze(runs);

        // Assert
        Assert.Equal(LocalFlakinessAnalyzer.MaxReportedTests, analysis.UnstableTests.Count);
    }
}
