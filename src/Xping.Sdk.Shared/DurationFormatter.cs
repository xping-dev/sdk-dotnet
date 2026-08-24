/*
 * © 2024 Xping.io. All Rights Reserved.
 *
 * This file is part of the Xping Solution.
 */

using System;
using System.Globalization;

namespace Xping.Sdk.Shared;

/// <summary>
/// Formats millisecond durations at the scale a reader thinks in, so a log line reads
/// "12.4s" rather than "12435ms".
/// </summary>
/// <remarks>
/// The unit steps up as the magnitude grows — milliseconds below a second, seconds below a
/// minute, then minutes and hours — keeping at most two units so the value stays scannable.
/// Raw millisecond counts remain available on the underlying statistics for machine consumers.
/// </remarks>
public static class DurationFormatter
{
    private const long MillisecondsPerSecond = 1000L;
    private const long MillisecondsPerMinute = 60L * MillisecondsPerSecond;
    private const long SecondsPerMinute = 60L;
    private const long SecondsPerHour = 60L * SecondsPerMinute;
    private const long MinutesPerHour = 60L;

    /// <summary>
    /// Formats the gap between the summed execution time and the wall clock as a suffix such as
    /// <c> (+10s overhead)</c>, ready to append to a rendered wall-clock duration.
    /// </summary>
    /// <param name="executionMilliseconds">The summed execution time of the tests themselves.</param>
    /// <param name="wallClockMilliseconds">The elapsed time of the whole session.</param>
    /// <returns>
    /// The suffix, including its leading space, or an empty string when there is no overhead to
    /// report. Under parallel execution the tests can add up to more than the wall clock, and a
    /// negative overhead says nothing useful — those runs get no suffix at all.
    /// </returns>
    public static string FormatOverhead(long executionMilliseconds, long wallClockMilliseconds)
    {
        long overhead = wallClockMilliseconds - executionMilliseconds;

        return overhead > 0 ? " (+" + Format(overhead) + " overhead)" : string.Empty;
    }

    /// <summary>
    /// Formats a duration expressed in milliseconds for human consumption.
    /// </summary>
    /// <param name="milliseconds">The duration in milliseconds. Negative values keep their sign.</param>
    /// <returns>
    /// A compact, unit-suffixed rendering such as <c>627ms</c>, <c>12.4s</c>, <c>1m 23s</c> or
    /// <c>2h 5m</c>.
    /// </returns>
    public static string Format(long milliseconds)
    {
        // A negative duration is nonsense, but printing it beats throwing from a log line.
        if (milliseconds < 0)
            return "-" + Format(milliseconds == long.MinValue ? long.MaxValue : -milliseconds);

        if (milliseconds < MillisecondsPerSecond)
            return milliseconds.ToString(CultureInfo.InvariantCulture) + "ms";

        if (milliseconds < MillisecondsPerMinute)
        {
            double seconds = Math.Round(milliseconds / (double)MillisecondsPerSecond, 1);

            // Rounding can push 59.97s up to a full minute; let it fall through to "1m" instead
            // of printing a nonsensical "60s".
            if (seconds < SecondsPerMinute)
                return seconds.ToString("0.#", CultureInfo.InvariantCulture) + "s";
        }

        long totalSeconds = (long)Math.Round(milliseconds / (double)MillisecondsPerSecond);

        if (totalSeconds < SecondsPerHour)
        {
            long minutes = totalSeconds / SecondsPerMinute;
            long seconds = totalSeconds % SecondsPerMinute;

            return seconds == 0
                ? minutes.ToString(CultureInfo.InvariantCulture) + "m"
                : minutes.ToString(CultureInfo.InvariantCulture) + "m " +
                  seconds.ToString(CultureInfo.InvariantCulture) + "s";
        }

        long totalMinutes = (long)Math.Round(totalSeconds / (double)SecondsPerMinute);
        long hours = totalMinutes / MinutesPerHour;
        long remainingMinutes = totalMinutes % MinutesPerHour;

        return remainingMinutes == 0
            ? hours.ToString(CultureInfo.InvariantCulture) + "h"
            : hours.ToString(CultureInfo.InvariantCulture) + "h " +
              remainingMinutes.ToString(CultureInfo.InvariantCulture) + "m";
    }
}
