namespace Xping.Sdk.Core.Session.Comparison.Internals.MarkdownDecorators;

internal class ParagraphMdDecorator(ITextReport textReport) : BaseMdDecorator(textReport)
{
    public override string Generate()
    {
        return base.Generate() + Environment.NewLine;
    }
}
