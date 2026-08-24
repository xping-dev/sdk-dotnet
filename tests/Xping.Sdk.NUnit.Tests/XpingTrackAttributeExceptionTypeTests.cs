/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit.Tests;

using global::NUnit.Framework.Interfaces;
using Xping.Sdk.Core.Models.Executions;
using Xunit;

/// <summary>
/// Tests for <see cref="XpingTrackAttribute.ExtractExceptionType"/>.
/// The message strings below are the real ones NUnit produces for each failure shape.
/// </summary>
public sealed class XpingTrackAttributeExceptionTypeTests
{
    private const string AssertionExceptionName = "NUnit.Framework.AssertionException";
    private const string MultipleAssertExceptionName = "NUnit.Framework.MultipleAssertException";

    // ----- Assertion failures -----

    [Fact]
    public void ExtractExceptionType_AssertThatFailure_ReturnsAssertionException()
    {
        var message =
            "  Assert.That(1 + 1, Is.EqualTo(3))\n" +
            "  Expected: 3\n" +
            "  But was:  2\n";

        var exceptionType = XpingTrackAttribute.ExtractExceptionType(ResultState.Failure, message);

        Assert.Equal(AssertionExceptionName, exceptionType);
    }

    [Fact]
    public void ExtractExceptionType_AssertThrowsFailure_ReturnsAssertionException()
    {
        // A failing Assert.Throws reports the assertion expression, not the exception it caught.
        var message =
            "  Assert.That(caughtException, expression)\n" +
            "  Expected: <System.InvalidOperationException>\n" +
            "  But was:  <System.ArgumentException: wrong type>\n";

        var exceptionType = XpingTrackAttribute.ExtractExceptionType(ResultState.Failure, message);

        Assert.Equal(AssertionExceptionName, exceptionType);
    }

    [Fact]
    public void ExtractExceptionType_AssertionMessageLooksLikeExceptionType_DoesNotReturnUserProse()
    {
        // Regression for #119: NUnit places the user-supplied message before the expression line.
        var message =
            "  Config error: System.IO.IOException was not handled\n" +
            "  Assert.That(1 + 1, Is.EqualTo(3))\n" +
            "  Expected: 3\n" +
            "  But was:  2\n";

        var exceptionType = XpingTrackAttribute.ExtractExceptionType(ResultState.Failure, message);

        Assert.Equal(AssertionExceptionName, exceptionType);
    }

    [Fact]
    public void ExtractExceptionType_FlakyTestAssertionMessage_ReturnsAssertionException()
    {
        // Real message recorded from FlakyTest_RandomFailure_FailsProbabilistically.
        var message =
            "  Watchdog (106 ms) fired before the simulated service responded (190 ms). This reproduces\n" +
            "  flakiness caused by network timeouts, service-side back-pressure, or CPU contention that\n" +
            "  shifts task-scheduling order.\n" +
            "Assert.That(winner == serviceCall, Is.True)\n" +
            "  Expected: True\n" +
            "  But was:  False\n";

        var exceptionType = XpingTrackAttribute.ExtractExceptionType(ResultState.Failure, message);

        Assert.Equal(AssertionExceptionName, exceptionType);
    }

    [Fact]
    public void ExtractExceptionType_MultipleAssertFailure_ReturnsMultipleAssertException()
    {
        var message =
            "Multiple failures or warnings in test:\n" +
            "  1) Expected: 3\n" +
            "  But was:  2\n" +
            "  2) Expected: True\n" +
            "  But was:  False\n";

        var exceptionType = XpingTrackAttribute.ExtractExceptionType(ResultState.Failure, message);

        Assert.Equal(MultipleAssertExceptionName, exceptionType);
    }

    [Fact]
    public void ExtractExceptionType_SetUpFailure_ReturnsAssertionException()
    {
        var exceptionType = XpingTrackAttribute.ExtractExceptionType(
            ResultState.SetUpFailure, "  Assert.That(config, Is.Not.Null)");

        Assert.Equal(AssertionExceptionName, exceptionType);
    }

    [Fact]
    public void ExtractExceptionType_FailureWithoutMessage_ReturnsAssertionException()
    {
        Assert.Equal(AssertionExceptionName, XpingTrackAttribute.ExtractExceptionType(ResultState.Failure, null));
        Assert.Equal(
            AssertionExceptionName, XpingTrackAttribute.ExtractExceptionType(ResultState.Failure, string.Empty));
    }

    [Fact]
    public void ExtractExceptionType_ChildFailure_ReturnsNull()
    {
        // A suite rollup: the failing type belongs to a child test, not to this one.
        var exceptionType = XpingTrackAttribute.ExtractExceptionType(
            ResultState.ChildFailure, "One or more child tests had errors");

        Assert.Null(exceptionType);
    }

    // ----- Unhandled exceptions -----

    [Fact]
    public void ExtractExceptionType_UnhandledException_ReturnsFullTypeName()
    {
        var message = "System.InvalidOperationException : This is a test exception for tracking purposes.";

        var exceptionType = XpingTrackAttribute.ExtractExceptionType(ResultState.Error, message);

        Assert.Equal("System.InvalidOperationException", exceptionType);
    }

    [Fact]
    public void ExtractExceptionType_SetUpError_SkipsSetUpPrefix()
    {
        var message = "SetUp : System.InvalidOperationException : Database was unreachable";

        var exceptionType = XpingTrackAttribute.ExtractExceptionType(ResultState.SetUpError, message);

        Assert.Equal("System.InvalidOperationException", exceptionType);
    }

    [Fact]
    public void ExtractExceptionType_TearDownError_SkipsTearDownPrefix()
    {
        var message = "TearDown : System.IO.IOException : The file is locked";

        var exceptionType = XpingTrackAttribute.ExtractExceptionType(ResultState.TearDownError, message);

        Assert.Equal("System.IO.IOException", exceptionType);
    }

    [Fact]
    public void ExtractExceptionType_CustomTypeWithoutExceptionSuffix_ReturnsFullTypeName()
    {
        var exceptionType = XpingTrackAttribute.ExtractExceptionType(ResultState.Error, "MyLib.Fault : boom");

        Assert.Equal("MyLib.Fault", exceptionType);
    }

    [Fact]
    public void ExtractExceptionType_ErrorMessageWithStackTraceLines_ReadsFirstLineOnly()
    {
        var message =
            "System.ArgumentException : Value does not fall within the expected range.\r\n" +
            "   at SampleApp.NUnit.SampleTests.ThrowingTest()\r\n";

        var exceptionType = XpingTrackAttribute.ExtractExceptionType(ResultState.Error, message);

        Assert.Equal("System.ArgumentException", exceptionType);
    }

    [Theory]
    [InlineData("Something went wrong")]                       // no separator at all
    [InlineData("Config error : plain prose here")]            // token before the separator has spaces
    [InlineData("Timeout : the operation timed out")]          // token before the separator is not dotted
    [InlineData("")]
    [InlineData(null)]
    public void ExtractExceptionType_ErrorWithoutTypeShapedToken_ReturnsNull(string? message)
    {
        var exceptionType = XpingTrackAttribute.ExtractExceptionType(ResultState.Error, message);

        Assert.Null(exceptionType);
    }

    // ----- Non-failed and unclassified outcomes -----

    [Fact]
    public void ExtractExceptionType_PassedTest_ReturnsNull()
    {
        Assert.Null(XpingTrackAttribute.ExtractExceptionType(ResultState.Success, null));
    }

    [Fact]
    public void ExtractExceptionType_NonFailedOutcomes_ReturnNull()
    {
        // A skipped or inconclusive test can still carry a message that looks like a type.
        const string message = "System.InvalidOperationException : looks like a type but is not one";

        Assert.Null(XpingTrackAttribute.ExtractExceptionType(ResultState.Skipped, message));
        Assert.Null(XpingTrackAttribute.ExtractExceptionType(ResultState.Ignored, message));
        Assert.Null(XpingTrackAttribute.ExtractExceptionType(ResultState.Explicit, message));
        Assert.Null(XpingTrackAttribute.ExtractExceptionType(ResultState.Inconclusive, message));
    }

    [Fact]
    public void ExtractExceptionType_CancelledOrNotRunnable_ReturnsNull()
    {
        const string message = "System.InvalidOperationException : looks like a type but is not one";

        Assert.Null(XpingTrackAttribute.ExtractExceptionType(ResultState.Cancelled, message));
        Assert.Null(XpingTrackAttribute.ExtractExceptionType(ResultState.NotRunnable, message));
    }

    /// <summary>
    /// NUnit reports a timeout as an ordinary Failure, which the assertion arm would otherwise label
    /// AssertionException — a claim that something asserted, when nothing did.
    /// </summary>
    [Fact]
    public void ResolveExceptionType_Timeout_ReturnsNullRatherThanAssertionException()
    {
        Assert.Null(XpingTrackAttribute.ResolveExceptionType(
            TestOutcome.Timeout, ResultState.Failure, "Test exceeded CancelAfter value of 500ms"));
    }

    [Fact]
    public void ResolveExceptionType_OrdinaryFailure_DelegatesToExtractExceptionType()
    {
        Assert.Equal(
            AssertionExceptionName,
            XpingTrackAttribute.ResolveExceptionType(TestOutcome.Failed, ResultState.Failure, "boom"));
    }

    [Fact]
    public void ExtractExceptionType_NullOutcome_ReturnsNull()
    {
        Assert.Null(XpingTrackAttribute.ExtractExceptionType(null, "System.InvalidOperationException : boom"));
    }
}
