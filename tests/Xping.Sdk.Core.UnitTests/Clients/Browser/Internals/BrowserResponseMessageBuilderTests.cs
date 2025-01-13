/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net;
using Microsoft.Playwright;
using Moq;
using Xping.Sdk.Core.Clients.Browser.Internals;

namespace Xping.Sdk.Core.UnitTests.Clients.Browser.Internals;

public sealed class BrowserResponseMessageBuilderTests
{
    private Mock<IBrowser> _mockBrowser;
    private Mock<IBrowserContext> _mockBrowserContext;
    private Mock<IPage> _mockPage;
    private Mock<IRequest> _mockRequest;
    private Mock<IResponse> _mockResponse;

    private BrowserResponseMessageBuilder _browserResponseMessageBuilder;

    private readonly Uri _testUrl = new("http://localhost");
    private readonly string _httpMethod = "GET";
    private readonly Dictionary<string, string> _httpResponseHeaders = [];

    [SetUp]
    public void SetUp()
    {
        _mockBrowser = new Mock<IBrowser>();
        _mockBrowserContext = new Mock<IBrowserContext>();
        _mockPage = new Mock<IPage>();
        _mockResponse = new Mock<IResponse>();
        _mockRequest = new Mock<IRequest>();

        // Setup mocks to return page content
        _mockPage.Setup(p => p.ContentAsync()).ReturnsAsync("<html><body>Hello!</body></html>");
        _mockPage.SetupGet(p => p.Url).Returns(_testUrl.AbsoluteUri);
        _mockResponse.SetupGet(p => p.Request).Returns(_mockRequest.Object);
        _mockResponse.SetupGet(p => p.Headers).Returns(_httpResponseHeaders);
        _mockRequest.SetupGet(p => p.Method).Returns(_httpMethod);
        _mockRequest.SetupGet(p => p.Url).Returns(_testUrl.AbsoluteUri);

        _browserResponseMessageBuilder = new BrowserResponseMessageBuilder();
        _browserResponseMessageBuilder.Build(browser: _mockBrowser.Object);
        _browserResponseMessageBuilder.Build(context: _mockBrowserContext.Object);
        _browserResponseMessageBuilder.Build(page: _mockPage.Object);
        _browserResponseMessageBuilder.Build(response: _mockResponse.Object);
    }

    [Test]
    public void BuildBrowserThrowsArgumentNullExceptionWhenBrowserIsNull()
    {
        // Arrange
        var builder = new BrowserResponseMessageBuilder();

        // Assert
        Assert.Throws<ArgumentNullException>(() => builder.Build(browser: null!));
    }

    [Test]
    public void BuildBrowserContextAllowsNullValue()
    {
        // Arrange
        var builder = new BrowserResponseMessageBuilder();

        // Assert
        Assert.DoesNotThrow(() => builder.Build(context: null));
    }

    [Test]
    public void BuildPageAllowsNullValue()
    {
        // Arrange
        var builder = new BrowserResponseMessageBuilder();

        // Assert
        Assert.DoesNotThrow(() => builder.Build(page: null));
    }


    [Test]
    public void BuildResponseAllowsNullValue()
    {
        // Arrange
        var builder = new BrowserResponseMessageBuilder();

        // Assert
        Assert.DoesNotThrow(() => builder.Build(response: null));
    }

    [Test]
    public async Task BuildDoesNotThrowWhenCanConstructPage()
    {
        using var result = await _browserResponseMessageBuilder.Build().ConfigureAwait(false);

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void BuildThrowsInvalidOperationExceptionWhenContextIsNull()
    {
        // Arrange
        var builder = new BrowserResponseMessageBuilder();
        builder.Build(browser: _mockBrowser.Object);
        builder.Build(context: null!);
        builder.Build(page: _mockPage.Object);
        builder.Build(response: _mockResponse.Object);

        // Act
        var exception = Assert.ThrowsAsync<InvalidOperationException>(builder.Build);

        // Assert
        Assert.That(exception, Is.Not.Null);
    }

    [Test]
    public void BuildThrowsInvalidOperationExceptionWhenPageIsNull()
    {
        // Arrange
        var builder = new BrowserResponseMessageBuilder();
        builder.Build(browser: _mockBrowser.Object);
        builder.Build(context: _mockBrowserContext.Object);
        builder.Build(page: null!);
        builder.Build(response: _mockResponse.Object);

        // Act
        var exception = Assert.ThrowsAsync<InvalidOperationException>(builder.Build);

        // Assert
        Assert.That(exception, Is.Not.Null);
    }

    [Test]
    public void BuildThrowsInvalidOperationExceptionWhenResponseIsNull()
    {
        // Arrange
        var builder = new BrowserResponseMessageBuilder();
        builder.Build(browser: _mockBrowser.Object);
        builder.Build(context: _mockBrowserContext.Object);
        builder.Build(page: _mockPage.Object);
        builder.Build(response: null!);

        // Act
        var exception = Assert.ThrowsAsync<InvalidOperationException>(builder.Build);

        // Assert
        Assert.That(exception, Is.Not.Null);
    }

    [Test]
    [TestCase(HttpStatusCode.OK)]
    [TestCase(HttpStatusCode.Redirect)]
    public async Task StatusCodeReflectsResponseStatus(HttpStatusCode statusCode)
    {
        // Arrange
        _mockResponse.SetupGet(m => m.Status).Returns((int)statusCode);

        // Act
        using var responseMessage = await _browserResponseMessageBuilder.Build().ConfigureAwait(false);

        // Assert
        Assert.That(responseMessage.HttpResponseMessage.StatusCode, Is.EqualTo(statusCode));
    }

    [Test]
    public async Task ResponseContentReflectsPageContent()
    {
        // Arrange
        const string htmlContent = "<html><body>Welcome</body></html>";

        _mockPage.Setup(p => p.ContentAsync()).ReturnsAsync(htmlContent);

        // Act
        using var responseMessage = await _browserResponseMessageBuilder.Build().ConfigureAwait(false);
        var data = await responseMessage.HttpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(responseMessage.HttpResponseMessage.Content, Is.TypeOf<StringContent>());
            Assert.That(data, Is.EqualTo(htmlContent));
        });
    }

    [Test]
    public async Task ResponseHeadersReflectsPageHeaders()
    {
        // Arrange
        var headers = new Dictionary<string, string> 
        { 
            { "SERVER", "ServerTest" }
        };
        
        _mockResponse.SetupGet(p => p.Headers).Returns(headers);

        // Act
        using var responseMessage = await _browserResponseMessageBuilder.Build().ConfigureAwait(false);

        // Collect all headers from the response message
        var responseHeaders =
            responseMessage.HttpResponseMessage.Headers.ToDictionary(
                h => h.Key.ToUpperInvariant(), h => string.Join(";", h.Value));
        var contentHeaders =
            responseMessage.HttpResponseMessage.Content.Headers.ToDictionary(
                h => h.Key.ToUpperInvariant(), h => string.Join(";", h.Value));
        var trailingHeaders =
            responseMessage.HttpResponseMessage.TrailingHeaders.ToDictionary(
                h => h.Key.ToUpperInvariant(), h => string.Join(";", h.Value));

        // Combine all headers; if there are duplicate keys across the dictionaries, the value from the dictionary that
        // appears first in the concatenation sequence will be used.
        var combinedHeaders = responseHeaders
            .Concat(contentHeaders)
            .Concat(trailingHeaders)
            .GroupBy(pair => pair.Key)
            .ToDictionary(group => group.Key, group => group.First().Value);

        // Assert
        Assert.That(combinedHeaders, Is.EqualTo(headers).AsCollection);
    }

    [Test]
    public async Task RequestMessageIsAvailableInTheResponse()
    {
        // Act
        using var responseMessage = await _browserResponseMessageBuilder.Build().ConfigureAwait(false);

        // Assert
        Assert.That(responseMessage.HttpResponseMessage.RequestMessage, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(responseMessage.HttpResponseMessage.RequestMessage.Method.ToString(), Is.EqualTo(_httpMethod));
            Assert.That(responseMessage.HttpResponseMessage.RequestMessage.RequestUri, Is.EqualTo(_testUrl));
        });
    }
}
