/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Rendering;
using Xping.Cli.Reporting;
using static Xping.Cli.Tests.Report.ReportFixtures;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The words for Xping Cloud's view of a test, from the values Cloud may send and the blocks it may
/// leave out (cloud-data-in-cli-cli-spec §7, §8, Amendment 3; contract §4, F4).
/// </summary>
/// <remarks>
/// The layout these words sit in is <see cref="CloudRenderingTests"/>' business; this pins the
/// words themselves, for the shapes the fixtures do not reach.
/// </remarks>
public sealed class CloudTextTests
{
    private static readonly CloudText Text = new(ReportGlyphs.Unicode);

    private static readonly CloudDigestDto Broken = CloudDigest("broken");

    // ---------------------------------------------------------------------
    // Finding rows
    // ---------------------------------------------------------------------

    [Fact]
    public void AnObservationWithoutAFlakyStateFallsBackToTheVerdict()
    {
        (_, IReadOnlyList<string> lines) = Row(nameof(FindingKind.TimingOut), Broken with { Flaky = null });

        Assert.Equal("cloud: broken on the canonical branch since 2026-10-11", lines[^1]);
    }

    [Fact]
    public void AnObservationWithNeitherSaysNoVerdict()
    {
        (_, IReadOnlyList<string> lines) = Row(nameof(FindingKind.TimingOut), Broken with { Flaky = null, Verdict = null });

        Assert.Equal("cloud: no verdict", lines[^1]);
    }

    [Fact]
    public void AVerdictKindWithoutAVerdictKeepsItsLocalLabel()
    {
        (string label, IReadOnlyList<string> lines) = Row(nameof(FindingKind.Flaky), Broken with { Verdict = null });

        Assert.Equal("local flaky", label);
        Assert.Equal("cloud: no verdict", lines[^1]);
    }

    [Fact]
    public void ARowWithoutAScoreHasNoConfidenceLine()
    {
        (_, IReadOnlyList<string> lines) = Row(nameof(FindingKind.Flaky), Broken with { Confidence = null });

        Assert.DoesNotContain(lines, l => l.Contains("confidence", StringComparison.Ordinal));
    }

    [Fact]
    public void ATestNotInAnyBoundProjectSaysSoWithoutAKey()
    {
        CloudDigestDto unknown = Broken with { State = "unknown" };
        FindingDto finding = Finding(nameof(FindingKind.Flaky), "high", "A.B", "headline") with { Cloud = unknown };
        ReportEnvelope envelope = Envelope(finding);
        CloudContextDto context = CloudContext(envelope) with { Projects = [] };

        Assert.Equal(["cloud: not in the project"], Text.Row(finding, context, "flaky").Lines);
    }

    // ---------------------------------------------------------------------
    // The confidence line and the latest-run line
    // ---------------------------------------------------------------------

    [Fact]
    public void InsufficientEvidenceWithoutItsNumbersSaysNothing()
    {
        CloudConfidenceDto confidence = Broken.Confidence! with { State = "insufficient-evidence", EvidenceLevel = null };

        Assert.Null(Text.ConfidenceLine(confidence));
    }

    [Fact]
    public void AnEvidenceWindowCloudLeftOutIsLeftOut()
    {
        CloudConfidenceDto confidence = Broken.Confidence! with
        {
            State = "insufficient-evidence",
            EffectiveSampleSize = 4,
            EvidenceLevel = "sparse",
            EvidenceWindow = null
        };

        Assert.Equal("cloud: evidence sparse (ESS 4 of 10)", Text.ConfidenceLine(confidence));
    }

    [Fact]
    public void AWindowCountedInRunsSaysRuns()
    {
        CloudConfidenceDto confidence = Broken.Confidence! with
        {
            Category = null,
            Delta = null,
            Window = new CloudWindowDto("runs", 50, null, null)
        };

        Assert.Equal("cloud: confidence 0.22 · 50 runs", Text.ConfidenceLine(confidence));
    }

    [Fact]
    public void TheLatestRunConfidenceWithoutAWindowIsTheScoreAlone()
    {
        CloudDigestDto digest = Broken with { Confidence = Broken.Confidence! with { Window = null } };

        Assert.Equal("confidence 0.22", LatestRun(digest, "unsignable")[0]);
    }

    [Fact]
    public void AMatchWithoutAMachineCountSaysItWasSeen()
    {
        CloudSignatureDto signature = Broken.Signatures[0] with { Machines = null };

        Assert.Equal("same error seen in Cloud", LatestRun(Broken, "matched", signature)[^1]);
    }

    [Fact]
    public void AMatchValueThisBuildDoesNotKnowIsPrintedAsSent()
    {
        Assert.Equal("resembles", LatestRun(Broken, "resembles")[^1]);
    }

    // ---------------------------------------------------------------------
    // Failure modes
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("unseen", "cloud: unseen")]
    [InlineData("unknown", "cloud: unknown")]
    [InlineData("unsignable", null)]
    public void AModeSaysWhatCloudKnowsOfItsError(string match, string? expected)
    {
        Assert.Equal(expected, Text.Mode(new CloudMatchDto(match, null)));
    }

    [Fact]
    public void AMatchedModeWithoutItsSignatureSaysSeen()
    {
        Assert.Equal("cloud: seen", Text.Mode(new CloudMatchDto("matched", null)));
    }

    [Fact]
    public void AMatchedModeSaysOnlyTheCountsCloudSent()
    {
        CloudSignatureDto signature = Broken.Signatures[0] with { Machines = null, CiRuns = null, LocalRuns = null, FirstSeenAt = null };

        Assert.Equal("cloud: same error", Text.Mode(new CloudMatchDto("matched", signature)));
    }

    [Fact]
    public void NoModeViewSaysNothing()
    {
        Assert.Null(Text.Mode(null));
    }

    // ---------------------------------------------------------------------
    // Pairs
    // ---------------------------------------------------------------------

    [Fact]
    public void ANameMismatchIsAPair()
    {
        CloudDigestDto digest = Broken with { Identity = "name-mismatch", TestName = "Shop.Tests.Old" };

        Assert.Contains(("cloud name", "differs: Shop.Tests.Old"), Pairs(digest));
    }

    [Fact]
    public void AnUnscoredConfidenceSaysWhyAndAMissingOneSaysNothing()
    {
        CloudDigestDto notScored = Broken with
        {
            Confidence = Broken.Confidence! with { State = "not-scored", Score = null, Category = null, Window = null, ComputedAt = null }
        };
        CloudDigestDto stateless = notScored with { Confidence = notScored.Confidence! with { State = null } };

        Assert.Equal("not scored", Value(notScored, "cloud confidence"));
        Assert.Equal("not scored", Value(stateless, "cloud confidence"));
        Assert.Null(Value(Broken with { Confidence = null }, "cloud confidence"));
        Assert.Null(Value(Broken with { Confidence = null }, "cloud evidence"));
    }

    [Fact]
    public void EvidenceWithoutTierCountsOrWindowIsTheLevelAndEss()
    {
        CloudDigestDto digest = Broken with
        {
            Confidence = Broken.Confidence! with { CanonicalRuns = null, SpeculativeRuns = null, UntrackedRuns = null, EvidenceWindow = null }
        };

        Assert.Equal("adequate · ESS 34", Value(digest, "cloud evidence"));
    }

    [Theory]
    [InlineData("inconclusive", "inconclusive · examined 2026-10-31 09:00 · 14 d window")]
    [InlineData("not-examined", "not examined in 14 d")]
    [InlineData("haunted", "haunted · examined 2026-10-31 09:00")]
    public void TheFlakyPairNeverReadsAsNotFlakyForAStateThatIsNot(string state, string expected)
    {
        Assert.Equal(expected, Value(CloudDigest("passing", flaky: state), "cloud flaky"));
    }

    [Theory]
    [InlineData("not-flaky", "not flaky · examined 2026-10-31 09:00")]
    [InlineData("inconclusive", "inconclusive · examined 2026-10-31 09:00")]
    [InlineData("not-examined", "not examined")]
    public void AFlakyPairWithoutAWindowLeavesItOut(string state, string expected)
    {
        CloudDigestDto digest = CloudDigest("passing", flaky: state);
        digest = digest with { Flaky = digest.Flaky! with { Window = null } };

        Assert.Equal(expected, Value(digest, "cloud flaky"));
    }

    [Fact]
    public void AFlakyStateWithoutDetailsIsTheWordAlone()
    {
        CloudDigestDto digest = CloudDigest("known-flaky", flaky: "flaky");
        digest = digest with { Flaky = digest.Flaky! with { Category = null, Severity = null, Since = null } };

        Assert.Equal("flaky", Value(digest, "cloud flaky"));
        Assert.Null(Value(digest with { Flaky = null }, "cloud flaky"));
    }

    [Fact]
    public void TheCanonicalPairSaysWhatCloudSent()
    {
        CloudDigestDto noHistory = CloudDigest("no-history");
        CloudDigestDto unexplained = Broken with { Canonical = null };
        CloudDigestDto empty = Broken with { Canonical = new CloudCanonicalDto(null, null, null, null, null, null, null) };
        CloudDigestDto bare = Broken with { Canonical = Broken.Canonical! with { FailingSince = null, LatestOutcome = null } };

        Assert.Equal("no canonical history", Value(noHistory, "cloud canonical"));
        Assert.Null(Value(unexplained, "cloud canonical"));
        Assert.Null(Value(empty, "cloud canonical"));
        Assert.Equal("failed 10/10 · 34 in a row · newest 2026-10-31 08:59", Value(bare, "cloud canonical"));
    }

    [Fact]
    public void ATransitionWithoutItsEndsOrTimeIsItsType()
    {
        CloudDigestDto digest = Broken with { LastTransition = new CloudTransitionDto("reliability-degraded", null, null, null) };

        Assert.Equal("reliability degraded", Value(digest, "cloud transition"));
        Assert.Null(Value(Broken with { LastTransition = null }, "cloud transition"));
    }

    [Fact]
    public void TheLifetimeWithoutALastRunIsTheCount()
    {
        Assert.Equal("1812 runs", Value(Broken with { LastExecutedAt = null }, "cloud lifetime"));
        Assert.Null(Value(Broken with { TotalRuns = null }, "cloud lifetime"));
    }

    [Fact]
    public void NoPairsWithoutAnAnswer()
    {
        Assert.Empty(Text.Pairs(null, "cloud ", null, fetched: true));
        Assert.Empty(Text.Pairs(Broken with { State = "unknown" }, "cloud ", null, fetched: true));
    }

    // ---------------------------------------------------------------------
    // The --test block
    // ---------------------------------------------------------------------

    [Fact]
    public void ASignatureSaysOnlyWhatCloudSent()
    {
        CloudSignatureDto empty = new(null, null, null, null, null, null, null, null, null, null, [], null);

        Assert.Equal(string.Empty, Text.Signature(empty, seenHere: false));
        Assert.Equal("1 machine", Text.Signature(empty with { Machines = 1 }, seenHere: false));
    }

    [Fact]
    public void TheLastRunSaysWhetherItIsScored()
    {
        CloudContextDto context = CloudContext(Envelope());
        CloudContextDto unscored = context with { Sessions = [context.Sessions[0] with { State = "processing", Scored = false }] };

        Assert.Equal("processing, not scored yet (session 4f1c0c9e)", Text.LastRun(unscored, CloudProject));
        Assert.Null(Text.LastRun(context, "another-project"));
        Assert.Null(Text.LastRun(null, CloudProject));
    }

    // ---------------------------------------------------------------------
    // The reason line
    // ---------------------------------------------------------------------

    [Fact]
    public void ANoProjectWithNoRowToNameStillSaysTheFix()
    {
        CloudContextDto context = CloudContext(Envelope(), "no-project", "not-found") with { Projects = [] };

        Assert.Equal("no project in this workspace · pass --project <key>", Text.Reason(context));
    }

    [Fact]
    public void ANoProjectNamesTheWorkspaceWhenTheSignInKnowsIt()
    {
        CloudContextDto context = CloudContext(Envelope(), "no-project", "not-found") with { Workspace = "Acme" };
        CloudProjectDto row = new("MyApp.Tests", null, null, "no-project", "not-found");

        Assert.Equal("no project in workspace Acme · pass --project <key>", Text.Reason(context with { Projects = [] }));
        Assert.Equal("no project in workspace Acme for MyApp.Tests · pass --project <key>", Text.Reason(context with { Projects = [row] }));
        Assert.Equal(
            "not in workspace Acme: project billing · pass --project <key>",
            Text.Reason(context with { Projects = [row with { ProjectKey = "billing" }] }));
    }

    [Fact]
    public void AWorkspaceNameIsMadeSafeForTheTerminal()
    {
        CloudContextDto context = CloudContext(Envelope(), "no-project", "not-found") with { Workspace = "Ac\u001b[2Jme", Projects = [] };

        Assert.DoesNotContain("\u001b", Text.Reason(context), StringComparison.Ordinal);
    }

    [Fact]
    public void NothingIsSaidWhenCloudAnsweredOrWasNeverAsked()
    {
        Assert.Null(Text.Reason(null));
        Assert.Null(Text.Reason(CloudContext(Envelope())));
        Assert.Null(Text.Reason(CloudContext(Envelope(), "not-attempted")));
    }

    private static (string Label, IReadOnlyList<string> Lines) Row(string kind, CloudDigestDto digest)
    {
        FindingDto finding = Finding(kind, "high", "A.B", "headline") with { Cloud = digest };
        return Text.Row(finding, CloudContext(Envelope(finding)), Text.Recorded(ReportVocabulary.LabelFor(kind)));
    }

    private static IReadOnlyList<string> LatestRun(CloudDigestDto digest, string match, CloudSignatureDto? signature = null)
    {
        LatestRunFailureDto failure = LatestRunFailure("new", "A", "passed before") with
        {
            Cloud = new CloudFailureDto(digest, match, signature)
        };

        return Text.LatestRun(failure, CloudContext(Envelope()));
    }

    private static IReadOnlyList<(string Label, string? Value)> Pairs(CloudDigestDto digest) =>
        Text.Pairs(digest, "cloud ", null, fetched: true);

    private static string? Value(CloudDigestDto digest, string label) =>
        Pairs(digest).FirstOrDefault(pair => pair.Label == label).Value;
}
