/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Moq.Protected;
using Moq;
using Xping.Sdk.Core.Clients.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Xping.Sdk.Core.UnitTests.HttpClients.Internals;

public sealed class WebApplicationHttpClientFactoryTests
{
    private Mock<HttpMessageHandler> _mockHandler;
    private HttpClient _client;
    private Func<WebApplicationFactoryClientOptions, HttpClient> _clientFunction;
    private WebApplicationHttpClientFactory _factory;

    [SetUp]
    public void SetUp()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        _client = new HttpClient(_mockHandler.Object);
        _clientFunction = (WebApplicationFactoryClientOptions options) => _client;
        _factory = new WebApplicationHttpClientFactory(_clientFunction, new HttpClientFactoryConfiguration());
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
    }

    [Test]
    public void CreateClientReturnsSameHttpClientInstance()
    {
        // Arrange
        var returnedClient = _factory.CreateClient("anyName");

        // Assert
        Assert.That(
            _clientFunction(new WebApplicationFactoryClientOptions()), 
            Is.EqualTo(returnedClient),
            "CreateClient should return the same HttpClient instance that was provided.");
    }
}
