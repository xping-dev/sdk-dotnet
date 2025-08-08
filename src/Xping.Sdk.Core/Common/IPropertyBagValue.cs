/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Common;

/// <summary>
/// IPropertyBagValue is an interface that specifies the common behavior of property bag values that are associated with 
/// test steps as outcomes. Two classes implement this interface: <see cref="PropertyBagValue{TValue}"/> and 
/// <see cref="NonSerializable{TValue}"/>. The former is a serializable value that implements ISerializable and can be 
/// serialized by compatible serializers. The latter is a non-serializable value that is ignored by the serialization 
/// process. Its main function is to store data during the <see cref="PropertyBag{TValue}"/> lifecycle and to transfer 
/// data among various objects that do not need this data to be serialized.
/// </summary>
public interface IPropertyBagValue : IEquatable<IPropertyBagValue>
{
    /// <summary>
    /// Returns a string representation of the property bag value.
    /// </summary>
    /// <returns>A string that represents the current property bag value.</returns>
    string? ToString();
}
