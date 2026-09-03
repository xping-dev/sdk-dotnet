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

    // ---------------------------------------------------------------------------------------
    // The one-sided tail
    // ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(3, 17, 0, 3, 0.596491)]     // ran in 3 of 17 and missed the last 3: the likeliest thing it could do
    [InlineData(8, 17, 0, 3, 0.192982)]
    [InlineData(12, 17, 0, 3, 0.049123)]    // the first table on a default window that clears 0.05
    [InlineData(17, 17, 0, 3, 0.000877)]    // ran in every one of them: one deal in 1140
    [InlineData(5, 5, 0, 3, 0.017857)]      // the shortest window that can produce this kind at all
    [InlineData(6, 6, 0, 1, 0.142857)]      // a one-session slice, and the floor it cannot get under
    [InlineData(5, 6, 1, 6, 0.040043)]      // a tail of more than one table, to exercise the sum
    public void TheUpperTailIsTheOneTheHypergeometricDistributionGives(
        int successes, int trials, int otherSuccesses, int otherTrials, double expected)
    {
        Assert.Equal(
            expected,
            FisherExact.OneSidedPValue(successes, trials, otherSuccesses, otherTrials),
            6);
    }

    [Fact]
    public void TheTailIsNoLargerThanTheTwoSidedAnswerOnAnExtremeTable()
    {
        // One tail of the same table against both of them, and only above the mode. There the tail
        // holds the observed table and rarer ones still, all of which the two-sided sum also counts,
        // so it can only be smaller — equal where the opposite tail holds nothing.
        Assert.True(
            FisherExact.OneSidedPValue(5, 6, 0, 6) <= FisherExact.TwoSidedPValue(5, 6, 0, 6));
        Assert.True(
            FisherExact.OneSidedPValue(15, 15, 10, 20) <= FisherExact.TwoSidedPValue(15, 15, 10, 20));
    }

    [Fact]
    public void BelowTheModeTheTailIsLargerThanTheTwoSidedAnswerInstead()
    {
        // Not a general bound, and the next test over is the counterexample. An upper tail taken from
        // below the mode sweeps up the mode itself, which is the commonest table there is and exactly
        // what a two-sided sum leaves out. Pinned so that nobody derives one of these from the other.
        Assert.True(
            FisherExact.OneSidedPValue(1, 12, 9, 10) > FisherExact.TwoSidedPValue(1, 12, 9, 10));
    }

    [Fact]
    public void SwappingTheArmsAnswersTheOppositeQuestion()
    {
        // Deliberately not the symmetry the two-sided test has. Naming an arm first is the hypothesis
        // here, so a caller that swapped them would be asking whether the arm that held almost none
        // of the behaviour held surprisingly many — which is a different question with a different
        // answer, and on this table a near-certainty rather than a finding.
        Assert.Equal(0.000187, FisherExact.OneSidedPValue(9, 10, 1, 12), 6);
        Assert.Equal(0.999998, FisherExact.OneSidedPValue(1, 12, 9, 10), 6);
    }

    [Theory]
    [InlineData(17, 12)]
    [InlineData(20, 14)]
    [InlineData(40, 27)]
    [InlineData(100, 65)]
    [InlineData(1000, 633)]
    public void TheRunRateAThreeSessionSliceDemandsEasesTowardsAFixedLimit(int baseline, int first)
    {
        // What `Vanished` costs, pinned where it is computed rather than left in a doc comment.
        // C(n,x)/C(n+3,x) tends to (1-r)^3, so the level of 0.05 falls away to 1 - 0.05^(1/3) =
        // 0.632 — the requirement gets *cheaper* as the history lengthens, not dearer, and 0.71 on
        // a default window is the expensive end of the range rather than the asymptote.
        Assert.True(FisherExact.OneSidedPValue(first, baseline, 0, 3) <= 0.05);
        Assert.True(FisherExact.OneSidedPValue(first - 1, baseline, 0, 3) > 0.05);

        Assert.InRange((double)first / baseline, 0.632, 0.71);
    }

    [Fact]
    public void TheTailClaimsNothingOnTheSameDegenerateTables()
    {
        Assert.Equal(1.0, FisherExact.OneSidedPValue(0, 0, 3, 10));
        Assert.Equal(1.0, FisherExact.OneSidedPValue(3, 10, 0, 0));
        Assert.Equal(1.0, FisherExact.OneSidedPValue(0, 8, 0, 12));
        Assert.Equal(1.0, FisherExact.OneSidedPValue(8, 8, 12, 12));
    }

    [Fact]
    public void TheTailSurvivesALongWindowToo()
    {
        // Same arithmetic as the two-sided test and the same reason it has to hold: no coefficient is
        // ever formed, so a thousand runs a side is a probability rather than an infinity.
        Assert.InRange(FisherExact.OneSidedPValue(500, 1000, 500, 1000), 0.4, 0.6);

        for (int arm = 5; arm <= 500; arm++)
            Assert.True(FisherExact.OneSidedPValue(arm, arm, 0, arm) > 0);

        // And it stops in the same place, for the same reason. Pinned rather than hidden behind a
        // `>= 0` that the clamp makes true whatever the arithmetic did.
        Assert.Equal(0, FisherExact.OneSidedPValue(1000, 1000, 0, 1000));
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
