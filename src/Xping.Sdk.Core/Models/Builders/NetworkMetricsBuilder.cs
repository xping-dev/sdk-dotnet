/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Environments;

namespace Xping.Sdk.Core.Models.Builders;

/// <summary>
/// Builder for constructing immutable <see cref="NetworkMetrics"/> instances.
/// </summary>
public sealed class NetworkMetricsBuilder
{
    private int? _latencyMs;
    private bool? _isOnline;
    private string _connectionType = NetworkMetrics.UnknownConnectionType;
    private int? _packetLossPercent;

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkMetricsBuilder"/> class.
    /// </summary>
    public NetworkMetricsBuilder()
    {
    }

    /// <summary>
    /// Sets the network latency in milliseconds.
    /// </summary>
    public NetworkMetricsBuilder WithLatencyMs(int? latencyMs)
    {
        _latencyMs = latencyMs;
        return this;
    }

    /// <summary>
    /// Sets whether the network is online.
    /// </summary>
    public NetworkMetricsBuilder WithIsOnline(bool? isOnline)
    {
        _isOnline = isOnline;
        return this;
    }

    /// <summary>
    /// Sets the connection type (e.g., "Wi-Fi", "Ethernet", "Cellular").
    /// </summary>
    public NetworkMetricsBuilder WithConnectionType(string connectionType)
    {
        _connectionType = connectionType;
        return this;
    }

    /// <summary>
    /// Sets the packet loss percentage.
    /// </summary>
    public NetworkMetricsBuilder WithPacketLossPercent(int? packetLossPercent)
    {
        _packetLossPercent = packetLossPercent;
        return this;
    }

    /// <summary>
    /// Builds an immutable <see cref="NetworkMetrics"/> instance.
    /// </summary>
    public NetworkMetrics Build()
    {
        return new NetworkMetrics(
            latencyMs: _latencyMs,
            isOnline: _isOnline,
            connectionType: _connectionType,
            packetLossPercent: _packetLossPercent);
    }

    /// <summary>
    /// Resets the builder to its initial state.
    /// </summary>
    public NetworkMetricsBuilder Reset()
    {
        _latencyMs = null;
        _isOnline = null;
        _connectionType = NetworkMetrics.UnknownConnectionType;
        _packetLossPercent = null;
        return this;
    }
}
