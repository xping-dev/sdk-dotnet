/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Microsoft.Playwright;
using Xping.Sdk.Core.Clients.Browser;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Actions.Internals;

internal class BrowserHttpRequestInterceptor(BrowserConfiguration configuration) : IHttpRequestInterceptor
{
    private readonly BrowserConfiguration _configuration = configuration.RequireNotNull(nameof(configuration));

    public async Task HandleAsync(IRoute route)
    {
        var httpContent = _configuration.GetHttpContent();
        var byteArray = httpContent != null ? await httpContent.ReadAsByteArrayAsync().ConfigureAwait(false) : null;

        // Modify the HTTP method here
        var routeOptions = new RouteContinueOptions
        {
            Method = _configuration.GetHttpMethod().Method,
            PostData = byteArray
        };

        await route.ContinueAsync(routeOptions).ConfigureAwait(false);
    }
}
