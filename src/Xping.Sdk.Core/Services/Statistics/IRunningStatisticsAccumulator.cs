/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Statistics;

namespace Xping.Sdk.Core.Services.Statistics;

/// <summary>
/// Accumulates test execution statistics incrementally across all batch uploads within a session,
/// producing a final <see cref="QuickStatistics"/> snapshot on demand.
/// </summary>
/// <remarks>
/// All methods must be thread-safe. A single instance is shared across the session lifetime
/// (singleton DI lifetime) and may receive concurrent calls from parallel test runners.
/// </remarks>
public interface IRunningStatisticsAccumulator
{
    /// <summary>
    /// Records a single test execution into the running totals.
    /// </summary>
    /// <param name="execution">The completed test execution to record.</param>
    void Record(TestExecution execution);

    /// <summary>
    /// Returns an immutable snapshot of the statistics accumulated so far.
    /// Safe to call at any time, including concurrently with <see cref="Record"/>.
    /// </summary>
    QuickStatistics GetSnapshot();

    /// <summary>
    /// Returns an immutable snapshot of the statistics accumulated so far, broken down by the test
    /// assembly each execution belongs to. Safe to call at any time, including concurrently with
    /// <see cref="Record"/>.
    /// </summary>
    /// <returns>
    /// One entry per assembly that recorded an execution, keyed by assembly name in ordinal order;
    /// empty when nothing was recorded, never <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A session records one test host process, not one test assembly, so <see cref="GetSnapshot"/>
    /// counts every test project a solution-wide <c>dotnet test</c> batched into that host. This is
    /// the same reading attributed to each of them.
    /// </para>
    /// <para>
    /// Only the counters that decompose appear here — see <see cref="AssemblyStatistics"/> for what
    /// is deliberately absent. An execution naming no assembly is counted by
    /// <see cref="GetSnapshot"/> alone rather than under an empty key.
    /// </para>
    /// </remarks>
    IReadOnlyDictionary<string, AssemblyStatistics> GetSnapshotByAssembly();

    /// <summary>
    /// Resets all counters and totals to zero.
    /// </summary>
    void Reset();
}
