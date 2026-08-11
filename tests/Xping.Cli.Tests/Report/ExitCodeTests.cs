/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Model;

namespace Xping.Cli.Tests.Report;

public sealed class ExitCodeTests
{
    private static Finding Build(Severity severity) =>
        new(
            "f_test",
            FindingKind.Flaky,
            severity,
            EvidenceLevel.Moderate,
            new FindingSubject.SingleTest(
                new TestReference("fp-A", "N.C.M", "M", null, null, "A.Tests")),
            new StubEvidence(),
            "xping report",
            0.5);

    // Severities travel as names rather than as the enum itself: the enum is internal to the CLI,
    // and a public [Theory] parameter cannot be less accessible than the method carrying it.
    [Theory]
    // No threshold: findings never fail the command.
    [InlineData("High", null, ExitCodes.Success)]
    [InlineData("Low", null, ExitCodes.Success)]
    // --fail-on high: only high findings fail.
    [InlineData("High", "High", ExitCodes.FindingsAtThreshold)]
    [InlineData("Medium", "High", ExitCodes.Success)]
    [InlineData("Low", "High", ExitCodes.Success)]
    // --fail-on medium: medium and above fail.
    [InlineData("High", "Medium", ExitCodes.FindingsAtThreshold)]
    [InlineData("Medium", "Medium", ExitCodes.FindingsAtThreshold)]
    [InlineData("Low", "Medium", ExitCodes.Success)]
    // --fail-on low: anything fails.
    [InlineData("High", "Low", ExitCodes.FindingsAtThreshold)]
    [InlineData("Medium", "Low", ExitCodes.FindingsAtThreshold)]
    [InlineData("Low", "Low", ExitCodes.FindingsAtThreshold)]
    public void ThresholdSelectsTheExitCode(string found, string? failOn, int expected)
    {
        Severity? threshold = failOn == null ? null : Enum.Parse<Severity>(failOn);

        Assert.Equal(
            expected, ExitCodes.ForReport([Build(Enum.Parse<Severity>(found))], threshold));
    }

    [Fact]
    public void NoFindingsSucceedsAtEveryThreshold()
    {
        Assert.Equal(ExitCodes.Success, ExitCodes.ForReport([], Severity.Low));
        Assert.Equal(ExitCodes.Success, ExitCodes.ForReport([], Severity.High));
        Assert.Equal(ExitCodes.Success, ExitCodes.ForReport([], null));
    }

    [Fact]
    public void OneFindingAtThresholdIsEnoughAmongMany()
    {
        Finding[] findings = [Build(Severity.Low), Build(Severity.Low), Build(Severity.High)];

        Assert.Equal(
            ExitCodes.FindingsAtThreshold, ExitCodes.ForReport(findings, Severity.High));
    }

    private sealed record StubEvidence : FindingEvidence;
}
