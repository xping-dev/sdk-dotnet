/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Services.Collector;

/// <summary>
/// Abstraction over high-resolution time, enabling deterministic unit testing of elapsed-time calculations.
/// </summary>
internal interface ITimeProvider
{
    /// <summary>
    /// Returns a high-resolution timestamp in the same units as <see cref="System.Diagnostics.Stopwatch.Frequency"/>.
    /// </summary>
    long GetTimestamp();

    /// <summary>
    /// The number of ticks per second for the current platform, matching
    /// <see cref="System.Diagnostics.Stopwatch.Frequency"/>.
    /// </summary>
    long Frequency { get; }
}
