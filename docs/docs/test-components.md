# Test Components Overview

Test components in the Xping SDK are designed to execute specific actions or validation test operations. When added to the execution pipeline, these components become individual test steps, ensuring a modular and flexible approach to testing.

## DNS Lookup

Using `TestAgent.UseDnsLookup()` configures the execution pipeline to run the @Xping.Sdk.Actions.DnsLookup action test step.

The DnsLookup mechanism works by sending a query to a DNS server and receiving a response that contains the IP address of the requested domain name. The query can be recursive or iterative, depending on the configuration of the DNS server. A recursive query instructs the DNS server to return the final answer or an error, while an iterative query instructs the DNS server to return the best answer it has or a referral to another DNS server. The @Xping.Sdk.Actions.DnsLookup component uses the mechanisms provided by the operating system to perform DNS lookups. This means that it can handle both iterative and recursive queries, depending on the configuration of the DNS server. The DNS lookup method can also follow the referrals until it reaches the authoritative DNS server that has the definitive answer. However, the component does not expose any option to specify the query type or the DNS server to use.

> [!NOTE] 
> The @Xping.Sdk.Actions.DnsLookup component operates without any dependency on other components. Please also note, the DNS lookup method may use cached data from the local machine if the DNS information for the host has been cached previously.

## IP Address Accessibility Check

Using `TestAgent.UseIPAddressAccessibilityCheck()` configures the execution pipeline to run the @Xping.Sdk.Actions.IPAddressAccessibilityCheck action test step.

The @Xping.Sdk.Actions.IPAddressAccessibilityCheck component uses the <see cref="Ping"/> class to send an Internet Control Message Protocol (ICMP) echo request to the IP address and receive an echo reply. The Ping class can handle both synchronous and asynchronous operations, and can also provide information such as the round-trip time, the time-to-live, and the buffer size of the ICMP packets. The @Xping.Sdk.Actions.IPAddressAccessibilityCheck component can use these information to determine the availability and performance of the IP address.

> [!NOTE]
> The @Xping.Sdk.Actions.IPAddressAccessibilityCheck component requires the DnsLookup component to be registered before it in the pipeline, because it depends on the DNS resolution results.

You can modify the behavior of the @Xping.Sdk.Actions.IPAddressAccessibilityCheck by changing its configuration options. For instance you can change `TTL` which is the number of routing nodes that can forward the <see cref="Ping"/> data before it is discarded. You can also update the maximum amount of time to wait for the ICMP echo reply message after sending the echo message.

## HTTP Client and Browser Client Requests

Xping uses two mechanisms to requests data from the server: `HttpClient` and `BrowserClient`.

* [HttpClient](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) is a .NET class that provides a high-level abstraction for sending and receiving HTTP requests and responses. It is fast, lightweight, and easy to use. However, it does not process HTML responses or run JavaScript code, which may limit its ability to validate server responses.

* @Xping.Sdk.Core.Clients.Browser.BrowserClient are browsers that run without a graphical user interface, but can still render web pages and execute JavaScript code. They are useful for simulating user interactions and testing dynamic web applications. However, they are slower, heavier, and more complex than HttpClient.

Using `TestAgent.UseHttpClient()` or `TestAgent.UseBrowserClient()` configures the execution pipeline to run the @Xping.Sdk.Actions.HttpClientRequestSender or the @Xping.Sdk.Actions.BrowserRequestSender action test steps.

> [!NOTE]
> Before using these test components, you need to register the necessary services by calling the `AddHttpClientFactory()` and the `AddBrowserClientFactory()` methods respectively.

There is another mechanism to request data from a server. When executing integration tests through `WebApplicationFactory`, which runs the web application in memory, it prevents you from using headless browsers. In such a scenario, `WebApplicationFactory` provides a method called `CreateClient`, which creates an `HttpClient` instance that can interact with the web application in memory. You need to register the necessary services via `AddTestServerHttpClientFactory` and pass the `CreateClient` method so that the Xping SDK library can access the `HttpClient` instance to interact with the web application in memory.

Check our [IntegrationTesting](https://github.com/xping-dev/sdk/tree/main/samples/IntegrationTesting) sample for an example code.

## HTTP Response Validation

Using `TestAgent.UseHttpValidation(Action<IHttpResponse>)` configures the execution pipeline to run the @Xping.Sdk.Validations.HttpResponse.HttpResponseValidator validation test step.

It takes a lambda function as a parameter, which receives a `IHttpResponse` object representing the HTTP response of the page being tested.

```csharp
TestAgent.UseHttpValidation(response =>
{
    Expect(response)
        .ToHaveSuccessStatusCode()
        .ToHaveResponseTimeLessThan(TimeSpan.FromSeconds(30))
        .ToHaveHeaderWithValue(HeaderNames.Server, value: "Google");
});
```

|HTTP Response Assert Function|Description|
|-----------------------------|-----------|
|`ToHaveSuccessStatusCode()`|Ensures that the HTTP response has a successful (2xx) status code.|
|`ToHaveStatusCode(HttpStatusCode)`|Validates that the HTTP response has the specified status code.|
|`ToHaveHttpHeader(string, TextOptions?)`|Validates that the HTTP response contains a specific header.|
|`ToHaveHeaderWithValue(string, string, TextOptions?)`|Validates that the HTTP response contains a specific header with value.|
|`ToHaveContentType(string contentType, TextOptions?)`|Validates the Content-Type header of the HTTP response.|
|`ToHaveBodyContaining(string expectedContent, TextOptions?)`|Validates that the response body contains a specific string.|
|`ToHaveResponseTimeLessThan(TimeSpan maxDuration)`|Validates that the response time is less than a specified duration.|

## HTML Response Validation

Using `TestAgent.UseHtmlValidation(Action<IHtmlContent>)` configures the execution pipeline to run the @Xping.Sdk.Validations.Content.Html.HtmlContentValidator validation test step.

> [!Note] 
> For simulating user interactions and testing dynamic web applications see [Browser Page Validation](#browser-page-validation)

It takes a lambda function as a parameter, which receives a @Xping.Sdk.Validations.Content.Html.IHtmlContent object representing the HTML response of the page being tested. 

```csharp
TestAgent.UseHtmlValidation(html =>
{
    Expect(html)
        .ToHaveMaxDocumentSize(maxSizeInBytes: 153600) // 150kB
        .ToHaveTitle("Home page - WebApp");
});
```

|HTML Content Assert Function|Description|
|----------------------------|-----------|
|`ToHaveTitle(string title, TextOptions? options = null)`|Validates that an HTML document has the specified title. The title is typically found within the &lt;title&gt; tag in the &lt;head&gt; section of an HTML document.|
|`ToHaveTitle(Regex title, TextOptions? options = null)`|Validates that title of HTML document matches the specified regex. The title is typically found within the &lt;title&gt; tag in the &lt;head&gt; section of an HTML document.|
|`ToHaveMaxDocumentSize(int maxSizeInBytes)`|Validates that the size in bytes of an HTML document is equal to or less than the specified maximum size.|
|`ToHaveMetaTag(HtmlAttribute attribute, int? expectedCount = null)`|Validates if the HTML meta tag with the specified attribute exist.|
|`ToHaveMetaTag(HtmlAttribute attribute, string expectedContent, TextOptions? options = null)`|Validates if the HTML meta tag with the specified attribute has the expected content value.|
|`ToHaveImageWithAltText(string altText, TextOptions? options = null)`|Validates that an HTML document contains an image with the specified alt text.|
|`ToHaveImageWithAltText(Regex altText, TextOptions? options = null)`|Validates that an HTML document contains an image with the specified alt text.|
|`ToHaveImageWithSrc(string src, TextOptions? options = null)`|Validates that an HTML document contains an image with the specified source URL.|
|`ToHaveImageWithSrc(Regex src, TextOptions? options = null)`|Validates that an HTML document contains an image with the specified source URL.|
|`ToHaveExternalStylesheet(string externalStylesheet, TextOptions? options = null)`|Validates that an HTML document includes the specified external stylesheet.|
|`ToHaveExternalScript(string externalScript, TextOptions? options = null)`|Validates that an HTML document includes the specified external script.|

## Browser Page Validation

Using `TestAgent.UsePageValidation(Action<IPage>)` configures the execution pipeline to run the @Xping.Sdk.Validations.Content.Page.PageContentValidator validation test step.

It takes a lambda function as a parameter, which receives a [IPage](https://playwright.dev/dotnet/docs/api/class-page) object representing the browser's page of the URL being tested.

```csharp
TestAgent.UsePageValidation(async page =>
{
    await Expect(page).ToHaveTitleAsync("STORE");
    await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Log in" })).ToBeVisibleAsync();

    var categories = page.Locator(selector: "//div[@class='list-group']/a");
    await Expect(categories).ToHaveCountAsync(count: 4);
});
```

To ensure you have access to the latest documentation and assertion functions provided by Playwright, please refer to the following links:

 - Page Assertions: For detailed information on page assertion functions, visit Playwright [Page Assertions](https://playwright.dev/dotnet/docs/api/class-pageassertions).
 - Locator Assertions: For detailed information on locator assertion functions, visit Playwright [Locator Assertions](https://playwright.dev/dotnet/docs/api/class-locatorassertions).

These links will provide you with the most up-to-date resources and examples to effectively utilize Playwright’s powerful assertion capabilities.