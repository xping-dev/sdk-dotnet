/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Microsoft.Playwright;
using Moq;
using Xping.Sdk.Core.Clients.Browser;
using Xping.Sdk.Core.Clients.Browser.Internals;
using Xping.Sdk.Core.Components;

namespace Xping.Sdk.Core.UnitTests.Clients.Browser.Internals;

public sealed class DefaultBrowserFactoryTests
{
    private Mock<IPlaywright> _mockPlaywright;
    private Mock<IBrowserType> _mockBrowserType;
    private Mock<ISelectors> _mockSelectors;

    class DefaultBrowserFactoryUnderTests(IPlaywright playwright) : DefaultBrowserFactory
    {
        private readonly IPlaywright _playwright = playwright;

        public bool CreatedPlaywright { get; private set; }

        protected override Task<IPlaywright> CreatePlaywrightAsync()
        {
            CreatedPlaywright = true;
            return Task.FromResult(_playwright);
        }
    }

    [SetUp]
    public void SetUp()
    {
        _mockPlaywright = new();
        _mockBrowserType = new();
        _mockSelectors = new();

        _mockPlaywright.SetupGet(m => m.Chromium).Returns(_mockBrowserType.Object);
        _mockPlaywright.SetupGet(m => m.Firefox).Returns(_mockBrowserType.Object);
        _mockPlaywright.SetupGet(m => m.Webkit).Returns(_mockBrowserType.Object);
        _mockPlaywright.SetupGet(m => m.Selectors).Returns(_mockSelectors.Object);

        _mockBrowserType.Setup(m => m.LaunchAsync(It.IsAny<BrowserTypeLaunchOptions?>()))
            .ReturnsAsync(Mock.Of<IBrowser>());
    }

    [Test]
    public async Task DisposeDisposesPlaywright()
    {
        // Arrange
        var factory = new DefaultBrowserFactoryUnderTests(_mockPlaywright.Object);
        await factory.CreateClientAsync(new BrowserConfiguration(), new TestSettings()).ConfigureAwait(false);

        // Act
        factory.Dispose();

        // Assert
        _mockPlaywright.Verify(m => m.Dispose(), Times.Once);
    }

    [Test]
    public async Task DisposeCalledMultipleTimesDisposesPlaywrightOnce()
    {
        // Arrange
        var factory = new DefaultBrowserFactoryUnderTests(_mockPlaywright.Object);
        await factory.CreateClientAsync(new BrowserConfiguration(), new TestSettings()).ConfigureAwait(false);

        // Act
        factory.Dispose();
        factory.Dispose();

        // Assert
        _mockPlaywright.Verify(m => m.Dispose(), Times.Once);
    }

    [Test]
    public void DisposeNotDisposingPlaywrightIfNotCreated()
    {
        // Arrange
        var factory = new DefaultBrowserFactoryUnderTests(_mockPlaywright.Object);

        // Act
        factory.Dispose();

        // Assert
        _mockPlaywright.Verify(m => m.Dispose(), Times.Never);
        Assert.That(factory.CreatedPlaywright, Is.False);
    }

    [Test]
    public void CreateClientAsyncThrowsArgumentNullExceptionWhenConfigurationIsNull()
    {
        // Arrange
        using var factory = new DefaultBrowserFactoryUnderTests(_mockPlaywright.Object);

        // Act
        Assert.ThrowsAsync<ArgumentNullException>(() => factory.CreateClientAsync(
            configuration: null!, new TestSettings()));
    }

    [Test]
    public async Task CreateClientAsyncAssignsHeadlessProperty()
    {
        // Arrange
        using var factory = new DefaultBrowserFactoryUnderTests(_mockPlaywright.Object);
        var configuration = new BrowserConfiguration
        {
            Headless = true
        };

        // Act
        var client = await factory.CreateClientAsync(configuration, new TestSettings()).ConfigureAwait(false);

        // Assert
        _mockBrowserType.Verify(browserType =>
            browserType.LaunchAsync(It.Is<BrowserTypeLaunchOptions?>(_p => _p != null && _p.Headless == configuration.Headless)));
    }

    [Test]
    public async Task CreateClientAsyncCreatesChromiumBrowserByDefault()
    {
        // Arrange
        using var factory = new DefaultBrowserFactoryUnderTests(_mockPlaywright.Object);
        var configuration = new BrowserConfiguration();

        // Act
        var client = await factory.CreateClientAsync(configuration, new TestSettings()).ConfigureAwait(false);

        // Assert
        _mockPlaywright.VerifyGet(m => m.Chromium, Times.Once);
        _mockPlaywright.VerifyGet(m => m.Webkit, Times.Never);
        _mockPlaywright.VerifyGet(m => m.Firefox, Times.Never);
    }

    [TestCase(BrowserType.Firefox)]
    [TestCase(BrowserType.Webkit)]
    [TestCase(BrowserType.Chromium)]
    [Test]
    public async Task CreateClientAsyncCreatesSpecifiedBrowser(string browser)
    {
        // Arrange
        using var factory = new DefaultBrowserFactoryUnderTests(_mockPlaywright.Object);
        var configuration = new BrowserConfiguration
        {
            BrowserType = browser,
        };

        // Act
        var client = await factory.CreateClientAsync(configuration, new TestSettings()).ConfigureAwait(false);

        // Assert
        switch (browser)
        {
            case BrowserType.Firefox:
                _mockPlaywright.VerifyGet(m => m.Firefox, Times.Once);
                break;
            case BrowserType.Webkit:
                _mockPlaywright.VerifyGet(m => m.Webkit, Times.Once);
                break;
            case BrowserType.Chromium:
                _mockPlaywright.VerifyGet(m => m.Chromium, Times.Once);
                break;
            default:
                _mockPlaywright.VerifyGet(m => m.Chromium, Times.Once);
                break;
        };
    }
}
