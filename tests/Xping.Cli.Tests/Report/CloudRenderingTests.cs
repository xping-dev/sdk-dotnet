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
    [InlineData("unavailable", "timeout", "login", "cloud: unavailable (timeout) · showing local results only")]
    [InlineData("login-required", "none", "login", "cloud: login required · run xping login")]
    [InlineData("forbidden", "none", "api-key", "cloud: forbidden · API keys need the Team plan; run xping login instead")]
    [InlineData("forbidden", "none", "login", "cloud: forbidden · showing local results only")]
    [InlineData("no-project", "not-found", "login", "cloud: not in this workspace: project payments · pass --project <key>")]
    [InlineData("partial", "timeout", "login", "cloud: partial, 9 of 14 tests answered (timeout)")]
    [InlineData("throttled", "rate-limited", "login", "cloud: throttled (rate-limited)")]
    public void TheReasonLineSitsAboveTheFenceOnce(string status, string reason, string credential, string expected)
    {
        ReportEnvelope local = Get("latest-run");
        ReportEnvelope envelope = local with
        {
            Context = local.Context! with { Cloud = CloudContext(local, status, reason, credential) }
        };

        string[] lines = Lines(Render(envelope, Unicode));
        int fence = Array.IndexOf(lines, Fence);

        Assert.Equal(expected, Assert.Single(lines, l => l.StartsWith("cloud: ", StringComparison.Ordinal)));
        Assert.True(Array.IndexOf(lines, expected) < fence);
    }

    [Fact]
    public void ALongReasonLineWrapsAtTheFence()
    {
        ReportEnvelope local = Get("latest-run");
        CloudContextDto context = CloudContext(local, "no-project", "not-found");
        context = context with
        {
            Projects = [.. context.Projects.Select(p => p with { Assembly = "Company.Product.Billing.Integration.Tests", ProjectKey = null })]
        };

        string[] lines = Lines(Render(local with { Context = local.Context! with { Cloud = context } }, Unicode));
        int first = Array.FindIndex(lines, l => l.StartsWith("cloud: ", StringComparison.Ordinal));

        Assert.Equal("cloud: no project in this workspace for", lines[first]);
        Assert.Equal("       Company.Product.Billing.Integration.Tests · pass --project <key>", lines[first + 1]);
        Assert.All(lines.Skip(1).Take(first), l => Assert.InRange(l.Length, 0, FenceWidth));
    }

    [Fact]
    public void ANoProjectWithoutAKeyNamesTheAssembly()
    {
        ReportEnvelope local = Get("latest-run");
        CloudContextDto context = CloudContext(local, "no-project", "not-found");
        context = context with { Projects = [.. context.Projects.Select(p => p with { ProjectKey = null })] };

        string report = Render(local with { Context = local.Context! with { Cloud = context } }, Unicode);

        // Read across the wrap: the line is past the fence's width.
        string joined = string.Join(' ', Lines(report).Select(l => l.Trim()));
        Assert.Contains("cloud: no project in this workspace for MyApp.Tests · pass --project <key>", joined, StringComparison.Ordinal);
    }

    [Fact]
    public void AReportWideStatusIsNotRepeatedOnEveryRow()
    {
        string report = Render(Get("cloud-login-required"), Unicode);
        string[] lines = Lines(report);

        Assert.Single(lines, l => l.StartsWith("cloud: ", StringComparison.Ordinal));
        Assert.DoesNotContain(Fenced(report), l => l.TrimStart().StartsWith("cloud:", StringComparison.Ordinal));
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
        Assert.Equal("    local: always failing in 20 runs here", lines[row + 3]);
    }

    [Fact]
    public void AnObservationKeepsItsLabelAndGainsTheFlakyState()
    {
        string[] lines = Lines(Render(Get("cloud-broken"), Unicode));
        int row = Array.IndexOf(lines, "2.  HIGH  local timing out");

        Assert.True(row > 0);
        Assert.Equal("    cloud: not flaky in 14 d", lines[row + 3]);
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
        Assert.StartsWith("    cloud: passing on the canonical branch ", lines[row + 3], StringComparison.Ordinal);
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

    [Theory]
    [InlineData(nameof(FindingKind.DurationRegression), "slower")]
    [InlineData(nameof(FindingKind.RetryDeepening), "deeper retries")]
    [InlineData(nameof(FindingKind.ParallelSensitive), "concurrency")]
    [InlineData(nameof(FindingKind.TimeSensitive), "time sensitive")]
    public void AKindAVerdictDoesNotSpeakToKeepsItsLabel(string kind, string word)
    {
        // A "slower" row labelled "cloud passing" would answer a question the row did not ask
        // (Amendment 3S).
        string[] lines = Lines(Render(Single(kind, CloudDigest("passing")), Unicode));

        Assert.Contains($"1.  HIGH  local {word}", lines);
        Assert.Contains("    cloud: not flaky in 14 d", lines);
    }

    [Fact]
    public void ADisagreementWithNothingToSayStillSaysSomething()
    {
        CloudDigestDto digest = CloudDigest("passing", disagrees: true) with { Verdict = null, Flaky = null };

        string[] lines = Lines(Render(Single(nameof(FindingKind.Flaky), digest), Unicode));

        Assert.Contains("    cloud: no verdict", lines);
        Assert.DoesNotContain("    cloud: ", lines);
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

        Assert.Contains("    cloud: known as Shop.Tests.Old.Name", lines);
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
    // The confidence line (Amendment 3O)
    // ---------------------------------------------------------------------

    [Fact]
    public void TheConfidenceIsTheFirstLineUnderTheName()
    {
        string[] lines = Lines(Render(Get("cloud-broken"), Unicode));
        int row = Array.IndexOf(lines, "1.  HIGH  cloud broken");

        Assert.True(row > 0);
        Assert.Equal("    cloud: confidence 0.22 ▼0.61 · highly unreliable · 35 d", lines[row + 2]);
        Assert.Equal("    local: always failing in 20 runs here", lines[row + 3]);
    }

    [Fact]
    public void EveryScoredRowCarriesItsConfidenceWhateverItsLabel()
    {
        string[] lines = Lines(Render(Get("cloud-broken"), Unicode));

        // Four single-test findings, every one scored; the cluster is never asked about.
        Assert.Equal(4, lines.Count(l => l.StartsWith("    cloud: confidence ", StringComparison.Ordinal)));
    }

    [Fact]
    public void TheTrailerStaysLocal()
    {
        string report = Render(Get("cloud-broken"), Unicode);

        Assert.Contains("    evidence high | -env-cluster | f_c7b87f12 | .../SampleTests.cs:66", report, StringComparison.Ordinal);
        Assert.DoesNotContain(Lines(report), l => l.Contains(" | confidence", StringComparison.Ordinal));
    }

    [Fact]
    public void InsufficientEvidenceShowsTheEssAgainstWhatItNeedsAndNoScore()
    {
        CloudDigestDto digest = CloudDigest("no-history", score: null);

        Assert.Equal("cloud: evidence sparse (ESS 4 of 10, 14 d)", UnicodeText.ConfidenceLine(digest.Confidence));
    }

    [Fact]
    public void APendingScoreSaysSoAndASmallDeltaIsNotShown()
    {
        CloudDigestDto digest = CloudDigest("broken", delta: 0.004, freshness: "pending");

        Assert.Equal("cloud: confidence 0.22 · highly unreliable · 35 d · pending", UnicodeText.ConfidenceLine(digest.Confidence));
    }

    [Fact]
    public void AConfidenceCloudDidNotScoreHasNoLine()
    {
        CloudConfidenceDto notScored = CloudDigest("broken").Confidence! with { State = "not-scored", Score = null };

        Assert.Null(UnicodeText.ConfidenceLine(notScored));
        Assert.Null(UnicodeText.ConfidenceLine(null));
    }

    [Fact]
    public void AsciiUsesItsOwnGlyphsOnTheConfidenceLine()
    {
        var text = new CloudText(ReportGlyphs.Ascii);

        Assert.Equal("cloud: confidence 0.22 v0.61 - highly unreliable - 35 d", text.ConfidenceLine(CloudDigest("broken").Confidence));
        Assert.StartsWith("cloud: confidence 0.58 ^0.04 ", text.ConfidenceLine(CloudDigest("broken", score: 0.58, delta: 0.04).Confidence), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // ---------------------------------------------------------------------
    // Latest run (§7.5, Amendment 3G, 3P)
    // ---------------------------------------------------------------------

    [Fact]
    public void TheLocalSentenceStaysAndTheCloudLineFollowsIt()
    {
        string[] lines = LatestRunSection(Render(Get("cloud-broken"), Unicode));
        int sentence = Array.IndexOf(lines, "                 passed the previous 19 runs, failed just now");

        Assert.True(sentence > 0);
        Assert.Equal("                 cloud: confidence 0.22 (35 d) · broken since 2026-10-11", lines[sentence + 1]);
        Assert.Equal("                        same error on 6 machines and CI", lines[sentence + 2]);
    }

    [Fact]
    public void TheConfidenceLeadsTheLatestRunLine()
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

        Assert.Contains("                 cloud: confidence 0.97 (35 d) · passing", LatestRunSection(Render(envelope, Unicode)));
    }

    [Fact]
    public void AnUncountedCiIsNotClaimedEitherWay()
    {
        ReportEnvelope envelope = Get("cloud-broken");
        CloudDigestDto broken = CloudDigest("broken");
        CloudSignatureDto uncounted = broken.Signatures[0] with { CiRuns = null };
        envelope = envelope with
        {
            LatestRun = envelope.LatestRun! with
            {
                Failures = [envelope.LatestRun.Failures[0] with { Cloud = new CloudFailureDto(broken, "matched", uncounted) }]
            }
        };

        string[] section = LatestRunSection(Render(envelope, Unicode));

        Assert.Contains("                        same error on 6 machines", section);
        Assert.DoesNotContain(section, l => l.Contains("CI", StringComparison.Ordinal));
    }

    [Fact]
    public void AScoreBesideAStateThatSaysUnscoredIsNotShown()
    {
        // The finding row shows the evidence Cloud lacks; the latest-run line must not show a number.
        CloudDigestDto digest = CloudDigest("broken");
        digest = digest with { Confidence = digest.Confidence! with { State = "insufficient-evidence", EffectiveSampleSize = 4, EvidenceLevel = "sparse" } };

        ReportEnvelope envelope = Get("cloud-broken");
        envelope = envelope with
        {
            LatestRun = envelope.LatestRun! with
            {
                Failures = [envelope.LatestRun.Failures[0] with { Cloud = new CloudFailureDto(digest, "unsignable", null) }]
            }
        };

        Assert.Contains("                 cloud: broken since 2026-10-11", LatestRunSection(Render(envelope, Unicode)));
        Assert.Equal("cloud: evidence sparse (ESS 4 of 10, 14 d)", UnicodeText.ConfidenceLine(digest.Confidence));
    }

    [Fact]
    public void ALatestRunSegmentTooLongForItsLineWrapsInsideTheFence()
    {
        ReportEnvelope envelope = Get("cloud-partial");
        CloudContextDto context = envelope.Context!.Cloud!;
        context = context with
        {
            Projects = [.. context.Projects.Select(p => p with { ProjectKey = "payments-and-billing-integration-suite-eu-west" })]
        };

        string[] section = LatestRunSection(Render(envelope with { Context = envelope.Context with { Cloud = context } }, Unicode));

        Assert.Contains("                 cloud: not in project", section);
        Assert.Contains("                        payments-and-billing-integration-suite-eu-west", section);
        Assert.All(section, l => Assert.InRange(l.Length, 0, FenceWidth));
    }

    [Fact]
    public void ALongSegmentAfterAShortOneStartsItsOwnLine()
    {
        ReportEnvelope envelope = Get("cloud-broken");
        CloudDigestDto digest = CloudDigest("regressing-on-every-branch-since-the-dependency-upgrade-landed");
        envelope = envelope with
        {
            LatestRun = envelope.LatestRun! with
            {
                Failures = [envelope.LatestRun.Failures[0] with { Cloud = new CloudFailureDto(digest, "unsignable", null) }]
            }
        };

        string[] section = LatestRunSection(Render(envelope, Unicode));
        int first = Array.IndexOf(section, "                 cloud: confidence 0.22 (35 d)");

        Assert.True(first > 0);
        Assert.StartsWith("                        regressing-", section[first + 1], StringComparison.Ordinal);
        Assert.All(section, l => Assert.True(l.Length <= FenceWidth || !l.TrimStart().Contains(' ', StringComparison.Ordinal)));
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
    public void NoRunsInCloudIsALineNotASection()
    {
        ReportEnvelope envelope = Get("history-cloud");
        envelope = envelope with { Test = envelope.Test! with { Cloud = envelope.Test.Cloud! with { Runs = [] } } };

        Assert.Contains("RUNS IN CLOUD  no runs in Cloud", Lines(Render(envelope, Unicode)));
    }

    [Fact]
    public void ACloudRunRowWithRoomCarriesItsSignature()
    {
        string[] lines = Lines(Render(WithOneRun(run => run with { MachineName = "ci-1", Attempts = 3 }), Unicode));

        // The attempts would take the row past the fence, so only the signature joins it.
        Assert.Contains("  10-31 08:59  ✗ failed   canonical  CI  ci-1  main  0053b0e  ab12…", lines);
    }

    [Fact]
    public void ACloudRunRowWithRoomCarriesItsAttempts()
    {
        string[] lines = Lines(Render(WithOneRun(run => run with { Tier = null, MachineName = "ci-1", ErrorMessageHash = null, Attempts = 3 }), Unicode));

        Assert.Contains("  10-31 08:59  ✗ failed   —  CI  ci-1  main  0053b0e  (attempt 3)", lines);
    }

    [Fact]
    public void TheHistoryMarksADisagreementOnItsVerdict()
    {
        ReportEnvelope envelope = Get("history-cloud");
        envelope = envelope with
        {
            Findings = [envelope.Findings[0] with { Cloud = envelope.Findings[0].Cloud! with { DisagreesWithLocal = true } }, .. envelope.Findings.Skip(1)]
        };

        Assert.Contains("    verdict     known flaky (≠ local: flaky)", Lines(Render(envelope, Unicode)));
    }

    [Fact]
    public void ADigestWithoutALifetimeStillListsItsSignatures()
    {
        ReportEnvelope envelope = Get("history-cloud");
        CloudTestHistoryDto cloud = envelope.Test!.Cloud!;
        envelope = envelope with { Test = envelope.Test with { Cloud = cloud with { Digest = cloud.Digest! with { TotalRuns = null } } } };

        string[] lines = Lines(Render(envelope, Unicode));

        Assert.Contains(lines, l => l.StartsWith("    signatures  ", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.StartsWith("    lifetime", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ok", null, "    not answered")]
    [InlineData("partial", null, "    not answered")]
    [InlineData("ok", "unknown", "    not in project payments")]
    public void TheHistorySaysWhyThereIsNoDigest(string status, string? state, string expected)
    {
        ReportEnvelope envelope = Get("history-cloud");
        CloudTestHistoryDto cloud = envelope.Test!.Cloud!;
        CloudDigestDto? digest = state is null ? null : cloud.Digest! with { State = state };
        envelope = envelope with
        {
            Test = envelope.Test with { Cloud = cloud with { Digest = digest } },
            Context = envelope.Context! with { Cloud = envelope.Context.Cloud! with { Status = status } }
        };

        string[] lines = Lines(Render(envelope, Unicode));
        int heading = Array.FindIndex(lines, l => l.StartsWith("CLOUD", StringComparison.Ordinal));

        Assert.Equal(expected, lines[heading + 3]);
        Assert.DoesNotContain(lines, l => l.StartsWith("RUNS IN CLOUD", StringComparison.Ordinal));
    }

    [Fact]
    public void TheCloudHeadingWithoutAProjectSaysWhenItWasFetched()
    {
        ReportEnvelope envelope = Get("history-cloud");
        CloudContextDto context = envelope.Context!.Cloud!;
        envelope = envelope with { Context = envelope.Context with { Cloud = context with { Projects = [] } } };

        string heading = Lines(Render(envelope, Unicode)).First(l => l.StartsWith("CLOUD", StringComparison.Ordinal));

        Assert.EndsWith("  fetched 2026-11-01 10:02", heading, StringComparison.Ordinal);
        Assert.DoesNotContain("payments", heading, StringComparison.Ordinal);
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

    private static ReportEnvelope WithOneRun(Func<CloudRunDto, CloudRunDto> edit)
    {
        ReportEnvelope envelope = Get("history-cloud");
        CloudTestHistoryDto cloud = envelope.Test!.Cloud!;

        return envelope with { Test = envelope.Test with { Cloud = cloud with { Runs = [edit(cloud.Runs[0])] } } };
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
