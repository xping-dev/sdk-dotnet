/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit.Diagnostics;

using System;
using global::Xunit.Abstractions;
using Xping.Sdk.Core.Diagnostics;

/// <summary>
/// xUnit-specific logger that writes to the test output using ITestOutputHelper when available,
/// and falls back to Console.WriteLine. This ensures logs are visible in xUnit test results.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="XpingXUnitLogger"/> class.
/// </remarks>
/// <param name="minLevel">The minimum log level to output. Defaults to Info.</param>
/// <param name="testOutputHelper">Optional test output helper for per-test logging.</param>
public sealed class XpingXUnitLogger(
    XpingLogLevel minLevel = XpingLogLevel.Info,
    ITestOutputHelper? testOutputHelper = null) : IXpingLogger
{
    private const string Prefix = "[Xping]";
    private readonly object _lock = new();
    private ITestOutputHelper? _testOutputHelper = testOutputHelper;

    /// <summary>
    /// Sets the test output helper for the current test.
    /// This allows dynamic binding of the output helper per test execution.
    /// </summary>
    /// <param name="testOutputHelper">The test output helper to use.</param>
    public void SetTestOutputHelper(ITestOutputHelper? testOutputHelper)
    {
        lock (_lock)
        {
            _testOutputHelper = testOutputHelper;
        }
    }

    /// <inheritdoc/>
    public void LogError(string message)
    {
        if (IsEnabled(XpingLogLevel.Error))
        {
            lock (_lock)
            {
                WriteToStderr($"{Prefix} ERROR: {message}");
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
                WriteToStderr($"{Prefix} WARNING: {message}");
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
                WriteToStdout($"{Prefix} DEBUG: {message}");
            }
        }
    }

    /// <inheritdoc/>
    public bool IsEnabled(XpingLogLevel level)
    {
        // None is never enabled (it's a sentinel value for disabling logging)
        if (level == XpingLogLevel.None || minLevel == XpingLogLevel.None)
        {
            return false;
        }

        return level <= minLevel;
    }

    /// <summary>
    /// Writes ERROR and WARNING messages to stderr (for MSBuild warnings).
    /// These will appear as warnings in the build output.
    /// </summary>
    /// <param name="message">The message to write.</param>
    private void WriteToStderr(string message)
    {
        try
        {
            // Write to stderr - shows as MSBuild warning
            Console.Error.WriteLine(message);

            // Also write to ITestOutputHelper if available
            _testOutputHelper?.WriteLine(message);
        }
        catch
        {
            // Suppress any errors during logging
        }
    }

    /// <summary>
    /// Writes INFO and DEBUG messages to stdout and ITestOutputHelper.
    /// Writing to stdout ensures logs appear in piped output and CI/CD environments.
    /// </summary>
    /// <param name="message">The message to write.</param>
    private void WriteToStdout(string message)
    {
        try
        {
            // Always write to stdout for consistent behavior with other test frameworks
            Console.WriteLine(message);

            // Also write to ITestOutputHelper if available (for xUnit test results)
            _testOutputHelper?.WriteLine(message);
        }
        catch
        {
            // Suppress any errors during logging
        }
    }
}
