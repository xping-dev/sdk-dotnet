/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Cli.Report.Model;

/// <summary>
/// Which executions, or which runs, a kind's published counts were taken over.
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
/// Executions <i>or runs</i>, because the two are not the same denominator and one kind counts the
/// second. <see cref="FindingKind.Vanished"/> counts session appearances, so what it sets aside is
/// a run rather than an execution — and a rule that could only describe executions would have to
/// call that kind <see cref="AllExecutions"/> and say nothing was set aside, which is how this
/// enum came to publish a false marker in the first place.
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
    ExcludesEnvironmentalAndClustered,

    /// <summary>
    /// Every run that covered the suite, without the runs that covered only part of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A discount and not an availability fact, which is why it belongs in this enum at all under
    /// the rule stated above: a run under a <c>dotnet test --filter</c> was recorded and is
    /// perfectly readable. The report makes a judgement — that its silence about the tests it never
    /// selected says nothing — and removes it from both sides of the comparison. That is the same
    /// species of decision as discounting an environmental run, and the same thing a reader needs
    /// told.
    /// </para>
    /// <para>
    /// A fourth member rather than a second, orthogonal marker on the finding, because the two
    /// dimensions are mutually exclusive in practice. A kind either reads the outcomes of
    /// executions — in which case it must count every run the test appeared in, filtered or not,
    /// since a filtered run's outcomes are as true as any other run's — or it reads appearances, in
    /// which case discounting executions means nothing to it. There is no
    /// <c>ExcludesEnvironmentalAndClusteredAndPartial</c> waiting to be written. A second marker
    /// would also double the comparison this one exists to make cheap, and spend the columns the
    /// finding's source path needs.
    /// </para>
    /// <para>
    /// Which runs covered the suite is <see cref="Indexes.SessionView.IsPartial"/>, against
    /// <see cref="LocalAnalysisConstants.PartialSessionShare"/> of the largest run in the window.
    /// </para>
    /// </remarks>
    ExcludesPartialRuns
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
    /// The two <see cref="PopulationRule.AllExecutions"/> kinds are the argued exceptions.
    /// <see cref="FindingKind.SharedFailure"/> and <see cref="FindingKind.BrokenFixture"/> keep
    /// environmental sessions because a shared cause is precisely what an environmental session
    /// looks like from underneath, and discounting them would silence the finding that explains
    /// them.
    /// </para>
    /// <para>
    /// <see cref="FindingKind.Vanished"/> is the one kind whose population is counted in runs. It
    /// keeps environmental runs — an environmental run is still a run the test either was or was
    /// not in, and dropping it would shorten the history the absence is measured against — and sets
    /// aside the runs that covered only part of the suite, because a run that never selected a test
    /// did not fail to see it. So its rule is <see cref="PopulationRule.ExcludesPartialRuns"/>
    /// rather than <see cref="PopulationRule.AllExecutions"/>, which is what it claimed while its
    /// provider was already setting those runs aside.
    /// </para>
    /// <para>
    /// Exhaustive rather than defaulted, and the default arm throws. C# cannot make a switch over an
    /// enum exhaustive — the type admits values no member declares — so a kind added to
    /// <see cref="FindingKind"/> without a decision recorded here reaches the arm at the bottom and
    /// takes the report down with it.
    /// </para>
    /// <para>
    /// Deliberately harsher than its neighbours. <see cref="Rendering.ReportVocabulary.LabelFor"/>
    /// and the headline resolver both degrade to something honest and useless for an unknown kind,
    /// because they choose words. This chooses a claim: the only value it could fall back to is
    /// <see cref="PopulationRule.AllExecutions"/>, and quietly publishing "nothing was set aside"
    /// about a kind whose discounting nobody has decided is the exact failure this type exists to
    /// prevent. <c>EveryKindRecordsWhichPopulationItsRatesAreTakenOver</c> turns it into a CI
    /// failure rather than one a reader ever meets.
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
        FindingKind.Vanished => PopulationRule.ExcludesPartialRuns,

        _ => throw new NotSupportedException($"No population rule is recorded for '{kind}'.")
    };
}
