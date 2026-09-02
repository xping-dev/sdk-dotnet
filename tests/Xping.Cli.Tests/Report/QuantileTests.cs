/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Scoring;

namespace Xping.Cli.Tests.Report;

public sealed class QuantileTests
{
    // ---------------------------------------------------------------------------------------
    // The two definitions, side by side
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void TheInterpolatedMedianOfTwoReadingsSitsBetweenThem()
    {
        // The defect this class exists to fix: nearest rank answers 10 here, and did so for every
        // even-sized sample of durations the report published.
        Assert.Equal(15, Quantile.Interpolated([10, 20], 0.50));
    }

    [Fact]
    public void TheNearestRankMedianOfTwoReadingsIsTheLowerOfThem()
    {
        // Relied on by RetryProvider.TypicalAttempts: from two runs, one at a single attempt and one
        // at three, the typical run needed one. A single deep run cannot produce a deepening finding
        // on its own, and this is the property that guarantees it.
        Assert.Equal(1, Quantile.NearestRank([1, 3], 0.50));
    }

    // ---------------------------------------------------------------------------------------
    // Interpolated
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2, 15)]      // 10 20                -> between the two
    [InlineData(3, 20)]      // 10 20 30             -> the middle reading
    [InlineData(4, 25)]      // 10 20 30 40          -> between the two middles
    [InlineData(5, 30)]      // 10 20 30 40 50       -> the middle reading
    [InlineData(6, 35)]      // 10 20 30 40 50 60    -> between the two middles
    public void TheInterpolatedMedianHasNoPreferenceBetweenOddAndEvenCounts(int count, double expected)
    {
        double[] sorted = [.. Enumerable.Range(1, count).Select(i => (double)(i * 10))];

        Assert.Equal(expected, Quantile.Interpolated(sorted, 0.50));
    }

    [Fact]
    public void TheInterpolatedQuantileSpansTheWholeSampleFromZeroToOne()
    {
        double[] sorted = [10, 20, 30, 40];

        Assert.Equal(10, Quantile.Interpolated(sorted, 0.00));
        Assert.Equal(40, Quantile.Interpolated(sorted, 1.00));
    }

    [Fact]
    public void TheInterpolatedQuartilesOfAFourReadingSampleFallBetweenReadings()
    {
        // Position (n - 1) * q, so 0.75 and 2.25 of 0..3. This is the property RobustDispersion
        // needs: nearest-rank quartiles jump a whole reading as the count crosses a multiple of
        // four, which put a cycle into its correction factors.
        double[] sorted = [10, 20, 30, 40];

        Assert.Equal(17.5, Quantile.Interpolated(sorted, 0.25));
        Assert.Equal(32.5, Quantile.Interpolated(sorted, 0.75));
    }

    [Fact]
    public void TheInterpolatedQuantileOfASingleReadingIsThatReading()
    {
        Assert.Equal(42, Quantile.Interpolated([42], 0.50));
        Assert.Equal(42, Quantile.Interpolated([42], 0.95));
    }

    [Fact]
    public void TheInterpolatedQuantileOfNothingIsZero()
    {
        // Not an exception: the duration provider asks for the median of a slice that may
        // legitimately hold no normalisable reading, and reads zero as "decline the gate".
        Assert.Equal(0, Quantile.Interpolated([], 0.50));
    }

    [Fact]
    public void TheInterpolatedQuantileIsReproducibleReadingForReading()
    {
        // The property nearest rank used to be defended on. Interpolation performs the same IEEE
        // operations in the same order on the same sorted readings, so it answers to the bit.
        double[] sorted = [1.1, 2.7, 3.3, 5.9, 8.2, 13.4];

        Assert.Equal(Quantile.Interpolated(sorted, 0.37), Quantile.Interpolated(sorted, 0.37));
    }

    // ---------------------------------------------------------------------------------------
    // Nearest rank
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2, 1)]       // 1 2             -> index 0
    [InlineData(3, 2)]       // 1 2 3           -> index 1
    [InlineData(4, 2)]       // 1 2 3 4         -> index 1
    [InlineData(5, 3)]       // 1 2 3 4 5       -> index 2
    [InlineData(6, 3)]       // 1 2 3 4 5 6     -> index 2
    public void TheNearestRankMedianTakesTheLowerMiddleAtEveryEvenCount(int count, int expected)
    {
        int[] sorted = [.. Enumerable.Range(1, count)];

        Assert.Equal(expected, Quantile.NearestRank(sorted, 0.50));
    }

    [Fact]
    public void TheNearestRankQuantileAlwaysReturnsOneOfTheReadings()
    {
        int[] sorted = [1, 2, 4, 8];

        foreach (double quantile in new[] { 0.00, 0.10, 0.25, 0.50, 0.75, 0.90, 1.00 })
            Assert.Contains(Quantile.NearestRank(sorted, quantile), sorted);
    }

    [Fact]
    public void TheNearestRankQuantileClampsBothEnds()
    {
        int[] sorted = [1, 2, 3];

        Assert.Equal(1, Quantile.NearestRank(sorted, 0.00));
        Assert.Equal(3, Quantile.NearestRank(sorted, 1.00));
    }

    [Fact]
    public void TheNearestRankQuantileOfNothingThrowsRatherThanInventingAReading()
    {
        // There is no value of T that means "none", so the caller has to have gated the count.
        Assert.Throws<ArgumentOutOfRangeException>(() => Quantile.NearestRank<int>([], 0.50));
    }
}
