/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Text.RegularExpressions;
using Xping.Sdk.Validations.TextUtils;

namespace Xping.Sdk.Validations.Content.Html;

/// <summary>
/// Represents an object for validating an HTML response.
/// </summary>
public interface IHtmlLocatorAssertions
{
    /// <summary>
    /// Validates that the specified number matches the number of elements corresponding to the provided locator.
    /// Usage: <code>html.GetByRole(AriaRole.Listitem).ToHaveCount(expectedCount);</code>
    /// where 'expectedCount' is the number of elements expected to be found.
    /// </summary>
    /// <param name="expectedCount">The expected number of elements to match the locator.</param>
    /// <returns>The current instance of <see cref="IHtmlLocatorAssertions"/> for method chaining.</returns>
    IHtmlLocatorAssertions ToHaveCount(int expectedCount);

    /// <summary>
    /// Confirms that the innerText of the element identified by the locator matches the specified string.
    /// Example: <code>html.GetByRole(AriaRole.Listitem).ToHaveInnerText("Sample Text");</code>
    /// In this example, "Sample Text" is the string anticipated to match the innerText of the located element.
    /// </summary>
    /// <param name="innerText">The string to verify against the innerText of the located element.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>The current instance of <see cref="IHtmlLocatorAssertions"/> for method chaining.</returns>
    IHtmlLocatorAssertions ToHaveInnerText(string innerText, TextOptions? options = null);

    /// <summary>
    /// Confirms that the innerText of the element identified by the locator matches the specified regex.
    /// Example: <code>html.GetByRole(AriaRole.Listitem).ToHaveInnerText("Sample Text");</code>
    /// In this example, "Sample Text" is the string anticipated to match the innerText of the located element.
    /// </summary>
    /// <param name="innerText">The string to verify against the innerText of the located element.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>The current instance of <see cref="IHtmlLocatorAssertions"/> for method chaining.</returns>
    IHtmlLocatorAssertions ToHaveInnerText(Regex innerText, TextOptions? options = null);
    
    /// <summary>
    /// Confirms that the inner text of each element identified by the locator matches the corresponding
    /// string in the specified enumerable. Example:
    /// <code>
    /// html.GetByRole(AriaRole.Listitem).ToHaveInnerText(["Item 1", "Item 2", "Item 3"]);
    /// </code>
    /// In this example, "Item 1", "Item 2", and "Item 3" are the strings anticipated to match the inner text of
    /// the located elements. The number of items in <paramref name="nodeInnerTexts"/> should match the number of
    /// nodes the locator points to.
    /// </summary>
    /// <param name="nodeInnerTexts">Strings to verify against the nodeInnerTexts of the located elements.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>The current instance of <see cref="IHtmlLocatorAssertions"/> for method chaining.</returns>
    IHtmlLocatorAssertions ToHaveInnerText(IEnumerable<string> nodeInnerTexts, TextOptions? options = null);

    /// <summary>
    /// Confirms that the innerHtml of the element identified by the locator matches the specified string.
    /// Example: <code>html.GetByRole(AriaRole.Listitem).ToHaveInnerHtml("&lt;div&gt;");</code>
    /// In this example, "&lt;div&gt;" is the string anticipated to match the innerHtml of the located element.
    /// </summary>
    /// <param name="innerHtml">The string to verify against the innerHtml of the located element.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>The current instance of <see cref="IHtmlLocatorAssertions"/> for method chaining.</returns>
    IHtmlLocatorAssertions ToHaveInnerHtml(string innerHtml, TextOptions? options = null);

    /// <summary>
    /// Confirms that the innerHtml of the element identified by the locator matches the specified regex.
    /// Example: <code>html.GetByRole(AriaRole.Listitem).ToHaveInnerText("&lt;div&gt;");</code>
    /// In this example, "&lt;div&gt;" is the string anticipated to match the innerHtml of the located element.
    /// </summary>
    /// <param name="innerHtml">The string to verify against the innerHtml of the located element.</param>
    /// <param name="options">Optional parameters for customizing the locator behavior.</param>
    /// <returns>The current instance of <see cref="IHtmlLocatorAssertions"/> for method chaining.</returns>
    IHtmlLocatorAssertions ToHaveInnerHtml(Regex innerHtml, TextOptions? options = null);
}
