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
    /// Records how many test cases the test framework discovered in an assembly and how many of
    /// them it was asked to run, replacing whatever was recorded for that assembly before.
    /// </summary>
    /// <param name="assembly">The test assembly, named as its executions name it.</param>
    /// <param name="discovered">
    /// The test cases discovered before any filter, or <see langword="null"/> when discovery was not
    /// observed in this process.
    /// </param>
    /// <param name="selected">The test cases the framework was asked to run.</param>
    /// <remarks>
    /// Surfaces as <see cref="AssemblyStatistics.DiscoveredTestCases"/> and
    /// <see cref="AssemblyStatistics.SelectedTestCases"/>, and only for an assembly that also
    /// recorded an execution: a breakdown entry with every counter at zero would claim a run of
    /// that assembly the session does not contain.
    /// </remarks>
    void RecordTestCases(string assembly, int? discovered, int selected);

    /// <summary>
    /// Returns an immutable snapshot of the statistics accumulated so far.
    /// Safe to call at any time, including concurrently with <see cref="Record"/>.
    /// </summary>
    QuickStatistics GetSnapshot();

    /// <summary>
    /// Returns an immutable snapshot of the statistics accumulated so far, including the
    /// wall-clock time elapsed since the session started.
    /// </summary>
    /// <param name="wallClockElapsed">
    /// The wall-clock time elapsed since the session started, used to populate
    /// <see cref="QuickStatistics.WallClockDurationMs"/>. Negative values are clamped to zero.
    /// </param>
    QuickStatistics GetSnapshot(TimeSpan wallClockElapsed);

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
    /// A session records one test host process, not one test assembly, so <see cref="GetSnapshot()"/>
    /// counts every test project a solution-wide <c>dotnet test</c> batched into that host. This is
    /// the same reading attributed to each of them.
    /// </para>
    /// <para>
    /// Only the counters that decompose appear here — see <see cref="AssemblyStatistics"/> for what
    /// is deliberately absent. An execution naming no assembly is counted by
    /// <see cref="GetSnapshot()"/> alone rather than under an empty key.
    /// </para>
    /// </remarks>
    IReadOnlyDictionary<string, AssemblyStatistics> GetSnapshotByAssembly();

    /// <summary>
    /// Resets all counters and totals to zero.
    /// </summary>
    void Reset();
}
