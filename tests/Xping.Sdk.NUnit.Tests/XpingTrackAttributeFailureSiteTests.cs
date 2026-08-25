/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit.Tests;

using global::NUnit.Framework;
using global::NUnit.Framework.Interfaces;
using global::NUnit.Framework.Internal;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Xunit;
using Assert = Xunit.Assert;
using FailureSite = Xping.Sdk.Core.Models.Executions.FailureSite;
using TestOutcome = Xping.Sdk.Core.Models.Executions.TestOutcome;

/// <summary>
/// Covers where a failing NUnit execution is said to have failed.
/// </summary>
/// <remarks>
/// The traces below were captured from real NUnit runs — 3.14.0 and 4.2.2 both produce these shapes —
/// rather than written to match the parser. NUnit reports no site of its own at
/// <c>ITestAction.AfterTest</c>: <c>ResultState.Site</c> is <c>Test</c> even when <c>[SetUp]</c> threw,
/// so the stack trace is the only evidence there is.
/// </remarks>
public sealed class XpingTrackAttributeFailureSiteTests
{
    /// <summary>
    /// A fixture used only as reflection material. It is nested deliberately: reflection spells a
    /// nested type <c>Outer+Inner</c> while a stack frame prints <c>Outer.Inner</c>, and a fixture
    /// declared inside another class would otherwise never match its own frames.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Reflection material: only its attributed members and their names are read.")]
    private sealed class SampleFixture
    {
        [SetUp] public void Setup() { }
        [TearDown] public void Teardown() { }
        [OneTimeSetUp] public void OnceBefore() { }
        [Test] public void Body() { }
    }

    private static readonly string Fixture =
        typeof(SampleFixture).FullName!.Replace('+', '.');

    private static string Frame(string method) =>
        $"   at {Fixture}.{method}() in /src/SampleFixture.cs:line 12";

    [Fact]
    public void ResolveFailureSite_APassingTest_RecordsNoSite()
    {
        var (site, member) = Resolve(TestOutcome.Passed, message: null, stackTrace: Frame("Body"));

        Assert.Null(site);
        Assert.Null(member);
    }

    [Fact]
    public void ResolveFailureSite_AFailureInSetUp_IsTestSetupAndNamesTheMethod()
    {
        string trace =
            Frame("Setup") + "\n" +
            "   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)";

        var (site, member) = Resolve(
            TestOutcome.Failed, "System.InvalidOperationException : boom in SetUp", trace);

        Assert.Equal(FailureSite.TestSetup, site);
        Assert.Equal("SampleFixture.Setup", member);
    }

    /// <summary>
    /// An assertion failing inside <c>[SetUp]</c> reads exactly like one failing in the body: the same
    /// result state, and a message with no marker on it. Only the frame separates them.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_AnAssertionFailingInSetUp_IsStillTestSetup()
    {
        var (site, member) = Resolve(TestOutcome.Failed, "  Expected: 2\n  But was:  1\n", Frame("Setup"));

        Assert.Equal(FailureSite.TestSetup, site);
        Assert.Equal("SampleFixture.Setup", member);
    }

    [Fact]
    public void ResolveFailureSite_AFailureInTheBody_IsTestBodyAndNamesNoMember()
    {
        var (site, member) = Resolve(TestOutcome.Failed, "  Expected: 2\n  But was:  1\n", Frame("Body"));

        Assert.Equal(FailureSite.TestBody, site);
        Assert.Null(member);
    }

    /// <summary>
    /// NUnit lists the test method in the trace even when the body passed and only teardown threw, so
    /// the frames alone put the test method on top and would read as a body failure. The framework's
    /// own <c>--TearDown</c> separator is what says which frames below it belong to teardown.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_AFailureInTearDown_IsTestTeardownDespiteTheTestMethodBeingOnTop()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            "TearDown : System.InvalidOperationException : boom in TearDown",
            Frame("Body") + "\n\n--TearDown\n" + Frame("Teardown"));

        Assert.Equal(FailureSite.TestTeardown, site);
        Assert.Equal("SampleFixture.Teardown", member);
    }

    /// <summary>
    /// NUnit 3 writes a leading newline before the prefix and NUnit 4 does not. Both are the same
    /// event and must classify alike.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_TheNUnit3TearDownMessage_ClassifiesLikeTheNUnit4One()
    {
        var (site, _) = Resolve(
            TestOutcome.Failed,
            "\nTearDown : System.InvalidOperationException : boom in TearDown",
            Frame("Body") + "\n\n--TearDown\n" + Frame("Teardown"));

        Assert.Equal(FailureSite.TestTeardown, site);
    }

    /// <summary>
    /// A test whose body failed and whose teardown then also failed carries the teardown text further
    /// down its message, not at the front. The body is the defect to report, and the prefix check is
    /// anchored so that it stays that way.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_ABodyFailureFollowedByATearDownFailure_ReportsTheBody()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            "  Expected: 2\n  But was:  1\nTearDown : System.InvalidOperationException : boom",
            Frame("Body") + "\n\n--TearDown\n" + Frame("Teardown"));

        Assert.Equal(FailureSite.TestBody, site);
        Assert.Null(member);
    }

    /// <summary>
    /// The message prefix is text a test may write for itself; requiring the framework's stack
    /// separator alongside it is what stops <c>Assert.Fail("TearDown : ...")</c> from being recorded
    /// as a teardown failure.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_AForgedTearDownMessageWithoutTheSeparator_IsNotTeardown()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed, "TearDown : not really", Frame("Body"));

        Assert.Equal(FailureSite.TestBody, site);
        Assert.Null(member);
    }

    /// <summary>
    /// A test the framework stopped was interrupted wherever it happened to be, so the frame on top
    /// says where the clock ran out rather than what is broken.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_ATimeout_IsUnknownRatherThanTheFrameItWasInterruptedOn()
    {
        var (site, member) = Resolve(TestOutcome.Timeout, "Test exceeded CancelAfter value of 500ms", Frame("Setup"));

        Assert.Equal(FailureSite.Unknown, site);
        Assert.Null(member);
    }

    [Fact]
    public void ResolveFailureSite_NoStackTrace_IsUnknownRatherThanTestBody()
    {
        var (site, member) = Resolve(TestOutcome.Failed, "something failed", stackTrace: null);

        Assert.Equal(FailureSite.Unknown, site);
        Assert.Null(member);
    }

    /// <summary>
    /// A trace made only of frames belonging to no known member — a helper, a framework internal —
    /// leaves the site unresolved. Falling back to the body would be a claim about the code under test
    /// made on no evidence.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_NoRecognisedFrame_IsUnknown()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed, "boom", "   at Some.Other.Helper.Explode() in /src/Helper.cs:line 3");

        Assert.Equal(FailureSite.Unknown, site);
        Assert.Null(member);
    }

    [Fact]
    public void ResolveFailureSite_NoTest_IsUnknown()
    {
        var (site, member) = XpingTrackAttribute.ResolveFailureSite(
            TestOutcome.Failed, test: null, "boom", Frame("Setup"));

        Assert.Equal(FailureSite.Unknown, site);
        Assert.Null(member);
    }

    /// <summary>
    /// An async lifecycle member compiles to a state machine, so its frame names
    /// <c>&lt;Setup&gt;d__1.MoveNext</c>. Lifecycle methods are among the most likely to be async, so a
    /// parser that only understood the plain form would miss most of them.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_AnAsyncSetUp_IsStillTestSetup()
    {
        string trace = $"   at {Fixture}.<Setup>d__3.MoveNext() in /src/SampleFixture.cs:line 12";

        var (site, member) = Resolve(TestOutcome.Failed, "boom", trace);

        Assert.Equal(FailureSite.TestSetup, site);
        Assert.Equal("SampleFixture.Setup", member);
    }

    private static (FailureSite? Site, string? Member) Resolve(
        TestOutcome outcome, string? message, string? stackTrace)
    {
        MethodInfo body = typeof(SampleFixture).GetMethod(
            nameof(SampleFixture.Body), BindingFlags.Public | BindingFlags.Instance)!;

        var test = new TestMethod(new MethodWrapper(typeof(SampleFixture), body));

        return XpingTrackAttribute.ResolveFailureSite(outcome, test, message, stackTrace);
    }
}
