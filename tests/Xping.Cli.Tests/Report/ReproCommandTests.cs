/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// A finding's repro reruns the test it names, as many times as its failure rate needs.
/// </summary>
public sealed class ReproCommandTests
{
    private const string Prefix = "dotnet test --no-build -e XPING_ENABLED=false --filter ";

    [Fact]
    public void AFlakyTestIsRerunEnoughTimesToSeeItFail()
    {
        Repro repro = Build(
            "SampleApp.MSTest.CalculatorTests.FlakyTest_RaceCondition_FailsIntermittently",
            Flaky(failing: 4, sessions: 9))!;

        Assert.Equal(
            Prefix + "FullyQualifiedName=SampleApp.MSTest.CalculatorTests.FlakyTest_RaceCondition_FailsIntermittently",
            repro.Command);
        Assert.Equal(6, repro.Runs);
        Assert.Equal(4, repro.FailingSessions);
        Assert.Equal(9, repro.Sessions);
    }

    /// <summary>D5's table.</summary>
    [Theory]
    [InlineData(4, 9, 6, 6)]
    [InlineData(6, 20, 9, 9)]
    [InlineData(10, 20, 5, 5)]
    [InlineData(2, 20, 29, 29)]
    [InlineData(1, 20, null, 59)]
    public void TheRunCountIsSizedFromTheShareOfRunsThatFailed(int failing, int sessions, int? runs, int needed)
    {
        Repro repro = Build("Ns.C.T", Flaky(failing, sessions))!;

        Assert.Equal(runs, repro.Runs);
        Assert.Equal(needed, repro.RunsNeeded);
    }

    [Fact]
    public void TheRateIsPerRunNotPerExecution()
    {
        // Retry attempts inflate the executions: 4 failures in 40 executions, but in 4 of 9 runs.
        var evidence = Flaky(failing: 4, sessions: 9) with { Failures = 4, ExecutionsConsidered = 40, FailureRate = 0.1 };

        Assert.Equal(6, Build("Ns.C.T", evidence)!.Runs);
    }

    [Fact]
    public void AnAlwaysFailingTestIsRunOnce()
    {
        var evidence = (AlwaysFailingEvidence)EvidenceSamples.For(FindingKind.AlwaysFailing) with
        {
            SessionsWithFailures = 19,
            SessionsConsidered = 20
        };

        Repro repro = Build("Ns.C.T", evidence)!;

        Assert.Equal(1, repro.Runs);
        Assert.Equal(1, repro.RunsNeeded);
        Assert.Equal(19, repro.FailingSessions);
    }

    [Fact]
    public void ATimingOutTestIsSizedFromTheRunsItTimedOutIn()
    {
        var evidence = (TimingOutEvidence)EvidenceSamples.For(FindingKind.TimingOut) with
        {
            SessionsWithTimeouts = 6,
            SessionsConsidered = 20
        };

        Repro repro = Build("Ns.C.T", evidence)!;

        Assert.Equal(Prefix + "FullyQualifiedName=Ns.C.T", repro.Command);
        Assert.Equal(9, repro.Runs);
    }

    [Fact]
    public void ABrokenFixtureRerunsTheMemberThatFailedMost()
    {
        var evidence = (BrokenFixtureEvidence)EvidenceSamples.For(FindingKind.BrokenFixture) with
        {
            Members = [Member("Ns.A.One", 2), Member("Ns.B.Two", 5), Member("Ns.C.Three", 5)],
            SessionsAffected = 10,
            Sessions = 20
        };

        Repro repro = ReproCommand.For(Group(evidence))!;

        Assert.Equal(Prefix + "FullyQualifiedName=Ns.B.Two", repro.Command);
        Assert.Equal(5, repro.Runs);
    }

    [Fact]
    public void AFixtureBrokenInEveryRunIsRunOnce()
    {
        var evidence = (BrokenFixtureEvidence)EvidenceSamples.For(FindingKind.BrokenFixture) with
        {
            SessionsAffected = 20,
            Sessions = 20
        };

        Assert.Equal(1, ReproCommand.For(Group(evidence))!.Runs);
    }

    [Fact]
    public void ASharedFailureRerunsItsThreeMostFailingMembers()
    {
        var evidence = (SharedFailureEvidence)EvidenceSamples.For(FindingKind.SharedFailure) with
        {
            Members =
            [
                Member("Ns.A.Zero", 1),
                Member("Ns.C.Three", 3),
                Member("Ns.A.One", 7),
                Member("Ns.D.Four", 1),
                Member("Ns.B.Two", 3)
            ],
            SessionsAffected = 6,
            Sessions = 20
        };

        Repro repro = ReproCommand.For(Group(evidence))!;

        Assert.Equal(
            Prefix + "'FullyQualifiedName=Ns.A.One|FullyQualifiedName=Ns.C.Three|FullyQualifiedName=Ns.B.Two'",
            repro.Command);
        Assert.Equal(9, repro.Runs);
    }

    /// <summary>D2's dashes: a rerun cannot show these failures.</summary>
    [Theory]
    [InlineData(nameof(FindingKind.RetryMasked))]
    [InlineData(nameof(FindingKind.RetryDeepening))]
    [InlineData(nameof(FindingKind.RetryExhausted))]
    [InlineData(nameof(FindingKind.DurationRegression))]
    [InlineData(nameof(FindingKind.DurationUnstable))]
    [InlineData(nameof(FindingKind.ParallelSensitive))]
    [InlineData(nameof(FindingKind.TimeSensitive))]
    [InlineData(nameof(FindingKind.Vanished))]
    public void AKindARerunCannotShowHasNoRepro(string name)
    {
        var kind = Enum.Parse<FindingKind>(name);

        Assert.Null(Build("Ns.C.T", EvidenceSamples.For(kind), kind));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(3, 0)]
    public void CountsThatSizeNothingGiveNoRepro(int failing, int sessions)
    {
        Assert.Null(Build("Ns.C.T", Flaky(failing, sessions)));
    }

    /// <summary>D3 and Amendment 1A, with the strings §3.2 prints.</summary>
    [Theory]
    [InlineData(
        "SampleApp.NUnit.CalculatorTests.Divide(10,0)",
        @"'FullyQualifiedName=SampleApp.NUnit.CalculatorTests.Divide\(10,0\)'")]
    [InlineData(
        "SampleApp.NUnit.CalculatorTests.Echo(\"a=b|c\")",
        @"'FullyQualifiedName=SampleApp.NUnit.CalculatorTests.Echo\(\""a\=b\|c\""\)'")]
    [InlineData("Ns.Outer+Inner.Nested", "FullyQualifiedName=Ns.Outer+Inner.Nested")]
    [InlineData("Ns.C.S(\"c:\\\\x\")", @"'FullyQualifiedName=Ns.C.S\(\""c:\\\\x\""\)'")]
    [InlineData("Ns.C.S(\"it's\")", @"'FullyQualifiedName=Ns.C.S\(\""it'\''s\""\)'")]
    [InlineData("Ns.C.S(\"a&b!~\")", @"'FullyQualifiedName=Ns.C.S\(\""a\&b\!\~\""\)'")]
    public void TheNameIsEscapedForTheFilterAndQuotedForTheShell(string name, string filter)
    {
        Assert.Equal(Prefix + filter, Build(name, Flaky(4, 9))!.Command);
    }

    private static FlakyEvidence Flaky(int failing, int sessions) =>
        (FlakyEvidence)EvidenceSamples.For(FindingKind.Flaky) with
        {
            SessionsWithFailures = failing,
            SessionsConsidered = sessions
        };

    private static ClusterMember Member(string name, int failures) => new("fp-" + name, name, failures);

    private static Repro? Build(string name, FindingEvidence evidence, FindingKind kind = FindingKind.Flaky) =>
        ReproCommand.For(
            new Finding(
                "f_00000000",
                kind,
                Severity.Medium,
                EvidenceLevel.Moderate,
                9,
                new FindingSubject.SingleTest(new TestReference("fp", name, name, null, null, "MyApp.Tests")),
                evidence,
                0.5));

    private static Finding Group(FindingEvidence evidence) =>
        new(
            "f_00000000",
            evidence is BrokenFixtureEvidence ? FindingKind.BrokenFixture : FindingKind.SharedFailure,
            Severity.Medium,
            EvidenceLevel.Moderate,
            9,
            new FindingSubject.Group("g-1", []),
            evidence,
            0.5);
}
