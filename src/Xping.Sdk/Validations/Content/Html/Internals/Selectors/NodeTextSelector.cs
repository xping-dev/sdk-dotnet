/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using Xping.Sdk.Shared;
using Xping.Sdk.Validations.TextUtils;
using Xping.Sdk.Validations.TextUtils.Internals;

namespace Xping.Sdk.Validations.Content.Html.Internals.Selectors;

internal class NodeTextSelector(XPath xpath, string text, TextOptions? options = null) : NodeSelector(xpath)
{
    private readonly string _text = text.RequireNotNullOrEmpty(nameof(text));
    private readonly TextComparer _textComparer = new(options);

    protected override bool IsMatch(string nodeInnerText)
    {
        return _textComparer.Compare(nodeInnerText, _text);
    }
}
