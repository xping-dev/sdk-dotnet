/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Retry;
using Xunit.v3;

namespace Xping.Sdk.XUnit.V3.Retry;

/// <summary>
/// Detects retry attributes on xUnit v3 test methods, such as xRetry.v3's <c>[RetryFact]</c> and
/// <c>[RetryTheory]</c>.
/// </summary>
/// <remarks>
/// xUnit has no native retry support, so the attribute is found by name through
/// <see cref="RetryAttributeRegistry"/> and read by reflection, without a dependency on any library.
/// </remarks>
[SuppressMessage("Performance", "CA1812", Justification = "Instantiated by the DI container.")]
internal sealed class XUnitRetryDetector : IXUnitRetryDetector
{
    // Keyed by method: every row and every attempt of a test asks about the same one, and the
    // attributes a method declares cannot change.
    private readonly ConcurrentDictionary<MethodInfo, Attribute?> _retryAttributes = new();

    /// <inheritdoc/>
    RetryMetadata? IRetryDetector<IXunitTestCase>.DetectRetryMetadata(IXunitTestCase test, TestOutcome testOutcome) =>
        Detect(test, testOutcome, attemptNumber: 1);

    /// <inheritdoc/>
    RetryMetadata? IXUnitRetryDetector.DetectRetryMetadata(IXunitTestCase testCase, TestOutcome testOutcome, int attemptNumber) =>
        Detect(testCase, testOutcome, attemptNumber);

    private RetryMetadata? Detect(IXunitTestCase testCase, TestOutcome testOutcome, int attemptNumber)
    {
        MethodInfo? method = testCase?.TestMethod?.Method;
        if (method == null)
            return null;

        Attribute? retryAttribute = _retryAttributes.GetOrAdd(method, FindRetryAttribute);
        if (retryAttribute == null)
            return null;

        RetryMetadataBuilder builder = new();

        // Recorded exactly as the attribute declares it: libraries disagree on whether it counts total
        // attempts (xRetry's MaxRetries) or retries after the first, and guessing would corrupt it.
        if (TryReadMember(retryAttribute, out object? maxRetries, "MaxRetries", "Count") && maxRetries is int max)
            builder.WithMaxRetries(max);

        if (TryReadMember(retryAttribute, out object? delay, "DelayBetweenRetriesMs", "DelayMilliseconds", "Delay") && delay is int delayMs)
            builder.WithDelayBetweenRetries(TimeSpan.FromMilliseconds(delayMs));

        if (TryReadMember(retryAttribute, out object? reasonValue, "Reason", "RetryReason") &&
            reasonValue is string reason &&
            !string.IsNullOrWhiteSpace(reason))
        {
            builder.WithRetryReason(reason);
        }

        return builder
            .WithRetryAttributeName(retryAttribute.GetType().Name.Replace("Attribute", ""))
            .WithAttemptNumber(attemptNumber)
            .WithPassedOnRetry(attemptNumber > 1 && testOutcome == TestOutcome.Passed)
            .Build();
    }

    private static Attribute? FindRetryAttribute(MethodInfo method)
    {
        try
        {
            foreach (Attribute attribute in method.GetCustomAttributes(inherit: true).OfType<Attribute>())
            {
                // The raw type name is passed through: the registry treats the "Attribute" suffix as
                // optional, so stripping it here would make suffixed entries unreachable.
                if (RetryAttributeRegistry.IsRegisteredForFramework("xunit", attribute.GetType().Name))
                    return attribute;
            }
        }
        catch
        {
            // An attribute that cannot be constructed is not one to read retry settings from.
        }

        return null;
    }

    /// <summary>
    /// Reads the first of the named members that exists on the attribute, checking properties and fields.
    /// </summary>
    /// <remarks>
    /// Fields as well as properties: xRetry declares <c>MaxRetries</c> and <c>DelayBetweenRetriesMs</c>
    /// as public readonly fields, which a property-only lookup misses.
    /// </remarks>
    private static bool TryReadMember(Attribute attribute, out object? value, params string[] memberNames)
    {
        Type type = attribute.GetType();

        foreach (string memberName in memberNames)
        {
            try
            {
                PropertyInfo? property = type.GetProperty(memberName);
                if (property is { CanRead: true })
                {
                    value = property.GetValue(attribute);
                    return true;
                }

                FieldInfo? field = type.GetField(memberName);
                if (field != null)
                {
                    value = field.GetValue(attribute);
                    return true;
                }
            }
            catch
            {
                // Try the next candidate name.
            }
        }

        value = null;
        return false;
    }
}
