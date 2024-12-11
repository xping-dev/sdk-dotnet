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
    public readonly static PropertyBagKey IPAddress = new(nameof(IPAddress));
    /// <summary>
    /// Represents IP status operation. See more on <see href="https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ipstatus"/>.
    /// </summary>
    public readonly static PropertyBagKey IPStatus = new(nameof(IPStatus));
    #endregion Network Information

    #region DNS Lookup
    /// <summary>
    /// Array of matching IP addresses associated with a DNS name of the host.
    /// </summary>
    public readonly static PropertyBagKey DnsResolvedIPAddresses = new(nameof(DnsResolvedIPAddresses));
    #endregion DNS Lookup

    #region PING 
    /// <summary>
    /// Represents the number of times the ping data can be forwarded.
    /// </summary>
    public readonly static PropertyBagKey PingTTLOption = new(nameof(PingTTLOption));

    /// <summary>
    /// Represents the boolean value that controls fragmentation of the data sent to the remote host.
    /// </summary>
    public readonly static PropertyBagKey PingDontFragmetOption = new(nameof(PingDontFragmetOption));

    /// <summary>
    /// Represents the number of milliseconds taken to send an Internet Control Message Protocol (ICMP) echo request and 
    /// receive the corresponding ICMP echo reply message.
    /// </summary>
    public readonly static PropertyBagKey PingRoundtripTime = new(nameof(PingRoundtripTime));
    #endregion PING

    #region HTTP
    /// <summary>
    /// Represents the HTTP response message.
    /// </summary>
    public readonly static PropertyBagKey HttpResponseMessage = new(nameof(HttpResponseMessage));

    /// <summary>
    /// Represents the HTTP method.
    /// </summary>
    public readonly static PropertyBagKey HttpMethod = new(nameof(HttpMethod));

    /// <summary>
    /// Represents the HTTP status. 
    /// </summary>
    public readonly static PropertyBagKey HttpStatus = new(nameof(HttpStatus));

    /// <summary>
    /// Represents the HTTP reason phrase.
    /// </summary>
    public readonly static PropertyBagKey HttpReasonPhrase = new(nameof(HttpReasonPhrase));

    /// <summary>
    /// Represents the HTTP version.
    /// </summary>
    public readonly static PropertyBagKey HttpVersion = new(nameof(HttpVersion));

    /// <summary>
    /// Represents the HTTP content.
    /// </summary>
    public readonly static PropertyBagKey HttpContent = new(nameof(HttpContent));

    /// <summary>
    /// Represents the HTTP content headers.
    /// </summary>
    public readonly static PropertyBagKey HttpContentHeaders = new(nameof(HttpContentHeaders));

    /// <summary>
    /// Represents the boolean value determining whether to retry failing HTTP request.
    /// </summary>
    public readonly static PropertyBagKey HttpRetry = new(nameof(HttpRetry));

    /// <summary>
    /// Represents the boolean value that indicates whether to follow redirection responses.
    /// </summary>
    public readonly static PropertyBagKey HttpFollowRedirect = new(nameof(HttpFollowRedirect));

    /// <summary>
    /// Represents the collection of HTTP request headers as IDictionary&lt;string, IEnumerable&lt;string&gt;&gt;. 
    /// </summary>
    public readonly static PropertyBagKey HttpRequestHeaders = new(nameof(HttpRequestHeaders));

    /// <summary>
    /// Represents the collection of HTTP response headers as 
    /// <see href="https://learn.microsoft.com/en-us/dotnet/api/system.net.http.headers.httpresponseheaders"/>.
    /// </summary>
    public readonly static PropertyBagKey HttpResponseHeaders = new(nameof(HttpResponseHeaders));

    /// <summary>
    /// Represents the collection of trailing headers included in an HTTP response as
    /// <see href="https://learn.microsoft.com/en-us/dotnet/api/system.net.http.headers.httpresponseheaders"/>.
    /// </summary>
    public readonly static PropertyBagKey HttpResponseTrailingHeaders = new(nameof(HttpResponseTrailingHeaders));
    #endregion HTTP

    #region BROWSER
    /// <summary>
    /// Represents the coordinates of the geographical location of a device.
    /// </summary>
    /// <remarks>
    /// This property key is used to access the geolocation details in the context of automated browser tests.
    /// It leverages the Geolocation feature from the Playwright library to simulate the geographical position
    /// which can be useful for testing location-based functionalities of web applications.
    /// </remarks>
    public readonly static PropertyBagKey Geolocation = new(nameof(Geolocation));

    /// <summary>
    /// Represents the <see cref="BrowserResponseMessage"/>.
    /// </summary>
    public readonly static PropertyBagKey BrowserResponseMessage = new(nameof(BrowserResponseMessage));
    #endregion BROWSER

    #region HTML_VALIDATION
    /// <summary>
    /// Represents the name of the html validation method invoked.
    /// </summary>
    public readonly static PropertyBagKey MethodName = new(nameof(MethodName));
    #endregion HTML_VALIDATION
}
