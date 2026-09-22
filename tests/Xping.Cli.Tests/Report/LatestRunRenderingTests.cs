/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Contract;
using Xping.Cli.Reporting;

using static Xping.Cli.Tests.Report.ReportFixtures;
using static Xping.Cli.Tests.Report.ReportText;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The <c>LATEST RUN</c> section, one rule per test: where it sits, what its heading says, how a
/// row is laid out, and when it is absent.
/// </summary>
public sealed class LatestRunRenderingTests
{
    private const int RowIndent = 17;

    private const string Bold = "\u001b[1m";

    private const string Reset = "\u001b[0m";

    private const string Faint = "\u001b[90m";

    private static readonly LatestRunFailureDto Regression = LatestRunFailure(
        "new", "Checkout_AppliesDiscount", "passed the previous 19 runs, failed just now",
        priorSessions: 19);

    private static readonly LatestRunFailureDto Fresh = LatestRunFailure(
        "newTest", "Checkout_RejectsExpiredCoupon", "first seen this run, failed",
        failureSummary: "EqualException");

    private static readonly LatestRunFailureDto Known = LatestRunFailure(
        "seenBefore", "Checkout_Rounds", "failed 2 of 4 runs, too few to classify yet",
        priorSessions: 3, priorFailures: 1);

    private static ReportEnvelope With(LatestRunDto? latestRun, params FindingDto[] findings) =>
        Envelope(findings) with { LatestRun = latestRun };

    private static string[] Section(string report)
    {
        string[] fenced = Fenced(report);
        int heading = Array.FindIndex(fenced, l => l.StartsWith("LATEST RUN", StringComparison.Ordinal));

        Assert.True(heading >= 0, "no LATEST RUN heading");

        int end = Array.FindIndex(fenced, heading, l => l.StartsWith("NEEDS ATTENTION", StringComparison.Ordinal));
        return fenced[heading..(end < 0 ? fenced.Length : end)];
    }

    // Distinct ids, because the "also failing" line names findings by id and the fixture's
    // default gives every finding the same one.
    private static FindingDto[] ThreeFindings() =>
    [
        Finding("AlwaysFailing", "high", "Alpha", "failed 20 of 20 executions (100%)") with { Id = "f_alpha" },
        Finding("Flaky", "medium", "Beta", "failed 6 of 20 executions (30%)") with { Id = "f_beta" },
        Finding("Vanished", "low", "Gamma", "ran in 5 of 20 runs") with { Id = "f_gamma" }
    ];

    // ---------------------------------------------------------------------------------------
    // Presence
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ACleanRunHasNoSection()
    {
        string report = Render(With(LatestRun([]), ThreeFindings()));

        Assert.DoesNotContain("LATEST RUN", report, StringComparison.Ordinal);
        Assert.StartsWith("NEEDS ATTENTION", Fenced(report)[0], StringComparison.Ordinal);
    }

    [Fact]
    public void AnEnvelopeWithoutALatestRunHasNoSection()
    {
        Assert.DoesNotContain("LATEST RUN", Render(With(null, ThreeFindings())), StringComparison.Ordinal);
    }

    [Fact]
    public void ASuppressedSectionIsAbsentEntirely()
    {
        LatestRunDto suppressed = LatestRun([Regression]) with { Suppressed = true };

        string report = Render(With(suppressed, ThreeFindings()));

        Assert.DoesNotContain("LATEST RUN", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Checkout_AppliesDiscount", report, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyKnownFailuresStillEarnTheSection()
    {
        string[] section = Section(Render(With(
            LatestRun([], explainedBy: ["f_alpha", "f_beta"]), ThreeFindings())));

        Assert.EndsWith("no new failures", section[0], StringComparison.Ordinal);
        Assert.Equal("    Also failing: 2 tests explained by findings below (#1, #2).", section[3]);
    }

    // ---------------------------------------------------------------------------------------
    // Placement
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TheSectionOpensTheFenceAndTheFindingsFollowIt()
    {
        string[] fenced = Fenced(Render(With(LatestRun([Regression]), ThreeFindings())));

        Assert.StartsWith("LATEST RUN", fenced[0], StringComparison.Ordinal);

        int heading = Array.FindIndex(fenced, l => l.StartsWith("NEEDS ATTENTION", StringComparison.Ordinal));
        Assert.True(heading > 0);

        // One blank line between the last line of the section and the next heading.
        Assert.Equal(string.Empty, fenced[heading - 1]);
        Assert.NotEqual(string.Empty, fenced[heading - 2]);
    }

    [Fact]
    public void BelowTheFindingGatesTheSectionPrecedesTheEmptyReportLine()
    {
        ReportEnvelope envelope = Get("nothing-reportable") with { LatestRun = LatestRun([Regression]) };

        string[] fenced = Fenced(Render(envelope));

        Assert.StartsWith("LATEST RUN", fenced[0], StringComparison.Ordinal);

        int reason = Array.FindIndex(fenced, l => l.Contains("Nothing reportable yet", StringComparison.Ordinal));
        Assert.True(reason > 0);
        Assert.Equal(string.Empty, fenced[reason - 1]);
        Assert.NotEqual(string.Empty, fenced[reason - 2]);
    }

    // ---------------------------------------------------------------------------------------
    // Heading
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TheHeadingCarriesTheTimeTheShaAndTheCount()
    {
        string[] section = Section(Render(
            With(LatestRun([Regression, Fresh]), ThreeFindings()),
            Drawn(ReportGlyphs.Unicode, color: false)));

        Assert.Equal("LATEST RUN  16:21 · eab9867                               2 new failures", section[0]);
        Assert.Equal(new string('─', FenceWidth), section[1]);
        Assert.Equal(string.Empty, section[2]);
    }

    [Fact]
    public void TheHeadingOmitsAShaThatWasNeverRecorded()
    {
        string[] section = Section(Render(With(LatestRun([Regression], sha: null), ThreeFindings())));

        Assert.StartsWith("LATEST RUN  16:21 ", section[0], StringComparison.Ordinal);
        Assert.DoesNotContain("|", section[0], StringComparison.Ordinal);
        Assert.Equal(FenceWidth, section[0].Length);
    }

    [Fact]
    public void TheAnnotationIsSingularForOne()
    {
        string heading = Section(Render(With(LatestRun([Regression]), ThreeFindings())))[0];

        Assert.EndsWith("1 new failure", heading, StringComparison.Ordinal);
        Assert.Equal(FenceWidth, heading.Length);
    }

    [Fact]
    public void TheAnnotationCountsNewRowsAndNotKnownOnes()
    {
        string heading = Section(Render(With(LatestRun([Regression, Known]), ThreeFindings())))[0];

        Assert.EndsWith("1 new failure", heading, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAnnotationReadsTheCountBeforeTheCap()
    {
        LatestRunDto capped = LatestRun([Regression]) with { NewFailures = 12, FailuresTotal = 12 };

        Assert.EndsWith("12 new failures", Section(Render(With(capped, ThreeFindings())))[0], StringComparison.Ordinal);
    }

    [Fact]
    public void TheHeadingUsesTheAsciiRuleAndSeparatorWhenAsked()
    {
        string[] section = Section(Render(
            With(LatestRun([Regression]), ThreeFindings()),
            Drawn(ReportGlyphs.Ascii, color: false)));

        Assert.StartsWith("LATEST RUN  16:21 | eab9867", section[0], StringComparison.Ordinal);
        Assert.Equal(new string('-', FenceWidth), section[1]);
    }

    // ---------------------------------------------------------------------------------------
    // Rows
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ARowIsStatusNameContrastAndTrailer()
    {
        string[] section = Section(Render(With(LatestRun([Regression]), ThreeFindings())));

        Assert.Equal("    new          SampleTests.Checkout_AppliesDiscount", section[3]);
        Assert.Equal("                 passed the previous 19 runs, failed just now", section[4]);
        Assert.Equal("                 NullReferenceException | SampleTests.cs:42", section[5]);
        Assert.Equal(string.Empty, section[6]);
    }

    [Fact]
    public void EveryStatusIsPrintedAsWordsInAThirteenColumnField()
    {
        string[] section = Section(Render(With(LatestRun([Regression, Fresh, Known]), ThreeFindings())));

        string[] first = [.. section.Where(l => l.Length > 4 && l.StartsWith("    ", StringComparison.Ordinal) && l[4] != ' ')];

        Assert.Equal(3, first.Length);
        Assert.StartsWith("    new          SampleTests.", first[0], StringComparison.Ordinal);
        Assert.StartsWith("    new test     SampleTests.", first[1], StringComparison.Ordinal);
        Assert.StartsWith("    seen before  SampleTests.", first[2], StringComparison.Ordinal);
    }

    [Fact]
    public void RowsAreSeparatedByOneBlankLine()
    {
        string[] section = Section(Render(With(LatestRun([Regression, Fresh]), ThreeFindings())));

        Assert.Equal(string.Empty, section[6]);
        Assert.StartsWith("    new test", section[7], StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheNewStatusIsEmphasisedAndOnlyInColour()
    {
        string coloured = Render(
            With(LatestRun([Regression, Fresh, Known]), ThreeFindings()),
            Drawn(ReportGlyphs.Unicode, color: true));

        string[] rows = [.. Lines(coloured).Where(l => l.StartsWith("    ", StringComparison.Ordinal) && l.Contains("SampleTests.Checkout", StringComparison.Ordinal))];

        Assert.Equal(3, rows.Length);
        Assert.Contains(Bold + "new" + Reset, rows[0], StringComparison.Ordinal);
        Assert.DoesNotContain(Bold, rows[1], StringComparison.Ordinal);
        Assert.DoesNotContain(Bold, rows[2], StringComparison.Ordinal);

        // No colour on any of them: colour is severity's, and these rows have none.
        string section = coloured.Split("NEEDS ATTENTION")[0];
        Assert.DoesNotContain("\u001b[31m", section, StringComparison.Ordinal);
        Assert.DoesNotContain("\u001b[33m", section, StringComparison.Ordinal);

        // Padded against the plain word: the name lands in the same column either way.
        Assert.Equal("SampleTests.", Strip(rows[0]).Substring(RowIndent, "SampleTests.".Length));
        Assert.Equal("SampleTests.", Strip(rows[1]).Substring(RowIndent, "SampleTests.".Length));
    }

    [Fact]
    public void ColourAddsNothingButEscapeCodes()
    {
        ReportEnvelope envelope = With(LatestRun([Regression, Fresh, Known], explainedBy: ["f_alpha"]), ThreeFindings());

        string plain = Render(envelope, Drawn(ReportGlyphs.Unicode, color: false));
        string coloured = Render(envelope, Drawn(ReportGlyphs.Unicode, color: true));

        Assert.Equal(plain, Strip(coloured));
    }

    [Fact]
    public void TheTrailerIsDim()
    {
        string coloured = Render(
            With(LatestRun([Regression]), ThreeFindings()),
            Drawn(ReportGlyphs.Unicode, color: true));

        string trailer = Lines(coloured).Single(l => l.Contains("NullReferenceException", StringComparison.Ordinal));

        Assert.Contains(Faint + "NullReferenceException | SampleTests.cs:42" + Reset, trailer, StringComparison.Ordinal);
    }

    [Fact]
    public void ATrailerWithoutAFailureSummaryIsTheLocationAlone()
    {
        LatestRunFailureDto row = LatestRunFailure(
            "new", "Checkout", "passed the previous 2 runs, failed just now", failureSummary: null);

        string[] section = Section(Render(With(LatestRun([row]), ThreeFindings())));

        Assert.Equal("                 SampleTests.cs:42", section[5]);
    }

    [Fact]
    public void ATrailerWithNeitherSummaryNorLocationIsOmitted()
    {
        LatestRunFailureDto row = LatestRunFailure(
            "new", "Checkout", "passed the previous 2 runs, failed just now",
            failureSummary: null, sourceFile: null);

        string[] section = Section(Render(With(LatestRun([row]), ThreeFindings())));

        Assert.Equal("                 passed the previous 2 runs, failed just now", section[4]);
        Assert.Equal(string.Empty, section[5]);
    }

    [Fact]
    public void ALongPathIsCutAtADirectoryBoundaryAndTheSummaryIsKeptWhole()
    {
        LatestRunFailureDto row = LatestRunFailure(
            "new", "Checkout", "passed the previous 2 runs, failed just now",
            failureSummary: "DbUpdateConcurrencyException",
            sourceFile: "src/MyCompany/MyProduct/Tests/Integration/Checkout/CheckoutTests.cs");

        string trailer = Section(Render(With(LatestRun([row]), ThreeFindings())))[5];

        Assert.StartsWith("                 DbUpdateConcurrencyException | .../", trailer, StringComparison.Ordinal);
        Assert.EndsWith("/CheckoutTests.cs:42", trailer, StringComparison.Ordinal);
        Assert.True(trailer.Length <= FenceWidth, trailer);
    }

    [Fact]
    public void ALongNameIsCutInFrontOfTheMethod()
    {
        LatestRunFailureDto row = LatestRunFailure(
            "new", "PlacesAnOrderAndSettlesItAgainstTheLedgerWithinTheSameTransaction",
            "passed the previous 2 runs, failed just now");

        string line = Section(Render(With(LatestRun([row]), ThreeFindings())))[3];

        Assert.Equal("    new          ...PlacesAnOrderAndSettlesItAgainstTheLedgerWithinTheSameTransaction", line);
    }

    [Fact]
    public void ALongContrastWrapsAtTheRowIndent()
    {
        LatestRunFailureDto row = LatestRunFailure(
            "new", "Checkout",
            "confidence 0.94 over 812 runs on main, failed on this branch and on no other in the last week");

        string[] section = Section(Render(With(LatestRun([row]), ThreeFindings())));

        Assert.Equal("                 confidence 0.94 over 812 runs on main, failed on this", section[4]);
        Assert.Equal("                 branch and on no other in the last week", section[5]);
    }

    // ---------------------------------------------------------------------------------------
    // Also failing
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TheAlsoFailingLineNamesTheRowsBelow()
    {
        string[] section = Section(Render(With(
            LatestRun([Regression], explainedBy: ["f_alpha", "f_gamma"]), ThreeFindings())));

        Assert.Equal(string.Empty, section[6]);
        Assert.Equal("    Also failing: 2 tests explained by findings below (#1, #3).", section[7]);
    }

    [Fact]
    public void TheAlsoFailingLineIsSingularForOne()
    {
        string[] section = Section(Render(With(
            LatestRun([Regression], explainedBy: ["f_beta"]), ThreeFindings())));

        Assert.Equal("    Also failing: 1 test explained by finding below (#2).", section[7]);
    }

    [Fact]
    public void AGroupFindingExplainingSeveralTestsIsOneRow()
    {
        string[] section = Section(Render(With(
            LatestRun([Regression], explainedBy: ["f_alpha"], explained: 3), ThreeFindings())));

        Assert.Equal("    Also failing: 3 tests explained by finding below (#1).", section[7]);
    }

    [Fact]
    public void AFindingCutByTopHasNoRowToName()
    {
        FindingDto[] shown = [.. ThreeFindings().Take(1)];
        ReportEnvelope envelope = Envelope(shown, shown: 1, total: 3) with
        {
            LatestRun = LatestRun([Regression], explainedBy: ["f_alpha", "f_gamma"])
        };

        string[] section = Section(Render(envelope));

        Assert.Equal("    Also failing: 2 tests explained by finding below (#1).", section[7]);
    }

    [Fact]
    public void WhenEveryExplainingFindingWasCutTheLineSaysSo()
    {
        FindingDto[] shown = [.. ThreeFindings().Take(1)];
        ReportEnvelope envelope = Envelope(shown, shown: 1, total: 3) with
        {
            LatestRun = LatestRun([Regression], explainedBy: ["f_beta", "f_gamma"])
        };

        string[] section = Section(Render(envelope));

        Assert.Equal("    Also failing: 2 tests explained by findings not shown.", section[7]);
    }

    // ---------------------------------------------------------------------------------------
    // Discipline
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void NothingInTheSectionExceedsTheFence()
    {
        string report = Render(
            With(LatestRun([Regression, Fresh, Known], explainedBy: ["f_alpha", "f_beta", "f_gamma"]), ThreeFindings()),
            Drawn(ReportGlyphs.Unicode, color: false));

        Assert.All(Section(report), line => Assert.True(line.Length <= FenceWidth, line));
    }

    [Fact]
    public void APipedSectionIsAscii()
    {
        string report = Render(
            With(LatestRun([Regression, Fresh, Known], explainedBy: ["f_alpha"]), ThreeFindings()),
            Capabilities(redirected: true));

        Assert.All(Section(report), line => Assert.All(line, ch => Assert.True(ch < 0x80, line)));
    }
}
