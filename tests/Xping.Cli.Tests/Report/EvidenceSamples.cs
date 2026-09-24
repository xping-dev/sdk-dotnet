/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// One plausible evidence payload for every kind a provider can emit.
/// </summary>
/// <remarks>
/// Shared by the headline tests and the detail fixtures, so the metrics a detail golden pins are the
/// ones <c>EvidenceHeadline</c> resolves from a real payload rather than a list typed beside it.
/// </remarks>
internal static class EvidenceSamples
{
    /// <summary>
    /// Builds the sample payload for a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Its evidence.</returns>
    public static FindingEvidence For(FindingKind kind)
    {
        SignatureView signature = new(
            "abc123",
            "System.InvalidOperationException",
            "Expected <n> but was <n>",
            ["MyApp.Tests.CheckoutTests.Completes()"],
            Degraded: false,
            Unavailable: false,
            Occurrences: 12,
            FirstSeenAt: new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
            FirstSeenSha: "a3f9c2e",
            FirstSeenSessionsAgo: 4,
            FirstSeenInLatestSession: false,
            FirstSeenAfterWindowStart: true);

        FailureExemplar exemplar = new(
            "11111111-1111-1111-1111-111111111111",
            new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc),
            "a3f9c2e",
            AttemptNumber: 1,
            DurationMs: 120,
            "System.InvalidOperationException",
            "boom",
            ["MyApp.Tests.CheckoutTests.Completes()"],
            "abc123",
            Site: nameof(FailureSite.TestBody),
            SiteMember: null);

        RetryConfiguration configuration = new("RetryAttribute", 2, "NetworkError", 250);

        RetryAttemptExemplar attemptExemplar = new(
            "11111111-1111-1111-1111-111111111111",
            new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc),
            "a3f9c2e",
            Attempts: 3,
            Outcome: nameof(TestOutcome.Failed),
            RetryWallClockMs: 8_200,
            "boom");

        return kind switch
        {
            FindingKind.RetryMasked =>
                new RetryMaskedEvidence(
                    4, 20, 20, 3, 0.2, 3, configuration, 12_400, 750, 2,
                    new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc), "a3f9c2e", []),

            FindingKind.RetryDeepening =>
                new RetryDeepeningEvidence(
                    new RetryDepthProfile(3, 4, 3, 0, 3),
                    new RetryDepthProfile(1, 2, 14, 0, 14),
                    new RetryDepthDelta(2, 200),
                    configuration,
                    2_400,
                    500,
                    0,
                    "a3f9c2e",
                    [attemptExemplar],
                    attemptExemplar),

            FindingKind.RetryExhausted =>
                new RetryExhaustedEvidence(
                    7, 8, 1, 20, 20, 0.875, 0.529, 3, 12, configuration, 41_000, 3_000, 0,
                    new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc), "a3f9c2e",
                    [attemptExemplar], attemptExemplar),

            FindingKind.Flaky =>
                new FlakyEvidence(
                    7, 20, 20, 5, 0.35, 2, 1, 3,
                    [
                        signature with { Occurrences = 4 },
                        signature with
                        {
                            Hash = "def456",
                            ExceptionType = "System.TimeoutException",
                            Message = "timed out after <n>ms",
                            Occurrences = 2
                        },
                        signature with
                        {
                            Hash = "0a1b2c",
                            ExceptionType = null,
                            Message = string.Empty,
                            Frames = [],
                            Unavailable = true,
                            Occurrences = 1
                        }
                    ],
                    [exemplar],
                    null),

            FindingKind.AlwaysFailing =>
                new AlwaysFailingEvidence(
                    19, 20, 20, 19, 0.95, 0, 0, signature with { Occurrences = 19 }, 1.0,
                    [exemplar], null),

            FindingKind.TimingOut =>
                new TimingOutEvidence(
                    9, 10, 20, 20, 9, 0.45, 0.9, 0, 0, 500, [512, 508, 503], [exemplar], null),

            FindingKind.BrokenFixture =>
                new BrokenFixtureEvidence(
                    nameof(FailureSite.TestSetup),
                    "CheckoutFixture.Setup",
                    signature,
                    12,
                    [new ClusterMember("fp", "MyApp.Tests.A", 4)],
                    47, 3, 20, 12,
                    new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc), "a3f9c2e", [exemplar]),

            FindingKind.SharedFailure =>
                new SharedFailureEvidence(
                    signature, 12, [new ClusterMember("fp", "MyApp.Tests.A", 4)], 47, 3, 20, 12,
                    new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc), "a3f9c2e", [exemplar]),

            FindingKind.DurationRegression =>
                new DurationRegressionEvidence(
                    new DurationProfile(1240, 1890, 4, 3, 0, 3, 0.21),
                    new DurationProfile(340, 410, 10, 10, 0, 10, 0.14),
                    new DurationDelta(264.7, 900),
                    new DurationShift(3.512, 1.94, 5.87, 251.2, 880, 0.004),
                    "a3f9c2e",
                    [],
                    null),

            FindingKind.DurationUnstable =>
                new DurationUnstableEvidence(18, 20, 0, 820, 3100, 210, 4100, 18, 900, 0.71, []),

            FindingKind.ParallelSensitive =>
                new ParallelSensitiveEvidence(
                    new ConcurrencyTrend(
                        2.874, 0.00405, 0.612, 18, nameof(ConcurrencyDirection.WithConcurrency)),
                    new ConcurrencyRange(1, 14, 4),
                    [
                        new ConcurrencyLevel(1, 6, 6, 0, 0),
                        new ConcurrencyLevel(4, 5, 5, 1, 0.2),
                        new ConcurrencyLevel(9, 5, 5, 3, 0.6),
                        new ConcurrencyLevel(14, 4, 4, 3, 0.75)
                    ],
                    [],
                    null,
                    0,
                    0),

            FindingKind.TimeSensitive =>
                new TimeSensitiveEvidence(
                    "LocalTimeOfDay",
                    new TimeArm(9, 10, 0.9, 7, "18:00-24:00 local"),
                    new TimeArm(0, 6, 0, 0, "the rest of the day"),
                    new TimeDelta(0.9, 90),
                    new TimeSignificance(0.001748, 2),
                    "Europe/Berlin",
                    [],
                    null,
                    0,
                    0),

            FindingKind.Vanished =>
                new VanishedEvidence(
                    12, 17, 3, 0, 0.706, 0.0491, 40,
                    new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc), "a3f9c2e"),

            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
