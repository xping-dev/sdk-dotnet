/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// A finding's repro reruns the test it names, as many times as its failure rate needs.
/// </summary>
public sealed class ReproCommandTests
{
    private const string Prefix = "dotnet test --no-build -e XPING_ENABLED=false --filter ";

    private static readonly ReproTarget VSTest = new(LocalSessionProperties.VSTestRunner, null);

    [Fact]
    public void AFlakyTestIsRerunEnoughTimesToSeeItFail()
    {
        Repro repro = Build(
            "SampleApp.MSTest.CalculatorTests.FlakyTest_RaceCondition_FailsIntermittently",
            Flaky(4), 9)!;

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
        Repro repro = Build("Ns.C.T", Flaky(failing), sessions)!;

        Assert.Equal(runs, repro.Runs);
        Assert.Equal(needed, repro.RunsNeeded);
    }

    [Fact]
    public void TheRateIsPerRunNotPerExecution()
    {
        // Retry attempts inflate the executions: 4 failures in 40 executions, but in 4 of 9 runs.
        var evidence = Flaky(4) with { Failures = 4, ExecutionsConsidered = 40, FailureRate = 0.1 };

        Assert.Equal(6, Build("Ns.C.T", evidence, 9)!.Runs);
    }

    /// <summary>Amendment 3A.</summary>
    [Fact]
    public void TheRateIsOverTheRunsTheTestRanInNotTheWindow()
    {
        // Two failures in the five runs since the test was added, in a twenty-run window.
        var evidence = Flaky(2) with { SessionsConsidered = 20 };

        Repro repro = Build("Ns.C.T", evidence, 5)!;

        Assert.Equal(6, repro.Runs);
        Assert.Equal(5, repro.Sessions);
    }

    [Fact]
    public void AnAlwaysFailingTestIsRunOnce()
    {
        var evidence = (AlwaysFailingEvidence)EvidenceSamples.For(FindingKind.AlwaysFailing) with
        {
            SessionsWithFailures = 19,
            SessionsConsidered = 20
        };

        Repro repro = Build("Ns.C.T", evidence, 20)!;

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

        Repro repro = Build("Ns.C.T", evidence, 20)!;

        Assert.Equal(Prefix + "FullyQualifiedName=Ns.C.T", repro.Command);
        Assert.Equal(9, repro.Runs);
    }

    /// <summary>Amendment 3B and 3C.</summary>
    [Fact]
    public void ABrokenFixtureRerunsTheMemberThatFailedInTheLargestShareOfItsRuns()
    {
        var evidence = (BrokenFixtureEvidence)EvidenceSamples.For(FindingKind.BrokenFixture) with
        {
            // Two has the most failures, but One failed in 3 of the 5 runs it was in.
            Members = [Member("Ns.A.One", 3, 3, 5), Member("Ns.B.Two", 6, 4, 20), Member("Ns.C.Three", 1, 1, 20)],
            SessionsAffected = 10,
            Sessions = 20
        };

        Repro repro = ReproCommand.For(Group(evidence), VSTest)!;

        Assert.Equal(Prefix + "FullyQualifiedName=Ns.A.One", repro.Command);
        Assert.Equal(4, repro.Runs);
        Assert.Equal(3, repro.FailingSessions);
        Assert.Equal(5, repro.Sessions);
    }

    [Fact]
    public void AMemberThatFailedEveryTimeItRanIsRunOnce()
    {
        var evidence = (BrokenFixtureEvidence)EvidenceSamples.For(FindingKind.BrokenFixture) with
        {
            Members = [Member("Ns.A.One", 20, 20, 20)]
        };

        Assert.Equal(1, ReproCommand.For(Group(evidence), VSTest)!.Runs);
    }

    [Fact]
    public void ASharedFailureRerunsTheThreeMembersMostLikelyToFail()
    {
        var evidence = (SharedFailureEvidence)EvidenceSamples.For(FindingKind.SharedFailure) with
        {
            Members =
            [
                Member("Ns.A.Zero", 1, 1, 20),
                Member("Ns.C.Three", 3, 3, 20),
                Member("Ns.A.One", 7, 6, 20),
                Member("Ns.D.Four", 4, 3, 20),
                Member("Ns.B.Two", 3, 3, 20),
                Member("Ns.E.Never", 0, 0, 0)
            ],
            SessionsAffected = 10,
            Sessions = 20
        };

        Repro repro = ReproCommand.For(Group(evidence), VSTest)!;

        // One at 6 of 20 first; then three at 3 of 20, where Four's extra failure wins, then
        // ordinal order puts Three before Two.
        Assert.Equal(
            Prefix + "'FullyQualifiedName=Ns.A.One|FullyQualifiedName=Ns.D.Four|FullyQualifiedName=Ns.C.Three'",
            repro.Command);
        Assert.Equal(9, repro.Runs);
        Assert.Equal(6, repro.FailingSessions);
    }

    [Fact]
    public void AClusterWhoseMembersNeverRanHasNoRepro()
    {
        var evidence = (SharedFailureEvidence)EvidenceSamples.For(FindingKind.SharedFailure) with
        {
            Members = [Member("Ns.A.One", 0, 0, 0)]
        };

        Assert.Null(ReproCommand.For(Group(evidence), VSTest));
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

        Assert.Null(Build("Ns.C.T", EvidenceSamples.For(kind), 20, kind));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(3, 0)]
    public void CountsThatSizeNothingGiveNoRepro(int failing, int sessions)
    {
        Assert.Null(Build("Ns.C.T", Flaky(failing), sessions));
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
    [InlineData("Ns.C.S(\"50%20off\")", @"'FullyQualifiedName=Ns.C.S\(\""50%2520off\""\)'")]
    public void OnVSTestTheNameIsEscapedForTheFilterMSBuildAndTheShell(string name, string filter)
    {
        Assert.Equal(Prefix + filter, Build(name, Flaky(4), 9)!.Command);
    }

    /// <summary>Amendment 2B: <c>dotnet run</c> hands the filter to the app, so only the filter syntax is escaped.</summary>
    [Theory]
    [InlineData(
        "SampleApp.NUnit.CalculatorTests.Echo(\"50%20off\")",
        @"'FullyQualifiedName=SampleApp.NUnit.CalculatorTests.Echo\(""50%20off""\)'")]
    [InlineData("Ns.C.S(\"c:\\\\x\")", @"'FullyQualifiedName=Ns.C.S\(""c:\\\\x""\)'")]
    [InlineData("Ns.C.S(\"a=b|c\")", @"'FullyQualifiedName=Ns.C.S\(""a\=b\|c""\)'")]
    [InlineData("Ns.Outer+Inner.Nested", "FullyQualifiedName=Ns.Outer+Inner.Nested")]
    public void OnTheTestingPlatformOnlyTheFilterSyntaxIsEscaped(string name, string filter)
    {
        Repro repro = Build(name, Flaky(4), 9, target: new ReproTarget(LocalSessionProperties.TestingPlatformRunner, null))!;

        Assert.Equal("dotnet run --no-build -e XPING_ENABLED=false -- --filter " + filter, repro.Command);
    }

    /// <summary>Amendment 2A.</summary>
    [Fact]
    public void AVSTestProjectRunsUnderDotnetTestNamingTheProject()
    {
        var target = new ReproTarget(LocalSessionProperties.VSTestRunner, "tests/SampleApp.MSTest/SampleApp.MSTest.csproj");

        Assert.Equal(
            "dotnet test tests/SampleApp.MSTest/SampleApp.MSTest.csproj --no-build -e XPING_ENABLED=false " +
            "--filter FullyQualifiedName=SampleApp.MSTest.CalculatorTests.Add",
            Build("SampleApp.MSTest.CalculatorTests.Add", Flaky(4), 9, target: target)!.Command);
    }

    [Fact]
    public void ATestingPlatformProjectRunsAsItsOwnExecutable()
    {
        var target = new ReproTarget(LocalSessionProperties.TestingPlatformRunner, "tests/SampleApp.NUnit/SampleApp.NUnit.csproj");

        Assert.Equal(
            "dotnet run --project tests/SampleApp.NUnit/SampleApp.NUnit.csproj --no-build -e XPING_ENABLED=false " +
            "-- --filter 'FullyQualifiedName=SampleApp.NUnit.CalculatorTests.Divide\\(10,0\\)'",
            Build("SampleApp.NUnit.CalculatorTests.Divide(10,0)", Flaky(4), 9, target: target)!.Command);
    }

    [Fact]
    public void AProjectPathTheShellWouldReadIsQuoted()
    {
        var target = new ReproTarget(LocalSessionProperties.VSTestRunner, "tests/My Tests/My.Tests.csproj");

        Assert.StartsWith(
            "dotnet test 'tests/My Tests/My.Tests.csproj' --no-build",
            Build("Ns.C.T", Flaky(4), 9, target: target)!.Command,
            StringComparison.Ordinal);
    }

    /// <summary>Amendment 2A: a guessed runner prints a command that fails.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("xunit.console")]
    public void WithNoKnownRunnerThereIsNoRepro(string? runner)
    {
        Assert.Null(Build("Ns.C.T", Flaky(4), 9, target: new ReproTarget(runner, "tests/A/A.csproj")));
    }

    private static FlakyEvidence Flaky(int failing) =>
        (FlakyEvidence)EvidenceSamples.For(FindingKind.Flaky) with { SessionsWithFailures = failing };

    private static ClusterMember Member(string name, int failures, int failingSessions, int sessions) =>
        new("fp-" + name, name, failures, failingSessions, sessions);

    private static Repro? Build(
        string name,
        FindingEvidence evidence,
        int ranIn,
        FindingKind kind = FindingKind.Flaky,
        ReproTarget? target = null) =>
        ReproCommand.For(
            new Finding(
                "f_00000000",
                kind,
                Severity.Medium,
                EvidenceLevel.Moderate,
                ranIn,
                new FindingSubject.SingleTest(new TestReference("fp", name, name, null, null, "MyApp.Tests")),
                evidence,
                0.5),
            target ?? VSTest);

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
