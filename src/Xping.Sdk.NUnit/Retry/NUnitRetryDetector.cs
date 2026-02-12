/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using NUnit.Framework.Interfaces;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Retry;

namespace Xping.Sdk.NUnit.Retry;

/// <summary>
/// Detects retry attributes and metadata for NUnit tests.
/// </summary>
/// <remarks>
/// NUnit has native retry support via the [Retry] attribute.
/// This detector uses reflection to identify and extract metadata from retry attributes.
/// </remarks>
[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by the DI container.")]
internal sealed class NUnitRetryDetector : IRetryDetector<ITest>
{
    /// <inheritdoc/>
    RetryMetadata? IRetryDetector<ITest>.DetectRetryMetadata(ITest test, TestOutcome testOutcom)
    {
        if (test.Method == null)
        {
            return null;
        }

        // Get the MethodInfo from the test
        MethodInfo? methodInfo = GetMethodInfo(test);
        if (methodInfo == null)
        {
            return null;
        }

        // Look for retry attributes
        Attribute? retryAttribute = FindRetryAttribute(methodInfo);
        if (retryAttribute == null)
        {
            return null;
        }

        return ExtractRetryMetadata(retryAttribute, test, testOutcom);
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
            if (test is { TypeInfo: not null, Method: not null })
            {
                Type type = test.TypeInfo.Type;
                MethodInfo? method = type.GetMethod(
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
        object[] attributes = methodInfo.GetCustomAttributes(true);

        // Look for known retry attributes
        foreach (object attr in attributes)
        {
            Type attrType = attr.GetType();
            string attrName = attrType.Name.Replace("Attribute", "");

            if (RetryAttributeRegistry.IsRegisteredForFramework("nunit", attrName))
            {
                return attr as Attribute;
            }
        }

        return null;
    }

    private static RetryMetadata ExtractRetryMetadata(Attribute retryAttribute, ITest test, TestOutcome testOutcome)
    {
        RetryMetadataBuilder builder = new();
        Type attrType = retryAttribute.GetType();

        // Extract MaxRetries property (NUnit Retry uses constructor parameter)
        // Try to get via properties
        PropertyInfo? tryCountProperty = attrType.GetProperty("TryCount", BindingFlags.Public | BindingFlags.Instance);
        if (tryCountProperty != null)
        {
            object? value = tryCountProperty.GetValue(retryAttribute);
            if (value is int tryCount)
            {
                builder.WithMaxRetries(tryCount);
            }
        }
        else // Fallback: try Count property for custom retry attributes
        {
            PropertyInfo? countProperty = attrType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
            if (countProperty != null)
            {
                object? value = countProperty.GetValue(retryAttribute);
                if (value is int count)
                {
                    builder.WithMaxRetries(count);
                }
            }
            else // Last resort: try accessing a private field (NUnit stores constructor param as private field)
            {
                FieldInfo? tryCountField =
                    attrType.GetField("_tryCount", BindingFlags.NonPublic | BindingFlags.Instance);
                if (tryCountField != null)
                {
                    object? value = tryCountField.GetValue(retryAttribute);
                    if (value is int tryCount)
                    {
                        builder.WithMaxRetries(tryCount);
                    }
                }
            }
        }

        // Get the current attempt number
        int attemptNumber = GetCurrentAttemptNumber(test);

        RetryMetadata metadata = builder
            .Reset()
            .WithRetryAttributeName(attrType.Name.Replace("Attribute", ""))
            .WithAttemptNumber(attemptNumber)
            .WithPassedOnRetry(attemptNumber > 1 && testOutcome == TestOutcome.Passed)
            .AddMetadata(ExtractAdditionalMetadata(retryAttribute)) // Store additional metadata from other properties
            .Build();

        return metadata;
    }

    private static int GetCurrentAttemptNumber(ITest test)
    {
        if (test.Properties.Keys.Count == 0)
        {
            return 1;
        }

        // NUnit stores retry count in test properties
        if (test.Properties.ContainsKey("_RETRY_COUNT"))
        {
            object? retryCountObj = test.Properties.Get("_RETRY_COUNT");
            if (retryCountObj != null && int.TryParse(retryCountObj.ToString(), out int retryCount))
            {
                // _RETRY_COUNT is 0-indexed, so add 1 for the attempt number
                return retryCount + 1;
            }
        }

        // Try to extract from the test name (NUnit appends retry info to test name)
        int attemptFromName = GetAttemptNumberFromTestName(test.Name);
        if (attemptFromName > 1)
        {
            return attemptFromName;
        }

        return 1;
    }

    private static int GetAttemptNumberFromTestName(string testName)
    {
        if (string.IsNullOrEmpty(testName))
        {
            return 1;
        }

        // NUnit may append retry info like: TestName (Retry 2)
        int retryIndex = testName.IndexOf("(retry", StringComparison.OrdinalIgnoreCase);
        if (retryIndex >= 0)
        {
            int endIndex = testName.IndexOf(')', retryIndex);
            if (endIndex > retryIndex)
            {
                // Extract text between "(retry" and ")"
                string retryText = testName.Substring(retryIndex + 6, endIndex - (retryIndex + 6)).Trim();
                if (int.TryParse(retryText, out int attempt))
                {
                    return attempt;
                }
            }
        }

        return 1;
    }

    private static Dictionary<string, string> ExtractAdditionalMetadata(Attribute retryAttribute)
    {
        Dictionary<string, string> additionalMetadata = [];
        Type attrType = retryAttribute.GetType();

        // Look for common retry-related properties
        string[] propertiesToCheck = ["OnlyOnce", "Reason", "Skip", "Timeout"];

        foreach (string propertyName in propertiesToCheck)
        {
            PropertyInfo? property = attrType.GetProperty(propertyName);
            if (property != null)
            {
                try
                {
                    object? value = property.GetValue(retryAttribute);
                    if (value != null)
                    {
                        additionalMetadata[propertyName] = value.ToString() ?? string.Empty;
                    }
                }
                catch
                {
                    // Ignore property access errors
                }
            }
        }

        return additionalMetadata;
    }
}
