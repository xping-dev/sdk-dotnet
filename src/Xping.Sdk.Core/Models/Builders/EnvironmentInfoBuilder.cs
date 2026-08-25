/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.ObjectModel;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Models.Builders;

/// <summary>
/// Builder for constructing immutable <see cref="EnvironmentInfo"/> instances.
/// </summary>
public sealed class EnvironmentInfoBuilder
{
    private string _machineName;
    private string _operatingSystem;
    private string _runtimeVersion;
    private string _framework;
    private string _environmentName;
    private bool _isCIEnvironment;
    private NetworkMetrics? _networkMetrics;
    private TimeSpan? _utcOffset;
    private string? _timeZoneId;
    private readonly Dictionary<string, string> _customProperties;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentInfoBuilder"/> class.
    /// </summary>
    public EnvironmentInfoBuilder()
    {
        _machineName = string.Empty;
        _operatingSystem = string.Empty;
        _runtimeVersion = string.Empty;
        _framework = string.Empty;
        _environmentName = string.Empty;
        _customProperties = [];
    }

    /// <summary>
    /// Sets the machine name.
    /// </summary>
    public EnvironmentInfoBuilder WithMachineName(string machineName)
    {
        _machineName = machineName;
        return this;
    }

    /// <summary>
    /// Sets the operating system.
    /// </summary>
    public EnvironmentInfoBuilder WithOperatingSystem(string operatingSystem)
    {
        _operatingSystem = operatingSystem;
        return this;
    }

    /// <summary>
    /// Sets the runtime version.
    /// </summary>
    public EnvironmentInfoBuilder WithRuntimeVersion(string runtimeVersion)
    {
        _runtimeVersion = runtimeVersion;
        return this;
    }

    /// <summary>
    /// Sets the framework.
    /// </summary>
    public EnvironmentInfoBuilder WithFramework(string framework)
    {
        _framework = framework;
        return this;
    }

    /// <summary>
    /// Sets the environment name.
    /// </summary>
    public EnvironmentInfoBuilder WithEnvironmentName(string environmentName)
    {
        _environmentName = environmentName;
        return this;
    }

    /// <summary>
    /// Sets whether this is a CI environment.
    /// </summary>
    public EnvironmentInfoBuilder WithIsCIEnvironment(bool isCIEnvironment)
    {
        _isCIEnvironment = isCIEnvironment;
        return this;
    }

    /// <summary>
    /// Sets the network metrics.
    /// </summary>
    public EnvironmentInfoBuilder WithNetworkMetrics(NetworkMetrics? networkMetrics)
    {
        _networkMetrics = networkMetrics;
        return this;
    }

    /// <summary>
    /// Sets the machine's local time zone.
    /// </summary>
    /// <param name="utcOffset">
    /// The offset from UTC, or <see langword="null"/> when it could not be determined.
    /// </param>
    /// <param name="timeZoneId">
    /// The zone identifier, or <see langword="null"/> when it could not be determined.
    /// </param>
    /// <returns>This builder.</returns>
    /// <remarks>
    /// Both are passed together because neither is worth much alone: the offset without the zone
    /// cannot distinguish a daylight-saving shift from a machine that moved, and the zone without the
    /// offset requires a time zone database the reader may not have.
    /// </remarks>
    public EnvironmentInfoBuilder WithLocalTimeZone(TimeSpan? utcOffset, string? timeZoneId)
    {
        _utcOffset = utcOffset;
        _timeZoneId = timeZoneId;
        return this;
    }

    /// <summary>
    /// Adds a custom property.
    /// </summary>
    public EnvironmentInfoBuilder AddCustomProperty(string key, string value)
    {
        if (!string.IsNullOrEmpty(key))
        {
            _customProperties[key] = value;
        }
        return this;
    }

    /// <summary>
    /// Adds multiple custom properties.
    /// </summary>
    public EnvironmentInfoBuilder AddCustomProperties(IDictionary<string, string> properties)
    {
        foreach (var kvp in properties.RequireNotNull())
        {
            _customProperties[kvp.Key] = kvp.Value;
        }

        return this;
    }

    /// <summary>
    /// Builds an immutable <see cref="EnvironmentInfo"/> instance.
    /// </summary>
    public EnvironmentInfo Build()
    {
        return new EnvironmentInfo(
            machineName: _machineName,
            operatingSystem: _operatingSystem,
            runtimeVersion: _runtimeVersion,
            framework: _framework,
            environmentName: _environmentName,
            isCIEnvironment: _isCIEnvironment,
            networkMetrics: _networkMetrics,
            utcOffset: _utcOffset,
            timeZoneId: _timeZoneId,
            customProperties: new ReadOnlyDictionary<string, string>(_customProperties));
    }

    /// <summary>
    /// Resets the builder to its initial state.
    /// </summary>
    public EnvironmentInfoBuilder Reset()
    {
        _machineName = string.Empty;
        _operatingSystem = string.Empty;
        _runtimeVersion = string.Empty;
        _framework = string.Empty;
        _environmentName = string.Empty;
        _isCIEnvironment = false;
        _networkMetrics = null;
        _utcOffset = null;
        _timeZoneId = null;
        _customProperties.Clear();
        return this;
    }
}
