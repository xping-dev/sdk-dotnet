/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Microsoft.Playwright;

namespace Xping.Sdk.Core.Clients.Browser;

/// <summary>
/// Defines a contract for handling HTTP responses received for a request by the browser.
/// </summary>
public interface IHttpResponseHandler
{
    /// <summary>
    /// Handles the action to be taken when an HTTP response is received.
    /// </summary>
    /// <param name="sender">The source of the HTTP response, which can be null.</param>
    /// <param name="response">The HTTP response information received by the browser.</param>
    void HandleResponse(object? sender, IResponse response);
}
