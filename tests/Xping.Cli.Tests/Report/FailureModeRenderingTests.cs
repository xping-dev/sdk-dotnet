/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Sdk.Core.Models.Executions;
using static Xping.Cli.Tests.Report.ReportFixtures;
using static Xping.Cli.Tests.Report.ReportText;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The <c>FAILURE MODES</c> block, as <c>xping report --id</c> and <c>--test</c> render it
/// (failure-mode spec D4, D5).
/// </summary>
public sealed class FailureModeRenderingTests
{
    private const string Heading = "FAILURE MODES";

    private const int ContinuationBudget = FenceWidth - Indent;

    [Fact]
    public void TheBlockIsLastInTheFenceAfterTheSameSubjectLine()
    {
        string[] fenced = Fenced(Render(Get("detail-test")));

        int also = Array.FindIndex(fenced, l => l.Contains("Also about this test:", StringComparison.Ordinal));
        int heading = Array.FindIndex(fenced, l => l.StartsWith(Heading, StringComparison.Ordinal));

        Assert.True(also >= 0 && heading > also, "the block follows the same-subject line");
        Assert.Equal(string.Empty, fenced[heading - 1]);
        Assert.Equal("1.  6 failed attempts | since the window opened", fenced[heading + 3]);
        Assert.Equal("    expected <num> but was <num>", fenced[^1]);
    }

    [Fact]
    public void TheHeadingCountsTheModes()
    {
        Assert.Contains(Fenced(Render(Get("detail-failure-modes"))), l => l.StartsWith(Heading, StringComparison.Ordinal)
            && l.EndsWith(" 3 failure modes", StringComparison.Ordinal) && l.Length == FenceWidth);

        Assert.Contains(Fenced(Render(Get("detail-unavailable"))), l => l.StartsWith(Heading, StringComparison.Ordinal)
            && l.EndsWith(" 1 failure mode", StringComparison.Ordinal));
    }

    [Fact]
    public void AFindingWithoutModesHasNoBlock()
    {
        Assert.DoesNotContain(Heading, Render(Get("detail-timing-out")), StringComparison.Ordinal);
    }

    [Fact]
    public void AModeNewInTheWindowNamesItsDateShaAndHowLongAgo()
    {
        Assert.Contains(
            "1.  4 failed attempts | first seen 08-12 09:40 at b71e004, 6 runs ago",
            Fenced(Render(Get("detail-failure-modes"))));
    }

    [Fact]
    public void AModePresentInTheOldestRunHasNoDate()
    {
        Assert.Contains("2.  2 failed attempts | since the window opened", Fenced(Render(Get("detail-failure-modes"))));
    }

    [Fact]
    public void AModeFirstSeenInTheLatestRunSaysSo()
    {
        Assert.Contains("3.  1 failed attempt | first seen in the latest run", Fenced(Render(Get("detail-failure-modes"))));
    }

    /// <summary>
    /// Discounting set aside every older failure (D3): the date is the counted failures', and the
    /// flags still say the mode is not new.
    /// </summary>
    [Fact]
    public void AModeWhoseCountedFailuresBeganInTheLatestRunIsDatedThere()
    {
        string[] block = Block(Render(With(Mode() with { FirstSeenSessionsAgo = 0 })));

        // Wider than the line, so it wraps at its last space, continuing under the count.
        Assert.Equal("1.  3 failed attempts | first seen 08-12 09:40 at b71e004, in the latest", block[0]);
        Assert.Equal("    run", block[1]);
    }

    [Fact]
    public void OneRunAgoIsSingular()
    {
        Assert.Contains(
            "1.  3 failed attempts | first seen 08-12 09:40 at b71e004, 1 run ago",
            Fenced(Render(With(Mode() with { FirstSeenSessionsAgo = 1 }))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AModeWithoutAShaDropsTheAt(string? sha)
    {
        string[] fenced = Fenced(Render(With(Mode() with { FirstSeenSha = sha })));

        Assert.Contains("1.  3 failed attempts | first seen 08-12 09:40, 6 runs ago", fenced);
        Assert.DoesNotContain(fenced, l => l.Contains(" at ,", StringComparison.Ordinal));
    }

    [Fact]
    public void AModeWithoutATypeSaysSo()
    {
        Assert.Contains("    exception type not recorded", Fenced(Render(With(Mode() with { ExceptionType = null }))));
    }

    [Fact]
    public void AnEmptyMessagePrintsNoLine()
    {
        string[] block = Block(Render(With(Mode() with { Message = string.Empty })));

        Assert.Equal(["    System.InvalidOperationException", "      at MyApp.Checkout.Basket.Total()"], block[1..]);
    }

    [Fact]
    public void ADegradedModeNotesItsFramesAreTheFrameworks()
    {
        string[] fenced = Fenced(Render(Get("detail-failure-modes")));
        int note = Array.IndexOf(fenced, "      (no test code on the stack; framework frames shown)");

        Assert.True(note > 0, "the note is printed");
        Assert.Equal("         SendAsync(HttpRequestMessage request)", fenced[note - 1]);
    }

    [Fact]
    public void ADegradedModeWithoutFramesHasNoNote()
    {
        string report = Render(With(Mode() with { FramesDegraded = true, Frames = [] }));

        Assert.DoesNotContain("framework frames shown", report, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnavailableModePrintsItsCountAndNothingElse()
    {
        string[] block = Block(Render(Get("detail-unavailable")));

        Assert.Equal(
            ["1.  19 failed attempts | first seen 08-06 09:00 at a3f9c2e, 18 runs ago", "    the adapter recorded no failure detail"],
            block);
    }

    [Fact]
    public void AFrameTooWideBreaksBeforeItsMethodAndContinuesUnderIt()
    {
        string[] block = Block(Render(With(Mode() with
        {
            Frames = ["MyApp.Infrastructure.Persistence.Repositories.OrderRepository.SaveChangesAsync(CancellationToken token)"]
        })));

        Assert.Equal("      at MyApp.Infrastructure.Persistence.Repositories.OrderRepository.", block[^2]);
        Assert.Equal("         SaveChangesAsync(CancellationToken token)", block[^1]);
    }

    [Theory]
    [InlineData("ctor")]
    [InlineData("cctor")]
    public void AConstructorFrameBreaksBeforeItsOwnDot(string constructor)
    {
        string[] block = Block(Render(With(Mode() with
        {
            Frames = [$"MyApp.Infrastructure.Persistence.Repositories.OrderRepository..{constructor}(CancellationToken token)"]
        })));

        Assert.Equal("      at MyApp.Infrastructure.Persistence.Repositories.OrderRepository.", block[^2]);
        Assert.Equal($"         .{constructor}(CancellationToken token)", block[^1]);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    public void ABlankMessagePrintsNoLine(string message)
    {
        string[] block = Block(Render(With(Mode() with { Message = message })));

        Assert.Equal(["    System.InvalidOperationException", "      at MyApp.Checkout.Basket.Total()"], block[1..]);
    }

    [Fact]
    public void AFrameNeverBreaksAtADotInsideItsArguments()
    {
        string[] block = Block(Render(With(Mode() with
        {
            Frames = ["Tests.Calc(System.Collections.Generic.IReadOnlyDictionary<System.String, System.Decimal> values)"]
        })));

        Assert.Equal("      at Tests.", block[^3]);
        Assert.All(block[^2..], line => Assert.StartsWith("         ", line, StringComparison.Ordinal));
    }

    [Fact]
    public void AMessageIsCappedAtSixLinesEndingInAnEllipsis()
    {
        string message = string.Join(' ', Enumerable.Repeat("expected <num> but was <num>", 40));
        string[] lines = MessageLines(Render(With(Mode() with { Message = message })));

        Assert.Equal(6, lines.Length);
        Assert.EndsWith("...", lines[5], StringComparison.Ordinal);
        Assert.All(lines, line => Assert.True(line.Length <= FenceWidth, $"'{line}' is {line.Length} columns"));
    }

    [Fact]
    public void ASixthLineThatIsOneLongTokenIsCutToFit()
    {
        // Five words that each fill a line, so the token is the sixth line and nothing follows it.
        string words = string.Join(' ', Enumerable.Repeat(new string('w', ContinuationBudget - 1), 5));
        string message = words + " " + new string('x', 400);
        string[] lines = MessageLines(Render(With(Mode() with { Message = message })));

        Assert.Equal(6, lines.Length);
        Assert.Equal(Indent + ContinuationBudget, lines[5].Length);
        Assert.EndsWith("xxx...", lines[5], StringComparison.Ordinal);
    }

    [Fact]
    public void AMessageOfSixLinesIsPrintedWhole()
    {
        string message = string.Join(' ', Enumerable.Repeat("expected <num> but was <num>", 12));
        string[] lines = MessageLines(Render(With(Mode() with { Message = message })));

        Assert.Equal(6, lines.Length);
        Assert.DoesNotContain(lines, l => l.EndsWith("...", StringComparison.Ordinal));
    }

    [Fact]
    public void FiveModesAreListedAndTheRestCounted()
    {
        FailureModeDto[] modes = [.. Enumerable.Range(1, 6).Select(i => Mode() with { Hash = $"h{i}" })];
        string[] fenced = Fenced(Render(With(modes)));

        Assert.Contains(fenced, l => l.StartsWith("5.  ", StringComparison.Ordinal));
        Assert.DoesNotContain(fenced, l => l.StartsWith("6.  ", StringComparison.Ordinal));
        Assert.Equal("    1 more failure mode in --format json", fenced[^1]);
        Assert.Equal(string.Empty, fenced[^2]);
        Assert.EndsWith(" 6 failure modes", fenced.First(l => l.StartsWith(Heading, StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public void FiveModesHaveNoCapLine()
    {
        FailureModeDto[] modes = [.. Enumerable.Range(1, 5).Select(i => Mode() with { Hash = $"h{i}" })];

        Assert.DoesNotContain("more failure mode", Render(With(modes)), StringComparison.Ordinal);
    }

    [Fact]
    public void ModesAreSeparatedByOneBlankLine()
    {
        string[] fenced = Fenced(Render(Get("detail-failure-modes")));

        foreach (string number in new[] { "2.  ", "3.  " })
        {
            int start = Array.FindIndex(fenced, l => l.StartsWith(number, StringComparison.Ordinal));

            Assert.Equal(string.Empty, fenced[start - 1]);
            Assert.NotEqual(string.Empty, fenced[start - 2]);
        }
    }

    /// <summary>
    /// The block's own words and the sha cut stay inside the fence however long the recorded values are.
    /// </summary>
    [Fact]
    public void LongValuesStayInsideTheFence()
    {
        FailureModeDto mode = Mode() with
        {
            Occurrences = 1_234_567,
            FirstSeenSha = new string('a', 80),
            ExceptionType = "MyApp.Infrastructure.Persistence.Repositories.Exceptions.ConcurrencyConflictException",
            Message = string.Join(' ', Enumerable.Repeat("conflict on <guid>", 30)),
            Frames =
            [
                "MyApp.Infrastructure.Persistence.Repositories.OrderRepository.SaveChangesAsync(CancellationToken token, Boolean acceptAllChangesOnSuccess)"
            ]
        };

        foreach (string line in Block(Render(With(mode))))
            Assert.True(line.Length <= FenceWidth, $"'{line}' is {line.Length} columns");
    }

    [Theory]
    [InlineData("history-flaky")]
    [InlineData("history-no-sha")]
    public void InAHistoryTheBlockFollowsRunsAndPrecedesFindings(string fixture)
    {
        string[] fenced = Fenced(Render(Get(fixture)));

        int runs = Array.FindIndex(fenced, l => l.StartsWith("RUNS", StringComparison.Ordinal));
        int heading = Array.FindIndex(fenced, l => l.StartsWith(Heading, StringComparison.Ordinal));
        int findings = Array.FindIndex(fenced, l => l.StartsWith("FINDINGS", StringComparison.Ordinal));

        Assert.True(runs >= 0 && heading > runs && findings > heading, "RUNS, then the block, then FINDINGS");

        // One blank line on each side, never two and never none.
        Assert.Equal(string.Empty, fenced[heading - 1]);
        Assert.StartsWith("  ", fenced[heading - 2], StringComparison.Ordinal);
        Assert.Equal(string.Empty, fenced[findings - 1]);
        Assert.NotEqual(string.Empty, fenced[findings - 2]);
    }

    /// <summary>
    /// <c>history-no-sha</c>'s test has no finding, so the block is followed by the empty findings
    /// block's reason.
    /// </summary>
    [Fact]
    public void InAHistoryWithoutFindingsTheBlockStillPrecedesTheFindingsHeading()
    {
        string[] fenced = Fenced(Render(Get("history-no-sha")));
        int findings = Array.FindIndex(fenced, l => l.StartsWith("FINDINGS", StringComparison.Ordinal));

        Assert.Equal("      at SampleApp.MSTest.InvoiceTests.Total_IncludesTax()", fenced[findings - 2]);
        Assert.Equal("no findings", fenced[findings + 3]);
    }

    [Fact]
    public void AHistoryThatNeverFailedHasNoBlock()
    {
        string[] fenced = Fenced(Render(Get("history-healthy")));
        int findings = Array.FindIndex(fenced, l => l.StartsWith("FINDINGS", StringComparison.Ordinal));

        Assert.DoesNotContain(fenced, l => l.StartsWith(Heading, StringComparison.Ordinal));
        Assert.Equal(string.Empty, fenced[findings - 1]);
        Assert.StartsWith("  ", fenced[findings - 2], StringComparison.Ordinal);
    }

    /// <summary>
    /// The summary counts runs by their deciding attempt, the block counts attempts (D4), so a test
    /// that always passed on retry reads "0 failed" above six failed attempts.
    /// </summary>
    [Fact]
    public void AHistoryCountsFailedAttemptsWhereItsSummaryReadsNoneFailed()
    {
        string[] fenced = Fenced(Render(Get("history-flaky")));

        Assert.Contains(fenced, l => l.EndsWith(": 0 passed, 6 passed on retry, 0 failed", StringComparison.Ordinal));
        Assert.Equal(["1.  6 failed attempts | since the window opened"], fenced.Where(l => l.StartsWith("1.  6 ", StringComparison.Ordinal)));
    }

    /// <summary>
    /// A timed-out attempt is a failed one (Amendment 1E): <c>--test</c> lists the mode that
    /// <c>--id</c>'s <c>TimingOut</c> finding carries none of.
    /// </summary>
    [Fact]
    public void AHistoryListsTheModeItsTimeoutsFailedWith()
    {
        string[] block = Block(Render(Get("history-timeout-retry")));

        Assert.Contains("2.  4 failed attempts | first seen 08-20 23:40 at c670fc3, 6 runs ago", block);
        Assert.Contains("    the adapter recorded no failure detail", block);
    }

    /// <summary>
    /// In a one-run window the newest run is the oldest, so a mode in it is both "only in the latest
    /// run" and "present in the oldest run". The oldest run wins: the report cannot see whether the
    /// mode is new (Amendment 4A).
    /// </summary>
    [Fact]
    public void InAOneRunWindowAModeIsSinceTheWindowOpened()
    {
        AnalysisContext context = TestSessionFactory.Context(TestSessionFactory.Session(
            0,
            [
                TestSessionFactory.Execution(
                    "Broken", TestOutcome.Failed, exceptionType: "System.InvalidOperationException", errorMessage: "boom")
            ]));

        FailureModeDto mode = Assert.Single(FailureModes.Of(context, "fp-Broken"));

        // Both flags hold: this is the case the order decides.
        Assert.True(mode.FirstSeenInLatestSession);
        Assert.False(mode.FirstSeenAfterWindowStart);

        ReportEnvelope history = Get("history-flaky");
        string[] block = Block(Render(history with { Test = history.Test! with { FailureModes = [mode] } }));

        Assert.Equal("1.  1 failed attempt | since the window opened", block[0]);
    }

    [Fact]
    public void InAHistoryTheBlockEndsBeforeFindings()
    {
        string[] block = Block(Render(Get("history-flaky")));

        Assert.Equal("      at SampleApp.MSTest.CalculatorTests.FlakyTest_PassesOnRetry()", block[^1]);
    }

    [Fact]
    public void AHistoryListsFiveModesAndCountsTheRest()
    {
        string[] fenced = Fenced(Render(Get("history-failure-modes")));
        int findings = Array.FindIndex(fenced, l => l.StartsWith("FINDINGS", StringComparison.Ordinal));

        Assert.EndsWith(" 7 failure modes", fenced.First(l => l.StartsWith(Heading, StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains(fenced, l => l.StartsWith("5.  ", StringComparison.Ordinal));
        Assert.DoesNotContain(fenced, l => l.StartsWith("6.  ", StringComparison.Ordinal) && l.Contains("failed attempt", StringComparison.Ordinal));
        Assert.Equal("    2 more failure modes in --format json", fenced[findings - 2]);
        Assert.Equal(string.Empty, fenced[findings - 3]);
    }

    [Fact]
    public void AHistoryCapsALongMessageAtSixLines()
    {
        string[] lines = MessageLines(Render(Get("history-failure-modes")));

        Assert.Equal(6, lines.Length);
        Assert.EndsWith("...", lines[5], StringComparison.Ordinal);
        Assert.All(lines, line => Assert.True(line.Length <= FenceWidth, $"'{line}' is {line.Length} columns"));
    }

    /// <summary>
    /// Stricter than the sweep's rule: nothing the block prints in a history needs the one-token
    /// exception.
    /// </summary>
    [Theory]
    [InlineData("history-flaky")]
    [InlineData("history-timeout-retry")]
    [InlineData("history-no-sha")]
    [InlineData("history-below-floor")]
    [InlineData("history-cluster-member")]
    [InlineData("history-store")]
    [InlineData("history-repro")]
    [InlineData("history-failure-modes")]
    [InlineData("hostile-text-history")]
    public void AHistorysBlockStaysInsideTheFence(string fixture)
    {
        string[] fenced = Fenced(Render(Get(fixture)));
        int heading = Array.FindIndex(fenced, l => l.StartsWith(Heading, StringComparison.Ordinal));
        int findings = Array.FindIndex(fenced, l => l.StartsWith("FINDINGS", StringComparison.Ordinal));

        Assert.True(heading >= 0, $"{fixture} prints the block");
        Assert.All(fenced[heading..findings], line =>
            Assert.True(line.Length <= FenceWidth, $"{fixture}: '{line}' is {line.Length} columns"));
    }

    private static FailureModeDto Mode() =>
        new(
            "abc123",
            "System.InvalidOperationException",
            "expected <num> but was <num>",
            ["MyApp.Checkout.Basket.Total()"],
            FramesDegraded: false,
            Unavailable: false,
            Occurrences: 3,
            FirstSeenAt: new DateTime(2026, 8, 12, 9, 40, 0, DateTimeKind.Utc),
            FirstSeenSha: "b71e004c93d2",
            FirstSeenSessionsAgo: 6,
            FirstSeenInLatestSession: false,
            FirstSeenAfterWindowStart: true);

    /// <summary>The flaky detail view, its modes replaced.</summary>
    private static ReportEnvelope With(params FailureModeDto[] modes)
    {
        ReportEnvelope detail = Get("detail-flaky");

        return detail with { Findings = [detail.Findings[0] with { FailureModes = modes }] };
    }

    /// <summary>
    /// The block's lines below its heading, rule and blank line, up to the blank line before the
    /// next heading, or to the fence's end.
    /// </summary>
    /// <remarks>
    /// Bounded, because in a history FINDINGS follows: a line asserted to be in the block must not
    /// pass by being under a finding.
    /// </remarks>
    private static string[] Block(string report)
    {
        string[] fenced = Fenced(report);
        int heading = Array.FindIndex(fenced, l => l.StartsWith(Heading, StringComparison.Ordinal));
        int next = Array.FindIndex(fenced, heading + 1, l => IsRule(l)) is var rule && rule >= 0
            ? Array.FindIndex(fenced, rule + 1, l => IsRule(l))
            : -1;

        return next < 0 ? fenced[(heading + 3)..] : fenced[(heading + 3)..(next - 2)];
    }

    private static bool IsRule(string line) =>
        line.Length == FenceWidth && line.All(c => c is '-' or '\u2500');

    /// <summary>A single mode's message lines: between its type line and its first frame.</summary>
    private static string[] MessageLines(string report)
    {
        string[] block = Block(report);

        return [.. block[2..].TakeWhile(l => !l.StartsWith("      at ", StringComparison.Ordinal))];
    }
}
