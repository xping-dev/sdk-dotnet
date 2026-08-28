/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.ObjectModel;

namespace Xping.Sdk.Core.Models.Environments;

/// <summary>
/// Immutable environment information where a test was executed.
/// </summary>
public sealed class EnvironmentInfo
{
    /// <summary>
    /// Parameterless constructor for JSON deserialization.
    /// </summary>
    public EnvironmentInfo()
    {
        MachineName = string.Empty;
        OperatingSystem = string.Empty;
        RuntimeVersion = string.Empty;
        Framework = string.Empty;
        EnvironmentName = string.Empty;
        CustomProperties = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
    }

    /// <summary>
    /// Internal constructor for builder or manual construction.
    /// </summary>
    internal EnvironmentInfo(
        string machineName,
        string operatingSystem,
        string runtimeVersion,
        string framework,
        string environmentName,
        bool isCIEnvironment,
        TimeSpan? utcOffset,
        string? timeZoneId,
        IReadOnlyDictionary<string, string> customProperties)
    {
        MachineName = machineName;
        OperatingSystem = operatingSystem;
        RuntimeVersion = runtimeVersion;
        Framework = framework;
        EnvironmentName = environmentName;
        IsCIEnvironment = isCIEnvironment;
        UtcOffset = utcOffset;
        TimeZoneId = timeZoneId;
        CustomProperties = customProperties;
    }

    /// <summary>
    /// Gets the name of the machine where the test was executed.
    /// </summary>
    public string MachineName { get; init; }

    /// <summary>
    /// Gets the operating system information (e.g., "Windows 11", "macOS 14.0", "Ubuntu 22.04").
    /// </summary>
    public string OperatingSystem { get; init; }

    /// <summary>
    /// Gets the .NET runtime version (e.g., ".NET 8.0.0").
    /// </summary>
    public string RuntimeVersion { get; init; }

    /// <summary>
    /// Gets the test framework name and version (e.g., ".NET").
    /// </summary>
    public string Framework { get; init; }

    /// <summary>
    /// Gets the environment name (e.g., "Local", "CI", "Staging", "Production").
    /// </summary>
    public string EnvironmentName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the test was executed in a CI/CD environment.
    /// </summary>
    public bool IsCIEnvironment { get; init; }

    /// <summary>
    /// Gets the machine's offset from UTC when the session started, or <see langword="null"/> when it
    /// was not captured.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Timestamps elsewhere in a session are UTC, which is unambiguous but says nothing about when a
    /// run happened <i>locally</i>. "Fails overnight" and "fails after lunch" are claims about the
    /// developer's clock, and on a machine that is not on UTC they cannot be recovered from the
    /// instant alone.
    /// </para>
    /// <para>
    /// <see langword="null"/> means nothing was recorded — either the session predates this field, or
    /// the machine had no usable time zone. It never means UTC. Analysis that treated the two alike
    /// would invent the very measurement it is trying to make.
    /// </para>
    /// <para>
    /// Captured once, when the environment is detected. A run that crosses a daylight-saving
    /// transition keeps the offset it started at; the shift shows up as a difference between
    /// sessions, which is where it is analysed.
    /// </para>
    /// </remarks>
    public TimeSpan? UtcOffset { get; init; }

    /// <summary>
    /// Gets the machine's time zone identifier, or <see langword="null"/> when it was not captured.
    /// </summary>
    /// <remarks>
    /// A Windows identifier on Windows ("W. Europe Standard Time") and an IANA one elsewhere
    /// ("Europe/Berlin"); the two are not translated, because nothing needs them to be. Its use is to
    /// tell one machine's offset change apart from another's: two sessions with different offsets are
    /// a daylight-saving shift only if they agree on the zone.
    /// </remarks>
    public string? TimeZoneId { get; init; }

    /// <summary>
    /// Gets the custom properties for additional environment information.
    /// </summary>
    public IReadOnlyDictionary<string, string> CustomProperties { get; init; }
}
