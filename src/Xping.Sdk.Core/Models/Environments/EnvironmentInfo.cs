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
        NetworkMetrics? networkMetrics,
        IReadOnlyDictionary<string, string> customProperties)
    {
        MachineName = machineName;
        OperatingSystem = operatingSystem;
        RuntimeVersion = runtimeVersion;
        Framework = framework;
        EnvironmentName = environmentName;
        IsCIEnvironment = isCIEnvironment;
        NetworkMetrics = networkMetrics;
        CustomProperties = customProperties;
    }

    /// <summary>
    /// Gets the name of the machine where the test was executed.
    /// </summary>
    public string MachineName { get; private set; }

    /// <summary>
    /// Gets the operating system information (e.g., "Windows 11", "macOS 14.0", "Ubuntu 22.04").
    /// </summary>
    public string OperatingSystem { get; private set; }

    /// <summary>
    /// Gets the .NET runtime version (e.g., ".NET 8.0.0").
    /// </summary>
    public string RuntimeVersion { get; private set; }

    /// <summary>
    /// Gets the test framework name and version (e.g., ".NET").
    /// </summary>
    public string Framework { get; private set; }

    /// <summary>
    /// Gets the environment name (e.g., "Local", "CI", "Staging", "Production").
    /// </summary>
    public string EnvironmentName { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the test was executed in a CI/CD environment.
    /// </summary>
    public bool IsCIEnvironment { get; private set; }

    /// <summary>
    /// Gets the network reliability metrics collected during test execution.
    /// This property is only populated when a network metrics collection is enabled.
    /// </summary>
    public NetworkMetrics? NetworkMetrics { get; private set; }

    /// <summary>
    /// Gets the custom properties for additional environment information.
    /// </summary>
    public IReadOnlyDictionary<string, string> CustomProperties { get; private set; }
}
