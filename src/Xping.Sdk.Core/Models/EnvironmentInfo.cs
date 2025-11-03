/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

#nullable enable

namespace Xping.Sdk.Core.Models;

using System.Collections.Generic;

/// <summary>
/// Represents information about the environment where a test was executed.
/// </summary>
public sealed class EnvironmentInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentInfo"/> class.
    /// </summary>
    public EnvironmentInfo()
    {
        MachineName = string.Empty;
        OperatingSystem = string.Empty;
        RuntimeVersion = string.Empty;
        Framework = string.Empty;
        EnvironmentName = string.Empty;
        CustomProperties = new Dictionary<string, string>();
    }

    /// <summary>
    /// Gets or sets the name of the machine where the test was executed.
    /// </summary>
    public string MachineName { get; set; }

    /// <summary>
    /// Gets or sets the operating system information (e.g., "Windows 11", "macOS 14.0", "Ubuntu 22.04").
    /// </summary>
    public string OperatingSystem { get; set; }

    /// <summary>
    /// Gets or sets the .NET runtime version (e.g., ".NET 8.0.0").
    /// </summary>
    public string RuntimeVersion { get; set; }

    /// <summary>
    /// Gets or sets the test framework name and version (e.g., "NUnit 4.0.1", "xUnit 2.6.5").
    /// </summary>
    public string Framework { get; set; }

    /// <summary>
    /// Gets or sets the environment name (e.g., "Local", "CI", "Staging", "Production").
    /// </summary>
    public string EnvironmentName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the test was executed in a CI/CD environment.
    /// </summary>
    public bool IsCIEnvironment { get; set; }

    /// <summary>
    /// Gets or sets the network reliability metrics collected during test execution.
    /// This property is only populated when network metrics collection is enabled.
    /// </summary>
    public NetworkMetrics? NetworkMetrics { get; set; }

    /// <summary>
    /// Gets the custom properties for additional environment information.
    /// </summary>
    public Dictionary<string, string> CustomProperties { get; }
}
