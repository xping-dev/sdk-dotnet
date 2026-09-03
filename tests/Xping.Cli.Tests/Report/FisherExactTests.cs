/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Cli.Report.Scoring;

namespace Xping.Cli.Tests.Report;

public sealed class FisherExactTests
{
    // ---------------------------------------------------------------------------------------
    // Tables that can be checked by hand
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void APerfectSplitOfSixAgainstSixIsTwoArrangementsInNineHundredAndTwentyFour()
    {
        // Twelve runs deal six failures C(12,6) = 924 ways. Exactly one puts all six in the first
        // arm, and its mirror image — all six in the second — is equally probable and equally
        // extreme, so a two-sided answer counts both.
        Assert.Equal(2.0 / 924, FisherExact.TwoSidedPValue(6, 6, 0, 6), 12);
    }

    [Fact]
    public void AnUnevenTailIsCountedOnceRatherThanDoubled()
    {
        // Five failures of six against none of six. The opposite tail — none of six against five of
        // six — is a different table with a different probability under these margins, and it is the
        // less extreme of the two, so it is not counted. A p-value formed by doubling one tail would
        // report 1/231 here instead of 1/462, which is the error this table exists to catch.
        Assert.Equal(1.0 / 462, FisherExact.TwoSidedPValue(5, 5, 0, 6), 12);
    }

    [Theory]
    [InlineData(5, 6, 0, 6, 0.015152)]      // five of six against none: the smallest gap that carries
    [InlineData(4, 6, 0, 6, 0.060606)]      // four of six: the first table a level of 0.05 refuses
    [InlineData(3, 6, 0, 6, 0.181818)]      // three of six: the commonest way six failures fall
    [InlineData(5, 5, 0, 5, 0.007937)]      // the floor at five runs a side, which no gap gets below
    [InlineData(9, 10, 0, 6, 0.000874)]
    [InlineData(15, 15, 10, 20, 0.001568)]
    public void TheAnswerIsTheOneTheHypergeometricDistributionGives(
        int failures, int trials, int otherFailures, int otherTrials, double expected)
    {
        Assert.Equal(
            expected, FisherExact.TwoSidedPValue(failures, trials, otherFailures, otherTrials), 6);
    }

    // ---------------------------------------------------------------------------------------
    // Symmetries the caller relies on
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void SwappingTheArmsGivesABitIdenticalAnswer()
    {
        // Exact rather than approximate on purpose. The provider compares p-values with `!=` to
        // decide which of two divisions to publish, and that tie-break only fires if two ways of
        // describing one division agree to the last bit.
        Assert.Equal(
            FisherExact.TwoSidedPValue(9, 10, 1, 12),
            FisherExact.TwoSidedPValue(1, 12, 9, 10));
    }

    [Fact]
    public void CountingThePassesInsteadOfTheFailuresGivesTheSameAnswer()
    {
        // The test is about whether the two arms divided one outcome unevenly, and which outcome was
        // called the interesting one is the caller's convention, not a property of the table.
        Assert.Equal(
            FisherExact.TwoSidedPValue(9, 10, 1, 12),
            FisherExact.TwoSidedPValue(1, 10, 11, 12),
            12);
    }

    // ---------------------------------------------------------------------------------------
    // Degenerate input
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AnEmptyArmClaimsNothingRatherThanThrowing()
    {
        Assert.Equal(1.0, FisherExact.TwoSidedPValue(0, 0, 3, 10));
        Assert.Equal(1.0, FisherExact.TwoSidedPValue(3, 10, 0, 0));
    }

    [Fact]
    public void AnOutcomeNothingVariedOnIsCertainRatherThanImpossible()
    {
        // Every run failed, or none did. Both margins are held at what was observed, so there is one
        // table consistent with them and the observed one is it.
        Assert.Equal(1.0, FisherExact.TwoSidedPValue(0, 8, 0, 12));
        Assert.Equal(1.0, FisherExact.TwoSidedPValue(8, 8, 12, 12));
    }

    [Fact]
    public void TwoArmsThatDividedTheFailuresEvenlyClaimNothing()
    {
        // Half of each arm red. This is the modal table under the null, so nothing is less probable
        // than it and every arrangement is counted.
        Assert.Equal(1.0, FisherExact.TwoSidedPValue(5, 10, 5, 10), 12);
    }

    [Fact]
    public void MoreFailuresThanRunsIsReadAsAllOfThemRatherThanReturningNaN()
    {
        Assert.Equal(FisherExact.TwoSidedPValue(6, 6, 0, 6), FisherExact.TwoSidedPValue(9, 6, -2, 6));
    }

    // ---------------------------------------------------------------------------------------
    // What the arithmetic has to survive
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(10)]
    [InlineData(60)]
    [InlineData(200)]
    [InlineData(1000)]
    public void ALongWindowDoesNotOverflow(int arm)
    {
        // `--runs` has no upper bound, and the number of ways a long window deals its failures is
        // astronomically large: at a thousand runs a side the central binomial coefficient is around
        // 10^600. Forming one would be an infinity, and every comparison against an infinity is
        // false, so the answer would come back as no arrangements at all rather than as a
        // probability. Nothing here forms one — the weights are accumulated as logarithms and
        // rescaled — so an even split still reads as the commonest thing a window can show.
        double even = FisherExact.TwoSidedPValue(arm / 2, arm, arm / 2, arm);

        Assert.InRange(even, 0.5, 1.0);
        Assert.True(FisherExact.TwoSidedPValue(arm, arm, 0, arm) < even);
    }

    [Fact]
    public void TheAnswerIsNeverZeroForAnyWindowAStoreCouldHold()
    {
        // The floor is the probability of the observed table itself, which the sum always includes.
        // A p-value of zero would be a claim of certainty, and no window of a plausible size makes
        // one: five hundred runs a side, perfectly separated, is still 10^-300 and a double.
        for (int arm = 5; arm <= 500; arm++)
            Assert.True(FisherExact.TwoSidedPValue(arm, arm, 0, arm) > 0);
    }

    [Fact]
    public void AThousandRunsASideIsWhereTheArithmeticStops()
    {
        // Stated rather than hidden. A perfectly separated split of two thousand runs has a
        // probability below the smallest positive double and underflows to zero, which reads as
        // certainty. It is the honest place for the limit to sit: no store holds that window, and a
        // claim resting on two thousand runs is not one a reader needs to tell from certain.
        Assert.Equal(0, FisherExact.TwoSidedPValue(1000, 1000, 0, 1000));
    }
}
