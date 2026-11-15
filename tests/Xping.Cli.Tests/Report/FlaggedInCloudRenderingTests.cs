/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Contract;

using static Xping.Cli.Tests.Report.ReportFixtures;
using static Xping.Cli.Tests.Report.ReportText;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The <c>FLAGGED IN CLOUD</c> section (cloud-data-in-cli-cli-spec Amendment 7G), one rule per test.
/// </summary>
public sealed class FlaggedInCloudRenderingTests
{
    private const string Heading = "FLAGGED IN CLOUD";

    private static readonly CloudHealthTestDto KnownFlaky = HealthTest(
        "MyApp.Tests.CheckoutTests.Checkout_Completes", CloudDigest("known-flaky", flaky: "flaky", score: 0.41, delta: null));

    private static ReportEnvelope WithHealth(ReportEnvelope envelope, CloudHealthDto health)
    {
        CloudContextDto cloud = CloudContext(envelope) with { Health = health };
        return envelope with { Context = (envelope.Context ?? new ContextDto(null, null, null)) with { Cloud = cloud } };
    }

    private static CloudHealthDto Listing(params CloudHealthTestDto[] tests) =>
        new("ok", "none", 412, tests.Length, 0, tests, tests.Length, tests.Length);

    [Fact]
    public void AReportWithNothingFlaggedInCloudHasNoSection()
    {
        string report = Render(Get("cloud-broken"));

        Assert.DoesNotContain(Heading, report, StringComparison.Ordinal);
    }

    [Fact]
    public void ACleanRunCloudFlagsIsNotGivenTheCleanBill()
    {
        string[] fenced = Fenced(Render(WithHealth(Envelope(), Listing(KnownFlaky))));

        Assert.DoesNotContain(fenced, l => l.Contains("No findings.", StringComparison.Ordinal));
        Assert.StartsWith(Heading, fenced[0], StringComparison.Ordinal);
        Assert.EndsWith("1 with no local finding", fenced[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ARowPutsCloudsVerdictWhereTheStatusWordSitsAndItsLineUnderTheName()
    {
        string[] fenced = Fenced(Render(WithHealth(Envelope(), Listing(KnownFlaky))));

        Assert.Equal("    known flaky  CheckoutTests.Checkout_Completes", fenced[3]);
        Assert.Equal("                 cloud: confidence 0.41 (35 d) - flaky (timing, major)", fenced[4]);
    }

    [Fact]
    public void TheSectionFollowsTheFindings()
    {
        ReportEnvelope envelope = Envelope(Finding("Flaky", "medium", "Beta", "failed 6 of 20 executions (30%)"));
        string[] fenced = Fenced(Render(WithHealth(envelope, Listing(KnownFlaky))));

        int findings = Array.FindIndex(fenced, l => l.StartsWith("NEEDS ATTENTION", StringComparison.Ordinal));
        int flagged = Array.FindIndex(fenced, l => l.StartsWith(Heading, StringComparison.Ordinal));

        Assert.InRange(findings, 0, flagged - 1);
        Assert.Equal(string.Empty, fenced[flagged - 1]);
    }

    [Fact]
    public void TestsShownAboveAreCountedOnTheClosingLineEvenWithNoRows()
    {
        ReportEnvelope envelope = Envelope(Finding("Flaky", "medium", "Beta", "failed 6 of 20 executions (30%)"));
        string[] fenced = Fenced(Render(WithHealth(envelope, new CloudHealthDto("ok", "none", 412, 2, 2, [], 0, 0))));

        int flagged = Array.FindIndex(fenced, l => l.StartsWith(Heading, StringComparison.Ordinal));

        Assert.Equal(Heading, fenced[flagged]);
        Assert.Equal("    Also flagged in Cloud: 2 tests shown above.", fenced[^1]);
    }

    [Fact]
    public void ACappedListSaysSoAndTheFooterNamesTheCommand()
    {
        CloudHealthTestDto[] tests = [.. Enumerable.Range(0, 10).Select(_ => KnownFlaky)];
        string report = Render(WithHealth(Envelope(), new CloudHealthDto("ok", "none", 412, 14, 0, tests, 10, 14)));

        Assert.Contains("    Showing 10 of 14", Fenced(report));
        Assert.Contains(Lines(report), l => l.StartsWith("Showing 10 of 14 flagged in Cloud", StringComparison.Ordinal));
    }

    [Fact]
    public void AHealthThatIsShortPrintsItsRowsAndNothingMore()
    {
        // The reason line above the fence already says the Cloud data is partial (Amendment 7G).
        CloudHealthDto health = Listing(KnownFlaky) with { State = "partial", Reason = "budget" };
        string[] fenced = Fenced(Render(WithHealth(Envelope(), health)));

        Assert.Equal(5, fenced.Length);
    }

    [Fact]
    public void AVerdictLongerThanTheStatusColumnIsCutToIt()
    {
        CloudHealthTestDto row = KnownFlaky with { Digest = KnownFlaky.Digest with { Verdict = "quarantined-by-owner" } };
        string[] fenced = Fenced(Render(WithHealth(Envelope(), Listing(row))));

        Assert.Equal("    quarantined  CheckoutTests.Checkout_Completes", fenced[3]);
    }

    [Fact]
    public void AVerdictIsWordedOnce()
    {
        CloudHealthTestDto row = HealthTest("MyApp.Tests.CheckoutTests.Checkout_Completes", CloudDigest("no-history", score: null, flaky: "not-examined"));
        string[] fenced = Fenced(Render(WithHealth(Envelope(), Listing(row))));

        Assert.Equal("    no history   CheckoutTests.Checkout_Completes", fenced[3]);
        Assert.Equal("                 cloud: evidence sparse (ESS 4 of 10, 14 d)", fenced[4]);
    }

    [Fact]
    public void AFlakyStateOtherThanFlakyIsSaid()
    {
        CloudHealthTestDto row = HealthTest("MyApp.Tests.CheckoutTests.Checkout_Completes", CloudDigest("known-flaky", flaky: "inconclusive", score: 0.41, delta: null));
        string[] fenced = Fenced(Render(WithHealth(Envelope(), Listing(row))));

        Assert.Equal(
            ["                 cloud: confidence 0.41 (35 d)", "                        flaky inconclusive in 14 d"],
            fenced[4..6]);
    }

    [Fact]
    public void TheCleanBillIsWithheldWheneverTheSectionPrints()
    {
        // A count alone still says something sits above, which a pass glyph would contradict.
        string[] fenced = Fenced(Render(WithHealth(Envelope(), new CloudHealthDto("ok", "none", 412, 1, 1, [], 0, 0))));

        Assert.DoesNotContain(fenced, l => l.Contains("No findings.", StringComparison.Ordinal));
    }
}
