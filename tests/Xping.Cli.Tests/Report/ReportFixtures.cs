/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
using Xping.Cli.Report;
using Xping.Cli.Report.Providers;
using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Rendering;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The envelopes the text report is pinned against.
/// </summary>
/// <remarks>
/// <para>
/// One producer for two consumers: the tests that assert one rule each, and the golden sweep that
/// renders the whole catalogue and compares it to files on disk. Two builders would be two shapes
/// the goldens could drift from.
/// </para>
/// <para>
/// Nothing here spells a value the envelope resolves for itself. The population token comes from
/// <see cref="PopulationRules.For(FindingKind)"/> and the short name from
/// <see cref="SubjectNames.ShortName"/>, so a fixture cannot pin a spelling the builder would not
/// have produced.
/// </para>
/// </remarks>
internal static class ReportFixtures
{
    /// <summary>Gets the keys the golden sweep and the width and charset sweeps enumerate.</summary>
    /// <remarks>
    /// Each entry exercises a layout path nothing else in the catalogue reaches. Adding a path to
    /// the renderer without adding one here leaves it golden-free, which is the one way this sweep
    /// can quietly stop covering the report.
    /// </remarks>
    public static IReadOnlyList<string> Keys { get; } =
    [
        "empty",
        "nothing-reportable",
        "spec-section-3",
        "every-kind",
        "cluster",
        "siblings",
        "truncated",
        "caveats",
        "long-names",
        "docs-mstest",
        "docs-xunit",
        "docs-checkout",
        "docs-cluster",
        "docs-location",
        "docs-source-location",
        "docs-time-sensitive",
        "docs-retry-deepening",
        "docs-retry-exhausted",
        "latest-run",
        "latest-run-known-only",
        "latest-run-suppressed",
        "latest-run-environmental",
        "latest-run-overflow",
        "latest-run-store",
        "latest-run-only",
        "detail-test",
        "detail-group",
        .. Enum.GetValues<FindingKind>().Select(DetailKey),
        "detail-store"
    ];

    /// <summary>The same keys, as a theory source.</summary>
    /// <returns>The keys.</returns>
    public static TheoryData<string> Names() => [.. Keys];

    /// <summary>
    /// Returns the envelope a key names.
    /// </summary>
    /// <param name="name">The key, as <see cref="Names"/> lists it.</param>
    /// <returns>The envelope.</returns>
    /// <remarks>
    /// Every finding's drill-down is derived from its id and the envelope's assembly, as the
    /// coordinator derives it. Fixtures set ids by hand, and a detail line naming an id the row above
    /// it does not carry would be a golden pinning a command that selects nothing.
    /// </remarks>
    public static ReportEnvelope Get(string name)
    {
        ReportEnvelope envelope = Build(name);

        return envelope with
        {
            Findings =
            [
                .. envelope.Findings.Select(finding =>
                    finding with { DrillDown = DrillDown.ForFinding(finding.Id, envelope.Context?.Assembly) })
            ]
        };
    }

    /// <summary>
    /// Narrows a full envelope to the detail of one of its rows, as <c>--id</c> would build it.
    /// </summary>
    /// <param name="full">The full report, every finding shown.</param>
    /// <param name="row">The row to select, from 1.</param>
    /// <returns>The envelope with one finding and its selection.</returns>
    /// <remarks>
    /// Siblings are found by the key the builder groups them on — the test's fingerprint — so a
    /// fixture cannot pin a same-subject line the selector would not have produced.
    /// </remarks>
    public static ReportEnvelope Detail(ReportEnvelope full, int row)
    {
        ArgumentNullException.ThrowIfNull(full);

        FindingDto finding = full.Findings[row - 1];
        string? test = finding.Subject.Members == null ? finding.Subject.Fingerprint : null;

        List<SameSubjectDto> siblings =
        [
            .. full.Findings
                .Select((other, index) => (Other: other, Row: index + 1))
                .Where(pair => test != null && pair.Row != row
                    && pair.Other.Subject.Members == null
                    && string.Equals(pair.Other.Subject.Fingerprint, test, StringComparison.Ordinal))
                .Select(pair => new SameSubjectDto(pair.Other.Id, pair.Other.Kind, pair.Row))
        ];

        return full with
        {
            Selection = new SelectionDto(finding.Id, true, finding.Kind, row, siblings),
            Findings = [finding],
            Truncated = full.Truncated with { Shown = 1 }
        };
    }

    private static ReportEnvelope Build(string name) => name switch
    {
        "empty" => Envelope(),
        "nothing-reportable" => NothingReportable(),
        "spec-section-3" => SpecSection3(),
        "every-kind" => EveryKind(),
        "cluster" => Envelope(Cluster("UnprovisionedDatabase..ctor", 5,
            "UnprovisionedDatabase..ctor failed, blocking 5 tests in 20 of 20 runs")),
        "siblings" => Siblings(),
        "truncated" => Truncated(),
        "caveats" => Caveats(),
        "long-names" => LongNames(),
        "docs-mstest" => DocsMsTest(),
        "docs-xunit" => DocsXUnit(),
        "docs-checkout" => DocsCheckout(),
        "docs-cluster" => Envelope(
            Cluster(
                "UnprovisionedDatabase..ctor",
                "UnprovisionedDatabase..ctor failed, blocking 3 tests in 20 of 20 runs",
                [
                    "Checkout.Tests.FixtureTests.FirstTestNeedingTheDatabase",
                    "Checkout.Tests.FixtureTests.SecondTestNeedingTheDatabase",
                    "Checkout.Tests.FixtureTests.ThirdTestNeedingTheDatabase"
                ]) with { Id = "f_7c905f05", EvidenceLevel = "high" }),
        "docs-location" => DocsLocation(),

        // One finding, whose trailer has room for its path whole: the section this feeds is about
        // what the last segment says, and an elided path there would illustrate the elision rather
        // than the thing being explained.
        "docs-source-location" => Envelope(
            Finding(nameof(FindingKind.DurationRegression), "medium",
                "Checkout.Tests.SummaryTests.GenerateMonthlySummary",
                "3.51x slower (95% CI 1.94-5.87x), 340ms -> 1.2s on the clock",
                "tests/Billing/SummaryTests.cs", 88) with
            { Id = "f_2a91c0de", EvidenceLevel = "high" }),
        "docs-time-sensitive" => Envelope(
            Finding(nameof(FindingKind.TimeSensitive), "medium",
                "MyApp.Tests.SchedulerTests.ScheduleEvent_CreatesEventForToday",
                "failed 100% in 00:00-06:00 local against 0% in the rest of the day, " +
                "gap 100 pts across 4 days",
                "tests/MyApp.Tests/SchedulerTests.cs", 18) with { Id = "f_3b0e91c4" }),
        "docs-retry-deepening" => Envelope(
            Finding(nameof(FindingKind.RetryDeepening), "medium",
                "MyApp.Tests.CheckoutTests.Checkout_CompletesWithinTheServiceBudget",
                "attempts to pass 1 -> 3 (+2) over 3 runs against 14 before, 2.4s spent retrying",
                "tests/MyApp.Tests/CheckoutTests.cs", 27) with { Id = "f_5da7c018" }),
        "docs-retry-exhausted" => Envelope(
            Finding(nameof(FindingKind.RetryExhausted), "high",
                "MyApp.Tests.CheckoutTests.Checkout_CompletesWithinTheServiceBudget",
                "gave up after 3 attempts in 7 of 8 retried runs (87.5%), 41s spent retrying",
                "tests/MyApp.Tests/CheckoutTests.cs", 27) with
            { Id = "f_9c14ab63", EvidenceLevel = "high" }),

        // The latest-run spec's §3.1: a regression, a new test, and the failures the findings
        // already explain, above the format spec's §3 block.
        "latest-run" => SpecSection3() with
        {
            LatestRun = SpecLatestRun(
                [Regression(), FreshFailure()], ["f_c7b87f12", "f_3ea537e4", "f_7c905f05"], explained: 5)
        },

        // Its §3.3: every failure in the run is one a finding accounts for.
        "latest-run-known-only" => SpecSection3() with
        {
            LatestRun = SpecLatestRun([], ["f_c7b87f12", "f_3ea537e4", "f_7c905f05"], explained: 5)
        },

        // A window of one session: the section is absent, and this golden is byte-identical to
        // spec-section-3's. Pinned so that absence stays a rule and not an accident.
        "latest-run-suppressed" => SpecSection3() with
        {
            LatestRun = SpecLatestRun([Regression()]) with { Suppressed = true }
        },
        "latest-run-environmental" => SpecSection3() with
        {
            LatestRun = SpecLatestRun([]) with
            {
                IsLikelyEnvironmental = true,
                TestsExecuted = 210,
                TestsFailed = 187
            }
        },
        "latest-run-overflow" => LatestRunOverflow(),
        "latest-run-store" => LatestRunFromStore(),

        // A regression on a window too short for any finding: the section is the whole report, and
        // the clean bill that would otherwise close it is withheld.
        "latest-run-only" => Envelope() with { LatestRun = SpecLatestRun([Regression()]) },
        "detail-test" => Detail(SpecSection3Measured(), 5),
        "detail-group" => Detail(SpecSection3Measured(), 3),
        "detail-store" => DetailFromStore(),
        _ when DetailKind(name) is { } kind => Detail(Envelope(Measured(kind)), 1),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "no such fixture")
    };

    // ---------------------------------------------------------------------
    // The catalogue
    // ---------------------------------------------------------------------

    /// <summary>
    /// A report that found nothing but withheld candidates, so the empty line states why.
    /// </summary>
    private static ReportEnvelope NothingReportable() =>
        Envelope(
            [],
            shown: 0,
            total: 0,
            lowEvidence: 4,
            notSignificant: 2,
            notMeasured: new Dictionary<string, NotMeasuredDto>(StringComparer.Ordinal)
            {
                [nameof(FindingKind.DurationRegression)] = new(3, 7),
                [nameof(FindingKind.TimeSensitive)] = new(0, 2)
            });

    /// <summary>
    /// The report §3 of the format spec illustrates, at the values it names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Built to §3's own figures — the assembly, the window, the revision, the suite counts, the
    /// finding ids and the source locations — so that the golden this renders into can be read
    /// against the spec line for line. The spec is checked against the golden and not the reverse:
    /// D10.2 makes hand-editing a golden to match a block of prose the defect it forbids.
    /// </para>
    /// <para>
    /// The population tokens are not spelled here. §3 states that finding 3 carries
    /// <c>all runs</c> while the rest carry <c>-env-cluster</c> or <c>-env</c>; those are
    /// <see cref="PopulationRules.For(FindingKind)"/> output, and letting the table produce them is
    /// what makes the golden able to contradict the spec rather than agree with it by construction.
    /// </para>
    /// </remarks>
    private static ReportEnvelope SpecSection3()
    {
        const string flakyTest =
            "SampleApp.XUnit.SampleTests.FlakyTest_EnvironmentState_FailsBasedOnSystemState";

        FindingDto[] findings =
        [
            Finding(
                nameof(FindingKind.AlwaysFailing),
                "high",
                "SampleApp.XUnit.SampleTests.ExceptionTest_ThrowsOnMissingDependency",
                "failed 20 of 20 executions (100%), one failure mode: " +
                "System.InvalidOperationException",
                "src/SampleApp.XUnit/SampleTests.cs",
                66) with
            {
                Id = "f_c7b87f12",
                EvidenceLevel = "high"
            },

            Finding(
                nameof(FindingKind.TimingOut),
                "high",
                "SampleApp.XUnit.SampleTests.TimeoutTest_UnresponsiveDependency_AwaitsForever",
                "timed out 20 of 20 executions (100%) in 20 of 20 runs, killed at its 500ms limit",
                "src/SampleApp.XUnit/SampleTests.cs",
                43) with
            {
                Id = "f_3ea537e4",
                EvidenceLevel = "high"
            },

            Cluster(
                "UnprovisionedDatabase..ctor",
                "UnprovisionedDatabase..ctor failed, blocking 3 tests in 20 of 20 runs",
                [
                    "SampleApp.XUnit.FixtureTests.FirstTestNeedingTheDatabase",
                    "SampleApp.XUnit.FixtureTests.SecondTestNeedingTheDatabase",
                    "SampleApp.XUnit.FixtureTests.ThirdTestNeedingTheDatabase"
                ]) with
            {
                Id = "f_7c905f05",
                EvidenceLevel = "high"
            },

            Finding(
                nameof(FindingKind.DurationUnstable),
                "high",
                flakyTest,
                "p50 73ms, ranging 18ms to 129ms, dispersion 0.73 over 20 executions",
                "src/SampleApp.XUnit/SampleTests.cs",
                83) with
            {
                Id = "f_66c10ec3",
                EvidenceLevel = "high"
            },

            Finding(
                nameof(FindingKind.Flaky),
                "high",
                flakyTest,
                "failed 6 of 20 executions (30%) in 6 of 20 runs, 1 failure mode",
                "src/SampleApp.XUnit/SampleTests.cs",
                83) with
            {
                Id = "f_445c562e",
                EvidenceLevel = "high",

                // Resolved by FindingOrder in the builder; spelled here because this fixture stands
                // in for its output rather than running it.
                Annotation = "same test as #4"
            }
        ];

        ReportEnvelope envelope = Envelope(findings);

        return envelope with
        {
            Window = envelope.Window with
            {
                From = new DateTime(2026, 9, 5, 9, 0, 0, DateTimeKind.Utc),
                To = new DateTime(2026, 9, 5, 16, 21, 0, DateTimeKind.Utc)
            },
            Context = new ContextDto("eab9867a1c40", "main", "SampleApp.XUnit"),
            Summary = envelope.Summary with { Tests = 16, Flagged = 6, Healthy = 10 }
        };
    }

    /// <summary>
    /// §3's report with rows 3 and 5 measured: their headlines and metrics resolved from evidence.
    /// </summary>
    /// <remarks>
    /// The full report prints a row's headline and nothing else, so <see cref="SpecSection3"/> can
    /// spell its metrics loosely. A detail view prints them, and the finding-detail spec's §3.1 and
    /// §3.2 read them against the headline above, so here both come from one payload.
    /// </remarks>
    private static ReportEnvelope SpecSection3Measured()
    {
        ReportEnvelope envelope = SpecSection3();

        SignatureView signature = new(
            "abc123", "System.InvalidOperationException", "Expected <n> but was <n>", [],
            Degraded: false, Unavailable: false, Occurrences: 6,
            FirstSeenAt: new DateTime(2026, 9, 5, 9, 0, 0, DateTimeKind.Utc), FirstSeenSha: "eab9867",
            FirstSeenSessionsAgo: 19, FirstSeenInLatestSession: false, FirstSeenAfterWindowStart: false);

        FindingDto cluster = envelope.Findings[2];
        (string clusterHeadline, IReadOnlyList<MetricDto> clusterMetrics) = EvidenceHeadline.For(
            FindingKind.BrokenFixture,
            new BrokenFixtureEvidence(
                nameof(Sdk.Core.Models.Executions.FailureSite.FixtureSetup), "UnprovisionedDatabase..ctor", signature with { Occurrences = 60 },
                3, [], 60, 20, 20, 3, new DateTime(2026, 9, 5, 16, 21, 0, DateTimeKind.Utc), "eab9867", []));

        FindingDto flaky = envelope.Findings[4];
        (string flakyHeadline, IReadOnlyList<MetricDto> flakyMetrics) = EvidenceHeadline.For(
            FindingKind.Flaky,
            new FlakyEvidence(6, 20, 20, 6, 0.3, 0, 0, 1, [signature], [], null));

        return envelope with
        {
            Findings =
            [
                envelope.Findings[0],
                envelope.Findings[1],
                cluster with
                {
                    Headline = clusterHeadline,
                    Metrics = clusterMetrics,
                    Subject = cluster.Subject with
                    {
                        Members =
                        [
                            .. cluster.Subject.Members!.Select((member, i) => member with
                            {
                                SourceFile = "tests/SampleApp.XUnit/FixtureTests.cs",
                                SourceLineNumber = 12 + (7 * i)
                            })
                        ]
                    }
                },
                envelope.Findings[3],
                flaky with { Headline = flakyHeadline, Metrics = flakyMetrics, Subject = flaky.Subject with { Assembly = "SampleApp.XUnit" } },
                .. envelope.Findings.Skip(5)
            ]
        };
    }

    /// <summary>The fixture key for one kind's detail view: <c>detail-</c> and the kind in kebab case.</summary>
    private static string DetailKey(FindingKind kind) =>
        "detail-" + string.Concat(kind.ToString().Select((c, i) =>
            char.IsUpper(c) ? (i > 0 ? "-" : string.Empty) + char.ToLowerInvariant(c) : c.ToString()));

    private static FindingKind? DetailKind(string name) =>
        Enum.GetValues<FindingKind>().Where(kind => DetailKey(kind) == name).Select(kind => (FindingKind?)kind).FirstOrDefault();

    /// <summary>
    /// A finding of one kind whose headline and metrics are resolved from that kind's sample payload.
    /// </summary>
    /// <remarks>
    /// So every kind's metrics block is pinned by a golden, and a label or a value format that moves
    /// in <c>EvidenceHeadline</c> surfaces as a diff in the view that prints it.
    /// </remarks>
    private static FindingDto Measured(FindingKind kind)
    {
        FindingEvidence evidence = EvidenceSamples.For(kind);
        (string headline, IReadOnlyList<MetricDto> metrics) = EvidenceHeadline.For(kind, evidence);

        FindingDto finding = evidence is BrokenFixtureEvidence or SharedFailureEvidence
            ? Cluster(
                "CheckoutFixture.Setup",
                headline,
                ["MyApp.Tests.CheckoutTests.Completes", "MyApp.Tests.CheckoutTests.Refunds", "MyApp.Tests.CartTests.Totals"])
            : Finding(kind.ToString(), "medium", "MyApp.Tests.CheckoutTests.Completes", headline,
                "tests/MyApp.Tests/CheckoutTests.cs", 42);

        return finding with
        {
            Id = FindingId.Compute(kind, finding.Subject.GroupId ?? finding.Subject.Fingerprint!),
            Kind = kind.ToString(),
            Severity = "medium",
            Population = ToCamelCase(PopulationRules.For(kind).ToString()),
            Headline = headline,
            Metrics = metrics
        };
    }

    /// <summary>
    /// A detail view the whole pipeline produced, selected by an id it computed.
    /// </summary>
    /// <remarks>
    /// The store behind <see cref="LatestRunFromStore"/>, run through the coordinator and the
    /// selector as <c>xping report --id</c> runs them, selecting the first finding. Pins everything
    /// downstream of the sessions, where the other detail fixtures pin the renderer.
    /// </remarks>
    private static ReportEnvelope DetailFromStore()
    {
        (AnalysisContext context, AnalysisResult analysis) = AnalyzeStore();
        LatestRunAnalysis? latestRun = LatestRunAnalyzer.Analyze(context, analysis.Findings);

        IReadOnlyList<Finding> ordered = FindingOrder.WithSiblingsAdjacent(analysis.Findings);

        return EnvelopeBuilder.Build(
            context,
            analysis,
            incompleteSessions: 0,
            unreadableSessions: 0,
            skewedSessions: 0,
            LocalAnalysisConstants.DefaultTopFindings,
            latestRun,
            FindingSelector.Select(ordered, ordered[0].Id));
    }

    /// <summary>
    /// The row the latest-run spec's §3.1 opens with: a test that had always passed.
    /// </summary>
    private static LatestRunFailureDto Regression() =>
        LatestRunFailure(
            "new",
            "Checkout_AppliesDiscount",
            "passed the previous 19 runs, failed just now",
            priorSessions: 19) with
        {
            Subject = SampleSubject("SampleApp.XUnit.CartTests.Checkout_AppliesDiscount", "CartTests.cs", 112)
        };

    /// <summary>
    /// The row the latest-run spec's §3.1 closes with: a test the window had never seen.
    /// </summary>
    private static LatestRunFailureDto FreshFailure() =>
        LatestRunFailure(
            "newTest",
            "Checkout_RejectsExpiredCoupon",
            "first seen this run, failed",
            failureSummary: "EqualException") with
        {
            Subject = SampleSubject("SampleApp.XUnit.CartTests.Checkout_RejectsExpiredCoupon", "CartTests.cs", 140)
        };

    /// <summary>
    /// The section as the whole pipeline produces it from sessions, not as a hand-built envelope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other latest-run fixtures spell the envelope and pin the renderer. This one spells the
    /// sessions and pins everything downstream of them — the index, the analyzer, the coordinator,
    /// the builder and the renderer — so that a contrast sentence or a status the builder phrases
    /// differently from the envelope fixtures surfaces here as a golden diff.
    /// </para>
    /// <para>
    /// Twenty runs of a small suite. One test passed nineteen times and fails in the newest run;
    /// one appears in the newest run alone and fails; one has failed in every run and is a finding,
    /// which the section defers to. The rest are stable, and there are enough of them that the
    /// newest run stays well under the environmental thresholds.
    /// </para>
    /// </remarks>
    private static ReportEnvelope LatestRunFromStore()
    {
        (AnalysisContext context, AnalysisResult analysis) = AnalyzeStore();
        LatestRunAnalysis? latestRun = LatestRunAnalyzer.Analyze(context, analysis.Findings);

        return EnvelopeBuilder.Build(
            context,
            analysis,
            incompleteSessions: 0,
            unreadableSessions: 0,
            skewedSessions: 0,
            LocalAnalysisConstants.DefaultTopFindings,
            latestRun);
    }

    /// <summary>
    /// Writes the twenty runs <see cref="LatestRunFromStore"/> describes and runs every provider.
    /// </summary>
    private static (AnalysisContext Context, AnalysisResult Analysis) AnalyzeStore()
    {
        const int runs = 20;
        var sessions = new List<Sdk.Core.Models.TestSession>();

        for (int ordinal = 0; ordinal < runs; ordinal++)
        {
            bool newest = ordinal == runs - 1;
            List<Sdk.Core.Models.Executions.TestExecution> executions =
            [
                TestSessionFactory.Execution(
                    "Checkout_AppliesDiscount",
                    newest ? Sdk.Core.Models.Executions.TestOutcome.Failed : Sdk.Core.Models.Executions.TestOutcome.Passed,
                    exceptionType: newest ? "System.NullReferenceException" : null,
                    errorMessage: newest ? "Object reference not set to an instance of an object." : null),
                TestSessionFactory.Execution(
                    "Checkout_RequiresProvisionedDatabase",
                    Sdk.Core.Models.Executions.TestOutcome.Failed,
                    exceptionType: "System.InvalidOperationException",
                    errorMessage: "The database has not been provisioned."),
                .. Enumerable.Range(0, 12).Select(i =>
                    TestSessionFactory.Execution($"Stable{i.ToString("00", CultureInfo.InvariantCulture)}"))
            ];

            if (newest)
            {
                executions.Add(TestSessionFactory.Execution(
                    "Checkout_RejectsExpiredCoupon",
                    Sdk.Core.Models.Executions.TestOutcome.Failed,
                    exceptionType: "Xunit.Sdk.EqualException",
                    errorMessage: "Assert.Equal() Failure: Values differ"));
            }

            sessions.Add(TestSessionFactory.Session(ordinal, executions, sha: "eab9867a1c40", branch: "main"));
        }

        AnalysisContext context = TestSessionFactory.Context([.. sessions]);

        var coordinator = new FindingCoordinator(
        [
            new FailureModeProvider(),
            new RetryProvider(),
            new DurationProvider(),
            new VanishedProvider(),
            new ParallelSensitiveProvider(),
            new TimeSensitiveProvider()
        ]);

        return (context, coordinator.Run(context, kinds: null, TextWriter.Null));
    }

    /// <summary>
    /// A latest-run section dated and signed like <see cref="SpecSection3"/>'s newest session.
    /// </summary>
    private static LatestRunDto SpecLatestRun(
        LatestRunFailureDto[] failures, string[]? explainedBy = null, int? explained = null) =>
        LatestRun(failures, explainedBy, explained, sha: "eab9867a1c40") with
        {
            StartedAt = new DateTime(2026, 9, 5, 16, 21, 0, DateTimeKind.Utc),
            TestsExecuted = 16
        };

    /// <summary>
    /// A single-test subject named the way the builder names one, at a given location.
    /// </summary>
    private static SubjectDto SampleSubject(string qualifiedName, string sourceFile, int line) =>
        new(
            "test", $"fp-{qualifiedName}", qualifiedName, qualifiedName,
            SubjectNames.ShortName(qualifiedName, qualifiedName), null,
            sourceFile, line, "SampleApp.XUnit", null, null, null);

    /// <summary>
    /// More new failures than the section shows: ten rows, a cap line, and the findings' share.
    /// </summary>
    private static ReportEnvelope LatestRunOverflow()
    {
        ReportEnvelope envelope = SpecSection3();

        // Descending prior-run counts, as the analyzer orders new rows, so the golden reads as
        // one the analyzer could have produced.
        LatestRunFailureDto[] rows =
        [
            .. Enumerable.Range(0, LocalAnalysisConstants.LatestRunMaxRows).Select(index =>
            {
                int prior = 19 - index;
                string name = $"SampleApp.XUnit.CartTests.Case{index.ToString("00", CultureInfo.InvariantCulture)}";

                return LatestRunFailure(
                    "new",
                    name,
                    $"passed the previous {prior.ToString(CultureInfo.InvariantCulture)} runs, failed just now",
                    priorSessions: prior) with
                {
                    Subject = SampleSubject(name, "src/SampleApp.XUnit/CartTests.cs", 40 + index)
                };
            })
        ];

        return envelope with
        {
            LatestRun = SpecLatestRun(rows, ["f_c7b87f12", "f_3ea537e4", "f_7c905f05"], explained: 5) with
            {
                TestsFailed = 28,
                NewFailures = 23,
                FailuresTotal = 23,
                OverflowCommand = "xping report --all"
            }
        };
    }

    /// <summary>
    /// One finding of every kind a provider can emit, so no kind's label is golden-free.
    /// </summary>
    private static ReportEnvelope EveryKind() =>
        Envelope(
        [
            .. Enum.GetValues<FindingKind>().Select((kind, index) => Finding(
                kind.ToString(),
                (index % 3) switch { 0 => "high", 1 => "medium", _ => "low" },
                $"MyApp.Tests.SampleTests.Case{index.ToString(CultureInfo.InvariantCulture)}",
                $"{ReportVocabulary.LabelFor(kind.ToString())} observed over 20 of 20 runs",
                "tests/MyApp.Tests/SampleTests.cs",
                20 + index))
        ]);

    /// <summary>
    /// One test carrying three findings, pulled adjacent and annotated.
    /// </summary>
    private static ReportEnvelope Siblings()
    {
        const string test = "MyApp.Tests.CartTests.PlacesAnOrderAndSettlesIt";

        return Envelope(
            Finding(nameof(FindingKind.AlwaysFailing), "high", test,
                "failed 20 of 20 executions (100%), one failure mode: System.TimeoutException"),
            Finding(nameof(FindingKind.DurationUnstable), "high", test,
                "p50 73ms, ranging 18ms to 129ms, dispersion 0.73 over 20 executions") with
            { Annotation = "same test as #1" },
            Finding(nameof(FindingKind.Flaky), "medium", test,
                "failed 6 of 20 executions (30%) in 6 of 20 runs, 1 failure mode") with
            { Annotation = "same test as #1" },
            Finding(nameof(FindingKind.Vanished), "low", "MyApp.Tests.CartTests.Settles",
                "ran in 12 of 17 earlier runs and in none of the last 3"));
    }

    /// <summary>
    /// A truncated report: the heading bands count what was produced, the footer says what is shown.
    /// </summary>
    private static ReportEnvelope Truncated()
    {
        ReportEnvelope envelope = Envelope(
            [
                Finding(nameof(FindingKind.Flaky), "high", "MyApp.Tests.CartTests.PlacesAnOrder",
                    "failed 7 of 20 executions (35%) in 5 of 20 runs, 3 failure modes"),
                Finding(nameof(FindingKind.RetryMasked), "medium", "MyApp.Tests.CartTests.Settles",
                    "passed on retry in 4 of 20 runs, never failing one")
            ],
            shown: 2,
            total: 9);

        return envelope with
        {
            Summary = envelope.Summary with { Counts = new SeverityCountsDto(5, 3, 1) }
        };
    }

    /// <summary>
    /// Every line the header can grow: withheld counts, unmeasured kinds, and the caveat line.
    /// </summary>
    private static ReportEnvelope Caveats()
    {
        ReportEnvelope envelope = Envelope(
            [Finding(nameof(FindingKind.Flaky), "high", "MyApp.Tests.CartTests.PlacesAnOrder",
                "failed 7 of 20 executions (35%) in 5 of 20 runs, 3 failure modes")],
            shown: 1,
            total: 1,
            lowEvidence: 6,
            notSignificant: 11,
            notMeasured: new Dictionary<string, NotMeasuredDto>(StringComparer.Ordinal)
            {
                [nameof(FindingKind.DurationRegression)] = new(5, 12),
                [nameof(FindingKind.DurationUnstable)] = new(1, 9),
                [nameof(FindingKind.ParallelSensitive)] = new(0, 4),
                [nameof(FindingKind.TimeSensitive)] = new(0, 2)
            });

        return envelope with
        {
            Summary = envelope.Summary with
            {
                EnvironmentalSessions = 2,
                PartialSessions = 3,
                IncompleteSessions = 1,
                UnreadableSessions = 4,
                SkewedSessions = 1,
                FailedProviders = ["DurationRegression"]
            }
        };
    }

    /// <summary>
    /// The widths nothing else reaches: an over-long name, an over-long path, and one unbreakable
    /// token wider than the fence itself.
    /// </summary>
    private static ReportEnvelope LongNames() =>
        Envelope(
            Finding(
                nameof(FindingKind.Flaky),
                "high",
                "MyApp.Tests.Checkout.Integration.VeryLongNamespace.PlacesAnOrderAndSettlesItEvenSo",
                "failed 7 of 20 executions (35%) in 5 of 20 runs, 3 failure modes, and a great " +
                "deal more text besides so that wrapping has to happen at least twice over",
                "tests/" + string.Join("/", Enumerable.Repeat("VeryLongDirectoryName", 12)) +
                    "/CartTests.cs",
                1042),
            Finding(
                nameof(FindingKind.AlwaysFailing),
                "medium",
                "MyApp.Tests.SampleTests." + new string('W', FenceOverflow),
                "failed 20 of 20 executions (100%), one failure mode: " +
                new string('E', FenceOverflow)),
            Finding(nameof(FindingKind.DurationUnstable), "low", "Short", "p50 820ms, dispersion 0.71"));

    /// <summary>A token wider than the fence, so the unbreakable-token path is reached.</summary>
    private const int FenceOverflow = 80;

    // ---------------------------------------------------------------------
    // The samples the documentation prints
    // ---------------------------------------------------------------------

    /// <summary>
    /// What the MSTest sample suite reports, as the repository README and the docs landing page
    /// show it.
    /// </summary>
    /// <remarks>
    /// One test carries two findings, so the sample demonstrates sibling adjacency rather than
    /// merely describing it. The findings are listed in the order the builder would have produced:
    /// ranked, then walked top to bottom pulling each repeat up beside the first naming its test.
    /// The annotation is spelled here for the same reason — this fixture stands in for
    /// the ranking layer's output rather than running it, and the rule itself is pinned by
    /// the sibling tests.
    /// </remarks>
    private static ReportEnvelope DocsMsTest()
    {
        const string passesOnRetry = "SampleApp.MSTest.SampleTests.FlakyTest_PassesOnRetry";

        ReportEnvelope envelope = Envelope(
            Finding(nameof(FindingKind.Flaky), "high", passesOnRetry,
                "failed 9 of 18 executions (50%) in 9 of 9 runs, 1 failure mode",
                "samples/SampleApp.MSTest/SampleTests.cs", 96) with { Id = "f_2b84a621" },

            Finding(nameof(FindingKind.RetryMasked), "high", passesOnRetry,
                "passed on retry 9 times in 9 of 9 runs, up to attempt 2",
                "samples/SampleApp.MSTest/SampleTests.cs", 96) with
            { Id = "f_e98db1e6", Annotation = "same test as #1" },

            Finding(nameof(FindingKind.AlwaysFailing), "high",
                "SampleApp.MSTest.SampleTests.ThrowingTestIsTracked",
                "failed 9 of 9 executions (100%), one failure mode: System.InvalidOperationException",
                "samples/SampleApp.MSTest/SampleTests.cs", 65) with
            { Id = "f_c1774d82", EvidenceLevel = "low" },

            Finding(nameof(FindingKind.Flaky), "high",
                "SampleApp.MSTest.SampleTests.FlakyTest_RaceCondition_FailsIntermittently",
                "failed 4 of 9 executions (44.4%) in 4 of 9 runs, 1 failure mode",
                "samples/SampleApp.MSTest/SampleTests.cs", 118) with
            { Id = "f_d24c5aa9", EvidenceLevel = "low" });

        return envelope with
        {
            Window = envelope.Window with
            {
                From = new DateTime(2026, 8, 20, 7, 52, 0, DateTimeKind.Utc),
                To = new DateTime(2026, 8, 20, 9, 2, 0, DateTimeKind.Utc),
                SessionCount = 9
            },
            Context = new ContextDto("bdbafba41e07", "main", "SampleApp.MSTest"),

            // Seventeen tests, three of them flagged: the first two findings name one test between
            // them, which is the arithmetic the second header line exists to make legible.
            Summary = envelope.Summary with { Tests = 17, Flagged = 3, Healthy = 14 }
        };
    }

    /// <summary>
    /// What a fresh xUnit project reports, as the quickstart and the CLI package readme show it.
    /// </summary>
    private static ReportEnvelope DocsXUnit()
    {
        ReportEnvelope envelope = Envelope(
            Finding(nameof(FindingKind.Flaky), "high",
                "MyTestProject.SampleTests.FlakyTest_PassesOnRetry",
                "failed 9 of 18 executions (50%) in 9 of 9 runs, 1 failure mode",
                "tests/MyTestProject/SampleTests.cs", 96) with { Id = "f_2b84a621" },

            Finding(nameof(FindingKind.AlwaysFailing), "high",
                "MyTestProject.SampleTests.ThrowingTestIsTracked",
                "failed 9 of 9 executions (100%), one failure mode: System.InvalidOperationException",
                "tests/MyTestProject/SampleTests.cs", 65) with
            { Id = "f_c1774d82", EvidenceLevel = "low" });

        return envelope with
        {
            Window = envelope.Window with
            {
                From = new DateTime(2026, 8, 20, 7, 52, 0, DateTimeKind.Utc),
                To = new DateTime(2026, 8, 20, 9, 2, 0, DateTimeKind.Utc),
                SessionCount = 9
            },
            Context = new ContextDto("bdbafba41e07", "main", "MyTestProject"),
            Summary = envelope.Summary with { Tests = 17, Flagged = 2, Healthy = 15 }
        };
    }

    /// <summary>
    /// The report the CLI command reference documents, which is the output contract.
    /// </summary>
    /// <remarks>
    /// Three kinds at three severities and three different population markers, because this is the
    /// sample the reference's prose about severity, evidence and populations all point at.
    /// </remarks>
    private static ReportEnvelope DocsCheckout()
    {
        ReportEnvelope envelope = Envelope(
            [.. DocsCheckoutFindings()],
            shown: 3,
            total: 3,
            lowEvidence: 41,
            notSignificant: 6,
            notMeasured: new Dictionary<string, NotMeasuredDto>(StringComparer.Ordinal)
            {
                [nameof(FindingKind.DurationRegression)] = new(63, 32),
                [nameof(FindingKind.ParallelSensitive)] = new(0, 108)
            });

        return envelope with
        {
            Context = new ContextDto("a3f9c2ed0011", "main", "Checkout.Tests"),
            Summary = envelope.Summary with { Tests = 412, Flagged = 3, Healthy = 409 }
        };
    }

    /// <summary>The three findings the reference's samples both describe.</summary>
    private static FindingDto[] DocsCheckoutFindings() =>
        [
            Finding(nameof(FindingKind.Flaky), "high",
                "Checkout.Tests.SummaryTests.GenerateMonthlySummary",
                "failed 7 of 20 executions (35%) in 5 of 20 runs, 3 failure modes",
                "tests/Billing/SummaryTests.cs", 88) with { Id = "f_2a91c0de" },

            Finding(nameof(FindingKind.DurationRegression), "medium",
                "Checkout.Tests.FlowTests.CheckoutFlow_Completes",
                "3.51x slower (95% CI 1.94-5.87x), 340ms -> 1.2s on the clock",
                "tests/Checkout/FlowTests.cs", 214) with
            { Id = "f_8c04b71a", EvidenceLevel = "high" },

            Finding(nameof(FindingKind.Vanished), "low",
                "Checkout.Tests.LegacyImport.Roundtrip",
                "ran in 12 of 17 earlier runs, absent from the last 3",
                "tests/Legacy/ImportTests.cs", 41) with { Id = "f_1d77e3f5" }
        ];

    /// <summary>
    /// One finding, for the limitation about where a source line comes from.
    /// </summary>
    /// <remarks>
    /// That page shows a row rather than a report: the point it makes is about the trailer's last
    /// segment, and a header and a legend around it would be three lines of context for a sentence
    /// about one.
    /// </remarks>
    private static ReportEnvelope DocsLocation() =>
        Envelope(
            Finding(nameof(FindingKind.Flaky), "high",
                "SampleApp.MSTest.SampleTests.FlakyTest_PassesOnRetry",
                "failed 5 of 10 executions (50%) in 5 of 5 runs, 1 failure mode",
                "samples/SampleApp.MSTest/SampleTests.cs", 135) with
            { Id = "f_8f042eab", EvidenceLevel = "low" });

    // ---------------------------------------------------------------------
    // Builders
    // ---------------------------------------------------------------------

    /// <summary>
    /// Builds a finding about one test, with a source location.
    /// </summary>
    /// <param name="kind">The kind, spelled as the enum does.</param>
    /// <param name="severity">The severity, spelled as the envelope does.</param>
    /// <param name="name">The test's fully qualified name.</param>
    /// <param name="headline">The already-resolved sentence.</param>
    /// <param name="sourceFile">Where the test is declared.</param>
    /// <param name="sourceLineNumber">The line it begins on.</param>
    /// <returns>The finding.</returns>
    public static FindingDto Finding(
        string kind,
        string severity,
        string name,
        string headline,
        string? sourceFile,
        int? sourceLineNumber)
    {
        FindingDto finding = Finding(kind, severity, name, headline);

        return finding with
        {
            Subject = finding.Subject with
            {
                SourceFile = sourceFile,
                SourceLineNumber = sourceLineNumber
            }
        };
    }

    /// <summary>
    /// Spells an enum name the way the envelope does, so the fixtures cannot drift from the builder.
    /// </summary>
    private static string ToCamelCase(string value) =>
        char.ToLowerInvariant(value[0]) + value.Substring(1);

    /// <summary>
    /// Builds a finding about one test.
    /// </summary>
    /// <param name="kind">The kind, spelled as the enum does.</param>
    /// <param name="severity">The severity, spelled as the envelope does.</param>
    /// <param name="name">The test's fully qualified name.</param>
    /// <param name="headline">The already-resolved sentence.</param>
    /// <returns>The finding.</returns>
    public static FindingDto Finding(string kind, string severity, string name, string headline) =>
        new(
            "f_2a91",
            kind,
            severity,
            "moderate",
            10,
            ToCamelCase(PopulationRules.For(Enum.Parse<FindingKind>(kind)).ToString()),
            new SubjectDto(
                "test", $"fp-{name}", name, name, SubjectNames.ShortName(name, name), null,
                null, null, "MyApp.Tests", null, null, null),
            null,
            headline,
            [new MetricDto("failed", "7 of 20 executions (35%)")],
            null,
            "xping report --id f_2a91 --assembly MyApp.Tests");

    /// <summary>
    /// A finding about a cluster: no name of its own, a cause, and members beneath it.
    /// </summary>
    /// <param name="cause">What the members have in common.</param>
    /// <param name="members">How many tests the cluster took down.</param>
    /// <param name="headline">The already-resolved sentence.</param>
    /// <returns>The finding.</returns>
    public static FindingDto Cluster(string cause, int members, string headline) =>
        Cluster(
            cause,
            headline,
            [
                .. Enumerable.Range(0, members).Select(i =>
                    $"MyApp.Tests.FixtureTests.Member{i.ToString(CultureInfo.InvariantCulture)}")
            ]);

    /// <summary>
    /// A finding about a cluster whose members are named.
    /// </summary>
    /// <param name="cause">What the members have in common.</param>
    /// <param name="headline">The already-resolved sentence.</param>
    /// <param name="members">The fully qualified names of the tests it took down.</param>
    /// <returns>The finding.</returns>
    /// <remarks>
    /// Member names are resolved through <see cref="SubjectNames.ShortName"/> like any other, so a
    /// fixture cannot pin a member spelling the builder would not have produced.
    /// </remarks>
    public static FindingDto Cluster(string cause, string headline, IReadOnlyList<string> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        return Finding("BrokenFixture", "high", "MyApp.Tests.SampleTests.First", headline) with
        {
            Subject = new SubjectDto(
                "group", null, null, null, null, cause, null, null, null,
                "sig_c7b87f12",
                members.Count,
                [
                    .. members.Select((name, i) => new SubjectDto(
                        "test",
                        $"fp{i.ToString(CultureInfo.InvariantCulture)}",
                        name,
                        name,
                        SubjectNames.ShortName(name, name),
                        null, null, null, "MyApp.Tests", null, null, null))
                ])
        };
    }

    /// <summary>
    /// Builds a latest-run section around the given rows, none of them capped.
    /// </summary>
    /// <param name="failures">The rows, in the order the analyzer would have put them.</param>
    /// <param name="explainedBy">Ids of the findings that account for other failures.</param>
    /// <param name="explained">How many failing tests those findings account for.</param>
    /// <param name="sha">Commit the run was at, or null for none recorded.</param>
    /// <returns>The section.</returns>
    public static LatestRunDto LatestRun(
        LatestRunFailureDto[] failures,
        string[]? explainedBy = null,
        int? explained = null,
        string? sha = "eab9867f00d") =>
        new(
            "6f9a2f1c-0000-4000-8000-000000000014",
            new DateTime(2026, 8, 19, 16, 21, 0, DateTimeKind.Utc),
            sha,
            IsLikelyEnvironmental: false,
            Suppressed: false,
            TestsExecuted: 16,
            TestsFailed: failures.Length + (explained ?? explainedBy?.Length ?? 0),
            NewFailures: failures.Count(f => f.Status != "seenBefore"),
            ExplainedByFindings: explained ?? explainedBy?.Length ?? 0,
            ExplainedByFindingIds: explainedBy ?? [],
            Failures: failures,
            FailuresShown: failures.Length,
            FailuresTotal: failures.Length,
            OverflowCommand: null);

    /// <summary>
    /// Builds one latest-run row, named the way the builder names a single test.
    /// </summary>
    /// <param name="status">The status, as the envelope spells it.</param>
    /// <param name="name">The test's method name; the class is <c>SampleTests</c>.</param>
    /// <param name="contrast">The already-resolved contrast sentence.</param>
    /// <param name="failureSummary">The exception type, namespace stripped, or null.</param>
    /// <param name="priorSessions">Sessions before this one the test recorded a verdict in.</param>
    /// <param name="priorFailures">Of those, how many it failed in.</param>
    /// <param name="sourceFile">Where the test lives, or null for none recorded.</param>
    /// <returns>The row.</returns>
    public static LatestRunFailureDto LatestRunFailure(
        string status,
        string name,
        string contrast,
        string? failureSummary = "NullReferenceException",
        int priorSessions = 0,
        int priorFailures = 0,
        string? sourceFile = "SampleTests.cs")
    {
        string qualified = $"MyApp.Tests.SampleTests.{name}";

        return new LatestRunFailureDto(
            status,
            new SubjectDto(
                "test", $"fp-{name}", qualified, name, SubjectNames.ShortName(qualified, name), null,
                sourceFile, sourceFile == null ? null : 42, "MyApp.Tests", null, null, null),
            contrast,
            failureSummary,
            priorSessions,
            priorFailures);
    }

    /// <summary>
    /// Builds an envelope around the given findings, none of them truncated away.
    /// </summary>
    /// <param name="findings">The findings.</param>
    /// <returns>The envelope.</returns>
    public static ReportEnvelope Envelope(params FindingDto[] findings) =>
        Envelope(findings, findings.Length, findings.Length);

    /// <summary>
    /// Builds an envelope that may be truncated.
    /// </summary>
    /// <param name="findings">The findings shown.</param>
    /// <param name="shown">How many are shown.</param>
    /// <param name="total">How many were produced.</param>
    /// <returns>The envelope.</returns>
    public static ReportEnvelope Envelope(FindingDto[] findings, int shown, int total) =>
        Envelope(findings, shown, total, lowEvidence: 0, notSignificant: 0);

    /// <summary>
    /// Builds an envelope that also withheld candidates.
    /// </summary>
    /// <param name="findings">The findings shown.</param>
    /// <param name="shown">How many are shown.</param>
    /// <param name="total">How many were produced.</param>
    /// <param name="lowEvidence">Candidates dropped for resting on too little data.</param>
    /// <param name="notSignificant">Candidates dropped for saying nothing.</param>
    /// <returns>The envelope.</returns>
    public static ReportEnvelope Envelope(
        FindingDto[] findings, int shown, int total, int lowEvidence, int notSignificant) =>
        Envelope(findings, shown, total, lowEvidence, notSignificant, notMeasured: null);

    /// <summary>
    /// Builds an envelope, in full.
    /// </summary>
    /// <param name="findings">The findings shown.</param>
    /// <param name="shown">How many are shown.</param>
    /// <param name="total">How many were produced.</param>
    /// <param name="lowEvidence">Candidates dropped for resting on too little data.</param>
    /// <param name="notSignificant">Candidates dropped for saying nothing.</param>
    /// <param name="notMeasured">Per kind, the tests it could not be measured on.</param>
    /// <returns>The envelope.</returns>
    public static ReportEnvelope Envelope(
        FindingDto[] findings,
        int shown,
        int total,
        int lowEvidence,
        int notSignificant,
        IReadOnlyDictionary<string, NotMeasuredDto>? notMeasured)
    {
        ArgumentNullException.ThrowIfNull(findings);

        int high = findings.Count(f => f.Severity == "high");
        int medium = findings.Count(f => f.Severity == "medium");
        int low = findings.Count(f => f.Severity == "low");
        int produced = Math.Max(total, findings.Length);

        // Tests, not findings, deduplicated the way the builder does it: one test carrying two
        // findings is one flagged test, and a cluster of five is five. A count of findings here
        // would pin a header whose arithmetic the second line exists to make legible.
        int flagged = findings
            .SelectMany(f => f.Subject.Members ?? [f.Subject])
            .Select(subject => subject.Fingerprint)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Count();

        return new ReportEnvelope(
            ReportEnvelope.CurrentSchemaVersion,
            new WindowDto(
                new DateTime(2026, 8, 5, 9, 12, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 19, 16, 40, 0, DateTimeKind.Utc),
                20,
                "default",
                null,
                3,
                []),
            new ContextDto("a3f9c2ed0011", "main", "MyApp.Tests"),
            new SummaryDto(
                412,
                produced,
                new SeverityCountsDto(high, medium, low),
                flagged,
                412 - flagged,
                lowEvidence,
                notSignificant,
                notMeasured ?? new Dictionary<string, NotMeasuredDto>(StringComparer.Ordinal),
                0,
                0,
                0,
                0,
                0,
                []),

            // No latest run yet: the fixtures pin the findings block, and a section that is absent
            // from the envelope is absent from the page. The latest-run fixtures carry their own.
            null,
            null,
            findings,
            new TruncationDto(shown, total, "xping report --all"));
    }
}
