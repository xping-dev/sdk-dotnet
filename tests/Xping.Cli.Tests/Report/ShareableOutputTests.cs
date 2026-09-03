/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Contract;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Cli.Report.Rendering;
using Xping.Cli.Reporting;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// The properties that make a report survive being pasted somewhere else.
/// </summary>
/// <remarks>
/// The report is shared more often than it is merely read, so the fence, the width and the ASCII
/// headline are load-bearing rather than cosmetic: a line over the width wraps in a chat client and
/// loses the alignment the fence exists to preserve, and a non-ASCII character can arrive as a
/// replacement glyph in whatever the reader is using.
/// </remarks>
public sealed class ShareableOutputTests
{
    private const int FenceWidth = 72;

    // Severity marker, two spaces, kind label, two spaces — the column the trailer starts at.
    private const int Indent = 6;
    private const string Fence = "```";

    // ---------------------------------------------------------------------
    // Headlines
    // ---------------------------------------------------------------------

    /// <summary>Every kind a provider can emit today.</summary>
    /// <remarks>
    /// Named rather than passed as evidence: the evidence records are internal, and a public theory
    /// member cannot expose them. The lookup below is the price of keeping the model internal, which
    /// is worth more than the indirection costs.
    /// </remarks>
    public static TheoryData<string> EveryEvidenceShape() =>
    [
        nameof(FindingKind.RetryMasked),
        nameof(FindingKind.RetryDeepening),
        nameof(FindingKind.RetryExhausted),
        nameof(FindingKind.Flaky),
        nameof(FindingKind.AlwaysFailing),
        nameof(FindingKind.TimingOut),
        nameof(FindingKind.BrokenFixture),
        nameof(FindingKind.SharedFailure),
        nameof(FindingKind.DurationRegression),
        nameof(FindingKind.DurationUnstable),
        nameof(FindingKind.ParallelSensitive),
        nameof(FindingKind.TimeSensitive),
        nameof(FindingKind.Vanished)
    ];

    private static FindingEvidence EvidenceFor(FindingKind kind)
    {
        SignatureView signature = new(
            "abc123",
            "System.InvalidOperationException",
            "Expected <n> but was <n>",
            ["MyApp.Tests.CheckoutTests.Completes()"],
            Degraded: false,
            Unavailable: false,
            Occurrences: 12,
            FirstSeenAt: new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
            FirstSeenSha: "a3f9c2e",
            FirstSeenSessionsAgo: 4,
            FirstSeenInLatestSession: false,
            FirstSeenAfterWindowStart: true);

        FailureExemplar exemplar = new(
            "11111111-1111-1111-1111-111111111111",
            new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc),
            "a3f9c2e",
            AttemptNumber: 1,
            DurationMs: 120,
            "System.InvalidOperationException",
            "boom",
            ["MyApp.Tests.CheckoutTests.Completes()"],
            "abc123",
            Site: nameof(FailureSite.TestBody),
            SiteMember: null);

        RetryConfiguration configuration = new("RetryAttribute", 2, "NetworkError", 250);

        RetryAttemptExemplar attemptExemplar = new(
            "11111111-1111-1111-1111-111111111111",
            new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc),
            "a3f9c2e",
            Attempts: 3,
            Outcome: nameof(TestOutcome.Failed),
            RetryWallClockMs: 8_200,
            "boom");

        return kind switch
        {
            FindingKind.RetryMasked =>
                new RetryMaskedEvidence(
                    4, 20, 20, 3, 0.2, 3, configuration, 12_400, 750,
                    new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc), "a3f9c2e", []),

            FindingKind.RetryDeepening =>
                new RetryDeepeningEvidence(
                    new RetryDepthProfile(3, 4, 3, 0, 3),
                    new RetryDepthProfile(1, 2, 14, 0, 14),
                    new RetryDepthDelta(2, 200),
                    configuration,
                    2_400,
                    500,
                    0,
                    "a3f9c2e",
                    [attemptExemplar],
                    attemptExemplar),

            FindingKind.RetryExhausted =>
                new RetryExhaustedEvidence(
                    6, 7, 1, 20, 20, 0.857, 3, 12, configuration, 41_000, 3_000, 0,
                    new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc), "a3f9c2e",
                    [attemptExemplar], attemptExemplar),

            FindingKind.Flaky =>
                new FlakyEvidence(7, 20, 20, 5, 0.35, 2, 3, [signature], [exemplar], null),

            FindingKind.AlwaysFailing =>
                new AlwaysFailingEvidence(
                    19, 20, 20, 19, 0.95, 0, signature with { Occurrences = 19 }, 1.0,
                    [exemplar], null),

            FindingKind.TimingOut =>
                new TimingOutEvidence(
                    9, 10, 20, 20, 9, 0.45, 0.9, 0, 500, [512, 508, 503], [exemplar], null),

            FindingKind.BrokenFixture =>
                new BrokenFixtureEvidence(
                    nameof(FailureSite.TestSetup),
                    "CheckoutFixture.Setup",
                    signature,
                    12,
                    [new ClusterMember("fp", "MyApp.Tests.A", 4)],
                    47, 3, 20, 12,
                    new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc), "a3f9c2e", [exemplar]),

            FindingKind.SharedFailure =>
                new SharedFailureEvidence(
                    signature, 12, [new ClusterMember("fp", "MyApp.Tests.A", 4)], 47, 3, 20, 12,
                    new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc), "a3f9c2e", [exemplar]),

            FindingKind.DurationRegression =>
                new DurationRegressionEvidence(
                    new DurationProfile(1240, 1890, 4, 3, 3),
                    new DurationProfile(340, 410, 10, 10, 10),
                    new DurationDelta(264.7, 900),
                    new DurationShift(3.512, 1.94, 5.87, 251.2, 880, 0.004),
                    "a3f9c2e",
                    [],
                    null),

            FindingKind.DurationUnstable =>
                new DurationUnstableEvidence(18, 20, 820, 3100, 210, 4100, 18, 900, 0.71, []),

            FindingKind.ParallelSensitive =>
                new ParallelSensitiveEvidence(
                    new ConcurrencyTrend(
                        2.874, 0.00405, 0.612, 18, nameof(ConcurrencyDirection.WithConcurrency)),
                    new ConcurrencyRange(1, 14, 4),
                    [
                        new ConcurrencyLevel(1, 6, 6, 0, 0),
                        new ConcurrencyLevel(4, 5, 5, 1, 0.2),
                        new ConcurrencyLevel(9, 5, 5, 3, 0.6),
                        new ConcurrencyLevel(14, 4, 4, 3, 0.75)
                    ],
                    [],
                    null),

            FindingKind.TimeSensitive =>
                new TimeSensitiveEvidence(
                    "LocalTimeOfDay",
                    new TimeArm(9, 10, 0.9, 7, "18:00-24:00 local"),
                    new TimeArm(0, 6, 0, 0, "the rest of the day"),
                    new TimeDelta(0.9, 90),
                    new TimeSignificance(0.001748, 2),
                    "Europe/Berlin",
                    [],
                    null),

            FindingKind.Vanished =>
                new VanishedEvidence(
                    12, 17, 3, 40, new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc), "a3f9c2e"),

            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    [Theory]
    [MemberData(nameof(EveryEvidenceShape))]
    public void EveryHeadlineIsAsciiAndCarriesItsDenominators(string kind)
    {
        FindingKind parsed = Enum.Parse<FindingKind>(kind);

        var (headline, metrics) = EvidenceHeadline.For(parsed, EvidenceFor(parsed));

        Assert.NotEmpty(headline);

        // The one property that keeps a pasted fence intact in a client whose font, encoding or
        // markdown dialect is not ours to choose.
        Assert.All(headline, c => Assert.InRange(c, (char)0x20, (char)0x7E));

        Assert.NotEmpty(metrics);
        Assert.All(metrics, m =>
        {
            Assert.NotEmpty(m.Label);
            Assert.NotEmpty(m.Value);
        });
    }

    [Fact]
    public void ASlowerHeadlineLeadsWithTheFactorAndItsInterval()
    {
        var (headline, metrics) = EvidenceHeadline.For(
            FindingKind.DurationRegression, EvidenceFor(FindingKind.DurationRegression));

        // The sentence a developer reads first. It leads with the normalised claim rather than the
        // clock, because the raw pair can fall while the test slows, and it carries the interval,
        // because "3.51x slower" alone invites the reader to treat three runs as settled.
        Assert.Equal(
            "3.51x slower (95% CI 1.94-5.87x), 340ms -> 1.2s on the clock", headline);

        Assert.Contains(metrics, m => m.Value == "3.51x (95% CI 1.94-5.87x), +880ms at reference speed");
        Assert.Contains(metrics, m => m.Value == "p 0.004 one-sided, 3 recent runs against 10");
    }

    [Fact]
    public void ATimeSensitiveFindingShowsHowWideASearchFoundIt()
    {
        var (headline, metrics) = EvidenceHeadline.For(
            FindingKind.TimeSensitive, EvidenceFor(FindingKind.TimeSensitive));

        // The headline is unchanged by the search charge: what a reader needs first is when the test
        // fails and over how many days, and a probability answers neither question.
        Assert.Equal(
            "failed 90% in 18:00-24:00 local against 0% in the rest of the day, gap 90 pts " +
            "across 7 days",
            headline);

        // The probability is the one the search has already been charged for, and the count beside
        // it says how wide that search was — which is the difference between a gap someone went
        // looking for and one that was there to begin with.
        Assert.Contains(metrics, m => m.Value == "p 0.00175 two-sided, 2 splits compared");
    }

    [Fact]
    public void AConcurrencyFindingShowsItsDoseResponse()
    {
        var (headline, metrics) = EvidenceHeadline.For(
            FindingKind.ParallelSensitive, EvidenceFor(FindingKind.ParallelSensitive));

        // Both ends of the range, always lowest concurrency first, with the direction carried by a
        // word — and the level count, which is what separates a dose-response from two points that
        // happened to differ.
        Assert.Equal(
            "failed 0% at concurrency 1 and 75% at 14, rising with concurrency across 4 levels " +
            "in 18 runs",
            headline);

        // The probability was computed over runs, not over attempts, so the run count travels with
        // it: a trend over eight runs and one over forty read alike without it.
        Assert.Contains(metrics, m => m.Value == "p 0.00405 two-sided, Z 2.87 over 18 runs");
    }

    [Fact]
    public void AConcurrencyHeadlineNeverQuotesAPairThatContradictsItsOwnTrend()
    {
        // A rising trend established by two well-populated levels in the middle, with a single
        // execution at each end of the range running the other way. Quoting the ends of the range —
        // the obvious choice — would print "failed 100% at concurrency 1 and 0% at 14, rising with
        // concurrency". The widest rising step in the table is the pair that actually shows it.
        var evidence = new ParallelSensitiveEvidence(
            new ConcurrencyTrend(
                2.51, 0.0121, 0.402, 22, nameof(ConcurrencyDirection.WithConcurrency)),
            new ConcurrencyRange(1, 14, 4),
            [
                new ConcurrencyLevel(1, 1, 1, 1, 1.0),
                new ConcurrencyLevel(4, 10, 10, 0, 0),
                new ConcurrencyLevel(9, 10, 10, 6, 0.6),
                new ConcurrencyLevel(14, 1, 1, 0, 0)
            ],
            [],
            null);

        var (headline, _) = EvidenceHeadline.For(FindingKind.ParallelSensitive, evidence);

        Assert.Equal(
            "failed 0% at concurrency 4 and 60% at 9, rising with concurrency across 4 levels " +
            "in 22 runs",
            headline);
    }

    [Fact]
    public void AFallingConcurrencyHeadlineQuotesItsPairTheSameWayRound()
    {
        // The milder level is named first whichever way the trend runs, so the sentence always reads
        // along the same axis and the direction is carried by the word rather than by the order.
        var evidence = new ParallelSensitiveEvidence(
            new ConcurrencyTrend(
                -2.51, 0.0121, -0.402, 22, nameof(ConcurrencyDirection.AgainstConcurrency)),
            new ConcurrencyRange(1, 9, 2),
            [
                new ConcurrencyLevel(1, 10, 10, 7, 0.7),
                new ConcurrencyLevel(9, 10, 10, 1, 0.1)
            ],
            [],
            null);

        var (headline, _) = EvidenceHeadline.For(FindingKind.ParallelSensitive, evidence);

        Assert.Equal(
            "failed 10% at concurrency 9 and 70% at 1, falling with concurrency across 2 levels " +
            "in 22 runs",
            headline);
    }

    [Fact]
    public void AHeadlineNamesTheFailureTypeOnlyWhenTheAdapterRecordedOne()
    {
        SignatureView unnamed = new(
            "abc123", null, "no detail", [], false, true, 19,
            new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc), null, 0, true, false);

        var (headline, _) = EvidenceHeadline.For(
            FindingKind.AlwaysFailing,
            new AlwaysFailingEvidence(19, 20, 20, 19, 0.95, 0, unnamed, 1.0, [], null));

        // An adapter that captures no failure detail is not the same as a failure that had none.
        Assert.EndsWith("one failure mode", headline, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAlwaysFailingHeadlineSaysWhenTheFailuresWereNotIdentical()
    {
        // "One failure mode" is a claim about every failure, and the classification only requires a
        // dominant one. A reader who opens two exemplars and finds different messages has to be able
        // to see that the report already knew.
        var (headline, metrics) = EvidenceHeadline.For(
            FindingKind.AlwaysFailing, EvidenceFor(FindingKind.AlwaysFailing) switch
            {
                // 17 of the 19 failures agreed, which is what a share below one means.
                AlwaysFailingEvidence e => e with
                {
                    Signature = e.Signature with { Occurrences = 17 },
                    ModalSignatureShare = 0.895
                },
                var other => other
            });

        Assert.Equal(
            "failed 19 of 20 executions (95%), one dominant failure mode (89.5% of failures): " +
            "System.InvalidOperationException",
            headline);

        Assert.Contains(
            metrics, m => m.Label == "dominant mode" && m.Value == "89.5% of failures");
    }

    [Fact]
    public void TheRetryHeadlinesNameTheirUnitOnceAndAtTheEnd()
    {
        // Pinned as whole sentences because the two counts either side of a retry comparison share
        // one trailing unit, as "3 of 12 runs" does everywhere else here. Naming it on the first
        // number and not the second reads as though the two were counting different things.
        var (deepening, _) = EvidenceHeadline.For(
            FindingKind.RetryDeepening, EvidenceFor(FindingKind.RetryDeepening));

        Assert.Equal(
            "attempts to pass 1 -> 3 (+2) across 3 recent and 14 earlier runs, " +
            "2.4s spent retrying",
            deepening);

        var (exhausted, _) = EvidenceHeadline.For(
            FindingKind.RetryExhausted, EvidenceFor(FindingKind.RetryExhausted));

        Assert.Equal(
            "gave up after 3 attempts in 6 of 7 retried runs (85.7%), 41s spent retrying",
            exhausted);
    }

    // ---------------------------------------------------------------------
    // The fenced report
    // ---------------------------------------------------------------------

    [Fact]
    public void NothingInsideTheFenceExceedsTheWidth()
    {
        string report = Render(Envelope(
            Finding(
                "Flaky",
                "high",
                "MyApp.Tests.Checkout.Integration.VeryLongNamespace.PlacesAnOrderAndSettlesIt",
                "failed 7 of 20 executions (35%) in 5 of 20 runs, 3 failure modes, " +
                "and a great deal more text besides so that wrapping has to happen"),
            Finding("DurationUnstable", "low", "Short", "p50 820ms, dispersion 0.71")));

        foreach (string line in Fenced(report))
            Assert.True(line.Length <= FenceWidth, $"'{line}' is {line.Length} columns");
    }

    [Fact]
    public void TheFenceOpensAndClosesOnceEvenWithNothingToReport()
    {
        string clean = Render(Envelope());
        string dirty = Render(Envelope(Finding("Flaky", "high", "Alpha", "failed twice")));

        // A clean report and a full one paste as the same shape, or a reader learns to read the
        // presence of a block as bad news.
        Assert.Equal(2, Lines(clean).Count(l => l.Trim() == Fence));
        Assert.Equal(2, Lines(dirty).Count(l => l.Trim() == Fence));
        Assert.Single(Fenced(clean));
    }

    [Fact]
    public void EveryHeadlineIsInsideTheFence()
    {
        string report = Render(Envelope(
            Finding("Flaky", "high", "Alpha", "failed 7 of 20 executions (35%)"),
            Finding("Vanished", "low", "Beta", "ran in 12 of 17 earlier runs")));

        Assert.Contains(
            Fenced(report), l => l.Contains("failed 7 of 20 executions", StringComparison.Ordinal));
        Assert.Contains(
            Fenced(report), l => l.Contains("ran in 12 of 17 earlier", StringComparison.Ordinal));
    }

    [Fact]
    public void TheRestOfTheFindingsAreOfferedOnlyWhenSomeWereWithheld()
    {
        FindingDto[] findings =
        [
            Finding("Flaky", "high", "Alpha", "failed 7 of 20 executions (35%)"),
            Finding("Vanished", "low", "Beta", "ran in 12 of 17 earlier runs")
        ];

        string truncated = Render(Envelope(findings, shown: 1, total: 21));
        string complete = Render(Envelope(findings));

        // One offer for the whole report. Ten near-identical command lines are ten lines of noise in
        // anything the report is pasted into.
        Assert.Equal(
            1, Lines(truncated).Count(l => l.Contains("xping report --all", StringComparison.Ordinal)));
        Assert.Contains("Showing 1 of 21", truncated, StringComparison.Ordinal);

        // And no offer at all when there is nothing more to show: the command would be the one the
        // reader just ran, and it names no format, so it cannot be offering a different view either.
        Assert.DoesNotContain("xping report --all", complete, StringComparison.Ordinal);
        Assert.EndsWith(Fence + Environment.NewLine, complete, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoRendersOfOneEnvelopeAreByteIdentical()
    {
        ReportEnvelope envelope = Envelope(
            Finding("Flaky", "high", "Alpha", "failed 7 of 20 executions (35%)"));

        Assert.Equal(Render(envelope), Render(envelope));
    }

    [Fact]
    public void ColourIsEmittedForATerminalAndNeverForAPipe()
    {
        ReportEnvelope envelope = Envelope(
            Finding("Flaky", "high", "Alpha", "failed 7 of 20 executions (35%)"));

        string piped = Render(envelope, Capabilities(redirected: true));
        string terminal = Render(envelope, Capabilities(redirected: false));

        Assert.DoesNotContain("\u001b", piped, StringComparison.Ordinal);
        Assert.Contains("\u001b", terminal, StringComparison.Ordinal);

        // The escape codes are the only difference: colour must not move a column.
        Assert.Equal(piped, Strip(terminal));
    }

    [Fact]
    public void TheOneLineSummaryStatesTheSameCountsAsTheReport()
    {
        ReportEnvelope envelope = Envelope(
            Finding("Flaky", "high", "Alpha", "failed 7 of 20 executions (35%)"));

        using var writer = new StringWriter();
        new SummaryReportRenderer().Render(envelope, writer);

        Assert.Equal(
            "Xping: 1 finding (1 high) in 20 runs of MyApp.Tests",
            writer.ToString().TrimEnd());
    }

    // ---------------------------------------------------------------------
    // Capabilities
    // ---------------------------------------------------------------------

    [Fact]
    public void APipeGetsAsciiWithoutColourOrDecoration()
    {
        OutputCapabilities capabilities = OutputCapabilities.Resolve(
            forceAscii: false, noColor: false, redirected: true, _ => null);

        Assert.Same(ReportGlyphs.Ascii, capabilities.Glyphs);
        Assert.False(capabilities.Color);
        Assert.False(capabilities.Decorate);
    }

    [Fact]
    public void NoColorWinsOverEverything()
    {
        OutputCapabilities byFlag = OutputCapabilities.Resolve(false, noColor: true, false, _ => null);
        OutputCapabilities byVariable = OutputCapabilities.Resolve(
            false, false, false, name => name == "NO_COLOR" ? "1" : null);

        Assert.False(byFlag.Color);
        Assert.False(byVariable.Color);

        // Both together: the informal standard says NO_COLOR wins, and a caller who set both is more
        // likely to have inherited FORCE_COLOR from a tool than to have meant it here.
        OutputCapabilities both = OutputCapabilities.Resolve(
            false, false, true, name => "1");

        Assert.False(both.Color);
    }

    [Fact]
    public void ForceColorLiftsColourButNeverDecoration()
    {
        OutputCapabilities capabilities = OutputCapabilities.Resolve(
            false, false, redirected: true, name => name == "FORCE_COLOR" ? "1" : null);

        Assert.True(capabilities.Color);

        // FORCE_COLOR says what the stream can render, not that a caller piping the report into a
        // file wants a call to action in it.
        Assert.False(capabilities.Decorate);
    }

    // ---------------------------------------------------------------------
    // The source location trailer
    // ---------------------------------------------------------------------

    /// <summary>
    /// The location is the difference between knowing a test is flaky and being able to open it, so
    /// it goes on the trailer whenever the SDK captured one.
    /// </summary>
    [Fact]
    public void AFindingCarryingASourceLocationEndsItsTrailerWithFileAndLine()
    {
        string report = Render(Envelope(
            Finding("Flaky", "high", "CartTests.Checkout", "failed 7 of 20", "tests/CartTests.cs", 42)));

        string trailer = Fenced(report).Single(l => l.Contains("evidence", StringComparison.Ordinal));

        Assert.EndsWith("tests/CartTests.cs:42", trailer.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void AFindingWithAFileButNoLineShowsTheFileAlone()
    {
        string report = Render(Envelope(
            Finding("Flaky", "high", "CartTests.Checkout", "failed 7 of 20", "tests/CartTests.cs", null)));

        string trailer = Fenced(report).Single(l => l.Contains("evidence", StringComparison.Ordinal));

        Assert.EndsWith("tests/CartTests.cs", trailer.TrimEnd(), StringComparison.Ordinal);
        Assert.DoesNotContain("CartTests.cs:", trailer, StringComparison.Ordinal);
    }

    [Fact]
    public void AFindingWithNoSourceLocationSaysNothingAboutOne()
    {
        string report = Render(Envelope(
            Finding("Flaky", "high", "CartTests.Checkout", "failed 7 of 20", null, null)));

        string trailer = Fenced(report).Single(l => l.Contains("evidence", StringComparison.Ordinal));

        Assert.EndsWith("f_2a91", trailer.TrimEnd(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A long path must not push the trailer past the fence, and must not be paid for by the
    /// evidence level: truncation cuts from the left, so without a budget of its own the path would
    /// survive whole and eat the words in front of it.
    /// </summary>
    [Fact]
    public void ALongPathIsElidedRatherThanTheRestOfTheTrailer()
    {
        string report = Render(Envelope(Finding(
            "Flaky",
            "high",
            "CartTests.Checkout",
            "failed 7 of 20",
            "tests/Integration/Checkout/Regression/Baskets/CartTests.cs",
            1042)));

        string trailer = Fenced(report).Single(l => l.Contains("evidence", StringComparison.Ordinal));

        Assert.Contains("evidence moderate | f_2a91 | ", trailer, StringComparison.Ordinal);
        Assert.EndsWith("CartTests.cs:1042", trailer.TrimEnd(), StringComparison.Ordinal);
        Assert.Contains("...", trailer, StringComparison.Ordinal);
    }

    /// <summary>
    /// The elision cuts at a directory boundary, so what is left still reads as a path rather than
    /// as a name broken part-way through.
    /// </summary>
    [Fact]
    public void AnElidedPathKeepsWholeDirectorySegments()
    {
        string report = Render(Envelope(Finding(
            "Flaky",
            "high",
            "CartTests.Checkout",
            "failed 7 of 20",
            "tests/Integration/Checkout/Regression/Baskets/CartTests.cs",
            1042)));

        string trailer = Fenced(report).Single(l => l.Contains("evidence", StringComparison.Ordinal));
        string path = trailer.Split(" | ", StringSplitOptions.None)[^1].TrimEnd();

        Assert.StartsWith(".../", path, StringComparison.Ordinal);
    }

    /// <summary>
    /// The width invariant, restated for the trailer specifically: a path is the longest thing the
    /// SDK can put on one of these lines.
    /// </summary>
    [Fact]
    public void ATrailerWithAVeryLongPathStillFitsInsideTheFence()
    {
        string report = Render(Envelope(Finding(
            "Flaky",
            "high",
            "CartTests.Checkout",
            "failed 7 of 20",
            "tests/" + string.Join("/", Enumerable.Repeat("VeryLongDirectoryName", 12)) + "/CartTests.cs",
            1042)));

        Assert.All(Fenced(report), line => Assert.True(
            line.Length <= FenceWidth,
            $"'{line}' is {line.Length} columns, over the {FenceWidth} the fence allows"));
    }

    /// <summary>
    /// The trailer's budget must spend the fence exactly, neither overrunning it nor eliding a path
    /// that would have fitted. Both failure directions are invisible in ordinary output — one shows
    /// up only at the widest paths, the other only as an ellipsis nobody questions — so the boundary
    /// is pinned here.
    /// </summary>
    [Theory]
    [InlineData("moderate")]
    [InlineData("high")]
    public void ThePathIsGivenEveryColumnTheTrailerHasLeftAndNoMore(string evidence)
    {
        // Grow one column at a time and find the longest path that survives whole.
        string longest = "";
        for (int length = 10; length < FenceWidth; length++)
        {
            string path = "tests/" + new string('x', length - 10) + ".cs";
            string trailer = Trailer(Render(Envelope(FindingWith(evidence, path, null))));

            if (!trailer.Contains("...", StringComparison.Ordinal))
                longest = path;
        }

        Assert.NotEqual("", longest);

        // That path fills the line to the fence, proving no column was left unspent.
        string full = Trailer(Render(Envelope(FindingWith(evidence, longest, null))));
        Assert.Equal(FenceWidth, full.Length);
        Assert.EndsWith(longest, full, StringComparison.Ordinal);
    }

    /// <summary>
    /// The location may be dropped when the trailer has no room, but it must never be the reason the
    /// line overflows or the head gets cut — a finding id long enough to squeeze the budget must
    /// still leave "evidence …" readable.
    /// </summary>
    [Fact]
    public void AnOversizedTrailerDropsTheLocationRatherThanTruncatingTheEvidence()
    {
        FindingDto finding = FindingWith("moderate", "tests/Cart/CartTests.cs", 42) with
        {
            Id = new string('f', FenceWidth - Indent - "evidence moderate | ".Length)
        };

        string trailer = Trailer(Render(Envelope(finding)));

        Assert.True(trailer.Length <= FenceWidth, $"'{trailer}' is {trailer.Length} columns");
        Assert.StartsWith("evidence moderate | ", trailer.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("CartTests", trailer, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static OutputCapabilities Capabilities(bool redirected) =>
        OutputCapabilities.Resolve(forceAscii: true, noColor: false, redirected, _ => null);

    private static string Render(ReportEnvelope envelope, OutputCapabilities? capabilities = null)
    {
        using var writer = new StringWriter();
        new TextReportRenderer(capabilities ?? Capabilities(redirected: true)).Render(envelope, writer);

        return writer.ToString();
    }

    private static string[] Lines(string report) =>
        report.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

    /// <summary>
    /// Returns the lines between the fences — the part that has to survive a paste.
    /// </summary>
    private static string[] Fenced(string report)
    {
        string[] lines = Lines(report);
        int open = Array.FindIndex(lines, l => l.Trim() == Fence);
        int close = Array.FindLastIndex(lines, l => l.Trim() == Fence);

        Assert.True(open >= 0 && close > open, "the report is not fenced");

        return lines[(open + 1)..close];
    }

    private static string Strip(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);

        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != '\u001b')
            {
                builder.Append(value[i]);
                continue;
            }

            while (i < value.Length && value[i] != 'm')
                i++;
        }

        return builder.ToString();
    }

    private static FindingDto FindingWith(string evidence, string? sourceFile, int? sourceLineNumber) =>
        Finding("Flaky", "high", "CartTests.Checkout", "failed 7 of 20", sourceFile, sourceLineNumber)
            with { EvidenceLevel = evidence };

    /// <summary>Returns the dim trailer line of a single-finding report.</summary>
    private static string Trailer(string report) =>
        Fenced(report).Single(l => l.Contains("evidence", StringComparison.Ordinal)).TrimEnd();

    private static FindingDto Finding(
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

    private static FindingDto Finding(string kind, string severity, string name, string headline) =>
        new(
            "f_2a91",
            kind,
            severity,
            "moderate",
            new SubjectDto("test", "fp", name, name, null, null, "MyApp.Tests", null, null, null),
            headline,
            [new MetricDto("failed", "7 of 20 executions (35%)")],
            null,
            "xping report --kind Flaky --format json");

    private static ReportEnvelope Envelope(params FindingDto[] findings) =>
        Envelope(findings, findings.Length, findings.Length);

    private static ReportEnvelope Envelope(FindingDto[] findings, int shown, int total)
    {
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
                412 - findings.Length,
                0,
                0,
                0,
                0,
                []),
            findings,
            new TruncationDto(shown, total, "xping report --all"));
    }
}
