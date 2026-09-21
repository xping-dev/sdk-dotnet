/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Globalization;
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
        "docs-retry-exhausted"
    ];

    /// <summary>The same keys, as a theory source.</summary>
    /// <returns>The keys.</returns>
    public static TheoryData<string> Names() => [.. Keys];

    /// <summary>
    /// Returns the envelope a key names.
    /// </summary>
    /// <param name="name">The key, as <see cref="Names"/> lists it.</param>
    /// <returns>The envelope.</returns>
    public static ReportEnvelope Get(string name) => name switch
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
            "xping report --kind Flaky --format json");

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
                findings.Length,
                412 - findings.Length,
                lowEvidence,
                notSignificant,
                notMeasured ?? new Dictionary<string, NotMeasuredDto>(StringComparer.Ordinal),
                0,
                0,
                0,
                0,
                0,
                []),
            findings,
            new TruncationDto(shown, total, "xping report --all"));
    }
}
