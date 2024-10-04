using Xping.Sdk.Core.Common;
using Xping.Sdk.Core.Components;

namespace Xping.Sdk.Core.Extensions;

/// <summary>
/// Provides extension methods for the TestContext class to access property bag values.
/// </summary>
public static class TestContextExtensions
{
    /// <summary>
    /// Gets a non-serializable value from the property bag of the test context.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to get.</typeparam>
    /// <param name="context">The test context to get the value from.</param>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>The value associated with the key, or null if not found.</returns>
    public static TValue? GetNonSerializablePropertyBagValue<TValue>(this TestContext context, PropertyBagKey key)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.TryGetPropertyBagValue(key, out NonSerializable<TValue>? popertyBagValue) &&
            popertyBagValue != null)
        {
            return popertyBagValue.Value;
        }

        return default;
    }

    /// <summary>
    /// Gets a serializable value from the property bag of the test context.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to get.</typeparam>
    /// <param name="context">The test context to get the value from.</param>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>The value associated with the key, or null if not found.</returns>
    public static TValue? GetPropertyBagValue<TValue>(this TestContext context, PropertyBagKey key)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.TryGetPropertyBagValue(key, out PropertyBagValue<TValue>? popertyBagValue) &&
            popertyBagValue != null)
        {
            return popertyBagValue.Value;
        }

        return default;
    }

    internal static bool TryGetPropertyBagValue<TValue>(
        this TestContext context,
        PropertyBagKey key,
        out PropertyBagValue<TValue>? value) 
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        PropertyBagValue<TValue>? propertyBagValue = null;
        value = default;

        if (context.SessionBuilder.Steps.FirstOrDefault(step =>
            step.PropertyBag != null &&
            step.PropertyBag.TryGetProperty(key, out propertyBagValue)) != null &&
            propertyBagValue != null)
        {
            value = propertyBagValue;
            return true;
        }

        return false;
    }

    internal static bool TryGetPropertyBagValue<TValue>(
        this TestContext context,
        PropertyBagKey key,
        out NonSerializable<TValue>? value)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        NonSerializable<TValue>? propertyBagValue = null;
        value = default;

        if (context.SessionBuilder.Steps.FirstOrDefault(step =>
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
