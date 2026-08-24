/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit.Tests;

using global::NUnit.Framework;
using global::NUnit.Framework.Interfaces;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Xping.Sdk.Core.Models.Executions;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Tests for XpingTrackAttribute.
/// Note: Deep integration testing with actual NUnit test execution is done in sample projects.
/// These unit tests verify the attribute's contract and error handling.
/// </summary>
[Collection("XpingContext")]
public sealed class XpingTrackAttributeTests : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        return XpingContext.ShutdownAsync().AsTask();
    }

    public Task DisposeAsync()
    {
        return XpingContext.ShutdownAsync().AsTask();
    }

    [Fact]
    public void Targets_ReturnsTestLevel()
    {
        var attribute = new XpingTrackAttribute();

        var targets = ((ITestAction)attribute).Targets;

        // ActionTargets.Test has value 1
        Assert.Equal(1, (int)targets);
    }

    [Fact]
    public void BeforeTest_WithNullTest_ThrowsArgumentNullException()
    {
        var attribute = new XpingTrackAttribute();

        Assert.Throws<ArgumentNullException>(() => ((ITestAction)attribute).BeforeTest(null!));
    }

    [Fact]
    public void AfterTest_WithNullTest_DoesNotThrow()
    {
        var attribute = new XpingTrackAttribute();

        // AfterTest returns early when test is null
        var exception = Record.Exception(() => ((ITestAction)attribute).AfterTest(null!));

        Assert.Null(exception);
    }

    [Fact]
    public void AfterTest_WithoutBeforeTest_DoesNotThrow()
    {
        var attribute = new XpingTrackAttribute();

        var exception = Record.Exception(() => ((ITestAction)attribute).AfterTest(null!));

        Assert.Null(exception);
    }

    [Fact]
    public void AttributeUsage_CanBeAppliedToMethod()
    {
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        Assert.True((usage.ValidOn & AttributeTargets.Method) == AttributeTargets.Method);
    }

    [Fact]
    public void AttributeUsage_CanBeAppliedToClass()
    {
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        Assert.True((usage.ValidOn & AttributeTargets.Class) == AttributeTargets.Class);
    }

    [Fact]
    public void AttributeUsage_CanBeAppliedToAssembly()
    {
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        Assert.True((usage.ValidOn & AttributeTargets.Assembly) == AttributeTargets.Assembly);
    }

    [Fact]
    public void AttributeUsage_IsInherited()
    {
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        Assert.True(usage.Inherited);
    }

    [Fact]
    public void AttributeUsage_AllowsOnlyOneInstance()
    {
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        Assert.False(usage.AllowMultiple);
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
    public void MapOutcome_TimeoutMessageOnTestWithDeclaredBudget_ReturnsTimeout()
    {
        var outcome = InvokeMapOutcome(
            ResultState.Failure, "Test exceeded Timeout value of 500ms", budgetDeclared: true);

        Assert.Equal(TestOutcome.Timeout, outcome);
    }

    [Fact]
    public void MapOutcome_CancelAfterMessageOnTestWithDeclaredBudget_ReturnsTimeout()
    {
        var outcome = InvokeMapOutcome(
            ResultState.Failure, "Test exceeded CancelAfter value of 500ms", budgetDeclared: true);

        Assert.Equal(TestOutcome.Timeout, outcome);
    }

    /// <summary>
    /// NUnit gives a timed-out test the same result state as any other failure and marks it only in
    /// the message, so a test is free to fail with text that reads exactly like the framework's. The
    /// declared budget is what separates the two: a test that never declared a timeout cannot have
    /// exceeded one, whatever it says.
    /// </summary>
    [Fact]
    public void MapOutcome_TimeoutMessageOnTestWithoutDeclaredBudget_ReturnsFailed()
    {
        var outcome = InvokeMapOutcome(
            ResultState.Failure, "Test exceeded Timeout value of 1ms", budgetDeclared: false);

        Assert.Equal(TestOutcome.Failed, outcome);
    }

    [Fact]
    public void MapOutcome_OrdinaryFailureOnTestWithDeclaredBudget_ReturnsFailed()
    {
        var outcome = InvokeMapOutcome(ResultState.Failure, "boom", budgetDeclared: true);

        Assert.Equal(TestOutcome.Failed, outcome);
    }

    [Fact]
    public void MapOutcome_Cancelled_ReturnsTimeout()
    {
        var outcome = InvokeMapOutcome(ResultState.Cancelled, null, budgetDeclared: false);

        Assert.Equal(TestOutcome.Timeout, outcome);
    }

    [Fact]
    public void MapOutcome_Success_ReturnsPassed()
    {
        var outcome = InvokeMapOutcome(ResultState.Success, null, budgetDeclared: true);

        Assert.Equal(TestOutcome.Passed, outcome);
    }

    [Fact]
    public void MapOutcome_Skipped_ReturnsSkipped()
    {
        var outcome = InvokeMapOutcome(ResultState.Skipped, null, budgetDeclared: false);

        Assert.Equal(TestOutcome.Skipped, outcome);
    }

    [Fact]
    public void MapOutcome_Inconclusive_ReturnsInconclusive()
    {
        var outcome = InvokeMapOutcome(ResultState.Inconclusive, null, budgetDeclared: false);

        Assert.Equal(TestOutcome.Inconclusive, outcome);
    }

    private static TestOutcome InvokeMapOutcome(ResultState resultState, string? message, bool budgetDeclared)
    {
        MethodInfo method = typeof(XpingTrackAttribute).GetMethod(
            "MapOutcome",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        object? value = method.Invoke(null, [resultState, message, budgetDeclared]);
        return Assert.IsType<TestOutcome>(value);
    }

    private static (string? stackTrace, bool stackTraceOmitted) InvokeResolveStackTrace(
        TestOutcome outcome,
        string? stackTrace,
        bool captureStackTraces)
    {
        MethodInfo method = typeof(XpingTrackAttribute).GetMethod(
            "ResolveStackTrace",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        object? value = method.Invoke(null, [outcome, stackTrace, captureStackTraces]);
        return Assert.IsType<(string?, bool)>(value);
    }
}
