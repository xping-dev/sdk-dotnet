/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Scoring;

namespace Xping.Cli.Tests.Report;

public sealed class RobustDispersionTests
{
    // Draws per sample size in the simulation below. Large enough that the median of the estimate
    // is stable to about half a percent, small enough that the whole file runs in a second.
    private const int Draws = 40_000;

    // What a sample with a median of 2 and a median deviation of 1 reads before its correction is
    // applied: 1.4826 x 1 / 2. Dividing it out recovers the factor itself.
    private const double UncorrectedSpreadOfOneAroundTwo = 1.4826 / 2;

    // ---------------------------------------------------------------------------------------
    // The correction table
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2, 1.4137)]
    [InlineData(3, 1.4136)]
    [InlineData(4, 1.3786)]
    [InlineData(5, 1.1893)]
    [InlineData(6, 1.1805)]
    [InlineData(7, 1.1121)]
    [InlineData(8, 1.1222)]
    [InlineData(9, 1.0700)]
    [InlineData(10, 1.0778)]
    [InlineData(11, 1.0540)]
    [InlineData(12, 1.0610)]
    [InlineData(13, 1.0346)]
    [InlineData(14, 1.0437)]
    [InlineData(15, 1.0324)]
    [InlineData(16, 1.0382)]
    [InlineData(17, 1.0211)]
    [InlineData(18, 1.0281)]
    [InlineData(19, 1.0209)]
    [InlineData(20, 1.0262)]
    [InlineData(21, 1.0136)]
    [InlineData(22, 1.0192)]
    [InlineData(23, 1.0138)]
    [InlineData(24, 1.0173)]
    [InlineData(25, 1.0082)]
    [InlineData(26, 1.0141)]
    [InlineData(27, 1.0101)]
    [InlineData(28, 1.0123)]
    [InlineData(29, 1.0056)]
    [InlineData(30, 1.0101)]
    [InlineData(31, 1.0077)]
    [InlineData(32, 1.0097)]
    [InlineData(33, 1.0033)]
    [InlineData(34, 1.0069)]
    [InlineData(35, 1.0047)]
    [InlineData(36, 1.0074)]
    [InlineData(37, 1.0016)]
    [InlineData(38, 1.0051)]
    [InlineData(39, 1.0032)]
    [InlineData(40, 1.0046)]
    [InlineData(41, 1.0)]
    [InlineData(60, 1.0)]
    public void EveryCountReadsThroughTheFactorTheTableStatesForIt(int count, double factor)
    {
        // Every entry gets a row, including two past the table where no correction applies, so a
        // transposed digit anywhere in it fails and names the count it is in. The rows are the
        // table: a reader can set this column beside `Corrections` and compare them line for line.
        //
        // A sample with a median of exactly 2 and a median absolute deviation of exactly 1 isolates
        // the factor — the deviation estimate reads 1.4826 / 2 at every count and beats the
        // quartile estimate by a hair, so dividing the answer by it returns the factor and nothing
        // else. Exact arithmetic rather than a simulation, hence the tolerance.
        double recovered =
            RobustDispersion.Of(SpreadOfOneAroundTwo(count)) / UncorrectedSpreadOfOneAroundTwo;

        Assert.Equal(factor, recovered, 1e-9);
    }

    [Fact]
    public void TheFactorFallsAsTheSampleGrows()
    {
        // Not strictly, and the exceptions are the point: the sequence alternates slightly between
        // odd and even counts because which of the two estimates is larger depends on where the
        // quartiles land between readings. What must hold is the trend — a correction that grew
        // with the sample would be correcting the wrong way.
        double atFive = RobustDispersion.Of(SpreadOfOneAroundTwo(5));
        double atTwenty = RobustDispersion.Of(SpreadOfOneAroundTwo(20));
        double atSixty = RobustDispersion.Of(SpreadOfOneAroundTwo(60));

        Assert.True(atFive > atTwenty, $"{atFive} should exceed {atTwenty}");
        Assert.True(atTwenty > atSixty, $"{atTwenty} should exceed {atSixty}");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(18)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(25)]
    [InlineData(30)]
    [InlineData(40)]
    [InlineData(50)]
    public void TheEstimateExceedsTheTrueDispersionAboutHalfTheTimeAtEverySampleSize(int count)
    {
        // Where the theory above pins each factor, this one says why it is the number it is. It
        // re-runs the derivation: draw standard normals, and the median of the estimate should sit
        // on the truth. Uncorrected it does not — both estimates are badly biased low on a handful
        // of points — so a fixed threshold applied without this waves small samples through and
        // holds large ones to a stricter bar than the constant states.
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

        // A one percent band. Over two independent seeds at every count above, the widest deviation
        // from one is 0.0057, so this is about twice the simulation's own noise and no wider: a
        // correction factor wrong by more than a percent fails, and a correct one does not fail
        // intermittently. Tightening it to half a percent would put the assertion inside the noise
        // and make the test a coin toss.
        Assert.InRange(estimates[Draws / 2], 0.99, 1.01);
    }

    // ---------------------------------------------------------------------------------------
    // The two estimates
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void ATestWithTwoSpeedsIsNotCalledSteadyBecauseOneOfThemIsCommoner()
    {
        // The defect the quartile estimate exists to close. Six readings at 3.0 and four at 0.5 is
        // a test that runs six times slower more often than not — a cache hit against a miss, a
        // connection pooled or opened. The deviation estimate reads exactly zero on it, because the
        // majority mode is the median and the typical reading sits on top of it. Left at that, the
        // regression gate would take this for a perfectly steady baseline.
        double[] twoSpeeds = [3.0, 3.0, 3.0, 3.0, 3.0, 3.0, 0.5, 0.5, 0.5, 0.5];

        Assert.Equal(0.666, RobustDispersion.Of(twoSpeeds), 3);
    }

    [Theory]
    [InlineData(new[] { 1.0, 1.0, 1.0, 5.0, 5.0, 5.0, 5.0 }, 0.660)]
    [InlineData(new[] { 2.0, 2.0, 2.0, 2.0, 2.0, 10.0, 10.0, 10.0 }, 3.328)]
    [InlineData(new[] { 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 4.0, 4.0, 4.0, 4.0 }, 2.397)]
    public void EitherSplitOfATwoSpeedTestIsSeen(double[] values, double expected)
    {
        // Whichever mode holds the majority, and whichever end it sits at. The deviation estimate
        // reads exactly zero on every one of these.
        Assert.Equal(expected, RobustDispersion.Of(values), 3);
    }

    [Fact]
    public void TheTwoEstimatesAgreeOnASteadyTestSoTheLargerCostsNothing()
    {
        // At the normal both estimate the same standard deviation, which is why taking the larger
        // is close to free on the shape this usually sees and only speaks up on the shapes the
        // deviation estimate cannot see. Twenty evenly spaced readings: 1.4826 x 5 against
        // 9.5 / 1.349, a difference of five percent.
        double[] steady = [.. Enumerable.Range(0, 20).Select(i => 200.0 + i)];

        Assert.Equal(0.036, RobustDispersion.Of(steady), 3);
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
    public void AQuarterOfTheSampleHasToMoveBeforeTheAnswerCanBeMadeToSayAnything()
    {
        // The breakdown point stated as the guarantee it is. A quarter rather than a half, because
        // the quartile estimate is the weaker of the two against outright corruption: four of
        // twenty readings replaced by an arbitrarily large number leave the estimate where it
        // started, and the fifth takes it wherever the corruption wants.
        //
        // That is the price of seeing a test with two speeds, and it is the right way round. Five
        // runs in twenty at twenty seconds is a thing worth reporting, not a thing worth ignoring.
        double[] steady = [.. Enumerable.Range(0, 20).Select(i => 200.0 + i)];

        double[] four = [.. steady];
        for (int i = 0; i < 4; i++)
            four[i] = 20_000;

        double[] five = [.. four];
        five[4] = 20_000;

        Assert.Equal(0.036, RobustDispersion.Of(steady), 3);
        Assert.Equal(0.036, RobustDispersion.Of(four), 3);
        Assert.True(RobustDispersion.Of(five) > 17, $"{RobustDispersion.Of(five)}");
    }

    // ---------------------------------------------------------------------------------------
    // Shape
    // ---------------------------------------------------------------------------------------

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

    /// <summary>
    /// Builds <paramref name="count"/> readings with a median of exactly 2 and a median absolute
    /// deviation of exactly 1, so the answer is the correction factor and nothing else.
    /// </summary>
    private static double[] SpreadOfOneAroundTwo(int count)
    {
        var values = new List<double>(count);

        for (int i = 0; i < count / 2; i++)
            values.Add(1);

        if (count % 2 == 1)
            values.Add(2);

        for (int i = 0; i < count / 2; i++)
            values.Add(3);

        return [.. values];
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
