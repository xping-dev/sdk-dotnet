/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

// XpingConsoleFormatter is the release-build console formatter, so it only exists — and can only
// be tested — when the SDK is compiled without DEBUG. CI runs the suite in Release.
#if !DEBUG

using System.Globalization;
using Serilog.Events;
using Serilog.Parsing;
using Xping.Sdk.Core.Extensions.Internals;

namespace Xping.Sdk.Core.Tests.Extensions;

public sealed class XpingConsoleFormatterTests
{
    private static readonly MessageTemplateParser Parser = new();

    [Fact]
    public void Format_StringProperty_RendersLiterallyWithoutQuotes()
    {
        string output = Render(
            "Total tests recorded: {Total} · {Outcomes} · wall: {WallClockDuration}",
            ("Total", 13),
            ("Outcomes", "10 passed, 1 failed"),
            ("WallClockDuration", "10.6s"));

        Assert.Contains("Total tests recorded: 13 · 10 passed, 1 failed · wall: 10.6s", output,
            StringComparison.Ordinal);
        Assert.DoesNotContain("\"", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_Always_PrefixesTheXpingBannerAndLevel()
    {
        string output = Render("Session finalized.");

        Assert.Matches(@"^\[Xping \d{2}:\d{2}:\d{2} INF\] Session finalized\.", output);
    }

    [Fact]
    public void Format_WithException_AppendsTypeAndMessageWithoutStackTrace()
    {
        var formatter = new XpingConsoleFormatter();
        var logEvent = new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Error,
            new HttpRequestException("Connection refused"),
            Parser.Parse("Upload failed"),
            []);

        using var writer = new StringWriter();
        formatter.Format(logEvent, writer);
        string output = writer.ToString();

        Assert.Contains("ERR] Upload failed → HttpRequestException: Connection refused", output,
            StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", output, StringComparison.Ordinal);
    }

    private static string Render(string messageTemplate, params (string Name, object Value)[] properties)
    {
        var logEvent = new LogEvent(
            DateTimeOffset.Now,
            LogEventLevel.Information,
            exception: null,
            Parser.Parse(messageTemplate),
            properties.Select(p => new LogEventProperty(p.Name, new ScalarValue(p.Value))).ToList());

        using var writer = new StringWriter();
        new XpingConsoleFormatter().Format(logEvent, writer);

        return writer.ToString();
    }
}

#endif
