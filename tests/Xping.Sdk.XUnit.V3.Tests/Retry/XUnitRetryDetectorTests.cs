/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Moq;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.XUnit.V3.Probe;
using Xping.Sdk.XUnit.V3.Retry;
using Xunit.v3;

namespace Xping.Sdk.XUnit.V3.Tests.Retry;

public sealed class XUnitRetryDetectorTests
{
    private readonly XUnitRetryDetector _detector = new();

    [Fact]
    public void ATestWithoutARetryAttribute_HasNoRetryMetadata()
    {
        IRetryDetector<IXunitTestCase> detector = _detector;

        Assert.Null(detector.DetectRetryMetadata(TestCase(typeof(OutcomeTests), nameof(OutcomeTests.Passes)), TestOutcome.Passed));
    }

    [Fact]
    public void XRetrysAttribute_IsReadForItsSettings()
    {
        IRetryDetector<IXunitTestCase> detector = _detector;

        RetryMetadata? metadata = detector.DetectRetryMetadata(
            TestCase(typeof(RetryTests), nameof(RetryTests.PassesOnItsSecondAttempt)), TestOutcome.Passed);

        Assert.NotNull(metadata);
        Assert.Equal("RetryFact", metadata.RetryAttributeName);
        Assert.Equal(3, metadata.MaxRetries);
        Assert.Equal(TimeSpan.Zero, metadata.DelayBetweenRetries);
    }

    /// <summary>
    /// With no announcement to say otherwise, the result is taken to be the first attempt, which
    /// cannot have passed on a retry.
    /// </summary>
    [Fact]
    public void WithoutAKnownAttempt_TheResultIsTheFirstAttempt()
    {
        IRetryDetector<IXunitTestCase> detector = _detector;

        RetryMetadata? metadata = detector.DetectRetryMetadata(
            TestCase(typeof(RetryTests), nameof(RetryTests.PassesOnItsSecondAttempt)), TestOutcome.Passed);

        Assert.Equal(1, metadata!.AttemptNumber);
        Assert.False(metadata.PassedOnRetry);
    }

    [Theory]
    [InlineData(TestOutcome.Passed, true)]
    [InlineData(TestOutcome.Failed, false)]
    public void AKnownLaterAttempt_PassedOnRetryOnlyWhenItPassed(TestOutcome outcome, bool passedOnRetry)
    {
        IXUnitRetryDetector detector = _detector;

        RetryMetadata? metadata = detector.DetectRetryMetadata(
            TestCase(typeof(RetryTests), nameof(RetryTests.PassesOnItsSecondAttempt)), outcome, attemptNumber: 2);

        Assert.Equal(2, metadata!.AttemptNumber);
        Assert.Equal(passedOnRetry, metadata.PassedOnRetry);
    }

    [Fact]
    public void ATestCaseWithoutAMethod_HasNoRetryMetadata()
    {
        IRetryDetector<IXunitTestCase> detector = _detector;

        Assert.Null(detector.DetectRetryMetadata(Mock.Of<IXunitTestCase>(), TestOutcome.Passed));
    }

    /// <summary>
    /// Another library's attribute, with its settings under other names and a member that throws when
    /// read, which is passed over for the next name.
    /// </summary>
    [Fact]
    public void AnotherLibrarysAttribute_IsReadUnderItsOwnNames()
    {
        IRetryDetector<IXunitTestCase> detector = _detector;

        RetryMetadata? metadata = detector.DetectRetryMetadata(
            TestCase(typeof(Holder), nameof(Holder.RetriedByAnotherLibrary)), TestOutcome.Failed);

        Assert.NotNull(metadata);
        Assert.Equal("Retry", metadata.RetryAttributeName);
        Assert.Equal(4, metadata.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(50), metadata.DelayBetweenRetries);
        Assert.Equal("talks to a slow service", metadata.RetryReason);
    }

    [Fact]
    public void AnAttributeThatCannotBeConstructed_IsNoRetryAttribute()
    {
        IRetryDetector<IXunitTestCase> detector = _detector;

        Assert.Null(detector.DetectRetryMetadata(TestCase(typeof(Holder), nameof(Holder.HasABrokenAttribute)), TestOutcome.Passed));
    }

    private static IXunitTestCase TestCase(Type type, string method)
    {
        var testMethod = new Mock<IXunitTestMethod>();
        testMethod.Setup(m => m.Method).Returns(type.GetMethod(method)!);

        var testCase = new Mock<IXunitTestCase>();
        testCase.Setup(c => c.TestMethod).Returns(testMethod.Object);
        return testCase.Object;
    }
}

#pragma warning disable CA1019, CA1813 // Shaped like a retry library's attribute rather than to the design guidelines.
/// <summary>Named as a registered retry attribute, with settings named as another library's would be.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class RetryAttribute : Attribute
{
    public int MaxRetries => throw new NotSupportedException("read through Count instead");

    public int Count { get; } = 4;

    public int DelayMilliseconds { get; } = 50;

    public string RetryReason { get; } = "talks to a slow service";
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class BrokenAttribute : Attribute
{
    public BrokenAttribute() => throw new InvalidOperationException("the attribute cannot be constructed");
}
#pragma warning restore CA1019, CA1813

internal static class Holder
{
    [Retry]
    public static void RetriedByAnotherLibrary()
    {
    }

    [Broken]
    public static void HasABrokenAttribute()
    {
    }
}
