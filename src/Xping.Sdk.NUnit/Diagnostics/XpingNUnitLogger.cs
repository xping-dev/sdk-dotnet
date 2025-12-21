/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit.Diagnostics;

using global::NUnit.Framework;
using Xping.Sdk.Core.Diagnostics;

/// <summary>
/// NUnit-specific logger that writes to the test output using TestContext.
/// This ensures logs are visible in NUnit test results and console output.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="XpingNUnitLogger"/> class.
/// </remarks>
/// <param name="minLevel">The minimum log level to output. Defaults to Info.</param>
public sealed class XpingNUnitLogger(XpingLogLevel minLevel = XpingLogLevel.Info) : IXpingLogger
{
    private const string Prefix = "[Xping]";
    private readonly XpingLogLevel _minLevel = minLevel;
    private readonly object _lock = new();

    /// <inheritdoc/>
    public void LogError(string message)
    {
        if (IsEnabled(XpingLogLevel.Error))
        {
            lock (_lock)
            {
                // Errors should appear in stderr (will show as MSBuild warnings)
                WriteToStderr($"{Prefix} {message}");
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
                // Warnings should appear in stderr (will show as MSBuild warnings)
                WriteToStderr($"{Prefix} {message}");
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
                // Info messages should NOT appear in stderr (to avoid MSBuild warning count)
                // Use TestContext.Out which writes to stdout for test visibility
                WriteToStdout($"{Prefix} {message}");
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
                // Debug messages should NOT appear in stderr
                WriteToStdout($"{Prefix} DEBUG: {message}");
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

    /// <summary>
    /// Writes ERROR and WARNING messages to TestContext.Error (visible in test output).
    /// These will appear as MSBuild warnings but that's acceptable for actual errors/warnings.
    /// </summary>
    /// <param name="message">The message to write.</param>
    private static void WriteToStderr(string message)
    {
        try
        {
            // Write to TestContext.Error which routes to stderr
            // This shows as MSBuild warning but avoids duplication
            if (TestContext.CurrentContext != null)
            {
                TestContext.Error.WriteLine(message);
            }
            else
            {
                // Fallback to Console.Error if TestContext is not available
                Console.Error.WriteLine(message);
            }
        }
        catch
        {
            // Suppress any errors during logging
        }
    }

    /// <summary>
    /// Writes INFO and DEBUG messages to TestContext.Out only (not Console.Error).
    /// This avoids inflating MSBuild warning counts with informational messages.
    /// </summary>
    /// <param name="message">The message to write.</param>
    private static void WriteToStdout(string message)
    {
        try
        {
            // Write to TestContext.Out which writes to stdout
            // This is visible in test output without being treated as a warning
            if (TestContext.CurrentContext != null)
            {
                TestContext.Progress.WriteLine(message);
            }
            else
            {
                // Fallback to stdout if TestContext is not available
                Console.WriteLine(message);
            }
        }
        catch
        {
            // Suppress any errors during logging
        }
    }
}
