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
/// Only the counters that decompose appear here. <c>WallClockDurationMs</c> does not: assemblies
/// interleave inside one host, so a slice of wall clock is not a thing that exists. Neither does
/// <c>TotalTestsExpected</c>, which the runner reports for the host. Both remain on
/// <see cref="QuickStatistics"/>, which is where they are true.
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
