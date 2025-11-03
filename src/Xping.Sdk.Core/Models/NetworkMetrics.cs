/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

#nullable enable

namespace Xping.Sdk.Core.Models;

/// <summary>
/// Represents network reliability metrics collected during test execution.
/// These metrics provide direct signals about network quality without inferring from location.
/// </summary>
public sealed class NetworkMetrics
{
    /// <summary>
    /// Gets or sets the network latency in milliseconds (ping to Xping API).
    /// </summary>
    public int? LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the network is available.
    /// </summary>
    public bool? IsOnline { get; set; }

    /// <summary>
    /// Gets or sets the connection type (e.g., "WiFi", "Ethernet", "Cellular", "Unknown").
    /// </summary>
    public string? ConnectionType { get; set; }

    /// <summary>
    /// Gets or sets the packet loss percentage if measurable.
    /// </summary>
    public int? PacketLossPercent { get; set; }
}
