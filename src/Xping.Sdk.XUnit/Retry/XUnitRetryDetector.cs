/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Xping.Sdk.Core.Models.Builders;
using Xunit.Abstractions;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Retry;

namespace Xping.Sdk.XUnit.Retry;

/// <summary>
/// Detects retry attributes and metadata for xUnit tests.
/// </summary>
/// <remarks>
/// XUnit does not have native retry support, but several popular libraries provide retry functionality:
/// - xunit.extensions.retry (RetryFact, RetryTheory)
/// - Custom retry implementations
/// This detector uses reflection to identify and extract metadata from these retry attributes.
/// </remarks>
[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by the DI container.")]
internal sealed class XUnitRetryDetector : IRetryDetector<ITest>
{
    private static readonly string[] _attemptSeparators = ["(attempt", ")"];

    /// <inheritdoc/>
    RetryMetadata? IRetryDetector<ITest>.DetectRetryMetadata(ITest test, TestOutcome testOutcome)
    {
        if (test.TestCase?.TestMethod?.Method == null)
        {
            return null;
        }

        IMethodInfo? method = test.TestCase.TestMethod.Method;

        // Try to get reflection-based method info
        MethodInfo? methodInfo = GetMethodInfo(method);
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

        return ExtractRetryMetadata(retryAttribute, test, testOutcome);
    }

    private static MethodInfo? GetMethodInfo(IMethodInfo method)
    {
        try
        {
            // xUnit's IMethodInfo can be converted to reflection MethodInfo
            string? typeName = method.Type.Name;
            Type? type = Type.GetType(typeName);

            if (type == null)
            {
                // Try to find the type in loaded assemblies
                type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.FullName == typeName);
            }

            MethodInfo? methodInfo = type?.GetMethod(
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
        object[] attributes = methodInfo.GetCustomAttributes(true);

        // Look for known retry attributes
        foreach (object attr in attributes)
        {
            Type attrType = attr.GetType();
            string attrName = attrType.Name.Replace("Attribute", "");

            if (RetryAttributeRegistry.IsRegisteredForFramework("xunit", attrName))
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

        // Extract MaxRetries property (common in retry attributes)
        PropertyInfo? maxRetriesProperty =
            attrType.GetProperty("MaxRetries") ??
            attrType.GetProperty("Count");

        if (maxRetriesProperty != null)
        {
            object? value = maxRetriesProperty.GetValue(retryAttribute);
            if (value is int maxRetries)
            {
                builder.WithMaxRetries(maxRetries);
            }
        }

        // Extract Delay property if available
        PropertyInfo? delayProperty =
            attrType.GetProperty("DelayMilliseconds") ??
            attrType.GetProperty("Delay");

        if (delayProperty != null)
        {
            object? value = delayProperty.GetValue(retryAttribute);
            if (value is int delayMs)
            {
                builder.WithDelayBetweenRetries(TimeSpan.FromMilliseconds(delayMs));
            }
        }

        // Extract Reason property if available
        PropertyInfo? reasonProperty =
            attrType.GetProperty("Reason") ??
            attrType.GetProperty("RetryReason");

        if (reasonProperty != null)
        {
            object? value = reasonProperty.GetValue(retryAttribute);
            if (value is string reason && !string.IsNullOrWhiteSpace(reason))
            {
                builder.WithRetryReason(reason);
            }
        }

        // Get the current attempt number
        int attemptNumber = GetCurrentAttemptNumber(test);

        RetryMetadata metadata = builder
            .WithRetryAttributeName(attrType.Name.Replace("Attribute", ""))
            .WithAttemptNumber(attemptNumber)
            .WithPassedOnRetry(attemptNumber > 1 && test.TestCase != null && testOutcome == TestOutcome.Passed)
            .AddMetadata(ExtractAdditionalMetadata(retryAttribute)) // Store additional metadata from other properties
            .Build();

        return metadata;
    }

    private static int GetCurrentAttemptNumber(ITest test)
    {
        // xUnit doesn't expose retry attempt count directly in ITest
        // We need to look for custom properties or trait data

        if (test.TestCase == null)
        {
            return 1;
        }

        // Check for a retry attempt in traits (some retry libraries set this)
        Dictionary<string, List<string>>? traits = test.TestCase.Traits;
        if (traits.TryGetValue("RetryAttempt", out List<string>? attemptValues))
        {
            if (attemptValues.Count > 0 && int.TryParse(attemptValues.First(), out int attempt))
            {
                return attempt;
            }
        }

        // Try to extract from the display name (some retry libraries append attempt number)
        int attemptFromDisplayName = GetAttemptNumberFromDisplayName(test.DisplayName);
        if (attemptFromDisplayName > 1)
        {
            return attemptFromDisplayName;
        }

        // Default to the first attempt
        return 1;
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
            string[] parts = displayName.Split(_attemptSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                // After split, parts[1] contain the attempt number before the closing ")"
                string attemptStr = parts[1].Trim();
                if (int.TryParse(attemptStr, out int attempt))
                {
                    return attempt;
                }
            }
        }

        // Some libraries use a format: TestName [Retry 2]
        int retryIndex = displayName.IndexOf("[retry", StringComparison.OrdinalIgnoreCase);
        if (retryIndex >= 0)
        {
            int endIndex = displayName.IndexOf(']', retryIndex);
            if (endIndex > retryIndex)
            {
                // Extract text between "[retry" and "]"
                string retryText = displayName
                    .Substring(retryIndex + 6, endIndex - (retryIndex + 6))
                    .Trim();

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
        string[] propertiesToCheck =
        [
            "ExceptionTypes",
            "Filter",
            "OnlyRetryOn",
            "Skip",
            "Timeout"
        ];

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
