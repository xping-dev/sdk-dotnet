/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using HtmlAgilityPack;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Validations.Content.Html.Internals.Selectors;

internal abstract class NodeSelector(XPath xpath) : ISelector
{
    private readonly XPath _xpath = xpath.RequireNotNull(nameof(xpath));

    public HtmlNodeCollection Select(HtmlNode node)
    {
        ArgumentNullException.ThrowIfNull(node, nameof(node));
        HtmlNodeCollection nodes = new(parentnode: node);

        var selectedNodes = node.SelectNodes(_xpath.Expression);

        if (selectedNodes == null)
            return nodes;
        
        foreach (HtmlNode n in selectedNodes)
        {
            var nodeInnerText = n.InnerText.Trim();

            if (IsMatch(nodeInnerText))
            {
                nodes.Add(n);
            }
        }

        return nodes;
    }

    protected abstract bool IsMatch(string nodeInnerText);
}
