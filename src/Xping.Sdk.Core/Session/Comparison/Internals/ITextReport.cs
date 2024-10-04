namespace Xping.Sdk.Core.Session.Comparison.Internals;

internal interface ITextReport
{
    string Generate();
}

internal class TextReport(string text) : ITextReport
{
    private readonly string _text = text;

    public string Generate()
    {
        return _text;
    }
}
