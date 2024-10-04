namespace Xping.Sdk.Core.Session.Comparison.Internals.MarkdownDecorators;

internal class TableDataMdDecorator(ITextReport textReport) : BaseMdDecorator(textReport)
{
    private const string TableMarker = "|";

    public override string Generate()
    {
        return TableMarker + base.Generate();
    }
}
