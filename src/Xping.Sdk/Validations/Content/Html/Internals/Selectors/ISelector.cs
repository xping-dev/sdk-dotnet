/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using HtmlAgilityPack;

namespace Xping.Sdk.Validations.Content.Html.Internals.Selectors;

internal interface ISelector
{
    public HtmlNodeCollection? Select(HtmlNode node);
}
