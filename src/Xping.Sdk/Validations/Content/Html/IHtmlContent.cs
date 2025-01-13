/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Text.RegularExpressions;
using System.Xml.XPath;
using HtmlAgilityPack;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Validations.TextUtils;

namespace Xping.Sdk.Validations.Content.Html;

/// <summary>
/// Defines a contract for HTML content manipulation and element location within a web page.
/// </summary>
public interface IHtmlContent
{
    /// <summary>
    /// Gets the test context associated with the response.
    /// </summary>
    internal TestContext Context { get; }

    /// <summary>
    /// Gets the html document associated with the response.
    /// </summary>
    internal HtmlDocument Document { get; }

    /// <summary>
    /// Locates an HTML element using an XPath selector and returns a locator for further actions.
    /// </summary>
    /// <param name="selector">The XPath expression used to resolve the DOM element.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>An IHtmlLocator instance representing the located element.</returns>
    IHtmlLocator Locator(XPathExpression selector, FilterOptions? options = default);
    
    /// <summary>
    /// Locates an HTML element using an XPath selector and returns a locator for further actions.
    /// </summary>
    /// <param name="selector">The string representing XPath expression used to resolve the DOM element.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>An IHtmlLocator instance representing the located element.</returns>
    IHtmlLocator Locator(string selector, FilterOptions? options = default);

    /// <summary>
    /// Locates elements with an 'alt' attribute text matching the specified string.
    /// </summary>
    /// <param name="text">The text to match against the 'alt' attribute.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>An IHtmlLocator instance representing the located elements.</returns>
    IHtmlLocator GetByAltText(string text, TextOptions? options = null);

    /// <summary>
    /// Locates elements with an 'alt' attribute text matching the specified regular expression.
    /// </summary>
    /// <param name="text">The regular expression to match against the 'alt' attribute.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>An IHtmlLocator instance representing the located elements.</returns>
    IHtmlLocator GetByAltText(Regex text, TextOptions? options = null);

    /// <summary>
    /// Locates elements with a 'label' element text matching the specified string.
    /// </summary>
    /// <param name="text">The text to match against the 'label' element text.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>An IHtmlLocator instance representing the located elements.</returns>
    IHtmlLocator GetByLabel(string text, TextOptions? options = null);

    /// <summary>
    /// Locates elements with a 'label' element text matching the specified regular expression.
    /// </summary>
    /// <param name="text">The regular expression to match against the 'label' element text.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>An IHtmlLocator instance representing the located elements.</returns>
    IHtmlLocator GetByLabel(Regex text, TextOptions? options = null);

    /// <summary>
    /// Locates elements with a 'placeholder' attribute text matching the specified string.
    /// </summary>
    /// <param name="text">The text to match against the 'placeholder' attribute.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>An IHtmlLocator instance representing the located elements.</returns>
    IHtmlLocator GetByPlaceholder(string text, TextOptions? options = null);

    /// <summary>
    /// Locates elements with a 'placeholder' attribute text matching the specified regular expression.
    /// </summary>
    /// <param name="text">The regular expression to match against the 'placeholder' attribute.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>An IHtmlLocator instance representing the located elements.</returns>
    IHtmlLocator GetByPlaceholder(Regex text, TextOptions? options = null);

    /// <summary>
    /// Locates elements with a test id attribute matching the specified string. By default, the `data-testid` attribute 
    /// is used as a test id. Use <see cref="TestSettings.TestIdAttribute"/> to configure a different test id attribute 
    /// if necessary.
    /// </summary>
    /// <param name="testId">The text to match against the test id attribute.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>An IHtmlLocator instance representing the located elements.</returns>
    IHtmlLocator GetByTestId(string testId, TextOptions? options = null);

    /// <summary>
    /// Locates elements with a test id attribute matching the specified regular expression. By default, the 
    /// `data-testid` attribute is used as a test id. Use <see cref="TestSettings.TestIdAttribute"/> to configure a 
    /// different test id attribute if necessary.
    /// </summary>
    /// <param name="testId">The regular expression to match against the test id attribute.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>An IHtmlLocator instance representing the located elements.</returns>
    IHtmlLocator GetByTestId(Regex testId, TextOptions? options = null);

    /// <summary>
    /// Locates elements with a 'title' attribute text matching the specified string.
    /// </summary>
    /// <param name="text">The text to match against the 'title' attribute.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>An IHtmlLocator instance representing the located elements.</returns>
    IHtmlLocator GetByTitle(string text, TextOptions? options = null);

    /// <summary>
    /// Locates elements with a 'title' attribute text matching the specified regular expression.
    /// </summary>
    /// <param name="text">The regular expression to match against the 'title' attribute.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>An IHtmlLocator instance representing the located elements.</returns>
    IHtmlLocator GetByTitle(Regex text, TextOptions? options = null);
}
