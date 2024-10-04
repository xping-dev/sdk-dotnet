using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Session.Comparison.Internals.MarkdownDecorators;

internal abstract class BaseMdDecorator(ITextReport textReport) : ITextReport
{
    private readonly ITextReport _textReport = textReport.RequireNotNull(nameof(textReport));

    public virtual string Generate()
    {
        return _textReport.Generate();
    }
}
