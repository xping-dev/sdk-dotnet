/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;

namespace Xping.Sdk.Core.Tests.Builders;

public sealed class NetworkMetricsBuilderTests
{
    // ---------------------------------------------------------------------------
    // Build — defaults
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_ShouldReturnNetworkMetrics_WithUnknownConnectionType_ByDefault()
    {
        var metrics = new NetworkMetricsBuilder().Build();

        Assert.Equal("Unknown", metrics.ConnectionType);
        Assert.Null(metrics.LatencyMs);
        Assert.Null(metrics.IsOnline);
        Assert.Null(metrics.PacketLossPercent);
    }

    // ---------------------------------------------------------------------------
    // With* methods
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithLatencyMs_ShouldSetLatency()
    {
        var metrics = new NetworkMetricsBuilder().WithLatencyMs(120).Build();
        Assert.Equal(120, metrics.LatencyMs);
    }

    [Fact]
    public void WithLatencyMs_Null_ShouldSetNull()
    {
        var metrics = new NetworkMetricsBuilder().WithLatencyMs(50).WithLatencyMs(null).Build();
        Assert.Null(metrics.LatencyMs);
    }

    [Fact]
    public void WithIsOnline_True_ShouldSetTrue()
    {
        var metrics = new NetworkMetricsBuilder().WithIsOnline(true).Build();
        Assert.True(metrics.IsOnline);
    }

    [Fact]
    public void WithIsOnline_False_ShouldSetFalse()
    {
        var metrics = new NetworkMetricsBuilder().WithIsOnline(false).Build();
        Assert.False(metrics.IsOnline);
    }

    [Fact]
    public void WithIsOnline_Null_ShouldSetNull()
    {
        var metrics = new NetworkMetricsBuilder().WithIsOnline(true).WithIsOnline(null).Build();
        Assert.Null(metrics.IsOnline);
    }

    [Fact]
    public void WithConnectionType_ShouldSetConnectionType()
    {
        var metrics = new NetworkMetricsBuilder().WithConnectionType("Wi-Fi").Build();
        Assert.Equal("Wi-Fi", metrics.ConnectionType);
    }

    [Fact]
    public void WithPacketLossPercent_ShouldSetPacketLoss()
    {
        var metrics = new NetworkMetricsBuilder().WithPacketLossPercent(5).Build();
        Assert.Equal(5, metrics.PacketLossPercent);
    }

    [Fact]
    public void WithPacketLossPercent_Null_ShouldSetNull()
    {
        var metrics = new NetworkMetricsBuilder().WithPacketLossPercent(10).WithPacketLossPercent(null).Build();
        Assert.Null(metrics.PacketLossPercent);
    }

    // ---------------------------------------------------------------------------
    // Reset
    // ---------------------------------------------------------------------------

    [Fact]
    public void Reset_ShouldRestoreDefaults()
    {
        var builder = new NetworkMetricsBuilder()
            .WithLatencyMs(100)
            .WithIsOnline(false)
            .WithConnectionType("Cellular")
            .WithPacketLossPercent(20);

        builder.Reset();
        var metrics = builder.Build();

        Assert.Null(metrics.LatencyMs);
        Assert.Null(metrics.IsOnline);
        Assert.Equal("Unknown", metrics.ConnectionType);
        Assert.Null(metrics.PacketLossPercent);
    }

    [Fact]
    public void Reset_ShouldReturnBuilderInstance_ForChaining()
    {
        var builder = new NetworkMetricsBuilder();
        Assert.Same(builder, builder.Reset());
    }

    // ---------------------------------------------------------------------------
    // Fluent chain
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_ShouldSupportFullFluentChain()
    {
        var metrics = new NetworkMetricsBuilder()
            .WithLatencyMs(30)
            .WithIsOnline(true)
            .WithConnectionType("Ethernet")
            .WithPacketLossPercent(0)
            .Build();

        Assert.Equal(30, metrics.LatencyMs);
        Assert.True(metrics.IsOnline);
        Assert.Equal("Ethernet", metrics.ConnectionType);
        Assert.Equal(0, metrics.PacketLossPercent);
    }
}
