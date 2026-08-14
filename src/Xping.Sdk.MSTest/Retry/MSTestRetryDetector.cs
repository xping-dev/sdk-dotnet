/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
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
public sealed class MSTestRetryDetector : IMSTestRetryDetector
{
    /// <summary>
    /// Attempts recorded so far for each test identity, together with the outcome of the most recent
    /// one. Only tests carrying a retry attribute are tracked, so the map stays as small as the retry
    /// surface of the suite. The detector is a DI singleton, which scopes this to the session.
    /// </summary>
    private readonly ConcurrentDictionary<string, AttemptRecord> _attempts = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    RetryMetadata? IRetryDetector<TestContext>.DetectRetryMetadata(TestContext testContext, TestOutcome testOutcome) =>
        Detect(testContext, testOutcome, testFingerprint: null);

    /// <inheritdoc/>
    RetryMetadata? IMSTestRetryDetector.DetectRetryMetadata(
        TestContext testContext,
        TestOutcome testOutcome,
        string testFingerprint) =>
        Detect(testContext, testOutcome, testFingerprint);

    private RetryMetadata? Detect(TestContext testContext, TestOutcome testOutcome, string? testFingerprint)
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

        return ExtractRetryMetadata(retryAttribute, testContext, testOutcome, testFingerprint);
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
            // The runtime type name is passed as-is: the registry treats the "Attribute" suffix as
            // optional, so stripping it here would make suffixed entries such as "RetryAttribute"
            // unreachable — which is exactly what kept [Retry] invisible to this detector.
            if (RetryAttributeRegistry.IsRegisteredForFramework("mstest", attr.GetType().Name))
                return attr as Attribute;
        }

        return null;
    }

    private RetryMetadata ExtractRetryMetadata(
        Attribute retryAttribute,
        TestContext testContext,
        TestOutcome testOutcome,
        string? testFingerprint)
    {
        Type attrType = retryAttribute.GetType();

        // Extract the configured retry count. The value is recorded exactly as the attribute declares
        // it: implementations disagree on whether it counts total attempts or retries excluding the
        // first, and guessing which one applies would corrupt the record either way.
        TryReadInt(retryAttribute, out int maxRetries, "MaxRetries", "Count", "RetryCount");

        // Extract the configured delay between attempts if available
        TimeSpan delay = TryReadInt(retryAttribute, out int delayMs, "DelayMilliseconds", "DelayBetweenRetriesMs", "Delay")
            ? TimeSpan.FromMilliseconds(delayMs)
            : TimeSpan.Zero;

        // Extract Reason if available
        string? reason = TryReadMember(retryAttribute, out object? reasonValue, "Reason", "RetryReason") &&
                         reasonValue is string r &&
                         !string.IsNullOrWhiteSpace(r)
            ? r
            : null;

        int attemptNumber = GetAttemptNumber(testContext, testOutcome, testFingerprint);

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

    private int GetAttemptNumber(TestContext testContext, TestOutcome testOutcome, string? testFingerprint)
    {
        int publishedAttempt = GetPublishedAttemptNumber(testContext);

        if (testFingerprint == null)
        {
            // No identity to count against — all that is left is whatever the retry helper published.
            return publishedAttempt;
        }

        int countedAttempt = CountAttempt(testFingerprint, testOutcome);

        // A retry helper that publishes the attempt number knows better than the count does, so let it
        // win — but never report fewer attempts than have actually been recorded for this test.
        return Math.Max(countedAttempt, publishedAttempt);
    }

    /// <summary>
    /// Counts this execution against the ones already recorded for the same test identity and returns
    /// its 1-based attempt number.
    /// </summary>
    /// <remarks>
    /// A retry only ever follows an attempt that did not pass, so a test identity that passed starts a
    /// fresh chain. That keeps two runs of a genuinely repeated identity — such as a pair of
    /// <c>[DataRow]</c> rows carrying identical values, which share a fingerprint — from being reported
    /// as a retry of one another.
    /// </remarks>
    private int CountAttempt(string testFingerprint, TestOutcome testOutcome)
    {
        AttemptRecord record = _attempts.AddOrUpdate(
            testFingerprint,
            _ => new AttemptRecord(1, testOutcome),
            (_, previous) => previous.Outcome == TestOutcome.Passed
                ? new AttemptRecord(1, testOutcome)
                : new AttemptRecord(previous.AttemptNumber + 1, testOutcome));

        return record.AttemptNumber;
    }

    /// <summary>
    /// Reads an attempt number published by a retry helper, either as a test property or as a marker
    /// in the test name. Returns 1 when nothing published one.
    /// </summary>
    private static int GetPublishedAttemptNumber(TestContext testContext)
    {
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

        string[] membersToCheck = ["ExceptionTypes", "Filter", "OnlyRetryOn", "Skip", "Timeout"];

        foreach (string memberName in membersToCheck)
        {
            if (TryReadMember(retryAttribute, out object? value, memberName) && value != null)
                additionalMetadata[memberName] = value.ToString() ?? string.Empty;
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
    /// configuration as public readonly fields, which a property-only lookup silently misses.
    /// </remarks>
    private static bool TryReadMember(Attribute retryAttribute, out object? value, params string[] memberNames)
    {
        Type attrType = retryAttribute.GetType();

        foreach (string memberName in memberNames)
        {
            try
            {
                PropertyInfo? property = attrType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanRead)
                {
                    value = property.GetValue(retryAttribute);
                    return true;
                }

                FieldInfo? field = attrType.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
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

    /// <summary>
    /// The number of attempts recorded so far for a test identity, and the outcome of the last one.
    /// </summary>
    private sealed class AttemptRecord(int attemptNumber, TestOutcome outcome)
    {
        public int AttemptNumber { get; } = attemptNumber;

        public TestOutcome Outcome { get; } = outcome;
    }
}
