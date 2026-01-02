/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Environment;

using Configuration;
using Models;

/// <summary>
/// Provides functionality to detect and collect environment information.
/// </summary>
public interface IEnvironmentDetector
{
    /// <summary>
    /// Detects the current environment information including OS, runtime, CI platform, and custom properties.
    /// </summary>
    /// <param name="configuration">The Xping configuration.</param>
    /// <returns>An <see cref="EnvironmentInfo"/> object containing detected environment details.</returns>
    EnvironmentInfo Detect(XpingConfiguration configuration);
}
