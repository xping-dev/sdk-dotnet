using Microsoft.Playwright;
using Xping.Sdk.Core.Common;

namespace Xping.Sdk.Core.Clients.Browser;

/// <summary>
/// 
/// </summary>
public class BrowserConfiguration : BaseConfiguration
{
    /// <summary>
    /// Gets or sets a value which specifies the browser type to use for the web tests. It can be one of the values from 
    /// the BrowserType enum in the Playwright library, such as “chromium”, “firefox”, or “webkit”. The default value 
    /// is “chromium”.
    /// </summary>
    /// <remarks>
    /// The BrowserType property is used by to create a new browser instance of the specified type. 
    /// For example, you can set this property as follows
    /// <code>
    /// var settings = new TestSettings
    /// {
    ///     BrowserType = BrowserType.Firefox;
    /// }
    /// </code>
    /// </remarks>
    public string BrowserType { get; set; } = "chromium";

    /// <summary>
    /// Specify user locale, for example <c>en-GB</c>, <c>de-DE</c>, etc. Locale will affect <c>navigator.language</c>
    /// value, <c>Accept-Language</c> request header value as well as number and date formatting rules. Defaults to the
    /// system default locale. Learn more about emulation in our 
    /// <a href="https://playwright.dev/dotnet/docs/emulation#locale--timezone">emulation guide</a>.
    /// </summary>
    public string? Locale { get; set; }

    /// <summary>
    /// Whether to run browser in headless mode. More details for <a href="https://developers.google.com/web/updates/2017/04/headless-chrome">Chromium</a>
    /// and <a href="https://developer.mozilla.org/en-US/docs/Mozilla/Firefox/Headless_mode">Firefox</a>.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool? Headless { get; set; } = true;

    /// <summary>
    /// <para>When to consider operation succeeded, defaults to <c>load</c>. Events can be either:</para>
    /// <list type="bullet">
    /// <item><description>
    /// <c>'domcontentloaded'</c> - consider operation to be finished when the <c>DOMContentLoaded</c>
    /// event is fired.
    /// </description></item>
    /// <item><description>
    /// <c>'load'</c> - consider operation to be finished when the <c>load</c> event is
    /// fired.
    /// </description></item>
    /// <item><description>
    /// <c>'networkidle'</c> - **DISCOURAGED** consider operation to be finished when there
    /// are no network connections for at least <c>500</c> ms. Don't use this method for
    /// testing, rely on web assertions to assess readiness instead.
    /// </description></item>
    /// <item><description>
    /// <c>'commit'</c> - consider operation to be finished when network response is received
    /// and the document started loading.
    /// </description></item>
    /// </list>
    /// </summary>
    public WaitUntilState? WaitUntil { get; set; } = WaitUntilState.Load;

    /// <summary>
    /// Stores the geolocation coordinates in the test settings instance for use with a headless browser.
    /// </summary>
    /// <param name="geolocation">The Geolocation object containing latitude and longitude coordinates.</param>
    /// <remarks>
    /// <note>
    /// Note that this method is not applicable for setting geolocation in an HttpClient instance, as HttpClient does 
    /// not inherently support geolocation.
    /// </note>
    /// This method updates the PropertyBag with the provided Geolocation object. If the Geolocation key already exists,
    /// it will be updated with the new coordinates; otherwise, a new entry will be added. This is particularly useful
    /// for setting up location-based scenarios in automated tests using a headless browser. 
    /// </remarks>
    public void SetGeolocation(Geolocation geolocation)
    {
        PropertyBag.AddOrUpdateProperty(PropertyBagKeys.Geolocation, geolocation);
    }

    /// <summary>
    /// Retrieves the geolocation coordinates from the test settings instance.
    /// </summary>
    /// <returns>
    /// A Geolocation object containing latitude and longitude coordinates if found; otherwise, null.
    /// </returns>
    /// <remarks>
    /// This method attempts to retrieve the Geolocation object associated with the current test settings.
    /// If the PropertyBag contains the Geolocation key, the method will return the corresponding Geolocation object;
    /// otherwise, it will return null, indicating that no geolocation has been set.
    /// </remarks>
    public Geolocation? GetGeolocation()
    {
        PropertyBag.TryGetProperty<Geolocation>(PropertyBagKeys.Geolocation, out var geolocation);
        return geolocation;
    }

    /// <summary>
    /// Removes the geolocation information from the test settings instance.
    /// </summary>
    /// <remarks>
    /// This method clears any stored geolocation data associated with the Geolocation key in the PropertyBag.
    /// It is useful for resetting the test environment or ensuring that no location data persists between tests.
    /// </remarks>
    public void ClearGeolocation()
    {
        PropertyBag.Clear(PropertyBagKeys.Geolocation);
    }
}
