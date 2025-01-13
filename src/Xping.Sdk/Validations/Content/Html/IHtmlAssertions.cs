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
public interface IHtmlAssertions
{
    /// <summary>
    /// Validates that an HTML document or element has the specified title.
    /// The title is typically found within the &lt;title&gt; tag in the &lt;head&gt; section of an HTML document.
    /// </summary>
    /// <param name="title">The expected title to validate against.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="IHtmlAssertions"/> to allow for further assertions.</returns>
    IHtmlAssertions ToHaveTitle(string title, TextOptions? options = null);

    /// <summary>
    /// Validates that an HTML document or element has the specified title.
    /// The title is typically found within the &lt;title&gt; tag in the &lt;head&gt; section of an HTML document.
    /// </summary>
    /// <param name="title">The regex expresion of the expected title to validate against.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="IHtmlAssertions"/> to allow for further assertions.</returns>
    IHtmlAssertions ToHaveTitle(Regex title, TextOptions? options = null);

    /// <summary>
    /// Validates that the size in bytes of an HTML document is equal to or less than the specified maximum size.
    /// </summary>
    /// <param name="maxSizeInBytes">The maximum allowed size of the HTML document in bytes.</param>
    /// <returns>An instance of <see cref="IHtmlAssertions"/> to allow for further assertions.</returns>
    IHtmlAssertions ToHaveMaxDocumentSize(int maxSizeInBytes);

    /// <summary>
    /// Validates if the HTML meta tag with the specified attribute exist.
    /// </summary>
    /// <param name="attribute">The HTML attribute to look for.</param>
    /// <param name="expectedCount">
    /// The expected number of occurrences of the &lt;meta&gt; tag with the specified attribute.
    /// If not specified, the method will validate that at least one occurrence is present.
    /// </param>
    /// <returns>An instance of <see cref="IHtmlAssertions"/> to allow for further assertions.</returns>
    /// <example>
    /// <code>
    /// // For a given HTML 
    /// // &lt;meta property="og:image" content="image.png"&gt;
    /// Expect(html).ToHaveMetaTag(new("property", "og:image"));
    /// </code>
    /// </example>
    IHtmlAssertions ToHaveMetaTag(HtmlAttribute attribute, int? expectedCount = null);

    /// <summary>
    /// Validates if the HTML meta tag with the specified attribute has the expected content value.
    /// </summary>
    /// <param name="attribute">The HTML attribute to look for.</param>
    /// <param name="expectedContent">The expected content of the meta tag.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>A validation result indicating whether the meta tag has the expected content.</returns>
    /// <example>
    /// <code>
    /// // For a given HTML 
    /// // &lt;meta property="og:image" content="image.png"&gt;
    /// Expect(html).ToHaveMetaTag(new HtmlAttribute("property", "og:image"), expectedContent: "image.png");
    /// </code>
    /// </example>
    IHtmlAssertions ToHaveMetaTag(HtmlAttribute attribute, string expectedContent, TextOptions? options = null);

    // /// <summary>
    // /// Validates that all links in an HTML document are valid.
    // /// </summary>
    // /// <returns>An instance of <see cref="IHtmlAssertions"/> to allow for further assertions.</returns>
    // IHtmlAssertions ToHaveValidLinks();

    /// <summary>
    /// Validates that an HTML document contains an image with the specified alt text.
    /// </summary>
    /// <param name="altText">The alt attribute of the image.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="IHtmlAssertions"/> to allow for further assertions.</returns>
    IHtmlAssertions ToHaveImageWithAltText(string altText, TextOptions? options = null);

    /// <summary>
    /// Validates that an HTML document contains an image with the specified alt text.
    /// </summary>
    /// <param name="altText">The regular expression used to match the alt text of the image.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="IHtmlAssertions"/> to allow for further assertions.</returns>
    IHtmlAssertions ToHaveImageWithAltText(Regex altText, TextOptions? options = null);

    /// <summary>
    /// Validates that an HTML document contains an image with the specified source URL.
    /// </summary>
    /// <param name="src">The src attribute of the image.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="IHtmlAssertions"/> to allow for further assertions.</returns>
    IHtmlAssertions ToHaveImageWithSrc(string src, TextOptions? options = null);

    /// <summary>
    /// Validates that an HTML document contains an image with the specified source URL.
    /// </summary>
    /// <param name="src">The regular expression used to match the src attribute of the image.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="IHtmlAssertions"/> to allow for further assertions.</returns>
    IHtmlAssertions ToHaveImageWithSrc(Regex src, TextOptions? options = null);

    /// <summary>
    /// Validates that an HTML document includes the specified external stylesheet.
    /// </summary>
    /// <param name="externalStylesheet">The URL of the external stylesheet.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="IHtmlAssertions"/> to allow for further assertions.</returns>
    IHtmlAssertions ToHaveExternalStylesheet(string externalStylesheet, TextOptions? options = null);

    /// <summary>
    /// Validates that an HTML document includes the specified external script.
    /// </summary>
    /// <param name="externalScript">The URL of the external script.</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    /// <returns>An instance of <see cref="IHtmlAssertions"/> to allow for further assertions.</returns>
    IHtmlAssertions ToHaveExternalScript(string externalScript, TextOptions? options = null);
}
