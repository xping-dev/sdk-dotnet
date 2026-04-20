/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.MSTest.Tests;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core.Models;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Tests for XpingTestBase outcome mapping and metadata extraction.
/// </summary>
public class XpingTestBaseTests
{
    [Fact]
    public void MapOutcome_Passed_ReturnsPassed()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.Passed);

        Assert.Equal(TestOutcome.Passed, outcome);
    }

    [Fact]
    public void MapOutcome_Failed_ReturnsFailed()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.Failed);

        Assert.Equal(TestOutcome.Failed, outcome);
    }

    [Fact]
    public void MapOutcome_Inconclusive_ReturnsInconclusive()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.Inconclusive);

        Assert.Equal(TestOutcome.Inconclusive, outcome);
    }

    [Fact]
    public void MapOutcome_Timeout_ReturnsFailed()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.Timeout);

        Assert.Equal(TestOutcome.Failed, outcome);
    }

    [Fact]
    public void MapOutcome_Aborted_ReturnsFailed()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.Aborted);

        Assert.Equal(TestOutcome.Failed, outcome);
    }

    [Fact]
    public void MapOutcome_Unknown_ReturnsNotExecuted()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.Unknown);

        Assert.Equal(TestOutcome.NotExecuted, outcome);
    }

    [Fact]
    public void MapOutcome_NotRunnable_ReturnsNotExecuted()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.NotRunnable);

        Assert.Equal(TestOutcome.NotExecuted, outcome);
    }

    [Fact]
    public void MapOutcome_InProgress_ReturnsNotExecuted()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.InProgress);

        Assert.Equal(TestOutcome.NotExecuted, outcome);
    }

    [Fact]
    public void ResolveStackTrace_CaptureStackTracesDisabled_FailedTestWithStackTrace_SetsOmittedTrue()
    {
        var result = InvokeResolveStackTrace(TestOutcome.Failed, "  at Method()", false);

        Assert.Null(result.stackTrace);
        Assert.True(result.stackTraceOmitted);
    }

    [Fact]
    public void ResolveStackTrace_CaptureStackTracesEnabled_FailedTest_PreservesStackTrace()
    {
        var result = InvokeResolveStackTrace(TestOutcome.Failed, "  at Method()", true);

        Assert.Equal("  at Method()", result.stackTrace);
        Assert.False(result.stackTraceOmitted);
    }

    [Fact]
    public void ResolveStackTrace_CaptureStackTracesDisabled_PassedTest_DoesNotMarkOmitted()
    {
        var result = InvokeResolveStackTrace(TestOutcome.Passed, null, false);

        Assert.Null(result.stackTrace);
        Assert.False(result.stackTraceOmitted);
    }

    // Helper method to invoke private MapOutcome using reflection
    private static TestOutcome InvokeMapOutcome(UnitTestOutcome outcome)
    {
        var method = typeof(XpingTestBase).GetMethod(
            "MapOutcome",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
        {
            throw new InvalidOperationException("MapOutcome method not found");
        }

        var result = method.Invoke(null, new object[] { outcome });
        return (TestOutcome)result!;
    }

    private static (string? stackTrace, bool stackTraceOmitted) InvokeResolveStackTrace(
        TestOutcome outcome,
        string? stackTrace,
        bool captureStackTraces)
    {
        var method = typeof(XpingTestBase).GetMethod(
            "ResolveStackTrace",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
        {
            throw new InvalidOperationException("ResolveStackTrace method not found");
        }

        var result = method.Invoke(null, new object?[] { outcome, stackTrace, captureStackTraces });
        return Assert.IsType<(string?, bool)>(result);
    }
}
