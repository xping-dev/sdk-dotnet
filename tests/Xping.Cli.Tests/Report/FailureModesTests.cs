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
    // By name: the kind enum is internal, and a public theory cannot take it as a parameter.
    public static TheoryData<string> Kinds() => [.. Enum.GetNames<FindingKind>()];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EachKindPublishesTheSignaturesItsEvidenceCarries(string kind)
    {
        FindingEvidence evidence = EvidenceSamples.For(Enum.Parse<FindingKind>(kind));

        SignatureView[] expected = evidence switch
        {
            FlakyEvidence e => [.. e.DistinctSignatures],
            AlwaysFailingEvidence e => [e.Signature],
            BrokenFixtureEvidence e => [e.Signature],
            SharedFailureEvidence e => [e.Signature],
            _ => []
        };

        IReadOnlyList<FailureModeDto> modes = FailureModes.Of(evidence);

        Assert.Equal(expected.Select(s => s.Hash), modes.Select(m => m.Hash));
    }

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
                signature.Occurrences,
                signature.FirstSeenAt,
                signature.FirstSeenSha,
                signature.FirstSeenSessionsAgo,
                signature.FirstSeenInLatestSession,
                signature.FirstSeenAfterWindowStart),
            mode);
    }
}
