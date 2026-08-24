/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics;
using System.Reflection;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.XUnit.Tests;

public sealed class XpingMessageSinkTests
{
    [Fact]
    public void ResolveStackTrace_CaptureStackTracesDisabled_FailedTestWithStackTrace_SetsOmittedTrue()
    {
        (string? stackTrace, bool stackTraceOmitted) = InvokeResolveStackTrace(TestOutcome.Failed, "  at Method()", false);

        Assert.Null(stackTrace);
        Assert.True(stackTraceOmitted);
    }

    [Fact]
    public void ResolveStackTrace_CaptureStackTracesEnabled_FailedTest_PreservesStackTrace()
    {
        (string? stackTrace, bool stackTraceOmitted) = InvokeResolveStackTrace(TestOutcome.Failed, "  at Method()", true);

        Assert.Equal("  at Method()", stackTrace);
        Assert.False(stackTraceOmitted);
    }

    [Fact]
    public void ResolveStackTrace_CaptureStackTracesDisabled_PassedTest_DoesNotMarkOmitted()
    {
        (string? stackTrace, bool stackTraceOmitted) = InvokeResolveStackTrace(TestOutcome.Passed, null, false);

        Assert.Null(stackTrace);
        Assert.False(stackTraceOmitted);
    }

    [Fact]
    public void ResolveStackTrace_CaptureStackTracesEnabled_WhitespaceStackTrace_ReturnsNullAndStackTraceOmittedIsFalse()
    {
        (string? stackTrace, bool stackTraceOmitted) = InvokeResolveStackTrace(TestOutcome.Failed, "   ", true);

        Assert.Null(stackTrace);
        Assert.False(stackTraceOmitted);
    }

    [Fact]
    public void ClassifyFailure_TimeoutExceptionWithTimeoutMessage_ReturnsTimeout()
    {
        TestOutcome outcome = InvokeClassifyFailure(
            ["Xunit.Sdk.TestTimeoutException"],
            "Test execution timed out after 500 milliseconds");

        Assert.Equal(TestOutcome.Timeout, outcome);
    }

    /// <summary>
    /// xUnit applies a timeout only to async tests and fails a synchronous one outright, reusing the
    /// timeout exception to do it. That is a misconfigured test rather than a hanging one, and
    /// calling it a timeout would send the reader looking for a deadlock that does not exist.
    /// </summary>
    [Fact]
    public void ClassifyFailure_TimeoutExceptionOnSynchronousTest_ReturnsFailed()
    {
        TestOutcome outcome = InvokeClassifyFailure(
            ["Xunit.Sdk.TestTimeoutException"],
            "Tests marked with Timeout are only supported for async tests");

        Assert.Equal(TestOutcome.Failed, outcome);
    }

    [Fact]
    public void ClassifyFailure_AssertionException_ReturnsFailed()
    {
        TestOutcome outcome = InvokeClassifyFailure(["Xunit.Sdk.TrueException"], "Assert.True() Failure");

        Assert.Equal(TestOutcome.Failed, outcome);
    }

    /// <summary>
    /// The message alone must not be enough: a test is free to fail with text that reads like the
    /// framework's own.
    /// </summary>
    [Fact]
    public void ClassifyFailure_TimeoutMessageWithoutTimeoutException_ReturnsFailed()
    {
        TestOutcome outcome = InvokeClassifyFailure(
            ["System.InvalidOperationException"],
            "Test execution timed out after 500 milliseconds");

        Assert.Equal(TestOutcome.Failed, outcome);
    }

    [Fact]
    public void ClassifyFailure_NoExceptionTypes_ReturnsFailed()
    {
        TestOutcome outcome = InvokeClassifyFailure(null, "boom");

        Assert.Equal(TestOutcome.Failed, outcome);
    }

    /// <summary>
    /// xUnit never sees a test it abandoned finish, so it reports no execution time for one. Taking
    /// that zero at face value would publish a timed-out test as having run for 0 ms next to a 500 ms
    /// budget — destroying the one comparison the timeout outcome exists to support.
    /// </summary>
    [Fact]
    public void ResolveFailureDuration_Timeout_MeasuresFromTheStartNotification()
    {
        long start = 0;
        long end = Stopwatch.Frequency / 2;

        TimeSpan duration = XpingMessageSink.ResolveFailureDuration(
            TestOutcome.Timeout, reportedExecutionTime: 0m, start, end);

        // A tick or two of rounding is expected converting stopwatch frequency to TimeSpan ticks.
        Assert.InRange(duration.TotalMilliseconds, 499, 501);
    }

    [Fact]
    public void ResolveFailureDuration_OrdinaryFailure_KeepsTheTimeXUnitReported()
    {
        long start = 0;
        long end = Stopwatch.Frequency * 9;

        TimeSpan duration = XpingMessageSink.ResolveFailureDuration(
            TestOutcome.Failed, reportedExecutionTime: 0.25m, start, end);

        Assert.Equal(TimeSpan.FromMilliseconds(250), duration);
    }

    private static TestOutcome InvokeClassifyFailure(string[]? exceptionTypes, string? errorMessage)
    {
        MethodInfo method = typeof(XpingMessageSink).GetMethod(
            "ClassifyFailure",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        object? value = method.Invoke(null, [exceptionTypes, errorMessage]);
        return Assert.IsType<TestOutcome>(value);
    }

    private static (string? stackTrace, bool stackTraceOmitted) InvokeResolveStackTrace(
        TestOutcome outcome,
        string? stackTrace,
        bool captureStackTraces)
    {
        MethodInfo method = typeof(XpingMessageSink).GetMethod(
            "ResolveStackTrace",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        object? value = method.Invoke(null, [outcome, stackTrace, captureStackTraces]);
        return Assert.IsType<(string?, bool)>(value);
    }
}
