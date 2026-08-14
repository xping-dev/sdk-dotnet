/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest.Tests;

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Tests that failure details are read from <see cref="TestContext.TestException"/>.
/// </summary>
/// <remarks>
/// These getters previously read <c>TestContext.Properties</c> keys that MSTest never defines, so
/// every failed MSTest test recorded empty failure text.
/// </remarks>
public sealed class XpingTestBaseFailureDetailsTests
{
    [Fact]
    public void GetErrorMessage_FailedTest_ReturnsTheExceptionMessage()
    {
        var context = FailedWith(new InvalidOperationException("boom"));

        Assert.Equal("boom", Invoke<string>("GetErrorMessage", context));
    }

    [Fact]
    public void GetExceptionType_FailedTest_ReturnsTheFullTypeName()
    {
        var context = FailedWith(new InvalidOperationException("boom"));

        Assert.Equal("System.InvalidOperationException", Invoke<string>("GetExceptionType", context));
    }

    [Fact]
    public void GetStackTrace_FailedTest_ReturnsTheExceptionStackTrace()
    {
        var context = FailedWith(Thrown(new InvalidOperationException("boom")));

        var stackTrace = Invoke<string>("GetStackTrace", context);

        Assert.NotNull(stackTrace);
        Assert.Contains(nameof(Thrown), stackTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void Getters_UnwrapTheReflectionWrapperAroundTheTestsOwnException()
    {
        var inner = new InvalidOperationException("boom");
        var context = FailedWith(new TargetInvocationException(inner));

        Assert.Equal("boom", Invoke<string>("GetErrorMessage", context));
        Assert.Equal("System.InvalidOperationException", Invoke<string>("GetExceptionType", context));
    }

    [Fact]
    public void Getters_PassedTest_ReturnNull()
    {
        var context = new FailureTestContext(UnitTestOutcome.Passed, new InvalidOperationException("boom"));

        Assert.Null(Invoke<string>("GetErrorMessage", context));
        Assert.Null(Invoke<string>("GetExceptionType", context));
        Assert.Null(Invoke<string>("GetStackTrace", context));
    }

    [Fact]
    public void Getters_FailedTestWithoutAnException_ReturnNull()
    {
        // A timeout or an aborted run fails the test without an exception behind it.
        var context = new FailureTestContext(UnitTestOutcome.Timeout, testException: null);

        Assert.Null(Invoke<string>("GetErrorMessage", context));
        Assert.Null(Invoke<string>("GetExceptionType", context));
        Assert.Null(Invoke<string>("GetStackTrace", context));
    }

    private static FailureTestContext FailedWith(Exception exception) =>
        new FailureTestContext(UnitTestOutcome.Failed, exception);

    /// <summary>
    /// Throws and catches the exception so that it carries a real stack trace.
    /// </summary>
    private static Exception Thrown(Exception exception)
    {
        try
        {
            throw exception;
        }
        catch (Exception caught)
        {
            return caught;
        }
    }

    private static T? Invoke<T>(string methodName, TestContext context)
        where T : class
    {
        MethodInfo method = typeof(XpingTestBase).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{methodName} method not found");

        return (T?)method.Invoke(null, [context]);
    }

    /// <summary>
    /// Minimal <see cref="TestContext"/> exposing an outcome and the exception MSTest publishes for it.
    /// </summary>
    private sealed class FailureTestContext : TestContext
    {
        private readonly UnitTestOutcome _outcome;

        public FailureTestContext(UnitTestOutcome outcome, Exception? testException)
        {
            _outcome = outcome;
            TestException = testException;
        }

#pragma warning disable CS8609 // Nullability of reference types in return type doesn't match overridden member
        public override System.Collections.IDictionary Properties { get; } = new Dictionary<string, object?>();
#pragma warning restore CS8609

        public override UnitTestOutcome CurrentTestOutcome => _outcome;

        public override void AddResultFile(string fileName) { }

        public override void WriteLine(string? message) { }

        public override void WriteLine(string format, params object?[]? args) { }

        public override void Write(string? message) { }

        public override void Write(string format, params object?[]? args) { }
    }
}
