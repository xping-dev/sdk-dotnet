/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// A finding publishes the failure modes its evidence names, and no kind invents any.
/// </summary>
public sealed class FailureModesTests
{
    // The D2 table, written out rather than derived from the evidence, so a wrong mapping in
    // FailureModes cannot be mirrored here and still pass. By name: the kind enum is internal,
    // and a public theory cannot take it as a parameter.
    public static TheoryData<string, string[]> Kinds() => new()
    {
        { nameof(FindingKind.Flaky), ["abc123", "def456", "0a1b2c"] },
        { nameof(FindingKind.AlwaysFailing), ["abc123"] },
        { nameof(FindingKind.BrokenFixture), ["abc123"] },
        { nameof(FindingKind.SharedFailure), ["abc123"] },
    };

    public static TheoryData<string> KindsWithoutModes() =>
        [.. Enum.GetNames<FindingKind>().Where(k => !Kinds().Any(row => (string)row[0] == k))];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void AKindWithSignaturesPublishesThemMostFrequentFirst(string kind, string[] hashes)
    {
        IReadOnlyList<FailureModeDto> modes = FailureModes.Of(EvidenceSamples.For(Enum.Parse<FindingKind>(kind)));

        Assert.Equal(hashes, modes.Select(m => m.Hash));
    }

    [Theory]
    [MemberData(nameof(KindsWithoutModes))]
    public void EveryOtherKindPublishesNone(string kind) =>
        Assert.Empty(FailureModes.Of(EvidenceSamples.For(Enum.Parse<FindingKind>(kind))));

    [Fact]
    public void AModeCarriesEveryFieldOfItsSignature()
    {
        var evidence = (FlakyEvidence)EvidenceSamples.For(FindingKind.Flaky);
        SignatureView signature = evidence.DistinctSignatures[0];

        FailureModeDto mode = FailureModes.Of(evidence)[0];

        Assert.Equal(
            new FailureModeDto(
                signature.Hash,
                signature.ExceptionType,
                signature.Message,
                signature.Frames,
                signature.Degraded,
                signature.Unavailable,
                signature.TimedOut,
                signature.Occurrences,
                signature.FirstSeenAt,
                signature.FirstSeenSha,
                signature.FirstSeenSessionsAgo,
                signature.FirstSeenInLatestSession,
                signature.FirstSeenAfterWindowStart),
            mode);
    }
}
