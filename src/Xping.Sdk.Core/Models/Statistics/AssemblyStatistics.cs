/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models.Statistics;

/// <summary>
/// One test assembly's slice of a session's <see cref="QuickStatistics"/>.
/// Only populated on the <c>TestSessionState.Finalized</c> upload.
/// </summary>
/// <remarks>
/// <para>
/// A session records one test host process, not one test assembly: a solution-wide
/// <c>dotnet test</c> batches several test projects into a single host. <see cref="QuickStatistics"/>
/// counts that whole host, so it cannot be attributed to any one of the assemblies the host ran.
/// This is the same reading, restricted to the executions belonging to one of them.
/// </para>
/// <para>
/// Only what can be attributed to one assembly appears here. <c>WallClockDurationMs</c> does not:
/// assemblies interleave inside one host, so a slice of wall clock is not a thing that exists. It
/// remains on <see cref="QuickStatistics"/>, which is where it is true.
/// </para>
/// <para>
/// <see cref="DiscoveredTestCases"/> and <see cref="SelectedTestCases"/> are the exception to
/// "slice of <see cref="QuickStatistics"/>": they have no host-wide counterpart, because a test
/// framework discovers and selects one assembly at a time and nothing adds them up across a host.
/// They are here, and only here, because the local store keeps this breakdown when it projects a
/// session onto one assembly and drops everything host-wide.
/// </para>
/// <para>
/// The two readings of <see cref="QuickStatistics"/> carry over unchanged: the unprefixed counters
/// count <b>executions</b>, so every retry attempt is counted separately, while the <c>Final*</c>
/// counters count <b>distinct tests</b>, each contributing only the outcome of its highest-numbered
/// attempt.
/// </para>
/// </remarks>
public sealed class AssemblyStatistics
{
    /// <summary>
    /// Gets the total number of test executions this assembly recorded.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Gets the number of executions that passed.
    /// </summary>
    public int Passed { get; init; }

    /// <summary>
    /// Gets the number of executions that failed.
    /// </summary>
    public int Failed { get; init; }

    /// <summary>
    /// Gets the number of executions that were skipped.
    /// </summary>
    public int Skipped { get; init; }

    /// <summary>
    /// Gets the number of executions with an inconclusive result.
    /// </summary>
    public int Inconclusive { get; init; }

    /// <summary>
    /// Gets the number of executions that were not executed.
    /// </summary>
    public int NotExecuted { get; init; }

    /// <summary>
    /// Gets the number of executions killed by their framework for exceeding a timeout.
    /// </summary>
    public int Timeout { get; init; }

    /// <summary>
    /// Gets the number of distinct tests this assembly recorded, counting all attempts of a retried
    /// test as one test.
    /// </summary>
    public int DistinctTests { get; init; }

    /// <summary>
    /// Gets the number of distinct tests whose highest-numbered attempt passed.
    /// </summary>
    public int FinalPassed { get; init; }

    /// <summary>
    /// Gets the number of distinct tests whose highest-numbered attempt failed.
    /// </summary>
    public int FinalFailed { get; init; }

    /// <summary>
    /// Gets the number of distinct tests whose highest-numbered attempt was skipped.
    /// </summary>
    public int FinalSkipped { get; init; }

    /// <summary>
    /// Gets the number of distinct tests whose highest-numbered attempt was inconclusive.
    /// </summary>
    public int FinalInconclusive { get; init; }

    /// <summary>
    /// Gets the number of distinct tests whose highest-numbered attempt was not executed.
    /// </summary>
    public int FinalNotExecuted { get; init; }

    /// <summary>
    /// Gets the number of distinct tests whose highest-numbered attempt timed out.
    /// </summary>
    public int FinalTimeout { get; init; }

    /// <summary>
    /// Gets how many test cases the test framework discovered in this assembly before any filter was
    /// applied, or <see langword="null"/> when discovery was not observed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A test case is the framework's unit of selection, not a distinct test: a theory whose data
    /// cannot be enumerated at discovery is one case that runs as several tests. Compare it with
    /// <see cref="SelectedTestCases"/>, which counts the same unit, and never with
    /// <see cref="DistinctTests"/>.
    /// </para>
    /// <para>
    /// <see langword="null"/> means "not observed", never "none discovered". Only xUnit reports it,
    /// and only when discovery ran in the test process — it does under <c>dotnet test</c>, with or
    /// without <c>--filter</c>, and does not when an IDE runs tests it discovered earlier. NUnit and
    /// MSTest expose no count that can be trusted for this; see
    /// <c>docs/known-limitations.md</c>.
    /// </para>
    /// </remarks>
    public int? DiscoveredTestCases { get; init; }

    /// <summary>
    /// Gets how many test cases the test framework was asked to run in this assembly, after any
    /// filter was applied, or <see langword="null"/> when selection was not observed.
    /// </summary>
    /// <remarks>
    /// Fewer selected than <see cref="DiscoveredTestCases"/> is the fact that tells a run under
    /// <c>dotnet test --filter</c> from one whose tests were deleted: a filtered run discovered the
    /// tests it did not run, and a run after a deletion did not discover them at all.
    /// </remarks>
    public int? SelectedTestCases { get; init; }

    /// <summary>
    /// Gets the combined duration of this assembly's executions in milliseconds.
    /// </summary>
    public long TotalDurationMs { get; init; }

    /// <summary>
    /// Gets the display name of this assembly's slowest test, or <c>null</c> when it recorded none.
    /// </summary>
    public string? SlowestTestName { get; init; }

    /// <summary>
    /// Gets the duration of this assembly's slowest test in milliseconds.
    /// </summary>
    public long SlowestTestDurationMs { get; init; }

    /// <summary>
    /// Gets the proportion of this assembly's executions that passed, as a ratio from 0.0 to 1.0.
    /// Returns 0.0 when <see cref="Total"/> is zero.
    /// </summary>
    /// <remarks>
    /// Computed rather than stored, here and on the two properties below. A rate copied from the
    /// host-wide statistics would describe every assembly but this one, and that is precisely the
    /// mistake this type exists to prevent — so the ratios are not values that can be set.
    /// </remarks>
    public double SuccessRate => Total == 0 ? 0.0 : (double)Passed / Total;

    /// <summary>
    /// Gets the proportion of this assembly's distinct tests that ended passed, as a ratio from
    /// 0.0 to 1.0. Returns 0.0 when <see cref="DistinctTests"/> is zero.
    /// </summary>
    public double FinalSuccessRate => DistinctTests == 0 ? 0.0 : (double)FinalPassed / DistinctTests;

    /// <summary>
    /// Gets the mean duration per execution in milliseconds.
    /// Returns 0 when <see cref="Total"/> is zero.
    /// </summary>
    public long AverageDurationMs => Total == 0 ? 0L : TotalDurationMs / Total;
}
