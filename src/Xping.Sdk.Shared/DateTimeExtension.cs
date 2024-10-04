namespace Xping.Sdk.Shared;

internal static class DateTimeExtensions
{
    /// <summary>
    /// Formats a TimeSpan object into a string, using the appropriate units and rounding.
    /// </summary>
    /// <param name="time">The TimeSpan object to format.</param>
    /// <returns>A string representation of the TimeSpan object.</returns>
    public static string GetFormattedTime(this TimeSpan time)
    {
        if (time.TotalMinutes >= 1)
        {
            return $"{Math.Round(time.TotalMinutes, 2)} min";
        }
        if (time.TotalSeconds >= 1)
        {
            return $"{Math.Round(time.TotalSeconds, 2)} s";
        }

        return $"{Math.Round(time.TotalMilliseconds, 0)} ms";
    }

    /// <summary>
    /// Formats the TimeSpan value into a string representation based on the specified unit.
    /// </summary>
    /// <param name="time">The TimeSpan to format.</param>
    /// <param name="unit">The unit of time to format the TimeSpan into.</param>
    /// <returns>A string representation of the TimeSpan value in the specified unit.</returns>
    public static string GetFormattedTime(this TimeSpan time, TimeSpanUnit unit)
    {
        return unit switch
        {
            TimeSpanUnit.Millisecond => $"{Math.Round(time.TotalMilliseconds, 0)} ms",
            TimeSpanUnit.Second => $"{Math.Round(time.TotalSeconds, 2)} s",
            TimeSpanUnit.Minute => $"{Math.Round(time.TotalMinutes, 2)} min",
            TimeSpanUnit.Hour => time.TotalHours switch
            {
                // Check if the total hours are close to 0 (considering a small threshold for rounding errors)
                var hours when Math.Abs(hours) < 0.01 => "0 hours",
                // Check if the total hours are within the range of 0 to 2 (exclusive)
                var hours when hours < 2 => $"{Math.Round(hours, 2)} hour",
                // Default case for 2 or more hours
                var hours => $"{Math.Round(hours, 2)} hours"
            },
            TimeSpanUnit.Day => time.TotalDays switch
            {
                // Check if the total days are close to 0 (considering a small threshold for rounding errors)
                var days when Math.Abs(days) < 0.01 => "0 days",
                // Check if the total days are within the range of 0 to 2 (exclusive)
                var days when days < 2 => $"{Math.Round(days, 2)} day",
                // Default case for 2 or more days
                var days => $"{Math.Round(days, 2)} days"
            },
            _ => GetFormattedTime(time)
        };
    }
}
