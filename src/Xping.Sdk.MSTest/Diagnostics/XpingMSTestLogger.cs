/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest.Diagnostics;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core.Diagnostics;

/// <summary>
/// MSTest-specific logger that writes to the test output using TestContext.
/// This ensures logs are visible in MSTest test results and console output.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="XpingMSTestLogger"/> class.
/// </remarks>
/// <param name="minLevel">The minimum log level to output. Defaults to Info.</param>
/// <param name="testContext">Optional test context for per-test logging.</param>
public sealed class XpingMSTestLogger(XpingLogLevel minLevel = XpingLogLevel.Info, TestContext? testContext = null) : IXpingLogger
{
    private const string Prefix = "[Xping]";
    private readonly XpingLogLevel _minLevel = minLevel;
    private readonly object _lock = new();
    private TestContext? _testContext = testContext;

    /// <summary>
    /// Sets the test context for the current test.
    /// This allows dynamic binding of the context per test execution.
    /// </summary>
    /// <param name="testContext">The test context to use.</param>
    public void SetTestContext(TestContext? testContext)
    {
        lock (_lock)
        {
            _testContext = testContext;
        }
    }

    /// <inheritdoc/>
    public void LogError(string message)
    {
        if (IsEnabled(XpingLogLevel.Error))
        {
            lock (_lock)
            {
                WriteToOutput($"{Prefix} ERROR: {message}");
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
                WriteToOutput($"{Prefix} WARNING: {message}");
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
                WriteToOutput($"{Prefix} {message}");
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
                WriteToOutput($"{Prefix} DEBUG: {message}");
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
    /// Writes all messages to stdout and TestContext.
    /// MSTest/VSTest suppresses stderr, so we use stdout which appears in test output.
    /// </summary>
    /// <param name="message">The message to write.</param>
    private void WriteToOutput(string message)
    {
        try
        {
            // MSTest suppresses Console.Error, so use Console.WriteLine (stdout)
            Console.WriteLine(message);

            // Also write to TestContext if available (for detailed test results)
            _testContext?.WriteLine(message);
        }
        catch
        {
            // Suppress any errors during logging
        }
    }
}
