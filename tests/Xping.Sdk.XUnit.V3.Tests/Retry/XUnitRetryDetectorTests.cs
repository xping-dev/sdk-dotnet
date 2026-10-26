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

    private static IXunitTestCase TestCase(Type type, string method)
    {
        var testMethod = new Mock<IXunitTestMethod>();
        testMethod.Setup(m => m.Method).Returns(type.GetMethod(method)!);

        var testCase = new Mock<IXunitTestCase>();
        testCase.Setup(c => c.TestMethod).Returns(testMethod.Object);
        return testCase.Object;
    }
}
