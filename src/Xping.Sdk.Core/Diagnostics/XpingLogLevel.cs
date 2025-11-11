/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Diagnostics;

/// <summary>
/// Log levels for SDK diagnostics output.
/// </summary>
public enum XpingLogLevel
{
    /// <summary>
    /// No logging output.
    /// </summary>
    None = 0,

    /// <summary>
    /// Only log errors and critical issues.
    /// Use in production when you only want to know about failures.
    /// </summary>
    Error = 1,

    /// <summary>
    /// Log warnings and errors.
    /// Use in production to track potential issues.
    /// </summary>
    Warning = 2,

    /// <summary>
    /// Log informational messages, warnings, and errors (default).
    /// Use in development and CI/CD to track SDK activity.
    /// </summary>
    Info = 3,

    /// <summary>
    /// Log detailed diagnostic information for troubleshooting.
    /// Use when debugging SDK issues or integration problems.
    /// </summary>
    Debug = 4,
}
