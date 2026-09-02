/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Scoring;

namespace Xping.Cli.Tests.Report;

public sealed class HodgesLehmannTests
{
    // Draws per arm shape in the coverage simulation below.
    private const int Draws = 5_000;

    // ---------------------------------------------------------------------------------------
    // The estimate
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TwoArmsWithNoSpreadReadTheRatioBetweenThem()
    {
        // Every one of the twenty-one pairs is 8 over 2, so nothing is being averaged and the
        // answer has to be exactly four. It is the case the duration provider's own fixtures take,
        // and a log-and-exponentiate round trip reads 3.999999999999999 for it.
        RatioEstimate estimate = HodgesLehmann.Of([2, 2, 2, 2, 2, 2, 2], [8, 8, 8]);

        Assert.Equal(4.0, estimate.Ratio);
        Assert.Equal(4.0, estimate.Low);
        Assert.Equal(4.0, estimate.High);
    }

    [Fact]
    public void TheEstimateIsTheMedianOfEveryPairRatherThanARatioOfTwoMedians()
    {
        // Baseline 1, 1, 1, 1, 100 and a recent arm at 2. The quotient of the two medians is 2;
        // the median of the ten pairwise ratios is also 2, because eight of them are 2 and two are
        // 0.02 — but move one baseline reading and the two answers part company. This fixture pins
        // the pairwise definition rather than the quotient.
        Assert.Equal(2.0, HodgesLehmann.Of([1, 1, 1, 1, 100], [2, 2]).Ratio);
    }

    [Fact]
    public void AnEvenNumberOfPairsTakesTheGeometricMidpoint()
    {
        // Three baseline readings against two: six pairs, sorted 0.5, 2, 2, 8, 8, 32, so the two
        // central ones are 2 and 8. The midpoint between twice as slow and eight times as slow is
        // four times, not five — these are ratios, and the arithmetic mean of two of them is not a
        // ratio of anything.
        Assert.Equal(4.0, HodgesLehmann.Of([1, 4, 16], [8, 32]).Ratio, 12);
    }

    [Fact]
    public void TheEstimateIsTheExponentialOfTheHodgesLehmannShiftOfTheLogarithms()
    {
        // The identity that lets the whole comparison stay on the ratio scale: a median only
        // depends on ordering, and the logarithm preserves it. Checked against the definition
        // spelled out the long way.
        double[] baseline = [1.2, 0.8, 3.4, 2.0, 0.9];
        double[] current = [4.1, 7.7, 6.2];

        var differences = new List<double>();
        foreach (double c in current)
        {
            foreach (double b in baseline)
                differences.Add(Math.Log(c) - Math.Log(b));
        }

        differences.Sort();

        Assert.Equal(
            Math.Exp(differences[differences.Count / 2]),
            HodgesLehmann.Of(baseline, current).Ratio,
            10);
    }

    [Fact]
    public void NoChangeAtAllReadsOne()
    {
        Assert.Equal(1.0, HodgesLehmann.Of([3, 3, 3, 3, 3], [3, 3, 3]).Ratio);
    }

    // ---------------------------------------------------------------------------------------
    // The interval
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(5, 3, 1)]       // the full min-to-max range, 96.4% coverage
    [InlineData(6, 3, 2)]       // 95.2%
    [InlineData(7, 3, 2)]       // 96.7%
    [InlineData(8, 3, 3)]       // 95.2%
    [InlineData(10, 3, 4)]      // 95.1%
    [InlineData(17, 3, 7)]      // 95.9%
    public void TheIntervalIsTheExactRankSumOrderStatisticCountedFromEachEnd(
        int baselineCount, int currentCount, int rank)
    {
        // Powers of two on one side and powers of three on the other, so that every pairwise ratio
        // is 1000 x 3^j / 2^i and no two of them can coincide — unique factorisation guarantees it.
        // Powers of one base on both sides would not: 1024/1 and 2048/2 are the same number, and
        // adjacent order statistics that are equal let an off-by-one rank pass this test.
        double[] baseline = Powers(1, 2, baselineCount);
        double[] current = Powers(1000, 3, currentCount);

        double[] pairs = Pairs(baseline, current);
        Assert.Equal(pairs.Length, pairs.Distinct().Count());

        RatioEstimate estimate = HodgesLehmann.Of(baseline, current);

        Assert.Equal(pairs[rank - 1], estimate.Low);
        Assert.Equal(pairs[pairs.Length - rank], estimate.High);
    }

    [Fact]
    public void ArmsTooThinForTheApproximationStillProduceAnInterval()
    {
        // Five baseline readings against three. The normal approximation asks for an order
        // statistic below the first here and yields no interval at all; the exact rule gives the
        // extremes of the pairwise ratios, at 96.4% coverage. An interval that spans everything
        // observed is a weak statement, which is the point — it is the true one at these sizes.
        double[] baseline = [1, 2, 4, 8, 16];
        double[] current = [1000, 3000, 9000];

        RatioEstimate estimate = HodgesLehmann.Of(baseline, current);

        Assert.Equal(1000.0 / 16, estimate.Low);
        Assert.Equal(9000.0 / 1, estimate.High);
    }

    [Fact]
    public void TheIntervalAlwaysContainsTheEstimate()
    {
        double[] baseline = [1.2, 0.8, 3.4, 2.0, 0.9, 1.1, 5.0];
        double[] current = [4.1, 7.7, 6.2];

        RatioEstimate estimate = HodgesLehmann.Of(baseline, current);

        Assert.True(estimate.Low <= estimate.Ratio);
        Assert.True(estimate.Ratio <= estimate.High);
    }

    [Fact]
    public void AWiderBaselineWidensTheInterval()
    {
        // Two windows estimating the same slowdown from different amounts of agreement. The
        // ranking a report is sorted by reads the lower bound, so this difference is the whole
        // reason the interval is computed.
        // Both baselines sit at 2 in the middle and both recent arms are 8, so both estimate a
        // fourfold slowdown; one baseline held there and the other ranged from 1 to 4.
        RatioEstimate steady = HodgesLehmann.Of([2, 2, 2, 2, 2, 2, 2], [8, 8, 8]);
        RatioEstimate spread = HodgesLehmann.Of([1, 1, 1, 2, 4, 4, 4], [8, 8, 8]);

        Assert.Equal(4.0, steady.Ratio);
        Assert.Equal(4.0, spread.Ratio);

        Assert.Equal(4.0, steady.Low);
        Assert.Equal(2.0, spread.Low);
        Assert.Equal(8.0, spread.High);
    }

    // ---------------------------------------------------------------------------------------
    // Degenerate input
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AnEmptyArmClaimsNothingRatherThanThrowing()
    {
        Assert.Equal(new RatioEstimate(1, 1, 1), HodgesLehmann.Of([], [1, 2, 3]));
        Assert.Equal(new RatioEstimate(1, 1, 1), HodgesLehmann.Of([1, 2, 3], []));
    }

    [Fact]
    public void TheCallerSSamplesAreNotReordered()
    {
        double[] baseline = [5, 1, 3, 2, 4];
        double[] current = [9, 7, 8];

        HodgesLehmann.Of(baseline, current);

        Assert.Equal([5, 1, 3, 2, 4], baseline);
        Assert.Equal([9, 7, 8], current);
    }

    // ---------------------------------------------------------------------------------------
    // What the interval is worth
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(17)]
    public void TheIntervalCoversAPlantedShiftAtLeastNineteenTimesInTwenty(int baselineCount)
    {
        // The claim the number on the report makes. A planted doubling, lognormal noise at a
        // dispersion of 0.35, and the interval has to contain the truth at its stated rate — a
        // location-shift interval on a location shift, which is the model it assumes.
        ulong state = 20260902UL + (ulong)baselineCount;

        double sigma = Math.Sqrt(Math.Log(1 + (0.35 * 0.35)));
        int covered = 0;

        for (int draw = 0; draw < Draws; draw++)
        {
            double[] baseline = Lognormal(ref state, baselineCount, sigma);
            double[] current = Lognormal(ref state, 3, sigma);

            for (int i = 0; i < current.Length; i++)
                current[i] *= 2;

            RatioEstimate estimate = HodgesLehmann.Of(baseline, current);
            if (estimate.Low <= 2 && 2 <= estimate.High)
                covered++;
        }

        Assert.InRange((double)covered / Draws, 0.95, 1.0);
    }

    // ---------------------------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------------------------

    private static double[] Powers(double start, double factor, int count)
    {
        double[] values = new double[count];
        for (int i = 0; i < count; i++)
            values[i] = start * Math.Pow(factor, i);

        return values;
    }

    /// <summary>
    /// Every pairwise ratio, ascending — a second implementation, so a test never asks the code
    /// under test where its own bounds are.
    /// </summary>
    private static double[] Pairs(double[] baseline, double[] current)
    {
        var pairs = new List<double>();
        foreach (double c in current)
        {
            foreach (double b in baseline)
                pairs.Add(c / b);
        }

        pairs.Sort();

        return [.. pairs];
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
