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
    /// Gets the deployed environment the tests targeted: the configured
    /// <c>Environment</c>, or <c>"Default"</c> when none was configured.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing is inferred from the host. Every distinct environment name in the system traces to a
    /// human decision, because confidence is scored per (test, environment) and each additional name
    /// splits that test's history. A CI run and a local run of the same suite therefore share an
    /// environment - what separates them is the trust placed in the revision under test, which
    /// <see cref="IsCiEnvironment"/> and the <c>ExecutionContext</c> custom property record
    /// diagnostically rather than by fragmenting the sample.
    /// </para>
    /// </remarks>
    string EnvironmentName { get; }

    /// <summary>
    /// Builds a complete EnvironmentInfo object with all detected values.
    /// This is a convenience method that aggregates all properties.
    /// </summary>
    /// <remarks>
    /// Observing the token is optional, and neither implementation shipped here does. Both read
    /// cached values and the local clock, which leaves nothing for a token to interrupt. The
    /// parameter exists for an implementation that does reach out for its data — without it, adding
    /// one would be a signature change across every caller.
    /// </remarks>
    /// <param name="cancellationToken">
    /// Cancellation token, honoured only by implementations with something to cancel.
    /// </param>
    /// <returns>Complete environment information.</returns>
    Task<EnvironmentInfo> BuildEnvironmentInfoAsync(
        CancellationToken cancellationToken = default);
}
