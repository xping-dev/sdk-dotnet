/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Microsoft.Playwright;
using Xping.Sdk.Shared;
using Xping.Sdk.Validations.Content.Html;
using Xping.Sdk.Validations.Content.Html.Internals;
using Xping.Sdk.Validations.Content.Page.Internals;
using Xping.Sdk.Validations.HttpResponse;
using Xping.Sdk.Validations.HttpResponse.Internals;

namespace Xping.Sdk;

/// <summary>
/// The <c>XpingAssertions</c> class provides static methods for assertion validation that can be used to make 
/// assertions state in the tests.
/// </summary>
public abstract class XpingAssertions
{
    /// <summary>
    /// Creates an instance of <see cref="IPageAssertions"/> for the specified browser Page.
    /// </summary>
    /// <param name="page">The <see cref="IPage"/> instance to be validated. Must not be null.</param>
    /// <returns>An instance of <see cref="IPageAssertions"/> that provides assertions for the specified page.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="page"/> is null.</exception>
    protected static IPageAssertions Expect(IPage page) =>
        new InstrumentedPageAssertions(
            context: ((InstrumentedPage)page.RequireNotNull(nameof(page))).GetTestContext(),
            pageAssertions: Assertions.Expect(((InstrumentedPage)page).GetPage()));

    /// <summary>
    /// Creates an instance of <see cref="ILocatorAssertions"/> for the specified locator.
    /// </summary>
    /// <param name="locator">The <see cref="ILocator"/> instance to be validated. Must not be null.</param>
    /// <returns>
    /// An instance of <see cref="ILocatorAssertions"/> that provides assertions for the specified locator.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="locator"/> is null.</exception>
    protected static ILocatorAssertions Expect(ILocator locator) =>
        new InstrumentedLocatorAssertions(
            context: ((InstrumentedLocator)locator.RequireNotNull(nameof(locator))).GetTestContext(),
            locatorAssertions: Assertions.Expect(((InstrumentedLocator)locator).ActiveLocator));
    
    /// <summary>
    /// Creates an instance of <see cref="IHttpAssertions"/> for the specified HTTP response.
    /// </summary>
    /// <param name="httpResponse">The HTTP response to be validated. Must not be null.</param>
    /// <returns>An <see cref="IHttpAssertions"/> instance for performing assertions on the HTTP response.</returns>
    protected static IHttpAssertions Expect(IHttpResponse httpResponse) => 
        new HttpAssertions(httpResponse);

    /// <summary>
    /// Creates an instance of <see cref="IHtmlAssertions"/> for the specified HTML response.
    /// </summary>
    /// <param name="htmlContent">The HTML response to be validated. Must not be null.</param>
    /// <returns>An instance of <see cref="IHtmlAssertions"/> for performing assertions on the HTML response.</returns>
    protected static IHtmlAssertions Expect(IHtmlContent htmlContent) => 
        new HtmlAssertions(htmlContent);

    /// <summary>
    /// Creates an instance of <see cref="IHtmlLocatorAssertions"/> for the specified HTML response.
    /// </summary>
    /// <param name="htmlLocator">The HTML locator response to be validated. Must not be null.</param>
    /// <returns>
    /// An instance of <see cref="IHtmlLocatorAssertions"/> for performing assertions on the HTML locator response.
    /// </returns>
    protected static IHtmlLocatorAssertions Expect(IHtmlLocator htmlLocator) => 
        new HtmlLocatorAssertions(htmlLocator);
}
