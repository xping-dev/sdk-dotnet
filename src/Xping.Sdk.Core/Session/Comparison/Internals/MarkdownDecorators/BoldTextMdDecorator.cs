namespace Xping.Sdk.Core.Session.Comparison.Internals.MarkdownDecorators;

internal class BoldTextMdDecorator(ITextReport textReport) : BaseMdDecorator(textReport)
{
    private const string BoldMarker = "**";

    public override string Generate()
    {
        var text = base.Generate();

        if (!string.IsNullOrEmpty(text))
        {
            return BoldMarker + base.Generate() + BoldMarker;
        }

        return text;        
    }
}
