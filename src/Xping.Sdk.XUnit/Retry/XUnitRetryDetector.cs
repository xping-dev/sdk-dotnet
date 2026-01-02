/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit.Retry;

using System;
using System.Linq;
using System.Reflection;
using Xunit.Abstractions;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Retry;

/// <summary>
/// Detects retry attributes and metadata for xUnit tests.
/// </summary>
/// <remarks>
/// XUnit does not have native retry support, but several popular libraries provide retry functionality:
/// - xunit.extensions.retry (RetryFact, RetryTheory)
/// - Custom retry implementations
/// This detector uses reflection to identify and extract metadata from these retry attributes.
/// </remarks>
public sealed class XUnitRetryDetector : IRetryDetector<ITest>
{
    private static readonly string[] AttemptSeparators = ["(attempt", ")"];
    /// <inheritdoc/>
    public RetryMetadata? DetectRetryMetadata(ITest test)
    {
        if (test?.TestCase?.TestMethod?.Method == null)
        {
            return null;
        }

        var method = test.TestCase.TestMethod.Method;

        // Try to get reflection-based method info
        var methodInfo = GetMethodInfo(method);
        if (methodInfo == null)
        {
            return null;
        }

        // Look for retry attributes
        var retryAttribute = FindRetryAttribute(methodInfo);
        if (retryAttribute == null)
        {
            return null;
        }

        return ExtractRetryMetadata(retryAttribute, test);
    }

    /// <inheritdoc/>
    public bool HasRetryAttribute(ITest test)
    {
        return DetectRetryMetadata(test) != null;
    }

    /// <inheritdoc/>
    public int GetCurrentAttemptNumber(ITest test)
    {
        // xUnit doesn't expose retry attempt count directly in ITest
        // We need to look for custom properties or trait data

        if (test?.TestCase == null)
        {
            return 1;
        }

        // Check for retry attempt in traits (some retry libraries set this)
        var traits = test.TestCase.Traits;
        if (traits.TryGetValue("RetryAttempt", out var attemptValues))
        {
            if (attemptValues.Count > 0 && int.TryParse(attemptValues.First(), out var attempt))
            {
                return attempt;
            }
        }

        // Try to extract from display name (some retry libraries append attempt number)
        var attemptFromDisplayName = GetAttemptNumberFromDisplayName(test.DisplayName);
        if (attemptFromDisplayName > 1)
        {
            return attemptFromDisplayName;
        }

        // Default to first attempt
        return 1;
    }

    private static MethodInfo? GetMethodInfo(IMethodInfo method)
    {
        try
        {
            // xUnit's IMethodInfo can be converted to reflection MethodInfo
            var typeName = method.Type.Name;
            var type = Type.GetType(typeName);

            if (type == null)
            {
                // Try to find the type in loaded assemblies
                type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.FullName == typeName);
            }

            if (type == null)
            {
                return null;
            }

            var methodInfo = type.GetMethod(
                method.Name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            return methodInfo;
        }
        catch
        {
            return null;
        }
    }

    private static Attribute? FindRetryAttribute(MethodInfo methodInfo)
    {
        // Get all attributes
        var attributes = methodInfo.GetCustomAttributes(true);

        // Look for known retry attributes
        foreach (var attr in attributes)
        {
            var attrType = attr.GetType();
            var attrName = attrType.Name.Replace("Attribute", "");

            if (RetryAttributeRegistry.IsRegisteredForFramework("xunit", attrName))
            {
                return attr as Attribute;
            }
        }

        return null;
    }

    private static RetryMetadata ExtractRetryMetadata(Attribute retryAttribute, ITest test)
    {
        var attrType = retryAttribute.GetType();
        var metadata = new RetryMetadata
        {
            RetryAttributeName = attrType.Name.Replace("Attribute", ""),
            AttemptNumber = 1, // Will be updated during execution
        };

        // Extract MaxRetries property (common in retry attributes)
        var maxRetriesProperty = attrType.GetProperty("MaxRetries") ?? attrType.GetProperty("Count");
        if (maxRetriesProperty != null)
        {
            var value = maxRetriesProperty.GetValue(retryAttribute);
            if (value is int maxRetries)
            {
                metadata.MaxRetries = maxRetries;
            }
        }

        // Extract Delay property if available
        var delayProperty = attrType.GetProperty("DelayMilliseconds") ?? attrType.GetProperty("Delay");
        if (delayProperty != null)
        {
            var value = delayProperty.GetValue(retryAttribute);
            if (value is int delayMs)
            {
                metadata.DelayBetweenRetries = TimeSpan.FromMilliseconds(delayMs);
            }
        }

        // Extract Reason property if available
        var reasonProperty = attrType.GetProperty("Reason") ?? attrType.GetProperty("RetryReason");
        if (reasonProperty != null)
        {
            var value = reasonProperty.GetValue(retryAttribute);
            if (value is string reason && !string.IsNullOrWhiteSpace(reason))
            {
                metadata.RetryReason = reason;
            }
        }

        // Get current attempt number
        metadata.AttemptNumber = GetAttemptNumberFromTest(test);

        // Determine if passed on retry
        metadata.PassedOnRetry = metadata.AttemptNumber > 1 && test.TestCase != null;

        // Store additional metadata from other properties
        ExtractAdditionalMetadata(retryAttribute, metadata);

        return metadata;
    }

    private static int GetAttemptNumberFromTest(ITest test)
    {
        // Try to extract from display name
        var displayName = test.DisplayName;
        return GetAttemptNumberFromDisplayName(displayName);
    }

    private static int GetAttemptNumberFromDisplayName(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return 1;
        }

        // Some retry libraries append attempt number like: TestName (attempt 2)
        if (displayName.IndexOf("(attempt", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var parts = displayName.Split(AttemptSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                // After split, parts[1] contains the attempt number before the closing ")"
                var attemptStr = parts[1].Trim();
                if (int.TryParse(attemptStr, out var attempt))
                {
                    return attempt;
                }
            }
        }

        // Some libraries use format: TestName [Retry 2]
        var retryIndex = displayName.IndexOf("[retry", StringComparison.OrdinalIgnoreCase);
        if (retryIndex >= 0)
        {
            var endIndex = displayName.IndexOf(']', retryIndex);
            if (endIndex > retryIndex)
            {
                // Extract text between "[retry" and "]"
                var retryText = displayName.Substring(retryIndex + 6, endIndex - (retryIndex + 6)).Trim();
                if (int.TryParse(retryText, out var attempt))
                {
                    return attempt;
                }
            }
        }

        return 1;
    }

    private static void ExtractAdditionalMetadata(Attribute retryAttribute, RetryMetadata metadata)
    {
        var attrType = retryAttribute.GetType();

        // Look for common retry-related properties
        var propertiesToCheck = new[]
        {
            "ExceptionTypes",
            "Filter",
            "OnlyRetryOn",
            "Skip",
            "Timeout"
        };

        foreach (var propertyName in propertiesToCheck)
        {
            var property = attrType.GetProperty(propertyName);
            if (property != null)
            {
                try
                {
                    var value = property.GetValue(retryAttribute);
                    if (value != null)
                    {
                        metadata.AdditionalMetadata[propertyName] = value.ToString() ?? string.Empty;
                    }
                }
                catch
                {
                    // Ignore property access errors
                }
            }
        }
    }
}
