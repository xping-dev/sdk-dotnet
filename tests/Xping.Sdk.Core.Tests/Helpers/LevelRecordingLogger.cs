/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;

namespace Xping.Sdk.Core.Tests.Helpers;

/// <summary>
/// Records the level of every log call, or throws on each one to test that logging failures are
/// contained.
/// </summary>
// Moq can't proxy ILogger<T> for an internal T: Logging.Abstractions is strong-named.
internal sealed class LevelRecordingLogger<T> : ILogger<T>
{
    public List<LogLevel> Levels { get; } = [];

    public bool ThrowOnLog { get; init; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (ThrowOnLog)
            throw new InvalidOperationException("Logger failure.");

        Levels.Add(logLevel);
    }
}
