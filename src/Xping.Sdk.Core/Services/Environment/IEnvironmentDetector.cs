/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Environments;

namespace Xping.Sdk.Core.Services.Environment;

/// <summary>
/// Provides functionality to detect and collect environment information.
/// Exposes individual properties for granular access with lazy evaluation.
/// </summary>
public interface IEnvironmentDetector
{
    /// <summary>
    /// Gets the machine name where the application is running.
    /// </summary>
    string MachineName { get; }

    /// <summary>
    /// Gets the operating system information.
    /// </summary>
    string OperatingSystem { get; }

    /// <summary>
    /// Gets the .NET runtime version.
    /// </summary>
    string RuntimeVersion { get; }

    /// <summary>
    /// Gets the framework name (.NET, .NET Framework, .NET Core, etc.).
    /// </summary>
    string Framework { get; }

    /// <summary>
    /// Gets whether the application is running in a CI/CD environment.
    /// </summary>
    bool IsCiEnvironment { get; }

    /// <summary>
    /// Gets whether the application is running in a container (Docker, Kubernetes, etc.).
    /// </summary>
    bool IsContainer { get; }

    /// <summary>
    /// Gets custom properties collected from the environment
    /// (platform info, processor architecture, CI-specific metadata).
    /// </summary>
    IReadOnlyDictionary<string, string> CustomProperties { get; }

    /// <summary>
    /// Gets the environment name based on configuration and auto-detection rules.
    /// Priority:
    ///   XPING_ENVIRONMENT > AutoDetectCI > Options.Environment > ASPNETCORE_ENVIRONMENT/DOTNET_ENVIRONMENT > "Local"
    /// </summary>
    /// <remarks>
    /// The environment name is used in confidence calculations, which are performed both globally across all executions
    /// and per-environment to enable behavioral comparison and trend analysis.
    /// </remarks>
    string EnvironmentName { get; }

    /// <summary>
    /// Builds a complete EnvironmentInfo object with all detected values.
    /// This is a convenience method that aggregates all properties.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete environment information.</returns>
    Task<EnvironmentInfo> BuildEnvironmentInfoAsync(
        CancellationToken cancellationToken = default);
}
