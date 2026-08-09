/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Analysis;
using Xping.Sdk.Core.Models.Local;

namespace Xping.Cli.Tests.Analysis;

public sealed class AggregateAnalyzerTests
{
    private static LocalRun Run(string assembly, string testName, bool passed, int minute) =>
        new(
            new LocalRunHeader
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Assembly = assembly,
                StartedAtUtc = new DateTime(2026, 1, 1, 0, minute, 0, DateTimeKind.Utc)
            },
            [
                new LocalTestRecord
                {
                    Fingerprint = $"{assembly}:{testName}",
                    Name = testName,
                    Outcome = passed ? OutcomeCodes.Passed : OutcomeCodes.Failed
                }
            ]);

    [Fact]
    public void ReturnsEmptyForNoRuns()
    {
        Assert.Same(LocalAnalysis.Empty, AggregateAnalyzer.Analyze([]));
    }

    [Fact]
    public void TagsEachFindingWithItsAssembly()
    {
        // Arrange
        var runs = new[]
        {
            Run("Alpha.Tests", "AlphaTest", true, 1),
            Run("Alpha.Tests", "AlphaTest", false, 2),
            Run("Alpha.Tests", "AlphaTest", true, 3),
            Run("Beta.Tests", "BetaTest", false, 4),
            Run("Beta.Tests", "BetaTest", true, 5),
            Run("Beta.Tests", "BetaTest", false, 6)
        };

        // Act
        var analysis = AggregateAnalyzer.Analyze(runs);

        // Assert
        Assert.Equal(2, analysis.AssembliesAnalysed);
        Assert.Equal(2, analysis.UnstableTests.Count);
        Assert.Contains(analysis.UnstableTests, t => t.Assembly == "Alpha.Tests");
        Assert.Contains(analysis.UnstableTests, t => t.Assembly == "Beta.Tests");
    }

    [Fact]
    public void ScopesEachTestHistoryToItsOwnAssembly()
    {
        // Arrange — six runs total, three per assembly. Pooling them would describe each test
        // against a six-run window it only appeared in half of.
        var runs = new[]
        {
            Run("Alpha.Tests", "AlphaTest", true, 1),
            Run("Beta.Tests", "BetaTest", true, 2),
            Run("Alpha.Tests", "AlphaTest", false, 3),
            Run("Beta.Tests", "BetaTest", false, 4),
            Run("Alpha.Tests", "AlphaTest", true, 5),
            Run("Beta.Tests", "BetaTest", true, 6)
        };

        // Act
        var analysis = AggregateAnalyzer.Analyze(runs);

        // Assert — every finding is measured against its own assembly's three runs.
        Assert.All(analysis.UnstableTests, t => Assert.Equal(3, t.RunCount));
        Assert.All(analysis.UnstableTests, t => Assert.Equal(3, t.History.Count));
    }

    [Fact]
    public void DoesNotFlagATestThatIsStableWithinItsOwnAssembly()
    {
        // Arrange — Alpha always passes, Beta is flaky. Alpha must not be dragged in.
        var runs = new[]
        {
            Run("Alpha.Tests", "AlphaTest", true, 1),
            Run("Alpha.Tests", "AlphaTest", true, 2),
            Run("Alpha.Tests", "AlphaTest", true, 3),
            Run("Beta.Tests", "BetaTest", true, 4),
            Run("Beta.Tests", "BetaTest", false, 5),
            Run("Beta.Tests", "BetaTest", true, 6)
        };

        // Act
        var analysis = AggregateAnalyzer.Analyze(runs);

        // Assert
        var finding = Assert.Single(analysis.UnstableTests);
        Assert.Equal("Beta.Tests", finding.Assembly);
    }

    [Fact]
    public void CountsTotalRunsAcrossAssemblies()
    {
        var runs = new[]
        {
            Run("Alpha.Tests", "A", true, 1),
            Run("Alpha.Tests", "A", true, 2),
            Run("Beta.Tests", "B", true, 3)
        };

        var analysis = AggregateAnalyzer.Analyze(runs);

        Assert.Equal(3, analysis.RunsAnalysed);
        Assert.Equal(2, analysis.AssembliesAnalysed);
    }

    [Fact]
    public void HandlesRunsWithNoRecordedAssembly()
    {
        // Arrange — older runs, or an adapter that failed to resolve a name.
        var runs = new[]
        {
            Run(null!, "A", true, 1),
            Run(null!, "A", false, 2),
            Run(null!, "A", true, 3)
        };

        // Act
        var analysis = AggregateAnalyzer.Analyze(runs);

        // Assert — grouped together, and the finding carries no misleading assembly label.
        var finding = Assert.Single(analysis.UnstableTests);
        Assert.Null(finding.Assembly);
    }
}
