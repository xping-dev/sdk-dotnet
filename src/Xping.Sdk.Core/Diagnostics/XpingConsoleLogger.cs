/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Diagnostics;

using System;

/// <summary>
/// Console-based logger implementation that writes to stdout and stderr.
/// Thread-safe and works in all test frameworks and CI/CD environments.
/// </summary>
public sealed class XpingConsoleLogger : IXpingLogger
{
    private const string Prefix = "[Xping]";
    private readonly XpingLogLevel _minLevel;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingConsoleLogger"/> class.
    /// </summary>
    /// <param name="minLevel">The minimum log level to output. Defaults to Info.</param>
    public XpingConsoleLogger(XpingLogLevel minLevel = XpingLogLevel.Info)
    {
        _minLevel = minLevel;
    }

    /// <inheritdoc/>
    public void LogError(string message)
    {
        if (IsEnabled(XpingLogLevel.Error))
        {
            lock (_lock)
            {
                Console.Error.WriteLine($"{Prefix} ERROR: {message}");
            }
        }
    }

    /// <inheritdoc/>
    public void LogWarning(string message)
    {
        if (IsEnabled(XpingLogLevel.Warning))
        {
            lock (_lock)
            {
                Console.WriteLine($"{Prefix} WARNING: {message}");
            }
        }
    }

    /// <inheritdoc/>
    public void LogInfo(string message)
    {
        if (IsEnabled(XpingLogLevel.Info))
        {
            lock (_lock)
            {
                Console.WriteLine($"{Prefix} {message}");
            }
        }
    }

    /// <inheritdoc/>
    public void LogDebug(string message)
    {
        if (IsEnabled(XpingLogLevel.Debug))
        {
            lock (_lock)
            {
                Console.WriteLine($"{Prefix} DEBUG: {message}");
            }
        }
    }

    /// <inheritdoc/>
    public bool IsEnabled(XpingLogLevel level)
    {
        // None is never enabled (it's a sentinel value for disabling logging)
        if (level == XpingLogLevel.None || _minLevel == XpingLogLevel.None)
        {
            return false;
        }

        return level <= _minLevel;
    }
}
