/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xping.Cli.Commands;
using Xping.Cli.Report;
using Xping.Cli.Report.Model;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Cli.Tests.Report;

// Resolves the store from a temp directory and mutates XPING_NO_BANNER.
[Collection("Sequential")]
public sealed class ReportEnvelopeTests : IDisposable
{
    private readonly string _root;

    public ReportEnvelopeTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-envelope-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("XPING_LOCAL_STORE", _root);
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, "1");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XPING_LOCAL_STORE", null);
        Environment.SetEnvironmentVariable(CtaThrottle.SuppressBannerVariable, null);

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    // The resolver that owns this layout is internal to the SDK and not visible here, so the path is
    // rebuilt. Kept in one place so a layout change surfaces as one failing helper, not ten tests.
    private string SessionsDirectory => Path.Combine(_root, "sessions");

    /// <summary>
    /// Writes sessions in which several tests stop running, so the report has findings to rank.
    /// </summary>
    /// <param name="vanishingTests">Tests that stop.</param>
    /// <param name="total">Sessions to write.</param>
    /// <param name="presentIn">How many of the oldest sessions run the vanishing tests.</param>
    /// <remarks>
    /// <para>
    /// The suite is sized from <paramref name="vanishingTests"/> rather than fixed, because these
    /// tests want findings and the kind is squeezed from both sides at once.
    /// </para>
    /// <para>
    /// Too few stable tests and the later sessions drop below half the suite, which reads as runs
    /// that covered part of it — see <see cref="LocalAnalysisConstants.PartialSessionShare"/> — and
    /// a kind reading absence rightly declines them. Too many and the Benjamini-Hochberg pass, whose
    /// bar is <see cref="LocalAnalysisConstants.FalseDiscoveryRate"/> times the discoveries over the
    /// family, tightens past the 1/56 these eight sessions can produce. One more stable test than
    /// vanishing ones sits comfortably inside both, at every count seeded here. <c>Vanished</c> is
    /// only the most convenient finding to make several of; neither bound is what is under test.
    /// </para>
    /// </remarks>
    private static void SeedVanishing(int vanishingTests = 1, int total = 8, int presentIn = 5)
    {
        ILocalSessionStore store = LocalSessionStore.Create();

        for (int i = 0; i < total; i++)
        {
            var executions = new List<TestExecution>();

            for (int t = 0; t <= vanishingTests; t++)
                executions.Add(TestSessionFactory.Execution($"Stable{t}"));

            if (i < presentIn)
            {
                for (int t = 0; t < vanishingTests; t++)
                    executions.Add(TestSessionFactory.Execution($"Removed{t}"));
            }

            store.Write(TestSessionFactory.Session(i, executions));
        }
    }

    /// <summary>
    /// Writes 17 runs of a suite, then optionally 3 runs naming a single test.
    /// </summary>
    /// <param name="filtered">Whether to append the runs that cover part of the suite.</param>
    /// <remarks>
    /// The shape an ordinary inner loop produces, and the one issue #204 was reported on: a stretch
    /// of full runs, then a `dotnet test --filter` loop long enough to fill the current slice.
    /// </remarks>
    private static void SeedFilteredTail(bool filtered)
    {
        ILocalSessionStore store = LocalSessionStore.Create();

        for (int i = 0; i < 17; i++)
        {
            var executions = new List<TestExecution> { TestSessionFactory.Execution("Selected") };

            // A test that stops running 3 full runs before the end, so `Vanished` has something to
            // report and the finding carries a population marker to read.
            if (i < 14)
                executions.Add(TestSessionFactory.Execution("Removed"));

            for (int t = 0; t < 15; t++)
                executions.Add(TestSessionFactory.Execution($"Stable{t}"));

            store.Write(TestSessionFactory.Session(i, executions));
        }

        if (!filtered)
            return;

        for (int i = 0; i < 3; i++)
            store.Write(TestSessionFactory.Session(17 + i, [TestSessionFactory.Execution("Selected")]));
    }

    /// <summary>
    /// A run under a filter changes what the report says it could measure, and nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The issue this pins claimed three filtered runs push tests under the per-test evidence
    /// floor. They do not, and cannot: the floor reads the runs the subject itself appeared in, so
    /// a test absent from a filtered run simply has fewer of them, and the window count the
    /// filtered runs inflate is not what decides — see
    /// <c>TheWindowArmOfTheFloorNeverDecidesOnItsOwn</c>. Asserted equal across the two stores
    /// rather than argued, so a change to either constant surfaces here.
    /// </para>
    /// <para>
    /// What does move is <c>notMeasured</c>. With nothing but filtered runs in the current slice
    /// the duration kinds cannot read the tests those runs did not select, and the report says so
    /// instead of reporting a suite it measured in full.
    /// </para>
    /// </remarks>
    [Fact]
    public void FilteredRunsMoveWhatCouldBeMeasuredAndNotTheEvidenceFloor()
    {
        SeedFilteredTail(filtered: true);

        JsonElement root = RunJson("--all");
        JsonElement summary = root.GetProperty("summary");

        Assert.Equal(20, root.GetProperty("window").GetProperty("sessionCount").GetInt32());
        Assert.Equal(3, summary.GetProperty("partialSessions").GetInt32());

        // The filtered runs add no test the full runs did not already hold, so the denominator the
        // headline states is unmoved by them.
        Assert.Equal(17, summary.GetProperty("tests").GetInt32());
        Assert.Equal(0, summary.GetProperty("excludedLowEvidence").GetInt32());

        // Fifteen, not sixteen. The suite is `Selected`, `Removed` and fifteen `Stable` tests:
        // `Selected` ran in the filtered runs and needs nothing, the fifteen are waiting on a run
        // of the suite, and `Removed` is not here at all — it has genuinely gone, and is reported
        // once as `Vanished` below rather than a second time as a measurement duration could not
        // take.
        Assert.Equal(
            15,
            summary.GetProperty("notMeasured").GetProperty("DurationRegression")
                   .GetProperty("awaitingRuns").GetInt32());

        JsonElement vanished = root.GetProperty("findings").EnumerateArray()
            .Single(f => f.GetProperty("kind").GetString() == "Vanished");

        Assert.Equal("excludesPartialRuns", vanished.GetProperty("population").GetString());
        Assert.Equal("Removed", vanished.GetProperty("subject").GetProperty("displayName").GetString());
    }

    /// <summary>
    /// The same store without the filtered runs, so the two can be read against each other.
    /// </summary>
    /// <remarks>
    /// The marker states the rule and not whether it fired, so <c>Vanished</c> carries it here too —
    /// a reader must not have to know whether a window happened to contain a filtered run before
    /// they can tell what a rate was counted over.
    /// </remarks>
    [Fact]
    public void WithoutFilteredRunsTheSameStoreMeasuresEverythingAndKeepsTheMarker()
    {
        SeedFilteredTail(filtered: false);

        JsonElement root = RunJson("--all");
        JsonElement summary = root.GetProperty("summary");

        Assert.Equal(17, root.GetProperty("window").GetProperty("sessionCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("partialSessions").GetInt32());

        Assert.Equal(17, summary.GetProperty("tests").GetInt32());
        Assert.Equal(0, summary.GetProperty("excludedLowEvidence").GetInt32());

        Assert.Equal(
            0,
            summary.GetProperty("notMeasured").GetProperty("DurationRegression")
                   .GetProperty("awaitingRuns").GetInt32());

        JsonElement vanished = root.GetProperty("findings").EnumerateArray()
            .Single(f => f.GetProperty("kind").GetString() == "Vanished");

        Assert.Equal("excludesPartialRuns", vanished.GetProperty("population").GetString());
    }

    /// <summary>
    /// Writes quiet runs, then runs in which one broken lifecycle member takes three tests down.
    /// </summary>
    /// <param name="member">The member the adapter names, or null where it named none.</param>
    /// <remarks>
    /// The one shape that produces a group subject, which is the only subject carrying a cause. Two
    /// stable tests ride along so the failing runs still cover the suite.
    /// </remarks>
    private static void SeedBrokenFixture(string? member)
    {
        ILocalSessionStore store = LocalSessionStore.Create();

        for (int i = 0; i < 8; i++)
        {
            var executions = new List<TestExecution>
            {
                TestSessionFactory.Execution("Stable0"),
                TestSessionFactory.Execution("Stable1")
            };

            foreach (string name in (string[])["Alpha", "Beta", "Gamma"])
            {
                executions.Add(i < 4
                    ? TestSessionFactory.Execution(name)
                    : TestSessionFactory.Execution(
                        name,
                        TestOutcome.Failed,
                        exceptionType: "System.Net.Sockets.SocketException",
                        errorMessage: "Connection refused",
                        failureSite: FailureSite.FixtureSetup,
                        failureSiteMember: member));
            }

            store.Write(TestSessionFactory.Session(i, executions));
        }
    }

    /// <summary>
    /// A cluster is named by what its members share, and each member by its own identity.
    /// </summary>
    /// <remarks>
    /// The group id is never the label. It is a signature hash — the cluster is keyed on the
    /// signature so the subject survives a promotion between kinds — and a hash presented as a cause
    /// is worse than saying the cause was not recorded.
    /// </remarks>
    [Fact]
    public void AClusterIsNamedByItsCauseAndItsMembersByTheirOwnNames()
    {
        SeedBrokenFixture("UnprovisionedDatabase..ctor");

        JsonElement subject = RunJson("--all").GetProperty("findings").EnumerateArray()
            .Select(f => f.GetProperty("subject"))
            .Single(s => s.GetProperty("type").GetString() == "group");

        Assert.Equal(
            "UnprovisionedDatabase..ctor", subject.GetProperty("causeLabel").GetString());

        // A group has no name of its own: it is not a test, and naming it after one of its members
        // is the defect this field exists to remove.
        Assert.Equal(JsonValueKind.Null, subject.GetProperty("shortName").ValueKind);

        Assert.Equal(
            ["SampleTests.Alpha", "SampleTests.Beta", "SampleTests.Gamma"],
            subject.GetProperty("members").EnumerateArray()
                   .Select(m => m.GetProperty("shortName").GetString()));
    }

    /// <summary>
    /// Where the adapter named no member, the cluster is named by where it failed.
    /// </summary>
    [Fact]
    public void AClusterWhoseMemberWasNotRecordedIsNamedByItsSite()
    {
        SeedBrokenFixture(member: null);

        JsonElement subject = RunJson("--all").GetProperty("findings").EnumerateArray()
            .Select(f => f.GetProperty("subject"))
            .Single(s => s.GetProperty("type").GetString() == "group");

        Assert.Equal("fixture setup", subject.GetProperty("causeLabel").GetString());
    }

    private static void SeedWithRevision(int count, string sha, string branch)
    {
        ILocalSessionStore store = LocalSessionStore.Create();

        for (int i = 0; i < count; i++)
        {
            store.Write(TestSessionFactory.Session(
                i, [TestSessionFactory.Execution("Alpha")], sha: sha, branch: branch));
        }
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int code = Program.Run(["report", .. args], output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static JsonElement RunJson(params string[] args)
    {
        var (_, output, _) = Run([.. args, "--format", "json"]);

        using JsonDocument document = JsonDocument.Parse(output);
        return document.RootElement.Clone();
    }

    [Fact]
    public void TheEnvelopeCarriesEveryDocumentedSection()
    {
        SeedVanishing();

        JsonElement root = RunJson();

        Assert.Equal("1.20", root.GetProperty("schemaVersion").GetString());

        JsonElement window = root.GetProperty("window");
        foreach (string key in
            (string[])["from", "to", "sessionCount", "resolution", "currentSliceSize", "sessionIds"])
        {
            Assert.True(window.TryGetProperty(key, out _), $"window.{key} missing");
        }

        JsonElement summary = root.GetProperty("summary");
        foreach (string key in (string[])
        [
            "tests", "findings", "flagged", "healthy", "excludedLowEvidence", "excludedNotSignificant",
            "notMeasured", "environmentalSessions", "incompleteSessions", "unreadableSessions",
            "skewedSessions", "failedProviders"
        ])
        {
            Assert.True(summary.TryGetProperty(key, out _), $"summary.{key} missing");
        }

        JsonElement truncated = root.GetProperty("truncated");
        Assert.True(truncated.TryGetProperty("shown", out _));
        Assert.True(truncated.TryGetProperty("total", out _));
        Assert.Equal(
            "xping report --all", truncated.GetProperty("command").GetString());

        Assert.True(root.TryGetProperty("context", out _));
        Assert.True(root.TryGetProperty("findings", out _));

        JsonElement latestRun = root.GetProperty("latestRun");
        foreach (string key in (string[])
        [
            "sessionId", "startedAt", "sha", "isLikelyEnvironmental", "suppressed", "testsExecuted",
            "testsFailed", "newFailures", "explainedByFindings", "explainedByFindingIds", "failures",
            "failuresShown", "failuresTotal", "overflowCommand"
        ])
        {
            Assert.True(latestRun.TryGetProperty(key, out _), $"latestRun.{key} missing");
        }
    }

    // ---------------------------------------------------------------------------------------
    // latestRun
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Writes <paramref name="passing"/> green runs of a two-test suite, then one run in which
    /// <c>Alpha</c> fails: the shape the section exists for, and one no finding can reach.
    /// </summary>
    private static void SeedRegression(int passing, string? sha = null)
    {
        ILocalSessionStore store = LocalSessionStore.Create();

        for (int i = 0; i < passing; i++)
        {
            store.Write(TestSessionFactory.Session(
                i,
                [TestSessionFactory.Execution("Alpha"), TestSessionFactory.Execution("Stable")],
                sha: sha));
        }

        store.Write(TestSessionFactory.Session(
            passing,
            [
                TestSessionFactory.Execution(
                    "Alpha", TestOutcome.Failed, exceptionType: "System.NullReferenceException", errorMessage: "boom"),
                TestSessionFactory.Execution("Stable")
            ],
            sha: sha));
    }

    [Fact]
    public void TheLatestRunSitsBetweenTheSummaryAndTheFindings()
    {
        SeedRegression(passing: 3);

        var (_, output, _) = Run("--format", "json");

        // Serialised order is declaration order and is part of the contract: the section renders
        // above the findings, and a consumer reading the document top to bottom sees it there.
        int summary = output.IndexOf("\"summary\"", StringComparison.Ordinal);
        int latestRun = output.IndexOf("\"latestRun\"", StringComparison.Ordinal);
        // The list, not the summary's count of the same name.
        int findings = output.IndexOf("\"findings\": [", StringComparison.Ordinal);

        Assert.True(summary < latestRun, "latestRun precedes summary");
        Assert.True(latestRun < findings, "latestRun follows findings");
    }

    [Fact]
    public void ARegressionBelowEveryGateIsReportedAsANewFailure()
    {
        // Four sessions: under MinimumSessionsToReport, so the findings list is empty and this is
        // the only place the report can say anything.
        SeedRegression(passing: 3, sha: "eab98671234567890");

        JsonElement root = RunJson();
        JsonElement latestRun = root.GetProperty("latestRun");

        Assert.Empty(root.GetProperty("findings").EnumerateArray());

        Assert.Equal(
            TestSessionFactory.SessionIdFor(3).ToString("D"),
            latestRun.GetProperty("sessionId").GetString());
        Assert.Equal("eab98671234567890", latestRun.GetProperty("sha").GetString());
        Assert.False(latestRun.GetProperty("suppressed").GetBoolean());
        Assert.False(latestRun.GetProperty("isLikelyEnvironmental").GetBoolean());
        Assert.Equal(2, latestRun.GetProperty("testsExecuted").GetInt32());
        Assert.Equal(1, latestRun.GetProperty("testsFailed").GetInt32());
        Assert.Equal(1, latestRun.GetProperty("newFailures").GetInt32());
        Assert.Equal(0, latestRun.GetProperty("explainedByFindings").GetInt32());
        Assert.Empty(latestRun.GetProperty("explainedByFindingIds").EnumerateArray());
        Assert.Equal(1, latestRun.GetProperty("failuresShown").GetInt32());
        Assert.Equal(1, latestRun.GetProperty("failuresTotal").GetInt32());
        Assert.Equal(JsonValueKind.Null, latestRun.GetProperty("overflowCommand").ValueKind);

        JsonElement failure = Assert.Single(latestRun.GetProperty("failures").EnumerateArray());
        Assert.Equal("new", failure.GetProperty("status").GetString());
        Assert.Equal("passed the previous 3 runs, failed just now", failure.GetProperty("contrast").GetString());
        // Namespace stripped: the trailer identifies the failure, and the namespace is the part
        // that pushes the location off the line.
        Assert.Equal("NullReferenceException", failure.GetProperty("failureSummary").GetString());
        Assert.Equal(3, failure.GetProperty("priorSessions").GetInt32());
        Assert.Equal(0, failure.GetProperty("priorFailures").GetInt32());

        // A single-test subject, named the way every finding's is, and never a group.
        JsonElement subject = failure.GetProperty("subject");
        Assert.Equal("test", subject.GetProperty("type").GetString());
        Assert.Equal("fp-Alpha", subject.GetProperty("fingerprint").GetString());
        Assert.Equal("SampleTests.Alpha", subject.GetProperty("shortName").GetString());
        Assert.Equal(JsonValueKind.Null, subject.GetProperty("causeLabel").ValueKind);
        Assert.Equal("SampleTests.cs", subject.GetProperty("sourceFile").GetString());
    }

    [Fact]
    public void ASingleSessionIsSuppressedButStillPublished()
    {
        SeedRegression(passing: 0);

        JsonElement latestRun = RunJson().GetProperty("latestRun");

        // Distinguishable from a clean run: the counts say a test failed, and suppressed says why
        // no row lists it.
        Assert.True(latestRun.GetProperty("suppressed").GetBoolean());
        Assert.Equal(1, latestRun.GetProperty("testsFailed").GetInt32());
        Assert.Empty(latestRun.GetProperty("failures").EnumerateArray());
        Assert.Equal(0, latestRun.GetProperty("failuresTotal").GetInt32());
    }

    [Fact]
    public void ATestAlreadyCarryingAFindingIsExplainedByItsId()
    {
        // The vanishing fixture's newest sessions carry no failure, so add one: a test that failed
        // in every one of eight sessions is `AlwaysFailing`, and the section defers to it.
        ILocalSessionStore store = LocalSessionStore.Create();
        for (int i = 0; i < 8; i++)
        {
            store.Write(TestSessionFactory.Session(
                i,
                [
                    TestSessionFactory.Execution("Stable"),
                    TestSessionFactory.Execution("Broken", TestOutcome.Failed, errorMessage: "boom")
                ]));
        }

        JsonElement root = RunJson();
        JsonElement latestRun = root.GetProperty("latestRun");

        string findingId = root.GetProperty("findings")[0].GetProperty("id").GetString()!;

        Assert.Equal(1, latestRun.GetProperty("explainedByFindings").GetInt32());
        Assert.Equal([findingId], latestRun.GetProperty("explainedByFindingIds").EnumerateArray().Select(e => e.GetString()));
        Assert.Empty(latestRun.GetProperty("failures").EnumerateArray());
    }

    [Fact]
    public void AKindFilterNarrowsWhatTheSectionDefersToAndNotWhatItReports()
    {
        // A test that failed in every one of eight runs. Unfiltered, the AlwaysFailing finding
        // accounts for it and the section counts it on the closing line; under --kind Flaky that
        // finding is never produced, so the section lists it instead — and the row says what its
        // history is without claiming why nothing explains it.
        ILocalSessionStore store = LocalSessionStore.Create();
        for (int i = 0; i < 8; i++)
        {
            store.Write(TestSessionFactory.Session(
                i,
                [
                    TestSessionFactory.Execution("Stable"),
                    TestSessionFactory.Execution("Broken", TestOutcome.Failed, errorMessage: "boom")
                ]));
        }

        JsonElement unfiltered = RunJson().GetProperty("latestRun");
        Assert.Equal(1, unfiltered.GetProperty("explainedByFindings").GetInt32());
        Assert.Empty(unfiltered.GetProperty("failures").EnumerateArray());

        JsonElement filtered = RunJson("--kind", "Flaky").GetProperty("latestRun");
        Assert.Equal(0, filtered.GetProperty("explainedByFindings").GetInt32());

        JsonElement row = Assert.Single(filtered.GetProperty("failures").EnumerateArray());
        Assert.Equal("seenBefore", row.GetProperty("status").GetString());
        Assert.Equal("failed 8 of 8 runs", row.GetProperty("contrast").GetString());
    }

    [Fact]
    public void AnEnvironmentalRunIsNotCountedAgainstATestsHistory()
    {
        // Four runs: an outage, two clean, then one failure. Counting the outage would make the
        // regression read as a test that has failed before.
        ILocalSessionStore store = LocalSessionStore.Create();
        string[] names = [.. Enumerable.Range(0, 30).Select(i => $"T{i:00}")];

        for (int session = 0; session < 4; session++)
        {
            store.Write(TestSessionFactory.Session(
                session,
                [
                    .. names.Select((name, index) =>
                    {
                        bool failing = (session == 0 && index < 12) || (session == 3 && index == 0);

                        return TestSessionFactory.Execution(
                            name,
                            failing ? TestOutcome.Failed : TestOutcome.Passed,
                            errorMessage: failing ? "boom" : null);
                    })
                ]));
        }

        JsonElement latestRun = RunJson().GetProperty("latestRun");

        Assert.Equal(1, latestRun.GetProperty("newFailures").GetInt32());

        JsonElement row = Assert.Single(latestRun.GetProperty("failures").EnumerateArray());
        Assert.Equal("new", row.GetProperty("status").GetString());
        Assert.Equal("passed the previous 2 runs, failed just now", row.GetProperty("contrast").GetString());
    }

    [Fact]
    public void ExplainingFindingIdsAreInEnvelopeOrder()
    {
        // Two always-failing tests whose fingerprints sort the other way round from their rank:
        // "Zulu" fails in every run and "Alpha" in most, so Zulu ranks first and Alpha's
        // fingerprint sorts first. The ids follow the rank, because that is what the row numbers
        // on the "also failing" line are.
        ILocalSessionStore store = LocalSessionStore.Create();
        for (int i = 0; i < 8; i++)
        {
            store.Write(TestSessionFactory.Session(
                i,
                [
                    TestSessionFactory.Execution("Stable"),
                    TestSessionFactory.Execution("Zulu", TestOutcome.Failed, errorMessage: "boom"),
                    TestSessionFactory.Execution("Alpha", i % 4 == 0 ? TestOutcome.Passed : TestOutcome.Failed, errorMessage: i % 4 == 0 ? null : "boom")
                ]));
        }

        JsonElement root = RunJson();

        string[] ranked = [.. root.GetProperty("findings").EnumerateArray().Select(f => f.GetProperty("id").GetString()!)];
        string[] explained = [.. root.GetProperty("latestRun").GetProperty("explainedByFindingIds").EnumerateArray().Select(e => e.GetString()!)];

        Assert.Equal(2, explained.Length);
        Assert.Equal(ranked.Where(explained.Contains), explained);
    }

    [Fact]
    public void TheOverflowCommandIsTheSameOneTheFindingsTruncationPrints()
    {
        ILocalSessionStore store = LocalSessionStore.Create();
        int many = LocalAnalysisConstants.LatestRunMaxRows + 2;
        string[] names = [.. Enumerable.Range(0, many).Select(i => $"T{i:00}")];
        string[] padding = [.. Enumerable.Range(0, 60).Select(i => $"P{i:00}")];

        store.Write(TestSessionFactory.Session(0, [.. names.Concat(padding).Select(n => TestSessionFactory.Execution(n))]));
        store.Write(TestSessionFactory.Session(
            1,
            [
                .. names.Select(n => TestSessionFactory.Execution(n, TestOutcome.Failed, errorMessage: "boom")),
                .. padding.Select(n => TestSessionFactory.Execution(n))
            ]));

        JsonElement capped = RunJson().GetProperty("latestRun");
        Assert.Equal(LocalAnalysisConstants.LatestRunMaxRows, capped.GetProperty("failuresShown").GetInt32());
        Assert.Equal(many, capped.GetProperty("failuresTotal").GetInt32());
        Assert.Equal("xping report --all", capped.GetProperty("overflowCommand").GetString());

        JsonElement lifted = RunJson("--all").GetProperty("latestRun");
        Assert.Equal(many, lifted.GetProperty("failuresShown").GetInt32());
        Assert.Equal(JsonValueKind.Null, lifted.GetProperty("overflowCommand").ValueKind);
    }

    [Fact]
    public void EveryContrastIsAscii()
    {
        ILocalSessionStore store = LocalSessionStore.Create();
        store.Write(TestSessionFactory.Session(0, [TestSessionFactory.Execution("Known", TestOutcome.Failed, errorMessage: "boom"), TestSessionFactory.Execution("Regressed")]));
        store.Write(TestSessionFactory.Session(1, [TestSessionFactory.Execution("Known"), TestSessionFactory.Execution("Regressed")]));
        store.Write(TestSessionFactory.Session(
            2,
            [
                TestSessionFactory.Execution("Known", TestOutcome.Failed, errorMessage: "boom"),
                TestSessionFactory.Execution("Regressed", TestOutcome.Failed, errorMessage: "boom"),
                TestSessionFactory.Execution("Fresh", TestOutcome.Failed, errorMessage: "boom")
            ]));

        JsonElement failures = RunJson().GetProperty("latestRun").GetProperty("failures");

        // The glyph set is chosen after the builder runs and never reaches these, so a non-ASCII
        // character here would survive into piped output whatever --ascii said.
        string[] contrasts = [.. failures.EnumerateArray().Select(f => f.GetProperty("contrast").GetString()!)];
        Assert.Equal(
            [
                "passed the previous 2 runs, failed just now",
                "first seen this run, failed",
                "failed 2 of 3 runs"
            ],
            contrasts);
        Assert.All(contrasts, c => Assert.All(c, ch => Assert.True(ch < 0x80, $"non-ASCII in '{c}'")));
    }

    [Fact]
    public void AFindingCarriesItsSubjectAndSourceLocation()
    {
        SeedVanishing();

        JsonElement finding = RunJson().GetProperty("findings")[0];

        Assert.StartsWith("f_", finding.GetProperty("id").GetString(), StringComparison.Ordinal);
        Assert.Equal("Vanished", finding.GetProperty("kind").GetString());
        Assert.Equal("low", finding.GetProperty("severity").GetString());
        Assert.Equal("low", finding.GetProperty("evidenceLevel").GetString());

        // The number the band above was decided from, published so it can be reconciled with the
        // counts inside `evidence` rather than taken on trust. For a vanished test it is the
        // baseline runs it appeared in, which is what its absence is a change from — not the runs
        // in the window, and not the runs it ran in on either side of the slice boundary.
        Assert.Equal(
            finding.GetProperty("evidence").GetProperty("baselineSessions").GetInt32(),
            finding.GetProperty("evidenceSessions").GetInt32());

        // Which runs the counts below were taken over. Vanished counts session appearances and sets
        // aside the runs that covered part of the suite, so it says so rather than leaving the
        // reader to infer it — and says so whether or not any run in this window was set aside,
        // because the marker states the rule and not whether it fired.
        Assert.Equal("excludesPartialRuns", finding.GetProperty("population").GetString());

        JsonElement subject = finding.GetProperty("subject");
        Assert.Equal("test", subject.GetProperty("type").GetString());
        Assert.Equal("fp-Removed0", subject.GetProperty("fingerprint").GetString());
        Assert.Equal(
            "MyApp.Tests.SampleTests.Removed0",
            subject.GetProperty("fullyQualifiedName").GetString());

        // The name the report shows, resolved on the envelope rather than by a renderer: the
        // namespace goes, the class stays, and what is left is what a reader greps for and pastes
        // after `dotnet test --filter FullyQualifiedName~`. The whole name is still beside it.
        Assert.Equal("SampleTests.Removed0", subject.GetProperty("shortName").GetString());

        // A single test has no shared cause to name. Written rather than dropped, so a consumer can
        // tell a subject that has none from a field this build stopped emitting.
        Assert.Equal(JsonValueKind.Null, subject.GetProperty("causeLabel").ValueKind);

        // Never stripped for brevity: this is what lets an agent open the file.
        Assert.Equal("SampleTests.cs", subject.GetProperty("sourceFile").GetString());
        Assert.Equal(10, subject.GetProperty("sourceLineNumber").GetInt32());

        Assert.NotEqual(
            JsonValueKind.Null, finding.GetProperty("evidence").ValueKind);
        Assert.False(string.IsNullOrEmpty(finding.GetProperty("drillDown").GetString()));
    }

    [Fact]
    public void EveryKindRecordsWhichPopulationItsRatesAreTakenOver()
    {
        // The report ranks kinds against each other and they do not all count the same executions,
        // so a kind with no rule recorded would publish a rate a reader cannot place. Exhaustive by
        // construction: a kind added to the enum without a decision throws here rather than quietly
        // claiming it counted everything.
        foreach (FindingKind kind in Enum.GetValues<FindingKind>())
            Assert.True(Enum.IsDefined(PopulationRules.For(kind)));

        // And every arm is reachable, so the marker actually distinguishes findings rather than
        // printing one word on every line.
        Assert.Equal(
            Enum.GetValues<PopulationRule>().Length,
            Enum.GetValues<FindingKind>().Select(PopulationRules.For).Distinct().Count());

        // Vanished is the only kind counted in runs rather than executions, and the only one that
        // sets a run aside for having covered part of the suite. A second kind arriving here means
        // the argument in `docs/internals/finding-populations.md` needs revisiting rather than
        // extending, so it is pinned rather than left to be noticed.
        Assert.Equal(
            [FindingKind.Vanished],
            Enum.GetValues<FindingKind>()
                .Where(kind => PopulationRules.For(kind) == PopulationRule.ExcludesPartialRuns));
    }

    [Fact]
    public void ContextIsPopulatedFromTheRecordedCommit()
    {
        SeedWithRevision(6, "a3f9c2e1d0b7a4f18e6c5d3b2a190f8e7d6c5b4a", "main");

        JsonElement context = RunJson().GetProperty("context");

        Assert.Equal(
            "a3f9c2e1d0b7a4f18e6c5d3b2a190f8e7d6c5b4a", context.GetProperty("sha").GetString());
        Assert.Equal("main", context.GetProperty("branch").GetString());

        // There is deliberately no `dirty`: the SDK records staged changes, which is a different
        // question, and a field that is wrong for unstaged edits is worse than one that is absent.
        Assert.False(context.TryGetProperty("dirty", out _));
    }

    [Fact]
    public void TruncationIsAccurateAndTheFullListIsReachable()
    {
        SeedVanishing(vanishingTests: 5);

        JsonElement limited = RunJson("--top", "2");

        Assert.Equal(2, limited.GetProperty("findings").GetArrayLength());
        Assert.Equal(2, limited.GetProperty("truncated").GetProperty("shown").GetInt32());
        Assert.Equal(5, limited.GetProperty("truncated").GetProperty("total").GetInt32());

        // The summary counts everything produced, not merely what is shown.
        Assert.Equal(5, limited.GetProperty("summary").GetProperty("findings").GetInt32());

        JsonElement all = RunJson("--all");
        Assert.Equal(5, all.GetProperty("findings").GetArrayLength());
        Assert.Equal(5, all.GetProperty("truncated").GetProperty("shown").GetInt32());
    }

    [Fact]
    public void TruncationDoesNotChangeTheExitCode()
    {
        // Hiding the sixth finding must not also hide its effect on the threshold, or --top would
        // quietly decide whether the build fails.
        SeedVanishing(vanishingTests: 5);

        var (limited, _, _) = Run("--top", "1", "--fail-on", "low", "--format", "json");
        var (full, _, _) = Run("--all", "--fail-on", "low", "--format", "json");

        Assert.Equal(full, limited);
        Assert.Equal(1, limited);
    }

    [Fact]
    public void HealthyCountsTestsNoFindingNamed()
    {
        SeedVanishing(vanishingTests: 2);

        JsonElement summary = RunJson().GetProperty("summary");

        // Five distinct tests in the window; two vanished.
        Assert.Equal(5, summary.GetProperty("tests").GetInt32());
        Assert.Equal(2, summary.GetProperty("findings").GetInt32());
        Assert.Equal(2, summary.GetProperty("flagged").GetInt32());
        Assert.Equal(3, summary.GetProperty("healthy").GetInt32());

        // The line has to add up. Here one finding names one test and the two counts happen to
        // agree, which is exactly the case that hides a deduplication going wrong.
        Assert.Equal(
            summary.GetProperty("tests").GetInt32(),
            summary.GetProperty("healthy").GetInt32() + summary.GetProperty("flagged").GetInt32());
    }

    /// <summary>
    /// One finding covering three tests is one finding and three unhealthy tests.
    /// </summary>
    /// <remarks>
    /// The shape that makes the count worth publishing. Without it a reader sees one finding beside
    /// two healthy tests out of five and reads an arithmetic error — the deduplication that reconciles
    /// them happens in the builder and is invisible from the report.
    /// </remarks>
    [Fact]
    public void FlaggedCountsTestsAndNotFindings()
    {
        SeedBrokenFixture("UnprovisionedDatabase..ctor");

        JsonElement summary = RunJson("--all").GetProperty("summary");

        Assert.Equal(5, summary.GetProperty("tests").GetInt32());
        Assert.Equal(1, summary.GetProperty("findings").GetInt32());
        Assert.Equal(3, summary.GetProperty("flagged").GetInt32());
        Assert.Equal(2, summary.GetProperty("healthy").GetInt32());
    }

    [Fact]
    public void UnreadableSessionsAreSkippedCountedAndWarnedAbout()
    {
        SeedVanishing();

        File.WriteAllText(
            Path.Combine(SessionsDirectory, "session-0000000000000000001-aaaaaaaa.json.gz"),
            "not gzip at all");

        string truncated =
            Path.Combine(SessionsDirectory, "session-0000000000000000002-bbbbbbbb.json.gz");
        using (var file = new FileStream(truncated, FileMode.Create))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            writer.Write("{\"sessionId\":\"" + Guid.NewGuid().ToString("D") + "\",\"executions\":[{");
        }

        var (code, output, _) = Run("--format", "json");

        // The report still renders, and says how much history it could not see.
        Assert.Equal(0, code);

        using JsonDocument document = JsonDocument.Parse(output);
        Assert.Equal(
            2, document.RootElement.GetProperty("summary").GetProperty("unreadableSessions").GetInt32());
        Assert.Equal(
            8, document.RootElement.GetProperty("window").GetProperty("sessionCount").GetInt32());
    }

    [Fact]
    public void ARunFromAFastClockIsExcludedCountedAndWarnedAbout()
    {
        SeedVanishing();

        // A machine sharing this checkout whose clock runs four days fast. Its stamp is the largest
        // in the store, so the window would take it as the instant every other finding is aged
        // against — and it does not have to be the run carrying a finding to do that.
        LocalSessionStore.Create().Write(TestSessionFactory.Session(
            99,
            [TestSessionFactory.Execution("Stable0")],
            startedAt: DateTime.UtcNow.AddDays(4)));

        var (code, output, error) = Run("--format", "json");

        // The report still renders, over the runs it can date, and says what it left out.
        Assert.Equal(0, code);

        using JsonDocument document = JsonDocument.Parse(output);
        JsonElement root = document.RootElement;

        Assert.Equal(1, root.GetProperty("summary").GetProperty("skewedSessions").GetInt32());
        Assert.Equal(8, root.GetProperty("window").GetProperty("sessionCount").GetInt32());
        Assert.True(root.GetProperty("window").GetProperty("to").GetDateTime() <= DateTime.UtcNow);

        // On standard error, where the JSON reader still sees it: the defect is the clock on the
        // machine that recorded the run, and nothing in the suite will fix it.
        Assert.Contains("ahead of this machine's clock", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTextReportSaysWhenARunWasLeftOutForItsClock()
    {
        SeedVanishing();

        LocalSessionStore.Create().Write(TestSessionFactory.Session(
            99,
            [TestSessionFactory.Execution("Stable0")],
            startedAt: DateTime.UtcNow.AddDays(4)));

        var (_, output, _) = Run();

        Assert.Contains("stamped ahead of this machine's clock", output, StringComparison.Ordinal);
    }

    [Fact]
    public void WarningsGoToStandardErrorSoJsonStaysParsable()
    {
        SeedVanishing();

        File.WriteAllText(
            Path.Combine(SessionsDirectory, "session-0000000000000000001-aaaaaaaa.json.gz"),
            "not gzip at all");

        var (_, output, _) = Run("--format", "json");

        // Would throw if a warning had been interleaved into stdout.
        using JsonDocument document = JsonDocument.Parse(output);
        Assert.Equal("1.20", document.RootElement.GetProperty("schemaVersion").GetString());
    }

    [Fact]
    public void TwoRunsOverAnUnchangedStoreProduceByteIdenticalJson()
    {
        SeedVanishing(vanishingTests: 4);

        var (_, first, _) = Run("--format", "json");
        var (_, second, _) = Run("--format", "json");

        Assert.Equal(first, second);
    }

    [Fact]
    public void TheTextReportIsAlsoStableAcrossRuns()
    {
        SeedVanishing(vanishingTests: 4);

        var (_, first, _) = Run("--ascii");
        var (_, second, _) = Run("--ascii");

        Assert.Equal(first, second);
    }

    [Fact]
    public void TheKindFilterNarrowsTheFindings()
    {
        SeedVanishing(vanishingTests: 3);

        Assert.Equal(3, RunJson("--kind", "Vanished").GetProperty("findings").GetArrayLength());

        // A kind no provider implements yet is valid input and correctly yields nothing.
        Assert.Equal(0, RunJson("--kind", "Flaky").GetProperty("findings").GetArrayLength());
    }
}
