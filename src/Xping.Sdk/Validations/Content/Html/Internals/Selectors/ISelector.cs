using HtmlAgilityPack;

namespace Xping.Sdk.Validations.Content.Html.Internals.Selectors;

internal interface ISelector
{
    public HtmlNodeCollection? Select(HtmlNode node);
}
