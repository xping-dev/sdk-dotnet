using System.Xml.XPath;
using HtmlAgilityPack;
using Xping.Sdk.Core.Components;
using Xping.Sdk.Core.Session;
using Xping.Sdk.Validations.Content.Html.Internals;
using Xping.Sdk.Validations.TextUtils;

namespace Xping.Sdk.Validations.Content.Html;

/// <summary>
/// Html locators represent a way to find element(s) on the html content.
/// </summary>
public interface IHtmlLocator
{
    internal HtmlNodeCollection Nodes { get; }
    internal IIterator<HtmlNode> Iterator { get; }
    internal TestContext Context { get; }

    /// <summary>
    /// This method narrows existing locator according to the filter, for example filters by text.
    /// <code>
    /// html.GetByRole(AriaRole.Listitem)<br/>
    ///     .Filter(new { HasText = "text in column 1" })<br/>
    /// </code>
    /// </summary>
    /// <param name="filter">Filter filter</param>
    /// <param name="options">Optional parameter for customizing the search text behavior.</param>
    IHtmlLocator Filter(FilterOptions filter, TextOptions? options = null);

    /// <summary>
    /// Returns locator to the first matching element.
    /// Example: <code>html.GetByRole(AriaRole.Listitem).First().ToHaveInnerText("Some text");</code>
    /// </summary>
    IHtmlLocator First();

    /// <summary>
    /// Returns locator to the last matching element.
    /// Example: <code>html.GetByRole(AriaRole.Listitem).Last().ToHaveInnerText("Some text");</code>
    /// </summary>
    IHtmlLocator Last();

    /// <summary>
    /// Returns locator to the n-th matching element. It's zero based, <c>nth(0)</c> selects the first element.
    /// Example: <code>html.GetByRole(AriaRole.Listitem).Nth(2).ToHaveInnerText("Some text");</code>
    /// </summary>
    /// <param name="index">
    /// Zero based index of the matching element. 
    /// </param>
    /// <exception cref="ValidationException">
    /// When the index is outside the allowable range of matching elements. The exception is reported as failure in
    /// the <see cref="TestSession"/>.
    /// </exception>
    IHtmlLocator Nth(int index);

    /// <summary>
    /// The method finds an element matching the specified selector in the locator's subtree.
    /// </summary>
    /// <param name="selector">A xpath selector to use when resolving DOM element.</param>
    IHtmlLocator Locate(XPathExpression selector);
}
