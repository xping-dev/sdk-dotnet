/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Collections;

namespace Xping.Sdk.Actions.Internals;

internal class OrderedUrlRedirections : IEnumerable<string>
{
    private readonly HashSet<string> _hashSet = [];
    private readonly List<string> _insertionOrder = [];

    public int Count => _hashSet.Count;

    // Add items to HashSet and track order of insertion
    public bool Add(string? url)
    {
        if (!string.IsNullOrEmpty(url) && _hashSet.Add(url))
        {
            _insertionOrder.Add(url);
            return true;
        }

        return false;
    }

    public bool Contains(string? value) => value != null && _hashSet.Contains(value);

    // Find first item matching a condition starting from the end
    public string? FindLastMatchingItem(Func<string, bool> matchCondition)
    {
        for (int i = _insertionOrder.Count - 1; i >= 0; i--)
        {
            if (matchCondition(_insertionOrder[i]))
            {
                return _insertionOrder[i];
            }
        }

        return null;
    }

    public void Clear()
    {
        _hashSet.Clear();
        _insertionOrder.Clear();
    }

    public IEnumerator<string> GetEnumerator()
    {
        return ((IEnumerable<string>)_insertionOrder).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_insertionOrder).GetEnumerator();
    }
}
