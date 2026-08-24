/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest.Tests;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;
using Assert = Xunit.Assert;
using FailureSite = Xping.Sdk.Core.Models.Executions.FailureSite;
using TestOutcome = Xping.Sdk.Core.Models.Executions.TestOutcome;

/// <summary>
/// Covers where a failing MSTest execution is said to have failed.
/// </summary>
/// <remarks>
/// MSTest exposes no site at all: <c>CurrentTestOutcome</c> is <c>Failed</c> whether the body or a
/// <c>[TestInitialize]</c> threw, and <c>TestException</c> is the raw exception with no wrapper and no
/// message prefix. The traces here were captured from a real MSTest 3.7.2 run.
/// </remarks>
public sealed class XpingTestBaseFailureSiteTests
{
    /// <summary>
    /// Reflection material. Nested deliberately: reflection spells a nested type <c>Outer+Inner</c>
    /// while a stack frame prints <c>Outer.Inner</c>, and a test class declared inside another would
    /// otherwise never match its own frames.
    /// </summary>
    [TestClass]
    [SuppressMessage(
        "Design",
        "CA1034:Nested types should not be visible",
        Justification = "Reflection material for this test class alone; MSTest's analyzers require it to be public.")]
    [SuppressMessage(
        "Design",
        "CA1515:Consider making public types internal",
        Justification = "MSTest's lifecycle analyzers reject these signatures on a non-public class.")]
    public sealed class SampleTests
    {
        [TestInitialize] public void Init() { }
        [TestCleanup] public void Clean() { }
        [ClassInitialize] public static void ClassInit(TestContext context) => _ = context;
        [TestMethod] public void Body() { }
    }

    private static readonly string Class = typeof(SampleTests).FullName!.Replace('+', '.');

    private static string Frame(string method) =>
        $"   at {Class}.{method}() in /src/SampleTests.cs:line 12";

    [Fact]
    public void ResolveFailureSite_APassingTest_RecordsNoSite()
    {
        var (site, member) = Resolve(TestOutcome.Passed, Frame("Body"));

        Assert.Null(site);
        Assert.Null(member);
    }

    [Fact]
    public void ResolveFailureSite_AFailureInTestInitialize_IsTestSetupAndNamesTheMethod()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            Frame("Init") + "\n   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)");

        Assert.Equal(FailureSite.TestSetup, site);
        Assert.Equal("SampleTests.Init", member);
    }

    /// <summary>
    /// An assertion failing inside <c>[TestInitialize]</c> is reported exactly like one failing in the
    /// body, with the framework's own assert frames sitting above the user's method. The walk has to
    /// see past them.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_AnAssertionFailingInTestInitialize_IsStillTestSetup()
    {
        string trace =
            "   at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.ThrowAssertFailed(String assertionName, String message) in /_/src/TestFramework/Assert.cs:line 60\n" +
            "   at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual[T](T expected, T actual) in /_/src/TestFramework/Assert.AreEqual.cs:line 34\n" +
            Frame("Init");

        var (site, member) = Resolve(TestOutcome.Failed, trace);

        Assert.Equal(FailureSite.TestSetup, site);
        Assert.Equal("SampleTests.Init", member);
    }

    [Fact]
    public void ResolveFailureSite_AFailureInTheBody_IsTestBodyAndNamesNoMember()
    {
        var (site, member) = Resolve(TestOutcome.Failed, Frame("Body"));

        Assert.Equal(FailureSite.TestBody, site);
        Assert.Null(member);
    }

    /// <summary>
    /// Mapped but unreachable through this hook, and pinned so it stays correct if MSTest ever does
    /// surface it: a failing <c>[ClassInitialize]</c> aborts the class before <c>[TestInitialize]</c>
    /// runs, so no execution is recorded at all today.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_AFailureInClassInitialize_IsFixtureSetup()
    {
        var (site, member) = Resolve(TestOutcome.Failed, Frame("ClassInit"));

        Assert.Equal(FailureSite.FixtureSetup, site);
        Assert.Equal("SampleTests.ClassInit", member);
    }

    [Fact]
    public void ResolveFailureSite_AFailureInTestCleanup_IsTestTeardown()
    {
        var (site, member) = Resolve(TestOutcome.Failed, Frame("Clean"));

        Assert.Equal(FailureSite.TestTeardown, site);
        Assert.Equal("SampleTests.Clean", member);
    }

    [Fact]
    public void ResolveFailureSite_ATimeout_IsUnknownRatherThanTheFrameItWasInterruptedOn()
    {
        var (site, member) = Resolve(TestOutcome.Timeout, Frame("Init"));

        Assert.Equal(FailureSite.Unknown, site);
        Assert.Null(member);
    }

    /// <summary>
    /// A timeout leaves no exception, so there is no trace to read. Falling through to the body would
    /// be a claim about the code under test made on no evidence.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ResolveFailureSite_NoStackTrace_IsUnknownRatherThanTestBody(string? stackTrace)
    {
        var (site, member) = Resolve(TestOutcome.Failed, stackTrace);

        Assert.Equal(FailureSite.Unknown, site);
        Assert.Null(member);
    }

    [Fact]
    public void ResolveFailureSite_TheTestClassCouldNotBeResolved_IsUnknown()
    {
        var (site, member) = XpingTestBase.ResolveFailureSite(
            TestOutcome.Failed, testClass: null, Frame("Init"));

        Assert.Equal(FailureSite.Unknown, site);
        Assert.Null(member);
    }

    [Fact]
    public void ResolveFailureSite_NoRecognisedFrame_IsUnknown()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed, "   at Some.Other.Helper.Explode() in /src/Helper.cs:line 3");

        Assert.Equal(FailureSite.Unknown, site);
        Assert.Null(member);
    }

    [Fact]
    public void ResolveFailureSite_AnAsyncTestInitialize_IsStillTestSetup()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            $"   at {Class}.<Init>d__2.MoveNext() in /src/SampleTests.cs:line 12");

        Assert.Equal(FailureSite.TestSetup, site);
        Assert.Equal("SampleTests.Init", member);
    }

    private static (FailureSite? Site, string? Member) Resolve(TestOutcome outcome, string? stackTrace) =>
        XpingTestBase.ResolveFailureSite(outcome, typeof(SampleTests), stackTrace);
}
