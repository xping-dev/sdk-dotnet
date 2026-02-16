/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.NUnit.Retry;

#pragma warning disable CA1812, CA1515

namespace Xping.Sdk.NUnit.Tests.Retry;

using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Tests for the internal <see cref="NUnitRetryDetector"/>.
/// Uses <see cref="IRetryDetector{ITest}"/> since DetectRetryMetadata is an explicit interface implementation.
/// </summary>
public sealed class NUnitRetryDetectorTests
{
    private readonly IRetryDetector<ITest> _detector = new NUnitRetryDetector();

    // ---------------------------------------------------------------------------
    // Guard clauses
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectRetryMetadata_WithTestWithoutRetryAttribute_ReturnsNull()
    {
        var test = CreateMockTest(nameof(SampleTestClass.TestMethod));

        var result = _detector.DetectRetryMetadata(test, TestOutcome.Passed);

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // Attribute detection
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectRetryMetadata_WithRetryAttribute_ReturnsMetadata()
    {
        var test = CreateMockTest(nameof(SampleTestClass.TestWithRetry));

        var result = _detector.DetectRetryMetadata(test, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal("Retry", result.RetryAttributeName);
        Assert.Equal(3, result.MaxRetries);
    }

    [Fact]
    public void DetectRetryMetadata_WithCustomRetryAttribute_ReturnsMetadata()
    {
        RetryAttributeRegistry.RegisterCustomRetryAttribute("nunit", "CustomRetry");
        var test = CreateMockTest(nameof(SampleTestClass.TestWithCustomRetry));

        var result = _detector.DetectRetryMetadata(test, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal("CustomRetry", result.RetryAttributeName);
        Assert.Equal(5, result.MaxRetries);
    }

    // ---------------------------------------------------------------------------
    // Attempt number
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectRetryMetadata_FirstAttempt_AttemptNumberIsOne()
    {
        var test = CreateMockTest(nameof(SampleTestClass.TestWithRetry));

        var result = _detector.DetectRetryMetadata(test, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal(1, result.AttemptNumber);
        Assert.False(result.PassedOnRetry);
    }

    // ---------------------------------------------------------------------------
    // PassedOnRetry
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectRetryMetadata_FirstAttempt_PassedOnRetry_IsFalse()
    {
        // AttemptNumber == 1 on first attempt → PassedOnRetry must be false even on pass
        var test = CreateMockTest(nameof(SampleTestClass.TestWithRetry));

        var result = _detector.DetectRetryMetadata(test, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.False(result.PassedOnRetry);
    }

    [Fact]
    public void DetectRetryMetadata_FirstAttempt_PassedOnRetry_IsFalse_WhenFailed()
    {
        var test = CreateMockTest(nameof(SampleTestClass.TestWithRetry));

        var result = _detector.DetectRetryMetadata(test, TestOutcome.Failed);

        Assert.NotNull(result);
        Assert.False(result.PassedOnRetry);
    }

    // ---------------------------------------------------------------------------
    // Additional metadata
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectRetryMetadata_AdditionalMetadata_IsNotNull()
    {
        var test = CreateMockTest(nameof(SampleTestClass.TestWithRetry));

        var result = _detector.DetectRetryMetadata(test, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.NotNull(result.AdditionalMetadata);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static TestMethod CreateMockTest(string methodName)
    {
        var type = typeof(SampleTestClass);
        var method = type.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        if (method == null)
            throw new InvalidOperationException($"Method {methodName} not found on {type.Name}");

        return new TestMethod(new MethodWrapper(type, method)) { Name = methodName };
    }

    // ---------------------------------------------------------------------------
    // Test helper class
    // ---------------------------------------------------------------------------

    private sealed class SampleTestClass
    {
        public static void TestMethod() { }

        [Retry(3)]
        public static void TestWithRetry() { }

        [CustomRetry(Count = 5)]
        public static void TestWithCustomRetry() { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    private sealed class CustomRetryAttribute : Attribute
    {
        public int Count { get; set; }
    }
}
