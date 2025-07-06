/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Common;

/// <summary>
/// This class is used to provide a set of predefined keys that can be used with the <see cref="PropertyBag{TValue}"/> 
/// class.
/// </summary>
/// <remarks>
/// Since the PropertyBagKeys class is a static class, it cannot be instantiated. The class contains the properties
/// for the common areas for instance, area related to Network Information or HTTP communication.
/// </remarks>
public static class PropertyBagKeys
{
    #region Network Information
    /// <summary>
    /// Represents IP address. See more on <see href="https://learn.microsoft.com/en-us/dotnet/api/system.net.ipaddress"/>.
    /// </summary>
    public static readonly PropertyBagKey IPAddress = new(nameof(IPAddress));
    /// <summary>
    /// Represents IP status operation. See more on <see href="https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ipstatus"/>.
    /// </summary>
    public static readonly PropertyBagKey IPStatus = new(nameof(IPStatus));
    #endregion Network Information

    #region DNS Lookup
    /// <summary>
    /// Array of matching IP addresses associated with a DNS name of the host.
    /// </summary>
    public static readonly PropertyBagKey DnsResolvedIPAddresses = new(nameof(DnsResolvedIPAddresses));
    #endregion DNS Lookup

    #region PING 
    /// <summary>
    /// Represents the number of times the ping data can be forwarded.
    /// </summary>
    public static readonly PropertyBagKey PingTtlOption = new(nameof(PingTtlOption));

    /// <summary>
    /// Represents the boolean value that controls fragmentation of the data sent to the remote host.
    /// </summary>
    public static readonly PropertyBagKey PingDontFragmentOption = new(nameof(PingDontFragmentOption));

    /// <summary>
    /// Represents the number of milliseconds taken to send an Internet Control Message Protocol (ICMP) echo request and 
    /// receive the corresponding ICMP echo reply message.
    /// </summary>
    public static readonly PropertyBagKey PingRoundtripTime = new(nameof(PingRoundtripTime));
    #endregion PING

    #region HTTP
    /// <summary>
    /// Represents the HTTP response message.
    /// </summary>
    public static readonly PropertyBagKey HttpResponseMessage = new(nameof(HttpResponseMessage));

    /// <summary>
    /// Represents the HTTP method.
    /// </summary>
    public static readonly PropertyBagKey HttpMethod = new(nameof(HttpMethod));
    
    /// <summary>
    /// Represents the HTTP status. 
    /// </summary>
    public static readonly PropertyBagKey HttpResponseStatus = new(nameof(HttpResponseStatus));

    /// <summary>
    /// Represents the HTTP reason phrase.
    /// </summary>
    public static readonly PropertyBagKey HttpResponsePhrase = new(nameof(HttpResponsePhrase));

    /// <summary>
    /// Represents the HTTP version.
    /// </summary>
    public static readonly PropertyBagKey HttpResponseVersion = new(nameof(HttpResponseVersion));

    /// <summary>
    /// Represents the HTTP content.
    /// </summary>
    public static readonly PropertyBagKey HttpResponseContent = new(nameof(HttpResponseContent));
    
    /// <summary>
    /// Represents the HTTP content.
    /// </summary>
    public static readonly PropertyBagKey HttpRequestContent = new(nameof(HttpRequestContent));

    /// <summary>
    /// Represents the HTTP content headers.
    /// </summary>
    public static readonly PropertyBagKey HttpContentHeaders = new(nameof(HttpContentHeaders));
    
    /// <summary>
    /// Represents a value that specifies the maximum time to wait for a network request or a browser operation to
    /// finish.
    /// </summary>
    public static readonly PropertyBagKey HttpRequestTimeout = new (nameof(HttpRequestTimeout));

    /// <summary>
    /// Represents the boolean value determining whether to retry a failing HTTP request.
    /// </summary>
    public static readonly PropertyBagKey HttpRetry = new(nameof(HttpRetry));

    /// <summary>
    /// Represents the boolean value that indicates whether to follow redirection responses.
    /// </summary>
    public static readonly PropertyBagKey HttpFollowRedirect = new(nameof(HttpFollowRedirect));
    
    /// <summary>
    /// Represents the maximum number of allowed HTTP redirects.
    /// </summary>
    public static readonly PropertyBagKey MaxRedirections = new(nameof(MaxRedirections));

    /// <summary>
    /// Represents the collection of HTTP request headers as IDictionary&lt;string, IEnumerable&lt;string&gt;&gt; 
    /// </summary>
    public static readonly PropertyBagKey HttpRequestHeaders = new(nameof(HttpRequestHeaders));

    /// <summary>
    /// Represents the collection of HTTP response headers as 
    /// <see href="https://learn.microsoft.com/en-us/dotnet/api/system.net.http.headers.httpresponseheaders"/>.
    /// </summary>
    public static readonly PropertyBagKey HttpResponseHeaders = new(nameof(HttpResponseHeaders));

    /// <summary>
    /// Represents the collection of trailing headers included in an HTTP response as
    /// <see href="https://learn.microsoft.com/en-us/dotnet/api/system.net.http.headers.httpresponseheaders"/>.
    /// </summary>
    public static readonly PropertyBagKey HttpResponseTrailingHeaders = new(nameof(HttpResponseTrailingHeaders));
    #endregion HTTP

    #region BROWSER
    
    /// <summary>
    /// Represents the name of the browser being used for automation.
    /// </summary>
    /// <remarks>
    /// This property key is used to store the browser type (e.g., Chromium, Firefox, or WebKit) that is
    /// currently being used for automated testing. It is particularly useful when working with multiple
    /// browser types and need to track or make decisions based on the active browser.
    /// </remarks>
    public static readonly PropertyBagKey BrowserName = new(nameof(BrowserName));

    /// <summary>
    /// Represents the version of the browser being used for automation.
    /// </summary>
    public static readonly PropertyBagKey BrowserVersion = new(nameof(BrowserVersion));
    
    /// <summary>
    /// Represents a boolean flag that determines whether the browser runs in headless mode during automation.
    /// </summary>
    /// <remarks>
    /// When set to true, the browser operates without a graphical user interface (headless mode),
    /// which is useful for automated testing in CI/CD environments. When false, the browser runs
    /// with its full graphical interface visible, which can be helpful for debugging and development.
    /// </remarks>
    public static readonly PropertyBagKey BrowserHeadless = new(nameof(BrowserHeadless));

    /// <summary>
    /// Represents the coordinates of the device geographical location.
    /// </summary>
    /// <remarks>
    /// This property key is used to access the geolocation details in the context of automated browser tests.
    /// It leverages the Geolocation feature from the Playwright library to simulate the geographical position,
    /// which can be useful for testing location-based functionalities of web applications.
    /// </remarks>
    public static readonly PropertyBagKey Geolocation = new(nameof(Geolocation));

    /// <summary>
    /// Represents the <see cref="BrowserResponseMessage"/>.
    /// </summary>
    public static readonly PropertyBagKey BrowserResponseMessage = new(nameof(BrowserResponseMessage));
    #endregion BROWSER

    #region HTML_VALIDATION
    /// <summary>
    /// Represents the name of the HTML validation method invoked.
    /// </summary>
    public static readonly PropertyBagKey MethodName = new(nameof(MethodName));
    #endregion HTML_VALIDATION
}
