/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Scoring;

namespace Xping.Cli.Tests.Report;

public sealed class WilsonIntervalTests
{
    [Theory]
    [InlineData(1, 1, 0.207)]
    [InlineData(2, 2, 0.342)]
    [InlineData(5, 5, 0.566)]
    [InlineData(20, 20, 0.839)]
    [InlineData(19, 20, 0.764)]
    [InlineData(10, 20, 0.299)]
    [InlineData(20, 40, 0.352)]
    [InlineData(3, 17, 0.062)]
    public void TheLowerBoundMatchesTheWilsonScoreInterval(int successes, int trials, double expected)
    {
        Assert.Equal(expected, WilsonInterval.LowerBound(successes, trials), 3);
    }

    [Fact]
    public void NoTrialsBoundToTheWholeRangeRatherThanToNaN()
    {
        // Having observed nothing, the proportion is somewhere in [0,1] and the interval says so.
        // An upper bound of 0 would assert that a behaviour never observed also cannot happen.
        Assert.Equal(0, WilsonInterval.LowerBound(0, 0));
        Assert.Equal(1, WilsonInterval.UpperBound(0, 0));

        // The difference bound guards its arms itself: with one arm empty there is no comparison to
        // make, and no difference is the only honest answer.
        Assert.Equal(0, WilsonInterval.DifferenceBoundNearestZero(0, 0, 3, 10));
        Assert.Equal(0, WilsonInterval.DifferenceBoundNearestZero(3, 10, 0, 0));
    }

    [Fact]
    public void NegativeTrialsBoundTheSameWayAsNoneRatherThanThrowing()
    {
        Assert.Equal(0, WilsonInterval.LowerBound(0, -1));
        Assert.Equal(1, WilsonInterval.UpperBound(0, -1));
    }

    [Fact]
    public void MoreSuccessesThanTrialsBoundsRatherThanReturningNaN()
    {
        // NaN survives Math.Clamp and every comparison the impact scorer makes, so a statistic that
        // could not be computed would rank the finding at zero without saying anything.
        Assert.Equal(1.0, WilsonInterval.UpperBound(7, 5));
        Assert.False(double.IsNaN(WilsonInterval.LowerBound(7, 5)));
        Assert.False(double.IsNaN(WilsonInterval.DifferenceBoundNearestZero(7, 5, 1, 5)));
    }

    [Fact]
    public void ABehaviourNeverObservedBoundsToZero()
    {
        Assert.Equal(0, WilsonInterval.LowerBound(0, 40));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 17)]
    [InlineData(19, 20)]
    [InlineData(400, 1000)]
    public void TheBoundNeverExceedsThePointEstimateAndTheIntervalNeverInverts(int successes, int trials)
    {
        double point = (double)successes / trials;
        double lower = WilsonInterval.LowerBound(successes, trials);
        double upper = WilsonInterval.UpperBound(successes, trials);

        Assert.True(lower <= point, $"{lower} <= {point}");
        Assert.True(point <= upper, $"{point} <= {upper}");
        Assert.InRange(lower, 0.0, 1.0);
        Assert.InRange(upper, 0.0, 1.0);
    }

    /// <summary>
    /// The property the whole change exists for: the same observed rate, measured more often, is a
    /// stronger claim.
    /// </summary>
    [Fact]
    public void TheBoundRisesWithTheSampleAtAFixedRate()
    {
        double[] bounds =
        [
            WilsonInterval.LowerBound(1, 2),
            WilsonInterval.LowerBound(5, 10),
            WilsonInterval.LowerBound(10, 20),
            WilsonInterval.LowerBound(20, 40),
            WilsonInterval.LowerBound(500, 1000)
        ];

        for (int i = 1; i < bounds.Length; i++)
            Assert.True(bounds[i] > bounds[i - 1], $"{bounds[i]} > {bounds[i - 1]}");
    }

    [Fact]
    public void TheBoundConvergesOnTheRateItBounds()
    {
        Assert.Equal(0.5, WilsonInterval.LowerBound(50_000, 100_000), 2);
    }

    [Fact]
    public void ADifferenceTheArmsCannotSupportBoundsToZero()
    {
        // Zero of five against two of five: an observed gap of 0.40 on the smallest split the
        // report allows, and an interval that still admits no difference at all.
        Assert.Equal(0, WilsonInterval.DifferenceBoundNearestZero(2, 5, 0, 5));
    }

    [Fact]
    public void ADifferenceTheArmsDoSupportBoundsAboveZero()
    {
        Assert.True(WilsonInterval.DifferenceBoundNearestZero(30, 40, 4, 40) > 0);
    }

    [Fact]
    public void TheDifferenceBoundIsAMagnitudeAndDoesNotDependOnArmOrder()
    {
        Assert.Equal(
            WilsonInterval.DifferenceBoundNearestZero(30, 40, 4, 40),
            WilsonInterval.DifferenceBoundNearestZero(4, 40, 30, 40),
            10);
    }

    [Fact]
    public void TheDifferenceBoundNeverExceedsTheObservedGap()
    {
        double observed = (30.0 / 40) - (4.0 / 40);

        Assert.True(WilsonInterval.DifferenceBoundNearestZero(30, 40, 4, 40) <= observed);
    }

    [Fact]
    public void TheDifferenceBoundRisesWithArmSizeAtFixedRates()
    {
        double[] bounds =
        [
            WilsonInterval.DifferenceBoundNearestZero(4, 5, 1, 5),
            WilsonInterval.DifferenceBoundNearestZero(8, 10, 2, 10),
            WilsonInterval.DifferenceBoundNearestZero(16, 20, 4, 20),
            WilsonInterval.DifferenceBoundNearestZero(80, 100, 20, 100)
        ];

        for (int i = 1; i < bounds.Length; i++)
            Assert.True(bounds[i] > bounds[i - 1], $"{bounds[i]} > {bounds[i - 1]}");
    }

    [Fact]
    public void TwoIdenticalArmsSupportNoDifference()
    {
        Assert.Equal(0, WilsonInterval.DifferenceBoundNearestZero(10, 20, 10, 20));
    }
}
