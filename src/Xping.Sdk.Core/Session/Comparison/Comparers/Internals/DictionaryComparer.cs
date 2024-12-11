namespace Xping.Sdk.Core.Session.Comparison.Comparers.Internals;

internal sealed class DictionaryComparer
{
    /// <summary>
    /// Compares two Dictionary&lt;string, string&gt; dictionaries for equality.
    /// </summary>
    /// <param name="dict1">The first dictionary to compare.</param>
    /// <param name="dict2">The second dictionary to compare.</param>
    /// <returns><c>true</c> if the dictionaries are equal; otherwise, <c>false</c>.</returns>
    public static bool CompareDictionaries<TKey, TValue>(Dictionary<TKey, TValue> dict1, Dictionary<TKey, TValue> dict2)
        where TKey : class
    {
        if (dict1 == null || dict2 == null)
        {
            return false;
        }

        if (ReferenceEquals(dict1, dict2))
        {
            return true;
        }

        // Check if the dictionaries have the same number of items
        if (dict1.Count != dict2.Count)
        {
            return false; // If not, they are not equal
        }

        // Loop over the key-value pairs of the first dictionary
        foreach (var pair in dict1)
        {
            // Get the key and value
            TKey key = pair.Key;
            TValue value = pair.Value;

            // Check if the second dictionary contains the same key
            if (!dict2.TryGetValue(key, out TValue? value2))
            {
                return false; // If not, they are not equal
            }

            // Check if the second dictionary has the same value for the key
            if (!ReferenceEquals(value, value2))
            {
                if (value == null || value2 == null)
                {
                    return false; // If not, they are not equal
                }

                if (!value2.Equals(value))
                {
                    return false; // If not, they are not equal
                }
            }
        }

        // If the loop completes without returning false, the dictionaries are equal
        return true;
    }
}
