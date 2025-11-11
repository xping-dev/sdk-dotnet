/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Diagnostics;

/// <summary>
/// Logger interface for Xping SDK diagnostics.
/// Implementations should be thread-safe as they may be called concurrently.
/// </summary>
public interface IXpingLogger
{
    /// <summary>
    /// Logs an error message. These indicate failures that prevent the SDK from functioning correctly.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    void LogError(string message);

    /// <summary>
    /// Logs a warning message. These indicate potential issues that don't prevent operation but may need attention.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    void LogWarning(string message);

    /// <summary>
    /// Logs an informational message. These provide general status updates about SDK operations.
    /// </summary>
    /// <param name="message">The informational message to log.</param>
    void LogInfo(string message);

    /// <summary>
    /// Logs a debug message. These provide detailed diagnostic information for troubleshooting.
    /// </summary>
    /// <param name="message">The debug message to log.</param>
    void LogDebug(string message);

    /// <summary>
    /// Determines whether logging is enabled for the specified level.
    /// </summary>
    /// <param name="level">The log level to check.</param>
    /// <returns>True if logging is enabled for the specified level; otherwise, false.</returns>
    bool IsEnabled(XpingLogLevel level);
}
