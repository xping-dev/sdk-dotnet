/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Services.Collector;

/// <summary>
/// Represents statistics about the test execution collector.
/// </summary>
public sealed class CollectorStats
{
    /// <summary>
    /// Gets or sets the total number of tests recorded.
    /// </summary>
    public long TotalRecorded { get; set; }

    /// <summary>
    /// Gets or sets the total number of tests currently in the buffer.
    /// </summary>
    public int BufferCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of tests sampled out (not recorded due to sampling).
    /// </summary>
    public long TotalSampled { get; set; }
}
