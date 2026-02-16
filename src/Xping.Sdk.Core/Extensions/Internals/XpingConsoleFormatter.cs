/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

#if !DEBUG

using System.Globalization;
using Serilog.Events;
using Serilog.Formatting;

namespace Xping.Sdk.Core.Extensions.Internals;

/// <summary>
/// A compact Serilog console formatter for release builds.
/// Shows the exception type and message on a single line without stack traces,
/// keeping output clean and immediately actionable for end users.
/// Example: [Xping 12:34:56 ERR] Upload failed → HttpRequestException: Connection refused
/// </summary>
internal sealed class XpingConsoleFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        var level = logEvent.Level switch
        {
            LogEventLevel.Verbose => "VRB",
            LogEventLevel.Debug => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error => "ERR",
            LogEventLevel.Fatal => "FTL",
            _ => "UNK"
        };

        output.Write($"[Xping {logEvent.Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} {level}] ");
        logEvent.RenderMessage(output, CultureInfo.InvariantCulture);

        if (logEvent.Exception is { } ex)
            output.Write($" \u2192 {ex.GetType().Name}: {ex.Message}");

        output.WriteLine();
    }
}

#endif
