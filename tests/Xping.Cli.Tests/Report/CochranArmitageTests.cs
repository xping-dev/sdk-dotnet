/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Scoring;

namespace Xping.Cli.Tests.Report;

public sealed class CochranArmitageTests
{
    // ---------------------------------------------------------------------------------------
    // Closed forms
    // ---------------------------------------------------------------------------------------

    [Theory]

    // G clusters, each one pass at level 1 and one failure at level 2. Every cluster contributes the
    // same 0.5 to the statistic, so T = G/2, the sandwich variance is G^2/(4(G-1)), the levels are one
    // apart and the half-step comes off the top: Z = (G-1)^1.5 / G.
    [InlineData(5, 1.6)]
    [InlineData(10, 2.7)]
    [InlineData(20, 4.14095399636)]
    [InlineData(17, 3.76470588235)]
    public void IdenticalClustersStandardiseToAClosedForm(int clusters, double expected)
    {
        List<TrendPoint> points = [];

        for (int cluster = 0; cluster < clusters; cluster++)
        {
            points.Add(new TrendPoint(1, false, cluster));
            points.Add(new TrendPoint(2, true, cluster));
        }

        Assert.Equal(expected, CochranArmitage.Of(points).Z, 9);
    }

    [Theory]

    // Perfectly separated windows: n runs clean at concurrency 2 against n failing at 8, one
    // execution each. These are the shapes the provider's own fixtures are built from, and four a
    // side is the smallest window that reaches the conventional level at all.
    [InlineData(4, 1.9843134833, 0.0472209040036)]
    [InlineData(5, 2.4, 0.0163950718492)]
    [InlineData(8, 3.38886042793, 0.000701837242658)]
    public void APerfectlySeparatedWindowIsStandardisedAndReferred(
        int aSide, double z, double probability)
    {
        TrendStatistic statistic = CochranArmitage.Of(Separated(aSide));

        Assert.Equal(z, statistic.Z, 9);
        Assert.Equal(probability, statistic.PValue, 9);
    }

    [Fact]
    public void TheTrendIsSignedTowardsTheHigherLevel()
    {
        List<TrendPoint> rising = Separated(5);
        List<TrendPoint> falling =
            [.. rising.Select(p => new TrendPoint(p.Level, !p.Occurred, p.Cluster))];

        Assert.True(CochranArmitage.Of(rising).Z > 0);
        Assert.True(CochranArmitage.Of(falling).Z < 0);
        Assert.Equal(CochranArmitage.Of(rising).PValue, CochranArmitage.Of(falling).PValue);
    }

    // ---------------------------------------------------------------------------------------
    // What the clustered variance buys
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void RepeatingEveryObservationWithinItsClusterBuysAlmostNothing()
    {
        List<TrendPoint> once = Separated(5);
        List<TrendPoint> twice = [.. once, .. once];

        // Both the statistic and the sandwich variance grow with the repetition — the first by a
        // factor of two and the second by four — so the ratio between them does not move at all. What
        // moves is the continuity correction, which is a fixed half-step and is genuinely a smaller
        // share of the larger statistic. A variance over observations would have multiplied the
        // evidence by the square root of two, to 3.39.
        Assert.Equal(2.4, CochranArmitage.Of(once).Z, 6);
        Assert.Equal(2.7, CochranArmitage.Of(twice).Z, 6);
    }

    [Fact]
    public void OneClusterCannotCarryATrendHoweverManyObservationsItHolds()
    {
        // #180's shape, in the abstract: one occasion supplies every occurrence, out of a burst of
        // repeats, while the occasions around it supply only the breadth. Counted as independent
        // observations this is four sigma.
        List<TrendPoint> points = [];

        for (int cluster = 0; cluster < 10; cluster++)
        {
            points.Add(new TrendPoint(2, false, cluster));
            points.Add(new TrendPoint(2, false, cluster));
        }

        for (int repeat = 0; repeat < 8; repeat++)
            points.Add(new TrendPoint(8, true, 0));

        for (int cluster = 1; cluster <= 5; cluster++)
            points.Add(new TrendPoint(8, false, cluster));

        TrendStatistic statistic = CochranArmitage.Of(points);

        Assert.Equal(1.06208577647, statistic.Z, 9);
        Assert.Equal(0.288196746602, statistic.PValue, 9);
    }

    [Fact]
    public void ALoneClusterAtOneEndOfTheRangeIsNotADoseResponse()
    {
        // One quiet occasion against any number of loud ones is a perfect rank correlation and no
        // evidence at all: the lone cluster carries the whole statistic, and the sandwich charges it
        // for that. This is what makes a per-level session gate unnecessary.
        for (int loud = 4; loud <= 12; loud++)
        {
            List<TrendPoint> points = [new TrendPoint(1, false, 0)];

            for (int cluster = 1; cluster <= loud; cluster++)
                points.Add(new TrendPoint(8, true, cluster));

            Assert.True(CochranArmitage.Of(points).PValue > 0.5, $"{loud} loud");
        }
    }

    // ---------------------------------------------------------------------------------------
    // The continuity correction
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void MultiplyingEveryLevelLeavesTheStatisticWhereItWas()
    {
        // The half-step is the greatest common divisor of the gaps between levels, so it scales with
        // them exactly as the statistic and its standard error do. Levels of 1 and 3 are the same
        // measurement as levels of 2 and 6, and reporting them differently would make the finding a
        // property of the units the scheduler happened to count in.
        List<TrendPoint> unit = Separated(6, 1, 3);

        foreach (int factor in new[] { 2, 3, 7, 11 })
        {
            List<TrendPoint> scaled = Separated(6, factor, 3 * factor);

            Assert.Equal(CochranArmitage.Of(unit).Z, CochranArmitage.Of(scaled).Z, 9);
        }
    }

    [Fact]
    public void AStatisticInsideItsOwnHalfStepClaimsNothing()
    {
        // Two failures in five crowded runs against one in five quiet ones. The levels are six apart,
        // so the statistic cannot move by less than six and this one has not moved by three.
        List<TrendPoint> points = [];

        for (int cluster = 0; cluster < 5; cluster++)
            points.Add(new TrendPoint(2, cluster == 0, cluster));

        for (int cluster = 5; cluster < 10; cluster++)
            points.Add(new TrendPoint(8, cluster < 7, cluster));

        Assert.Equal(0, CochranArmitage.Of(points).Z);
        Assert.Equal(1.0, CochranArmitage.Of(points).PValue);
    }

    // ---------------------------------------------------------------------------------------
    // Degenerate inputs claim nothing rather than throwing
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AnEmptyOrSingletonWindowClaimsNothing()
    {
        AssertNothing([]);
        AssertNothing([new TrendPoint(1, true, 0)]);
    }

    [Fact]
    public void OneLevelClaimsNothing() =>
        AssertNothing([.. Enumerable.Range(0, 10).Select(c => new TrendPoint(8, c < 5, c))]);

    [Fact]
    public void AnOutcomeThatAlwaysHappenedOrNeverDidClaimsNothing()
    {
        AssertNothing([.. Enumerable.Range(0, 10).Select(c => new TrendPoint(c, true, c))]);
        AssertNothing([.. Enumerable.Range(0, 10).Select(c => new TrendPoint(c, false, c))]);
    }

    [Fact]
    public void OneClusterClaimsNothing() =>
        AssertNothing([.. Enumerable.Range(0, 10).Select(i => new TrendPoint(i, i < 5, 0))]);

    [Fact]
    public void TheOrderObservationsArriveInDoesNotChangeTheAnswer()
    {
        List<TrendPoint> points = Separated(7);
        List<TrendPoint> shuffled = [.. points.OrderBy(p => p.Level).ThenByDescending(p => p.Cluster)];

        Assert.Equal(CochranArmitage.Of(points).Z, CochranArmitage.Of(shuffled).Z);
    }

    [Fact]
    public void ANullWindowIsAProgrammingError() =>
        Assert.Throws<ArgumentNullException>(() => CochranArmitage.Of(null!));

    private static void AssertNothing(List<TrendPoint> points)
    {
        TrendStatistic statistic = CochranArmitage.Of(points);

        Assert.Equal(0, statistic.Z);
        Assert.Equal(1.0, statistic.PValue);
    }

    /// <summary>
    /// Builds a perfectly separated window: <paramref name="aSide"/> clean clusters at the low level
    /// and as many failing clusters at the high one.
    /// </summary>
    private static List<TrendPoint> Separated(int aSide, int low = 2, int high = 8)
    {
        List<TrendPoint> points = [];

        for (int cluster = 0; cluster < aSide; cluster++)
            points.Add(new TrendPoint(low, false, cluster));

        for (int cluster = aSide; cluster < 2 * aSide; cluster++)
            points.Add(new TrendPoint(high, true, cluster));

        return points;
    }
}
