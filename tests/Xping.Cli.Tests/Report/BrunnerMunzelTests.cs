/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Scoring;

namespace Xping.Cli.Tests.Report;

public sealed class BrunnerMunzelTests
{
    // Draws per cell in the simulations below. Enough to separate a rejection rate of 0.05 from
    // the 0.08 an approximation would produce at these sizes, small enough that a comparison
    // enumerating over a thousand arrangements can be run this many times in a test suite.
    private const int Draws = 5_000;

    // ---------------------------------------------------------------------------------------
    // What the statistic estimates
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void EveryRecentReadingAboveEveryBaselineOneIsCompleteSuperiority()
    {
        Assert.Equal(1.0, BrunnerMunzel.Superiority([1, 2, 3, 4, 5], [6, 7, 8]));
    }

    [Fact]
    public void EveryRecentReadingBelowEveryBaselineOneIsNone()
    {
        Assert.Equal(0.0, BrunnerMunzel.Superiority([6, 7, 8, 9, 10], [1, 2, 3]));
    }

    [Fact]
    public void TwoArmsThatCannotBeToldApartAreEven()
    {
        Assert.Equal(0.5, BrunnerMunzel.Superiority([2, 2, 2, 2, 2], [2, 2, 2]));
    }

    [Fact]
    public void TiesCountAsHalfAnOrdering()
    {
        // One recent reading against two baseline ones, sitting on top of the larger. It beats the
        // first outright and ties with the second, so it is above one and a half of the two.
        Assert.Equal(0.75, BrunnerMunzel.Superiority([1, 2], [2]));
    }

    [Fact]
    public void TheEstimateCountsPairsAndCanBeCheckedByCountingThem()
    {
        // Every pair, by hand: 5 beats 1 and 4, ties nothing; 3 beats 1 only; 2 beats 1 only.
        // Four wins out of six pairs.
        Assert.Equal(4.0 / 6, BrunnerMunzel.Superiority([1, 4], [5, 3, 2]), 12);
    }

    // ---------------------------------------------------------------------------------------
    // The p-value, and the floor three readings put under it
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(5, 3, 56)]      // the arm floors the duration provider gates at
    [InlineData(7, 3, 120)]
    [InlineData(17, 3, 1140)]
    [InlineData(10, 5, 3003)]
    public void CompleteSeparationIsWorthExactlyOneArrangementInAll(
        int baselineCount, int currentCount, int arrangements)
    {
        // The strongest statement this test can ever make, and it is not a small p-value: with
        // three recent runs against five, every one of them slower than everything before it is
        // one arrangement in fifty-six. Reporting anything smaller would be inventing evidence.
        double[] baseline = Ascending(1, baselineCount);
        double[] current = Ascending(1000, currentCount);

        Assert.Equal(1.0 / arrangements, BrunnerMunzel.OneSidedPValue(baseline, current), 12);
    }

    [Fact]
    public void AWhollyTiedSampleIsCertainRatherThanImpossible()
    {
        // The shape a fast test takes when every run rounds to the same millisecond. Both rank
        // variances are exactly zero and the studentised statistic is 0/0, so a comparison written
        // against the raw arithmetic would compare against NaN, count nothing and report a p-value
        // of zero — a certainty of a slowdown, from a sample in which nothing moved at all.
        Assert.Equal(1.0, BrunnerMunzel.OneSidedPValue([2, 2, 2, 2, 2, 2, 2], [2, 2, 2]));
    }

    [Fact]
    public void SeparationWithNoSpreadInsideEitherArmIsTheFloorRatherThanZero()
    {
        // The other degenerate shape, and the one every steady regressing test produces: the
        // statistic is a positive number over zero. Treated as the infinity it is, exactly one
        // arrangement of the hundred and twenty ties with it.
        Assert.Equal(1.0 / 120, BrunnerMunzel.OneSidedPValue([2, 2, 2, 2, 2, 2, 2], [8, 8, 8]), 12);
    }

    [Fact]
    public void ARecentArmBelowTheBaselineIsNotEvidenceOfASlowdown()
    {
        // One-sided, in the direction the caller claims. A test that got faster is at the other end
        // of the distribution, not near this end of it.
        Assert.Equal(1.0, BrunnerMunzel.OneSidedPValue([6, 7, 8, 9, 10], [1, 2, 3]));
    }

    [Fact]
    public void OneBaselineRunSlowerThanOneRecentRunIsAlreadyTooMuchAtTheArmFloor()
    {
        // Five baseline runs against three is the thinnest comparison the duration provider will
        // make, and it has fifty-six arrangements. Let one baseline run be slower than all three
        // recent ones and four arrangements are at least this favourable, so 0.071 and no finding.
        // A single overlapping run is the difference between a claim and silence at these sizes,
        // which is the honest cost of three recent runs rather than a defect in the test.
        Assert.Equal(4.0 / 56, BrunnerMunzel.OneSidedPValue([1, 1, 1, 1, 9], [8, 8, 8]), 12);
    }

    // ---------------------------------------------------------------------------------------
    // Only the ordering matters
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AnyIncreasingTransformOfTheReadingsLeavesBothAnswersAlone()
    {
        // The claim that lets the duration provider compare normalised durations directly and take
        // its effect size as a ratio, rather than computing logarithms it would only have to
        // exponentiate again. A rank test cannot see a monotone transform.
        double[] baseline = [1.2, 0.8, 3.4, 2.0, 0.9, 1.1, 5.0];
        double[] current = [4.1, 7.7, 6.2];

        double[] logged = [.. baseline.Select(v => Math.Log(v))];
        double[] loggedCurrent = [.. current.Select(v => Math.Log(v))];

        Assert.Equal(
            BrunnerMunzel.Superiority(baseline, current),
            BrunnerMunzel.Superiority(logged, loggedCurrent),
            12);

        Assert.Equal(
            BrunnerMunzel.OneSidedPValue(baseline, current),
            BrunnerMunzel.OneSidedPValue(logged, loggedCurrent),
            12);
    }

    [Fact]
    public void TheOrderTheReadingsArriveInDoesNotMoveTheAnswer()
    {
        double[] baseline = [3.4, 0.8, 1.2, 5.0, 0.9, 2.0, 1.1];
        double[] shuffled = [1.1, 5.0, 0.9, 1.2, 2.0, 3.4, 0.8];

        Assert.Equal(
            BrunnerMunzel.OneSidedPValue(baseline, [6.2, 4.1, 7.7]),
            BrunnerMunzel.OneSidedPValue(shuffled, [4.1, 7.7, 6.2]),
            12);
    }

    // ---------------------------------------------------------------------------------------
    // Degenerate input
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AnEmptyArmClaimsNothingRatherThanThrowing()
    {
        Assert.Equal(0.5, BrunnerMunzel.Superiority([], [1, 2, 3]));
        Assert.Equal(0.5, BrunnerMunzel.Superiority([1, 2, 3], []));
        Assert.Equal(1.0, BrunnerMunzel.OneSidedPValue([], [1, 2, 3]));
        Assert.Equal(1.0, BrunnerMunzel.OneSidedPValue([1, 2, 3], []));
    }

    // ---------------------------------------------------------------------------------------
    // What it costs under the null
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0.20)]
    [InlineData(0.35)]
    [InlineData(0.50)]
    [InlineData(0.70)]
    public void TheRejectionRateStaysUnderFivePercentAtEveryDispersion(double coefficient)
    {
        // Seventeen baseline runs against three, drawn from one lognormal distribution with no
        // shift planted in it, at the dispersions test durations actually take. An exact test's
        // whole claim is this number, and it has to hold at every shape of the underlying data —
        // which is what "exact" buys over an approximation whose degrees of freedom are two.
        //
        // The theoretical level is not 0.05 but the largest attainable p-value at or below it: the
        // p-values here are twentieths of a percent apart, so 57/1140 is the last one admitted and
        // the rate is 0.0500 exactly. Over a million draws at each dispersion the measured rate
        // sits between 0.0497 and 0.0501; the band below is Monte-Carlo slack at five thousand.
        ulong state = 20260902UL + (ulong)(coefficient * 100);

        double sigma = Math.Sqrt(Math.Log(1 + (coefficient * coefficient)));
        int rejected = 0;

        for (int draw = 0; draw < Draws; draw++)
        {
            double[] baseline = Lognormal(ref state, 17, sigma);
            double[] current = Lognormal(ref state, 3, sigma);

            if (BrunnerMunzel.OneSidedPValue(baseline, current) <= 0.05)
                rejected++;
        }

        Assert.InRange((double)rejected / Draws, 0.04, 0.06);
    }

    [Fact]
    public void ATrueSlowdownIsStillFoundAtThreeRecentRuns()
    {
        // The companion to the rate above: a test that only ever declines is not a test. A true
        // doubling on a steady test separates cleanly enough to be found 99.7% of the time at
        // seventeen baseline runs — and 97.8% at the five the provider gates at, which is the
        // number that matters for the thinnest window it will report from.
        ulong state = 20260902UL;

        double sigma = Math.Sqrt(Math.Log(1 + (0.20 * 0.20)));
        int found = 0;

        for (int draw = 0; draw < Draws; draw++)
        {
            double[] baseline = Lognormal(ref state, 17, sigma);
            double[] current = Lognormal(ref state, 3, sigma);

            for (int i = 0; i < current.Length; i++)
                current[i] *= 2;

            if (BrunnerMunzel.OneSidedPValue(baseline, current) <= 0.05)
                found++;
        }

        Assert.InRange((double)found / Draws, 0.99, 1.0);
    }

    // ---------------------------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------------------------

    private static double[] Ascending(double start, int count)
    {
        double[] values = new double[count];
        for (int i = 0; i < count; i++)
            values[i] = start + i;

        return values;
    }

    private static double[] Lognormal(ref ulong state, int count, double sigma)
    {
        double[] values = new double[count];
        for (int i = 0; i < count; i++)
            values[i] = Math.Exp(sigma * Gaussian(ref state));

        return values;
    }

    /// <summary>
    /// Draws one standard normal value by the Box–Muller transform.
    /// </summary>
    /// <remarks>
    /// Hand-written rather than <see cref="Random"/> for the reason
    /// <c>RobustDispersionTests</c> gives: a seeded <see cref="Random"/> sequence is not guaranteed
    /// stable across runtime versions, and a simulation whose answer moves with the runtime is not
    /// an assertion.
    /// </remarks>
    private static double Gaussian(ref ulong state) =>
        Math.Sqrt(-2 * Math.Log(1 - Uniform(ref state))) *
        Math.Cos(2 * Math.PI * Uniform(ref state));

    /// <summary>
    /// Draws one value in [0,1) from a splitmix64 generator.
    /// </summary>
    private static double Uniform(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;

        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;

        return (z >> 11) * (1.0 / 9007199254740992.0);
    }
}
