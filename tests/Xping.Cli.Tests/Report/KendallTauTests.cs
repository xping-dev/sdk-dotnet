/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Scoring;

namespace Xping.Cli.Tests.Report;

#pragma warning disable CA5394
public sealed class KendallTauTests
{
    [Fact]
    public void TheOnePassAgreesWithEnumeratingEveryPair()
    {
        // The single most valuable fact here. The implementation reads the correlation off a level
        // table with running totals, which is linear in the number of levels; the definition is
        // quadratic in the number of observations. They must not be allowed to drift apart, and heavy
        // ties on both axes are exactly where a running-total form goes wrong.
        var random = new Random(20260903);

        for (int trial = 0; trial < 400; trial++)
        {
            int count = 2 + random.Next(0, 40);
            List<TrendPoint> points = [];

            for (int i = 0; i < count; i++)
                points.Add(new TrendPoint(1 + random.Next(0, 5), random.Next(0, 2) == 0, i));

            Assert.Equal(BruteForce(points), KendallTau.TauB(points), 12);
        }
    }

    [Fact]
    public void PerfectSeparationIsExactlyOne()
    {
        // Not merely close to one: the tie correction exists so that a table this clean reads 1.00
        // despite every pass being tied with every other pass. This is what tau_a cannot do.
        Assert.Equal(1.0, KendallTau.TauB(Separated(9)));
        Assert.Equal(1.0, KendallTau.TauB(Separated(40)));
    }

    [Fact]
    public void TheOppositeSeparationIsExactlyMinusOne()
    {
        List<TrendPoint> reversed =
            [.. Separated(9).Select(p => new TrendPoint(p.Level, !p.Occurred, p.Cluster))];

        Assert.Equal(-1.0, KendallTau.TauB(reversed));
    }

    [Fact]
    public void TheTieCorrectionIsTheRatioBetweenHowTiedTheTwoAxesAre()
    {
        // A suite pinned at 8 with five serial runs, failing eleven times out of fifteen when
        // crowded and never when alone. The excess of concordant over discordant pairs is 55; the
        // outcome leaves 11 x 9 = 99 pairs untied and the level leaves 190 - 115 = 75. So Somers' D
        // is 55/99 and tau_b is that multiplied by the square root of 99/75 - the discount running
        // the other way, because this exposure is more tied than this outcome.
        List<TrendPoint> pinned = [];

        for (int cluster = 0; cluster < 15; cluster++)
            pinned.Add(new TrendPoint(8, cluster < 11, cluster));

        for (int cluster = 15; cluster < 20; cluster++)
            pinned.Add(new TrendPoint(1, false, cluster));

        double somers = 55 / 99.0;

        Assert.Equal(0.555556, somers, 6);
        Assert.Equal(1.148913, Math.Sqrt(99 / 75.0), 6);
        Assert.Equal(somers * Math.Sqrt(99 / 75.0), KendallTau.TauB(pinned), 12);
        Assert.Equal(0.638285, KendallTau.TauB(pinned), 6);
    }

    [Fact]
    public void ASpreadExposureReadsBelowItsOwnAssociation()
    {
        // The other side of the same discount, and the reason the published number carries a warning:
        // the same underlying behaviour on a suite spread over fourteen levels reads lower than on a
        // pinned one, because the level axis leaves far more pairs untied.
        List<TrendPoint> spread = [];

        for (int level = 1; level <= 14; level++)
        {
            for (int run = 0; run < 3; run++)
                spread.Add(new TrendPoint(level, level > 7, spread.Count));
        }

        // Perfectly separated at the halfway point, so Somers' D is 1.00 and the whole of the
        // shortfall is the tie correction: 441 untied outcome pairs against 819 untied level pairs.
        Assert.Equal(Math.Sqrt(441 / 819.0), KendallTau.TauB(spread), 12);
        Assert.Equal(0.733799, KendallTau.TauB(spread), 6);
    }

    [Fact]
    public void TheClusterAnObservationCameFromIsNotReadHere()
    {
        // Ranks are ranks. Whether two observations shared an occasion is the variance's business,
        // and reading it here would make the estimate depend on how the runs were bundled.
        List<TrendPoint> spread = Separated(6);
        List<TrendPoint> bundled = [.. spread.Select(p => new TrendPoint(p.Level, p.Occurred, 0))];

        Assert.Equal(KendallTau.TauB(spread), KendallTau.TauB(bundled));
    }

    [Fact]
    public void TheOrderObservationsArriveInDoesNotChangeTheAnswer()
    {
        List<TrendPoint> points = Separated(7);
        List<TrendPoint> shuffled = [.. points.OrderByDescending(p => p.Level)];

        Assert.Equal(KendallTau.TauB(points), KendallTau.TauB(shuffled));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnOutcomeWithNoVariationInItClaimsNothing(bool occurred) =>
        Assert.Equal(0, KendallTau.TauB(
            [.. Enumerable.Range(0, 10).Select(i => new TrendPoint(i, occurred, i))]));

    [Fact]
    public void OneLevelClaimsNothing() =>
        Assert.Equal(0, KendallTau.TauB(
            [.. Enumerable.Range(0, 10).Select(i => new TrendPoint(8, i < 5, i))]));

    [Fact]
    public void AnEmptyOrSingletonWindowClaimsNothing()
    {
        Assert.Equal(0, KendallTau.TauB([]));
        Assert.Equal(0, KendallTau.TauB([new TrendPoint(1, true, 0)]));
    }

    [Fact]
    public void ANullWindowIsAProgrammingError() =>
        Assert.Throws<ArgumentNullException>(() => KendallTau.TauB(null!));

    /// <summary>
    /// τ_b by the definition: every pair, classified.
    /// </summary>
    private static double BruteForce(List<TrendPoint> points)
    {
        long concordant = 0;
        long discordant = 0;
        long tiedLevelOnly = 0;
        long tiedOutcomeOnly = 0;

        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                bool sameLevel = points[i].Level == points[j].Level;
                bool sameOutcome = points[i].Occurred == points[j].Occurred;

                if (sameLevel && sameOutcome)
                    continue;

                if (sameLevel)
                    tiedLevelOnly++;
                else if (sameOutcome)
                    tiedOutcomeOnly++;
                else if ((points[i].Level < points[j].Level) == (!points[i].Occurred && points[j].Occurred))
                    concordant++;
                else
                    discordant++;
            }
        }

        long pairs = concordant + discordant;
        double denominator = Math.Sqrt(
            (double)(pairs + tiedOutcomeOnly) * (pairs + tiedLevelOnly));

        return denominator == 0 ? 0 : (concordant - discordant) / denominator;
    }

    private static List<TrendPoint> Separated(int aSide)
    {
        List<TrendPoint> points = [];

        for (int cluster = 0; cluster < aSide; cluster++)
            points.Add(new TrendPoint(2, false, cluster));

        for (int cluster = aSide; cluster < 2 * aSide; cluster++)
            points.Add(new TrendPoint(8, true, cluster));

        return points;
    }
}
