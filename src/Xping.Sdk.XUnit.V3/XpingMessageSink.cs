/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Xping.Sdk.Core.Attributes;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Diagnostics;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.Shared;
using Xping.Sdk.XUnit.V3.Retry;
using Xunit.Sdk;
using Xunit.v3;

namespace Xping.Sdk.XUnit.V3;

/// <summary>
/// Message sink that turns the xUnit v3 messages of one run into Xping test executions, and ends the
/// session when the test assembly finishes.
/// </summary>
/// <remarks>
/// <para>
/// xUnit v3 messages are flat records that name their test case by id, so the test cases the run was
/// handed are what the method, its attributes and its declared timeout are read from.
/// </para>
/// <para>
/// Messages reach the sink on xUnit's reporting thread, some time after the test produced them, so
/// every time recorded comes from the message itself rather than from the clock when it arrives.
/// </para>
/// </remarks>
internal sealed class XpingMessageSink : IMessageSink
{
    private readonly IMessageSink _innerSink;
    private readonly IExecutionTracker _executionTracker;
    private readonly IRetryDetector<IXunitTestCase> _retryDetector;
    private readonly ITestIdentityGenerator _identityGenerator;
    private readonly bool _captureStackTraces;
    private readonly string _assemblyName;
    private readonly Action<TestExecution> _record;
    private readonly Func<Task<XpingNetworkException?>> _endSession;
    private readonly Action<XpingNetworkException> _failRun;

    private readonly Dictionary<string, IXunitTestCase> _testCases = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TestExecutionData> _testData = new(StringComparer.Ordinal);

    // The attempt a retry library last announced, by test case display name; see RecordRetryAttempt.
    // Read rather than taken: a theory case that was not pre-enumerated reports every row of the
    // attempt after one announcement.
    private readonly ConcurrentDictionary<string, int> _retryAttempts = new(StringComparer.Ordinal);

    // Relativized source paths, by the path xUnit recorded. Relativizing walks the filesystem up to
    // the repository root, and every test in a file has the same answer.
    private static readonly ConcurrentDictionary<string, string> _sourceFiles = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingMessageSink"/> class for a run of the
    /// SDK's own session.
    /// </summary>
    internal XpingMessageSink(
        IMessageSink innerSink,
        IReadOnlyCollection<IXunitTestCase> testCases,
        IExecutionTracker executionTracker,
        IRetryDetector<IXunitTestCase> retryDetector,
        ITestIdentityGenerator identityGenerator,
        bool captureStackTraces,
        string assemblyName)
        : this(
            innerSink,
            testCases,
            executionTracker,
            retryDetector,
            identityGenerator,
            captureStackTraces,
            assemblyName,
            XpingContext.RecordTest,
            XpingContext.FinalizeAndShutdownAsync,
            XpingContext.FailRun)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XpingMessageSink"/> class.
    /// </summary>
    /// <param name="innerSink">The runner's sink every message is forwarded to.</param>
    /// <param name="testCases">The test cases of the run.</param>
    /// <param name="executionTracker">Tracks concurrency and suite position.</param>
    /// <param name="retryDetector">Reads retry attributes.</param>
    /// <param name="identityGenerator">Generates stable test identities.</param>
    /// <param name="captureStackTraces">Whether stack traces are kept.</param>
    /// <param name="assemblyName">The simple name of the test assembly.</param>
    /// <param name="record">Records a finished execution.</param>
    /// <param name="endSession">Ends the session, returning the strict-mode error to fail the run on.</param>
    /// <param name="failRun">Fails the run where an error message does not.</param>
    internal XpingMessageSink(
        IMessageSink innerSink,
        IReadOnlyCollection<IXunitTestCase> testCases,
        IExecutionTracker executionTracker,
        IRetryDetector<IXunitTestCase> retryDetector,
        ITestIdentityGenerator identityGenerator,
        bool captureStackTraces,
        string assemblyName,
        Action<TestExecution> record,
        Func<Task<XpingNetworkException?>> endSession,
        Action<XpingNetworkException> failRun)
    {
        _innerSink = innerSink.RequireNotNull();
        _executionTracker = executionTracker.RequireNotNull();
        _retryDetector = retryDetector.RequireNotNull();
        _identityGenerator = identityGenerator.RequireNotNull();
        _captureStackTraces = captureStackTraces;
        _assemblyName = assemblyName.RequireNotNull();
        _record = record.RequireNotNull();
        _endSession = endSession.RequireNotNull();
        _failRun = failRun.RequireNotNull();

        foreach (IXunitTestCase testCase in testCases.RequireNotNull())
            _testCases[testCase.UniqueID] = testCase;
    }

    /// <inheritdoc/>
    public bool OnMessage(IMessageSinkMessage message)
    {
        try
        {
            Observe(message);
        }
        catch
        {
            // Recording must never interfere with the run.
        }

        return _innerSink.OnMessage(message);
    }

    private void Observe(IMessageSinkMessage message)
    {
        switch (message)
        {
            case ITestStarting testStarting:
                HandleTestStarting(testStarting);
                break;

            case ITestPassed testPassed:
                HandleTestFinished(testPassed, TestOutcome.Passed, failure: null, skipReason: null);
                break;

            case ITestFailed testFailed:
                HandleTestFinished(testFailed, outcome: null, failure: testFailed, skipReason: null);
                break;

            case ITestSkipped testSkipped:
                HandleTestFinished(testSkipped, TestOutcome.Skipped, failure: null, testSkipped.Reason);
                break;

            case ITestNotRun testNotRun:
                // An explicit test the run left out. Nothing ran, so there is nothing to record.
                if (_testData.TryRemove(testNotRun.TestUniqueID, out TestExecutionData? notRun))
                    _executionTracker.RecordTestEnd(notRun.CollectionName);
                break;

            case IDiagnosticMessage diagnostic:
                RecordRetryAttempt(diagnostic.Message);
                break;

            case ITestAssemblyFinished testAssemblyFinished:
                // Handled before it is forwarded. Once the runner sees the assembly finish it wraps up
                // the run, and the process exits with the session half written.
                HandleTestAssemblyFinished(testAssemblyFinished);
                break;
        }
    }

    private void HandleTestStarting(ITestStarting testStarting)
    {
        if (!_testCases.TryGetValue(testStarting.TestCaseUniqueID, out IXunitTestCase? testCase))
            return;

        string collectionName = CollectionName(testCase.TestCollection.TestCollectionDisplayName);

        TestExecutionData data = new()
        {
            TestCase = testCase,
            DisplayName = testStarting.TestDisplayName,
            Traits = testStarting.Traits,
            StartTime = testStarting.StartTime.UtcDateTime,
            CollectionName = collectionName,
        };

        if (_testData.TryAdd(testStarting.TestUniqueID, data))
        {
            // Paired with the RecordTestEnd that every way out of the test makes.
            _executionTracker.RecordTestStart(collectionName);
        }
    }

    private void HandleTestFinished(
        ITestResultMessage result,
        TestOutcome? outcome,
        ITestFailed? failure,
        string? skipReason)
    {
        if (!_testData.TryRemove(result.TestUniqueID, out TestExecutionData? data))
            return;

        DateTime endTime = result.FinishTime.UtcDateTime;

        string? exceptionType = null;
        string? errorMessage = null;
        string? stackTrace = null;

        if (failure != null)
        {
            exceptionType = failure.ExceptionTypes.FirstOrDefault();
            errorMessage = string.Join(Environment.NewLine, failure.Messages);
            stackTrace = string.Join(Environment.NewLine, failure.StackTraces);
            outcome = ClassifyFailure(failure.Cause, budgetDeclared: data.TestCase.Timeout > 0);
        }
        else if (outcome == TestOutcome.Skipped)
        {
            errorMessage = $"Test skipped: {skipReason}";
        }

        // xUnit reports no execution time for a test it skipped without running.
        TimeSpan duration = outcome == TestOutcome.Skipped
            ? Clamp(endTime - data.StartTime)
            : CalculateDuration(result.ExecutionTime);

        RecordTestExecution(data, outcome!.Value, endTime, duration, result.Output, exceptionType, errorMessage, stackTrace);
    }

    private void HandleTestAssemblyFinished(ITestAssemblyFinished testAssemblyFinished)
    {
        // Task.Run keeps the async work off the runner's message thread, so no continuation can
        // deadlock trying to resume on it.
        XpingNetworkException? runFailure = Task.Run(_endSession).GetAwaiter().GetResult();
        if (runFailure is null)
            return;

        // xUnit's console runner, which vstest drives, fails the run on this error and keeps every
        // result it already has. It has to arrive before the assembly finishes.
        _innerSink.OnMessage(ErrorMessage.FromException(runFailure, testAssemblyFinished.AssemblyUniqueID));
        _failRun(runFailure);
    }

    /// <summary>
    /// Notes the attempt a retry library announced for a test case.
    /// </summary>
    /// <remarks>
    /// xRetry runs each attempt behind a bus of its own that discards every attempt but the one it
    /// reports, so the attempts it retried never reach this sink. Before each attempt it announces
    /// <c>Running test "{name}" attempt ({n}/{max})</c> on the run's own bus, which this sink does see,
    /// and the result it then reports is that of the last attempt announced. A message in any other
    /// form is ignored, which leaves the attempt at 1, as it is for a library with no announcement.
    /// </remarks>
    private void RecordRetryAttempt(string? message)
    {
        if (TryParseRetryAttempt(message, out string? testCaseDisplayName, out int attempt))
            _retryAttempts[testCaseDisplayName!] = attempt;
    }

    private const string RetryAttemptPrefix = "Running test \"";
    private const string RetryAttemptMarker = "\" attempt (";

    /// <summary>
    /// Reads a retry library's announcement of an attempt.
    /// </summary>
    /// <param name="message">A diagnostic message.</param>
    /// <param name="testCaseDisplayName">The display name of the test case the attempt belongs to.</param>
    /// <param name="attempt">The 1-based attempt number.</param>
    /// <returns><see langword="true"/> when the message announces an attempt.</returns>
    internal static bool TryParseRetryAttempt(string? message, out string? testCaseDisplayName, out int attempt)
    {
        testCaseDisplayName = null;
        attempt = 0;

        if (message == null || !message.StartsWith(RetryAttemptPrefix, StringComparison.Ordinal))
            return false;

        int marker = message.LastIndexOf(RetryAttemptMarker, StringComparison.Ordinal);
        if (marker < RetryAttemptPrefix.Length)
            return false;

        int numberStart = marker + RetryAttemptMarker.Length;
        int slash = message.IndexOf('/', numberStart);
        if (slash <= numberStart ||
            !int.TryParse(message.Substring(numberStart, slash - numberStart), NumberStyles.None, CultureInfo.InvariantCulture, out attempt) ||
            attempt < 1)
        {
            return false;
        }

        testCaseDisplayName = message.Substring(RetryAttemptPrefix.Length, marker - RetryAttemptPrefix.Length);
        return true;
    }

    private void RecordTestExecution(
        TestExecutionData data,
        TestOutcome outcome,
        DateTime endTime,
        TimeSpan duration,
        string output,
        string? exceptionType,
        string? errorMessage,
        string? stackTrace)
    {
        try
        {
            _record(CreateTestExecution(data, outcome, endTime, duration, output, exceptionType, errorMessage, stackTrace));
        }
        catch
        {
            // Swallow exceptions to avoid interfering with test execution
        }
        finally
        {
            // Release the in-flight slot even when record creation failed, so later tests are not
            // reported as having run concurrently with this one.
            _executionTracker.RecordTestEnd(data.CollectionName);
        }
    }

    private TestExecution CreateTestExecution(
        TestExecutionData data,
        TestOutcome outcome,
        DateTime endTime,
        TimeSpan duration,
        string output,
        string? exceptionType,
        string? errorMessage,
        string? stackTrace)
    {
        IXunitTestCase testCase = data.TestCase;
        MethodInfo method = testCase.TestMethod.Method;
        string testClassName = testCase.TestClassName;
        string testMethodName = testCase.TestMethodName;

        object[]? arguments = ResolveArguments(testCase, data.DisplayName);

        (string? sourceFile, int? sourceLineNumber) = ResolveSourceLocation(testCase.SourceFilePath, testCase.SourceLineNumber, method);

        TestIdentity identity = _identityGenerator.Generate(
            $"{testClassName}.{testMethodName}",
            _assemblyName,
            arguments,
            data.DisplayName,
            sourceFile,
            sourceLineNumber,
            testFingerprint: method.GetCustomAttribute<XpingFingerprintAttribute>(inherit: false)?.Fingerprint);

        TestMetadata metadata = ExtractMetadata(data.Traits, arguments, output);

        int? announcedAttempt = _retryAttempts.TryGetValue(testCase.TestCaseDisplayName, out int attempt) ? attempt : null;
        RetryMetadata? retryMetadata = announcedAttempt is int known && _retryDetector is IXUnitRetryDetector detector
            ? detector.DetectRetryMetadata(testCase, outcome, known)
            : _retryDetector.DetectRetryMetadata(testCase, outcome);

        (string? configuredStackTrace, bool stackTraceOmitted) = ResolveStackTrace(outcome, stackTrace, _captureStackTraces);
        (TimeSpan? timeoutBudget, TimeoutBudgetSource? timeoutBudgetSource) = testCase.Timeout > 0
            ? (TimeSpan.FromMilliseconds(testCase.Timeout), TimeoutBudgetSource.Declared)
            : ((TimeSpan?)null, (TimeoutBudgetSource?)null);

        // Resolved from the raw trace, before ResolveStackTrace nulls it for a user who opted out of
        // capture. The site is a classification rather than the trace itself, so it survives that.
        (FailureSite? failureSite, string? failureSiteMember) = ResolveFailureSite(
            outcome, testClassName, testMethodName, exceptionType, errorMessage, stackTrace);

        // xUnit has no separate worker concept, so the collection name doubles as both the
        // concurrency worker key and the record's collection metadata. Retried executions reuse the
        // position of the first attempt.
        TestOrchestrationRecord orchestrationRecord = _executionTracker.CreateExecutionContext(
            workerId: data.CollectionName,
            collectionName: data.CollectionName,
            attemptNumber: retryMetadata?.AttemptNumber ?? announcedAttempt ?? 1);

        TestExecution testExecution = new TestExecutionBuilder()
            .WithExecutionId(Guid.NewGuid())
            .WithIdentity(identity)
            .WithTestName(data.DisplayName)
            .WithOutcome(outcome)
            .WithDuration(duration)
            .WithStartTime(data.StartTime)
            .WithEndTime(endTime)
            .WithMetadata(metadata)
            .WithException(exceptionType, errorMessage, configuredStackTrace)
            .WithErrorMessageHash(_identityGenerator.GenerateErrorMessageHash(errorMessage))
            .WithStackTraceHash(_identityGenerator.GenerateStackTraceHash(configuredStackTrace))
            .WithStackTraceOmitted(stackTraceOmitted)
            .WithTimeoutBudget(timeoutBudget, timeoutBudgetSource)
            .WithFailureSite(failureSite, failureSiteMember)
            .WithTestOrchestrationRecord(orchestrationRecord)
            .WithRetry(retryMetadata)
            .Build();

        _executionTracker.RecordTestCompletion(
            workerId: data.CollectionName,
            identity.TestFingerprint,
            data.DisplayName,
            outcome);

        return testExecution;
    }

    /// <summary>
    /// Determines the arguments a test ran with, as far as they tell its rows apart.
    /// </summary>
    /// <param name="testCase">The test case the test belongs to.</param>
    /// <param name="testDisplayName">The display name xUnit gave the test.</param>
    /// <returns>The arguments, or <see langword="null"/> for a test method without parameters.</returns>
    /// <remarks>
    /// <para>
    /// A theory case xUnit v3 did not pre-enumerate carries no arguments: its rows exist only as tests,
    /// which no message hands over. The argument list xUnit writes into each test's display name is the
    /// one thing that tells the rows apart.
    /// </para>
    /// <para>
    /// It is used for pre-enumerated cases too, which do carry their arguments, so a test keeps its
    /// identity whichever way the runner enumerated it: vstest and Microsoft.Testing.Platform
    /// pre-enumerate, xUnit's own console runner does not by default.
    /// </para>
    /// <para>
    /// The exception is a display name xUnit shortened, which it marks with an ellipsis: long strings,
    /// long collections and deep objects. Rows that differ only past the cut would share one identity,
    /// so a case that carries its arguments is identified by them instead.
    /// </para>
    /// </remarks>
    internal static object[]? ResolveArguments(IXunitTestCase testCase, string testDisplayName)
    {
        if (testCase.TestMethod.Parameters.Count == 0)
            return null;

        string argumentText = ArgumentText(testDisplayName);

        if (argumentText.Contains(ArgumentFormatterEllipsis) && testCase.TestMethod.TestMethodArguments is { Length: > 0 } arguments)
        {
            // A null argument is a value like any other: the identity generator formats it as null.
            return arguments!;
        }

        return [argumentText];
    }

    /// <summary>The mark xUnit's argument formatter leaves where it shortened a value: three middle dots.</summary>
    private const string ArgumentFormatterEllipsis = "\u00B7\u00B7\u00B7";

    /// <summary>
    /// Reads the argument list out of a theory row's display name: <c>Class.Method(x: 1)</c> gives <c>x: 1</c>.
    /// </summary>
    /// <param name="testDisplayName">The display name of the test.</param>
    /// <returns>The text inside the parentheses, or the whole name when it has none.</returns>
    internal static string ArgumentText(string testDisplayName)
    {
        int open = testDisplayName.IndexOf('(');

        return open >= 0 && testDisplayName.EndsWith(")", StringComparison.Ordinal)
            ? testDisplayName.Substring(open + 1, testDisplayName.Length - open - 2)
            : testDisplayName;
    }

    /// <summary>
    /// Drops the id xUnit v3 appends to the name of a test class's default collection.
    /// </summary>
    /// <param name="displayName">The collection's display name.</param>
    /// <returns>The name without the id: <c>Test collection for Shop.CartTests</c>.</returns>
    /// <remarks>
    /// The class name is already in the default name, and the id is a hash that only makes the name
    /// longer in every report line it appears in.
    /// </remarks>
    internal static string CollectionName(string displayName)
    {
        int id = displayName.LastIndexOf(" (id: ", StringComparison.Ordinal);

        return id > 0 && displayName.EndsWith(")", StringComparison.Ordinal)
            ? displayName.Substring(0, id)
            : displayName;
    }

    /// <summary>
    /// Decides whether a failed test failed or was stopped for running too long.
    /// </summary>
    /// <param name="cause">The cause xUnit gave the failure.</param>
    /// <param name="budgetDeclared">Whether the test declared a timeout.</param>
    /// <returns><see cref="TestOutcome.Timeout"/> for an overrun, otherwise <see cref="TestOutcome.Failed"/>.</returns>
    /// <remarks>
    /// xUnit marks a failure as a timeout when its exception is a timeout exception, which a test can
    /// throw itself. A test that declared no timeout cannot have overrun one, whatever it threw.
    /// </remarks>
    internal static TestOutcome ClassifyFailure(FailureCause cause, bool budgetDeclared) =>
        cause == FailureCause.Timeout && budgetDeclared ? TestOutcome.Timeout : TestOutcome.Failed;

    /// <summary>The exception xUnit v3 wraps a failing fixture constructor in.</summary>
    private const string TestPipelineExceptionType = "Xunit.Sdk.TestPipelineException";

    /// <summary>The part of that exception's message naming the fixture, common to every scope.</summary>
    private const string FixtureMarker = "fixture type '";

    /// <summary>
    /// The framework frame xUnit disposes a test class through. Written by the runner rather than by
    /// the test, so unlike a message it cannot be produced by the code under test.
    /// </summary>
    private const string DisposeTestClassFrame = "Xunit.v3.TestRunner.DisposeTestClassInstance";

    private const string ConstructorSuffix = "..ctor";

    /// <summary>
    /// Determines where in the test lifecycle a failing execution failed.
    /// </summary>
    /// <param name="outcome">The outcome Xping resolved.</param>
    /// <param name="testClassName">The full name of the class the test belongs to.</param>
    /// <param name="testMethodName">The name of the test method.</param>
    /// <param name="exceptionType">The first exception type xUnit recorded.</param>
    /// <param name="errorMessage">The combined failure message.</param>
    /// <param name="stackTrace">The combined stack trace, before any capture setting is applied.</param>
    /// <returns>The site and the member that failed, or two nulls when the test did not fail.</returns>
    /// <remarks>
    /// <para>
    /// xUnit v3 wraps a fixture whose constructor throws, of any scope — assembly, collection or
    /// class — in <c>TestPipelineException</c>, and names the fixture's type in its message. The
    /// fixture's own exception follows it, with its own trace.
    /// </para>
    /// <para>
    /// Everything else comes from the trace, and the test class's own frames are what anchor it. A
    /// constructor frame belonging to some other type only means a fixture when the test class appears
    /// nowhere in the trace: a test body that does <c>new Thing()</c> and watches it throw produces a
    /// foreign constructor frame too, and calling that a broken fixture would blame shared setup for a
    /// defect inside one test.
    /// </para>
    /// </remarks>
    internal static (FailureSite? Site, string? Member) ResolveFailureSite(
        TestOutcome outcome,
        string? testClassName,
        string? testMethodName,
        string? exceptionType,
        string? errorMessage,
        string? stackTrace)
    {
        if (!outcome.IsFailure())
            return (null, null);

        // A test the framework abandoned was interrupted wherever it happened to be; the frames say
        // where the clock ran out rather than what is broken.
        if (outcome == TestOutcome.Timeout)
            return (FailureSite.Unknown, null);

        if (string.Equals(exceptionType, TestPipelineExceptionType, StringComparison.Ordinal))
        {
            (string Type, string Step)? fixture = ReadFixtureFailure(errorMessage);
            if (fixture is (string type, string step))
                return FixtureFailureSite(type, step);
        }

        var frames = new List<string>(StackFrameLookup.Frames(stackTrace));
        if (frames.Count == 0)
            return (FailureSite.Unknown, null);

        bool disposing = frames.Contains(DisposeTestClassFrame);

        foreach (string frame in frames)
        {
            if (!BelongsTo(frame, testClassName))
                continue;

            // The runner's own disposal frame settles teardown regardless of what the member is
            // called, which matters because it may be Dispose, DisposeAsync, or an explicit
            // interface implementation.
            if (disposing)
                return (FailureSite.TestTeardown, StackFrameLookup.Shorten(frame));

            if (frame.EndsWith(ConstructorSuffix, StringComparison.Ordinal))
                return (FailureSite.TestSetup, StackFrameLookup.Shorten(frame));

            string member = frame.Substring(frame.LastIndexOf('.') + 1);

            if (string.Equals(member, testMethodName, StringComparison.Ordinal))
                return (FailureSite.TestBody, null);

            // IAsyncLifetime is xUnit's per-test setup and teardown for work that cannot happen in a
            // constructor.
            if (string.Equals(member, "InitializeAsync", StringComparison.Ordinal))
                return (FailureSite.TestSetup, StackFrameLookup.Shorten(frame));

            if (string.Equals(member, "DisposeAsync", StringComparison.Ordinal) ||
                string.Equals(member, "Dispose", StringComparison.Ordinal))
                return (FailureSite.TestTeardown, StackFrameLookup.Shorten(frame));
        }

        // No frame belongs to the test class at all, so nothing in it ran. A constructor frame here
        // is a fixture the test never got past.
        foreach (string frame in frames)
        {
            if (frame.EndsWith(ConstructorSuffix, StringComparison.Ordinal))
                return (FailureSite.FixtureSetup, StackFrameLookup.Shorten(frame));
        }

        return (FailureSite.Unknown, null);
    }

    /// <summary>
    /// Whether a frame is a member of the test class, an explicit interface implementation included.
    /// </summary>
    /// <remarks>
    /// An explicit implementation is written <c>Shop.CartTests.System.IDisposable.Dispose</c>, which
    /// reads as a member of a type named <c>Shop.CartTests.System.IDisposable</c>. A nested type is
    /// written with <c>+</c>, so a declaring type that continues the class name after a dot is one.
    /// </remarks>
    private static bool BelongsTo(string frame, string? testClassName)
    {
        string? declaringType = StackFrameLookup.DeclaringType(frame);

        return testClassName != null && declaringType != null &&
               (string.Equals(declaringType, testClassName, StringComparison.Ordinal) ||
                (declaringType.StartsWith(testClassName, StringComparison.Ordinal) &&
                 declaringType.Length > testClassName.Length &&
                 declaringType[testClassName.Length] == '.'));
    }

    /// <summary>
    /// Reads the fixture's type name, and what follows it, out of the message xUnit wraps a failing
    /// fixture in: <c>Class fixture type 'Shop.Db' threw in InitializeAsync</c>.
    /// </summary>
    private static (string Type, string Step)? ReadFixtureFailure(string? errorMessage)
    {
        if (errorMessage == null)
            return null;

        int marker = errorMessage.IndexOf(FixtureMarker, StringComparison.Ordinal);
        if (marker < 0)
            return null;

        int start = marker + FixtureMarker.Length;
        int end = errorMessage.IndexOf('\'', start);
        if (end <= start)
            return null;

        int lineEnd = errorMessage.IndexOf('\n', end);
        string step = lineEnd < 0 ? errorMessage.Substring(end + 1) : errorMessage.Substring(end + 1, lineEnd - end - 1);

        return (errorMessage.Substring(start, end - start), step.Trim());
    }

    /// <summary>
    /// Names the fixture member xUnit's message says failed.
    /// </summary>
    /// <remarks>
    /// xUnit v3 4.0.2 writes "threw in its constructor", "threw in InitializeAsync", "threw in Dispose",
    /// "threw in DisposeAsync", "had one or more unresolved constructor arguments" and "may only define a
    /// single public constructor". The last two are about the constructor too, and so is any wording a
    /// later version adds: the constructor is where a fixture most often fails.
    /// </remarks>
    private static (FailureSite? Site, string? Member) FixtureFailureSite(string type, string step)
    {
        foreach (string member in new[] { "InitializeAsync", "DisposeAsync", "Dispose" })
        {
            if (!string.Equals(step, "threw in " + member, StringComparison.Ordinal))
                continue;

            FailureSite site = member == "InitializeAsync" ? FailureSite.FixtureSetup : FailureSite.FixtureTeardown;
            return (site, StackFrameLookup.Shorten(type + "." + member));
        }

        return (FailureSite.FixtureSetup, StackFrameLookup.Shorten(type + ConstructorSuffix));
    }

    private static (string? stackTrace, bool stackTraceOmitted) ResolveStackTrace(
        TestOutcome outcome,
        string? stackTrace,
        bool captureStackTraces)
    {
        string? normalizedStackTrace = string.IsNullOrWhiteSpace(stackTrace) ? null : stackTrace;

        if (!captureStackTraces)
            return (null, outcome.IsFailure() && normalizedStackTrace != null);

        return (normalizedStackTrace, false);
    }

    private static TestMetadata ExtractMetadata(
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> traits,
        object[]? arguments,
        string output)
    {
        List<string> categories = [];
        List<string> tags = ["framework:xUnit"];
        Dictionary<string, string> customAttributes = [];
        string? description = null;

        // xUnit has no built-in [Description] attribute; the idiomatic equivalent is
        // [Trait("Description", "...")], whose first value becomes the description instead of a tag.
        foreach (KeyValuePair<string, IReadOnlyCollection<string>> trait in traits)
        {
            foreach (string value in trait.Value)
            {
                if (trait.Key.Equals("Description", StringComparison.OrdinalIgnoreCase))
                    description ??= value;
                else if (trait.Key.Equals("Category", StringComparison.OrdinalIgnoreCase))
                    categories.Add(value);
                else
                    tags.Add($"{trait.Key}:{value}");
            }
        }

        if (arguments is { Length: 1 } && arguments[0] is string argumentText)
        {
            customAttributes.Add("Arguments", argumentText);
            tags.Add("type:theory");
        }
        else
        {
            tags.Add("type:fact");
        }

        if (!string.IsNullOrEmpty(output))
            customAttributes.Add("Output", output);

        return new TestMetadataBuilder()
            .AddCategories(categories)
            .AddTags(tags)
            .WithDescription(description)
            .AddCustomAttributes(customAttributes)
            .Build();
    }

    /// <summary>
    /// Determines where the test method is declared.
    /// </summary>
    /// <param name="sourceFilePath">The source file xUnit recorded for the test case, if any.</param>
    /// <param name="sourceLineNumber">The line xUnit recorded for the test case, if any.</param>
    /// <param name="method">The test method, read from its PDB when xUnit recorded nothing.</param>
    /// <returns>The source file and line, or two nulls when neither route produced one.</returns>
    /// <remarks>
    /// xUnit v3 takes the location from the caller information of <c>[Fact]</c> and <c>[Theory]</c>.
    /// An attribute deriving from them that does not pass its own caller information on, as retry
    /// libraries' do not, leaves it empty, so the PDB fallback the other adapters use is kept. Both
    /// routes go through the same relativizer, so a report does not spell one file two ways.
    /// </remarks>
    internal static (string? File, int? Line) ResolveSourceLocation(string? sourceFilePath, int? sourceLineNumber, MethodInfo method)
    {
        if (!string.IsNullOrEmpty(sourceFilePath))
            return (_sourceFiles.GetOrAdd(sourceFilePath!, static path => SourceLocationLookup.Relativize(path, repositoryRoot: null)), sourceLineNumber);

        return SourceLocationLookup.Of(method);
    }

    internal static TimeSpan CalculateDuration(decimal executionTime) =>
        TimeSpan.FromTicks((long)(executionTime * TimeSpan.TicksPerSecond));

    private static TimeSpan Clamp(TimeSpan duration) => duration < TimeSpan.Zero ? TimeSpan.Zero : duration;

    private sealed class TestExecutionData
    {
        public IXunitTestCase TestCase { get; set; } = null!;
        public string DisplayName { get; set; } = string.Empty;
        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Traits { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public string CollectionName { get; set; } = string.Empty;
    }
}
