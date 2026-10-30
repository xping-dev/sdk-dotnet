/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Rendering;
using Xping.Cli.Reporting;
using Xping.Sdk.Core.Models.Executions;
using static Xping.Cli.Tests.Report.ReportFixtures;
using static Xping.Cli.Tests.Report.ReportText;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The text view of one test's history, as <c>xping report --test</c> renders it.
/// </summary>
public sealed class HistoryRenderingTests
{
    private const string Name = "MyApp.Tests.CalculatorTests.FlakyTest_PassesOnRetry";

    private const string All = "xping report --all --assembly MyApp.Tests --runs 20";

    private static readonly DateTime Start = new(2026, 8, 20, 15, 35, 0, DateTimeKind.Utc);

    [Fact]
    public void TheHeadingCarriesTheShortFingerprintAndTheIdentityFollows()
    {
        string[] fenced = Fenced(Render(History([Ran(0)])));

        Assert.StartsWith("TEST ", fenced[0], StringComparison.Ordinal);
        Assert.EndsWith(" fp 3a597eb2", fenced[0], StringComparison.Ordinal);
        Assert.Equal(FenceWidth, fenced[0].Length);
        Assert.Equal("", fenced[2]);
        Assert.Equal($"    test    {Name}", fenced[3]);
        Assert.Equal("    source  tests/CalculatorTests.cs:42", fenced[4]);
        Assert.Equal("", fenced[5]);
    }

    [Fact]
    public void TheSummaryCountsRunsAndOmitsOtherWhenThereIsNone()
    {
        string[] fenced = Fenced(Render(History([Ran(0)], counts: new TestRunCountsDto(6, 20, 0, 6, 0, 0))));

        Assert.Contains("ran in 6 of 20 runs: 0 passed, 6 passed on retry, 0 failed", fenced);
    }

    [Fact]
    public void OtherOutcomesAreCountedWhenThereAreAny()
    {
        string[] fenced = Fenced(Render(History([Ran(0)], counts: new TestRunCountsDto(3, 3, 1, 0, 0, 2))));

        Assert.Contains("ran in 3 of 3 runs: 1 passed, 0 passed on retry, 0 failed, 2 other", fenced);
    }

    [Fact]
    public void TheDurationLineSharesTheUnitOfItsRange()
    {
        string[] fenced = Fenced(Render(History([Ran(0)])));

        Assert.Contains("duration p50 12ms | p95 13ms | range 11-13ms", fenced);
    }

    [Fact]
    public void TheDurationLineIsOmittedWhenNoRunReachedAVerdict()
    {
        string[] fenced = Fenced(Render(History([Ran(0, TestOutcome.Skipped)], timed: false)));

        Assert.DoesNotContain(fenced, l => l.StartsWith("duration", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(11, 13, "11-13ms")]
    [InlineData(12, 12, "12ms")]
    [InlineData(1200, 3000, "1.2-3s")]
    [InlineData(900, 1200, "900ms-1.2s")]
    [InlineData(65_000, 120_000, "1m 5s-2m")]
    public void ARangeWritesASharedSingleUnitOnce(long min, long max, string expected) =>
        Assert.Equal(expected, TextReportRenderer.Range(min, max));

    [Theory]
    [InlineData(TestOutcome.Failed, TestOutcome.Passed, 2, "  08-20 15:35  x->+  attempt 2         12ms  c670fc3")]
    [InlineData(TestOutcome.Failed, TestOutcome.Timeout, 2, "  08-20 15:35  x     timeout @2        12ms  c670fc3")]
    [InlineData(TestOutcome.Skipped, TestOutcome.Skipped, 1, "  08-20 15:35  o     skipped           12ms  c670fc3")]
    [InlineData(TestOutcome.NotExecuted, TestOutcome.NotExecuted, 1, "  08-20 15:35  o     not run           12ms  c670fc3")]
    [InlineData(TestOutcome.Failed, TestOutcome.Inconclusive, 99, "  08-20 15:35  x->o  inconcl. @99      12ms  c670fc3")]
    [InlineData(TestOutcome.Passed, TestOutcome.Passed, 1, "  08-20 15:35  +                       12ms  c670fc3")]
    public void ARunIsOneRowOfFixedColumns(TestOutcome first, TestOutcome deciding, int attempt, string expected)
    {
        TestSessionDto run = Ran(0, deciding, attempt) with
        {
            Attempts = [new TestAttemptDto(1, first, 10), new TestAttemptDto(attempt, deciding, 12)]
        };

        Assert.Contains(expected, Fenced(Render(History([run]))));
    }

    [Fact]
    public void UnicodeGlyphsKeepTheColumnsAlignedWithAscii()
    {
        TestSessionDto run = Ran(0, TestOutcome.Passed, 2) with
        {
            Attempts = [new TestAttemptDto(1, TestOutcome.Failed, 10), new TestAttemptDto(2, TestOutcome.Passed, 12)]
        };

        string[] fenced = Fenced(Render(History([run]), Drawn(ReportGlyphs.Unicode, color: false)));

        Assert.Contains("  08-20 15:35  ✗→✓   attempt 2         12ms  c670fc3", fenced);
    }

    [Fact]
    public void ARunWithNoShaEndsOnItsDuration()
    {
        string[] fenced = Fenced(Render(History([Ran(0) with { Sha = null }])));

        Assert.Contains("  08-20 15:35  +                       12ms", fenced);
    }

    [Fact]
    public void APartialRunIsTaggedAndTheTagIsDim()
    {
        string report = Render(History([Ran(0) with { Partial = true }]), Drawn(ReportGlyphs.Ascii, color: true));
        string[] fenced = Fenced(report);

        string row = Assert.Single(fenced, l => l.StartsWith("  08-20", StringComparison.Ordinal));
        Assert.Equal("  08-20 15:35  +                       12ms  c670fc3  partial", Strip(row));
        Assert.NotEqual(Strip(row), row);
    }

    [Fact]
    public void AbsentRunsCollapseIntoOneLineInTheirPlace()
    {
        TestSessionDto[] sessions =
        [
            Absent(0, partial: true),
            Absent(1, partial: true),
            Ran(2),
            Absent(3, partial: false),
            Ran(4)
        ];

        string[] fenced = Fenced(Render(History(sessions)));
        int runs = Array.IndexOf(fenced, "RUNS" + new string(' ', FenceWidth - 4 - 12) + "newest first");
        string[] rows = fenced[(runs + 3)..(runs + 7)];

        Assert.Equal("               -     absent from 2 runs, 2 partial", rows[0]);
        Assert.StartsWith("  08-20 13:35", rows[1], StringComparison.Ordinal);
        Assert.Equal("               -     absent from 1 run", rows[2]);
        Assert.StartsWith("  08-20 11:35", rows[3], StringComparison.Ordinal);
    }

    [Fact]
    public void AFindingKeepsItsReportRowAndDropsTheNameAndTheSiblingNote()
    {
        FindingDto finding = Finding("Flaky", "high", Name, "failed 6 of 12 executions") with
        {
            Annotation = "same test as #4"
        };

        string[] fenced = Fenced(Render(History([Ran(0)], [finding], [5])));
        int heading = Array.FindIndex(fenced, l => l.StartsWith("FINDINGS", StringComparison.Ordinal));

        Assert.EndsWith(" 1 finding", fenced[heading], StringComparison.Ordinal);
        Assert.Equal("5.  HIGH  flaky", fenced[heading + 3]);
        Assert.Equal("    failed 6 of 12 executions", fenced[heading + 4]);
        Assert.StartsWith("    evidence moderate", fenced[heading + 5], StringComparison.Ordinal);
        Assert.DoesNotContain(fenced, l => l.Contains("same test as", StringComparison.Ordinal));
    }

    [Fact]
    public void AClusterKeepsItsCauseLine()
    {
        FindingDto cluster = Cluster("fixture setup failed", 2, "2 tests failed together");

        string[] fenced = Fenced(Render(History([Ran(0)], [cluster], [3])));

        Assert.Contains("3.  HIGH  broken fixture", fenced);
        Assert.Contains("    fixture setup failed (2 tests)", fenced);
    }

    [Fact]
    public void AnEmptyBlockSaysNoFindingsUnderABareHeading()
    {
        string[] fenced = Fenced(Render(History([Ran(0)])));

        Assert.Contains("FINDINGS", fenced);
        Assert.Equal("no findings", fenced[^1]);
    }

    [Fact]
    public void AnEmptyBlockBelowTheFloorsSaysWhy()
    {
        const string Reason = "too little history to judge: findings need 5 runs, this test has 3";

        string[] fenced = Fenced(Render(History([Ran(0)], withheld: Reason)));

        Assert.Equal(Reason, fenced[^1]);
    }

    [Theory]
    [InlineData(0, "no findings about this test | all: " + All)]
    [InlineData(1, "1 finding about this test | all: " + All)]
    [InlineData(2, "2 findings about this test | all: " + All)]
    public void TheFooterCountsTheFindingsAboutTheTest(int count, string expected)
    {
        FindingDto[] findings = [.. Enumerable.Range(0, count).Select(_ => Finding("Flaky", "high", Name, "h"))];

        string[] lines = Lines(Render(History([Ran(0)], findings, [.. Enumerable.Range(1, count)])));

        Assert.Contains(expected, lines);
    }

    [Fact]
    public void TheDetailLineNamesTheFirstFindingsReportRow()
    {
        FindingDto finding = Finding("Flaky", "high", Name, "h");

        string[] lines = Lines(Render(History([Ran(0)], [finding], [7])));

        Assert.Equal("Detail of row 7: " + finding.DrillDown, lines.Last(l => l.Length > 0));
    }

    [Fact]
    public void AFindingWithAReproEndsItsRowWithTheSentence()
    {
        FindingDto finding = Finding("Flaky", "high", Name, "failed 4 of 9 executions") with { Repro = Repro("cmd") };

        string[] fenced = Fenced(Render(History([Ran(0)], [finding], [5])));
        int row = Array.IndexOf(fenced, "5.  HIGH  flaky");

        Assert.StartsWith("    evidence moderate", fenced[row + 2], StringComparison.Ordinal);
        Assert.Equal("    repro  run 6 times: at 4 of 9 runs failing (44.4%), 6 runs show a", fenced[row + 3]);
        Assert.Equal("           failure 95% of the time", fenced[row + 4]);
    }

    [Fact]
    public void TheReproLineNamesTheFirstFindingThatHasOne()
    {
        FindingDto[] findings =
        [
            Finding("DurationUnstable", "high", Name, "h") with { Id = "f_0001" },
            Finding("Flaky", "medium", Name, "h") with { Id = "f_0002", Repro = Repro("first") },
            Finding("TimingOut", "medium", Name, "h") with { Id = "f_0003", Repro = Repro("second") }
        ];

        string[] lines = [.. Lines(Render(History([Ran(0)], findings, [3, 5, 6]))).Where(l => l.Length > 0)];

        Assert.StartsWith("Detail of row 3: ", lines[^2], StringComparison.Ordinal);
        Assert.Equal("Repro of row 5: first", lines[^1]);
    }

    [Fact]
    public void AReproWithACountIsPreferredToATooRareOne()
    {
        FindingDto[] findings =
        [
            Finding("Flaky", "high", Name, "h") with { Id = "f_0001", Repro = Repro("rare") with { Runs = null } },
            Finding("TimingOut", "medium", Name, "h") with { Id = "f_0002", Repro = Repro("counted") }
        ];

        string[] lines = [.. Lines(Render(History([Ran(0)], findings, [3, 5]))).Where(l => l.Length > 0)];

        Assert.Equal("Repro of row 5: counted", lines[^1]);
    }

    [Fact]
    public void ATooRareReproIsPrintedWhenItIsTheOnlyOne()
    {
        FindingDto finding = Finding("Flaky", "high", Name, "h") with { Repro = Repro("rare") with { Runs = null } };

        string[] lines = [.. Lines(Render(History([Ran(0)], [finding], [3]))).Where(l => l.Length > 0)];

        Assert.Equal("Repro of row 3: rare", lines[^1]);
    }

    [Fact]
    public void AClusterPrintsNoReproInTheHistoryOfOneOfItsMembers()
    {
        FindingDto cluster = Cluster("fixture setup failed", 2, "2 tests failed together") with { Repro = Repro("member") };

        string report = Render(History([Ran(0)], [cluster], [3]));

        Assert.DoesNotContain("repro", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoFindingWithAReproPrintsNoReproLine()
    {
        string withFinding = Render(History([Ran(0)], [Finding("Flaky", "high", Name, "h")], [1]));
        string healthy = Render(History([Ran(0)]));

        Assert.DoesNotContain("repro", withFinding, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("repro", healthy, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EveryFencedLineFitsTheFence()
    {
        TestSessionDto[] sessions =
        [
            Absent(0, partial: true),
            Ran(1, TestOutcome.Inconclusive, 99) with { Partial = true, DurationMs = 7_250_000 },
            Ran(2)
        ];

        FindingDto[] findings =
        [
            Finding("Flaky", "high", Name, "failed 6 of 12 executions in 6 of 20 runs") with { Repro = Repro("cmd") }
        ];

        foreach (ReportGlyphs glyphs in new[] { ReportGlyphs.Ascii, ReportGlyphs.Unicode })
        {
            foreach (string line in Fenced(Render(History(sessions, findings, [1]), Drawn(glyphs, color: false))))
                Assert.True(line.Length <= FenceWidth, $"{line.Length}: {line}");
        }
    }

    [Fact]
    public void AnUnresolvedNameSaysSoInsteadOfAnEmptyHistory()
    {
        ReportEnvelope envelope = Envelope() with { Test = TestHistoryBuilder.NotFound("Ad") };

        string report = Render(envelope);

        Assert.Equal(["'Ad' names no one test in this window."], Fenced(report));
        Assert.DoesNotContain("about this test", report, StringComparison.Ordinal);
    }

    private static TestSessionDto Ran(int index, TestOutcome outcome = TestOutcome.Passed, int attempt = 1) =>
        new(
            Guid.NewGuid(),
            Start.AddHours(-index),
            "c670fc3a91",
            Appeared: true,
            Partial: false,
            outcome,
            attempt,
            12,
            [new TestAttemptDto(attempt, outcome, 12)]);

    private static ReproDto Repro(string command) =>
        new(command, 6, 6, 4, 9, 0.444,
            "run 6 times: at 4 of 9 runs failing (44.4%), 6 runs show a failure 95% of the time");

    private static TestSessionDto Absent(int index, bool partial) =>
        new(Guid.NewGuid(), Start.AddHours(-index), "c670fc3a91", false, partial, null, null, null, []);

    private static ReportEnvelope History(
        TestSessionDto[] sessions,
        FindingDto[]? findings = null,
        int[]? rows = null,
        TestRunCountsDto? counts = null,
        string? withheld = null,
        bool timed = true)
    {
        findings ??= [];
        rows ??= [];

        var subject = new SubjectDto(
            "test", "3a597eb2c0ffee", Name, Name, "CalculatorTests.FlakyTest_PassesOnRetry", null,
            "tests/CalculatorTests.cs", 42, "MyApp.Tests", null, null, null);

        var history = new TestHistoryDto(
            Name,
            subject,
            "3a597eb2",
            sessions,
            counts ?? new TestRunCountsDto(sessions.Count(s => s.Appeared), sessions.Length, 0, 0, 0, 0),
            timed ? new TestDurationDto(12, 13, 11, 13, 0.1, 6) : null,
            [],
            withheld,
            [.. findings.Select((f, i) => new SameSubjectDto(f.Id, f.Kind, rows[i]))],
            []);

        return Envelope(findings) with
        {
            Test = history,
            Truncated = new TruncationDto(findings.Length, 9, All)
        };
    }
}
