namespace Xping.Sdk.Core.Session.Comparison.Internals.MarkdownDecorators;

internal class CodeTextMdDecorator(ITextReport textReport) : BaseMdDecorator(textReport)
{
    private const char CodeMarker = '`';

    public override string Generate()
    {
        var text = base.Generate();

        if (!string.IsNullOrEmpty(text))
        {
            return CodeMarker + base.Generate() + CodeMarker;
        }

        return text;
    }
}
