/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Environments;

namespace Xping.Sdk.Core.Services.Environment.Internals;

/// <summary>
/// A no-op implementation of <see cref="IEnvironmentDetector"/> used when the SDK
/// is disabled or configuration validation fails. Returns minimal environment information
/// without performing expensive detection operations.
/// </summary>
internal sealed class NoOpEnvironmentDetector : IEnvironmentDetector
{
    /// <inheritdoc/>
    public string MachineName => System.Environment.MachineName;

    /// <inheritdoc/>
    public string OperatingSystem => System.Environment.OSVersion.ToString();

    /// <inheritdoc/>
    public string RuntimeVersion => System.Environment.Version.ToString();

    /// <inheritdoc/>
    public string Framework =>
        System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

    /// <inheritdoc/>
    public bool IsCiEnvironment => false;

    /// <inheritdoc/>
    public bool IsContainer => false;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> CustomProperties =>
        new Dictionary<string, string>();

    /// <inheritdoc/>
    public string EnvironmentName => "Local";

    /// <inheritdoc/>
    public Task<EnvironmentInfo> BuildEnvironmentInfoAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new EnvironmentInfo
        {
            MachineName = MachineName,
            OperatingSystem = OperatingSystem,
            RuntimeVersion = RuntimeVersion,
            Framework = Framework,
            IsCIEnvironment = IsCiEnvironment,
            CustomProperties = CustomProperties,
            EnvironmentName = EnvironmentName
        });
    }
}
