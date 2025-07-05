/*
 * © 2025 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using System.Diagnostics;
using System.Globalization;
using System.Runtime.Serialization;
using Xping.Sdk.Core.Session.Comparison.Comparers.Internals;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Common;

/// <summary>
/// Represents a serializable value that implements the <see cref="IPropertyBagValue"/> and <see cref="ISerializable"/> 
/// interfaces.
/// </summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
/// <remarks>
/// This class is used to store serializable values that are associated with test steps as outcomes. It supports 
/// multiple types of values: string, string[], byte[], Dictionary&lt;string, string&gt;, int, double, bool, DateTime, and TimeSpan.
/// It throws an ArgumentException during a serialization process if the value to be serialized is not of one of these types.
/// </remarks>
[Serializable]
[KnownType(typeof(byte[]))]
[KnownType(typeof(string[]))]
[KnownType(typeof(Dictionary<string, string>))]
[KnownType(typeof(int))]
[KnownType(typeof(double))]
[KnownType(typeof(bool))]
[KnownType(typeof(DateTime))]
[KnownType(typeof(TimeSpan))]
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public sealed class PropertyBagValue<TValue> : IPropertyBagValue, ISerializable
{
    private static Type Type => typeof(TValue);

    /// <summary>
    /// Gets the value of the serializable property bag value.
    /// </summary>
    public TValue Value { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyBagValue{TValue}"/> class with the specified value.
    /// </summary>
    /// <param name="value">The value of the serializable property bag value.</param>
    public PropertyBagValue(TValue value)
    {
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyBagValue{TValue}"/> class with serialized data.
    /// </summary>
    /// <param name="info">
    /// The <see cref="SerializationInfo"/> that holds the serialized object data about the 
    /// exception being thrown.
    /// </param>
    /// <param name="context">
    /// The <see cref="StreamingContext"/> that contains contextual information about the source or destination.
    /// </param>
    public PropertyBagValue(SerializationInfo info, StreamingContext context)
    {
        ArgumentNullException.ThrowIfNull(info, nameof(info));

        var type = Type
            .GetType(info.GetString(nameof(Type))
            .RequireNotNull(nameof(Type)));
        Value = (TValue)info.GetValue(nameof(Value), type!).RequireNotNull(nameof(Value));
    }

    /// <summary>
    /// Determines whether the current IPropertyBagValue object is equal to another IPropertyBagValue object.
    /// </summary>
    /// <param name="other">The IPropertyBagValue object to compare with the current object.</param>
    /// <returns><c>true</c> if the current object and other have the same value; otherwise, <c>false</c>.</returns>
    public bool Equals(IPropertyBagValue? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is PropertyBagValue<TValue> otherBag)
        {
            if (ReferenceEquals(Value, otherBag.Value))
            {
                return true;
            }

            if (Value == null || otherBag.Value == null)
            {
                return false;
            }

            return Value switch
            {
                string str1 => otherBag.Value is string str2 && str1 == str2,
                byte[] bytes1 => otherBag.Value is byte[] bytes2 && ArrayComparer.AreByteArraysEqual(bytes1, bytes2),
                string[] array1 => otherBag.Value is string[] array2 && ArrayComparer.AreArraysEqual(array1, array2),
                Dictionary<string, string> dictionary1 => otherBag.Value is Dictionary<string, string> dictionary2 &&
                    DictionaryComparer.CompareDictionaries(dictionary1, dictionary2),
                int int1 => otherBag.Value is int int2 && int1 == int2,
                double double1 => otherBag.Value is double double2 && double1.Equals(double2),
                bool bool1 => otherBag.Value is bool bool2 && bool1 == bool2,
                DateTime dateTime1 => otherBag.Value is DateTime dateTime2 && dateTime1 == dateTime2,
                TimeSpan timeSpan1 => otherBag.Value is TimeSpan timeSpan2 && timeSpan1 == timeSpan2,
                _ => Value.Equals(otherBag.Value)
            };
        }

        return false;
    }

    /// <summary>
    /// Determines whether the current IPropertyBagValue object is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>
    /// <c>true</c> if the current object and obj are both IPropertyBagValue objects and have the same value; otherwise, 
    /// <c>false</c>.
    /// </returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as PropertyBagValue<TValue>);
    }

    /// <summary>
    /// Returns the hash code for the current Error object.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode()
    {
        return Value?.GetHashCode() ?? string.GetHashCode(string.Empty, StringComparison.InvariantCulture);
    }

    /// <summary>
    /// Returns a string that represents the current PropertyBagValue object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string? ToString()
    {
        return Value switch
        {
            string str => str,
            byte[] bytes => $"{bytes.Length} bytes",
            string[] array => string.Join("; ", array.Select(a => $"\"{a}\"")),
            Dictionary<string, string> dictionary =>
                string.Join("; ", dictionary.Select(kvp => $"[{kvp.Key}]:\"{kvp.Value}\"")),
            int intVal => intVal.ToString(CultureInfo.InvariantCulture),
            double doubleVal => doubleVal.ToString("G", CultureInfo.InvariantCulture),
            bool boolVal => $"{boolVal}",
            DateTime dateTimeVal => dateTimeVal.ToString("O"), // ISO 8601 format
            TimeSpan timeSpanVal => timeSpanVal.ToString("c"), // Constant format
            _ => base.ToString()
        };
    }

    void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
    {
        // Null values are not permitted, since a null entry represents the absence of the key.
        if (Value is null)
        {
            return;
        }

        info.AddValue(nameof(Type), typeof(TValue).FullName);
        info.AddValue(nameof(Value), Value switch
        {
            string str => str,
            byte[] bytes => bytes,
            string[] array => array,
            Dictionary<string, string> dictionary => dictionary,
            int intVal => intVal,
            double doubleVal => doubleVal,
            bool boolVal => boolVal,
            DateTime dateTimeVal => dateTimeVal,
            TimeSpan timeSpanVal => timeSpanVal,
            _ => throw new ArgumentException(
                "Invalid type argument. Only byte[], string, string[], Dictionary<string, string>, int, double, bool, DateTime, and TimeSpan are supported.")
        });
    }

    private string GetDebuggerDisplay()
    {
        return this.ToString() ?? "n/a";
    }
}