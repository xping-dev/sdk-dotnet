/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Scoring;

namespace Xping.Cli.Tests.Report;

public sealed class NormalTailTests
{
    [Theory]

    // Reference values, to the six significant digits a double carries here.
    [InlineData(0.0, 1.0)]
    [InlineData(1.0, 3.173105e-01)]
    [InlineData(1.96, 4.999579e-02)]
    [InlineData(3.0, 2.699796e-03)]
    [InlineData(6.0, 1.973175e-09)]
    [InlineData(8.0, 1.244192e-15)]
    [InlineData(10.0, 1.523971e-23)]
    [InlineData(20.0, 5.507248e-89)]
    public void TheTailIsAccurateToSixSignificantDigits(double z, double expected)
    {
        double actual = NormalTail.TwoSidedPValue(z);

        Assert.Equal(expected, actual, expected * 1e-6);
    }

    [Fact]
    public void TheFarTailIsNotCollapsedOntoZero()
    {
        // The whole reason this is erfc and not one minus a cumulative. `2 * (1 - Phi(z))` has lost
        // half its digits by six and returns exactly zero from about 8.3 outwards, so a report built
        // on it would publish a claim of certainty on an ordinary window.
        Assert.True(NormalTail.TwoSidedPValue(8.5) > 0);
        Assert.True(NormalTail.TwoSidedPValue(20) > 0);
        Assert.True(NormalTail.TwoSidedPValue(37) > 0);
    }

    [Fact]
    public void TheArithmeticStopsWhereADoubleDoes()
    {
        // Past about 38 the true probability is below the smallest positive double. Nothing a store
        // holds reaches it, and underflowing there is honest rather than wrong.
        Assert.Equal(0, NormalTail.TwoSidedPValue(40));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(1.96)]
    [InlineData(4.0)]
    [InlineData(12.0)]
    public void TheSignOfTheDeviateIsIrrelevant(double z) =>
        Assert.Equal(NormalTail.TwoSidedPValue(z), NormalTail.TwoSidedPValue(-z));

    [Fact]
    public void TheTailFallsAwayMonotonically()
    {
        double previous = double.MaxValue;

        for (double z = 0; z < 12; z += 0.05)
        {
            double tail = NormalTail.TwoSidedPValue(z);
            Assert.True(tail <= previous, $"{tail} <= {previous} at z = {z}");
            previous = tail;
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ADeviateThatIsNotANumberClaimsNothing(double z) =>
        Assert.Equal(1.0, NormalTail.TwoSidedPValue(z));

    [Fact]
    public void EveryAnswerIsAProbability()
    {
        for (double z = -8; z <= 8; z += 0.13)
        {
            double tail = NormalTail.TwoSidedPValue(z);
            Assert.InRange(tail, 0.0, 1.0);
        }
    }
}
