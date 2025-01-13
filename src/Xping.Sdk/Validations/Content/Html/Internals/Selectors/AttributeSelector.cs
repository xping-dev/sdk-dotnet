/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using HtmlAgilityPack;
using Xping.Sdk.Shared;
using Exception = System.Exception;

namespace Xping.Sdk.Validations.Content.Html.Internals.Selectors;

internal abstract class AttributeSelector(XPath expression) : ISelector
{
    private readonly XPath _xpath = expression.RequireNotNull(nameof(expression));

    public HtmlNodeCollection Select(HtmlNode node)
    {
        ArgumentNullException.ThrowIfNull(node, nameof(node));
        HtmlNodeCollection nodes = new(parentnode: node);

        var selectedNodes = node.SelectNodes(_xpath.Expression);

        if (selectedNodes == null) 
            return nodes;
        
        foreach (var n in selectedNodes)
        {
            var attrValue = n.Attributes[_xpath.MetaData].Value.Trim();

            if (IsMatch(attrValue))
            {
                nodes.Add(n);
            }
        }

        return nodes;
    }

    protected abstract bool IsMatch(string attributeValue);
}