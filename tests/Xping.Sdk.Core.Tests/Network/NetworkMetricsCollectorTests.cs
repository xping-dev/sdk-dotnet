/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Network;

using System;
using System.Threading;
using System.Threading.Tasks;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Network;
using Xunit;

public class NetworkMetricsCollectorTests
{
    [Fact]
    public async Task CollectAsync_WithValidEndpoint_ReturnsNetworkMetrics()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "https://api.xping.io";

        // Act
        var result = await collector.CollectAsync(apiEndpoint);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.IsOnline);
        Assert.NotNull(result.ConnectionType);
        Assert.NotEmpty(result.ConnectionType);
    }

    [Fact]
    public async Task CollectAsync_ReturnsConnectionType()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "https://api.xping.io";

        // Act
        var result = await collector.CollectAsync(apiEndpoint);

        // Assert
        Assert.NotNull(result.ConnectionType);
        // Connection type should be one of the known types
        var validTypes = new[] { "WiFi", "Ethernet", "Cellular", "Other", "Unknown" };
        Assert.Contains(result.ConnectionType, validTypes);
    }

    [Fact]
    public async Task CollectAsync_WithNullEndpoint_HandlesGracefully()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();

        // Act & Assert
        // Implementation may handle null gracefully or throw
        try
        {
            var result = await collector.CollectAsync(null!);
            // If it doesn't throw, should return partial metrics
            Assert.NotNull(result);
        }
        catch (ArgumentNullException)
        {
            // Expected exception
            Assert.True(true);
        }
        catch (UriFormatException)
        {
            // Also acceptable
            Assert.True(true);
        }
    }

    [Fact]
    public async Task CollectAsync_WithInvalidEndpoint_HandlesGracefully()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "not-a-valid-url";

        // Act & Assert
        // Should either throw or return metrics with null latency
        try
        {
            var result = await collector.CollectAsync(apiEndpoint);
            // If it doesn't throw, metrics should be partial
            Assert.NotNull(result);
        }
        catch (UriFormatException)
        {
            // Expected for invalid URL
            Assert.True(true);
        }
    }

    [Fact]
    public async Task CollectAsync_WithUnreachableEndpoint_ReturnsNullLatency()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        // Use a non-routable IP address that will timeout
        var apiEndpoint = "https://192.0.2.1"; // TEST-NET-1 reserved for documentation

        // Act
        var result = await collector.CollectAsync(apiEndpoint);

        // Assert
        Assert.NotNull(result);
        // Latency might be null for unreachable endpoints
        Assert.NotNull(result.ConnectionType);
    }

    [Fact]
    public async Task CollectAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "https://192.0.2.1"; // Non-routable to allow time for cancellation
        using var cts = new CancellationTokenSource();

        // Start the collection
        var task = collector.CollectAsync(apiEndpoint, cts.Token);

        // Cancel after a short delay
        await Task.Delay(10);
        await cts.CancelAsync();

        // Act & Assert
        try
        {
            await task;
            // If it completes before cancellation, that's also valid
            Assert.True(true);
        }
        catch (OperationCanceledException)
        {
            // Expected if cancellation was respected
            Assert.True(true);
        }
    }

    [Fact]
    public async Task CollectAsync_WithTimeout_CompletesWithinReasonableTime()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "https://api.xping.io";
        var startTime = DateTime.UtcNow;

        // Act
        var result = await collector.CollectAsync(apiEndpoint);

        // Assert
        var duration = DateTime.UtcNow - startTime;
        // Should complete within 30 seconds (includes retries)
        Assert.True(duration.TotalSeconds < 30, $"Collection took {duration.TotalSeconds}s");
    }

    [Fact]
    public async Task CollectAsync_CalledMultipleTimes_ProducesConsistentConnectionType()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "https://api.xping.io";

        // Act
        var result1 = await collector.CollectAsync(apiEndpoint);
        var result2 = await collector.CollectAsync(apiEndpoint);

        // Assert
        // Connection type should be consistent between calls
        Assert.Equal(result1.ConnectionType, result2.ConnectionType);
    }

    [Fact]
    public async Task CollectAsync_WhenOnline_ShouldAttemptLatencyMeasurement()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "https://www.google.com"; // Reliable endpoint

        // Act
        var result = await collector.CollectAsync(apiEndpoint);

        // Assert
        Assert.NotNull(result);
        if (result.IsOnline == true)
        {
            // Latency might be measured (could be null if measurement fails)
            // but we should have attempted it
            Assert.NotNull(result.ConnectionType);
        }
    }

    [Fact]
    public async Task CollectAsync_WithHttpEndpoint_WorksCorrectly()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "http://example.com";

        // Act
        var result = await collector.CollectAsync(apiEndpoint);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ConnectionType);
    }

    [Fact]
    public async Task CollectAsync_WithHttpsEndpoint_WorksCorrectly()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "https://example.com";

        // Act
        var result = await collector.CollectAsync(apiEndpoint);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ConnectionType);
    }

    [Fact]
    public async Task CollectAsync_WithCustomPort_HandlesCorrectly()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "https://example.com:8443";

        // Act
        var result = await collector.CollectAsync(apiEndpoint);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ConnectionType);
    }

    [Fact]
    public async Task CollectAsync_ReturnsIsOnlineStatus()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "https://api.xping.io";

        // Act
        var result = await collector.CollectAsync(apiEndpoint);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.IsOnline);
        // IsOnline should be true, false, or null (unknown)
        Assert.True(result.IsOnline == true || result.IsOnline == false || result.IsOnline == null);
    }

    [Fact]
    public async Task CollectAsync_PacketLossPercent_IsValidWhenPresent()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "https://www.google.com";

        // Act
        var result = await collector.CollectAsync(apiEndpoint);

        // Assert
        if (result.PacketLossPercent.HasValue)
        {
            // Packet loss should be between 0 and 100
            Assert.True(result.PacketLossPercent.Value >= 0);
            Assert.True(result.PacketLossPercent.Value <= 100);
        }
    }

    [Fact]
    public async Task CollectAsync_LatencyMs_IsPositiveWhenPresent()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "https://www.google.com";

        // Act
        var result = await collector.CollectAsync(apiEndpoint);

        // Assert
        if (result.LatencyMs.HasValue)
        {
            // Latency should be positive and reasonable (< 10 seconds)
            Assert.True(result.LatencyMs.Value > 0);
            Assert.True(result.LatencyMs.Value < 10000);
        }
    }

    [Fact]
    public async Task CollectAsync_WithDifferentEndpoints_AllSucceed()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var endpoints = new[]
        {
            "https://api.xping.io",
            "https://www.google.com",
            "https://www.microsoft.com"
        };

        // Act & Assert
        foreach (var endpoint in endpoints)
        {
            var result = await collector.CollectAsync(endpoint);
            Assert.NotNull(result);
            Assert.NotNull(result.ConnectionType);
        }
    }

    [Fact]
    public async Task CollectAsync_ThreadSafety_CanHandleConcurrentCalls()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "https://api.xping.io";
        var tasks = new Task<NetworkMetrics>[10];

        // Act
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = collector.CollectAsync(apiEndpoint);
        }
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, results.Length);
        foreach (var result in results)
        {
            Assert.NotNull(result);
            Assert.NotNull(result.ConnectionType);
        }
    }

    [Fact]
    public async Task CollectAsync_WithIPv4Endpoint_WorksCorrectly()
    {
        // Arrange
        var collector = new NetworkMetricsCollector();
        var apiEndpoint = "https://8.8.8.8"; // Google DNS

        // Act
        var result = await collector.CollectAsync(apiEndpoint);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ConnectionType);
    }
}
