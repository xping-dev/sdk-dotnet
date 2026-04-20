/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

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
