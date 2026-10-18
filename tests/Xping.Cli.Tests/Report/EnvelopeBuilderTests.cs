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
            latestRun: null, scope: scope, test: test);

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
        var scope = new ReportScope(TestSessionFactory.DefaultAssembly, WindowResolution.Store, 5);

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
            latestRun: null, id: "f_first", test: History(context)));
    }

    [Fact]
    public void TheJsonCarriesTheHistoryBetweenSelectionAndFindings()
    {
        AnalysisContext context = TestSessionFactory.Context(
            TestSessionFactory.Session(0, [TestSessionFactory.Execution("Target", TestOutcome.Timeout)]),
            TestSessionFactory.Session(1, [TestSessionFactory.Execution("Target", TestOutcome.NotExecuted)]));

        using JsonDocument document = JsonDocument.Parse(Json(Build(context, AnalysisResult.Empty, History(context))));
        JsonElement root = document.RootElement;

        Assert.Equal("1.23", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(
            ["schemaVersion", "window", "context", "summary", "latestRun", "selection", "test", "findings", "truncated"],
            root.EnumerateObject().Select(property => property.Name));

        JsonElement test = root.GetProperty("test");
        Assert.Equal(
            ["query", "test", "sessions", "counts", "duration", "findingsWithheld", "findingRows", "candidates"],
            test.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            ["notExecuted", "timeout"],
            test.GetProperty("sessions").EnumerateArray().Select(s => s.GetProperty("outcome").GetString()));
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, ReportJsonOptions.Default);

    private sealed record StubEvidence : FindingEvidence;
}
