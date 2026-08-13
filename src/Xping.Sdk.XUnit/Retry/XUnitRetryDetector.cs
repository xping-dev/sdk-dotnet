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
internal sealed class XUnitRetryDetector : IXUnitRetryDetector
{
    private static readonly string[] _attemptSeparators = ["(attempt", ")"];

    /// <inheritdoc/>
    RetryMetadata? IRetryDetector<ITest>.DetectRetryMetadata(ITest test, TestOutcome testOutcome) =>
        Detect(test, testOutcome, attemptNumber: null);

    /// <inheritdoc/>
    RetryMetadata? IXUnitRetryDetector.DetectRetryMetadata(ITest test, TestOutcome testOutcome, int attemptNumber) =>
        Detect(test, testOutcome, attemptNumber);

    private static RetryMetadata? Detect(ITest test, TestOutcome testOutcome, int? attemptNumber)
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

        return ExtractRetryMetadata(retryAttribute, test, testOutcome, attemptNumber);
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
            // The raw type name is passed through: the registry treats the "Attribute" suffix as
            // optional, so stripping it here would make suffixed entries unreachable.
            if (RetryAttributeRegistry.IsRegisteredForFramework("xunit", attr.GetType().Name))
            {
                return attr as Attribute;
            }
        }

        return null;
    }

    private static RetryMetadata ExtractRetryMetadata(
        Attribute retryAttribute,
        ITest test,
        TestOutcome testOutcome,
        int? knownAttemptNumber)
    {
        RetryMetadataBuilder builder = new();
        Type attrType = retryAttribute.GetType();

        // Extract the configured retry count. The value is recorded exactly as the attribute declares
        // it: libraries disagree on whether it counts total attempts (xRetry's MaxRetries) or retries
        // excluding the first, and guessing which one applies would corrupt the record either way.
        if (TryReadInt(retryAttribute, out int maxRetries, "MaxRetries", "Count"))
        {
            builder.WithMaxRetries(maxRetries);
        }

        // Extract the configured delay between attempts if available
        if (TryReadInt(retryAttribute, out int delayMs, "DelayMilliseconds", "DelayBetweenRetriesMs", "Delay"))
        {
            builder.WithDelayBetweenRetries(TimeSpan.FromMilliseconds(delayMs));
        }

        // Extract Reason if available
        if (TryReadMember(retryAttribute, out object? reasonValue, "Reason", "RetryReason") &&
            reasonValue is string reason &&
            !string.IsNullOrWhiteSpace(reason))
        {
            builder.WithRetryReason(reason);
        }

        // Use the attempt number the caller established when it drove the retry loop itself,
        // and fall back to inference only when nothing observed the attempts.
        int attemptNumber = knownAttemptNumber ?? GetCurrentAttemptNumber(test);

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

        // Look for common retry-related members
        string[] membersToCheck =
        [
            "ExceptionTypes",
            "Filter",
            "OnlyRetryOn",
            "Skip",
            "SkipOnExceptions",
            "Timeout"
        ];

        foreach (string memberName in membersToCheck)
        {
            if (TryReadMember(retryAttribute, out object? value, memberName) && value != null)
            {
                additionalMetadata[memberName] = value.ToString() ?? string.Empty;
            }
        }

        return additionalMetadata;
    }

    /// <summary>
    /// Reads the first of the named members that exists on the attribute and holds an <see cref="int"/>.
    /// </summary>
    private static bool TryReadInt(Attribute retryAttribute, out int value, params string[] memberNames)
    {
        if (TryReadMember(retryAttribute, out object? raw, memberNames) && raw is int intValue)
        {
            value = intValue;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Reads the first of the named members that exists on the attribute, checking properties and fields.
    /// </summary>
    /// <remarks>
    /// Fields are checked as well as properties because retry attributes commonly declare their
    /// configuration as public readonly fields — xRetry's <c>RetryFactAttribute.MaxRetries</c> and
    /// <c>DelayBetweenRetriesMs</c> among them — which a property-only lookup silently misses.
    /// </remarks>
    private static bool TryReadMember(Attribute retryAttribute, out object? value, params string[] memberNames)
    {
        Type attrType = retryAttribute.GetType();

        foreach (string memberName in memberNames)
        {
            try
            {
                PropertyInfo? property = attrType.GetProperty(memberName);
                if (property != null && property.CanRead)
                {
                    value = property.GetValue(retryAttribute);
                    return true;
                }

                FieldInfo? field = attrType.GetField(memberName);
                if (field != null)
                {
                    value = field.GetValue(retryAttribute);
                    return true;
                }
            }
            catch
            {
                // Ignore member access errors and continue with the next candidate name
            }
        }

        value = null;
        return false;
    }
}
