/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Services.Environment;

/// <summary>
/// Abstracts access to process environment variables, enabling testability of components
/// that read from the environment (e.g. CI/CD-specific detectors).
/// </summary>
public interface IEnvironmentVariableProvider
{
    /// <summary>
    /// Returns the value of the named environment variable, or <c>null</c> if the variable
    /// is not set or cannot be read.
    /// </summary>
    /// <param name="name">The name of the environment variable.</param>
    /// <returns>The variable value, or <c>null</c>.</returns>
    string? GetVariable(string name);
}
