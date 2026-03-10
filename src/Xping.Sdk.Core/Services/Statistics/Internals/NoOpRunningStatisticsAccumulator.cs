/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Statistics;

namespace Xping.Sdk.Core.Services.Statistics.Internals;

/// <summary>
/// A no-op implementation of <see cref="IRunningStatisticsAccumulator"/> used when the SDK
/// is disabled or configuration validation fails. All operations are no-ops and
/// <see cref="GetSnapshot"/> returns zeroed statistics.
/// </summary>
internal sealed class NoOpRunningStatisticsAccumulator : IRunningStatisticsAccumulator
{
    /// <inheritdoc/>
    public void Record(TestExecution execution)
    {
        // No-op: discard the execution
    }

    /// <inheritdoc/>
    public QuickStatistics GetSnapshot() => new();

    /// <inheritdoc/>
    public void Reset()
    {
        // No-op: nothing to reset
    }
}
