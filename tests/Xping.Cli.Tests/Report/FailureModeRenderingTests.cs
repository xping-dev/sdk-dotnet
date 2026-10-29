/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Contract;
using static Xping.Cli.Tests.Report.ReportFixtures;
using static Xping.Cli.Tests.Report.ReportText;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The <c>FAILURE MODES</c> block, as <c>xping report --id</c> renders it (failure-mode spec D5).
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
        Assert.Equal("    Expected <n> but was <n>", fenced[^1]);
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

    /// <summary>The block's lines below its heading, rule and blank line.</summary>
    private static string[] Block(string report)
    {
        string[] fenced = Fenced(report);
        int heading = Array.FindIndex(fenced, l => l.StartsWith(Heading, StringComparison.Ordinal));

        return fenced[(heading + 3)..];
    }

    /// <summary>A single mode's message lines: between its type line and its first frame.</summary>
    private static string[] MessageLines(string report)
    {
        string[] block = Block(report);

        return [.. block[2..].TakeWhile(l => !l.StartsWith("      at ", StringComparison.Ordinal))];
    }
}
