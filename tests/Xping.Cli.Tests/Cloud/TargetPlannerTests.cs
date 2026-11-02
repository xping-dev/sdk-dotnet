/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Cloud;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Tests.Report;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Cloud;

/// <summary>
/// Which tests a report asks Xping Cloud about, in which order, and with which signatures and
/// session keys (cloud-data-in-cli-cli-spec §4.1, §4.2; contract §7.2).
/// </summary>
public sealed class TargetPlannerTests
{
    private const string Assembly = TestSessionFactory.DefaultAssembly;

    private static readonly string HashA = new('a', 64);
    private static readonly string HashB = new('b', 64);
    private static readonly string HashC = new('c', 64);
    private static readonly string HashD = new('d', 64);

    [Fact]
    public void TheSubjectComesFirstThenFindingsInRankThenLatestRunFailuresThenOtherLatestFailures()
    {
        TestSession latest = TestSessionFactory.Session(
            1,
            [
                Failed("Zeta"),
                Failed("Alpha"),
                Failed("RowFailure"),
                TestSessionFactory.Execution("Passing")
            ]);

        ReportEnvelope envelope = Envelope(subject: "Subject", latestRunFailures: ["RowFailure"]);

        TargetPlan plan = TargetPlanner.Plan(envelope, [Finding("Ranked1"), Finding("Ranked2")], [latest]);

        Assert.Equal(
            ["fp-Subject", "fp-Ranked1", "fp-Ranked2", "fp-RowFailure", "fp-Alpha", "fp-Zeta"],
            plan.Targets.Select(t => t.Fingerprint));
    }

    [Fact]
    public void FindingsAreReadBeforeTopCutTheEnvelope()
    {
        // The envelope holds no findings at all; the ranked list still decides the targets.
        TargetPlan plan = TargetPlanner.Plan(Envelope(), [Finding("Hidden")], []);

        Assert.Equal("fp-Hidden", Assert.Single(plan.Targets).Fingerprint);
    }

    [Fact]
    public void ATestIsAskedAboutOnceWhereverItAppears()
    {
        // Q-1: the --test subject that is also a finding is one target.
        ReportEnvelope envelope = Envelope(subject: "Same", latestRunFailures: ["Same"]);

        TargetPlan plan = TargetPlanner.Plan(envelope, [Finding("Same")], [TestSessionFactory.Session(1, [Failed("Same")])]);

        Assert.Equal("fp-Same", Assert.Single(plan.Targets).Fingerprint);
    }

    [Fact]
    public void NoMoreThanTwoHundredTestsAreAsked()
    {
        List<Finding> findings = [.. Enumerable.Range(0, 250).Select(i => Finding($"T{i}"))];

        TargetPlan plan = TargetPlanner.Plan(Envelope(), findings, []);

        Assert.Equal(TargetPlanner.MaxEnrichedTests, plan.Targets.Count);
        Assert.Equal("fp-T199", plan.Targets[^1].Fingerprint);
    }

    [Fact]
    public void AGroupFindingIsNotATargetItself()
    {
        var group = new Finding(
            "f_group",
            FindingKind.SharedFailure,
            Severity.High,
            EvidenceLevel.High,
            20,
            new FindingSubject.Group("sig_1", [Reference("Member")]),
            new NoEvidence(),
            0.5);

        Assert.Empty(TargetPlanner.Plan(Envelope(), [group], []).Targets);
    }

    [Fact]
    public void AFingerprintLongerThanTheDigestAcceptsIsNotAsked()
    {
        // Contract §9.3: Cloud cannot know it, and sending it would cost its batch a 400.
        string longName = new('x', TargetPlanner.MaxFingerprintLength + 1);

        TargetPlan plan = TargetPlanner.Plan(Envelope(), [Finding(longName, fingerprint: longName), Finding("Short")], []);

        Assert.Equal("fp-Short", Assert.Single(plan.Targets).Fingerprint);
    }

    [Fact]
    public void TheNewestSessionsHashesComeFirstThenTheMostFrequentUpToThree()
    {
        TestSession older = TestSessionFactory.Session(
            0,
            [
                Failed("T", HashC, attempt: 1),
                Failed("T", HashC, attempt: 2),
                Failed("T", HashD, attempt: 3)
            ]);
        TestSession newest = TestSessionFactory.Session(
            1,
            [
                Failed("T", HashA, attempt: 1, startOffsetMs: 0),
                Failed("T", HashB, attempt: 2, startOffsetMs: 500)
            ]);

        CloudTarget target = Assert.Single(TargetPlanner.Plan(Envelope(), [Finding("T")], [newest, older]).Targets);

        Assert.Equal([HashB, HashA, HashC], target.Hashes);
        Assert.Equal(HashB, target.LatestFailureHash);
    }

    [Fact]
    public void FrequencyTiesBreakOnTheSmallerHashSoTheRequestDoesNotDependOnReadOrder()
    {
        TestSession older = TestSessionFactory.Session(0, [Failed("T", HashD, attempt: 1), Failed("T", HashB, attempt: 2)]);

        CloudTarget target = Assert.Single(TargetPlanner.Plan(Envelope(), [Finding("T")], [TestSessionFactory.Session(1, "T"), older]).Targets);

        Assert.Equal([HashB, HashD], target.Hashes);
        Assert.Null(target.LatestFailureHash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-hash")]
    [InlineData("ABABABABABABABABABABABABABABABABABABABABABABABABABABABABABABABAB")]
    public void AHashTheDigestWouldRefuseIsNotSent(string? hash)
    {
        // One malformed value would cost the whole batch a 400, and the store is a file anyone can edit.
        TestSession newest = TestSessionFactory.Session(1, [Failed("T", hash)]);

        CloudTarget target = Assert.Single(TargetPlanner.Plan(Envelope(), [Finding("T")], [newest]).Targets);

        Assert.Empty(target.Hashes);
        Assert.Null(target.LatestFailureHash);
    }

    [Fact]
    public void EachAssemblysNewestSessionKeyIsSent()
    {
        TestSession oldOther = TestSessionFactory.Session(0, [TestSessionFactory.Execution("A", assembly: "Other.Tests")]);
        TestSession newOther = TestSessionFactory.Session(1, [TestSessionFactory.Execution("A", assembly: "Other.Tests")]);
        TestSession newest = TestSessionFactory.Session(2, [TestSessionFactory.Execution("B")]);

        TargetPlan plan = TargetPlanner.Plan(Envelope(), [], [newest, newOther, oldOther]);

        Assert.Equal(
            new Dictionary<string, string>
            {
                [Assembly] = newest.SessionId.ToString(),
                ["Other.Tests"] = newOther.SessionId.ToString()
            },
            plan.SessionKeys);
    }

    [Fact]
    public void ATestThatPassedOnItsFinalAttemptIsNotAnOtherFailure()
    {
        TestSession latest = TestSessionFactory.Session(
            1,
            [Failed("Retried", HashA, attempt: 1), TestSessionFactory.Execution("Retried", attempt: 2)]);

        TargetPlan plan = TargetPlanner.Plan(Envelope(), [], [latest]);

        Assert.Empty(plan.Targets);
    }

    private static TestExecution Failed(string name, string? hash = null, int attempt = 1, int startOffsetMs = 0) =>
        TestSessionFactory.Execution(
            name,
            TestOutcome.Failed,
            attempt: attempt,
            errorMessage: "boom",
            executionId: Guid.NewGuid(),
            errorMessageHash: hash,
            startOffsetMs: startOffsetMs);

    private static Finding Finding(string name, string? fingerprint = null) =>
        new(
            $"f_{name}",
            FindingKind.Flaky,
            Severity.High,
            EvidenceLevel.High,
            20,
            new FindingSubject.SingleTest(Reference(name, fingerprint)),
            new NoEvidence(),
            0.5);

    private static TestReference Reference(string name, string? fingerprint = null) =>
        new(fingerprint ?? $"fp-{name}", $"MyApp.Tests.SampleTests.{name}", name, null, null, Assembly);

    private static ReportEnvelope Envelope(string? subject = null, IReadOnlyList<string>? latestRunFailures = null)
    {
        ReportEnvelope envelope = ReportFixtures.Envelope();

        return envelope with
        {
            Test = subject is null
                ? null
                : ReportFixtures.Get("history-flaky").Test! with
                {
                    Test = ReportFixtures.LatestRunFailure("new", subject, "x").Subject with { Assembly = Assembly }
                },
            LatestRun = latestRunFailures is null
                ? null
                : ReportFixtures.Get("latest-run").LatestRun! with
                {
                    Failures = [.. latestRunFailures.Select(n => ReportFixtures.LatestRunFailure("new", n, "x") with
                    {
                        Subject = ReportFixtures.LatestRunFailure("new", n, "x").Subject with { Assembly = Assembly }
                    })]
                }
        };
    }

    private sealed record NoEvidence : FindingEvidence;
}
