/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Scoring;

/// <summary>
/// How strongly a binary outcome and an ordered exposure move together.
/// </summary>
/// <remarks>
/// <para>
/// Kendall's τ_b. Every pair of observations is either concordant — the one at the higher level is
/// also the one that showed the behaviour — discordant, or tied on one axis or both, and τ_b is the
/// excess of concordant over discordant pairs as a share of the pairs that could have been either.
/// </para>
/// <para>
/// <b>τ_b rather than τ_a, because this data is nothing but ties.</b> A binary outcome ties every
/// pair of passes with each other and every pair of failures with each other, and a concurrency axis
/// concentrated on a handful of levels ties most of the rest. τ_a divides by the total number of
/// pairs and would therefore report a perfectly separated table as something far below one; τ_b
/// divides by the pairs each axis actually leaves untied and reports it as exactly one.
/// </para>
/// <para>
/// <b>What the tie correction costs, and it must be said out loud: τ_b is not comparable across
/// exposure distributions.</b> Write <c>P</c> for the excess of concordant over discordant pairs,
/// <c>R(N−R)</c> for the pairs the outcome leaves untied and <c>n₀ − nₓ</c> for the pairs the level
/// leaves untied. Then <c>τ_b = (P / R(N−R)) × √(R(N−R) / (n₀ − nₓ))</c>: an association measured
/// per untied outcome pair — Somers' D — multiplied by a discount that is nothing but the ratio
/// between how tied the two axes are. A suite pinned at one concurrency with a few serial runs is
/// far more tied on the level than on the outcome and reads about 1.15 times its Somers' D; a suite
/// spread evenly over a dozen levels reads about 0.72 times its own. Two tests behaving identically
/// on differently configured suites therefore do not get the same number. A threshold applied to
/// this is a threshold on an association discounted by how much room the exposure had, which is a
/// defensible thing to threshold on and is not the same thing as the association.
/// </para>
/// <para>
/// <b>An estimate only.</b> This reports how strongly the two move together and says nothing about
/// how sure of it the data makes anyone — see the remark on <see cref="TauB"/> for why no interval is
/// offered here, and <see cref="CochranArmitage"/> for what does answer that.
/// </para>
/// </remarks>
internal static class KendallTau
{
    /// <summary>
    /// Rank correlation between the level and the outcome.
    /// </summary>
    /// <param name="points">The observations, in any order.</param>
    /// <returns>
    /// τ_b in [-1,1]; positive where the behaviour grows more common as the level rises. Exactly 0
    /// wherever the question cannot be asked — one level, one outcome, or nothing to compare.
    /// </returns>
    /// <remarks>
    /// An estimate, with no interval around it. τ_b's asymptotic variance assumes independent
    /// observations, which is the assumption <see cref="TrendPoint.Cluster"/> exists to deny, and the
    /// obvious clustered alternative — a delete-one-cluster jackknife — is degenerate on exactly the
    /// data this serves: it measures the spread between occasions, so a window of occasions that
    /// agree with each other returns a standard error of zero however few of them there are, and a
    /// bound built on it would rank five identical runs alongside fifty. A caller wanting to know how
    /// much of this the evidence supports should discount it by the precision of the test that
    /// measured the same association, which is what <see cref="CochranArmitage"/> reports.
    /// </remarks>
    public static double TauB(IReadOnlyList<TrendPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        return Table.Of(points).TauB();
    }

    /// <summary>
    /// The observations counted by level.
    /// </summary>
    /// <remarks>
    /// Levels are held ascending, which is what lets τ_b be read in one pass over them with running
    /// totals rather than by enumerating pairs.
    /// </remarks>
    private sealed class Table
    {
        private int[] _trials = [];
        private int[] _occurrences = [];

        /// <summary>Counts <paramref name="points"/> by level, ascending.</summary>
        public static Table Of(IReadOnlyList<TrendPoint> points)
        {
            var distinct = new SortedSet<int>();

            foreach (TrendPoint point in points)
                distinct.Add(point.Level);

            int[] levels = [.. distinct];
            var index = new Dictionary<int, int>(levels.Length);

            for (int i = 0; i < levels.Length; i++)
                index[levels[i]] = i;

            var table = new Table
            {
                _trials = new int[levels.Length],
                _occurrences = new int[levels.Length]
            };

            foreach (TrendPoint point in points)
            {
                table._trials[index[point.Level]]++;

                if (point.Occurred)
                    table._occurrences[index[point.Level]]++;
            }

            return table;
        }

        /// <summary>
        /// Reads τ_b off the level counts in one ascending pass.
        /// </summary>
        /// <remarks>
        /// A pair is concordant when the observation at the lower level passed and the one at the
        /// higher level showed the behaviour, so the concordant count is each level's occurrences
        /// against every non-occurrence below it, and the discordant count is its non-occurrences
        /// against every occurrence below it. Running totals of both make that one pass rather than
        /// the quadratic enumeration the definition describes.
        /// </remarks>
        public double TauB()
        {
            long total = 0;
            long occurred = 0;
            long tiedByLevel = 0;
            long concordant = 0;
            long discordant = 0;
            long belowQuiet = 0;
            long belowOccurred = 0;

            for (int i = 0; i < _trials.Length; i++)
            {
                long trials = _trials[i];
                long occurrences = _occurrences[i];
                long quiet = trials - occurrences;

                concordant += occurrences * belowQuiet;
                discordant += quiet * belowOccurred;

                belowQuiet += quiet;
                belowOccurred += occurrences;

                tiedByLevel += trials * (trials - 1) / 2;
                total += trials;
                occurred += occurrences;
            }

            long pairs = total * (total - 1) / 2;
            long untiedByLevel = pairs - tiedByLevel;
            long untiedByOutcome = occurred * (total - occurred);

            if (untiedByLevel <= 0 || untiedByOutcome <= 0)
                return 0;

            return (concordant - discordant) /
                Math.Sqrt((double)untiedByLevel * untiedByOutcome);
        }

    }
}
