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
    /// Sets the machine's local time zone.
    /// </summary>
    /// <param name="utcOffset">
    /// The offset from UTC, or <see langword="null"/> when it could not be determined.
    /// </param>
    /// <param name="timeZoneId">
    /// The zone identifier, or <see langword="null"/> when it could not be determined.
    /// </param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">
    /// One of the two was supplied without the other.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Both or neither. Neither is worth anything alone: an offset with no zone cannot tell a
    /// daylight-saving shift apart from a machine that moved, and a zone with no offset needs a time
    /// zone database the reader may not have.
    /// </para>
    /// <para>
    /// Half a pair throws rather than being stored, because the alternative is worse than a loud
    /// failure. Analysis excludes a run it cannot place on a local clock, and it does so silently —
    /// the same way it treats a run that recorded no zone at all. A caller that set one field and
    /// forgot the other would get no error, no warning, and no findings, with nothing anywhere to
    /// say why. This is a caller mistake rather than a detection failure, so it is not covered by
    /// the rule that a run must never be taken down by telemetry: the detector reads both from one
    /// <see cref="TimeZoneInfo"/> and cannot produce a half pair.
    /// </para>
    /// </remarks>
    public EnvironmentInfoBuilder WithLocalTimeZone(TimeSpan? utcOffset, string? timeZoneId)
    {
        // Whitespace counts as absent. It reaches analysis as a zone that names nothing, which is
        // indistinguishable from having no zone and would be excluded just as quietly.
        bool hasZone = !string.IsNullOrWhiteSpace(timeZoneId);

        if (utcOffset.HasValue != hasZone)
        {
            throw new ArgumentException(
                "A local time zone is recorded as an offset and a zone identifier together, or not " +
                "at all. Supplying one without the other produces a run that analysis silently " +
                "excludes.",
                utcOffset.HasValue ? nameof(timeZoneId) : nameof(utcOffset));
        }

        _utcOffset = utcOffset;
        _timeZoneId = hasZone ? timeZoneId : null;
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
        _utcOffset = null;
        _timeZoneId = null;
        _customProperties.Clear();
        return this;
    }
}
