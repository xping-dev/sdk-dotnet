/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Retry;

namespace Xping.Sdk.MSTest.Retry;

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
    RetryMetadata? IRetryDetector<TestContext>.DetectRetryMetadata(TestContext testContext, TestOutcome testOutcome)
    {
        if (testContext == null)
            return null;

        // Get the test method via reflection
        MethodInfo? methodInfo = GetTestMethod(testContext);
        if (methodInfo == null)
            return null;

        // Look for retry attributes
        Attribute? retryAttribute = FindRetryAttribute(methodInfo);
        if (retryAttribute == null)
            return null;

        return ExtractRetryMetadata(retryAttribute, testContext, testOutcome);
    }

    private static MethodInfo? GetTestMethod(TestContext testContext)
    {
        try
        {
            var fullClassName = testContext.FullyQualifiedTestClassName;
            if (string.IsNullOrEmpty(fullClassName))
                return null;

            // Find the type in loaded assemblies
            Type? type = Type.GetType(fullClassName);
            if (type == null)
            {
                type = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Array.Empty<Type>(); }
                    })
                    .FirstOrDefault(t => t.FullName == fullClassName);
            }

            if (type == null)
                return null;

            var testName = testContext.TestName;
            if (string.IsNullOrEmpty(testName))
                return null;

            // For data-driven tests, the test name may include parameters in parentheses
            var methodName = testName!;
            var parenIndex = methodName.IndexOf('(');
            if (parenIndex > 0)
                methodName = methodName.Substring(0, parenIndex).Trim();

            return type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }
        catch
        {
            return null;
        }
    }

    private static Attribute? FindRetryAttribute(MethodInfo methodInfo)
    {
        object[] attributes = methodInfo.GetCustomAttributes(true);

        foreach (object attr in attributes)
        {
            Type attrType = attr.GetType();
            string attrName = attrType.Name.Replace("Attribute", "");

            if (RetryAttributeRegistry.IsRegisteredForFramework("mstest", attrName))
                return attr as Attribute;
        }

        return null;
    }

    private static RetryMetadata ExtractRetryMetadata(
        Attribute retryAttribute,
        TestContext testContext,
        TestOutcome testOutcome)
    {
        Type attrType = retryAttribute.GetType();

        // Extract MaxRetries property (common names: MaxRetries, Count, RetryCount)
        PropertyInfo? maxRetriesProperty =
            attrType.GetProperty("MaxRetries", BindingFlags.Public | BindingFlags.Instance)
            ?? attrType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance)
            ?? attrType.GetProperty("RetryCount", BindingFlags.Public | BindingFlags.Instance);

        int maxRetries = maxRetriesProperty?.GetValue(retryAttribute) is int mr ? mr : 0;

        // Extract Delay property if available
        PropertyInfo? delayProperty =
            attrType.GetProperty("DelayMilliseconds", BindingFlags.Public | BindingFlags.Instance)
            ?? attrType.GetProperty("Delay", BindingFlags.Public | BindingFlags.Instance);

        TimeSpan delay = delayProperty?.GetValue(retryAttribute) is int delayMs
            ? TimeSpan.FromMilliseconds(delayMs)
            : TimeSpan.Zero;

        // Extract Reason property if available
        PropertyInfo? reasonProperty =
            attrType.GetProperty("Reason", BindingFlags.Public | BindingFlags.Instance)
            ?? attrType.GetProperty("RetryReason", BindingFlags.Public | BindingFlags.Instance);

        string? reason = reasonProperty?.GetValue(retryAttribute) is string r && !string.IsNullOrWhiteSpace(r)
            ? r
            : null;

        int attemptNumber = GetAttemptNumber(testContext);

        RetryMetadata metadata = new RetryMetadataBuilder()
            .WithRetryAttributeName(attrType.Name.Replace("Attribute", ""))
            .WithAttemptNumber(attemptNumber)
            .WithMaxRetries(maxRetries)
            .WithDelayBetweenRetries(delay)
            .WithRetryReason(reason ?? string.Empty)
            .WithPassedOnRetry(attemptNumber > 1 && testOutcome == TestOutcome.Passed)
            .AddMetadata(ExtractAdditionalMetadata(retryAttribute))
            .Build();

        return metadata;
    }

    private static int GetAttemptNumber(TestContext testContext)
    {
        if (testContext.Properties.Keys is { Count: 0 })
            return 1;

        // Check for a retry attempt in test properties (some retry libraries set this)
        if (testContext.Properties.Contains("RetryAttempt"))
        {
            var attemptObj = testContext.Properties["RetryAttempt"];
            if (attemptObj != null && int.TryParse(attemptObj.ToString(), out int attempt))
                return attempt;
        }

        // Check for retry count property (0-indexed)
        if (testContext.Properties.Contains("RetryCount"))
        {
            var countObj = testContext.Properties["RetryCount"];
            if (countObj != null && int.TryParse(countObj.ToString(), out int count))
                return count + 1;
        }

        // Try to extract from the test name
        return GetAttemptNumberFromTestName(testContext.TestName);
    }

    private static int GetAttemptNumberFromTestName(string? testName)
    {
        if (string.IsNullOrEmpty(testName))
            return 1;

        // Some retry libraries append retry info like: TestName (Retry 2)
        int retryIndex = testName!.IndexOf("(retry", StringComparison.OrdinalIgnoreCase);
        if (retryIndex >= 0)
        {
            int endIndex = testName.IndexOf(')', retryIndex);
            if (endIndex > retryIndex)
            {
                string retryText = testName.Substring(retryIndex + 6, endIndex - (retryIndex + 6)).Trim();
                if (int.TryParse(retryText, out int attempt))
                    return attempt;
            }
        }

        // Some libraries use format: TestName [Attempt 2]
        int attemptIndex = testName.IndexOf("[attempt", StringComparison.OrdinalIgnoreCase);
        if (attemptIndex >= 0)
        {
            int endIndex = testName.IndexOf(']', attemptIndex);
            if (endIndex > attemptIndex)
            {
                string attemptText = testName.Substring(attemptIndex + 8, endIndex - (attemptIndex + 8)).Trim();
                if (int.TryParse(attemptText, out int attempt))
                    return attempt;
            }
        }

        return 1;
    }

    private static Dictionary<string, string> ExtractAdditionalMetadata(Attribute retryAttribute)
    {
        Dictionary<string, string> additionalMetadata = [];
        Type attrType = retryAttribute.GetType();

        string[] propertiesToCheck = ["ExceptionTypes", "Filter", "OnlyRetryOn", "Skip", "Timeout"];

        foreach (string propertyName in propertiesToCheck)
        {
            PropertyInfo? property = attrType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                try
                {
                    object? value = property.GetValue(retryAttribute);
                    if (value != null)
                        additionalMetadata[propertyName] = value.ToString() ?? string.Empty;
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
