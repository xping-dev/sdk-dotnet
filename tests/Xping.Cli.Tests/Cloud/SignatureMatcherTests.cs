/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Cloud;
using Xping.Cli.Report.Contract;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// Every row of cloud-data-in-cli-cli-spec §5.3 (contract §4.7).
/// </summary>
public sealed class SignatureMatcherTests
{
    private static readonly string Seen = new('a', 64);
    private static readonly string Requested = new('b', 64);
    private static readonly string NotRequested = new('c', 64);

    private static readonly CloudSignatureDto SeenSignature = Signature(Seen, runs: 4);

    private static readonly CloudDigestDto Digest = new(
        "ok", DateTimeOffset.UnixEpoch, false, null, "passing", null, null, 10, null, null, null, null,
        [SeenSignature, Signature(Requested, runs: 0)]);

    [Fact]
    public void AHashCloudHasRunsForIsMatchedWithItsSignature() =>
        Assert.Equal(new CloudMatchDto("matched", SeenSignature), SignatureMatcher.Match(Seen, Digest, algorithmMismatch: false));

    [Fact]
    public void ARequestedHashWithNoRunsIsUnseen() =>
        Assert.Equal(new CloudMatchDto("unseen", null), SignatureMatcher.Match(Requested, Digest, algorithmMismatch: false));

    [Fact]
    public void AHashNotInTheAnswerIsUnseen() =>
        // Only a hash beyond the ones a target carries can be missing (contract §6.2).
        Assert.Equal(new CloudMatchDto("unseen", null), SignatureMatcher.Match(NotRequested, Digest, algorithmMismatch: false));

    [Fact]
    public void AFailureWithoutAHashIsUnsignable() =>
        Assert.Equal(new CloudMatchDto("unsignable", null), SignatureMatcher.Match(null, Digest, algorithmMismatch: false));

    [Fact]
    public void NoDigestIsUnknownEvenWithoutAHash() =>
        Assert.Equal(new CloudMatchDto("unknown", null), SignatureMatcher.Match(null, null, algorithmMismatch: false));

    [Fact]
    public void ATestCloudDoesNotKnowIsUnknown() =>
        Assert.Equal(
            new CloudMatchDto("unknown", null),
            SignatureMatcher.Match(Seen, Digest with { State = "unknown" }, algorithmMismatch: false));

    [Fact]
    public void AnotherSignatureAlgorithmMakesEveryMatchUnknown() =>
        // Contract §11: two algorithms' hashes cannot be compared, even when they happen to agree.
        Assert.Equal(new CloudMatchDto("unknown", null), SignatureMatcher.Match(Seen, Digest, algorithmMismatch: true));

    private static CloudSignatureDto Signature(string hash, int runs) =>
        new(hash, null, true, runs, 0, 0, runs, 1, null, null, [], null);
}
