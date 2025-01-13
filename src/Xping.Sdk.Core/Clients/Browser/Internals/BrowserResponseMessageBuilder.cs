/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Net;
using Microsoft.Playwright;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Clients.Browser.Internals;

internal sealed class BrowserResponseMessageBuilder
{
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private IResponse _response = null!;

    public BrowserResponseMessageBuilder Build(IBrowser browser)
    {
        _browser = browser.RequireNotNull(nameof(browser));

        return this;
    }

    public BrowserResponseMessageBuilder Build(IBrowserContext? context)
    {
        if (context != null)
        {
            _context = context;
        }

        return this;
    }

    public BrowserResponseMessageBuilder Build(IPage? page)
    {
        if (page != null)
        {
            _page = page;
        }

        return this;
    }

    public BrowserResponseMessageBuilder Build(IResponse? response)
    {
        if (response != null)
        {
            _response = response;
        }

        return this;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "CA2000:Dispose objects before losing scope",
        Justification = "Ownership of browser and its context is transferred to the BrowserResponseMessage.")]
    public async Task<BrowserResponseMessage> Build()
    {
        if (!CanConstructPage())
        {
            // Create an error message for the user
            var errorMessage =
                "An error occurred while fetching data from the server. " +
                "The HTTP response content could not be obtained. " +
                "Please check the following:\n" +
                " - The URL of the request is valid and reachable\n" +
                " - The HTTP server is up and running\n" +
                " - The format and content of the data response are correct and expected\n" +
                "If the error persists, please contact the support team for assistance.";

            throw new InvalidOperationException(errorMessage);
        }

        HttpResponseMessage responseMessage = null!;

        try
        {
            responseMessage = await BuildHttpResponseMessageAsync().ConfigureAwait(false);

            // The BrowserResponseMessage class assumes ownership of the browser, context, and page, and is solely
            // responsible for the proper disposal of these resources.
            var browserResponseMessage = new BrowserResponseMessage(_browser, _context, _page, responseMessage);

            return browserResponseMessage;
        }
        catch (Exception)
        {
            responseMessage?.Dispose();
            throw;
        }
    }

    private bool CanConstructPage()
    {
        bool result = _browser != null && _context != null && _page != null && _response != null;

        return result;
    }

    private async Task<HttpResponseMessage> BuildHttpResponseMessageAsync()
    {
        var responseMessage = new HttpResponseMessage
        {
            StatusCode = (HttpStatusCode)_response.Status,
            Content = new StringContent(await _page.ContentAsync().ConfigureAwait(false)),
            RequestMessage = new HttpRequestMessage(
                    HttpMethod.Parse(_response.Request.Method),
                    new Uri(_page.Url))
        };

        // Remove any existing HTTP headers from the HttpResponseMessage instance. This ensures that only the actual 
        // headers from the browser's response are present.
        responseMessage.Headers.Clear();
        responseMessage.Content.Headers.Clear();
        responseMessage.TrailingHeaders.Clear();

        foreach (var httpHeader in _response.Headers)
        {
            if (httpHeader.Key
                .ToUpperInvariant()
                .StartsWith("CONTENT", StringComparison.InvariantCulture))
            {
                if (responseMessage.Content.Headers.Contains(httpHeader.Key))
                {
                    responseMessage.Content.Headers.Remove(httpHeader.Key);
                }

                responseMessage.Content.Headers.TryAddWithoutValidation(httpHeader.Key, httpHeader.Value);
            }
            else
            {
                responseMessage.Headers.TryAddWithoutValidation(httpHeader.Key, httpHeader.Value);
            }
        }

        return responseMessage;
    }
}
