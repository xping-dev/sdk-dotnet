/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Diagnostics;

/// <summary>
/// Null logger implementation that discards all log messages.
/// Used when logging is disabled or no logger is configured.
/// </summary>
public sealed class XpingNullLogger : IXpingLogger
{
    private static readonly Lazy<XpingNullLogger> _instance = new(() => new XpingNullLogger());

    /// <summary>
    /// Gets a singleton instance of the null logger.
    /// </summary>
    public static XpingNullLogger Instance => _instance.Value;

    private XpingNullLogger()
    {
    }

    /// <inheritdoc/>
    public void LogError(string message)
    {
    }

    /// <inheritdoc/>
    public void LogWarning(string message)
    {
    }

    /// <inheritdoc/>
    public void LogInfo(string message)
    {
    }

    /// <inheritdoc/>
    public void LogDebug(string message)
    {
    }

    /// <inheritdoc/>
    public bool IsEnabled(XpingLogLevel level) => false;
}
