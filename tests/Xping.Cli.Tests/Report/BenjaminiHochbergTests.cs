/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Scoring;

namespace Xping.Cli.Tests.Report;

public sealed class BenjaminiHochbergTests
{
    /// <summary>
    /// The worked example from Benjamini and Hochberg (1995) §2, fifteen hypotheses at q = 0.05.
    /// </summary>
    /// <remarks>
    /// The example is the one the procedure is defined by, and it is chosen here over an invented
    /// case for the reason it was chosen there: four of its p-values sit between the Bonferroni bar
    /// and the step-up bar, so a bug that fell back on either the uncorrected level or the
    /// family-wise one produces a different answer rather than the same one by luck.
    /// </remarks>
    [Fact]
    public void TheStepUpReproducesTheProceduresOwnWorkedExample()
    {
        double[] p =
        [
            0.0001, 0.0004, 0.0019, 0.0095, 0.0201, 0.0278, 0.0298, 0.0344,
            0.0459, 0.3240, 0.4262, 0.5719, 0.6528, 0.7590, 1.0000
        ];

        // The published answer: the first four are discoveries and 0.0095 is the largest of them.
        Assert.Equal(0.0095, BenjaminiHochberg.Cutoff(p, p.Length, 0.05));
    }

    /// <summary>
    /// The step-up half of the name: a rank that clears its own bar carries every smaller p-value
    /// with it, including ones that failed a bar of their own.
    /// </summary>
    /// <remarks>
    /// The difference from a step-down procedure, and the whole reason the loop does not stop at the
    /// first failure. Here 0.030 fails the bar for rank 2 (0.020) while 0.039 clears the bar for
    /// rank 4 (0.040), so all four are discoveries.
    /// </remarks>
    [Fact]
    public void APValueThatFailedItsOwnRankIsCarriedByALargerOneThatPassed()
    {
        double[] p = [0.009, 0.030, 0.031, 0.039];

        Assert.Equal(0.039, BenjaminiHochberg.Cutoff(p, 10, 0.10));
    }

    /// <summary>
    /// The family, not the list, is the denominator — which is the entire point of the pass.
    /// </summary>
    [Fact]
    public void TheBarTightensWithTheFamilyAndNotWithHowManyResultsWereSupplied()
    {
        double[] p = [0.008];

        // One comparison out of five: 0.10 * 1/5 = 0.02, which 0.008 clears.
        Assert.Equal(0.008, BenjaminiHochberg.Cutoff(p, 5, 0.10));

        // The same finding out of three hundred: 0.10 * 1/300 = 3.3e-4, which it does not.
        Assert.Null(BenjaminiHochberg.Cutoff(p, 300, 0.10));
    }

    /// <summary>
    /// Equal p-values are one claim told twice, and are accepted or rejected together.
    /// </summary>
    /// <remarks>
    /// Not a corner case. Fisher's exact test on a small table lands on a coarse ladder of attainable
    /// values, so two fingerprints holding the identical p-value is the ordinary case, and a cutoff
    /// that fell on an index rather than a value would keep one of a pair and drop its twin on the
    /// strength of list order.
    /// </remarks>
    [Fact]
    public void TiedPValuesAreKeptOrDroppedTogether()
    {
        // Rank 1's bar is 0.010 and rank 2's is 0.020, so the pair clears only at the second rank
        // and a step that cut on an index would keep the later of two identical claims.
        double[] tied = [0.015, 0.015];

        Assert.Equal(0.015, BenjaminiHochberg.Cutoff(tied, 10, 0.10));
    }

    /// <summary>
    /// The bar is inclusive: a p-value exactly on it is a discovery.
    /// </summary>
    [Fact]
    public void APValueExactlyOnItsBarIsKept()
    {
        Assert.Equal(0.01, BenjaminiHochberg.Cutoff([0.01], 10, 0.10));
    }

    /// <summary>
    /// A family smaller than the results in it is a caller bug, and the answer is the stricter of
    /// the two readings rather than an exception.
    /// </summary>
    /// <remarks>
    /// Trusting the smaller number is the one thing that must not happen: it would correct against a
    /// family narrower than the evidence, which is the direction that invents findings. Throwing
    /// would cost the whole report for one provider's miscount, which the coordinator's own
    /// containment exists to avoid.
    /// </remarks>
    [Fact]
    public void AFamilySmallerThanItsOwnResultsIsTreatedAsTheirNumber()
    {
        double[] p = [0.02, 0.03, 0.04, 0.05, 0.30];

        Assert.Equal(BenjaminiHochberg.Cutoff(p, 5, 0.10), BenjaminiHochberg.Cutoff(p, 1, 0.10));
    }

    /// <summary>
    /// Nothing to report reads as nothing to report, whatever the shape of the input.
    /// </summary>
    [Theory]
    [InlineData(0.10)]
    [InlineData(1.00)]
    public void AFamilyOfLargePValuesAdmitsNothing(double rate)
    {
        Assert.Null(BenjaminiHochberg.Cutoff([0.4, 0.6, 0.9], 100, rate));
    }

    [Fact]
    public void NoResultsAdmitNothing()
    {
        Assert.Null(BenjaminiHochberg.Cutoff([], 300, 0.10));
    }

    /// <summary>
    /// A rate that is not a proportion is not a rate, and claims nothing rather than throwing.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void ARateThatIsNotAProportionAdmitsNothing(double rate)
    {
        Assert.Null(BenjaminiHochberg.Cutoff([0.0001], 1, rate));
    }

    /// <summary>
    /// The cutoff never falls below the smallest observed p-value or above the largest.
    /// </summary>
    /// <remarks>
    /// The invariant that makes "every p at or below the cutoff is a discovery" a filter on the
    /// supplied list rather than an arithmetic bar that might sit between two of them: whatever is
    /// returned is always one of the values that was handed in.
    /// </remarks>
    [Fact]
    public void TheCutoffIsAlwaysOneOfTheSuppliedPValues()
    {
        double[] p = [0.0001, 0.0004, 0.0019, 0.0095, 0.0201];

        double? cutoff = BenjaminiHochberg.Cutoff(p, p.Length, 0.05);

        Assert.Contains(cutoff!.Value, p);
    }

    /// <summary>
    /// A looser rate can only admit more, never less.
    /// </summary>
    [Fact]
    public void RaisingTheRateNeverSilencesAFindingItAlreadyAdmitted()
    {
        double[] p = [0.001, 0.012, 0.030, 0.200];

        double? strict = BenjaminiHochberg.Cutoff(p, 20, 0.05);
        double? loose = BenjaminiHochberg.Cutoff(p, 20, 0.10);

        Assert.NotNull(strict);
        Assert.NotNull(loose);
        Assert.True(loose >= strict, $"{loose} >= {strict}");
    }
}
