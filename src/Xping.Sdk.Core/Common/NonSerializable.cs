namespace Xping.Sdk.Core.Common;

/// <summary>
/// Represents a non-serializable value that implements the <see cref="IPropertyBagValue"/> interface.
/// </summary>
/// <remarks>
/// This class is used to store any value that should be excluded from being serialized during the 
/// <see cref="PropertyBag{TValue}"/> serialization process. Its main purpose is to transfer data among different 
/// objects that do not need this data to be serialized.
/// </remarks>
public sealed class NonSerializable<TValue>(TValue value) : IPropertyBagValue
{
    /// <summary>
    /// Gets the value of the non-serializable property bag value.
    /// </summary>
    /// <value>
    /// The value of the non-serializable property bag value.
    /// </value>
    public TValue Value { get; init; } = value;

    /// <summary>
    /// Determines whether the current NonSerializable object is equal to another NonSerializable object.
    /// </summary>
    /// <param name="other">The NonSerializable object to compare with the current object.</param>
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

        if (other is NonSerializable<TValue> nonSerializable)
        {
            return Value?.Equals(nonSerializable.Value) ?? false;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the current Error object is equal to a specified object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>
    /// true if the current object and obj are both Error objects and have the same value; otherwise, false.
    /// </returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as NonSerializable<TValue>);
    }

    /// <summary>
    /// Returns the hash code for the current Error object.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode()
    {
        return Value?.GetHashCode() ?? string.GetHashCode(string.Empty, StringComparison.InvariantCulture);
    }
}