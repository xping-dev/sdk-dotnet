/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Xping.Sdk.Core.Session.Comparison.Comparers.Internals;

namespace Xping.Sdk.Core.Common;

/// <summary>
/// PropertyBag class represents a collection of key-value pairs that allows to store any object for a given unique key.
/// All keys are represented by <see cref="PropertyBagKey"/> but values may be of any type. Null values are not 
/// permitted, since a null entry represents the absence of the key.
/// </summary>
[Serializable]
public sealed class PropertyBag<TValue> : ISerializable, IEquatable<PropertyBag<TValue>>
{
    private const string SerializableEntryName = "Properties";

    private readonly Dictionary<PropertyBagKey, TValue> _properties;

    /// <summary>
    /// Gets the number of items in the collection.
    /// </summary>
    public int Count => _properties.Count;

    /// <summary>
    /// Gets a read-only collection that contains the key-value pairs in the property bag.
    /// </summary>
    public IReadOnlyDictionary<PropertyBagKey, TValue> Properties => _properties.AsReadOnly();

    /// <summary>
    /// Gets a read-only collection that contains the keys in the property bag.
    /// </summary>
    public IReadOnlyCollection<PropertyBagKey> Keys => _properties.Keys;

    /// <summary>
    /// Gets a read-only collection that contains the values in the property bag.
    /// </summary>
    public IReadOnlyCollection<TValue> Values => _properties.Values;

    /// <summary>
    /// Initializes a new instance of the PropertyBag class.
    /// </summary>
    /// <param name="properties">An optional dictionary of properties to initialize the property bag with.</param>
    /// <remarks>
    /// Null values are disallowed as they signify a non-existent key. Any attempt to add a null value will result in it 
    /// being disregarded.
    /// </remarks>
    public PropertyBag(IDictionary<PropertyBagKey, TValue>? properties = null)
    {
        _properties = properties?.Where(kvp => kvp.Value != null).ToDictionary() ?? [];
    }

    /// <summary>
    /// Initializes a new instance of the PropertyBag class with serialized data.
    /// </summary>
    /// <param name="info">The SerializationInfo that holds the serialized object data.</param>
    /// <param name="context">
    /// The StreamingContext that contains contextual information about the source or destination.
    /// </param>
    /// <remarks>
    /// The PropertyBag class implements the ISerializable interface and can be serialized and deserialized using binary 
    /// or XML formatters.
    /// </remarks>
    public PropertyBag(SerializationInfo info, StreamingContext context)
    {
        ArgumentNullException.ThrowIfNull(info, nameof(info));

        if (info.GetValue(SerializableEntryName, typeof(Dictionary<PropertyBagKey, TValue>))
            is Dictionary<PropertyBagKey, TValue> properties)
        {
            _properties = properties;
        }
        else
        {
            _properties = [];
        }
    }

    /// <summary>
    /// Determines whether the current <see cref="PropertyBag{TValue}"/> object is equal to another 
    /// <see cref="PropertyBag{TValue}"/> object.
    /// </summary>
    /// <param name="other">The <see cref="PropertyBag{TValue}"/> object to compare with the current object.</param>
    /// <returns><c>true</c> if the current object and other have the same value; otherwise, <c>false</c>.</returns>
    public bool Equals(PropertyBag<TValue>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (_properties == null || other._properties == null)
        {
            return false;
        }

        return DictionaryComparer.CompareDictionaries(_properties, other._properties);
    }

    /// <summary>
    /// Determines whether the current <see cref="PropertyBag{TValue}"/> object is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>
    /// <c>true</c> if the current object and obj are both <see cref="PropertyBag{TValue}"/> objects and have the same 
    /// value; otherwise, <c>false</c>.
    /// </returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as PropertyBag<TValue>);
    }

    /// <summary>
    /// Returns the hash code for the current <see cref="PropertyBag{TValue}"/> object.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode()
    {
        return _properties.GetHashCode();
    }

    /// <summary>
    /// Determines whether two <see cref="PropertyBag{TValue}"/> objects have the same value.
    /// </summary>
    /// <param name="lhs">The first <see cref="PropertyBag{TValue}"/> object to compare.</param>
    /// <param name="rhs">The second <see cref="PropertyBag{TValue}"/> object to compare.</param>
    /// <returns><c>true</c> if lhs and rhs have the same value; otherwise, <c>false</c>.</returns>
    public static bool operator ==(PropertyBag<TValue>? lhs, PropertyBag<TValue>? rhs)
    {
        if (lhs is null || rhs is null)
        {
            return Equals(lhs, rhs);
        }

        return lhs.Equals(rhs);
    }

    /// <summary>
    /// Determines whether two <see cref="PropertyBag{TValue}"/> objects have different values.
    /// </summary>
    /// <param name="lhs">The first <see cref="PropertyBag{TValue}"/> object to compare.</param>
    /// <param name="rhs">The second <see cref="PropertyBag{TValue}"/> object to compare.</param>
    /// <returns><c>true</c> if lhs and rhs have different values; otherwise, <c>false</c>.</returns>
    public static bool operator !=(PropertyBag<TValue>? lhs, PropertyBag<TValue>? rhs)
    {
        if (lhs is null || rhs is null)
        {
            return !Equals(lhs, rhs);
        }

        return !lhs.Equals(rhs);
    }

    /// <summary>
    /// Determines whether the collection contains an element that has the specified key.
    /// </summary>
    /// <param name="key">A key represented as <see cref="PropertyBagKey"/> type.</param>
    /// <returns>Boolean value determining whether the key is found in a collection.</returns>
    public bool ContainsKey(PropertyBagKey key) => _properties.ContainsKey(key);

    /// <summary>
    /// Adds or updates a collection of key-value pairs to the collection.
    /// </summary>
    /// <param name="properties">A collection of a key-value pairs, where key is represented as 
    /// <see cref="PropertyBagKey"/> type.</param>
    /// <remarks>
    /// If a key is not found in the collection, a new key-value pair is created. If a key is already present in the
    /// collection, the value associated with the key is updated.
    /// </remarks>
    public void AddOrUpdateProperties(IDictionary<PropertyBagKey, TValue> properties)
    {
        ArgumentNullException.ThrowIfNull(properties, nameof(properties));

        foreach (var property in properties)
        {
            AddOrUpdateProperty(property.Key, property.Value);
        }
    }

    /// <summary>
    /// Adds a new key-value pair to the property bag or updates the value of an existing key.
    /// </summary>
    /// <param name="key">A key represented as <see cref="PropertyBagKey"/> type.</param>
    /// <param name="value">Any value which should be stored in a collection. </param>
    /// <remarks>
    /// If a key is not found in the collection, a new key-value pair is created. If a key is already present in the
    /// collection, the value associated with the key is updated. Null values are disallowed as they signify a 
    /// non-existent key. Any attempt to add a null value will result in it being disregarded.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when the key is null.</exception>
    public void AddOrUpdateProperty(PropertyBagKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        
        if (value == null) // Null values are disallowed as they signify a non-existent key.
        {
            return;
        }

        if (!_properties.TryAdd(key, value))
        {
            _properties[key] = value;
        }
    }

    /// <summary>
    /// This method attempts to get the value associated with the specified key from the collection.
    /// </summary>
    /// <param name="key">A key represented as <see cref="PropertyBagKey"/> type.</param>
    /// <param name="value">
    /// When this method returns, contains the object value stored in the collection, if the key is found, or null if 
    /// the key is not found.
    /// </param>
    /// <returns>true if a key was found successfully; otherwise, false</returns>
    public bool TryGetProperty(PropertyBagKey key, out TValue? value)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (_properties.TryGetValue(key, out value))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// This method attempts to get the value of the specified type T from a PropertyBagValue associated with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of value to extract from the PropertyBagValue.</typeparam>
    /// <param name="key">A key represented as <see cref="PropertyBagKey"/> type.</param>
    /// <param name="value">
    /// When this method returns, contains the value of type T extracted from the PropertyBagValue, if the key is found 
    /// and the value can be converted to type T, or the default value of T if the key is not found or conversion fails.
    /// </param>
    /// <returns>true if a key was found successfully and the value could be converted to type T; otherwise, false</returns>
    public bool TryGetPropertyValue<T>(PropertyBagKey key, out T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        value = default!;

        if (_properties.TryGetValue(key, out TValue? bagValue) &&
            bagValue != null &&
            bagValue is PropertyBagValue<T> propertyBagValue)
        {
            value = propertyBagValue.Value;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the value of the specified type T from a PropertyBagValue associated with the specified key.
    /// </summary>
    /// <typeparam name="T">The type of value to extract from the PropertyBagValue.</typeparam>
    /// <param name="key">A key represented as <see cref="PropertyBagKey"/> type.</param>
    /// <returns>The value of type T extracted from the PropertyBagValue. If the specified key is not found, this operation
    /// throws a KeyNotFoundException exception. If the specified key is found, but its type does not match
    /// with PropertyBagValue&lt;T&gt; it throws InvalidCastException.</returns>
    /// <exception cref="KeyNotFoundException">If the specified key is not found.</exception>
    /// <exception cref="InvalidCastException">If the value is not a PropertyBagValue&lt;T&gt; or cannot be converted to type T.</exception>
    public T GetPropertyValue<T>(PropertyBagKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (!_properties.TryGetValue(key, out TValue? bagValue))
        {
            throw new KeyNotFoundException($"The key '{key}' was not found in the property bag.");
        }

        if (bagValue is not PropertyBagValue<T> propertyBagValue)
        {
            throw new InvalidCastException($"The value associated with key '{key}' is not a PropertyBagValue<{typeof(T).Name}>.");
        }

        return propertyBagValue.Value;
    }

    /// <summary>
    /// Gets the value associated with the specified key from the collection.
    /// </summary>
    /// <param name="key">A key represented as <see cref="PropertyBagKey"/> type.</param>
    /// <returns>The value associated with the specified key. If the specified key is not found, this operation
    /// throws a KeyNotFoundException exception.</returns>
    /// <exception cref="KeyNotFoundException">If the specified key is not found.</exception>
    public TValue GetProperty(PropertyBagKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return _properties[key];
    }

    /// <summary>
    /// This method attempts to get the value associated with the specified key from the collection and cast it 
    /// to the specified type T.
    /// </summary>
    /// <typeparam name="T">The value type associated with the specified key.</typeparam>
    /// <param name="key">A key represented as <see cref="PropertyBagKey"/> type.</param>
    /// <param name="value">When this method returns, contains the value of specified <typeparamref name="T"/> type
    /// stored in the collection, if the key is found, or default value of <typeparamref name="T"/> if the key is
    /// not found.
    /// </param>
    /// <returns>true if a key was found successfully and its type matches with <typeparamref name="T"/>; 
    /// otherwise, false
    /// </returns>
    /// <remarks>This method does not throw exception when type of a value associated with a given key does not match 
    /// with <typeparamref name="T"/>.
    /// </remarks>
    public bool TryGetProperty<T>(PropertyBagKey key, out T? value) where T : TValue
    {
        value = default;

        // It is not expected to throw InvalidCastException when property cannot be cast to type T.
        if (TryGetProperty(key, out TValue? bag) && bag is T TProperty)
        {
            value = TProperty;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the value associated with the specified key from the collection and casts it to the specified type
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The value type associated with the specified key.</typeparam>
    /// <param name="key">A key represented as <see cref="PropertyBagKey"/> type.</param>
    /// <returns>The value associated with the specified key. If the specified key is not found, this operation
    /// throws a KeyNotFoundException exception. If the specified key is found, however its type does not match
    /// with <typeparamref name="T"/> it throws InvalidCastException.
    /// </returns>
    /// <exception cref="KeyNotFoundException">If the specified key is not found.</exception>
    /// <exception cref="InvalidCastException">If the specified value type <typeparamref name="T"/> does not match 
    /// with a value type stored in the collection.
    /// </exception>
    public T GetProperty<T>(PropertyBagKey key) where T : TValue
    {
        // It is expected to throw InvalidCastException when property cannot be cast to type T.
        return (T)GetProperty(key)!;
    }

    /// <summary>
    /// Attempts to remove the property associated with the specified key from the property bag.
    /// </summary>
    /// <param name="key">The key of the property to remove.</param>
    /// <returns>true if the property is successfully found and removed; otherwise, false.</returns>
    public bool Clear(PropertyBagKey key)
    {
        return _properties.Remove(key);
    }

    /// <summary>
    /// Removes all items from the collection.
    /// </summary>
    public void Clear()
    {
        _properties.Clear();
    }

    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    {
        ArgumentNullException.ThrowIfNull(info, nameof(info));

        var serializable = _properties
            .Where(item => item.Value is ISerializable)
            .ToDictionary(i => i.Key, i => i.Value);
        info.AddValue(SerializableEntryName, serializable);
    }
}
