/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest.Retry;

using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Retry;

/// <summary>
/// Detects retry attributes and metadata for MSTest tests.
/// </summary>
/// <remarks>
/// MSTest does not have native retry support, but community libraries and custom implementations provide retry functionality.
/// This detector uses reflection and TestContext to identify and extract retry metadata.
/// </remarks>
public sealed class MSTestRetryDetector : IRetryDetector<TestContext>
{
    /// <inheritdoc/>
    public RetryMetadata? DetectRetryMetadata(TestContext testContext)
    {
        if (testContext == null)
        {
            return null;
        }

        // Get the test method via reflection
        var methodInfo = GetTestMethod(testContext);
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

        return ExtractRetryMetadata(retryAttribute, testContext);
    }

    /// <inheritdoc/>
    public bool HasRetryAttribute(TestContext testContext)
    {
        return DetectRetryMetadata(testContext) != null;
    }

    /// <inheritdoc/>
    public int GetCurrentAttemptNumber(TestContext testContext)
    {
        if (testContext?.Properties == null)
        {
            return 1;
        }

        // Check for retry attempt in test properties (some retry libraries set this)
        if (testContext.Properties.Contains("RetryAttempt"))
        {
            var attemptObj = testContext.Properties["RetryAttempt"];
            if (attemptObj != null && int.TryParse(attemptObj.ToString(), out var attempt))
            {
                return attempt;
            }
        }

        // Check for retry count property
        if (testContext.Properties.Contains("RetryCount"))
        {
            var countObj = testContext.Properties["RetryCount"];
            if (countObj != null && int.TryParse(countObj.ToString(), out var count))
            {
                // RetryCount is typically 0-indexed
                return count + 1;
            }
        }

        // Try to extract from test name
        var attemptFromName = GetAttemptNumberFromTestName(testContext.TestName);
        if (attemptFromName > 1)
        {
            return attemptFromName;
        }

        return 1;
    }

    private static MethodInfo? GetTestMethod(TestContext testContext)
    {
        try
        {
            // Get the test class type
            var fullClassName = testContext.FullyQualifiedTestClassName;
            if (string.IsNullOrEmpty(fullClassName))
            {
                return null;
            }

            // Find the type in loaded assemblies
            var type = Type.GetType(fullClassName);
            if (type == null)
            {
                // Try to find in all loaded assemblies
                type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try
                        {
                            return a.GetTypes();
                        }
                        catch
                        {
                            return Array.Empty<Type>();
                        }
                    })
                    .FirstOrDefault(t => t.FullName == fullClassName);
            }

            if (type == null)
            {
                return null;
            }

            // Get the test method
            var testName = testContext.TestName;
            if (string.IsNullOrEmpty(testName))
            {
                return null;
            }

            // For data-driven tests, test name may include parameters in parentheses
            var methodName = testName!;
            var parenIndex = methodName.IndexOf('(');
            if (parenIndex > 0)
            {
                methodName = methodName.Substring(0, parenIndex).Trim();
            }

            var method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            return method;
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

            if (RetryAttributeRegistry.IsRegisteredForFramework("mstest", attrName))
            {
                return attr as Attribute;
            }
        }

        return null;
    }

    private static RetryMetadata ExtractRetryMetadata(Attribute retryAttribute, TestContext testContext)
    {
        var attrType = retryAttribute.GetType();
        var metadata = new RetryMetadata
        {
            RetryAttributeName = attrType.Name.Replace("Attribute", ""),
            AttemptNumber = 1, // Will be updated during execution
        };

        // Extract MaxRetries property (common names: Count, MaxRetries, RetryCount)
        var maxRetriesProperty = attrType.GetProperty("MaxRetries", BindingFlags.Public | BindingFlags.Instance)
            ?? attrType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance)
            ?? attrType.GetProperty("RetryCount", BindingFlags.Public | BindingFlags.Instance);

        if (maxRetriesProperty != null)
        {
            var value = maxRetriesProperty.GetValue(retryAttribute);
            if (value is int maxRetries)
            {
                metadata.MaxRetries = maxRetries;
            }
        }

        // Extract Delay property if available
        var delayProperty = attrType.GetProperty("DelayMilliseconds", BindingFlags.Public | BindingFlags.Instance)
            ?? attrType.GetProperty("Delay", BindingFlags.Public | BindingFlags.Instance);

        if (delayProperty != null)
        {
            var value = delayProperty.GetValue(retryAttribute);
            if (value is int delayMs)
            {
                metadata.DelayBetweenRetries = TimeSpan.FromMilliseconds(delayMs);
            }
        }

        // Extract Reason property if available
        var reasonProperty = attrType.GetProperty("Reason", BindingFlags.Public | BindingFlags.Instance)
            ?? attrType.GetProperty("RetryReason", BindingFlags.Public | BindingFlags.Instance);

        if (reasonProperty != null)
        {
            var value = reasonProperty.GetValue(retryAttribute);
            if (value is string reason && !string.IsNullOrWhiteSpace(reason))
            {
                metadata.RetryReason = reason;
            }
        }

        // Get current attempt number from test context
        var attemptNumber = GetAttemptNumberFromTestContext(testContext);
        metadata.AttemptNumber = attemptNumber;

        // Determine if passed on retry
        metadata.PassedOnRetry = attemptNumber > 1;

        // Store additional metadata from other properties
        ExtractAdditionalMetadata(retryAttribute, metadata);

        return metadata;
    }

    private static int GetAttemptNumberFromTestContext(TestContext testContext)
    {
        if (testContext?.Properties == null)
        {
            return 1;
        }

        // Check for retry attempt in test properties
        if (testContext.Properties.Contains("RetryAttempt"))
        {
            var attemptObj = testContext.Properties["RetryAttempt"];
            if (attemptObj != null && int.TryParse(attemptObj.ToString(), out var attempt))
            {
                return attempt;
            }
        }

        // Check for retry count property
        if (testContext.Properties.Contains("RetryCount"))
        {
            var countObj = testContext.Properties["RetryCount"];
            if (countObj != null && int.TryParse(countObj.ToString(), out var count))
            {
                return count + 1;
            }
        }

        // Try to extract from test name
        return GetAttemptNumberFromTestName(testContext.TestName);
    }

    private static int GetAttemptNumberFromTestName(string? testName)
    {
        if (string.IsNullOrEmpty(testName))
        {
            return 1;
        }

        // Some retry libraries append retry info like: TestName (Retry 2)
        var retryIndex = testName!.IndexOf("(retry", StringComparison.OrdinalIgnoreCase);
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

        // Some libraries use format: TestName [Attempt 2]
        var attemptIndex = testName.IndexOf("[attempt", StringComparison.OrdinalIgnoreCase);
        if (attemptIndex >= 0)
        {
            var endIndex = testName.IndexOf(']', attemptIndex);
            if (endIndex > attemptIndex)
            {
                var attemptText = testName.Substring(attemptIndex + 8, endIndex - (attemptIndex + 8)).Trim();
                if (int.TryParse(attemptText, out var attempt))
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
            var property = attrType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
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
