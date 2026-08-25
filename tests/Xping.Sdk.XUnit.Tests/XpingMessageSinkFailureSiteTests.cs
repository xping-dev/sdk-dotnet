/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.XUnit.Tests;

/// <summary>
/// Covers where a failing xUnit execution is said to have failed. Every trace and message here was
/// captured from a real xUnit 2.9.2 run rather than written to match the parser.
/// </summary>
public class XpingMessageSinkFailureSiteTests
{
    private const string TestClass = "Probe.SampleTests";
    private const string TestMethod = "One";

    [Fact]
    public void ResolveFailureSite_APassingTest_RecordsNoSite()
    {
        var (site, member) = Resolve(TestOutcome.Passed, null, null, Frame(TestClass, TestMethod));

        Assert.Null(site);
        Assert.Null(member);
    }

    /// <summary>
    /// A class fixture is the one case xUnit marks for itself: the exception is wrapped and the
    /// fixture's type is named in the message, so neither the trace nor a guess is needed.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_AClassFixtureConstructorThrowing_IsFixtureSetupAndNamesTheFixture()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            "Xunit.Sdk.TestClassException",
            "Class fixture type 'Probe.BrokenClassFixture' threw in its constructor",
            stackTrace: "   at Probe.BrokenClassFixture..ctor() in /src/Tests.cs:line 43");

        Assert.Equal(FailureSite.FixtureSetup, site);
        Assert.Equal("BrokenClassFixture..ctor", member);
    }

    /// <summary>
    /// A collection fixture is not wrapped — it arrives as the bare exception, with only the trace to
    /// go on. The same event as the class fixture above, recognised by different evidence.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_ACollectionFixtureConstructorThrowing_IsStillFixtureSetup()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            "System.InvalidOperationException",
            "boom in collection fixture ctor",
            "   at Probe.BrokenCollectionFixture..ctor() in /src/Tests.cs:line 74\n" +
            "   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)");

        Assert.Equal(FailureSite.FixtureSetup, site);
        Assert.Equal("BrokenCollectionFixture..ctor", member);
    }

    /// <summary>
    /// A body that constructs something which throws produces a foreign constructor frame too. What
    /// separates it from a broken fixture is that the test class is on the stack — the test ran. Were
    /// this classified as a fixture, one test's own defect would be reported as broken shared setup.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_ABodyConstructingSomethingThatThrows_IsTestBodyNotAFixture()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            "System.InvalidOperationException",
            "boom",
            "   at Probe.Thing..ctor() in /src/Thing.cs:line 5\n" + Frame(TestClass, TestMethod));

        Assert.Equal(FailureSite.TestBody, site);
        Assert.Null(member);
    }

    [Fact]
    public void ResolveFailureSite_TheTestClassConstructorThrowing_IsTestSetup()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            "System.InvalidOperationException",
            "boom in ctor",
            $"   at {TestClass}..ctor() in /src/Tests.cs:line 16\n" +
            "   at System.RuntimeType.CreateInstanceDefaultCtor(Boolean publicOnly, Boolean wrapExceptions)");

        Assert.Equal(FailureSite.TestSetup, site);
        Assert.Equal("SampleTests..ctor", member);
    }

    [Fact]
    public void ResolveFailureSite_InitializeAsyncThrowing_IsTestSetup()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            "System.InvalidOperationException",
            "boom in InitializeAsync",
            Frame(TestClass, "InitializeAsync") + "\n" +
            "   at Xunit.Sdk.ExceptionAggregator.RunAsync[T](Func`1 code) in /_/src/xunit.core/Sdk/ExceptionAggregator.cs:line 107");

        Assert.Equal(FailureSite.TestSetup, site);
        Assert.Equal("SampleTests.InitializeAsync", member);
    }

    [Fact]
    public void ResolveFailureSite_DisposeThrowing_IsTestTeardown()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            "System.InvalidOperationException",
            "boom in Dispose",
            Frame(TestClass, "Dispose") + "\n" +
            "   at Xunit.Sdk.ExecutionTimer.Aggregate(Action action) in /_/src/xunit.execution/Sdk/Frameworks/ExecutionTimer.cs:line 31\n" +
            "   at ReflectionAbstractionExtensions.DisposeTestClass(ITest test, Object testClass, IMessageBus messageBus, ExecutionTimer timer, CancellationTokenSource cancellationTokenSource) in /_/src/xunit.execution/Extensions/ReflectionAbstractionExtensions.cs:line 79");

        Assert.Equal(FailureSite.TestTeardown, site);
        Assert.Equal("SampleTests.Dispose", member);
    }

    /// <summary>
    /// The runner's disposal frame settles teardown whatever the member is called, which is what
    /// covers an explicit interface implementation or a helper the disposer delegates to.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_ADisposerDelegatingToAHelper_IsStillTestTeardown()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            "System.InvalidOperationException",
            "boom",
            "   at Probe.Helper.Close() in /src/Helper.cs:line 9\n" +
            Frame(TestClass, "CleanUpEverything") + "\n" +
            "   at ReflectionAbstractionExtensions.DisposeTestClass(ITest test) in /_/x.cs:line 79");

        Assert.Equal(FailureSite.TestTeardown, site);
        Assert.Equal("SampleTests.CleanUpEverything", member);
    }

    [Fact]
    public void ResolveFailureSite_AFailureInTheBody_IsTestBodyAndNamesNoMember()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            "Xunit.Sdk.EqualException",
            "Assert.Equal() Failure: Values differ",
            "   at Xunit.Assert.Equal[T](T expected, T actual) in /_/src/xunit.assert/Asserts/EqualityAsserts.cs:line 89\n" +
            Frame(TestClass, TestMethod));

        Assert.Equal(FailureSite.TestBody, site);
        Assert.Null(member);
    }

    [Fact]
    public void ResolveFailureSite_ATimeout_IsUnknownRatherThanTheFrameItWasInterruptedOn()
    {
        var (site, member) = Resolve(
            TestOutcome.Timeout,
            "Xunit.Sdk.TestTimeoutException",
            "Test execution timed out after 500ms",
            Frame(TestClass, TestMethod));

        Assert.Equal(FailureSite.Unknown, site);
        Assert.Null(member);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ResolveFailureSite_NoStackTrace_IsUnknownRatherThanTestBody(string? stackTrace)
    {
        var (site, member) = Resolve(TestOutcome.Failed, "System.Exception", "boom", stackTrace);

        Assert.Equal(FailureSite.Unknown, site);
        Assert.Null(member);
    }

    /// <summary>
    /// The wrapper type without the message it always carries is not evidence of a fixture. Falling
    /// through to the trace is what keeps the two conditions joined.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_TheWrapperTypeWithoutItsMessage_FallsBackToTheTrace()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            "Xunit.Sdk.TestClassException",
            "something else entirely",
            Frame(TestClass, TestMethod));

        Assert.Equal(FailureSite.TestBody, site);
        Assert.Null(member);
    }

    [Fact]
    public void ResolveFailureSite_NoRecognisedFrame_IsUnknown()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed, "System.Exception", "boom",
            "   at Some.Other.Helper.Explode() in /src/Helper.cs:line 3");

        Assert.Equal(FailureSite.Unknown, site);
        Assert.Null(member);
    }

    private static string Frame(string type, string method) =>
        $"   at {type}.{method}() in /src/Tests.cs:line 12";

    private static (FailureSite? Site, string? Member) Resolve(
        TestOutcome outcome, string? exceptionType, string? errorMessage, string? stackTrace) =>
        XpingMessageSink.ResolveFailureSite(
            outcome, TestClass, TestMethod, exceptionType, errorMessage, stackTrace);
}
