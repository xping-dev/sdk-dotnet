namespace Xping.Sdk.Core.Session.Comparison.Internals.MarkdownDecorators;

internal class TableRowEndMdDecorator(ITextReport textReport) : BaseMdDecorator(textReport)
{
    private const string TableRowEndMark = "|\r\n";

    public override string Generate()
    {
        return base.Generate() + TableRowEndMark;
    }
}
