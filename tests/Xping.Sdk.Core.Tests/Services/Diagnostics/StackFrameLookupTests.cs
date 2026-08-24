/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.Diagnostics;

namespace Xping.Sdk.Core.Tests.Services.Diagnostics;

/// <summary>
/// Every trace here was captured from a real run of the framework named, not written by hand. A
/// hand-written trace proves the parser handles the shape its author imagined; these prove it handles
/// the shape the runtime actually emits.
/// </summary>
public class StackFrameLookupTests
{
    // NUnit 3.14, [SetUp] throwing InvalidOperationException.
    private const string NUnitSetUpTrace =
        "   at Probe.A_SetUpThrows.Setup() in /tmp/probe/Probe.cs:line 62\n" +
        "   at System.Reflection.MethodBaseInvoker.InterpretedInvoke_Method(Object obj, IntPtr* args)\n" +
        "   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)";

    // NUnit 3.14, [TearDown] throwing after the test body passed. Note the --TearDown separator.
    private const string NUnitTearDownTrace =
        "   at Probe.B_TearDownThrows.One() in /tmp/probe/Probe.cs:line 71\n" +
        "\n" +
        "--TearDown\n" +
        "   at Probe.B_TearDownThrows.Teardown() in /tmp/probe/Probe.cs:line 70\n" +
        "   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)";

    // MSTest 3.7.2, an assertion failing inside [TestInitialize]. The framework's own assert frames
    // sit above the user's method.
    private const string MSTestInitAssertionTrace =
        "   at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.ThrowAssertFailed(String assertionName, String message) in /_/src/TestFramework/TestFramework/Assertions/Assert.cs:line 60\n" +
        "   at Microsoft.VisualStudio.TestTools.UnitTesting.Assert.AreEqual[T](T expected, T actual) in /_/src/TestFramework/TestFramework/Assertions/Assert.AreEqual.cs:line 34\n" +
        "   at Probe.F_InitAssertionFails.Init() in /tmp/probe/Probe.cs:line 95\n" +
        "   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)";

    // xUnit 2.9.2, a test class constructor throwing.
    private const string XUnitCtorTrace =
        "   at Probe.A_CtorThrows..ctor() in /tmp/probe/Tests.cs:line 16\n" +
        "   at System.RuntimeType.CreateInstanceDefaultCtor(Boolean publicOnly, Boolean wrapExceptions)";

    // xUnit 2.9.2, IDisposable.Dispose throwing.
    private const string XUnitDisposeTrace =
        "   at Probe.B_DisposeThrows.Dispose() in /tmp/probe/Tests.cs:line 28\n" +
        "   at Xunit.Sdk.ExecutionTimer.Aggregate(Action action) in /_/src/xunit.execution/Sdk/Frameworks/ExecutionTimer.cs:line 31\n" +
        "   at ReflectionAbstractionExtensions.DisposeTestClass(ITest test, Object testClass, IMessageBus messageBus, ExecutionTimer timer, CancellationTokenSource cancellationTokenSource) in /_/src/xunit.execution/Extensions/ReflectionAbstractionExtensions.cs:line 79";

    [Fact]
    public void Frames_ReadsTheUserMethodOutOfARealNUnitSetUpFailure()
    {
        Assert.Equal("Probe.A_SetUpThrows.Setup", Frames(NUnitSetUpTrace)[0]);
    }

    /// <summary>
    /// The file path is cut before the argument list, because a path is free to contain parentheses
    /// and cutting on the first one would truncate the identifier at the wrong place.
    /// </summary>
    [Fact]
    public void Frames_APathContainingParentheses_DoesNotTruncateTheIdentifier()
    {
        const string trace = "   at Probe.Fixture.Setup() in /Users/me/src (copy)/Probe.cs:line 3";

        Assert.Equal("Probe.Fixture.Setup", Frames(trace)[0]);
    }

    [Fact]
    public void Frames_ReadsAConstructorFrame()
    {
        Assert.Equal("Probe.A_CtorThrows..ctor", Frames(XUnitCtorTrace)[0]);
    }

    /// <summary>
    /// An async lifecycle member compiles to a state machine, so the frame names
    /// <c>&lt;Setup&gt;d__1.MoveNext</c> rather than <c>Setup</c>. Matching that against a reflected
    /// method name fails for exactly the members most likely to be async.
    /// </summary>
    [Fact]
    public void Frames_AnAsyncMethod_IsReportedUnderTheNameItWasDeclaredWith()
    {
        const string trace =
            "   at Probe.Fixture.<SetupAsync>d__1.MoveNext() in /tmp/probe/Probe.cs:line 20";

        Assert.Equal("Probe.Fixture.SetupAsync", Frames(trace)[0]);
    }

    [Fact]
    public void Frames_ALambdaInsideAMethod_IsReportedUnderTheEnclosingMethod()
    {
        const string trace =
            "   at Probe.Fixture.<>c__DisplayClass4_0.<Setup>b__0() in /tmp/probe/Probe.cs:line 20";

        Assert.Equal("Probe.Fixture.Setup", Frames(trace)[0]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Frames_NothingToRead_ReturnsEmpty(string? stackTrace)
    {
        Assert.Empty(StackFrameLookup.Frames(stackTrace));
    }

    [Fact]
    public void FirstMatch_FindsTheLifecycleMemberBelowTheFrameworksOwnAssertFrames()
    {
        string? match = StackFrameLookup.FirstMatch(
            MSTestInitAssertionTrace, new HashSet<string> { "Probe.F_InitAssertionFails.Init" });

        Assert.Equal("Probe.F_InitAssertionFails.Init", match);
    }

    /// <summary>
    /// The teardown method sits below the test method in the trace. Whichever candidate set is
    /// supplied, the frame that matches is the one returned — the walk never stops at the top frame
    /// just because it is first.
    /// </summary>
    [Fact]
    public void FirstMatch_ATearDownFrameBelowTheTestMethod_IsStillFound()
    {
        string? match = StackFrameLookup.FirstMatch(
            NUnitTearDownTrace, new HashSet<string> { "Probe.B_TearDownThrows.Teardown" });

        Assert.Equal("Probe.B_TearDownThrows.Teardown", match);
    }

    /// <summary>
    /// When both a lifecycle member and the test method are candidates, the innermost frame wins —
    /// that is the nearest cause, not the outermost caller.
    /// </summary>
    [Fact]
    public void FirstMatch_SeveralCandidatesInOneTrace_ReturnsTheInnermost()
    {
        var candidates = new HashSet<string>
        {
            "Probe.B_TearDownThrows.One",
            "Probe.B_TearDownThrows.Teardown",
        };

        Assert.Equal("Probe.B_TearDownThrows.One", StackFrameLookup.FirstMatch(NUnitTearDownTrace, candidates));
    }

    [Fact]
    public void FirstMatch_NoCandidateAppears_ReturnsNull()
    {
        Assert.Null(StackFrameLookup.FirstMatch(
            NUnitSetUpTrace, new HashSet<string> { "Probe.Other.Setup" }));
    }

    [Fact]
    public void FirstMatch_NoCandidatesSupplied_ReturnsNull()
    {
        Assert.Null(StackFrameLookup.FirstMatch(NUnitSetUpTrace, new HashSet<string>()));
        Assert.Null(StackFrameLookup.FirstMatch(NUnitSetUpTrace, null!));
    }

    [Fact]
    public void Frames_ReadsTheDisposeFrameFromARealXUnitTeardownFailure()
    {
        Assert.Equal("Probe.B_DisposeThrows.Dispose", Frames(XUnitDisposeTrace)[0]);
    }

    [Theory]
    [InlineData("Probe.Deep.Namespace.Fixture.Setup", "Fixture.Setup")]
    [InlineData("Fixture.Setup", "Fixture.Setup")]
    [InlineData("Setup", "Setup")]
    [InlineData("", "")]
    public void Shorten_KeepsTheTypeAndMethod(string qualified, string expected)
    {
        Assert.Equal(expected, StackFrameLookup.Shorten(qualified));
    }

    /// <summary>
    /// Splitting a constructor on its last two dots yields ".ctor" and discards the type — the one
    /// part a reader needs in order to know which class failed to build.
    /// </summary>
    [Fact]
    public void Shorten_AConstructor_KeepsTheTypeItBuilds()
    {
        Assert.Equal("A_CtorThrows..ctor", StackFrameLookup.Shorten("Probe.A_CtorThrows..ctor"));
    }

    [Theory]
    [InlineData("Probe.Fixture.Setup", "Probe.Fixture")]
    [InlineData("Probe.A_CtorThrows..ctor", "Probe.A_CtorThrows")]
    [InlineData("Setup", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void DeclaringType_ReturnsTheTypeTheFrameBelongsTo(string? frame, string? expected)
    {
        Assert.Equal(expected, StackFrameLookup.DeclaringType(frame));
    }

    private static string[] Frames(string trace) => [.. StackFrameLookup.Frames(trace)];
}
