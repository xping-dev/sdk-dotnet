/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Session.Comparison.Comparers.Internals;

using System;
using System.Linq;

internal sealed class ArrayComparer
{
    /// <summary>
    /// Compares two string arrays for equality.
    /// </summary>
    /// <param name="array1">The first string array to compare.</param>
    /// <param name="array2">The second string array to compare.</param>
    /// <returns><c>true</c> if the arrays are equal; otherwise, <c>false</c>.</returns>
    public static bool AreArraysEqual(string[] array1, string[] array2)
    {
        if (ReferenceEquals(array1, array2))
        {
            return true;
        }

        if (array1 == null || array2 == null)
        {
            return false;
        }

        return array1.SequenceEqual(array2);
    }

    /// <summary>
    /// This method first checks if both arrays are the same instance or if either is null. Then it compares their 
    /// lengths, and finally, it iterates through the arrays comparing each element. If any elements are different, it 
    /// returns false. If all elements are the same, it returns true. This method is efficient for small to medium-sized 
    /// arrays. It is not designed to work for very large arrays.
    /// </summary>
    /// <param name="array1">The first byte array to compare.</param>
    /// <param name="array2">The second byte array to compare.</param>
    /// <returns><c>true</c> if the arrays are equal; otherwise, <c>false</c>.</returns>
    public static bool AreByteArraysEqual(byte[] array1, byte[] array2)
    {
        if (ReferenceEquals(array1, array2))
        {
            return true;
        }

        if (array1 == null || array2 == null)
        {
            return false;
        }

        if (array1.Length != array2.Length)
        {
            return false;
        }

        for (int i = 0; i < array1.Length; i++)
        {
            if (array1[i] != array2[i])
            {
                return false;
            }
        }

        return true;
    }
}
