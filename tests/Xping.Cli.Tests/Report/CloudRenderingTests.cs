/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Text.Json;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Rendering;
using Xping.Cli.Reporting;
using static Xping.Cli.Tests.Report.ReportFixtures;
using static Xping.Cli.Tests.Report.ReportText;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// Xping Cloud's layer in the text report (cloud-data-in-cli-cli-spec §7, §8, Amendment 3).
/// </summary>
public sealed class CloudRenderingTests
{
    private static readonly OutputCapabilities Unicode = Drawn(ReportGlyphs.Unicode, color: false);

    private static readonly OutputCapabilities Ascii = Drawn(ReportGlyphs.Ascii, color: false);

    private static readonly CloudText UnicodeText = new(ReportGlyphs.Unicode);

    // ---------------------------------------------------------------------
    // Without a Cloud layer, the local report (contract R1)
    // ---------------------------------------------------------------------

    [Fact]
    public void AReportTheCloudLayerDidNotRunForIsTheLocalReport()
    {
        ReportEnvelope local = Get("latest-run");
        ReportEnvelope notAttempted = local with
        {
            Context = local.Context! with { Cloud = CloudContext(local, "not-attempted") }
        };

        Assert.Equal(Render(local, Unicode), Render(notAttempted, Unicode));
        Assert.Equal(Render(local, Ascii), Render(notAttempted, Ascii));
    }

    // ---------------------------------------------------------------------
    // Header (§7.5) and reason line (§7.6)
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("cloud-broken", " · cloud")]
    [InlineData("cloud-partial", " · cloud (partial)")]
    public void TheHeaderSaysCloudWhenItAnswered(string fixture, string token)
    {
        string header = Lines(Render(Get(fixture), Unicode))[0];

        Assert.EndsWith(token, header, StringComparison.Ordinal);
    }

    [Fact]
    public void TheHeaderMakesNoCloudClaimWhenNothingAnswered()
    {
        string header = Lines(Render(Get("cloud-login-required"), Unicode))[0];

        Assert.DoesNotContain("cloud", header, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unavailable", "timeout", "login", "cloud  unavailable (timeout) · showing local results only")]
    [InlineData("login-required", "none", "login", "cloud  login required · run xping login")]
    [InlineData("forbidden", "none", "api-key", "cloud  forbidden · API keys need the Team plan; run xping login instead")]
    [InlineData("forbidden", "none", "login", "cloud  forbidden · showing local results only")]
    [InlineData("no-project", "not-found", "login", "cloud  not in this workspace: project payments · pass --project <key>")]
    [InlineData("partial", "timeout", "login", "cloud  partial: 9 of 14 tests answered (timeout)")]
    [InlineData("throttled", "rate-limited", "login", "cloud  throttled (rate-limited)")]
    public void TheReasonLineSitsAboveTheFenceOnce(string status, string reason, string credential, string expected)
    {
        ReportEnvelope local = Get("latest-run");
        ReportEnvelope envelope = local with
        {
            Context = local.Context! with { Cloud = CloudContext(local, status, reason, credential) }
        };

        string[] lines = Lines(Render(envelope, Unicode));
        int fence = Array.IndexOf(lines, Fence);

        Assert.Equal(expected, Assert.Single(lines, l => l.StartsWith("cloud  ", StringComparison.Ordinal)));
        Assert.True(Array.IndexOf(lines, expected) < fence);
    }

    [Fact]
    public void ANoProjectWithoutAKeyNamesTheAssembly()
    {
        ReportEnvelope local = Get("latest-run");
        CloudContextDto context = CloudContext(local, "no-project", "not-found");
        context = context with { Projects = [.. context.Projects.Select(p => p with { ProjectKey = null })] };

        string report = Render(local with { Context = local.Context! with { Cloud = context } }, Unicode);

        Assert.Contains("cloud  no project in this workspace for MyApp.Tests · pass --project <key>", report, StringComparison.Ordinal);
    }

    [Fact]
    public void AReportWideStatusIsNotRepeatedOnEveryRow()
    {
        string[] lines = Lines(Render(Get("cloud-login-required"), Unicode));

        Assert.DoesNotContain(lines, l => l.TrimStart().StartsWith("cloud:", StringComparison.Ordinal));
        Assert.Contains("1.  HIGH  local always failing", lines);
        Assert.Contains(lines, l => l.StartsWith("5.  HIGH  local flaky ", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // The layered label (§7.2, contract L1–L4)
    // ---------------------------------------------------------------------

    [Fact]
    public void AVerdictTakesTheLabelAndTheLocalKindMovesUnderTheName()
    {
        string[] lines = Lines(Render(Get("cloud-broken"), Unicode));
        int row = Array.IndexOf(lines, "1.  HIGH  cloud broken");

        Assert.True(row > 0);
        Assert.Equal("    SampleTests.ExceptionTest_ThrowsOnMissingDependency", lines[row + 1]);
        Assert.Equal("    local: always failing in 20 runs here", lines[row + 2]);
    }

    [Fact]
    public void AnObservationKeepsItsLabelAndGainsTheFlakyState()
    {
        string[] lines = Lines(Render(Get("cloud-broken"), Unicode));
        int row = Array.IndexOf(lines, "2.  HIGH  local timing out");

        Assert.True(row > 0);
        Assert.Equal("    cloud: not flaky in 14 d", lines[row + 2]);
    }

    [Fact]
    public void NoCanonicalHistoryKeepsTheLocalLabelAndSaysWhy()
    {
        string[] lines = Lines(Render(Single(nameof(FindingKind.Flaky), CloudDigest("no-history", score: null)), Unicode));

        Assert.Contains("1.  HIGH  local flaky", lines);
        Assert.Contains("    cloud: no canonical history", lines);
    }

    [Theory]
    [InlineData("unicode", "1.  HIGH  local always failing ≠ cloud passing")]
    [InlineData("ascii", "1.  HIGH  local always failing != cloud passing")]
    public void ADisagreementKeepsTheLocalLabelFirstAndShowsBoth(string mode, string label)
    {
        string[] lines = Lines(Render(Get("cloud-disagree"), mode == "ascii" ? Ascii : Unicode));
        int row = Array.IndexOf(lines, label);

        Assert.True(row > 0);
        Assert.StartsWith("    cloud: passing on the canonical branch ", lines[row + 2], StringComparison.Ordinal);
    }

    [Fact]
    public void PartialRowsSayWhetherCloudAnsweredOrDoesNotKnowTheTest()
    {
        string[] lines = Lines(Render(Get("cloud-partial"), Unicode));

        Assert.Contains("    cloud: not in project payments", lines);
        Assert.Equal(2, lines.Count(l => l == "    cloud: not answered"));
    }

    [Fact]
    public void AGroupFindingIsLocalAndAddsNoCloudLine()
    {
        string[] lines = Lines(Render(Get("cloud-broken"), Unicode));
        int row = Array.IndexOf(lines, "3.  HIGH  local broken fixture");

        Assert.True(row > 0);
        Assert.DoesNotContain(lines.Skip(row + 1).TakeWhile(l => l.Length > 0), l => l.Contains("cloud", StringComparison.Ordinal));
    }

    [Fact]
    public void AVerdictThisBuildDoesNotKnowIsPrintedAsSent()
    {
        string[] lines = Lines(Render(Single(nameof(FindingKind.Flaky), CloudDigest("regressing")), Unicode));

        Assert.Contains("1.  HIGH  local flaky", lines);
        Assert.Contains("    cloud: regressing", lines);
    }

    [Fact]
    public void ANameMismatchIsSaidUnderTheName()
    {
        CloudDigestDto digest = CloudDigest("broken") with { Identity = "name-mismatch", TestName = "Shop.Tests.Old.Name" };

        string[] lines = Lines(Render(Single(nameof(FindingKind.Flaky), digest), Unicode));

        Assert.Contains("    cloud name differs: Shop.Tests.Old.Name", lines);
    }

    [Theory]
    [InlineData("inconclusive", "flaky inconclusive in 14 d")]
    [InlineData("not-examined", "not examined for flakiness in 14 d")]
    public void AFlakyStateThatIsNotANegativeIsNeverReadAsNotFlaky(string state, string phrase)
    {
        string[] lines = Lines(Render(Single(nameof(FindingKind.RetryMasked), CloudDigest("passing", flaky: state)), Unicode));

        Assert.Contains("    cloud: " + phrase, lines);
        Assert.DoesNotContain(lines, l => l.Contains("not flaky", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // The trailer (§7.3, Amendment 3C)
    // ---------------------------------------------------------------------

    [Fact]
    public void TheTrailerCarriesTheScoreWithItsWindowRightAfterTheLocalEvidence()
    {
        string report = Render(Single(nameof(FindingKind.Flaky), CloudDigest("broken")), Unicode);

        Assert.Contains("    evidence moderate | confidence 0.22 (35 d) | -env-cluster | f_1", report, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTrailerShortensByTheDeltaThenTheEvidenceThenTheSegment()
    {
        CloudDigestDto digest = CloudDigest("broken");
        const string full = "confidence 0.22 ▼0.61 (35 d) · evidence adequate (ESS 34, 14 d)";
        const string noDelta = "confidence 0.22 (35 d) · evidence adequate (ESS 34, 14 d)";
        const string plain = "confidence 0.22 (35 d)";

        Assert.Equal(full, UnicodeText.Trailer(digest, full.Length));
        Assert.Equal(noDelta, UnicodeText.Trailer(digest, full.Length - 1));
        Assert.Equal(plain, UnicodeText.Trailer(digest, noDelta.Length - 1));
        Assert.Null(UnicodeText.Trailer(digest, plain.Length - 1));
    }

    [Fact]
    public void TheTrailerNeverDropsAWindowOrAnEssOnItsOwn()
    {
        CloudDigestDto digest = CloudDigest("broken");

        foreach (int room in Enumerable.Range(0, 80))
        {
            if (UnicodeText.Trailer(digest, room) is not { } segment)
                continue;

            Assert.Contains("(35 d)", segment, StringComparison.Ordinal);
            Assert.True(!segment.Contains("evidence", StringComparison.Ordinal) || segment.Contains("(ESS 34, 14 d)", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void InsufficientEvidenceShowsTheEssAgainstWhatItNeedsAndNoScore()
    {
        Assert.Equal("evidence sparse (ESS 4 of 10, 14 d)", UnicodeText.Trailer(CloudDigest("no-history", score: null), 80));
    }

    [Fact]
    public void APendingScoreSaysSoAndASmallDeltaIsNotShown()
    {
        string? segment = UnicodeText.Trailer(CloudDigest("broken", delta: 0.004, freshness: "pending"), 22 + 10);

        Assert.Equal("confidence 0.22 (35 d) · pending", segment);
    }

    [Fact]
    public void TheTrailerKeepsThePathsFileName()
    {
        // The local path is never cut for Cloud: with it, the segment does not fit, so it is left
        // to the detail view.
        string report = Render(Get("cloud-broken"), Unicode);

        Assert.Contains("    evidence high | -env-cluster | f_c7b87f12 | .../SampleTests.cs:66", report, StringComparison.Ordinal);
    }

    [Fact]
    public void AsciiUsesItsOwnGlyphsInsideTheSegment()
    {
        var text = new CloudText(ReportGlyphs.Ascii);

        Assert.Equal("confidence 0.22 v0.61 (35 d) - evidence adequate (ESS 34, 14 d)", text.Trailer(CloudDigest("broken"), 80));
        Assert.StartsWith("confidence 0.58 ^0.04 (35 d)", text.Trailer(CloudDigest("broken", score: 0.58, delta: 0.04), 80), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // Latest run (§7.5, Amendment 3G)
    // ---------------------------------------------------------------------

    [Fact]
    public void TheLocalSentenceStaysAndTheCloudLineFollowsIt()
    {
        string[] lines = LatestRunSection(Render(Get("cloud-broken"), Unicode));
        int sentence = Array.IndexOf(lines, "                 passed the previous 19 runs, failed just now");

        Assert.True(sentence > 0);
        Assert.Equal("                 cloud  broken since 2026-10-11", lines[sentence + 1]);
        Assert.Equal("                        same error on 6 machines and CI", lines[sentence + 2]);
    }

    [Fact]
    public void TheConfidenceJoinsTheLatestRunLineWhereItFits()
    {
        ReportEnvelope envelope = Get("cloud-broken");
        CloudDigestDto passing = CloudDigest("passing", score: 0.97);
        envelope = envelope with
        {
            LatestRun = envelope.LatestRun! with
            {
                Failures = [envelope.LatestRun.Failures[0] with { Cloud = new CloudFailureDto(passing, "unsignable", null) }]
            }
        };

        Assert.Contains("                 cloud  passing · confidence 0.97 (35 d)", LatestRunSection(Render(envelope, Unicode)));
    }

    [Fact]
    public void AMatchOnlyOnDeveloperMachinesSaysNotInCi()
    {
        ReportEnvelope envelope = Get("cloud-broken");
        CloudDigestDto passing = CloudDigest("passing", score: 0.97);
        envelope = envelope with
        {
            LatestRun = envelope.LatestRun! with
            {
                Failures = [envelope.LatestRun.Failures[0] with { Cloud = new CloudFailureDto(passing, "matched", passing.Signatures[1]) }]
            }
        };

        Assert.Contains(LatestRunSection(Render(envelope, Unicode)), l => l.Contains("same error on 1 machine, not in CI", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // Detail view (§7.4) and failure modes
    // ---------------------------------------------------------------------

    [Fact]
    public void TheDetailViewAppendsLabelledCloudPairs()
    {
        string[] lines = Lines(Render(Get("detail-cloud"), Unicode));

        string[] labels =
        [
            "cloud verdict", "cloud confidence", "cloud evidence", "cloud flaky", "cloud canonical",
            "cloud transition", "cloud lifetime", "cloud fetched"
        ];

        Assert.All(labels, label => Assert.Contains(lines, l => l.StartsWith("    " + label + " ", StringComparison.Ordinal)));
        Assert.Contains("    cloud lifetime    1812 runs · last 2026-10-31 08:59", lines);
        Assert.Contains("    cloud fetched     2026-11-01 10:02", lines);
    }

    [Fact]
    public void ADisagreementIsMarkedOnTheDetailVerdict()
    {
        ReportEnvelope full = Get("cloud-disagree");

        string[] lines = Lines(Render(Detail(full, 1), Unicode));

        Assert.Contains("    cloud verdict     passing on the canonical branch (≠ local)", lines);
    }

    [Fact]
    public void APendingScoreIsSaidBesideWhenItWasFetched()
    {
        ReportEnvelope full = Get("cloud-known-flaky");

        string[] lines = Lines(Render(Detail(full, 1), Unicode));

        Assert.Contains(lines, l => l.EndsWith("2026-11-01 10:02 (pending: newer runs not yet", StringComparison.Ordinal));
    }

    [Fact]
    public void EachFailureModeSaysWhetherCloudHasSeenItsError()
    {
        string[] lines = Lines(Render(Get("detail-cloud"), Unicode));

        Assert.Contains("    cloud: same error on 6 machines · 34 CI · 2 local · since 2026-10-11", lines);
        Assert.Contains("    cloud: unseen", lines);

        // The third mode has no message, so no signature: nothing to match, nothing said.
        string[] modes = [.. lines.SkipWhile(l => !l.StartsWith("FAILURE MODES", StringComparison.Ordinal))];
        Assert.Equal(2, modes.Count(l => l.StartsWith("    cloud: ", StringComparison.Ordinal)));
    }

    // ---------------------------------------------------------------------
    // --test (§8, Amendment 3H, 3N)
    // ---------------------------------------------------------------------

    [Fact]
    public void TheHistoryPutsCloudBetweenTheCountsAndTheRuns()
    {
        string[] lines = Lines(Render(Get("history-cloud"), Unicode));

        int counts = Array.FindIndex(lines, l => l.StartsWith("ran in ", StringComparison.Ordinal));
        int cloud = Array.FindIndex(lines, l => l.StartsWith("CLOUD ", StringComparison.Ordinal));
        int cloudRuns = Array.FindIndex(lines, l => l.StartsWith("RUNS IN CLOUD ", StringComparison.Ordinal));
        int runs = Array.FindIndex(lines, l => l.StartsWith("RUNS ", StringComparison.Ordinal) && !l.StartsWith("RUNS IN", StringComparison.Ordinal));

        Assert.True(counts < cloud && cloud < cloudRuns && cloudRuns < runs);
        Assert.EndsWith("payments · fetched 2026-11-01 10:02", lines[cloud], StringComparison.Ordinal);
        Assert.EndsWith("newest 4 runs in Cloud (retention 365 days)", lines[cloudRuns], StringComparison.Ordinal);
        Assert.Contains("  more runs in Cloud", lines);
    }

    [Fact]
    public void TheSignaturesMarkTheOneThisMachineFailedWith()
    {
        string[] lines = Lines(Render(Get("history-cloud"), Unicode));

        Assert.Contains("    signatures  ab12… InvalidOperationException · 36 runs · 6 machines ·", lines);
        Assert.Contains("                34 CI · since 2026-10-11 (seen here)", lines);
    }

    [Fact]
    public void ACloudRunRowFitsTheFenceAndCutsLongNamesFromTheLeft()
    {
        string[] lines = Lines(Render(Get("history-cloud"), Unicode));

        Assert.Contains("  10-31 03:59  ✗ timeout  speculative  CI  ...arge-04  ...ender  0053b0e", lines);
        Assert.Contains("  10-31 07:10  ✓ passed   untracked    —   adrian-mbp  feat/x    0053b0e", lines);
    }

    [Theory]
    [InlineData("budget")]
    [InlineData("unavailable")]
    public void RunsThatWereNotFetchedSaySo(string state)
    {
        ReportEnvelope envelope = Get("history-cloud");
        envelope = envelope with { Test = envelope.Test! with { Cloud = envelope.Test.Cloud! with { Runs = [], RunsState = state } } };

        Assert.Contains($"RUNS IN CLOUD  not fetched ({state})", Lines(Render(envelope, Unicode)));
    }

    [Fact]
    public void TheHistoryHasNoCloudBlockWhenTheLayerDidNotRun()
    {
        ReportEnvelope local = Get("history-flaky");
        ReportEnvelope notAttempted = local with { Context = local.Context! with { Cloud = CloudContext(local, "not-attempted") } };

        Assert.Equal(Render(local, Unicode), Render(notAttempted, Unicode));
    }

    [Fact]
    public void TheHistorySaysWhyCloudIsMissingUnderItsHeading()
    {
        ReportEnvelope local = Get("history-flaky");
        ReportEnvelope down = local with { Context = local.Context! with { Cloud = CloudContext(local, "unavailable", "timeout") } };

        string[] lines = Lines(Render(down, Unicode));
        int cloud = Array.FindIndex(lines, l => l.StartsWith("CLOUD", StringComparison.Ordinal));

        Assert.True(cloud > 0);
        Assert.Equal("    unavailable (timeout) · showing local results only", lines[cloud + 3]);
        Assert.DoesNotContain(lines, l => l.StartsWith("RUNS IN CLOUD", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------------
    // JSON (§6.6)
    // ---------------------------------------------------------------------

    [Fact]
    public void JsonKeepsCloudValuesUnderCloudAndTheLocalFieldsLocal()
    {
        ReportEnvelope plain = Get("latest-run");
        ReportEnvelope enriched = Get("cloud-broken");

        using JsonDocument before = JsonDocument.Parse(JsonSerializer.Serialize(plain, ReportJsonOptions.Default));
        using JsonDocument after = JsonDocument.Parse(JsonSerializer.Serialize(enriched, ReportJsonOptions.Default));

        JsonElement finding = after.RootElement.GetProperty("findings")[0];
        Assert.Equal("broken", finding.GetProperty("cloud").GetProperty("verdict").GetString());
        Assert.Equal("AlwaysFailing", finding.GetProperty("kind").GetString());
        Assert.Equal(
            before.RootElement.GetProperty("findings")[0].GetProperty("headline").GetString(),
            finding.GetProperty("headline").GetString());
        Assert.Equal(
            before.RootElement.GetProperty("latestRun").GetProperty("failures")[0].GetProperty("contrast").GetString(),
            after.RootElement.GetProperty("latestRun").GetProperty("failures")[0].GetProperty("contrast").GetString());
    }

    private static ReportEnvelope Single(string kind, CloudDigestDto digest)
    {
        FindingDto finding = Finding(kind, "high", "MyApp.Tests.CheckoutTests.Completes", "failed 7 of 20 executions (35%)") with
        {
            Id = "f_1",
            EvidenceSessions = 20,
            Cloud = digest
        };

        ReportEnvelope envelope = Envelope(finding);
        return envelope with { Context = envelope.Context! with { Cloud = CloudContext(envelope) } };
    }
}
