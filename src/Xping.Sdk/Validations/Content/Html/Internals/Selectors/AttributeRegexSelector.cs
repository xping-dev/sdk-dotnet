/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Text.RegularExpressions;
using Xping.Sdk.Shared;
using Xping.Sdk.Validations.TextUtils;
using Xping.Sdk.Validations.TextUtils.Internals;

namespace Xping.Sdk.Validations.Content.Html.Internals.Selectors;

internal class AttributeRegexSelector(XPath xpath, Regex textRegex, TextOptions? options = null) :
    AttributeSelector(xpath)
{
    private readonly TextComparer _textComparer = new(options);

    protected override bool IsMatch(string attributeValue)
    {
        return _textComparer.CompareWithRegex(attributeValue, textRegex.ToString());
    }
}
