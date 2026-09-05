/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Model;

/// <summary>
/// Which executions a kind's published counts were taken over.
/// </summary>
/// <remarks>
/// <para>
/// Every kind publishes a count and a rate computed against it, the report ranks those rates
/// against each other, and the population behind them is not the same in every kind. Without this
/// on the finding, a reader comparing two rates is comparing two denominators with nothing on the
/// page to say so — and the arithmetic is not small: a test with twenty executions, ten of them
/// clustered failures and two of its own, publishes a rate of 2/12 rather than 2/22.
/// </para>
/// <para>
/// The populations themselves are deliberately not made uniform. Each kind's choice answers a
/// question about that kind, and <c>docs/internals/finding-populations.md</c> is where the per-kind
/// decision and its argument are recorded. This enum makes the choice visible; it does not decide
/// it.
/// </para>
/// <para>
/// It describes <b>discounting</b> — a judgement the report makes about a run — and never data
/// availability. An execution whose adapter recorded no concurrency, or a session that recorded no
/// UTC offset, is a measurement that could not be taken rather than one the report set aside, and
/// those kinds publish their own counts of what they could not read. Folding the two together here
/// would let "excludes environmental" mean two different things.
/// </para>
/// </remarks>
internal enum PopulationRule
{
    /// <summary>Every execution of the subject in the window, with nothing set aside.</summary>
    AllExecutions,

    /// <summary>
    /// Every execution except those from sessions where enough of the suite failed at once that the
    /// session says more about the machine than about any test in it.
    /// </summary>
    ExcludesEnvironmental,

    /// <summary>
    /// As <see cref="ExcludesEnvironmental"/>, and also without failures belonging to a
    /// shared-failure cluster, which are reported once against the cluster rather than again
    /// against each test it took down.
    /// </summary>
    ExcludesEnvironmentalAndClustered
}

/// <summary>
/// The population each finding kind's counts are taken over.
/// </summary>
/// <remarks>
/// One table, resolved from the kind rather than carried by each provider alongside its evidence.
/// A provider that declared its own rule would be a second place the answer lives, and the two
/// would eventually disagree about the same kind — which is exactly how the inconsistency this
/// exists to expose arose in the first place, as six separate remarks in six files.
/// </remarks>
internal static class PopulationRules
{
    /// <summary>
    /// Gets the population a kind's published counts were taken over.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The rule.</returns>
    /// <remarks>
    /// <para>
    /// The three <see cref="PopulationRule.AllExecutions"/> kinds are the argued exceptions.
    /// <see cref="FindingKind.SharedFailure"/> and <see cref="FindingKind.BrokenFixture"/> keep
    /// environmental sessions because a shared cause is precisely what an environmental session
    /// looks like from underneath, and discounting them would silence the finding that explains
    /// them. <see cref="FindingKind.Vanished"/> keeps them because it counts session appearances:
    /// an environmental run is still a run the test either was or was not in, and dropping it would
    /// shorten the history the absence is measured against.
    /// </para>
    /// <para>
    /// Exhaustive rather than defaulted. A kind added to <see cref="FindingKind"/> without a
    /// decision recorded here should fail to compile rather than quietly claim it counted
    /// everything.
    /// </para>
    /// </remarks>
    public static PopulationRule For(FindingKind kind) => kind switch
    {
        FindingKind.RetryMasked => PopulationRule.ExcludesEnvironmental,
        FindingKind.RetryDeepening => PopulationRule.ExcludesEnvironmental,
        FindingKind.RetryExhausted => PopulationRule.ExcludesEnvironmental,
        FindingKind.Flaky => PopulationRule.ExcludesEnvironmentalAndClustered,
        FindingKind.AlwaysFailing => PopulationRule.ExcludesEnvironmentalAndClustered,
        FindingKind.TimingOut => PopulationRule.ExcludesEnvironmentalAndClustered,
        FindingKind.BrokenFixture => PopulationRule.AllExecutions,
        FindingKind.SharedFailure => PopulationRule.AllExecutions,
        FindingKind.DurationRegression => PopulationRule.ExcludesEnvironmental,
        FindingKind.DurationUnstable => PopulationRule.ExcludesEnvironmental,
        FindingKind.ParallelSensitive => PopulationRule.ExcludesEnvironmental,
        FindingKind.TimeSensitive => PopulationRule.ExcludesEnvironmental,
        FindingKind.Vanished => PopulationRule.AllExecutions,

        _ => throw new NotSupportedException($"No population rule is recorded for '{kind}'.")
    };
}
