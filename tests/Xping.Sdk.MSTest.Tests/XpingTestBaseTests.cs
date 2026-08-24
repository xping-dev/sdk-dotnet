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
    public void MapOutcome_Timeout_ReturnsTimeout()
    {
        var outcome = InvokeMapOutcome(UnitTestOutcome.Timeout);

        Assert.Equal(TestOutcome.Timeout, outcome);
    }

    [Fact]
    public void ResolveTimeoutBudget_MethodWithoutTimeoutAttribute_ReturnsNoBudget()
    {
        var (budget, source) = InvokeResolveTimeoutBudget(nameof(BudgetSamples.NoTimeout));

        Assert.Null(budget);
        Assert.Null(source);
    }

    [Fact]
    public void ResolveTimeoutBudget_MethodWithTimeoutAttribute_ReturnsDeclaredBudget()
    {
        var (budget, source) = InvokeResolveTimeoutBudget(nameof(BudgetSamples.FiveSecondTimeout));

        Assert.Equal(TimeSpan.FromSeconds(5), budget);
        Assert.Equal(TimeoutBudgetSource.Declared, source);
    }

    /// <summary>
    /// An infinite timeout is a declaration, not the absence of one, so it must not look like a test
    /// that said nothing about how long it may run.
    /// </summary>
    [Fact]
    public void ResolveTimeoutBudget_MethodWithInfiniteTimeout_ReturnsInfiniteWithNoBudget()
    {
        var (budget, source) = InvokeResolveTimeoutBudget(nameof(BudgetSamples.InfiniteTimeout));

        Assert.Null(budget);
        Assert.Equal(TimeoutBudgetSource.Infinite, source);
    }

    [Fact]
    public void ResolveTimeoutBudget_NullMethod_ReturnsNoBudget()
    {
        var (budget, source) = InvokeResolveTimeoutBudget(methodName: null);

        Assert.Null(budget);
        Assert.Null(source);
    }

    /// <summary>
    /// The single most important line in the MSTest adapter: MSTest never reports
    /// <c>UnitTestOutcome.Timeout</c> to cleanup, so the cancellation token is the only signal that
    /// separates a test the runner stopped from one that finished on its own.
    /// </summary>
    [Fact]
    public void ResolveOutcome_CancellationRequested_ReturnsTimeout()
    {
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();

        var outcome = InvokeResolveOutcome(UnitTestOutcome.InProgress, cts);

        Assert.Equal(TestOutcome.Timeout, outcome);
    }

    /// <summary>
    /// A cooperatively-cancelled test that ignored its token is reported to cleanup as
    /// <c>Passed</c>, even though the runner goes on to fail it. Reading the outcome alone would
    /// record a hung test as green.
    /// </summary>
    [Fact]
    public void ResolveOutcome_CancellationRequestedButOutcomeSaysPassed_StillReturnsTimeout()
    {
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();

        var outcome = InvokeResolveOutcome(UnitTestOutcome.Passed, cts);

        Assert.Equal(TestOutcome.Timeout, outcome);
    }

    [Theory]
    [InlineData(UnitTestOutcome.Passed, TestOutcome.Passed)]
    [InlineData(UnitTestOutcome.Failed, TestOutcome.Failed)]
    [InlineData(UnitTestOutcome.Inconclusive, TestOutcome.Inconclusive)]
    public void ResolveOutcome_NoCancellation_FallsBackToTheReportedOutcome(
        UnitTestOutcome reported, TestOutcome expected)
    {
        using var cts = new System.Threading.CancellationTokenSource();

        Assert.Equal(expected, InvokeResolveOutcome(reported, cts));
    }

    /// <summary>
    /// A context exposing no cancellation source at all must not be read as a timeout.
    /// </summary>
    [Fact]
    public void ResolveOutcome_NoCancellationSource_FallsBackToTheReportedOutcome()
    {
        Assert.Equal(TestOutcome.Failed, InvokeResolveOutcome(UnitTestOutcome.Failed, cancellation: null));
    }

    /// <summary>
    /// Methods carrying the timeout declarations the budget reader is tested against. They are never
    /// executed; only their attributes are read.
    /// </summary>
    private static class BudgetSamples
    {
        public static void NoTimeout()
        {
        }

        [Timeout(5000)]
        public static void FiveSecondTimeout()
        {
        }

        [Timeout(TestTimeout.Infinite)]
        public static void InfiniteTimeout()
        {
        }
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

    [Fact]
    public void ResolveStackTrace_CaptureStackTracesEnabled_WhitespaceStackTrace_ReturnsNullAndStackTraceOmittedIsFalse()
    {
        var result = InvokeResolveStackTrace(TestOutcome.Failed, "   ", true);

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

    private static TestOutcome InvokeResolveOutcome(
        UnitTestOutcome reported, System.Threading.CancellationTokenSource? cancellation)
    {
        var method = typeof(XpingTestBase).GetMethod(
            "ResolveOutcome",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
        {
            throw new InvalidOperationException("ResolveOutcome method not found");
        }

        var result = method.Invoke(null, new object?[] { new OutcomeTestContext(reported, cancellation) });
        return (TestOutcome)result!;
    }

    /// <summary>
    /// Minimal <see cref="TestContext"/> exposing the two members the outcome resolution reads.
    /// </summary>
    private sealed class OutcomeTestContext(
        UnitTestOutcome outcome, System.Threading.CancellationTokenSource? cancellation) : TestContext
    {
#pragma warning disable CS8609 // Nullability of reference types in return type doesn't match overridden member
        public override System.Collections.IDictionary Properties { get; } = new Dictionary<string, object?>();
#pragma warning restore CS8609

        public override UnitTestOutcome CurrentTestOutcome => outcome;

        public override System.Threading.CancellationTokenSource CancellationTokenSource => cancellation!;

        public override void AddResultFile(string fileName) { }

        public override void WriteLine(string? message) { }

        public override void WriteLine(string format, params object?[]? args) { }

        public override void Write(string? message) { }

        public override void Write(string format, params object?[]? args) { }
    }

    private static (TimeSpan? budget, TimeoutBudgetSource? source) InvokeResolveTimeoutBudget(string? methodName)
    {
        var method = typeof(XpingTestBase).GetMethod(
            "ResolveTimeoutBudget",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
        {
            throw new InvalidOperationException("ResolveTimeoutBudget method not found");
        }

        var target = methodName == null
            ? null
            : typeof(BudgetSamples).GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        var result = method.Invoke(null, new object?[] { target });
        return ((TimeSpan? budget, TimeoutBudgetSource? source))result!;
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
