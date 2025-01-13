/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Microsoft.Playwright;

namespace Xping.Sdk.Core.Clients.Browser;

/// <summary>
/// Defines the contract for intercepting and handling HTTP requests within a browser client.
/// </summary>
public interface IHttpRequestInterceptor
{
    /// <summary>
    /// Asynchronously handles an intercepted HTTP request.
    /// </summary>
    /// <param name="route">The route information associated with the intercepted request.</param>
    /// <returns>A task representing the asynchronous operation of request handling.</returns>
    Task HandleAsync(IRoute route);
}
