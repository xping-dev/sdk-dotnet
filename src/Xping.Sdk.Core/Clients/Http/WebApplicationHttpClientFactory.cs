/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Microsoft.AspNetCore.Mvc.Testing;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Clients.Http;

/// <summary>
/// Custom implementation of <see cref="IHttpClientFactory"/> specifically designed for use with WebApplicationFactory.
/// The purpose of this class is to ensure that the pre-configured <see cref="HttpClient"/> instance created by the
/// WebApplicationFactory.CreateClient method is returned, which aligns with additional system requirements where 
/// <see cref="HttpClient"/> objects are constructed via <see cref="IHttpClientFactory"/>.
/// </summary>
public class WebApplicationHttpClientFactory(
    Func<WebApplicationFactoryClientOptions, HttpClient> createClient,
    HttpClientFactoryConfiguration factoryConfiguration) : IHttpClientFactory
{
    private readonly Func<WebApplicationFactoryClientOptions, HttpClient> _createClient = 
        createClient.RequireNotNull(nameof(createClient));
    private readonly HttpClientFactoryConfiguration _factoryConfiguration = 
        factoryConfiguration.RequireNotNull(nameof(factoryConfiguration));
    
    /// <summary>
    /// Returns the <see cref="HttpClient"/> instance that was provided during the factory's instantiation.
    /// </summary>
    /// <param name="name">
    /// The logical name of the client to create. This parameter is not used in this implementation but is required by 
    /// the interface.
    /// </param>
    /// <returns>The pre-configured <see cref="HttpClient"/> instance.</returns>
    public HttpClient CreateClient(string name)
    {
        return _createClient(CreateWebApplicationFactoryClientOptions());
    }

    /// <summary>
    /// Creates and configures an instance of WebApplicationFactoryClientOptions.
    /// </summary>
    /// <remarks>
    /// This method provides a WebApplicationFactoryClientOptions object with predefined settings.
    /// The settings disable automatic redirections and cookie handling for the HTTP client used by the 
    /// WebApplicationFactory.
    /// This is useful when you need to test the behavior of endpoints that perform redirections or set cookies without 
    /// following them automatically.
    /// </remarks>
    /// <returns>
    /// A WebApplicationFactoryClientOptions object configured with:
    /// AllowAutoRedirect set to false, HandleCookies set to false, and MaxAutomaticRedirections set to 0.
    /// </returns>
    protected virtual WebApplicationFactoryClientOptions CreateWebApplicationFactoryClientOptions()
    {
        return new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = HttpClientFactoryConfiguration.HttpClientAutoRedirects,
            HandleCookies = HttpClientFactoryConfiguration.HttpClientHandleCookies,
            MaxAutomaticRedirections = _factoryConfiguration.MaxRedirections,
        };
    }
}
