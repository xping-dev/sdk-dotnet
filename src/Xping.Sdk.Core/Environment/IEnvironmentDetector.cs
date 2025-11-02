// <copyright file="IEnvironmentDetector.cs" company="Xping.io">
// Copyright (c) 2025 Xping.io. Licensed under the MIT License.
// </copyright>

namespace Xping.Sdk.Core.Environment;

using Xping.Sdk.Core.Models;

/// <summary>
/// Provides functionality to detect and collect environment information.
/// </summary>
public interface IEnvironmentDetector
{
    /// <summary>
    /// Detects the current environment information including OS, runtime, CI platform, and custom properties.
    /// </summary>
    /// <returns>An <see cref="EnvironmentInfo"/> object containing detected environment details.</returns>
    EnvironmentInfo Detect();
}
