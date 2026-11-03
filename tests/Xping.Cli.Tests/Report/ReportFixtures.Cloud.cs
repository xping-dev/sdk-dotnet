/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The envelopes with Xping Cloud's layer stitched in (cloud-data-in-cli-cli-spec §17.3).
/// </summary>
/// <remarks>
/// Built on the local fixtures, so a Cloud golden differs from its local twin by the Cloud layer
/// alone. Every value here is one the stitcher could have produced; none is a renderer's output.
/// </remarks>
internal static partial class ReportFixtures
{
    /// <summary>The project every Cloud fixture's assemblies are bound to.</summary>
    public const string CloudProject = "payments";

    /// <summary>A signature this machine failed with, and Cloud has seen across machines.</summary>
    public const string CloudHash = "ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12cd34ef56ab12";

    private static readonly DateTimeOffset Computed = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Fetched = new(2026, 11, 1, 10, 2, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset FailingSince = new(2026, 10, 11, 9, 14, 0, TimeSpan.Zero);

    /// <summary>
    /// What a report says about Cloud when it answered with <paramref name="status"/>, with one
    /// project row per assembly the envelope names.
    /// </summary>
    public static CloudContextDto CloudContext(
        ReportEnvelope envelope, string status = "ok", string reason = "none", string credential = "login")
    {
        ArgumentNullException.ThrowIfNull(envelope);

        IEnumerable<string?> assemblies = envelope.Findings.Select(f => f.Subject.Assembly)
            .Concat(envelope.LatestRun?.Failures.Select(f => f.Subject.Assembly) ?? [])
            .Concat([envelope.Test?.Test?.Assembly, envelope.Context?.Assembly]);

        bool answered = status is "ok" or "partial";

        return new CloudContextDto(
            status,
            reason,
            answered ? 1840 : 0,
            "https://app.xping.io",
            credential,
            credential == "login" ? "01JCLOUDWORKSPACE0000000000" : null,

            // No name, as a sign-in from before the workspace_name claim: the goldens keep the
            // "this workspace" wording, and the named form is tested in CloudTextTests.
            null,
            answered ? Fetched : null,
            answered ? "emh-1" : null,
            answered ? 365 : null,
            14,
            status == "partial" ? 9 : answered ? 14 : 0,
            [
                .. assemblies.OfType<string>().Distinct(StringComparer.Ordinal).Select(assembly =>
                    new CloudProjectDto(assembly, CloudProject, "session", status, reason))
            ],
            answered ? [new CloudSessionDto("4f1c0c9e-5d2a-4b7e-9c1f-0a2b3c4d5e6f", CloudProject, "complete", true, true)] : []);
    }

    /// <summary>
    /// A digest Cloud would send for a test with a canonical history, overridden per fixture.
    /// </summary>
    public static CloudDigestDto CloudDigest(
        string verdict,
        string flaky = "not-flaky",
        double? score = 0.22,
        double? delta = -0.61,
        string freshness = "current",
        bool disagrees = false) =>
        new(
            "ok",
            Fetched,
            disagrees,
            null,
            verdict,
            null,
            new DateTimeOffset(2026, 10, 31, 8, 59, 0, TimeSpan.Zero),
            1812,
            new CloudConfidenceDto(
                score is null ? "insufficient-evidence" : "scored",
                score,
                score is null ? null : score - 0.08,
                score is null ? null : score + 0.09,
                Category(score),
                "significant-degradation",
                delta,
                score is null ? 4 : 34,
                score is null ? "sparse" : "adequate",
                34,
                0,
                2,
                new CloudWindowDto("days", 35, Computed, null),
                new CloudWindowDto("days", 14, Computed, null),
                Computed,
                freshness),
            new CloudFlakyDto(
                flaky,
                flaky == "flaky" ? "timing" : null,
                flaky == "flaky" ? "major" : null,
                flaky == "flaky" ? new DateTimeOffset(2026, 10, 2, 7, 0, 0, TimeSpan.Zero) : null,
                Computed,
                new CloudWindowDto("days", 14, Computed, null),
                Computed,
                "current"),
            verdict == "no-history"
                ? null
                : new CloudCanonicalDto(
                    10,
                    verdict == "broken" ? 10 : verdict == "passing" ? 0 : 4,
                    verdict == "broken" ? 34 : 0,
                    verdict == "broken" ? FailingSince : null,
                    verdict == "broken" ? "failed" : "passed",
                    new DateTimeOffset(2026, 10, 31, 8, 59, 0, TimeSpan.Zero),
                    new CloudWindowDto("runs", 10, null, new DateTimeOffset(2026, 10, 29, 3, 0, 0, TimeSpan.Zero))),
            verdict == "broken"
                ? new CloudTransitionDto("reliability-degraded", "reliable", "highly-unreliable", FailingSince)
                : null,
            [
                Signature(CloudHash, "InvalidOperationException", requested: true, runs: 36, ci: 34, local: 2, machines: 6),
                Signature("c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0", "TimeoutException", requested: false, runs: 2, ci: 0, local: 2, machines: 1)
            ]);

    // Cloud's bands, so a fixture never pairs a score with a category Cloud would not send.
    private static string Category(double? score) => score switch
    {
        null => "insufficient-data",
        >= 0.9 => "highly-reliable",
        >= 0.75 => "reliable",
        >= 0.5 => "moderately-reliable",
        >= 0.3 => "unreliable",
        _ => "highly-unreliable"
    };

    private static CloudSignatureDto Signature(
        string hash, string type, bool requested, int runs, int ci, int local, int machines) =>
        new(
            hash, type, requested, runs, ci, ci, local, machines,
            FailingSince, new DateTimeOffset(2026, 10, 31, 8, 59, 0, TimeSpan.Zero),
            ["c0ffee00c0ffee00"],
            new CloudWindowDto("days", 35, Computed, null));

    private static CloudFailureDto Failure(CloudDigestDto digest, string match) =>
        new(digest, match, match == "matched" ? digest.Signatures[0] : null);

    // The local fixtures leave EvidenceSessions at the helper's default; the Cloud layer prints it
    // ("local: flaky in 20 runs here"), so it is the window's run count, as the builder would have it.
    private static ReportEnvelope WithCloud(ReportEnvelope envelope, CloudContextDto context) =>
        envelope with
        {
            Context = (envelope.Context ?? new ContextDto(null, null, null)) with { Cloud = context },
            Findings = [.. envelope.Findings.Select(f => f with { EvidenceSessions = envelope.Window.SessionCount })]
        };

    // The latest-run fixture, every row answered: a test broken on the canonical branch with this
    // machine's error, observations with Cloud's flaky state beside them, and a fresh test Cloud has
    // no canonical history for.
    private static ReportEnvelope CloudBroken()
    {
        ReportEnvelope envelope = Build("latest-run");

        CloudDigestDto broken = CloudDigest("broken");
        CloudDigestDto[] digests =
        [
            broken,
            CloudDigest("passing", score: 0.97, delta: 0.002),
            CloudDigest("unstable", flaky: "flaky", score: 0.41, delta: null),
            CloudDigest("known-flaky", flaky: "flaky", score: 0.41, delta: null)
        ];

        int next = 0;

        envelope = envelope with
        {
            Findings = [.. envelope.Findings.Select(f => f.Subject.Members == null ? f with { Cloud = digests[next++] } : f)],
            LatestRun = envelope.LatestRun! with
            {
                Failures =
                [
                    envelope.LatestRun.Failures[0] with { Cloud = Failure(broken, "matched") },
                    envelope.LatestRun.Failures[1] with { Cloud = Failure(CloudDigest("no-history", score: null, flaky: "not-examined"), "unseen") }
                ]
            }
        };

        return WithCloud(envelope, CloudContext(envelope));
    }

    // A local flaky finding Cloud calls known flaky, with a score still pending a rescore.
    private static ReportEnvelope CloudKnownFlaky()
    {
        FindingDto finding = Finding(
            nameof(FindingKind.Flaky), "medium",
            "MyApp.Tests.CheckoutTests.Checkout_CompletesWithinTheServiceBudget",
            "failed 4 of 20 executions (20%) in 4 of 20 runs, 1 failure mode",
            "tests/MyApp.Tests/CheckoutTests.cs", 27) with
        { Id = "f_5da7c018" };

        ReportEnvelope envelope = Envelope(
            finding with { Cloud = CloudDigest("known-flaky", flaky: "flaky", score: 0.58, delta: 0.04, freshness: "pending") });

        return WithCloud(envelope, CloudContext(envelope));
    }

    // Contract L3: a local flaky finding Cloud has examined and calls not flaky, and an always
    // failing one that passes on the canonical branch. Both keep their local label.
    private static ReportEnvelope CloudDisagree()
    {
        ReportEnvelope envelope = Build("spec-section-3");

        envelope = envelope with
        {
            Findings =
            [
                .. envelope.Findings.Select(f => f.Kind switch
                {
                    "AlwaysFailing" => f with { Cloud = CloudDigest("passing", score: 0.97, delta: null, disagrees: true) },
                    "Flaky" => f with { Cloud = CloudDigest("unstable", score: 0.81, delta: -0.02, disagrees: true) },
                    _ => f
                })
            ]
        };

        return WithCloud(envelope, CloudContext(envelope));
    }

    // One batch failed: one test answered, one Cloud does not know, the rest not answered.
    private static ReportEnvelope CloudPartial()
    {
        ReportEnvelope envelope = Build("latest-run");
        CloudDigestDto unknown = new("unknown", Fetched, false, null, null, null, null, null, null, null, null, null, []);

        envelope = envelope with
        {
            Findings =
            [
                envelope.Findings[0] with { Cloud = CloudDigest("broken") },
                envelope.Findings[1] with { Cloud = unknown },
                .. envelope.Findings.Skip(2)
            ],
            LatestRun = envelope.LatestRun! with
            {
                Failures = [envelope.LatestRun.Failures[0] with { Cloud = Failure(unknown, "unknown") }, envelope.LatestRun.Failures[1]]
            }
        };

        return WithCloud(envelope, CloudContext(envelope, "partial", "timeout"));
    }

    // The sign-in is over: said once above the fence, every row local and nothing else added.
    private static ReportEnvelope CloudLoginRequired()
    {
        ReportEnvelope envelope = Build("latest-run");
        return WithCloud(envelope, CloudContext(envelope, "login-required", "none"));
    }

    // The detail view of a finding whose failure modes Cloud matched, did not see, and could not sign.
    private static ReportEnvelope DetailCloud()
    {
        ReportEnvelope envelope = Get("detail-failure-modes");
        FindingDto finding = envelope.Findings[0];
        CloudDigestDto digest = CloudDigest("broken");

        string[] matches = ["matched", "unseen", "unsignable"];

        envelope = envelope with
        {
            Findings =
            [
                finding with
                {
                    Cloud = digest,
                    FailureModes =
                    [
                        .. finding.FailureModes.Select((mode, index) => mode with
                        {
                            Cloud = new CloudMatchDto(
                                index < matches.Length ? matches[index] : "unknown",
                                index == 0 ? digest.Signatures[0] : null)
                        })
                    ]
                }
            ]
        };

        return WithCloud(envelope, CloudContext(envelope));
    }

    // A history with Cloud's block and its runs across machines and branches.
    private static ReportEnvelope HistoryCloud()
    {
        ReportEnvelope envelope = Build("history-flaky");
        TestHistoryDto history = envelope.Test!;
        CloudDigestDto digest = CloudDigest("known-flaky", flaky: "flaky", score: 0.41, delta: -0.12);

        CloudRunDto Run(int minutesAgo, string outcome, string tier, bool ci, string machine, string branch, int attempts = 1) =>
            new(
                "4f1c0c9e-5d2a-4b7e-9c1f-0a2b3c4d5e6f",
                new DateTimeOffset(2026, 10, 31, 8, 59, 0, TimeSpan.Zero).AddMinutes(-minutesAgo),
                outcome,
                attempts,
                attempts > 1 ? "failed" : outcome,
                812,
                tier,
                ci,
                machine,
                branch,
                "0053b0ef9a8b7c6d5e4f",
                null,
                outcome == "passed" ? null : "InvalidOperationException",
                outcome == "passed" ? null : CloudHash);

        CloudRunDto[] runs =
        [
            Run(0, "failed", "canonical", ci: true, "gh-runner-17", "main"),
            Run(109, "passed", "untracked", ci: false, "adrian-mbp", "feat/x", attempts: 2),
            Run(300, "timeout", "speculative", ci: true, "gh-runner-intel-large-04", "feature/cloud-digest-render"),
            Run(1440, "passed", "canonical", ci: true, "gh-runner-3", "main")
        ];

        envelope = envelope with
        {
            Test = history with
            {
                FailureModes =
                [
                    .. history.FailureModes.Select((mode, index) => index == 0
                        ? mode with { ErrorMessageHash = CloudHash, Cloud = new CloudMatchDto("matched", digest.Signatures[0]) }
                        : mode with { Cloud = new CloudMatchDto("unseen", null) })
                ],
                Cloud = new CloudTestHistoryDto(digest, runs, "ok", 365, HasMore: true)
            },
            Findings = [.. envelope.Findings.Select(f => f with { Cloud = digest })]
        };

        return WithCloud(envelope, CloudContext(envelope));
    }
}
