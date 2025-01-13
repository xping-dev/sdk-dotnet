/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Xml.XPath;
using HtmlAgilityPack;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Validations.Content.Html.Internals.Selectors;

internal class XPathSelector(XPathExpression expression) : ISelector
{
    private readonly XPathExpression _expression = expression.RequireNotNull(nameof(expression));

    public HtmlNodeCollection? Select(HtmlNode node)
    {
        return node.SelectNodes(_expression);
    }
}
