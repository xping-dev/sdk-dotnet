/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Analysis;
using Xping.Cli.Commands;
using Xping.Sdk.Core.Models.Local;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Commands;

// Resolves the store from a temp directory and mutates XPING_NO_BANNER.
[Collection("Sequential")]
public sealed class ReviewFixesTests : IDisposable
{
    private readonly string _root;

    public ReviewFixesTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-reviewfix-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("XPING_LOCAL_STORE", _root);
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XPING_LOCAL_STORE", null);
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, null);

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static void Seed(
        string assembly, DateTime start, int count, bool connected = false, bool passing = true)
    {
        ILocalRunStore store = LocalRunStore.Create();

        for (int i = 0; i < count; i++)
        {
            store.Write(new LocalRun(
                new LocalRunHeader
                {
                    SessionId = Guid.NewGuid().ToString("N"),
                    StartedAtUtc = start.AddMinutes(i),
                    Assembly = assembly,
                    IsConnected = connected
                },
                [
                    new LocalTestRecord
                    {
                        Fingerprint = $"fp-{assembly}",
                        Name = $"{assembly}.Test",
                        // Alternate so the suite reads as flaky and produces findings.
                        Outcome = (passing && i % 2 == 0) ? OutcomeCodes.Passed : OutcomeCodes.Failed
                    }
                ]));
        }
    }

    private static (int Code, string Output) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(args, output, error);
        return (code, output.ToString() + error.ToString());
    }

    // -----------------------------------------------------------------------
    // Destructive command safety
    // -----------------------------------------------------------------------

    [Fact]
    public void ClearRejectsAnAssemblyFlagWithNoValueInsteadOfDeletingEverything()
    {
        // Arrange — `--assembly` with no value used to read as "no scope", turning a scoped delete
        // into a full one. For a destructive command that is the worst possible misparse.
        Seed("Alpha.Tests", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 3);

        // Act
        var (code, output) = Run("clear", "--force", "--assembly");

        // Assert
        Assert.Equal(2, code);
        Assert.Contains("Required argument missing", output, StringComparison.Ordinal);
        Assert.Equal(3, LocalRunStore.Create().ReadRecent(100).Count);
    }

    [Fact]
    public void ClearRejectsUnknownOptionsInsteadOfIgnoringThem()
    {
        Seed("Alpha.Tests", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 2);

        var (code, output) = Run("clear", "--force", "--dry-run");

        Assert.Equal(2, code);
        Assert.Contains("Unrecognized command or argument", output, StringComparison.Ordinal);
        Assert.Equal(2, LocalRunStore.Create().ReadRecent(100).Count);
    }

    [Fact]
    public void WhereRejectsUnknownOptions()
    {
        var (code, output) = Run("where", "--nonsense");

        Assert.Equal(2, code);
        Assert.Contains("Unrecognized command or argument", output, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Subcommand help
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("report")]
    [InlineData("where")]
    [InlineData("clear")]
    public void SubcommandHelpSucceeds(string verb)
    {
        // Parse errors advertise `xping <verb> --help`, so it has to work rather than being
        // rejected as an unknown option.
        var (code, output) = Run(verb, "--help");

        Assert.Equal(0, code);
        Assert.Contains("Usage:", output, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Aggregation window
    // -----------------------------------------------------------------------

    [Fact]
    public void AllIncludesASuiteWhoseRunsAreAllOlderThanAnotherSuites()
    {
        // Arrange — Beta owns every one of the newest runs. Reading `--last x assemblies` globally
        // would return only Beta's runs and drop Alpha from the report entirely.
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Seed("Alpha.Tests", baseTime, 4);
        Seed("Beta.Tests", baseTime.AddDays(1), 30);

        // Act
        var (code, output) = Run("report", "--all", "--last", "3", "--ascii");

        // Assert
        Assert.Equal(0, code);
        Assert.Contains("Alpha.Tests", output, StringComparison.Ordinal);
        Assert.Contains("Beta.Tests", output, StringComparison.Ordinal);
        Assert.Contains("2 assemblies", output, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Cloud invitation
    // -----------------------------------------------------------------------

    [Fact]
    public void CtaIsSuppressedWhenTheStoredRunsCameFromAConnectedProject()
    {
        // Arrange — isConnected was hard-coded to false, so existing customers were pitched to
        // despite CtaThrottle explicitly excluding them.
        Seed("Alpha.Tests", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 4, connected: true);

        // Act
        var (code, output) = Run("report", "--ascii");

        // Assert
        Assert.Equal(0, code);
        Assert.DoesNotContain("xping.io/start", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CtaIsOfferedForLocalOnlyRuns()
    {
        Seed("Alpha.Tests", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 4, connected: false);

        var (code, output) = Run("report", "--ascii");

        Assert.Equal(0, code);
        Assert.Contains("xping.io/start", output, StringComparison.Ordinal);
    }
}

public sealed class AnalyzerReviewFixesTests
{
    private static LocalRun Run(
        string name, string outcome, bool passedOnRetry = false, int attempt = 1) =>
        new(
            new LocalRunHeader { StartedAtUtc = DateTime.UtcNow },
            [
                new LocalTestRecord
                {
                    Fingerprint = "fp-" + name,
                    Name = name,
                    Outcome = outcome,
                    PassedOnRetry = passedOnRetry,
                    Attempt = attempt
                }
            ]);

    private static LocalRun EmptyRun() =>
        new(new LocalRunHeader { StartedAtUtc = DateTime.UtcNow }, []);

    [Fact]
    public void ARetryFlakeInAnOlderRunIsNotReportedAsThisRun()
    {
        // Arrange — the test flakes in run 1 and is then absent. A boolean flag stayed set, so the
        // report kept claiming it "flaked inside this run" long after it stopped running.
        var runs = new[]
        {
            Run("A", OutcomeCodes.Passed, passedOnRetry: true, attempt: 2),
            EmptyRun(),
            EmptyRun(),
            EmptyRun()
        };

        // Act
        var analysis = LocalFlakinessAnalyzer.Analyze(runs);

        // Assert
        Assert.DoesNotContain(
            analysis.UnstableTests, t => t.Kind == InstabilityKind.FlakedInRun);
    }

    [Fact]
    public void ARetryFlakeInTheMostRecentRunIsStillReported()
    {
        var runs = new[]
        {
            Run("A", OutcomeCodes.Passed),
            Run("A", OutcomeCodes.Passed),
            Run("A", OutcomeCodes.Passed, passedOnRetry: true, attempt: 2)
        };

        var finding = Assert.Single(LocalFlakinessAnalyzer.Analyze(runs).UnstableTests);
        Assert.Equal(InstabilityKind.FlakedInRun, finding.Kind);
    }

    [Fact]
    public void ATestSeenTwiceInAThreeRunWindowIsNotCalledFlaky()
    {
        // Arrange — the stated minimum is three runs, but the check only applied to the window, so
        // two observations of a newly added test were enough to label it flaky.
        var runs = new[]
        {
            EmptyRun(),
            Run("A", OutcomeCodes.Passed),
            Run("A", OutcomeCodes.Failed)
        };

        // Act
        var analysis = LocalFlakinessAnalyzer.Analyze(runs);

        // Assert
        Assert.False(analysis.HasFindings);
    }

    [Fact]
    public void ATestSeenThreeTimesIsEligible()
    {
        var runs = new[]
        {
            Run("A", OutcomeCodes.Passed),
            Run("A", OutcomeCodes.Failed),
            Run("A", OutcomeCodes.Passed)
        };

        Assert.Single(LocalFlakinessAnalyzer.Analyze(runs).UnstableTests);
    }

    [Fact]
    public void AggregateHistoryIsInsufficientWhenNoAssemblyHasEnoughRuns()
    {
        // Arrange — three assemblies with one run each total three runs, which previously read as
        // sufficient history even though nothing can be compared across runs.
        var runs = new[]
        {
            new LocalRun(
                new LocalRunHeader { Assembly = "A.Tests", StartedAtUtc = DateTime.UtcNow },
                [new LocalTestRecord { Fingerprint = "a", Name = "a", Outcome = OutcomeCodes.Passed }]),
            new LocalRun(
                new LocalRunHeader { Assembly = "B.Tests", StartedAtUtc = DateTime.UtcNow },
                [new LocalTestRecord { Fingerprint = "b", Name = "b", Outcome = OutcomeCodes.Passed }]),
            new LocalRun(
                new LocalRunHeader { Assembly = "C.Tests", StartedAtUtc = DateTime.UtcNow },
                [new LocalTestRecord { Fingerprint = "c", Name = "c", Outcome = OutcomeCodes.Passed }])
        };

        // Act
        var analysis = AggregateAnalyzer.Analyze(runs);

        // Assert
        Assert.Equal(3, analysis.RunsAnalysed);
        Assert.Equal(1, analysis.LargestAssemblyWindow);
        Assert.False(analysis.HasSufficientHistory);
    }

    [Fact]
    public void AggregateHistoryIsSufficientWhenOneAssemblyHasEnoughRuns()
    {
        var runs = Enumerable.Range(0, 3)
            .Select(i => new LocalRun(
                new LocalRunHeader { Assembly = "A.Tests", StartedAtUtc = DateTime.UtcNow.AddMinutes(i) },
                [new LocalTestRecord { Fingerprint = "a", Name = "a", Outcome = OutcomeCodes.Passed }]))
            .Append(new LocalRun(
                new LocalRunHeader { Assembly = "B.Tests", StartedAtUtc = DateTime.UtcNow },
                [new LocalTestRecord { Fingerprint = "b", Name = "b", Outcome = OutcomeCodes.Passed }]))
            .ToList();

        var analysis = AggregateAnalyzer.Analyze(runs);

        Assert.Equal(3, analysis.LargestAssemblyWindow);
        Assert.True(analysis.HasSufficientHistory);
    }
}
