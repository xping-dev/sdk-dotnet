/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.ObjectModel;
using System.Text.Json;
using Xping.Cli.Report;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Cli.Report.Windowing;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// Under <c>--test</c> the envelope is the full report's with the findings about one test in it.
/// </summary>
public sealed class EnvelopeBuilderTests
{
    private const string Target = "fp-Target";

    private static AnalysisContext Context(int runs) =>
        TestSessionFactory.Context(
        [
            .. Enumerable.Range(0, runs).Select(ordinal => TestSessionFactory.Session(
                ordinal, "Target", "Other0", "Other1"))
        ]);

    private static TestHistoryDto History(AnalysisContext context) =>
        TestHistoryBuilder.Found(
            context,
            "Target",
            new TestMatch(
                Target, Target, TestSessionFactory.DefaultAssembly, "MyApp.Tests.SampleTests.Target",
                TestSessionFactory.Epoch, context.Window.SessionCount));

    private static Finding Single(string id, string fingerprint, Severity severity) =>
        new(
            id,
            FindingKind.Flaky,
            severity,
            EvidenceLevel.Moderate,
            5,
            new FindingSubject.SingleTest(Reference(fingerprint)),
            new StubEvidence(),
            0.5);

    private static Finding Group(string id, params string[] members) =>
        new(
            id,
            FindingKind.BrokenFixture,
            Severity.Medium,
            EvidenceLevel.Moderate,
            5,
            new FindingSubject.Group("g-1", [.. members.Select(Reference)]),
            new StubEvidence(),
            0.5);

    private static TestReference Reference(string fingerprint) =>
        new(fingerprint, $"MyApp.Tests.SampleTests.{fingerprint}", fingerprint, null, null, TestSessionFactory.DefaultAssembly);

    /// <summary>
    /// Rows 1 to 4 of the full report: another test, a cluster holding the target, then two findings
    /// about the target itself.
    /// </summary>
    private static AnalysisResult Findings() =>
        new(
            [
                Single("f_other", "fp-Other0", Severity.High),
                Group("f_group", Target, "fp-Other1"),
                Single("f_first", Target, Severity.Medium),
                Single("f_second", Target, Severity.Low)
            ],
            [],
            0,
            0,
            ReadOnlyDictionary<FindingKind, NotMeasuredCount>.Empty);

    private static ReportEnvelope Build(
        AnalysisContext context, AnalysisResult result, TestHistoryDto? test, ReportScope? scope = null) =>
        EnvelopeBuilder.Build(
            context, result, incompleteSessions: 0, unreadableSessions: 0, skewedSessions: 0, top: null,
            latestRun: null, scope ?? TestSessionFactory.Scope(context), test: test);

    [Fact]
    public void OnlyTheFindingsAboutTheTestAreShownWithClustersLast()
    {
        AnalysisContext context = Context(5);

        ReportEnvelope envelope = Build(context, Findings(), History(context));

        Assert.Equal(["f_first", "f_second", "f_group"], envelope.Findings.Select(f => f.Id));
    }

    [Fact]
    public void EachFindingKeepsTheRowTheFullReportGivesIt()
    {
        AnalysisContext context = Context(5);

        TestHistoryDto history = Assert.IsType<TestHistoryDto>(Build(context, Findings(), History(context)).Test);

        Assert.Equal(
            [
                new SameSubjectDto("f_first", "Flaky", 3),
                new SameSubjectDto("f_second", "Flaky", 4),
                new SameSubjectDto("f_group", "BrokenFixture", 2)
            ],
            history.FindingRows);
        Assert.Null(history.FindingsWithheld);
    }

    [Fact]
    public void TheSummaryAndTruncationDescribeTheFullReport()
    {
        AnalysisContext context = Context(5);
        AnalysisResult result = Findings();

        ReportEnvelope full = Build(context, result, null);
        ReportEnvelope history = Build(context, result, History(context));

        Assert.Equal(Json(full.Summary), Json(history.Summary));
        Assert.Equal(new TruncationDto(3, 4, "xping report --all"), history.Truncated);
        Assert.Null(history.Selection);
        Assert.Null(full.Test);
    }

    [Fact]
    public void UnderAWholeStoreWindowEveryCommandPinsItsSize()
    {
        AnalysisContext context = Context(5);
        var scope = new ReportScope(TestSessionFactory.DefaultAssembly, WindowResolution.Store, 5, ShellDialect.Posix);

        ReportEnvelope envelope = Build(context, Findings(), History(context), scope);

        Assert.All(envelope.Findings, f => Assert.EndsWith(" --runs 5", f.DrillDown, StringComparison.Ordinal));
        // The assembly too, though the caller did not type it: under --test it came from the name.
        Assert.Equal("xping report --all --assembly MyApp.Tests --runs 5", envelope.Truncated.Command);
    }

    /// <summary>
    /// D6: below the floors, the empty block says why rather than reading as a healthy test.
    /// </summary>
    [Fact]
    public void BelowTheFloorTheEmptyBlockSaysWhy()
    {
        AnalysisContext context = Context(3);

        TestHistoryDto history = Assert.IsType<TestHistoryDto>(
            Build(context, AnalysisResult.Empty, History(context)).Test);

        Assert.Equal("too little history to judge: findings need 5 runs, this test has 3", history.FindingsWithheld);
        Assert.Empty(history.FindingRows);
    }

    [Fact]
    public void AHealthyTestAboveTheFloorHasNoReason()
    {
        AnalysisContext context = Context(5);

        TestHistoryDto history = Assert.IsType<TestHistoryDto>(
            Build(context, AnalysisResult.Empty, History(context)).Test);

        Assert.Null(history.FindingsWithheld);
    }

    [Fact]
    public void AnUnresolvedNameShowsNoFindings()
    {
        AnalysisContext context = Context(5);

        ReportEnvelope envelope = Build(context, Findings(), TestHistoryBuilder.NotFound("Nope"));

        Assert.Empty(envelope.Findings);
        Assert.Equal("Nope", Assert.IsType<TestHistoryDto>(envelope.Test).Query);
        Assert.Equal(new TruncationDto(0, 4, "xping report --all"), envelope.Truncated);
    }

    [Fact]
    public void IdAndTestCannotBothBeGiven()
    {
        AnalysisContext context = Context(5);

        Assert.Throws<ArgumentException>(() => EnvelopeBuilder.Build(
            context, Findings(), incompleteSessions: 0, unreadableSessions: 0, skewedSessions: 0, top: null,
            latestRun: null, scope: TestSessionFactory.Scope(context), id: "f_first", test: History(context)));
    }

    [Fact]
    public void TheJsonCarriesTheHistoryBetweenSelectionAndFindings()
    {
        AnalysisContext context = TestSessionFactory.Context(
            TestSessionFactory.Session(0, [TestSessionFactory.Execution("Target", TestOutcome.Timeout)]),
            TestSessionFactory.Session(1, [TestSessionFactory.Execution("Target", TestOutcome.NotExecuted)]));

        using JsonDocument document = JsonDocument.Parse(Json(Build(context, AnalysisResult.Empty, History(context))));
        JsonElement root = document.RootElement;

        Assert.Equal("1.26", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(
            ["schemaVersion", "window", "context", "summary", "latestRun", "selection", "test", "findings", "truncated"],
            root.EnumerateObject().Select(property => property.Name));

        JsonElement test = root.GetProperty("test");
        Assert.Equal(
            [
                "query", "test", "shortFingerprint", "sessions", "counts", "duration", "failureModes",
                "findingsWithheld", "findingRows", "candidates"
            ],
            test.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            ["notExecuted", "timeout"],
            test.GetProperty("sessions").EnumerateArray().Select(s => s.GetProperty("outcome").GetString()));

        // The timed-out attempt is a failure, and it recorded nothing to sign.
        JsonElement mode = Assert.Single(test.GetProperty("failureModes").EnumerateArray());
        Assert.Equal(
            [
                "hash", "exceptionType", "message", "frames", "framesDegraded", "unavailable", "timedOut",
                "occurrences",
                "firstSeenAt", "firstSeenSha", "firstSeenSessionsAgo", "firstSeenInLatestSession",
                "firstSeenAfterWindowStart"
            ],
            mode.EnumerateObject().Select(property => property.Name));
        Assert.True(mode.GetProperty("unavailable").GetBoolean());
        Assert.True(mode.GetProperty("timedOut").GetBoolean());
    }

    /// <summary>
    /// The newest session's runner and project describe the project as it is now (D4).
    /// </summary>
    [Fact]
    public void AReproFollowsTheRunnerTheNewestSessionRecorded()
    {
        AnalysisContext context = TestSessionFactory.Context(
            Recorded(0, LocalSessionProperties.VSTestRunner, "tests/Old/Old.csproj"),
            Recorded(1, LocalSessionProperties.TestingPlatformRunner, "tests/New/New.csproj"));

        ReproDto repro = Assert.IsType<ReproDto>(Build(context, Flaky(4, 9), test: null).Findings[0].Repro);

        Assert.StartsWith("dotnet run --project tests/New/New.csproj ", repro.Command, StringComparison.Ordinal);
        Assert.Equal(6, repro.Runs);
        Assert.Equal(0.444, repro.Rate);
    }

    /// <summary>
    /// Amendment 8A: the drill-down and the repro are quoted for the scope's shell, as the text report
    /// prints them.
    /// </summary>
    [Fact]
    public void EveryPrintedCommandIsQuotedForTheScopesShell()
    {
        AnalysisContext context = TestSessionFactory.Context(
            Recorded(0, LocalSessionProperties.VSTestRunner, "tests/O'Brien/O'Brien.csproj"));
        var scope = new ReportScope("MyApp.Tests", WindowResolution.Default, 1, ShellDialect.PowerShell, Directory: "/work/it's");

        FindingDto finding = Build(context, Flaky(4, 9), test: null, scope).Findings[0];

        Assert.EndsWith(" --directory '/work/it''s'", finding.DrillDown, StringComparison.Ordinal);
        Assert.StartsWith("dotnet test 'tests/O''Brien/O''Brien.csproj' ", finding.Repro!.Command, StringComparison.Ordinal);
    }

    [Fact]
    public void NoRunnerRecordedMeansNoRepro()
    {
        AnalysisContext context = TestSessionFactory.Context(Recorded(0, runner: null, "tests/A/A.csproj"));

        Assert.Null(Build(context, Flaky(4, 9), test: null).Findings[0].Repro);
    }

    [Fact]
    public void TheJsonCarriesReproAfterDrillDownAndNullWhereThereIsNone()
    {
        AnalysisContext context = TestSessionFactory.Context(
            Recorded(0, LocalSessionProperties.VSTestRunner, "tests/A/A.csproj"));

        using JsonDocument document = JsonDocument.Parse(Json(Build(context, Flaky(4, 9), test: null)));
        JsonElement finding = document.RootElement.GetProperty("findings")[0];
        List<string> names = [.. finding.EnumerateObject().Select(property => property.Name)];

        Assert.Equal(names.IndexOf("drillDown") + 1, names.IndexOf("repro"));
        Assert.Equal(
            ["command", "runs", "runsNeeded", "runsFailed", "runsJudged", "rate", "expectation"],
            finding.GetProperty("repro").EnumerateObject().Select(property => property.Name));

        using JsonDocument none = JsonDocument.Parse(Json(Build(context, Findings(), test: null)));
        Assert.All(
            none.RootElement.GetProperty("findings").EnumerateArray(),
            f => Assert.Equal(JsonValueKind.Null, f.GetProperty("repro").ValueKind));
    }

    /// <summary>D6, with D5's rate rounded as the headline rounds it (Amendment 5A).</summary>
    [Theory]
    [InlineData(4, 9, "run 6 times: at 4 of 9 runs failing (44.4%), 6 runs show a failure 95% of the time")]
    [InlineData(10, 20, "run 5 times: at 10 of 20 runs failing (50%), 5 runs show a failure 95% of the time")]
    [InlineData(1, 20, "failed in 1 of 20 runs: too rare to reproduce by rerunning (59 runs needed)")]
    [InlineData(19, 20, "run once: it failed in 19 of 20 runs")]
    public void TheExpectationSaysWhatTheRunsWillShow(int failing, int judged, string expected)
    {
        Finding finding = Flaky(4, 9).Findings[0] with { Evidence = FlakyEvidence(failing, judged) };

        Assert.Equal(expected, EnvelopeBuilder.BuildRepro(finding, VSTest, ShellDialect.Posix)!.Expectation);
    }

    /// <summary>
    /// Amendment 6B: past the cap the JSON still says how rare, without parsing the sentence.
    /// </summary>
    [Fact]
    public void ACappedReproCarriesItsUncappedCountAndTheCountsBehindIt()
    {
        Finding finding = Flaky(4, 9).Findings[0] with { Evidence = FlakyEvidence(1, 20) };

        ReproDto repro = EnvelopeBuilder.BuildRepro(finding, VSTest, ShellDialect.Posix)!;

        Assert.Null(repro.Runs);
        Assert.Equal(59, repro.RunsNeeded);
        Assert.Equal(1, repro.RunsFailed);
        Assert.Equal(20, repro.RunsJudged);
        Assert.Equal(0.05, repro.Rate);
    }

    [Fact]
    public void AnAlwaysFailingTestIsRunOnce()
    {
        Finding finding = Flaky(4, 9).Findings[0] with
        {
            Kind = FindingKind.AlwaysFailing,
            Evidence = (AlwaysFailingEvidence)EvidenceSamples.For(FindingKind.AlwaysFailing) with
            {
                RunsFailed = 9,
                RunsJudged = 10
            }
        };

        ReproDto repro = EnvelopeBuilder.BuildRepro(finding, VSTest, ShellDialect.Posix)!;

        Assert.Equal(1, repro.Runs);
        Assert.Equal("run once: it failed in 9 of 10 runs", repro.Expectation);
    }

    private static readonly ReproTarget VSTest = new(LocalSessionProperties.VSTestRunner, null);

    private static TestSession Recorded(int ordinal, string? runner, string projectPath)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LocalSessionProperties.ProjectPath] = projectPath
        };

        if (runner != null)
            properties[LocalSessionProperties.Runner] = runner;

        return TestSessionFactory.Session(
            ordinal, [TestSessionFactory.Execution("Target")], customProperties: properties);
    }

    private static FlakyEvidence FlakyEvidence(int failing, int judged) =>
        (FlakyEvidence)EvidenceSamples.For(FindingKind.Flaky) with { RunsFailed = failing, RunsJudged = judged };

    private static AnalysisResult Flaky(int failing, int judged) =>
        new(
            [Single("f_flaky", Target, Severity.High) with { Evidence = FlakyEvidence(failing, judged) }],
            [],
            0,
            0,
            ReadOnlyDictionary<FindingKind, NotMeasuredCount>.Empty);

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, ReportJsonOptions.Default);

    private sealed record StubEvidence : FindingEvidence;
}
