using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Session;

namespace Xping.Sdk.Core.Extensions;

/// <summary>
/// Provides extension methods for the TestSession class to access property bag values.
/// </summary>
public static class TestSessionExtensions
{
    /// <summary>
    /// Gets a non-serializable value from the property bag of the test session.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to get.</typeparam>
    /// <param name="session">The test session to get the value from.</param>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>The value associated with the key, or null if not found.</returns>
    public static TValue? GetNonSerializablePropertyBagValue<TValue>(this TestSession session, PropertyBagKey key)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.TryGetPropertyBagValue(key, out NonSerializable<TValue>? popertyBagValue) &&
            popertyBagValue != null)
        {
            return popertyBagValue.Value;
        }

        return default;
    }

    /// <summary>
    /// Gets a serializable value from the property bag of the test session.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to get.</typeparam>
    /// <param name="session">The test session to get the value from.</param>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>The value associated with the key, or null if not found.</returns>
    public static TValue? GetPropertyBagValue<TValue>(this TestSession session, PropertyBagKey key)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session.TryGetPropertyBagValue(key, out PropertyBagValue<TValue>? popertyBagValue) &&
            popertyBagValue != null)
        {
            return popertyBagValue.Value;
        }

        return default;
    }

    /// <summary>
    /// Tries to get a serializable value from the property bag of the test session.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to get.</typeparam>
    /// <param name="session">The test session to get the value from.</param>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">The output parameter that will receive the value if found.</param>
    /// <returns>True if the value was found, false otherwise.</returns>
    public static bool TryGetPropertyBagValue<TValue>(
        this TestSession session,
        PropertyBagKey key,
        out PropertyBagValue<TValue>? value)
    {
        ArgumentNullException.ThrowIfNull(session, nameof(session));

        PropertyBagValue<TValue>? propertyBagValue = null;
        value = default;

        if (session.Steps.FirstOrDefault(step =>
            step.PropertyBag != null &&
            step.PropertyBag.TryGetProperty(key, out propertyBagValue)) != null &&
            propertyBagValue != null)
        {
            value = propertyBagValue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to get a non-serializable value from the property bag of the test session.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to get.</typeparam>
    /// <param name="session">The test session to get the value from.</param>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">The output parameter that will receive the value if found.</param>
    /// <returns>True if the value was found, false otherwise.</returns>
    public static bool TryGetPropertyBagValue<TValue>(
        this TestSession session,
        PropertyBagKey key,
        out NonSerializable<TValue>? value)
    {
        ArgumentNullException.ThrowIfNull(session, nameof(session));

        NonSerializable<TValue>? propertyBagValue = null;
        value = default;

        if (session.Steps.FirstOrDefault(step =>
            step.PropertyBag != null &&
            step.PropertyBag.TryGetProperty(key, out propertyBagValue)) != null &&
            propertyBagValue != null)
        {
            value = propertyBagValue;
            return true;
        }

        return false;
    }
}
