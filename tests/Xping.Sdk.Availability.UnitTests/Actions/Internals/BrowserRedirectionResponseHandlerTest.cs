/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net;
using Microsoft.Net.Http.Headers;
using Microsoft.Playwright;
using Moq;
using Xping.Sdk.Actions.Internals;
using Xping.Sdk.UnitTests.Helpers;
using Xping.Sdk.Core.Clients.Browser;
using Xping.Sdk.Core.Session;

namespace Xping.Sdk.Availability.UnitTests.Actions.Internals;

public sealed class BrowserRedirectionResponseHandlerTest
{
    [Test]
    public void HandleResponseDoesNotHandleWhenResponseIsNull()
    {
        // Arrange
        var context = HtmlContentTestsHelpers.CreateTestContext();
        var handler = new BrowserRedirectionResponseHandler(
            url: new Uri("http://localhost"),
            context,
            urlRedirections: [],
            maxRedirections: 5);

        // Act
        handler.HandleResponse(sender: this, response: null!);

        // Assert
        Mock.Get(context.Progress!).Verify(_m => _m.Report(It.IsAny<TestStep>()), Times.Never);
    }

    [Test]
    public void HandleResponseDoesNotHandleWhenResponseIsNotHttpRedirect()
    {
        // Arrange
        var context = HtmlContentTestsHelpers.CreateTestContext();
        var handler = new BrowserRedirectionResponseHandler(
            url: new Uri("http://localhost"),
            context,
            urlRedirections: [],
            maxRedirections: 5);

        var mockResponse = new Mock<IResponse>();
        mockResponse.Setup(m => m.Status).Returns((int)HttpStatusCode.OK);

        // Act
        handler.HandleResponse(sender: this, response: mockResponse.Object);

        // Assert
        Mock.Get(context.Progress!).Verify(_m => _m.Report(It.IsAny<TestStep>()), Times.Never);
    }

    [Test]
    public void HandleResponseThrowsWhenCircularDependencyIsDetected()
    {
        // Arrange
        var context = HtmlContentTestsHelpers.CreateTestContext();
        var handler = new BrowserRedirectionResponseHandler(
            url: new Uri("http://localhost"),
            context,
            urlRedirections: ["http://localhost"], // Initialize URL redirections so the circular dependency can happen
            maxRedirections: 5);

        var mockResponse = new Mock<IResponse>();
        mockResponse.SetupGet(m => m.Status).Returns((int)HttpStatusCode.Redirect);
        mockResponse.SetupGet(m => m.Headers)
            .Returns(new Dictionary<string, string> { { HeaderNames.Location, "http://localhost" } });

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => 
            handler.HandleResponse(sender: this, response: mockResponse.Object));

        // Assert
        Assert.That(exception.Message, Is.EqualTo(
            $"A circular dependency was detected for the URL http://localhost. " +
            $"The redirection chain is: http://localhost/ -> http://localhost."));
    }

    [Test]
    public void HandleResponseTracksRedirectionWhenNoCircularDependencyIsDetected()
    {
        // Arrange
        OrderedUrlRedirections urlRedirections = ["http://localhost"];
        var context = HtmlContentTestsHelpers.CreateTestContext();
        var handler = new BrowserRedirectionResponseHandler(
            url: new Uri("http://localhost"),
            context,
            urlRedirections, 
            maxRedirections: 5);

        var mockResponse = new Mock<IResponse>();
        mockResponse.SetupGet(m => m.Status).Returns((int)HttpStatusCode.Redirect);
        mockResponse.SetupGet(m => m.Headers)
            .Returns(new Dictionary<string, string> { { HeaderNames.Location, "http://localhost/A" } });

        // Act
        handler.HandleResponse(sender: this, response: mockResponse.Object);

        // Assert
        Assert.That(urlRedirections.Last(), Is.EqualTo("http://localhost/A"));
    }

    [Test]
    public void HandleResponseThrowsWhenMaxRedirectionsReached()
    {
        // Arrange
        OrderedUrlRedirections urlRedirections = ["http://localhost"];
        var maxRedirections = 1;
        var context = HtmlContentTestsHelpers.CreateTestContext();
        var handler = new BrowserRedirectionResponseHandler(
            url: new Uri("http://localhost"),
            context,
            urlRedirections, 
            maxRedirections);

        var mockResponse = new Mock<IResponse>();
        mockResponse.SetupGet(m => m.Status).Returns((int)HttpStatusCode.Redirect);
        mockResponse.SetupGet(m => m.Headers)
            .Returns(new Dictionary<string, string> { { HeaderNames.Location, "http://localhost/A" } });

        // Act
        var exception = Assert.Throws<TooManyRedirectsException>(() =>
            handler.HandleResponse(sender: this, response: mockResponse.Object));

        // Assert
        Assert.That(exception.Message, Is.EqualTo(
            $"The maximum number of redirects ({maxRedirections}) has been exceeded for the URL " +
            $"http://localhost/. The last redirect URL was http://localhost/A."));
    }

    [Test]
    public void HandleResponseRestartsInstrumentation()
    {
        // Arrange
        var context = HtmlContentTestsHelpers.CreateTestContext();
        var handler = new BrowserRedirectionResponseHandler(
            url: new Uri("http://localhost"),
            context,
            urlRedirections: [],
            maxRedirections: 5);
        var mockResponse = new Mock<IResponse>();
        mockResponse.SetupGet(m => m.Status).Returns((int)HttpStatusCode.Redirect);
        mockResponse.SetupGet(m => m.Headers)
            .Returns(new Dictionary<string, string> { { HeaderNames.Location, "http://localhost/A" } });

        // Act
        handler.HandleResponse(sender: this, response: mockResponse.Object);

        // Assert
        Mock.Get(context.Instrumentation).Verify(_m => _m.Restart(), Times.Once);
    }
}
