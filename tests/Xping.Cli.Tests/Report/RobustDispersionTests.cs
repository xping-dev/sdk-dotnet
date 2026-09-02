/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Scoring;

namespace Xping.Cli.Tests.Report;

public sealed class RobustDispersionTests
{
    // Draws per sample size in the simulations below. Large enough that the median of the estimate
    // is stable to about half a percent, small enough that the whole file runs in a second.
    private const int Draws = 40_000;

    [Theory]
    [InlineData(new[] { 1.0, 3.0 }, 1.048)]
    [InlineData(new[] { 1.0, 2.0, 3.0 }, 1.388)]
    [InlineData(new[] { 1.0, 1.0, 3.0, 3.0 }, 1.104)]
    [InlineData(new[] { 1.0, 1.0, 2.0, 3.0, 3.0 }, 0.993)]
    [InlineData(new[] { 1.0, 1.0, 1.0, 3.0, 3.0, 3.0 }, 0.936)]
    [InlineData(new[] { 1.0, 1.0, 1.0, 2.0, 3.0, 3.0, 3.0 }, 0.895)]
    [InlineData(new[] { 1.0, 1.0, 1.0, 1.0, 3.0, 3.0, 3.0, 3.0 }, 0.873)]
    public void TheSameSpreadReadsDifferentlyAtDifferentSampleSizes(double[] values, double expected)
    {
        // Every sample here has a median of 2 and a median deviation of 1, so the only thing that
        // moves between the rows is the finite-sample correction — 1.4826 x 0.5 is 0.741, and each
        // expected value is that times the factor for its count. Three carries the largest of them
        // because at an odd count the deviation set contains the middle reading's distance from
        // itself, and at three that zero is one of only two values below the median deviation.
        Assert.Equal(expected, RobustDispersion.Of(values), 3);
    }

    [Fact]
    public void TheAnswerDoesNotDependOnTheOrderTheReadingsArrivedIn()
    {
        double[] ascending = [1, 2, 3, 4, 5, 6, 7];
        double[] shuffled = [4, 7, 1, 6, 2, 5, 3];

        Assert.Equal(RobustDispersion.Of(ascending), RobustDispersion.Of(shuffled));
    }

    [Fact]
    public void TheCallerSSampleIsNotReordered()
    {
        // Providers hand this their own lists, and one of them is sorted ascending because every
        // percentile it publishes reads an index into it.
        double[] values = [4, 7, 1, 6, 2, 5, 3];

        RobustDispersion.Of(values);

        Assert.Equal([4, 7, 1, 6, 2, 5, 3], values);
    }

    // ---------------------------------------------------------------------------------------
    // The zero contract
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ASingleReadingHasNoSpreadRatherThanAnUnknownOne()
    {
        // Zero falls on the reporting side of both gates: it passes a stability gate and fails an
        // instability gate, so absent data never produces a finding on its own.
        Assert.Equal(0, RobustDispersion.Of([]));
        Assert.Equal(0, RobustDispersion.Of([7.0]));
    }

    [Fact]
    public void AMedianOfZeroReturnsZeroRatherThanAnInfinity()
    {
        // The relative measure has no meaning without a positive middle to be relative to, and the
        // division would produce an infinity that then compares greater than every threshold.
        Assert.Equal(0, RobustDispersion.Of([0, 0, 0, 5, 9]));
        Assert.Equal(0, RobustDispersion.Of([-3, -1, 0, 1, 3]));
    }

    [Fact]
    public void ReadingsThatNeverMoveHaveNoSpread()
    {
        Assert.Equal(0, RobustDispersion.Of([200, 200, 200, 200, 200]));
    }

    // ---------------------------------------------------------------------------------------
    // Robustness
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void OneOutlierAmongTwentyDoesNotMoveTheAnswer()
    {
        // The property a coefficient of variation does not have. Nineteen readings say what the
        // measurement is; the twentieth is a GC pause, and it moves the mean and the sum of squares
        // far more than it moves anything this measure reads.
        double[] steady = [.. Enumerable.Range(0, 20).Select(i => 200.0 + i)];
        double[] withOutlier = [.. steady];
        withOutlier[19] = 20_000;

        Assert.Equal(RobustDispersion.Of(steady), RobustDispersion.Of(withOutlier), 6);
    }

    [Fact]
    public void HalfTheSampleHasToMoveBeforeTheAnswerCanBeMadeToSayAnything()
    {
        // The 50% breakdown point stated as the guarantee it is. Nine of twenty readings replaced
        // by an arbitrarily large number leave the estimate near where it started; the tenth takes
        // it wherever the corruption wants, because at that point the corruption is the sample.
        double[] steady = [.. Enumerable.Range(0, 20).Select(i => 200.0 + i)];

        double[] nine = [.. steady];
        for (int i = 0; i < 9; i++)
            nine[i] = 20_000;

        double[] ten = [.. nine];
        ten[9] = 20_000;

        Assert.Equal(0.038, RobustDispersion.Of(steady), 3);
        Assert.Equal(0.065, RobustDispersion.Of(nine), 3);
        Assert.Equal(1.538, RobustDispersion.Of(ten), 3);
    }

    // ---------------------------------------------------------------------------------------
    // The finite-sample correction
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(17)]
    [InlineData(20)]
    [InlineData(30)]
    [InlineData(50)]
    public void TheEstimateExceedsTheTrueDispersionAboutHalfTheTimeAtEverySampleSize(int count)
    {
        // This is what the correction table is for, and the whole reason this is not four lines
        // inside the duration provider. An uncorrected median absolute deviation read off five
        // points sits a quarter under the value it converges to, so a fixed threshold applied to it
        // waves small samples through and holds large ones to a stricter bar than the constant
        // states. Corrected, the same threshold means the same thing at five readings as at fifty.
        //
        // Standard normals shifted to a mean of 100, so the median is safely positive and the
        // dispersion of the sample is 0.01 times its standard deviation.
        ulong state = 20260902UL + (ulong)count;
        var estimates = new List<double>(Draws);

        for (int draw = 0; draw < Draws; draw++)
        {
            var sample = new double[count];
            for (int i = 0; i < count; i++)
                sample[i] = 100 + Gaussian(ref state);

            estimates.Add(RobustDispersion.Of(sample) * 100);
        }

        estimates.Sort();

        Assert.Equal(1.0, estimates[Draws / 2], 1);
    }

    /// <summary>
    /// Draws one standard normal by the Box-Muller transform.
    /// </summary>
    /// <remarks>
    /// On a generator written out here rather than <see cref="Random"/>, whose seeded sequence is
    /// not promised to be the same between runtime versions. A test that derives a constant by
    /// simulation has to derive the same one on every machine that runs it, or it is a coin toss
    /// wearing an assertion.
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

        // The top 53 bits, which is the mantissa a double can hold without rounding.
        return (z >> 11) * (1.0 / 9007199254740992.0);
    }
}
