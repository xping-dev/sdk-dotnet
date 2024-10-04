using Microsoft.Playwright;
using Moq;
using Xping.Sdk.Core.Clients.Browser;

namespace Xping.Sdk.Core.UnitTests.Clients.Browser;

public sealed class BrowserResponseMessageTests
{
    class HttpResponseMessageUnderTest : HttpResponseMessage
    {
        public bool Disposed { get; private set; }
        public int DisposedCount { get; private set; }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Disposed = true;
            ++DisposedCount;
        }
    }

    private Mock<IBrowser> _mockBrowser;
    private Mock<IBrowserContext> _mockBrowserContext;
    private Mock<IPage> _mockPage;
    private HttpResponseMessageUnderTest _httpResponseMessage;

    [SetUp]
    public void Setup()
    {
        _mockBrowser = new Mock<IBrowser>();
        _mockBrowserContext = new Mock<IBrowserContext>();
        _mockPage = new Mock<IPage>();
        _httpResponseMessage = new();
    }

    [Test]
    public void ConstructorAssignsPageAndHttpResponseMessage()
    {
        // Act
        using var browserResponseMessage = new BrowserResponseMessage(
            _mockBrowser.Object, _mockBrowserContext.Object, _mockPage.Object, _httpResponseMessage);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(browserResponseMessage.Page, Is.EqualTo(_mockPage.Object));
            Assert.That(browserResponseMessage.HttpResponseMessage, Is.EqualTo(_httpResponseMessage));
        });
    }

    [Test]
    public async Task DisposeAsyncClosesPage()
    {
        var browserResponseMessage = new BrowserResponseMessage(
            _mockBrowser.Object, _mockBrowserContext.Object, _mockPage.Object, _httpResponseMessage);

        // Act
        await browserResponseMessage.DisposeAsync().ConfigureAwait(false);

        // Assert
        _mockPage.Verify(page => page.CloseAsync(It.IsAny<PageCloseOptions?>()), Times.Once());
    }

    [Test]
    public async Task DisposeAsyncCalledMultipleTimesClosesPageOnce()
    {
        var browserResponseMessage = new BrowserResponseMessage(
            _mockBrowser.Object, _mockBrowserContext.Object, _mockPage.Object, _httpResponseMessage);

        // Act
        await browserResponseMessage.DisposeAsync().ConfigureAwait(false);
        await browserResponseMessage.DisposeAsync().ConfigureAwait(false); // Call DisposeAsync a second time

        // Assert
        _mockPage.Verify(page => page.CloseAsync(It.IsAny<PageCloseOptions?>()), Times.Once());
    }

    [Test]
    public async Task DisposeAsyncClosesContext()
    {
        var browserResponseMessage = new BrowserResponseMessage(
            _mockBrowser.Object, _mockBrowserContext.Object, _mockPage.Object, _httpResponseMessage);

        // Act
        await browserResponseMessage.DisposeAsync().ConfigureAwait(false);

        // Assert
        _mockBrowserContext.Verify(context => 
            context.CloseAsync(It.IsAny<BrowserContextCloseOptions?>()), Times.Once());
    }

    [Test]
    public async Task DisposeAsyncCalledMultipleTimesClosesContextOnce()
    {
        var browserResponseMessage = new BrowserResponseMessage(
            _mockBrowser.Object, _mockBrowserContext.Object, _mockPage.Object, _httpResponseMessage);

        // Act
        await browserResponseMessage.DisposeAsync().ConfigureAwait(false);
        await browserResponseMessage.DisposeAsync().ConfigureAwait(false); // Call DisposeAsync a second time

        // Assert
        _mockBrowserContext.Verify(context =>
             context.CloseAsync(It.IsAny<BrowserContextCloseOptions?>()), Times.Once());
    }

    [Test]
    public async Task DisposeAsyncDisposesBrowser()
    {
        var browserResponseMessage = new BrowserResponseMessage(
            _mockBrowser.Object, _mockBrowserContext.Object, _mockPage.Object, _httpResponseMessage);

        // Act
        await browserResponseMessage.DisposeAsync().ConfigureAwait(false);

        // Assert
        _mockBrowser.Verify(browser => browser.DisposeAsync(), Times.Once());
    }

    [Test]
    public async Task DisposeAsyncCalledMultipleTimesDisposesBrowserOnce()
    {
        var browserResponseMessage = new BrowserResponseMessage(
            _mockBrowser.Object, _mockBrowserContext.Object, _mockPage.Object, _httpResponseMessage);

        // Act
        await browserResponseMessage.DisposeAsync().ConfigureAwait(false);
        await browserResponseMessage.DisposeAsync().ConfigureAwait(false); // Call DisposeAsync a second time

        // Assert
        _mockBrowser.Verify(browser => browser.DisposeAsync(), Times.Once());
    }

    [Test]
    public async Task DisposeAsyncDisposesHttpResponseMessage()
    {
        var browserResponseMessage = new BrowserResponseMessage(
            _mockBrowser.Object, _mockBrowserContext.Object, _mockPage.Object, _httpResponseMessage);

        // Act
        await browserResponseMessage.DisposeAsync().ConfigureAwait(false);

        // Assert
        Assert.That(_httpResponseMessage.Disposed, Is.True);
    }

    [Test]
    public async Task DisposeAsyncCalledMultipleTimesDisposesHttpResponseMessageOnce()
    {
        var browserResponseMessage = new BrowserResponseMessage(
            _mockBrowser.Object, _mockBrowserContext.Object, _mockPage.Object, _httpResponseMessage);

        // Act
        await browserResponseMessage.DisposeAsync().ConfigureAwait(false);
        await browserResponseMessage.DisposeAsync().ConfigureAwait(false); // Call DisposeAsync a second time

        // Assert
        Assert.That(_httpResponseMessage.DisposedCount, Is.EqualTo(1));
    }

    [Test]
    public void DisposeDisposesHttpResponseMessage()
    {
        var browserResponseMessage = new BrowserResponseMessage(
            _mockBrowser.Object, _mockBrowserContext.Object, _mockPage.Object, _httpResponseMessage);

        // Act
        browserResponseMessage.Dispose();

        // Assert
        Assert.That(_httpResponseMessage.Disposed, Is.True);
    }

    [Test]
    public void DisposeCalledMultipleTimesDisposesHttpResponseMessageOnce()
    {
        var browserResponseMessage = new BrowserResponseMessage(
            _mockBrowser.Object, _mockBrowserContext.Object, _mockPage.Object, _httpResponseMessage);

        // Act
        browserResponseMessage.Dispose();
        browserResponseMessage.Dispose();

        // Assert
        Assert.That(_httpResponseMessage.DisposedCount, Is.EqualTo(1));
    }
}
