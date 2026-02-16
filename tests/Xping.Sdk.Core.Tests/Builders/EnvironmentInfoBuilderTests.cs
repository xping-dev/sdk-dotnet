/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Environments;

namespace Xping.Sdk.Core.Tests.Builders;

public sealed class EnvironmentInfoBuilderTests
{
    // ---------------------------------------------------------------------------
    // Build — defaults
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_ShouldReturnEnvironmentInfo_WithEmptyStringDefaults()
    {
        // Arrange & Act
        var info = new EnvironmentInfoBuilder().Build();

        // Assert
        Assert.Equal(string.Empty, info.MachineName);
        Assert.Equal(string.Empty, info.OperatingSystem);
        Assert.Equal(string.Empty, info.RuntimeVersion);
        Assert.Equal(string.Empty, info.Framework);
        Assert.Equal(string.Empty, info.EnvironmentName);
        Assert.False(info.IsCIEnvironment);
        Assert.Null(info.NetworkMetrics);
        Assert.Empty(info.CustomProperties);
    }

    // ---------------------------------------------------------------------------
    // With* methods
    // ---------------------------------------------------------------------------

    [Fact]
    public void WithMachineName_ShouldSetMachineName()
    {
        var info = new EnvironmentInfoBuilder().WithMachineName("build-agent-01").Build();
        Assert.Equal("build-agent-01", info.MachineName);
    }

    [Fact]
    public void WithOperatingSystem_ShouldSetOperatingSystem()
    {
        var info = new EnvironmentInfoBuilder().WithOperatingSystem("Ubuntu 22.04").Build();
        Assert.Equal("Ubuntu 22.04", info.OperatingSystem);
    }

    [Fact]
    public void WithRuntimeVersion_ShouldSetRuntimeVersion()
    {
        var info = new EnvironmentInfoBuilder().WithRuntimeVersion(".NET 9.0.0").Build();
        Assert.Equal(".NET 9.0.0", info.RuntimeVersion);
    }

    [Fact]
    public void WithFramework_ShouldSetFramework()
    {
        var info = new EnvironmentInfoBuilder().WithFramework("net9.0").Build();
        Assert.Equal("net9.0", info.Framework);
    }

    [Fact]
    public void WithEnvironmentName_ShouldSetEnvironmentName()
    {
        var info = new EnvironmentInfoBuilder().WithEnvironmentName("CI").Build();
        Assert.Equal("CI", info.EnvironmentName);
    }

    [Fact]
    public void WithIsCIEnvironment_True_ShouldSetFlag()
    {
        var info = new EnvironmentInfoBuilder().WithIsCIEnvironment(true).Build();
        Assert.True(info.IsCIEnvironment);
    }

    [Fact]
    public void WithIsCIEnvironment_False_ShouldClearFlag()
    {
        var info = new EnvironmentInfoBuilder()
            .WithIsCIEnvironment(true)
            .WithIsCIEnvironment(false)
            .Build();

        Assert.False(info.IsCIEnvironment);
    }

    [Fact]
    public void WithNetworkMetrics_ShouldSetNetworkMetrics()
    {
        var metrics = new NetworkMetricsBuilder().WithLatencyMs(50).Build();
        var info = new EnvironmentInfoBuilder().WithNetworkMetrics(metrics).Build();

        Assert.NotNull(info.NetworkMetrics);
        Assert.Equal(50, info.NetworkMetrics!.LatencyMs);
    }

    [Fact]
    public void WithNetworkMetrics_Null_ShouldSetNetworkMetricsToNull()
    {
        var info = new EnvironmentInfoBuilder()
            .WithNetworkMetrics(new NetworkMetricsBuilder().Build())
            .WithNetworkMetrics(null)
            .Build();

        Assert.Null(info.NetworkMetrics);
    }

    // ---------------------------------------------------------------------------
    // AddCustomProperty
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddCustomProperty_ShouldAddProperty()
    {
        var info = new EnvironmentInfoBuilder().AddCustomProperty("Region", "us-east-1").Build();
        Assert.True(info.CustomProperties.ContainsKey("Region"));
        Assert.Equal("us-east-1", info.CustomProperties["Region"]);
    }

    [Fact]
    public void AddCustomProperty_EmptyKey_ShouldNotAdd()
    {
        var info = new EnvironmentInfoBuilder().AddCustomProperty(string.Empty, "value").Build();
        Assert.Empty(info.CustomProperties);
    }

    [Fact]
    public void AddCustomProperty_NullKey_ShouldNotAdd()
    {
        var info = new EnvironmentInfoBuilder().AddCustomProperty(null!, "value").Build();
        Assert.Empty(info.CustomProperties);
    }

    [Fact]
    public void AddCustomProperty_OverwritesExistingKey()
    {
        var info = new EnvironmentInfoBuilder()
            .AddCustomProperty("Key", "first")
            .AddCustomProperty("Key", "second")
            .Build();

        Assert.Equal("second", info.CustomProperties["Key"]);
    }

    // ---------------------------------------------------------------------------
    // AddCustomProperties
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddCustomProperties_ShouldAddAllEntries()
    {
        var props = new Dictionary<string, string> { { "A", "1" }, { "B", "2" } };
        var info = new EnvironmentInfoBuilder().AddCustomProperties(props).Build();

        Assert.Equal(2, info.CustomProperties.Count);
        Assert.Equal("1", info.CustomProperties["A"]);
        Assert.Equal("2", info.CustomProperties["B"]);
    }

    [Fact]
    public void AddCustomProperties_NullDictionary_ShouldThrowArgumentNullException()
    {
        var builder = new EnvironmentInfoBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddCustomProperties(null!));
    }

    // ---------------------------------------------------------------------------
    // Reset
    // ---------------------------------------------------------------------------

    [Fact]
    public void Reset_ShouldRestoreAllDefaults()
    {
        // Arrange
        var builder = new EnvironmentInfoBuilder()
            .WithMachineName("host")
            .WithOperatingSystem("Windows")
            .WithIsCIEnvironment(true)
            .AddCustomProperty("k", "v");

        // Act
        builder.Reset();
        var info = builder.Build();

        // Assert
        Assert.Equal(string.Empty, info.MachineName);
        Assert.Equal(string.Empty, info.OperatingSystem);
        Assert.False(info.IsCIEnvironment);
        Assert.Empty(info.CustomProperties);
    }

    [Fact]
    public void Reset_ShouldReturnBuilderInstance_ForChaining()
    {
        var builder = new EnvironmentInfoBuilder();
        Assert.Same(builder, builder.Reset());
    }

    // ---------------------------------------------------------------------------
    // Fluent chain
    // ---------------------------------------------------------------------------

    [Fact]
    public void Build_ShouldSupportFullFluentChain()
    {
        var metrics = new NetworkMetricsBuilder().WithIsOnline(true).Build();
        var info = new EnvironmentInfoBuilder()
            .WithMachineName("ci-runner")
            .WithOperatingSystem("macOS 14")
            .WithRuntimeVersion(".NET 9")
            .WithFramework("net9.0")
            .WithEnvironmentName("CI")
            .WithIsCIEnvironment(true)
            .WithNetworkMetrics(metrics)
            .AddCustomProperty("branch", "main")
            .Build();

        Assert.Equal("ci-runner", info.MachineName);
        Assert.Equal("macOS 14", info.OperatingSystem);
        Assert.Equal(".NET 9", info.RuntimeVersion);
        Assert.Equal("net9.0", info.Framework);
        Assert.Equal("CI", info.EnvironmentName);
        Assert.True(info.IsCIEnvironment);
        Assert.NotNull(info.NetworkMetrics);
        Assert.Equal("main", info.CustomProperties["branch"]);
    }
}
