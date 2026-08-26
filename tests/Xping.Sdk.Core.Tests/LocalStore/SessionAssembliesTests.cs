/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Statistics;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Sdk.Core.Tests.LocalStore;

public sealed class SessionAssembliesTests
{
    private static TestExecution Execution(string name, string assembly)
    {
        TestIdentity identity = new TestIdentityBuilder()
            .WithTestFingerprint($"fingerprint-{name}")
            .WithFullyQualifiedName($"{assembly}.SampleTests.{name}")
            .WithAssembly(assembly)
            .WithMethodName(name)
            .Build();

        return new TestExecutionBuilder()
            .WithExecutionId(Guid.NewGuid())
            .WithIdentity(identity)
            .WithTestName(name)
            .WithOutcome(TestOutcome.Passed)
            .Build();
    }

    private static TestSession Session(params TestExecution[] executions)
    {
        EnvironmentInfo environment = new EnvironmentInfoBuilder()
            .WithMachineName("dev-box")
            .AddCustomProperties(new Dictionary<string, string> { ["Git.Branch"] = "main" })
            .Build();

        return new TestSessionBuilder()
            .WithSessionId(Guid.NewGuid())
            .WithStartedAt(new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc))
            .WithEndedAt(new DateTime(2026, 8, 1, 9, 5, 0, DateTimeKind.Utc))
            .WithEnvironmentInfo(environment)
            .AddExecutions(executions)
            .WithTotalTestsExpected(executions.Length)
            .WithSessionState(TestSessionState.Finalized)
            .WithQuickStatistics(new QuickStatistics())
            .Build();
    }

    // -----------------------------------------------------------------------
    // Of
    // -----------------------------------------------------------------------

    [Fact]
    public void OfReturnsEveryAssemblyARunCovered()
    {
        TestSession session = Session(
            Execution("A", "Beta.Tests"),
            Execution("B", "Alpha.Tests"),
            Execution("C", "Beta.Tests"));

        Assert.Equal(["Alpha.Tests", "Beta.Tests"], SessionAssemblies.Of(session));
    }

    [Fact]
    public void OfOrdersOrdinallyRatherThanByFirstAppearance()
    {
        // Analysis output has to be byte-identical across runs, and a test host is free to interleave
        // two projects differently each time. Execution order would leak that into the report.
        TestSession session = Session(
            Execution("A", "Zeta.Tests"),
            Execution("B", "Alpha.Tests"));

        Assert.Equal(["Alpha.Tests", "Zeta.Tests"], SessionAssemblies.Of(session));
    }

    [Fact]
    public void OfIgnoresExecutionsThatNameNoAssembly()
    {
        TestSession session = Session(
            Execution("A", string.Empty),
            Execution("B", "Alpha.Tests"));

        Assert.Equal(["Alpha.Tests"], SessionAssemblies.Of(session));
    }

    [Fact]
    public void OfReturnsNothingForARunThatNamedNoAssemblyAtAll()
    {
        // An unattributable run is reported as covering nothing rather than guessed at: it cannot be
        // scoped to, and inventing a name for it would put another suite's history in its report.
        TestSession session = Session(Execution("A", string.Empty));

        Assert.Empty(SessionAssemblies.Of(session));
    }

    [Fact]
    public void OfTreatsANullSessionAsCoveringNothing() =>
        Assert.Empty(SessionAssemblies.Of(null));

    // -----------------------------------------------------------------------
    // Covers
    // -----------------------------------------------------------------------

    [Fact]
    public void CoversFindsAnAssemblyThatIsNotTheFirstOneNamed()
    {
        TestSession session = Session(
            Execution("A", "Alpha.Tests"),
            Execution("B", "Beta.Tests"));

        Assert.True(SessionAssemblies.Covers(session, "Beta.Tests"));
        Assert.False(SessionAssemblies.Covers(session, "Gamma.Tests"));
    }

    [Fact]
    public void CoversIsCaseSensitive()
    {
        TestSession session = Session(Execution("A", "Alpha.Tests"));

        Assert.False(SessionAssemblies.Covers(session, "alpha.tests"));
    }

    // -----------------------------------------------------------------------
    // Project
    // -----------------------------------------------------------------------

    [Fact]
    public void ProjectKeepsOnlyTheRequestedAssemblysExecutions()
    {
        TestSession session = Session(
            Execution("A", "Alpha.Tests"),
            Execution("B", "Beta.Tests"),
            Execution("C", "Alpha.Tests"));

        TestSession projected = Assert.IsType<TestSession>(
            SessionAssemblies.Project(session, "Alpha.Tests"));

        Assert.Equal(2, projected.Executions.Count);
        Assert.All(projected.Executions, e => Assert.Equal("Alpha.Tests", e.Identity.Assembly));
    }

    [Fact]
    public void ProjectReturnsNullWhenTheRunNeverTouchedTheAssembly()
    {
        // Null distinguishes "not part of this assembly's history" from "a run of it that executed
        // nothing". The first must not occupy a window slot; the second is a real, if empty, run.
        TestSession session = Session(Execution("A", "Alpha.Tests"));

        Assert.Null(SessionAssemblies.Project(session, "Beta.Tests"));
    }

    [Fact]
    public void ProjectCarriesTheFieldsAScopedReportStillReads()
    {
        TestSession session = Session(
            Execution("A", "Alpha.Tests"),
            Execution("B", "Beta.Tests"));

        TestSession projected = Assert.IsType<TestSession>(
            SessionAssemblies.Project(session, "Alpha.Tests"));

        // SessionId and StartedAt keep a projection sorting and de-duplicating as the run it came
        // from; EnvironmentInfo is how the CLI knows whether the project is cloud-connected.
        Assert.Equal(session.SessionId, projected.SessionId);
        Assert.Equal(session.StartedAt, projected.StartedAt);
        Assert.Equal(session.EndedAt, projected.EndedAt);
        Assert.Equal(session.SessionState, projected.SessionState);
        Assert.Equal("main", projected.EnvironmentInfo.CustomProperties["Git.Branch"]);
    }

    [Fact]
    public void ProjectDropsCountsThatDescribeTheWholeRun()
    {
        TestSession session = Session(
            Execution("A", "Alpha.Tests"),
            Execution("B", "Beta.Tests"));

        TestSession projected = Assert.IsType<TestSession>(
            SessionAssemblies.Project(session, "Alpha.Tests"));

        Assert.Null(projected.TotalTestsExpected);
        Assert.Null(projected.QuickStatistics);
    }

    [Fact]
    public void ProjectPreservesTheSdkVersionThatRecordedTheRun()
    {
        // Guards against rebuilding through TestSessionBuilder, which stamps the running assembly's
        // version and would relabel a session recorded by an older SDK with the reader's version.
        TestSession session = new()
        {
            SessionId = Guid.NewGuid(),
            StartedAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
            SdkVersion = "0.0.1-ancient",
            Executions = [Execution("A", "Alpha.Tests"), Execution("B", "Beta.Tests")]
        };

        TestSession projected = Assert.IsType<TestSession>(
            SessionAssemblies.Project(session, "Alpha.Tests"));

        Assert.Equal("0.0.1-ancient", projected.SdkVersion);
    }

    // -----------------------------------------------------------------------
    // Excluding
    // -----------------------------------------------------------------------

    [Fact]
    public void ExcludingKeepsEveryOtherAssemblysExecutions()
    {
        TestSession session = Session(
            Execution("A", "Alpha.Tests"),
            Execution("B", "Beta.Tests"),
            Execution("C", "Gamma.Tests"));

        TestSession remaining = Assert.IsType<TestSession>(
            SessionAssemblies.Excluding(session, "Beta.Tests"));

        Assert.Equal(["Alpha.Tests", "Gamma.Tests"], SessionAssemblies.Of(remaining));
    }

    [Fact]
    public void ExcludingReturnsNullWhenTheRunRecordedNothingElse()
    {
        // Null is the signal to delete the run outright: there is no history left to keep.
        TestSession session = Session(
            Execution("A", "Alpha.Tests"),
            Execution("B", "Alpha.Tests"));

        Assert.Null(SessionAssemblies.Excluding(session, "Alpha.Tests"));
    }

    [Fact]
    public void ExcludingIsTheComplementOfProject()
    {
        TestSession session = Session(
            Execution("A", "Alpha.Tests"),
            Execution("B", "Beta.Tests"),
            Execution("C", "Alpha.Tests"));

        TestSession kept = Assert.IsType<TestSession>(
            SessionAssemblies.Project(session, "Alpha.Tests"));
        TestSession dropped = Assert.IsType<TestSession>(
            SessionAssemblies.Excluding(session, "Alpha.Tests"));

        // Between them they account for the whole run and share nothing.
        Assert.Equal(
            session.Executions.Count, kept.Executions.Count + dropped.Executions.Count);
        Assert.Empty(kept.Executions.Intersect(dropped.Executions));
    }

    [Fact]
    public void ExcludingPreservesTheRunItStrips()
    {
        TestSession session = Session(
            Execution("A", "Alpha.Tests"),
            Execution("B", "Beta.Tests"));

        TestSession remaining = Assert.IsType<TestSession>(
            SessionAssemblies.Excluding(session, "Alpha.Tests"));

        // The run is the same run — it merely records less. Its id and timing have to survive or the
        // rewritten file would land under a different name than the one it replaced.
        Assert.Equal(session.SessionId, remaining.SessionId);
        Assert.Equal(session.StartedAt, remaining.StartedAt);
        Assert.Equal(session.SdkVersion, remaining.SdkVersion);
    }

    [Fact]
    public void ProjectReturnsTheSessionItselfWhenItCoversNothingElse()
    {
        // The common case is a single-project `dotnet test`, which should not pay for a copy.
        TestSession session = Session(
            Execution("A", "Alpha.Tests"),
            Execution("B", "Alpha.Tests"));

        Assert.Same(session, SessionAssemblies.Project(session, "Alpha.Tests"));
    }
}
