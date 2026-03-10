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
    /// Resets all counters and totals to zero.
    /// </summary>
    void Reset();
}
