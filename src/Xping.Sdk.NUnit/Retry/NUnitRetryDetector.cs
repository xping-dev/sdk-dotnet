/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit.Retry;

using System;
using System.Reflection;
using global::NUnit.Framework.Interfaces;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Retry;

/// <summary>
/// Detects retry attributes and metadata for NUnit tests.
/// </summary>
/// <remarks>
/// NUnit has native retry support via the [Retry] attribute.
/// This detector uses reflection to identify and extract metadata from retry attributes.
/// </remarks>
public sealed class NUnitRetryDetector : IRetryDetector<ITest>
{
    /// <inheritdoc/>
    public RetryMetadata? DetectRetryMetadata(ITest test)
    {
        if (test?.Method == null)
        {
            return null;
        }

        // Get the MethodInfo from the test
        var methodInfo = GetMethodInfo(test);
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
        if (test?.Properties == null)
        {
            return 1;
        }

        // NUnit stores retry count in test properties
        if (test.Properties.ContainsKey("_RETRY_COUNT"))
        {
            var retryCountObj = test.Properties.Get("_RETRY_COUNT");
            if (retryCountObj != null && int.TryParse(retryCountObj.ToString(), out var retryCount))
            {
                // _RETRY_COUNT is 0-indexed, so add 1 for attempt number
                return retryCount + 1;
            }
        }

        // Try to extract from test name (NUnit appends retry info to test name)
        var attemptFromName = GetAttemptNumberFromTestName(test.Name);
        if (attemptFromName > 1)
        {
            return attemptFromName;
        }

        return 1;
    }

    private static MethodInfo? GetMethodInfo(ITest test)
    {
        try
        {
            // NUnit's ITest.Method provides MethodInfo
            if (test.Method?.MethodInfo != null)
            {
                return test.Method.MethodInfo;
            }

            // Fallback: try to get via reflection
            if (test.TypeInfo != null && test.Method != null)
            {
                var type = test.TypeInfo.Type;
                var method = type.GetMethod(
                    test.Method.Name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                return method;
            }

            return null;
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

            if (RetryAttributeRegistry.IsRegisteredForFramework("nunit", attrName))
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

        // Extract MaxRetries property (NUnit Retry uses constructor parameter)
        // Try to get via properties
        var tryCountProperty = attrType.GetProperty("TryCount", BindingFlags.Public | BindingFlags.Instance);
        if (tryCountProperty != null)
        {
            var value = tryCountProperty.GetValue(retryAttribute);
            if (value is int tryCount)
            {
                metadata.MaxRetries = tryCount;
            }
        }

        // Fallback: try Count property for custom retry attributes
        if (metadata.MaxRetries == 0)
        {
            var countProperty = attrType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
            if (countProperty != null)
            {
                var value = countProperty.GetValue(retryAttribute);
                if (value is int count)
                {
                    metadata.MaxRetries = count;
                }
            }
        }

        // Last resort: try accessing private field (NUnit stores constructor param as private field)
        if (metadata.MaxRetries == 0)
        {
            var tryCountField = attrType.GetField("_tryCount", BindingFlags.NonPublic | BindingFlags.Instance);
            if (tryCountField != null)
            {
                var value = tryCountField.GetValue(retryAttribute);
                if (value is int tryCount)
                {
                    metadata.MaxRetries = tryCount;
                }
            }
        }

        // Get current attempt number
        metadata.AttemptNumber = GetAttemptNumberFromTest(test);

        // Determine if passed on retry
        metadata.PassedOnRetry = metadata.AttemptNumber > 1;

        // Store additional metadata from other properties
        ExtractAdditionalMetadata(retryAttribute, metadata);

        return metadata;
    }

    private static int GetAttemptNumberFromTest(ITest test)
    {
        if (test?.Properties == null)
        {
            return 1;
        }

        // Check for retry count in properties
        if (test.Properties.ContainsKey("_RETRY_COUNT"))
        {
            var retryCountObj = test.Properties.Get("_RETRY_COUNT");
            if (retryCountObj != null && int.TryParse(retryCountObj.ToString(), out var retryCount))
            {
                return retryCount + 1;
            }
        }

        // Try to extract from test name
        return GetAttemptNumberFromTestName(test.Name);
    }

    private static int GetAttemptNumberFromTestName(string testName)
    {
        if (string.IsNullOrEmpty(testName))
        {
            return 1;
        }

        // NUnit may append retry info like: TestName (Retry 2)
        var retryIndex = testName.IndexOf("(retry", StringComparison.OrdinalIgnoreCase);
        if (retryIndex >= 0)
        {
            var endIndex = testName.IndexOf(')', retryIndex);
            if (endIndex > retryIndex)
            {
                // Extract text between "(retry" and ")"
                var retryText = testName.Substring(retryIndex + 6, endIndex - (retryIndex + 6)).Trim();
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
            "OnlyOnce",
            "Reason",
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
