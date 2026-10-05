/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Cli.Auth;

namespace Xping.Cli.Hosting;

/// <summary>
/// Writes the CLI's own log messages to stderr when <c>--verbose</c> is given.
/// </summary>
/// <remarks>
/// <para>
/// Writes to <see cref="ConsoleIO.Error"/> rather than <see cref="Console"/>, so it shares the test
/// seam and can never interleave with a <c>--json</c> document on stdout.
/// </para>
/// <para>
/// Only <c>Xping.Cli.*</c> categories pass. <c>System.Net.Http</c> in particular stays off: its
/// messages carry full URLs and headers, which is more than <see cref="Redaction"/> should be
/// trusted to clean.
/// </para>
/// </remarks>
internal sealed class VerboseLoggerProvider(ConsoleIO io, GlobalOptions options) : ILoggerProvider
{
    private const string CategoryPrefix = "Xping.Cli.";

    public ILogger CreateLogger(string categoryName) =>
        categoryName.StartsWith(CategoryPrefix, StringComparison.Ordinal)
            ? new VerboseLogger(io, options)
            : NullLogger.Instance;

    public void Dispose()
    {
    }

    private sealed class VerboseLogger(ConsoleIO io, GlobalOptions options) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        // Read on every call: the logger can be created before the command line is parsed.
        public bool IsEnabled(LogLevel logLevel) =>
            options.Verbose && logLevel >= LogLevel.Information && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (!IsEnabled(logLevel))
                return;

            string message = formatter(state, exception);
            if (exception != null)
                message = $"{message} ({exception.GetType().Name}: {exception.Message})";

            io.Error.WriteLine(Redaction.Scrub(message));
        }
    }
}
