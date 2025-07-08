/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Diagnostics;
using System.Runtime.Serialization;

namespace Xping.Sdk.Core.Session;

/// <summary>
/// Represents geographic location information for test execution.
/// Contains details about where a test is being executed including timezone information.
/// </summary>
[Serializable]
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public sealed class TestLocation : ISerializable, IDeserializationCallback, IEquatable<TestLocation>
{
    /// <summary>
    /// Gets or sets the country where the test is executed (e.g., "United States", "Germany").
    /// </summary>
    public string Country { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the country code (ISO 3166-1 alpha-2) where the test is executed (e.g., "US", "DE").
    /// </summary>
    public string CountryCode { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the region/state where the test is executed (e.g., "California", "Bavaria").
    /// </summary>
    public string Region { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the city where the test is executed (e.g., "San Francisco", "Munich").
    /// </summary>
    public string City { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the timezone identifier (e.g., "America/Los_Angeles", "Europe/Berlin").
    /// </summary>
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the public IP address used for location detection.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// Gets or sets the latitude coordinate (if available).
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// Gets or sets the longitude coordinate (if available).
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// Gets or sets the Internet Service Provider name (if available).
    /// </summary>
    public string? IspName { get; init; }

    /// <summary>
    /// Gets or sets how the location was determined (e.g., "IP-Geolocation", "Manual", "Environment").
    /// </summary>
    public string DetectionMethod { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets when the location information was detected.
    /// </summary>
    public DateTime DetectedAt { get; init; }

    /// <summary>
    /// Gets a value indicating whether this location has sufficient information for timezone calculations.
    /// </summary>
    public bool HasTimezoneInfo => !string.IsNullOrEmpty(TimeZone);

    /// <summary>
    /// Gets a display-friendly location string.
    /// </summary>
    public string DisplayName
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(City)) parts.Add(City);
            if (!string.IsNullOrEmpty(Region)) parts.Add(Region);
            if (!string.IsNullOrEmpty(Country)) parts.Add(Country);
            return parts.Count > 0 ? string.Join(", ", parts) : "Unknown Location";
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestLocation"/> class.
    /// </summary>
    public TestLocation()
    {
        DetectedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestLocation"/> class with serialized data.
    /// </summary>
    /// <param name="info">The serialization info.</param>
    /// <param name="context">The streaming context.</param>
    public TestLocation(SerializationInfo info, StreamingContext context)
    {
        ArgumentNullException.ThrowIfNull(info, nameof(info));

        Country = info.GetString(nameof(Country)) ?? string.Empty;
        CountryCode = info.GetString(nameof(CountryCode)) ?? string.Empty;
        Region = info.GetString(nameof(Region)) ?? string.Empty;
        City = info.GetString(nameof(City)) ?? string.Empty;
        TimeZone = info.GetString(nameof(TimeZone)) ?? string.Empty;
        IpAddress = info.GetString(nameof(IpAddress));
        Latitude = info.GetValue(nameof(Latitude), typeof(double?)) as double?;
        Longitude = info.GetValue(nameof(Longitude), typeof(double?)) as double?;
        IspName = info.GetString(nameof(IspName));
        DetectionMethod = info.GetString(nameof(DetectionMethod)) ?? string.Empty;
        DetectedAt = (DateTime)(info.GetValue(nameof(DetectedAt), typeof(DateTime)) ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Gets the TimeZoneInfo for this location if timezone information is available.
    /// </summary>
    /// <returns>TimeZoneInfo instance or null if timezone cannot be determined.</returns>
    public TimeZoneInfo? GetTimeZoneInfo()
    {
        if (string.IsNullOrEmpty(TimeZone))
            return null;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            // Try alternative timezone mappings for common cases
            var mappedTimeZone = MapToSystemTimeZone(TimeZone);
            if (!string.IsNullOrEmpty(mappedTimeZone))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(mappedTimeZone);
                }
                catch (TimeZoneNotFoundException)
                {
                    // Fall through to return null
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Converts UTC time to local time for this location.
    /// </summary>
    /// <param name="utcTime">The UTC time to convert.</param>
    /// <returns>Local time for this location, or the original UTC time if timezone is not available.</returns>
    public DateTime ConvertFromUtc(DateTime utcTime)
    {
        var timeZoneInfo = GetTimeZoneInfo();
        return timeZoneInfo != null 
            ? TimeZoneInfo.ConvertTimeFromUtc(utcTime, timeZoneInfo)
            : utcTime;
    }

    /// <summary>
    /// Returns a string representation of the location.
    /// </summary>
    /// <returns>A formatted string containing the location information.</returns>
    public override string ToString()
    {
        var timezone = !string.IsNullOrEmpty(TimeZone) ? $" ({TimeZone})" : "";
        return $"{DisplayName}{timezone}";
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>True if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as TestLocation);
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>True if the current object is equal to the other parameter; otherwise, false.</returns>
    public bool Equals(TestLocation? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Country == other.Country &&
               CountryCode == other.CountryCode &&
               Region == other.Region &&
               City == other.City &&
               TimeZone == other.TimeZone;
    }

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Country, CountryCode, Region, City, TimeZone);
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(TestLocation? left, TestLocation? right)
    {
        return Equals(left, right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(TestLocation? left, TestLocation? right)
    {
        return !Equals(left, right);
    }

    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue(nameof(Country), Country);
        info.AddValue(nameof(CountryCode), CountryCode);
        info.AddValue(nameof(Region), Region);
        info.AddValue(nameof(City), City);
        info.AddValue(nameof(TimeZone), TimeZone);
        info.AddValue(nameof(IpAddress), IpAddress);
        info.AddValue(nameof(Latitude), Latitude);
        info.AddValue(nameof(Longitude), Longitude);
        info.AddValue(nameof(IspName), IspName);
        info.AddValue(nameof(DetectionMethod), DetectionMethod);
        info.AddValue(nameof(DetectedAt), DetectedAt);
    }

    void IDeserializationCallback.OnDeserialization(object? sender)
    {
        // Validation after deserialization if needed
    }

    private string GetDebuggerDisplay()
    {
        return $"{DisplayName} ({TimeZone})";
    }

    /// <summary>
    /// Maps IANA timezone identifiers to Windows timezone identifiers for cross-platform compatibility.
    /// </summary>
    /// <param name="ianaTimeZone">The IANA timezone identifier.</param>
    /// <returns>The mapped system timezone identifier or null if no mapping exists.</returns>
    private static string? MapToSystemTimeZone(string ianaTimeZone)
    {
        // Common IANA to Windows timezone mappings
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "America/New_York", "Eastern Standard Time" },
            { "America/Chicago", "Central Standard Time" },
            { "America/Denver", "Mountain Standard Time" },
            { "America/Los_Angeles", "Pacific Standard Time" },
            { "Europe/London", "GMT Standard Time" },
            { "Europe/Paris", "W. Europe Standard Time" },
            { "Europe/Berlin", "W. Europe Standard Time" },
            { "Asia/Tokyo", "Tokyo Standard Time" },
            { "Asia/Shanghai", "China Standard Time" },
            { "Australia/Sydney", "AUS Eastern Standard Time" }
        };

        return mappings.TryGetValue(ianaTimeZone, out var mapped) ? mapped : null;
    }
}
