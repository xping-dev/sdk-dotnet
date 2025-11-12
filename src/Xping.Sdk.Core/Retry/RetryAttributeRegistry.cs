/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Retry;

using System;
using System.Collections.Generic;

/// <summary>
/// Registry of known retry attribute names across different test frameworks.
/// </summary>
/// <remarks>
/// Maintains a list of common retry attribute names to help identify
/// retry mechanisms even when reflection-based detection isn't available.
/// Supports registration of custom retry attributes for extensibility.
/// </remarks>
public static class RetryAttributeRegistry
{
    /// <summary>
    /// Known retry attribute names for xUnit.
    /// </summary>
    /// <remarks>
    /// Common xUnit retry attributes include:
    /// - RetryFact: Retry variation of [Fact]
    /// - RetryTheory: Retry variation of [Theory]
    /// - RetryAttribute: Generic retry attribute
    /// - XunitRetryAttribute: Explicit xUnit retry attribute
    /// </remarks>
    public static readonly HashSet<string> XUnitRetryAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "RetryFact",
        "RetryTheory",
        "RetryAttribute",
        "XunitRetryAttribute",
    };

    /// <summary>
    /// Known retry attribute names for NUnit.
    /// </summary>
    /// <remarks>
    /// NUnit has native retry support:
    /// - Retry: Native NUnit retry attribute (recommended)
    /// - RetryAttribute: Full name variation
    /// - NUnit.Framework.RetryAttribute: Fully qualified name
    /// </remarks>
    public static readonly HashSet<string> NUnitRetryAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Retry",
        "RetryAttribute",
        "NUnit.Framework.RetryAttribute",
    };

    /// <summary>
    /// Known retry attribute names for MSTest.
    /// </summary>
    /// <remarks>
    /// MSTest does not have native retry support, but common custom implementations include:
    /// - TestRetry: Custom retry attribute
    /// - RetryAttribute: Generic retry attribute
    /// - DataTestMethodWithRetry: Retry for data-driven tests
    /// </remarks>
    public static readonly HashSet<string> MSTestRetryAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TestRetry",
        "RetryAttribute",
        "DataTestMethodWithRetry",
    };

    /// <summary>
    /// Checks if the given attribute name is a known retry attribute for any framework.
    /// </summary>
    /// <param name="attributeName">The name of the attribute to check.</param>
    /// <returns>True if the attribute is recognized as a retry attribute; otherwise, false.</returns>
    /// <remarks>
    /// This method checks all framework registries to determine if the attribute name
    /// matches any known retry attribute pattern. Case-insensitive comparison is used.
    /// </remarks>
    public static bool IsKnownRetryAttribute(string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return false;
        }

        return XUnitRetryAttributes.Contains(attributeName) ||
               NUnitRetryAttributes.Contains(attributeName) ||
               MSTestRetryAttributes.Contains(attributeName);
    }

    /// <summary>
    /// Registers a custom retry attribute name for a specific framework.
    /// </summary>
    /// <param name="framework">The target framework: "xunit", "nunit", or "mstest" (case-insensitive).</param>
    /// <param name="attributeName">The name of the custom retry attribute to register.</param>
    /// <remarks>
    /// This allows users to register custom retry attributes that are not in the default registry.
    /// Once registered, the attribute will be recognized by <see cref="IsKnownRetryAttribute"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Register a custom XUnit retry attribute
    /// RetryAttributeRegistry.RegisterCustomRetryAttribute("xunit", "MyCustomRetry");
    /// 
    /// // Register a custom MSTest retry attribute
    /// RetryAttributeRegistry.RegisterCustomRetryAttribute("mstest", "ReliabilityTest");
    /// </code>
    /// </example>
    public static void RegisterCustomRetryAttribute(string framework, string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return;
        }

        var registry = framework?.ToUpperInvariant() switch
        {
            "XUNIT" => XUnitRetryAttributes,
            "NUNIT" => NUnitRetryAttributes,
            "MSTEST" => MSTestRetryAttributes,
            _ => null
        };

        registry?.Add(attributeName);
    }

    /// <summary>
    /// Gets all registered retry attribute names for a specific framework.
    /// </summary>
    /// <param name="framework">The target framework: "xunit", "nunit", or "mstest" (case-insensitive).</param>
    /// <returns>
    /// A collection of retry attribute names, or an empty set if the framework is not recognized.
    /// </returns>
    public static HashSet<string> GetAttributesForFramework(string framework)
    {
        var registry = framework?.ToUpperInvariant() switch
        {
            "XUNIT" => XUnitRetryAttributes,
            "NUNIT" => NUnitRetryAttributes,
            "MSTEST" => MSTestRetryAttributes,
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };

        return registry;
    }

    /// <summary>
    /// Checks if a specific framework has a registered retry attribute.
    /// </summary>
    /// <param name="framework">The target framework: "xunit", "nunit", or "mstest" (case-insensitive).</param>
    /// <param name="attributeName">The attribute name to check.</param>
    /// <returns>True if the attribute is registered for the specified framework; otherwise, false.</returns>
    public static bool IsRegisteredForFramework(string framework, string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return false;
        }

        var registry = GetAttributesForFramework(framework);
        return registry.Contains(attributeName);
    }

    /// <summary>
    /// Clears all custom registrations for a specific framework, resetting to defaults.
    /// </summary>
    /// <param name="framework">The target framework: "xunit", "nunit", or "mstest" (case-insensitive).</param>
    /// <remarks>
    /// This method is primarily intended for testing scenarios.
    /// Note: This does not affect the default attributes, only custom registrations.
    /// </remarks>
    internal static void ResetFramework(string framework)
    {
        // Note: Since we can't truly reset the HashSet to defaults without tracking custom additions,
        // this is left as internal for potential future implementation.
        // For now, custom attributes remain registered for the lifetime of the application.
    }
}
