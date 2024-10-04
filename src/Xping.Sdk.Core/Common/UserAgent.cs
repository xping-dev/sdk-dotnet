namespace Xping.Sdk.Core.Common;

/// <summary>
/// Contains commonly used user agent strings for desktop and mobile browsers.
/// </summary>
/// <remarks>
/// The UserAgent class provides a convenient way to access predefined user agent strings that can be used to simulate 
/// different browsers in HTTP requests. This can be particularly useful when performing automated testing where 
/// mimicking a specific browser is required.
/// </remarks>
public static class UserAgent
{
    /// <summary>
    /// Represents a user agent string for Google Chrome on desktop.
    /// </summary>
    public static readonly string ChromeDesktop = 
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/58.0.3029.110 Safari/537.3";

    /// <summary>
    /// Represents a user agent string for Mozilla Firefox on desktop.
    /// </summary>
    public static readonly string FirefoxDesktop = 
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:53.0) Gecko/20100101 Firefox/53.0";

    /// <summary>
    /// Represents a user agent string for Microsoft Edge on desktop.
    /// </summary>
    public static readonly string EdgeDesktop = 
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/42.0.2311.135 Safari/537.36 Edge/12.246";

    /// <summary>
    /// Represents a user agent string for Safari on mobile devices.
    /// </summary>
    public static readonly string SafariMobile = 
        "Mozilla/5.0 (iPhone; CPU iPhone OS 10_3_1 like Mac OS X) AppleWebKit/603.1.30 (KHTML, like Gecko) " +
        "Version/10.0 Mobile/14E304 Safari/602.1";

    /// <summary>
    /// Represents a user agent string for Google Chrome on iOS mobile devices.
    /// </summary>    
    public static readonly string ChromeiOSMobile =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) " +
        "CriOS/123.0.6312.52 Mobile/15E148 Safari/604.1";

    /// <summary>
    /// Represents a user agent string for Google Chrome on Android mobile devices.
    /// </summary>    
    public static readonly string ChromeAndroidMobile =
        "Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.6312.80 Mobile " +
        "Safari/537.36";

    /// <summary>
    /// Represents a user agent string for Mozilla Firefox on iOS mobile devices.
    /// </summary>
    public static readonly string FirefoxiOSMobile =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 14_4_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) " +
        "FxiOS/124.0 Mobile/15E148 Safari/605.1.15";

    /// <summary>
    /// Represents a user agent string for Mozilla Firefox on Android mobile devices.
    /// </summary>
    public static readonly string FirefoxAndroidMobile =
        "Mozilla/5.0 (Android 14; Mobile; rv:124.0) Gecko/124.0 Firefox/124.0";
}
