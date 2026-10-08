/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Rendering;
using Xping.Cli.Reporting;
using static Xping.Cli.Tests.Report.ReportFixtures;
using static Xping.Cli.Tests.Report.ReportText;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The three reserved Cloud slots of the text report, filled (cli-auth-cli-spec §11.3).
/// </summary>
public sealed class CloudRenderingTests
{
    private static readonly CloudTestDto Cloud = new(
        0.62, "moderately-reliable", "robust", 812, "stable", -0.03, true, null,
        new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));

    private static readonly OutputCapabilities Unicode = Drawn(ReportGlyphs.Unicode, color: false);

    [Fact]
    public void TheHeaderSaysCloudOnlyWhenSomethingOnThePageCameFromIt()
    {
        string plain = Lines(Render(Get("latest-run"), Unicode))[0];
        string enriched = Lines(Render(Enriched(), Unicode))[0];

        Assert.DoesNotContain("cloud", plain, StringComparison.Ordinal);
        Assert.Equal(plain + " · cloud", enriched);
    }

    [Fact]
    public void TheRowTrailerCarriesConfidenceRightAfterTheLocalEvidence()
    {
        ReportEnvelope enriched = Enriched(Cloud with { Category = "reliable" });
        enriched = enriched with { Findings = [enriched.Findings[0] with { Id = "f_1" }, .. enriched.Findings.Skip(1)] };

        string report = Render(enriched, Unicode);

        Assert.Contains("    evidence high | confidence 0.62 · reliable | -env-cluster | f_1", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ACategoryThatWouldPushTheTrailerPastTheFenceIsLeftToTheDetailView()
    {
        string[] trailers = [.. Lines(Render(Enriched(), Unicode)).Where(l => l.Contains("confidence 0.62", StringComparison.Ordinal) && l.Contains("f_", StringComparison.Ordinal))];

        Assert.NotEmpty(trailers);
        Assert.All(trailers, line =>
        {
            Assert.StartsWith("    evidence ", line, StringComparison.Ordinal);
            Assert.DoesNotContain("moderately", line, StringComparison.Ordinal);
            Assert.InRange(line.Length, 0, FenceWidth);
        });
    }

    [Fact]
    public void AsciiUsesItsOwnSeparatorInsideTheSegment()
    {
        // Not "|": that joins the trailer's own segments, and the category would read as one.
        ReportEnvelope enriched = Enriched(Cloud with { Category = "reliable" });
        enriched = enriched with { Findings = [enriched.Findings[0] with { Id = "f_1" }, .. enriched.Findings.Skip(1)] };

        string report = Render(enriched, Capabilities(redirected: true));

        Assert.Contains("evidence high | confidence 0.62 - reliable | -env-cluster | f_1", report, StringComparison.Ordinal);
        Assert.DoesNotContain("·", report, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHeaderMakesNoCloudClaimWhenNoRowShowsACloudValue()
    {
        string header = Lines(Render(Enriched(Cloud with { Confidence = null, Category = null }), Unicode))[0];

        Assert.DoesNotContain("cloud", header, StringComparison.Ordinal);
    }

    [Fact]
    public void InsufficientDataShowsNoTrailer()
    {
        string report = Render(Enriched(Cloud with { Confidence = null, Category = "insufficient-data" }), Unicode);

        Assert.DoesNotContain("confidence", report, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLatestRunContrastIsReplacedByCloudsStatement()
    {
        ReportEnvelope enriched = Enriched();
        string local = enriched.LatestRun!.Failures[0].Contrast;

        string[] section = LatestRunSection(Render(enriched, Unicode));

        Assert.Contains(section, line => line.Trim() == "confidence 0.62 over 812 runs");
        Assert.DoesNotContain(section, line => line.Contains(local, StringComparison.Ordinal));
    }

    [Fact]
    public void TheDetailViewAppendsLabelledCloudPairs()
    {
        ReportEnvelope full = Enriched();
        int row = full.Findings.ToList().FindIndex(f => f.Cloud != null) + 1;

        string[] lines = Lines(Render(Detail(full, row), Unicode));

        Assert.Contains(lines, l => l.Trim().StartsWith("cloud confidence", StringComparison.Ordinal) && l.EndsWith("0.62 (moderately reliable)", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Trim().StartsWith("cloud evidence", StringComparison.Ordinal) && l.EndsWith("robust, 812 runs", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Trim().StartsWith("cloud trend", StringComparison.Ordinal) && l.EndsWith("stable", StringComparison.Ordinal));
    }

    [Fact]
    public void JsonKeepsCloudValuesUnderCloudAndTheLocalFieldsLocal()
    {
        ReportEnvelope plain = Get("latest-run");
        ReportEnvelope enriched = Enriched();

        using JsonDocument before = JsonDocument.Parse(JsonSerializer.Serialize(plain, ReportJsonOptions.Default));
        using JsonDocument after = JsonDocument.Parse(JsonSerializer.Serialize(enriched, ReportJsonOptions.Default));

        JsonElement finding = after.RootElement.GetProperty("findings")[0];
        Assert.Equal(0.62, finding.GetProperty("cloud").GetProperty("confidence").GetDouble());
        Assert.Equal("moderately-reliable", finding.GetProperty("cloud").GetProperty("category").GetString());
        Assert.Equal(
            before.RootElement.GetProperty("findings")[0].GetProperty("metrics").GetRawText(),
            finding.GetProperty("metrics").GetRawText());
        Assert.Equal(
            before.RootElement.GetProperty("latestRun").GetProperty("failures")[0].GetProperty("contrast").GetString(),
            after.RootElement.GetProperty("latestRun").GetProperty("failures")[0].GetProperty("contrast").GetString());
        Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("findings")[0].GetProperty("cloud").ValueKind);
    }

    private static ReportEnvelope Enriched(CloudTestDto? cloud = null)
    {
        cloud ??= Cloud;
        ReportEnvelope envelope = Get("latest-run");

        return envelope with
        {
            Findings = [.. envelope.Findings.Select(f => f.Subject.Members == null ? f with { Cloud = cloud } : f)],
            LatestRun = envelope.LatestRun! with
            {
                Failures = [.. envelope.LatestRun.Failures.Select(f => f with { Cloud = cloud })]
            }
        };
    }
}
