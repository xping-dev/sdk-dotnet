/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Text.Json;

namespace Xping.Sdk.Core.Session;

/// <summary>
/// Service for detecting the geographic location where tests are being executed.
/// Uses IP geolocation services and environment variables for location detection.
/// </summary>
/// <remarks>
/// Initializes a new instance of the LocationDetectionService.
/// </remarks>
/// <param name="clientFactory">HTTP client factory for making geolocation requests.</param>
public class LocationDetectionService(IHttpClientFactory clientFactory) : ILocationDetectionService
{
    private readonly HttpClient _httpClient = clientFactory.CreateClient();

    private TestLocation? _cachedLocation;
    private DateTime _lastDetectionTime = DateTime.MinValue;
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromHours(1); // Cache location for 1 hour

    /// <summary>
    /// Detects the current location where the test is being executed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>TestLocation information or null if detection fails.</returns>
    public async Task<TestLocation?> DetectLocationAsync(CancellationToken cancellationToken = default)
    {
        // Check if we have a cached location that's still valid
        if (_cachedLocation != null && DateTime.UtcNow - _lastDetectionTime < _cacheTimeout)
        {
            return _cachedLocation;
        }

        // Try environment variables first (for CI/CD environments)
        var envLocation = TryGetLocationFromEnvironment();
        if (envLocation != null)
        {
            _cachedLocation = envLocation;
            _lastDetectionTime = DateTime.UtcNow;
            return envLocation;
        }

        // Try IP geolocation services
        var ipLocation = await TryGetLocationFromIpAsync(cancellationToken).ConfigureAwait(false);
        if (ipLocation != null)
        {
            _cachedLocation = ipLocation;
            _lastDetectionTime = DateTime.UtcNow;
            return ipLocation;
        }

        // Fallback to system timezone
        var fallbackLocation = GetFallbackLocation();
        _cachedLocation = fallbackLocation;
        _lastDetectionTime = DateTime.UtcNow;
        return fallbackLocation;
    }

    /// <summary>
    /// Attempts to get location information from environment variables.
    /// Useful for CI/CD environments where location can be explicitly set.
    /// </summary>
    private static TestLocation? TryGetLocationFromEnvironment()
    {
        var country = Environment.GetEnvironmentVariable("XPING_LOCATION_COUNTRY");
        var region = Environment.GetEnvironmentVariable("XPING_LOCATION_REGION");
        var city = Environment.GetEnvironmentVariable("XPING_LOCATION_CITY");
        var timezone = Environment.GetEnvironmentVariable("XPING_LOCATION_TIMEZONE");

        if (!string.IsNullOrEmpty(country) || !string.IsNullOrEmpty(timezone))
        {
            return new TestLocation
            {
                Country = country ?? "Unknown",
                CountryCode = Environment.GetEnvironmentVariable("XPING_LOCATION_COUNTRY_CODE") ?? "",
                Region = region ?? "Unknown",
                City = city ?? "Unknown",
                TimeZone = timezone ?? TimeZoneInfo.Local.Id,
                DetectionMethod = "Environment",
                DetectedAt = DateTime.UtcNow
            };
        }

        return null;
    }

    /// <summary>
    /// Attempts to get location information using IP geolocation services.
    /// </summary>
    private async Task<TestLocation?> TryGetLocationFromIpAsync(CancellationToken cancellationToken)
    {
        // Try multiple geolocation services for reliability
        var services = new[]
        {
            "http://ip-api.com/json/?fields=status,country,countryCode,region,regionName,city,timezone,lat,lon,isp,query",
            "https://ipapi.co/json/",
            "https://freegeoip.app/json/"
        };

        foreach (var serviceUrl in services)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(5)); // 5 second timeout per service

                var response = await _httpClient.GetAsync(new Uri(serviceUrl), cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var location = await ParseLocationResponseAsync(response, serviceUrl).ConfigureAwait(false);
                    if (location != null)
                        return location;
                }
            }
            catch (Exception)
            {
                // Continue to next service if this one fails
                continue;
            }
        }

        return null;
    }

    /// <summary>
    /// Parses the location response from different geolocation services.
    /// </summary>
    private static async Task<TestLocation?> ParseLocationResponseAsync(HttpResponseMessage response, string serviceUrl)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            // Handle different response formats from different services
            if (serviceUrl.Contains("ip-api.com", StringComparison.InvariantCultureIgnoreCase))
            {
                return ParseIpApiResponse(root);
            }
            else if (serviceUrl.Contains("ipapi.co", StringComparison.InvariantCultureIgnoreCase))
            {
                return ParseIpapiResponse(root);
            }
            else if (serviceUrl.Contains("freegeoip.app", StringComparison.InvariantCultureIgnoreCase))
            {
                return ParseFreeGeoIpResponse(root);
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses response from ip-api.com service.
    /// </summary>
    private static TestLocation? ParseIpApiResponse(JsonElement root)
    {
        if (root.TryGetProperty("status", out var status) && status.GetString() != "success")
            return null;

        return new TestLocation
        {
            Country = root.TryGetProperty("country", out var country) ? country.GetString() ?? "" : "",
            CountryCode = root.TryGetProperty("countryCode", out var countryCode) ? countryCode.GetString() ?? "" : "",
            Region = root.TryGetProperty("regionName", out var regionName) ? regionName.GetString() ?? "" : "",
            City = root.TryGetProperty("city", out var city) ? city.GetString() ?? "" : "",
            TimeZone = root.TryGetProperty("timezone", out var timezone) ? timezone.GetString() ?? "" : "",
            IpAddress = root.TryGetProperty("query", out var query) ? query.GetString() : null,
            Latitude = root.TryGetProperty("lat", out var lat) ? lat.GetDouble() : null,
            Longitude = root.TryGetProperty("lon", out var lon) ? lon.GetDouble() : null,
            IspName = root.TryGetProperty("isp", out var isp) ? isp.GetString() : null,
            DetectionMethod = "IP-Geolocation (ip-api.com)",
            DetectedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Parses response from ipapi.co service.
    /// </summary>
    private static TestLocation? ParseIpapiResponse(JsonElement root)
    {
        return new TestLocation
        {
            Country = root.TryGetProperty("country_name", out var country) ? country.GetString() ?? "" : "",
            CountryCode = root.TryGetProperty("country_code", out var countryCode) ? countryCode.GetString() ?? "" : "",
            Region = root.TryGetProperty("region", out var region) ? region.GetString() ?? "" : "",
            City = root.TryGetProperty("city", out var city) ? city.GetString() ?? "" : "",
            TimeZone = root.TryGetProperty("timezone", out var timezone) ? timezone.GetString() ?? "" : "",
            IpAddress = root.TryGetProperty("ip", out var ip) ? ip.GetString() : null,
            Latitude = root.TryGetProperty("latitude", out var lat) ? lat.GetDouble() : null,
            Longitude = root.TryGetProperty("longitude", out var lon) ? lon.GetDouble() : null,
            IspName = root.TryGetProperty("org", out var org) ? org.GetString() : null,
            DetectionMethod = "IP-Geolocation (ipapi.co)",
            DetectedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Parses response from freegeoip.app service.
    /// </summary>
    private static TestLocation? ParseFreeGeoIpResponse(JsonElement root)
    {
        return new TestLocation
        {
            Country = root.TryGetProperty("country_name", out var country) ? country.GetString() ?? "" : "",
            CountryCode = root.TryGetProperty("country_code", out var countryCode) ? countryCode.GetString() ?? "" : "",
            Region = root.TryGetProperty("region_name", out var region) ? region.GetString() ?? "" : "",
            City = root.TryGetProperty("city", out var city) ? city.GetString() ?? "" : "",
            TimeZone = root.TryGetProperty("time_zone", out var timezone) ? timezone.GetString() ?? "" : "",
            IpAddress = root.TryGetProperty("ip", out var ip) ? ip.GetString() : null,
            Latitude = root.TryGetProperty("latitude", out var lat) ? lat.GetDouble() : null,
            Longitude = root.TryGetProperty("longitude", out var lon) ? lon.GetDouble() : null,
            DetectionMethod = "IP-Geolocation (freegeoip.app)",
            DetectedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Provides a fallback location based on system timezone when other methods fail.
    /// </summary>
    private static TestLocation GetFallbackLocation()
    {
        var localTimeZone = TimeZoneInfo.Local;

        return new TestLocation
        {
            Country = "Unknown",
            CountryCode = "",
            Region = "Unknown",
            City = "Unknown",
            TimeZone = localTimeZone.Id,
            DetectionMethod = "System Timezone",
            DetectedAt = DateTime.UtcNow
        };
    }
}
