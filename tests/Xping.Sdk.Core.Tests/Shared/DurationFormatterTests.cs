/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Tests.Shared;

public sealed class DurationFormatterTests
{
    [Theory]
    [InlineData(0L, "0ms")]
    [InlineData(1L, "1ms")]
    [InlineData(627L, "627ms")]
    [InlineData(999L, "999ms")]
    public void Format_BelowOneSecond_KeepsMilliseconds(long milliseconds, string expected)
    {
        Assert.Equal(expected, DurationFormatter.Format(milliseconds));
    }

    [Theory]
    [InlineData(1000L, "1s")]
    [InlineData(1500L, "1.5s")]
    [InlineData(1949L, "1.9s")]
    [InlineData(12435L, "12.4s")]
    [InlineData(59_000L, "59s")]
    [InlineData(59_940L, "59.9s")]
    public void Format_BelowOneMinute_UsesSecondsWithOneDecimal(long milliseconds, string expected)
    {
        Assert.Equal(expected, DurationFormatter.Format(milliseconds));
    }

    [Theory]
    [InlineData(59_990L)] // rounds up to 60.0s
    [InlineData(59_999L)]
    [InlineData(60_000L)]
    public void Format_AtTheMinuteBoundary_NeverPrintsSixtySeconds(long milliseconds)
    {
        Assert.Equal("1m", DurationFormatter.Format(milliseconds));
    }

    [Theory]
    [InlineData(83_000L, "1m 23s")]
    [InlineData(120_000L, "2m")]
    [InlineData(125_400L, "2m 5s")]
    [InlineData(3_599_000L, "59m 59s")]
    public void Format_BelowOneHour_UsesMinutesAndSeconds(long milliseconds, string expected)
    {
        Assert.Equal(expected, DurationFormatter.Format(milliseconds));
    }

    [Theory]
    [InlineData(3_599_999L, "1h")] // rounds to 3600s
    [InlineData(3_600_000L, "1h")]
    [InlineData(3_900_000L, "1h 5m")]
    [InlineData(7_530_000L, "2h 6m")]
    public void Format_AnHourOrMore_UsesHoursAndMinutes(long milliseconds, string expected)
    {
        Assert.Equal(expected, DurationFormatter.Format(milliseconds));
    }

    [Theory]
    [InlineData(-1L, "-1ms")]
    [InlineData(-12_435L, "-12.4s")]
    public void Format_NegativeDuration_KeepsTheSign(long milliseconds, string expected)
    {
        Assert.Equal(expected, DurationFormatter.Format(milliseconds));
    }

    [Theory]
    [InlineData(591L, 10_600L, " (+10s overhead)")]
    [InlineData(0L, 1L, " (+1ms overhead)")]
    [InlineData(12_000L, 3_612_000L, " (+1h overhead)")]
    public void FormatOverhead_WallClockExceedsExecution_ReportsTheGap(
        long executionMs, long wallClockMs, string expected)
    {
        Assert.Equal(expected, DurationFormatter.FormatOverhead(executionMs, wallClockMs));
    }

    [Theory]
    [InlineData(10_000L, 10_000L)] // no gap at all
    [InlineData(40_000L, 12_000L)] // parallel execution: tests outrun the wall clock
    [InlineData(0L, 0L)]
    public void FormatOverhead_NoPositiveGap_ReportsNothing(long executionMs, long wallClockMs)
    {
        Assert.Equal(string.Empty, DurationFormatter.FormatOverhead(executionMs, wallClockMs));
    }

    [Fact]
    public void Format_LongMinValue_DoesNotOverflow()
    {
        Assert.StartsWith("-", DurationFormatter.Format(long.MinValue), StringComparison.Ordinal);
    }
}
