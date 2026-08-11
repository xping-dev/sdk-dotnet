/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Model;

namespace Xping.Cli.Tests.Report;

public sealed class FindingOrderTests
{
    private static Finding Build(
        string id,
        Severity severity = Severity.Medium,
        double impact = 0.5,
        FindingKind kind = FindingKind.Flaky,
        string fingerprint = "fp-Alpha") =>
        new(
            id,
            kind,
            severity,
            EvidenceLevel.Moderate,
            new FindingSubject.SingleTest(
                new TestReference(fingerprint, "N.C.M", "M", null, null, "A.Tests")),
            new StubEvidence(),
            "xping report",
            impact);

    private static List<Finding> Sorted(params Finding[] findings)
    {
        var list = findings.ToList();
        list.Sort(FindingOrder.Instance);
        return list;
    }

    [Fact]
    public void SeverityOutranksEverythingElse()
    {
        List<Finding> sorted = Sorted(
            Build("f_low", Severity.Low, impact: 0.99),
            Build("f_high", Severity.High, impact: 0.01),
            Build("f_med", Severity.Medium, impact: 0.5));

        Assert.Equal(["f_high", "f_med", "f_low"], sorted.Select(f => f.Id));
    }

    [Fact]
    public void ImpactBreaksASeverityTieDescending()
    {
        List<Finding> sorted = Sorted(
            Build("f_a", Severity.High, impact: 0.61),
            Build("f_b", Severity.High, impact: 0.95),
            Build("f_c", Severity.High, impact: 0.70));

        Assert.Equal(["f_b", "f_c", "f_a"], sorted.Select(f => f.Id));
    }

    [Fact]
    public void ImpactDifferencesBelowPublishedPrecisionDoNotReorder()
    {
        // Two scores that differ in the fifteenth decimal place are the same finding as far as a
        // reader is concerned. Letting that noise decide would make the order depend on the order
        // the floating-point terms happened to be summed in.
        List<Finding> sorted = Sorted(
            Build("f_b", Severity.High, impact: 0.5000000000000002, kind: FindingKind.Flaky),
            Build("f_a", Severity.High, impact: 0.5, kind: FindingKind.RetryMasked));

        // RetryMasked is declared first, so the kind tiebreaker decides rather than the noise.
        Assert.Equal(["f_a", "f_b"], sorted.Select(f => f.Id));
    }

    [Fact]
    public void KindBreaksAnImpactTie()
    {
        List<Finding> sorted = Sorted(
            Build("f_v", kind: FindingKind.Vanished),
            Build("f_r", kind: FindingKind.RetryMasked),
            Build("f_f", kind: FindingKind.Flaky));

        Assert.Equal(["f_r", "f_f", "f_v"], sorted.Select(f => f.Id));
    }

    [Fact]
    public void FingerprintBreaksAKindTie()
    {
        List<Finding> sorted = Sorted(
            Build("f_2", fingerprint: "fp-Charlie"),
            Build("f_1", fingerprint: "fp-Alpha"),
            Build("f_3", fingerprint: "fp-Bravo"));

        Assert.Equal(["f_1", "f_3", "f_2"], sorted.Select(f => f.Id));
    }

    [Fact]
    public void IdIsTheFinalTiebreakSoNoTwoFindingsCompareEqual()
    {
        Finding a = Build("f_aaa");
        Finding b = Build("f_bbb");

        Assert.True(FindingOrder.Instance.Compare(a, b) < 0);
        Assert.True(FindingOrder.Instance.Compare(b, a) > 0);
    }

    [Fact]
    public void OrderIsIndependentOfInputOrder()
    {
        Finding[] findings =
        [
            Build("f_1", Severity.High, 0.9, FindingKind.Flaky, "fp-A"),
            Build("f_2", Severity.High, 0.9, FindingKind.Flaky, "fp-B"),
            Build("f_3", Severity.Medium, 0.4, FindingKind.Vanished, "fp-C"),
            Build("f_4", Severity.Low, 0.1, FindingKind.NeverRun, "fp-D"),
            Build("f_5", Severity.Medium, 0.4, FindingKind.Flaky, "fp-E")
        ];

        List<string> expected = [.. Sorted(findings).Select(f => f.Id)];

        // Every rotation of the same set must sort identically.
        for (int shift = 1; shift < findings.Length; shift++)
        {
            Finding[] rotated = [.. findings.Skip(shift), .. findings.Take(shift)];
            Assert.Equal(expected, Sorted(rotated).Select(f => f.Id));
        }
    }

    private sealed record StubEvidence : FindingEvidence;
}
