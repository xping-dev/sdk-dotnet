/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.ObjectModel;

using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Statistics;

namespace Xping.Sdk.Core.Services.Statistics.Internals;

/// <summary>
/// A no-op implementation of <see cref="IRunningStatisticsAccumulator"/> used when the SDK
/// is disabled or configuration validation fails. All operations are no-ops,
/// <see cref="GetSnapshot()"/> returns zeroed statistics and
/// <see cref="GetSnapshotByAssembly"/> an empty breakdown.
/// </summary>
internal sealed class NoOpRunningStatisticsAccumulator : IRunningStatisticsAccumulator, IWallClockAwareStatisticsAccumulator
{
    // One shared instance, so it has to be genuinely read-only: a consumer casting the returned
    // value back to Dictionary would otherwise corrupt every later caller's empty breakdown.
    private static readonly IReadOnlyDictionary<string, AssemblyStatistics> NoAssemblies =
        new ReadOnlyDictionary<string, AssemblyStatistics>(
            new Dictionary<string, AssemblyStatistics>(StringComparer.Ordinal));

    /// <inheritdoc/>
    public void Record(TestExecution execution)
    {
        // No-op: discard the execution
    }

    /// <inheritdoc/>
    public QuickStatistics GetSnapshot() => new();

    /// <inheritdoc/>
    public QuickStatistics GetSnapshot(TimeSpan wallClockElapsed) => new();

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, AssemblyStatistics> GetSnapshotByAssembly() => NoAssemblies;

    /// <inheritdoc/>
    public void Reset()
    {
        // No-op: nothing to reset
    }
}
