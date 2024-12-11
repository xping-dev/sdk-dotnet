using Microsoft.Playwright;
using Moq;
using Xping.Sdk.Core.Clients.Browser;

namespace Xping.Sdk.Core.UnitTests.Clients.Browser;

public sealed class BrowserClientTests
{
    private Mock<IBrowser> _mockBrowser;
    private Mock<IBrowserType> _mockBrowserType;
    private Mock<IBrowserContext> _mockBrowserContext;
    private Mock<IPage> _mockPage;
    private Mock<IRequest> _mockRequest;
    private Mock<IResponse> _mockResponse;
    private BrowserConfiguration _configuration;
    private BrowserClient _client;
    private IHttpResponseHandler _handler;
    private IHttpRequestInterceptor _httpRequestInterceptor;

    private readonly Uri _testUrl = new("http://localhost");
    private readonly string _httpMethod = "GET";
    private readonly Dictionary<string, string> _httpResponseHeaders = [];

    [SetUp]
    public void SetUp()
    {
        _mockBrowser = new Mock<IBrowser>();
        _mockBrowserType = new Mock<IBrowserType>();
        _mockBrowserContext = new Mock<IBrowserContext>();
        _mockPage = new Mock<IPage>();
        _mockRequest = new Mock<IRequest>();
        _mockResponse = new Mock<IResponse>();
        _configuration = new BrowserConfiguration();
        _client = new BrowserClient(_mockBrowser.Object, _mockBrowserContext.Object, _mockPage.Object, _configuration);

        // Set up the Browser and browser's context to return mocked page.
        _mockBrowser.Setup(browser => browser.NewContextAsync(It.IsAny<BrowserNewContextOptions?>()))
            .ReturnsAsync(_mockBrowserContext.Object);
        _mockBrowserContext.Setup(context => context.NewPageAsync()).ReturnsAsync(_mockPage.Object);

        // Set up the GotoAsync method to to return mocked response.
        _mockPage.Setup(p => p.GotoAsync(It.IsAny<string>(), It.IsAny<PageGotoOptions?>()))
            .ReturnsAsync(_mockResponse.Object);
        _mockPage.Setup(p => p.ContentAsync()).ReturnsAsync("<html><body>Hello!</body></html>");
        _mockPage.SetupGet(p => p.Url).Returns(_testUrl.AbsoluteUri);
        _mockResponse.SetupGet(p => p.Request).Returns(_mockRequest.Object);
        _mockResponse.SetupGet(p => p.Headers).Returns(_httpResponseHeaders);
        _mockRequest.SetupGet(p => p.Method).Returns(_httpMethod);

        _handler = Mock.Of<IHttpResponseHandler>();
        _httpRequestInterceptor = Mock.Of<IHttpRequestInterceptor>();
    }

    [Test]
    public void ConstructorAssignsBrowserAndConfiguration()
    {
        // Arrange
        const string browserName = "Browser";
        const string browserVersion = "1.0";

        _mockBrowserType.SetupGet(t => t.Name).Returns(browserName);
        _mockBrowser.SetupGet(browser => browser.BrowserType).Returns(_mockBrowserType.Object);
        _mockBrowser.SetupGet(browser => browser.Version).Returns(browserVersion);

        Assert.Multiple(() =>
        {
            // Assert that the constructor has correctly assigned the browser and configuration
            Assert.That(_client.Name, Is.EqualTo(browserName));
            Assert.That(_client.Version, Is.EqualTo(browserVersion));
        });
    }

    [Test]
    public void SendAsyncThrowsArgumentNullExceptionWhenUriIsNull()
    {
        // Arrange
        Uri uri = null!;

        // Assert
        var exception = Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _client.SendAsync(uri).ConfigureAwait(false));

        Assert.That(exception, Is.Not.EqualTo(null));
    }

    [Test]
    public void SendAsyncThrowsArgumentNullExceptionWhenHandlerIsNull()
    {
        // Arrange
        Uri uri = _testUrl;

        // Assert
        var exception = Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _client.SendAsync(uri, handler: null!).ConfigureAwait(false));

        Assert.That(exception, Is.Not.EqualTo(null));
    }

    [Test]
    public void SendAsyncThrowsArgumentNullExceptionWhenRequestInterceptorIsNull()
    {
        // Arrange
        Uri uri = _testUrl;

        // Assert
        var exception = Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _client.SendAsync(uri, requestInterceptor: null!).ConfigureAwait(false));

        Assert.That(exception, Is.Not.EqualTo(null));
    }

    [Test]
    public async Task PageIsClosedBeforeNewPageIsOpened()
    {
        // BrowserClient manages single page only and thuse previous page has to be close before new can be opened.

        // Act
        await _client.SendAsync(_testUrl).ConfigureAwait(false);

        // Assert
        _mockPage.Verify(page => page.CloseAsync(It.IsAny<PageCloseOptions>()), Times.Once);
    }

    [Test]
    public async Task ContextIsClosedBeforeNewContextIsCreated()
    {
        // BrowserClient manages single context only and thus previous context has to be close before new can be
        // opened.

        // Act
        await _client.SendAsync(_testUrl).ConfigureAwait(false);

        // Assert
        _mockBrowserContext.Verify(context => context.CloseAsync(It.IsAny<BrowserContextCloseOptions>()), Times.Once);
    }

    [Test]
    public async Task PageIsNotClosedWhenItIsNull()
    {
        // Arrange
        _client = new BrowserClient(_mockBrowser.Object, _mockBrowserContext.Object, page: null!, _configuration);

        // Act
        await _client.SendAsync(_testUrl).ConfigureAwait(false);

        // Assert
        _mockPage.Verify(page => page.CloseAsync(null), Times.Never);
    }

    [Test]
    public async Task ContextIsNotClosedWhenItIsNull()
    {
        // Arrange
        _client = new BrowserClient(_mockBrowser.Object, context: null!, _mockPage.Object, _configuration);

        // Act
        await _client.SendAsync(_testUrl).ConfigureAwait(false);

        // Assert
        _mockBrowserContext.Verify(context => context.CloseAsync(null), Times.Never);
    }

    [Test]
    public void PageResponseEventHandlerIsAssignedWhenHandlerIsNotNull()
    {
        // Act
        _client.SendAsync(_testUrl, handler: _handler).ConfigureAwait(false);

        // Assert
        _mockPage.VerifyAdd(page => page.Response += It.IsAny<EventHandler<IResponse>>(), Times.Once);
    }

    [Test]
    public void PageResponseInvokesHandleResponseWhenRaised()
    {
        // Arrange
        var wasCalled = false;
        EventHandler<IResponse> responseHandler = (sender, response) =>
        {
            wasCalled = true;
        };

        Mock.Get(_handler)
            .Setup(h => h.HandleResponse(It.IsAny<object>(), It.IsAny<IResponse>()))
            .Callback(responseHandler);

        // Act
        _client.SendAsync(_testUrl, _handler).ConfigureAwait(false);

        // Trigger the event to simulate a response
        _mockPage.Raise(page => page.Response += _handler.HandleResponse, this, Mock.Of<IResponse>());

        // Assert
        Assert.That(wasCalled, Is.True, "The response handler was not called.");
    }

    [Test]
    public void PageRouteAsyncMatchesAnyUrlWhileRouting()
    {
        // Arrange
        const string expectedUrlPattern = "**/*";

        // Act
        _client.SendAsync(_testUrl, _httpRequestInterceptor).ConfigureAwait(false);

        // Assert
        _mockPage.Verify(page =>
            page.RouteAsync(
                It.Is<string>(_p => _p == expectedUrlPattern),
                It.IsAny<Func<IRoute, Task>>(),
                It.IsAny<PageRouteOptions>()),
            Times.Once);
    }

    [Test]
    public void PageRouteAsyncInvokesHandleAsyncWhileRouting()
    {
        // Act
        _client.SendAsync(_testUrl, _httpRequestInterceptor).ConfigureAwait(false);

        // Assert
        _mockPage.Verify(page =>
            page.RouteAsync(
                It.IsAny<string>(),
                It.Is<Func<IRoute, Task>>(_p => _p == _httpRequestInterceptor.HandleAsync),
                It.IsAny<PageRouteOptions>()),
            Times.Once);
    }

    [Test]
    public void PageGotoAsyncUsesAbsoluteUri()
    {
        // Arrange
        var url = _testUrl;

        // Act
        _client.SendAsync(url).ConfigureAwait(false);

        // Assert
        _mockPage.Verify(page =>
            page.GotoAsync(
                It.Is<string>(_p => _p == url.AbsoluteUri),
                It.IsAny<PageGotoOptions>()),
            Times.Once);
    }

    [Test]
    public void PageGotoAsyncUsesConfiguredTimeoutValue()
    {
        // Arrange
        _configuration.HttpRequestTimeout = TimeSpan.FromSeconds(15);

        // Act
        _client.SendAsync(_testUrl).ConfigureAwait(false);

        // Assert
        _mockPage.Verify(page =>
            page.GotoAsync(
                It.IsAny<string>(),
                It.Is<PageGotoOptions?>(_p =>
                    _p != null && _p.Timeout == _configuration.HttpRequestTimeout.TotalMilliseconds)),
            Times.Once);
    }

    [Test]
    public async Task SendAsyncReflectsCancellationRequest()
    {
        // Arrange
        var response = Mock.Of<IResponse>();
        var timeout = TimeSpan.FromMilliseconds(1000);
        using var cancellationSource = new CancellationTokenSource(timeout);

        // Set up the GotoAsync method to delay for a specified time before completing
        _mockPage
            .Setup(p => p.GotoAsync(It.IsAny<string>(), It.IsAny<PageGotoOptions?>()))
            .Returns<string, PageGotoOptions?>(
                (url, options) =>
                    // Return mocked IResponse after the delay
                    Task.Delay(timeout * 2).ContinueWith<IResponse?>(t => response, TaskScheduler.Default));

        // Act
        var result = await _client.SendAsync(_testUrl, cancellationSource.Token)
            .ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(cancellationSource.Token.IsCancellationRequested, Is.True);
            Assert.That(result, Is.Null);
        });
    }
}
