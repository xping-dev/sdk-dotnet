/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report;
using Xping.Cli.Report.Model;
using Xping.Cli.Report.Providers;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Cli.Tests.Report;

/// <summary>
/// What a suite-sized store reports when there is nothing in it to report.
/// </summary>
/// <remarks>
/// <para>
/// The other test classes here ask whether a provider notices one planted effect. This one asks the
/// opposite and larger question — what the report says about three hundred tests that are merely
/// unreliable — because that is the question a per-comparison threshold cannot answer however well
/// it is calibrated. A bar that fires once in twenty fires fifteen times on three hundred tests, and
/// every one of those fifteen is a developer's afternoon.
/// </para>
/// <para>
/// The store is the issue's own model of a bad suite: three hundred tests, a tenth of them failing
/// about three runs in ten and the rest solid, with the failures independent of the clock and of how
/// many tests ran alongside them. There is no effect anywhere in it. The proportion matters — a
/// suite where every test failed three runs in ten would have most of its runs discarded as
/// environmental before any of this was reached, which is a different bug being tested.
/// </para>
/// </remarks>
public sealed class FalseDiscoveryRateTests
{
    /// <summary>Tests in the synthetic suite.</summary>
    private const int Tests = 300;

    /// <summary>
    /// Tests that are unreliable; the rest of the suite is solid.
    /// </summary>
    /// <remarks>
    /// A tenth, which is what the issue models and what keeps a run's failure rate under
    /// <see cref="Xping.Cli.Report.LocalAnalysisConstants.EnvironmentalSessionFailureRate"/>. Making
    /// every test unreliable would have half the window written off as an outage, and the providers
    /// would then be declining runs rather than declining findings.
    /// </remarks>
    private const int Unreliable = 30;

    /// <summary>Chance one of those fails a given run, alike for every test and every run.</summary>
    private const double FailureRate = 0.30;

    /// <summary>Null stores drawn, so that a rate is measured rather than a single sample.</summary>
    private const int Stores = 20;

    /// <summary>Seed of the first of them; the rest follow it.</summary>
    private const int FirstSeed = 1000;

    /// <summary>Concurrency levels the runs are spread over, so a trend has something to fit.</summary>
    private static readonly int[] Levels = [1, 2, 4, 8];

    /// <summary>
    /// Twenty suites of three hundred merely-unreliable tests are not one finding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Twenty stores rather than one, because a false discovery rate is a claim about a distribution
    /// and a single draw of it says nothing either way. The seeds are fixed and the generator is
    /// written out here, so this is one particular twenty and the same twenty on every machine.
    /// </para>
    /// <para>
    /// The measured figures, recorded so that a later change to any threshold shows up as a number
    /// rather than as a mood: the two providers between them offer 28 candidates across the twenty
    /// stores — between one and two per report, on suites with nothing whatever in them — and the
    /// pass reports none of them, in any of the twenty. That is the whole issue, stated as a test.
    /// </para>
    /// <para>
    /// The candidate count is asserted rather than assumed. A pass that silenced everything by
    /// silencing its own input would satisfy the second assertion while doing nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public void SuitesWithNoEffectInThemReportNoTimeOrConcurrencyFindings()
    {
        int offered = 0;
        var reported = new List<int>();

        for (int store = 0; store < Stores; store++)
        {
            AnalysisContext context = NullStore(sessions: 20, seed: FirstSeed + store);

            offered +=
                new TimeSensitiveProvider().Analyze(context).Candidates.Count +
                new ParallelSensitiveProvider().Analyze(context).Candidates.Count;

            reported.Add(Run(context).Findings.Count);
        }

        Assert.True(offered > 0, "the providers offered nothing, so the pass was not exercised");
        Assert.Equal(new int[Stores], reported);
    }

    /// <summary>
    /// A real effect, in the same store, still arrives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The planted test fails every run inside one six-hour quarter of the local day and never
    /// outside it, over a forty-run window. That is deliberately unambiguous, and the size is the
    /// honest cost of the correction rather than a number tuned to clear it: against a family of
    /// three hundred the bar for the most significant finding of a kind is 0.10/300, and on a
    /// twenty-run window even a perfect separation only just reaches it. Forty runs is what a suite
    /// of this size has to have accumulated before a clock reading is worth printing.
    /// </para>
    /// <para>
    /// The rest of the store is the same three hundred tests with nothing in them, so the finding
    /// arrives against the same multiplicity that silenced the noise above.
    /// </para>
    /// </remarks>
    [Fact]
    public void APlantedEffectIsStillReportedAgainstTheSameMultiplicity()
    {
        AnalysisContext context = NullStore(sessions: 40, seed: FirstSeed, planted: "Planted");

        AnalysisResult result = Run(context);

        Finding finding = Assert.Single(result.Findings, f => f.Kind == FindingKind.TimeSensitive);

        Assert.Equal(
            "MyApp.Tests.SampleTests.Planted",
            Assert.Single(finding.Subject.Tests).FullyQualifiedName);
    }

    /// <summary>
    /// Kinds that count rather than test are untouched by any of this.
    /// </summary>
    /// <remarks>
    /// The bypass, asserted where it matters rather than only on a stub. `Flaky` is a statement about
    /// how often a test failed, not a hypothesis about why, so three hundred flaky tests in a store
    /// are three hundred findings and the multiplicity pass has no opinion about them.
    /// </remarks>
    [Fact]
    public void CountedKindsSurviveASuiteSizedStoreUncorrected()
    {
        AnalysisContext context = NullStore(sessions: 20, seed: FirstSeed);

        using var warnings = new StringWriter();
        AnalysisResult result = new FindingCoordinator([new FailureModeProvider()])
            .Run(context, null, warnings);

        // Every candidate the provider offered is a finding: the pass took nothing.
        Assert.Equal(
            new FailureModeProvider().Analyze(context).Candidates.Count, result.Findings.Count);

        Assert.Equal(0, result.ExcludedNotSignificant);

        // And it is the unreliable tests rather than a handful of survivors. Nothing here is a
        // hypothesis, so the size of the suite is not a reason to say less about it.
        Assert.True(
            result.Findings.Count > Unreliable * 0.9,
            $"{result.Findings.Count} findings from {Unreliable} unreliable tests");
    }

    private static AnalysisResult Run(AnalysisContext context)
    {
        using var warnings = new StringWriter();

        return new FindingCoordinator([new TimeSensitiveProvider(), new ParallelSensitiveProvider()])
            .Run(context, null, warnings);
    }

    /// <summary>
    /// Builds a suite-sized window in which failures are independent of everything measurable.
    /// </summary>
    /// <param name="sessions">Runs in the window.</param>
    /// <param name="seed">Fixes the failures, so the store is the same one on every machine.</param>
    /// <param name="planted">
    /// A test to give a genuine time effect to, or <see langword="null"/> for a store with no effect
    /// in it at all.
    /// </param>
    /// <returns>The window, its sessions and its indexes.</returns>
    /// <remarks>
    /// One run per day at an hour between nine and seven, which is what a dev store looks like and
    /// what the temporal axes need: runs clustered into one hour would leave every split with an
    /// empty arm, and runs on fewer than three dates are declined outright.
    /// </remarks>
    private static AnalysisContext NullStore(int sessions, int seed, string? planted = null)
    {
        var random = new Sequence(seed);
        var built = new List<TestSession>(sessions);

        for (int ordinal = 0; ordinal < sessions; ordinal++)
        {
            DateTime startedAt = TestSessionFactory.Epoch.Date
                .AddDays(ordinal)
                .AddHours(9 + random.Below(10))
                .AddMinutes(random.Below(60));

            int concurrency = Levels[random.Below(Levels.Length)];
            var executions = new List<TestExecution>(Tests + 1);

            for (int test = 0; test < Tests; test++)
            {
                executions.Add(Execution(
                    $"Test{test}",
                    test < Unreliable && random.Fraction() < FailureRate,
                    concurrency));
            }

            if (planted != null)
            {
                // Local hours 18 to 23, which is where a +2 offset puts a UTC start of 16 or later.
                // The one thing in the store that is not chance.
                executions.Add(Execution(planted, (startedAt.Hour + 2) >= 18, concurrency));
            }

            built.Add(TestSessionFactory.Session(
                ordinal,
                executions,
                startedAt: startedAt,
                utcOffset: TimeSpan.FromHours(2)));
        }

        return TestSessionFactory.Context([.. built]);
    }

    private static TestExecution Execution(string name, bool failed, int concurrency) =>
        TestSessionFactory.Execution(
            name,
            failed ? TestOutcome.Failed : TestOutcome.Passed,
            // Its own message per test. One shared message would make every failure in a run the
            // same signature, and the report would correctly read three hundred tests failing alike
            // as one shared cause rather than as three hundred unreliable tests.
            errorMessage: failed ? $"{name} did not agree" : null,
            concurrency: concurrency);

    /// <summary>
    /// A seeded stream of pseudo-random numbers, written out rather than taken from the framework.
    /// </summary>
    /// <remarks>
    /// A false discovery rate is a claim about a distribution, so the store this class asserts on
    /// has to be one particular sample of it and the same one everywhere. <see cref="Random"/> is
    /// seedable but its sequence is explicitly not guaranteed stable across runtimes, so a store
    /// built from it can change under a framework upgrade and take the assertions with it. This is
    /// Numerical Recipes' 64-bit linear congruential generator, which is nine lines and fixed.
    /// </remarks>
    /// <param name="seed">Fixes the stream.</param>
    private sealed class Sequence(int seed)
    {
        private ulong state = (ulong)seed;

        /// <summary>Draws a value in [0, bound).</summary>
        /// <param name="bound">One past the largest value drawn.</param>
        /// <returns>The value.</returns>
        public int Below(int bound) => (int)(Next() % (ulong)bound);

        /// <summary>Draws a value in [0, 1).</summary>
        /// <returns>The value.</returns>
        public double Fraction() => (Next() >> 11) * (1.0 / (1UL << 53));

        private ulong Next() => state = (state * 6364136223846793005UL) + 1442695040888963407UL;
    }
}
