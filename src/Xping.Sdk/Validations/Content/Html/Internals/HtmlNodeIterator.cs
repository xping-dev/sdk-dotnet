using HtmlAgilityPack;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Validations.Content.Html.Internals;

internal class HtmlNodeIterator(HtmlNodeCollection nodes) : IIterator<HtmlNode>
{
    private const int Uninitialized = -1;
    
    private readonly HtmlNodeCollection _nodes = nodes.RequireNotNull(nameof(nodes));

    public int Cursor { get; private set; } = Uninitialized;
    public bool IsAdvanced => Cursor >= 0;
    public int Count => _nodes.Count;

    public HtmlNode? Current()
    {
        if (_nodes.Count == 0)
        {
            // The collection of HTML nodes is empty. There are no elements to iterate over.
            return null;
        }

        if (Cursor == Uninitialized)
        {
            throw new InvalidOperationException(
                "The iterator has not been advanced. Call the 'First', 'Last' or 'Nth' methods to advance the " +
                "iterator before calling 'Current'.");
        }

        return _nodes[Cursor];
    }

    public void First()
    {
        if (_nodes.Count > 0)
        {
            Cursor = 0;
        }
    }

    public void Last()
    {
        if (_nodes.Count > 0)
        {
            Cursor = _nodes.Count - 1;
        }
    }

    public void Nth(int index)
    {
        if (index >= 0 && index < _nodes.Count)
        {
            Cursor = index;
        }
    }
}
