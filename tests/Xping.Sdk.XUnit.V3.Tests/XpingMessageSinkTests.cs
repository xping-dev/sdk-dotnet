/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Moq;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity.Internals;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.XUnit.V3.Probe;
using Xping.Sdk.XUnit.V3.Retry;
using Xunit.Sdk;
using Xunit.v3;

namespace Xping.Sdk.XUnit.V3.Tests;

public sealed class XpingMessageSinkTests
{
    private const string TestClass = "Probe.SampleTests";
    private const string TestMethod = "One";

    // ---------------------------------------------------------------------------
    // Ending the session
    // ---------------------------------------------------------------------------

    [Fact]
    public void AssemblyFinished_EndsTheSessionBeforeTheRunnerIsTold()
    {
        // Once the runner sees the assembly finish it wraps up the run, and the process exits with
        // the session half written. The session has to be over by then.
        var events = new List<string>();
        var inner = new Mock<IMessageSink>();
        inner.Setup(s => s.OnMessage(It.IsAny<IMessageSinkMessage>()))
            .Callback<IMessageSinkMessage>(m => events.Add(m is ITestAssemblyFinished ? "forwarded" : "other"))
            .Returns(true);

        XpingMessageSink sink = CreateSink(inner.Object, endSession: () =>
        {
            events.Add("session ended");
            return Task.FromResult<XpingNetworkException?>(null);
        });

        sink.OnMessage(AssemblyFinished());

        Assert.Equal(["session ended", "forwarded"], events);
    }

    [Fact]
    public void AssemblyFinished_StrictModeUploadFails_ReportsAnErrorBeforeTheAssemblyFinishesAndFailsTheRun()
    {
        var received = new List<IMessageSinkMessage>();
        var failure = new XpingNetworkException("Xping network error in strict mode");
        XpingNetworkException? failedWith = null;

        XpingMessageSink sink = CreateSink(
            Collecting(received),
            endSession: () => Task.FromResult<XpingNetworkException?>(failure),
            failRun: error => failedWith = error);

        sink.OnMessage(AssemblyFinished());

        Assert.Collection(
            received,
            m => Assert.Equal(typeof(XpingNetworkException).FullName, Assert.IsAssignableFrom<IErrorMessage>(m).ExceptionTypes[0]),
            m => Assert.IsAssignableFrom<ITestAssemblyFinished>(m));
        Assert.Same(failure, failedWith);
    }

    [Fact]
    public void AssemblyFinished_SessionEndsWithoutAnError_ReportsNothingAndDoesNotFailTheRun()
    {
        var received = new List<IMessageSinkMessage>();
        bool failed = false;

        XpingMessageSink sink = CreateSink(Collecting(received), failRun: _ => failed = true);

        sink.OnMessage(AssemblyFinished());

        Assert.IsAssignableFrom<ITestAssemblyFinished>(Assert.Single(received));
        Assert.False(failed);
    }

    // ---------------------------------------------------------------------------
    // Recording
    // ---------------------------------------------------------------------------

    [Fact]
    public void ATestThatPasses_IsRecordedWithTheTimesItsMessagesCarry()
    {
        var recorded = new List<TestExecution>();
        IXunitTestCase testCase = TestCase("case", nameof(OutcomeTests.Passes));
        XpingMessageSink sink = CreateSink(Collecting([]), [testCase], record: recorded.Add);

        var started = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        sink.OnMessage(Starting("test", "case", "Probe.OutcomeTests.Passes", started));
        sink.OnMessage(Mock.Of<ITestPassed>(m =>
            m.TestUniqueID == "test" &&
            m.ExecutionTime == 0.25m &&
            m.FinishTime == started.AddSeconds(1) &&
            m.Output == string.Empty));

        TestExecution execution = Assert.Single(recorded);
        Assert.Equal(TestOutcome.Passed, execution.Outcome);
        Assert.Equal(TimeSpan.FromMilliseconds(250), execution.Duration);
        Assert.Equal(started.UtcDateTime, execution.StartTimeUtc);
        Assert.Equal(started.AddSeconds(1).UtcDateTime, execution.EndTimeUtc);
    }

    [Fact]
    public void AnExplicitTestTheRunLeftOut_IsNotRecordedAndReleasesItsSlot()
    {
        var recorded = new List<TestExecution>();
        var tracker = new Mock<IExecutionTracker>();
        IXunitTestCase testCase = TestCase("case", nameof(OutcomeTests.RunsOnlyWhenAskedFor));
        XpingMessageSink sink = CreateSink(Collecting([]), [testCase], record: recorded.Add, tracker: tracker.Object);

        sink.OnMessage(Starting("test", "case", "Probe.OutcomeTests.RunsOnlyWhenAskedFor", DateTimeOffset.UtcNow));
        sink.OnMessage(Mock.Of<ITestNotRun>(m => m.TestUniqueID == "test"));

        Assert.Empty(recorded);
        tracker.Verify(t => t.RecordTestStart(It.IsAny<string>()), Times.Once);
        tracker.Verify(t => t.RecordTestEnd(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ARecorderThatThrows_DoesNotStopTheMessageReachingTheRunner()
    {
        var received = new List<IMessageSinkMessage>();
        IXunitTestCase testCase = TestCase("case", nameof(OutcomeTests.Passes));
        XpingMessageSink sink = CreateSink(Collecting(received), [testCase], record: _ => throw new InvalidOperationException("boom"));

        sink.OnMessage(Starting("test", "case", "Probe.OutcomeTests.Passes", DateTimeOffset.UtcNow));
        ITestPassed passed = Mock.Of<ITestPassed>(m => m.TestUniqueID == "test" && m.Output == string.Empty);

        Assert.True(sink.OnMessage(passed));
        Assert.Contains(passed, received);
    }

    [Fact]
    public void MessagesForATestCaseTheRunWasNotHanded_AreForwardedAndNotRecorded()
    {
        var recorded = new List<TestExecution>();
        var received = new List<IMessageSinkMessage>();
        XpingMessageSink sink = CreateSink(Collecting(received), [TestCase("case", nameof(OutcomeTests.Passes))], record: recorded.Add);

        ITestStarting starting = Starting("test", "another case", "Probe.Other.Test", DateTimeOffset.UtcNow);
        ITestPassed passed = Mock.Of<ITestPassed>(m => m.TestUniqueID == "test" && m.Output == string.Empty);
        sink.OnMessage(starting);
        sink.OnMessage(passed);

        Assert.Empty(recorded);
        Assert.Equal([starting, passed], received);
    }

    [Fact]
    public void AMessageTheSinkCannotRead_StillReachesTheRunner()
    {
        var received = new List<IMessageSinkMessage>();

        // A test case without a collection makes reading the start throw.
        var testCase = new Mock<IXunitTestCase>();
        testCase.Setup(c => c.UniqueID).Returns("case");
        XpingMessageSink sink = CreateSink(Collecting(received), [testCase.Object]);

        ITestStarting starting = Starting("test", "case", "Probe.OutcomeTests.Passes", DateTimeOffset.UtcNow);

        Assert.True(sink.OnMessage(starting));
        Assert.Contains(starting, received);
    }

    [Fact]
    public void ASkippedTest_LastsFromItsStartToItsFinishAndKeepsItsReason()
    {
        var recorded = new List<TestExecution>();
        XpingMessageSink sink = CreateSink(Collecting([]), [TestCase("case", nameof(OutcomeTests.Passes))], record: recorded.Add);

        var started = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        sink.OnMessage(Starting("test", "case", "Probe.OutcomeTests.Passes", started));
        sink.OnMessage(Mock.Of<ITestSkipped>(m =>
            m.TestUniqueID == "test" && m.Reason == "not today" && m.FinishTime == started.AddMilliseconds(30) && m.Output == string.Empty));

        TestExecution execution = Assert.Single(recorded);
        Assert.Equal(TestOutcome.Skipped, execution.Outcome);
        Assert.Equal(TimeSpan.FromMilliseconds(30), execution.Duration);
        Assert.Equal("Test skipped: not today", execution.ErrorMessage);
    }

    /// <summary>The two times come from different clocks, so a finish can read as earlier than the start.</summary>
    [Fact]
    public void ASkippedTestFinishingBeforeItStarted_LastsNoTime()
    {
        var recorded = new List<TestExecution>();
        XpingMessageSink sink = CreateSink(Collecting([]), [TestCase("case", nameof(OutcomeTests.Passes))], record: recorded.Add);

        var started = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        sink.OnMessage(Starting("test", "case", "Probe.OutcomeTests.Passes", started));
        sink.OnMessage(Mock.Of<ITestSkipped>(m =>
            m.TestUniqueID == "test" && m.Reason == "r" && m.FinishTime == started.AddMilliseconds(-5) && m.Output == string.Empty));

        Assert.Equal(TimeSpan.Zero, Assert.Single(recorded).Duration);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AFailedTest_KeepsItsTraceOnlyWhenTracesAreCaptured(bool captureStackTraces)
    {
        var recorded = new List<TestExecution>();
        XpingMessageSink sink = CreateSink(
            Collecting([]), [TestCase("case", nameof(OutcomeTests.Throws))], record: recorded.Add, captureStackTraces: captureStackTraces);

        string?[] exceptionTypes = ["System.InvalidOperationException"];
        string?[] messages = ["boom"];
        string?[] stackTraces = [Frame(typeof(OutcomeTests).FullName!, nameof(OutcomeTests.Throws))];

        sink.OnMessage(Starting("test", "case", "Probe.OutcomeTests.Throws", DateTimeOffset.UtcNow));
        sink.OnMessage(Mock.Of<ITestFailed>(m =>
            m.TestUniqueID == "test" &&
            m.Cause == FailureCause.Exception &&
            m.ExceptionTypes == exceptionTypes &&
            m.Messages == messages &&
            m.StackTraces == stackTraces &&
            m.Output == string.Empty));

        TestExecution execution = Assert.Single(recorded);
        Assert.Equal(TestOutcome.Failed, execution.Outcome);
        Assert.Equal("boom", execution.ErrorMessage);
        Assert.Equal(captureStackTraces, execution.StackTrace != null);
        Assert.Equal(!captureStackTraces, execution.StackTraceOmitted);
        Assert.Equal(FailureSite.TestBody, execution.Site);
    }

    // ---------------------------------------------------------------------------
    // Arguments and names
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("Probe.Tests.Theory(value: 1)", "value: 1")]
    [InlineData("Probe.Tests.Theory(a: 1, b: \"x (y)\")", "a: 1, b: \"x (y)\"")]
    [InlineData("A custom name", "A custom name")]
    public void ArgumentText_ReadsTheArgumentListOutOfTheDisplayName(string displayName, string expected)
    {
        Assert.Equal(expected, XpingMessageSink.ArgumentText(displayName));
    }

    [Fact]
    public void ResolveArguments_AMethodWithoutParameters_HasNone()
    {
        IXunitTestCase testCase = TestCase("case", nameof(OutcomeTests.Passes));

        Assert.Null(XpingMessageSink.ResolveArguments(testCase, "Probe.OutcomeTests.Passes"));
    }

    [Fact]
    public void ResolveArguments_ATheoryRow_IsTheArgumentTextOfItsDisplayName()
    {
        IXunitTestCase testCase = TestCase("case", nameof(OutcomeTests.Theory));

        Assert.Equal(["value: 2"], XpingMessageSink.ResolveArguments(testCase, "Probe.OutcomeTests.Theory(value: 2)"));
    }

    /// <summary>
    /// xUnit cuts a string argument past 50 characters, so two rows that differ only after the cut
    /// display the same. A pre-enumerated case carries its arguments, which still tell them apart.
    /// </summary>
    [Fact]
    public void ResolveArguments_ADisplayNameXunitShortened_IsTheArgumentsTheCaseCarries()
    {
        object[] arguments = [new string('a', 60) + "b"];
        IXunitTestCase testCase = TestCase("case", nameof(OutcomeTests.Theory), arguments);

        Assert.Equal(arguments, XpingMessageSink.ResolveArguments(testCase, "Probe.OutcomeTests.Theory(value: \"aaaa\"\u00B7\u00B7\u00B7)"));
    }

    [Fact]
    public void ResolveArguments_AShortenedDisplayNameOfACaseWithoutArguments_IsItsArgumentText()
    {
        IXunitTestCase testCase = TestCase("case", nameof(OutcomeTests.Theory));

        Assert.Equal(
            ["value: \"aaaa\"\u00B7\u00B7\u00B7"],
            XpingMessageSink.ResolveArguments(testCase, "Probe.OutcomeTests.Theory(value: \"aaaa\"\u00B7\u00B7\u00B7)"));
    }

    [Theory]
    [InlineData("Test collection for Shop.CartTests (id: 3162997e34be91f108c54ac8)", "Test collection for Shop.CartTests")]
    [InlineData("Database", "Database")]
    public void CollectionName_DropsTheIdOfADefaultCollection(string displayName, string expected)
    {
        Assert.Equal(expected, XpingMessageSink.CollectionName(displayName));
    }

    // ---------------------------------------------------------------------------
    // Retry attempts
    // ---------------------------------------------------------------------------

    [Fact]
    public void TryParseRetryAttempt_XRetrysAnnouncement_GivesTheTestCaseAndAttempt()
    {
        // Captured from xRetry.v3 1.0.0.
        Assert.True(XpingMessageSink.TryParseRetryAttempt(
            "Running test \"Probe.RetryTests.Flaky(name: \"a\")\" attempt (2/3)", out string? testCase, out int attempt));

        Assert.Equal("Probe.RetryTests.Flaky(name: \"a\")", testCase);
        Assert.Equal(2, attempt);
    }

    /// <summary>
    /// A theory case that was not pre-enumerated runs every row on each attempt, after one announcement.
    /// </summary>
    [Fact]
    public void AnAnnouncedAttempt_AppliesToEveryTestOfTheCase()
    {
        var recorded = new List<TestExecution>();
        IXunitTestCase testCase = TestCase("case", typeof(RetryTests), nameof(RetryTests.PassesOnItsSecondAttempt));
        XpingMessageSink sink = CreateSink(Collecting([]), [testCase], record: recorded.Add, retryDetector: new XUnitRetryDetector());

        sink.OnMessage(Mock.Of<IDiagnosticMessage>(m =>
            m.Message == $"Running test \"{testCase.TestCaseDisplayName}\" attempt (2/3)"));

        foreach (string test in new[] { "row 1", "row 2" })
        {
            sink.OnMessage(Starting(test, "case", test, DateTimeOffset.UtcNow));
            sink.OnMessage(Mock.Of<ITestPassed>(m => m.TestUniqueID == test && m.Output == string.Empty));
        }

        Assert.Equal([2, 2], recorded.Select(execution => execution.Retry!.AttemptNumber));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Test \"Probe.RetryTests.Flaky\" failed but is set to retry (1/3) . . .")]
    [InlineData("Running test \"Probe.RetryTests.Flaky\" attempt (x/3)")]
    [InlineData("Running test \"Probe.RetryTests.Flaky\" attempt (0/3)")]
    [InlineData("Running test \"Probe.RetryTests.Flaky\"")]
    public void TryParseRetryAttempt_AnyOtherMessage_IsNotAnAttempt(string? message)
    {
        Assert.False(XpingMessageSink.TryParseRetryAttempt(message, out _, out _));
    }

    // ---------------------------------------------------------------------------
    // ClassifyFailure
    // ---------------------------------------------------------------------------

    [Fact]
    public void ClassifyFailure_ATimeoutOfADeclaredBudget_IsATimeout()
    {
        Assert.Equal(TestOutcome.Timeout, XpingMessageSink.ClassifyFailure(FailureCause.Timeout, budgetDeclared: true));
    }

    /// <summary>
    /// <c>TestTimeoutException</c> is public, so a test can throw it itself. A test that declared no
    /// timeout cannot have overrun one.
    /// </summary>
    [Fact]
    public void ClassifyFailure_ATimeoutWithoutADeclaredBudget_IsAFailure()
    {
        Assert.Equal(TestOutcome.Failed, XpingMessageSink.ClassifyFailure(FailureCause.Timeout, budgetDeclared: false));
    }

    [Theory]
    [InlineData(FailureCause.Assertion)]
    [InlineData(FailureCause.Exception)]
    [InlineData(FailureCause.Unknown)]
    public void ClassifyFailure_AnyOtherCause_IsAFailure(FailureCause cause)
    {
        Assert.Equal(TestOutcome.Failed, XpingMessageSink.ClassifyFailure(cause, budgetDeclared: true));
    }

    // ---------------------------------------------------------------------------
    // ResolveFailureSite — every trace and message here was captured from a real xUnit v3 4.0.2 run
    // ---------------------------------------------------------------------------

    [Fact]
    public void ResolveFailureSite_APassingTest_RecordsNoSite()
    {
        Assert.Equal((null, null), Resolve(TestOutcome.Passed, null, null, Frame(TestClass, TestMethod)));
    }

    [Theory]
    [InlineData("Class fixture type 'Probe.BrokenClassFixture' threw in its constructor\nboom", "BrokenClassFixture..ctor")]
    [InlineData("Collection fixture type 'Probe.BrokenCollectionFixture' threw in its constructor\nboom", "BrokenCollectionFixture..ctor")]
    [InlineData("Assembly fixture type 'Probe.BrokenAssemblyFixture' threw in its constructor\nboom", "BrokenAssemblyFixture..ctor")]
    public void ResolveFailureSite_AFixtureOfAnyScopeThrowing_IsFixtureSetupAndNamesTheFixture(string message, string member)
    {
        var (site, failed) = Resolve(
            TestOutcome.Failed,
            "Xunit.Sdk.TestPipelineException",
            message,
            "   at Xunit.v3.FixtureMappingManager.TryGetFixture(Type fixtureType, Boolean forceCreation) in /_/src/xunit.v3.core/Utility/FixtureMappingManager_reflection.cs:line 186\n" +
            "   at Probe.BrokenClassFixture..ctor() in /src/Tests.cs:line 4");

        Assert.Equal(FailureSite.FixtureSetup, site);
        Assert.Equal(member, failed);
    }

    /// <summary>
    /// The messages are xUnit v3 4.0.2's own. A fixture that got past its constructor is named by the
    /// member that failed, rather than sending the reader to a constructor that worked.
    /// </summary>
    [Theory]
    [InlineData("Class fixture type 'Probe.Db' threw in InitializeAsync\nboom", FailureSite.FixtureSetup, "Db.InitializeAsync")]
    [InlineData("Collection fixture type 'Probe.Db' threw in DisposeAsync\nboom", FailureSite.FixtureTeardown, "Db.DisposeAsync")]
    [InlineData("Class fixture type 'Probe.Db' threw in Dispose\nboom", FailureSite.FixtureTeardown, "Db.Dispose")]
    [InlineData("Class fixture type 'Probe.Db' had one or more unresolved constructor arguments: Int32 x", FailureSite.FixtureSetup, "Db..ctor")]
    public void ResolveFailureSite_AFixtureFailingOutsideItsConstructor_NamesTheMemberThatFailed(
        string message, FailureSite expectedSite, string expectedMember)
    {
        var (site, member) = Resolve(TestOutcome.Failed, "Xunit.Sdk.TestPipelineException", message, string.Empty);

        Assert.Equal(expectedSite, site);
        Assert.Equal(expectedMember, member);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("a pipeline failure that names no fixture")]
    [InlineData("Class fixture type 'unterminated")]
    public void ResolveFailureSite_APipelineFailureWithoutAFixture_IsReadFromTheTrace(string? message)
    {
        var (site, member) = Resolve(TestOutcome.Failed, "Xunit.Sdk.TestPipelineException", message, Frame(TestClass, TestMethod));

        Assert.Equal(FailureSite.TestBody, site);
        Assert.Null(member);
    }

    [Fact]
    public void ResolveFailureSite_AFailureWithoutATrace_IsUnknown()
    {
        Assert.Equal((FailureSite.Unknown, null), Resolve(TestOutcome.Failed, "System.Exception", "boom", string.Empty));
    }

    /// <summary>
    /// With the test class nowhere on the stack, nothing in it ran, and a constructor frame is a fixture
    /// the test never got past.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_OnlyAForeignConstructorOnTheStack_IsFixtureSetup()
    {
        var (site, member) = Resolve(TestOutcome.Failed, "System.Exception", "boom", Frame("Probe.Database", ".ctor"));

        Assert.Equal(FailureSite.FixtureSetup, site);
        Assert.Equal("Database..ctor", member);
    }

    [Fact]
    public void ResolveFailureSite_NoFrameItCanPlace_IsUnknown()
    {
        Assert.Equal(
            (FailureSite.Unknown, null),
            Resolve(TestOutcome.Failed, "System.Exception", "boom", Frame("Probe.Helpers", "Compute")));
    }

    [Fact]
    public void ResolveFailureSite_WithoutATestClassName_NoFrameBelongsToTheTest()
    {
        var (site, _) = XpingMessageSink.ResolveFailureSite(
            TestOutcome.Failed, testClassName: null, TestMethod, "System.Exception", "boom", Frame(TestClass, TestMethod));

        Assert.Equal(FailureSite.Unknown, site);
    }

    /// <summary>
    /// A trace cut short above the runner's own frames still names the member.
    /// </summary>
    [Theory]
    [InlineData("Dispose")]
    [InlineData("DisposeAsync")]
    public void ResolveFailureSite_ADisposeFrameWithoutTheRunners_IsTestTeardown(string disposeMember)
    {
        var (site, member) = Resolve(TestOutcome.Failed, "System.InvalidOperationException", "boom", Frame(TestClass, disposeMember));

        Assert.Equal(FailureSite.TestTeardown, site);
        Assert.Equal($"SampleTests.{disposeMember}", member);
    }

    [Fact]
    public void ResolveFailureSite_DisposingThroughTheRunner_IsTestTeardownForAnyMemberOfTheClass()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            "System.InvalidOperationException",
            "boom dispose",
            Frame(TestClass, "ReleaseConnection") + "\n" +
            "   at Xunit.v3.TestRunner`2.DisposeTestClassInstance(TContext ctxt, Object testClassInstance) in /_/src/xunit.v3.core/Runners/Base/TestRunner.cs:line 103");

        Assert.Equal(FailureSite.TestTeardown, site);
        Assert.Equal("SampleTests.ReleaseConnection", member);
    }

    /// <summary>
    /// A body that constructs something which throws produces a foreign constructor frame too. What
    /// separates it from a broken fixture is that the test class is on the stack: the test ran.
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
            "boom ctor",
            $"   at {TestClass}..ctor() in /src/Tests.cs:line 9\n" +
            "   at System.Reflection.MethodBaseInvoker.InterpretedInvoke_Constructor(Object obj, IntPtr* args)");

        Assert.Equal(FailureSite.TestSetup, site);
        Assert.Equal("SampleTests..ctor", member);
    }

    [Fact]
    public void ResolveFailureSite_InitializeAsyncThrowing_IsTestSetup()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            "System.InvalidOperationException",
            "boom init",
            Frame(TestClass, "InitializeAsync") + "\n" +
            "   at Xunit.v3.XunitTestRunnerBase`2.CreateTestClassInstance(TContext ctxt) in /_/src/xunit.v3.core/Runners/Reflection/XunitTestRunnerBase_reflection.cs:line 64");

        Assert.Equal(FailureSite.TestSetup, site);
        Assert.Equal("SampleTests.InitializeAsync", member);
    }

    /// <summary>
    /// The runner's own disposal frame settles teardown whatever the member is called, as for an
    /// explicit interface implementation.
    /// </summary>
    [Fact]
    public void ResolveFailureSite_DisposingThroughTheRunner_IsTestTeardownWhateverTheMemberIsCalled()
    {
        var (site, member) = Resolve(
            TestOutcome.Failed,
            "System.InvalidOperationException",
            "boom dispose",
            Frame(TestClass, "System.IDisposable.Dispose") + "\n" +
            "   at Xunit.v3.TestRunner`2.DisposeTestClassInstance(TContext ctxt, Object testClassInstance) in /_/src/xunit.v3.core/Runners/Base/TestRunner.cs:line 103");

        Assert.Equal(FailureSite.TestTeardown, site);
        Assert.NotNull(member);
    }

    [Fact]
    public void ResolveFailureSite_ATimeout_IsUnknown()
    {
        var (site, member) = Resolve(
            TestOutcome.Timeout,
            "Xunit.Sdk.TestTimeoutException",
            "Test execution timed out after 200 milliseconds",
            "   at Xunit.v3.CoreTestRunner`3.<>c__DisplayClass8_0.<<RunTestWithTimeout>b__0>d.MoveNext() in /_/src/xunit.v3.core/Runners/Core/CoreTestRunner.cs:line 109");

        Assert.Equal(FailureSite.Unknown, site);
        Assert.Null(member);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static (FailureSite? Site, string? Member) Resolve(
        TestOutcome outcome, string? exceptionType, string? message, string stackTrace) =>
        XpingMessageSink.ResolveFailureSite(outcome, TestClass, TestMethod, exceptionType, message, stackTrace);

    private static string Frame(string type, string method) => $"   at {type}.{method}() in /src/Tests.cs:line 16";

    private static XpingMessageSink CreateSink(
        IMessageSink inner,
        IReadOnlyCollection<IXunitTestCase>? testCases = null,
        Action<TestExecution>? record = null,
        Func<Task<XpingNetworkException?>>? endSession = null,
        Action<XpingNetworkException>? failRun = null,
        IExecutionTracker? tracker = null,
        IRetryDetector<IXunitTestCase>? retryDetector = null,
        bool captureStackTraces = true) =>
        new(
            inner,
            testCases ?? [],
            tracker ?? Mock.Of<IExecutionTracker>(),
            retryDetector ?? Mock.Of<IRetryDetector<IXunitTestCase>>(),
            new TestIdentityGenerator(),
            captureStackTraces,
            assemblyName: "Probe",
            record ?? (_ => { }),
            endSession ?? (() => Task.FromResult<XpingNetworkException?>(null)),
            failRun ?? (_ => { }));

    private static IMessageSink Collecting(List<IMessageSinkMessage> received)
    {
        var sink = new Mock<IMessageSink>();
        sink.Setup(s => s.OnMessage(It.IsAny<IMessageSinkMessage>()))
            .Callback<IMessageSinkMessage>(received.Add)
            .Returns(true);
        return sink.Object;
    }

    private static ITestAssemblyFinished AssemblyFinished() =>
        Mock.Of<ITestAssemblyFinished>(m => m.AssemblyUniqueID == "assembly");

    private static ITestStarting Starting(string testId, string testCaseId, string displayName, DateTimeOffset startTime) =>
        Mock.Of<ITestStarting>(m =>
            m.TestUniqueID == testId &&
            m.TestCaseUniqueID == testCaseId &&
            m.TestDisplayName == displayName &&
            m.StartTime == startTime &&
            m.Traits == new Dictionary<string, IReadOnlyCollection<string>>());

    private static IXunitTestCase TestCase(string id, string probeMethod, object[]? arguments = null) =>
        TestCase(id, typeof(OutcomeTests), probeMethod, arguments);

    private static IXunitTestCase TestCase(string id, Type probeClass, string probeMethod, object[]? arguments = null)
    {
        MethodInfo method = probeClass.GetMethod(probeMethod)!;

        var testMethod = new Mock<IXunitTestMethod>();
        testMethod.Setup(m => m.Method).Returns(method);
        testMethod.Setup(m => m.Parameters).Returns(method.GetParameters());
        testMethod.Setup(m => m.TestMethodArguments).Returns(arguments ?? []);

        var collection = new Mock<IXunitTestCollection>();
        collection.Setup(c => c.TestCollectionDisplayName).Returns("Test collection for Probe.OutcomeTests (id: 1234)");

        var testCase = new Mock<IXunitTestCase>();
        testCase.Setup(c => c.UniqueID).Returns(id);
        testCase.Setup(c => c.TestMethod).Returns(testMethod.Object);
        testCase.Setup(c => c.TestCollection).Returns(collection.Object);
        testCase.Setup(c => c.TestClassName).Returns(probeClass.FullName!);
        testCase.Setup(c => c.TestMethodName).Returns(probeMethod);
        testCase.Setup(c => c.TestCaseDisplayName).Returns($"{probeClass.FullName}.{probeMethod}");
        return testCase.Object;
    }
}
