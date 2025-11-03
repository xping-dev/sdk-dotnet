/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

#pragma warning disable CA1031 // Do not catch general exception types - we want graceful degradation
#nullable enable

namespace Xping.Sdk.Core.Network;

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xping.Sdk.Core.Models;

/// <summary>
/// Default implementation of <see cref="INetworkMetricsCollector"/> that collects network reliability metrics.
/// </summary>
public sealed class NetworkMetricsCollector : INetworkMetricsCollector
{
    private const int DefaultTimeout = 5000; // 5 seconds
    private const int PingCount = 4; // Number of pings for latency calculation

    /// <inheritdoc/>
    public async Task<NetworkMetrics> CollectAsync(string apiEndpoint, CancellationToken cancellationToken = default)
    {
        var metrics = new NetworkMetrics
        {
            IsOnline = await CheckIsOnlineAsync(cancellationToken).ConfigureAwait(false),
            ConnectionType = DetectConnectionType()
        };

        // Only attempt latency measurement if we're online
        if (metrics.IsOnline == true)
        {
            try
            {
                var (latency, packetLoss) =
                    await MeasureLatencyAsync(apiEndpoint, cancellationToken).ConfigureAwait(false);
                metrics.LatencyMs = latency;
                metrics.PacketLossPercent = packetLoss;
            }
            catch
            {
                // If latency measurement fails, leave as null
            }
        }

        return metrics;
    }

    /// <summary>
    /// Checks if the network is available.
    /// </summary>
    private static async Task<bool?> CheckIsOnlineAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Check if any network interface is up
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return false;
            }

            // Try to resolve a reliable DNS name (Google's DNS)
            try
            {
                var addresses = await Dns.GetHostAddressesAsync("dns.google").ConfigureAwait(false);
                return addresses.Length > 0;
            }
            catch
            {
                return false;
            }
        }
        catch
        {
            return null; // Unable to determine
        }
    }

    /// <summary>
    /// Detects the connection type (WiFi, Ethernet, Cellular, Unknown).
    /// </summary>
    private static string DetectConnectionType()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                           ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                           ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToArray();

            if (interfaces.Length == 0)
            {
                return "Unknown";
            }

            // Prioritize connection types
            foreach (var ni in interfaces)
            {
                switch (ni.NetworkInterfaceType)
                {
                    case NetworkInterfaceType.Ethernet:
                    case NetworkInterfaceType.Ethernet3Megabit:
                    case NetworkInterfaceType.FastEthernetT:
                    case NetworkInterfaceType.FastEthernetFx:
                    case NetworkInterfaceType.GigabitEthernet:
                        return "Ethernet";

                    case NetworkInterfaceType.Wireless80211:
                        return "WiFi";

                    case NetworkInterfaceType.Wwanpp:
                    case NetworkInterfaceType.Wwanpp2:
                        return "Cellular";
                }
            }

            // If we have active interfaces but couldn't classify them
            return interfaces.Length > 0 ? "Other" : "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// Measures latency to the API endpoint using TCP connection.
    /// </summary>
    private static async Task<(int? latencyMs, int? packetLoss)> MeasureLatencyAsync(
        string apiEndpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract hostname from URL
            var uri = new Uri(apiEndpoint);
            var hostname = uri.Host;
            var port = uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80);

            // Resolve hostname to IP
            var addresses = await Dns.GetHostAddressesAsync(hostname).ConfigureAwait(false);
            if (addresses.Length == 0)
            {
                return (null, null);
            }

            var ipAddress = addresses.First(a => a.AddressFamily == AddressFamily.InterNetwork ||
                                                 a.AddressFamily == AddressFamily.InterNetworkV6);

            // Measure latency using Ping if available, otherwise use TCP connect
            var latencies = new List<int>();
            var successCount = 0;

            for (int i = 0; i < PingCount; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var latency = await MeasureSinglePingAsync(ipAddress, cancellationToken).ConfigureAwait(false);
                    if (latency.HasValue)
                    {
                        latencies.Add(latency.Value);
                        successCount++;
                    }
                }
                catch
                {
                    // Failed ping, don't add to latencies
                }

                // Small delay between pings
                if (i < PingCount - 1)
                {
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
            }

            if (latencies.Count == 0)
            {
                return (null, null);
            }

            // Calculate average latency
            var avgLatency = (int)latencies.Average();

            // Calculate packet loss percentage
            var packetLoss = ((PingCount - successCount) * 100) / PingCount;

            return (avgLatency, packetLoss);
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>
    /// Measures a single ping using ICMP if available, falls back to TCP connect timing.
    /// </summary>
    private static async Task<int?> MeasureSinglePingAsync(IPAddress ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(DefaultTimeout);

            var reply = await ping.SendPingAsync(ipAddress, DefaultTimeout).ConfigureAwait(false);

            if (reply.Status == IPStatus.Success)
            {
                return (int)reply.RoundtripTime;
            }

            return null;
        }
        catch
        {
            // Ping might not be available or might be blocked
            // Fall back to TCP connection timing
            return await MeasureTcpConnectionLatencyAsync(ipAddress, 443, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Measures latency using TCP connection time (fallback when ICMP ping is unavailable).
    /// </summary>
    private static async Task<int?> MeasureTcpConnectionLatencyAsync(
        IPAddress ipAddress,
        int port,
        CancellationToken cancellationToken)
    {
        try
        {
            using var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.SendTimeout = DefaultTimeout;
            socket.ReceiveTimeout = DefaultTimeout;

            var sw = Stopwatch.StartNew();
            await socket.ConnectAsync(ipAddress, port).ConfigureAwait(false);
            sw.Stop();

            return (int)sw.ElapsedMilliseconds;
        }
        catch
        {
            return null;
        }
    }
}
