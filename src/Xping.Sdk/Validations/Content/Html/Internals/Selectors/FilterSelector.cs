/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using HtmlAgilityPack;
using Xping.Sdk.Validations.TextUtils;
using Xping.Sdk.Validations.TextUtils.Internals;

namespace Xping.Sdk.Validations.Content.Html.Internals.Selectors;

internal class FilterSelector(FilterOptions filter, TextOptions? options = null) : ISelector
{
    private readonly TextComparer _textComparer = new(options);

    public HtmlNodeCollection Select(HtmlNode node)
    {
        ArgumentNullException.ThrowIfNull(node, nameof(node));
        HtmlNodeCollection filteredNodes = new(parentnode: node);

        foreach (var n in node.ChildNodes)
        {
            var nodeInnerText = n.InnerText.Trim();

            if (IsMatch(nodeInnerText))
            {
                filteredNodes.Add(n);
            }
        }

        return filteredNodes;
    }

    private bool IsMatch(string? innerText)
    {
        if (innerText == null)
            return false;
        
        if (!string.IsNullOrEmpty(filter.HasText))
            return _textComparer.Compare(innerText, filter.HasText);

        if (!string.IsNullOrEmpty(filter.HasNotText))
            return !_textComparer.Compare(innerText, filter.HasNotText);

        if (filter.HasTextRegex != null)
            return _textComparer.CompareWithRegex(innerText, filter.HasTextRegex.ToString());
        
        if (filter.HasNotTextRegex != null)
            return _textComparer.CompareWithRegex(innerText, filter.HasNotTextRegex.ToString());

        return false;
    }
}
