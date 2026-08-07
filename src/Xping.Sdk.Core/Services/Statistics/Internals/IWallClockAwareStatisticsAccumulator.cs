/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Statistics;

namespace Xping.Sdk.Core.Services.Statistics.Internals;

/// <summary>
/// Internal extension of <see cref="IRunningStatisticsAccumulator"/> that also accepts the
/// wall-clock time elapsed since the session started, to populate
/// <see cref="QuickStatistics.WallClockDurationMs"/>.
/// </summary>
/// <remarks>
/// Kept separate from the public <see cref="IRunningStatisticsAccumulator"/> interface — since
/// <c>netstandard2.0</c> does not support default interface members, adding this method directly
/// to the public interface would be a source-breaking change for any external implementers.
/// </remarks>
internal interface IWallClockAwareStatisticsAccumulator
{
    /// <summary>
    /// Returns an immutable snapshot of the statistics accumulated so far, including the
    /// wall-clock time elapsed since the session started.
    /// </summary>
    /// <param name="wallClockElapsed">
    /// The wall-clock time elapsed since the session started, used to populate
    /// <see cref="QuickStatistics.WallClockDurationMs"/>. Negative values are clamped to zero.
    /// </param>
    QuickStatistics GetSnapshot(TimeSpan wallClockElapsed);
}
