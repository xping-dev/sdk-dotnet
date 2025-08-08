/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Diagnostics;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Common;

/// <summary>
/// The PropertyBagKey class is used to represent a key in a <see cref="PropertyBag{TValue}"/>. 
/// The IEquatable&lt;PropertyBagKey?&gt; interface is implemented to allow for comparison of PropertyBagKey instances.
/// </summary>
/// <param name="key">
/// The key parameter represents the string value of the PropertyBagKey instance, which is used to 
/// identify this instance in a <see cref="PropertyBag{TValue}"/>.
/// </param>
[Serializable]
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class PropertyBagKey(string key) : IEquatable<PropertyBagKey?>
{
    private readonly string _key = key.RequireNotNullOrEmpty();

    /// <summary>
    /// Determines whether the current PropertyBagKey object is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>
    /// true if the current object and obj are both PropertyBagKey objects and have the same value; otherwise, false.
    /// </returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as PropertyBagKey);
    }

    /// <summary>
    /// Determines whether the current PropertyBagKey object is equal to another PropertyBagKey object.
    /// </summary>
    /// <param name="other">The PropertyBagKey object to compare with the current object.</param>
    /// <returns>true if the current object and other have the same value; otherwise, false.</returns>
    public bool Equals(PropertyBagKey? other)
    {
        return other is not null && _key == other._key;
    }

    /// <summary>
    /// Returns the hash code for the current PropertyBagKey object.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(_key);
    }

    /// <summary>
    /// Overloads the == operator and returns a boolean value indicating whether the two specified PropertyBagKey 
    /// instances are equal.
    /// </summary>
    /// <param name="left">The left operand of the comparison operator</param>
    /// <param name="right">The right operand of the comparison operator</param>
    /// <returns>Boolean value that indicates whether the left parameter is equal to the right parameter</returns>
    public static bool operator ==(PropertyBagKey? left, PropertyBagKey? right)
    {
        return EqualityComparer<PropertyBagKey>.Default.Equals(left, right);
    }

    /// <summary>
    /// Overloads the != operator and returns a boolean value indicating whether the two specified PropertyBagKey 
    /// instances are not equal.
    /// </summary>
    /// <param name="left">The left operand of the comparison operator</param>
    /// <param name="right">The right operand of the comparison operator</param>
    /// <returns>Boolean value that indicates whether the left parameter is not equal to the right parameter</returns>
    public static bool operator !=(PropertyBagKey? left, PropertyBagKey? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Returns a string that represents the current Error object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString() => _key;

    private string GetDebuggerDisplay() => _key;
}
