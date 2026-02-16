/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models.Environments;

/// <summary>
/// Immutable network reliability metrics collected during test execution.
/// These metrics provide direct signals about network quality without inferring from location.
/// Use <see cref="Builders.NetworkMetricsBuilder"/> to create instances.
/// </summary>
public sealed class NetworkMetrics
{
    internal const string UnknownConnectionType = "Unknown";

    /// <summary>
    /// Parameterless constructor for JSON deserialization.
    /// For creating instances in code, use <see cref="Builders.NetworkMetricsBuilder"/>.
    /// </summary>
    public NetworkMetrics()
    {
    }

    /// <summary>
    /// Internal constructor for builder.
    /// </summary>
    internal NetworkMetrics(
        int? latencyMs,
        bool? isOnline,
        string connectionType,
        int? packetLossPercent)
    {
        LatencyMs = latencyMs;
        IsOnline = isOnline;
        ConnectionType = connectionType;
        PacketLossPercent = packetLossPercent;
    }

    /// <summary>
    /// Gets the network latency in milliseconds (ping to Xping API).
    /// </summary>
    public int? LatencyMs { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the network is available.
    /// </summary>
    public bool? IsOnline { get; private set; }

    /// <summary>
    /// Gets the connection type (e.g., "Wi-Fi", "Ethernet", "Cellular", "Unknown").
    /// </summary>
    public string ConnectionType { get; private set; } = UnknownConnectionType;

    /// <summary>
    /// Gets the packet loss percentage if measurable.
    /// </summary>
    public int? PacketLossPercent { get; private set; }
}
