/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Models.Builders;
using Xunit.Abstractions;
using Xunit.Sdk;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Diagnostics;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.Shared;
using Xping.Sdk.Core.Attributes;
using Xping.Sdk.XUnit.Retry;

namespace Xping.Sdk.XUnit;

/// <summary>
/// Message sink that intercepts xUnit test execution messages and records them to Xping.
/// Tracks test start/end times and outcomes in a thread-safe manner.
/// </summary>
public sealed class XpingMessageSink(
    IMessageSink innerSink,
    IExecutionTracker executionTracker,
    IRetryDetector<ITest> retryDetector,
    ITestIdentityGenerator identityGenerator,
    ILogger<XpingMessageSink> logger,
    bool captureStackTraces,
    string assemblyName) : IMessageSink
{
    private readonly IMessageSink _innerSink = innerSink.RequireNotNull();
    private readonly IExecutionTracker _executionTracker = executionTracker.RequireNotNull();
    private readonly IRetryDetector<ITest> _retryDetector = retryDetector.RequireNotNull();
    private readonly ITestIdentityGenerator _identityGenerator = identityGenerator.RequireNotNull();
    private readonly ILogger<XpingMessageSink> _logger = logger.RequireNotNull();
    private readonly string _assemblyName = assemblyName.RequireNotNull();

    private readonly ConcurrentDictionary<string, TestExecutionData> _testData = new();

    /// <summary>
    /// Handles incoming messages from xUnit test execution.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <returns>True if the message was processed successfully.</returns>
    bool IMessageSink.OnMessage(IMessageSinkMessage message)
    {
        // Attempts of a test case Xping drives itself are recorded from inside the retry loop, where
        // every attempt is still visible. What reaches this sink is only what the retry library chose
        // to report — one attempt, carrying the cumulative duration of all of them — so it is forwarded
        // to the runner without being recorded a second time.
        if (message is ITestMessage testMessage && testMessage.Test.TestCase is IXpingManagedTestCase)
        {
            return _innerSink.OnMessage(message);
        }

        // Handle test lifecycle messages
        switch (message)
        {
            case ITestStarting testStarting:
                HandleTestStarting(testStarting);
                break;

            case ITestPassed testPassed:
                HandleTestPassed(testPassed);
                break;

            case ITestFailed testFailed:
                HandleTestFailed(testFailed);
                break;

            case ITestSkipped testSkipped:
                HandleTestSkipped(testSkipped);
                break;

            case ITestAssemblyFinished _:
                // When assembly finishes, flush all recorded test data
                HandleTestAssemblyFinished();
                break;
        }

        // Forward to the inner sink
        return _innerSink.OnMessage(message);
    }

    private void HandleTestStarting(ITestStarting testStarting)
    {
        string testKey = GetTestKey(testStarting.Test);
        string? collectionName = testStarting.Test.TestCase.TestMethod.TestClass.TestCollection.DisplayName;

        TestExecutionData data = new()
        {
            Test = testStarting.Test,
            StartTime = DateTime.UtcNow,
            StartTimestamp = Stopwatch.GetTimestamp(),
            CollectionName = collectionName,
        };

        if (_testData.TryAdd(testKey, data))
        {
            // Mark the test in flight so overlapping tests can be measured. Reported only when the
            // entry was added, so it pairs with the TryRemove in the result handlers; the matching
            // end runs in RecordTestExecution's finally block.
            _executionTracker.RecordTestStart(collectionName);
        }
    }

    private void HandleTestPassed(ITestPassed testPassed)
    {
        string testKey = GetTestKey(testPassed.Test);
        if (!_testData.TryRemove(testKey, out TestExecutionData? data))
        {
            return;
        }

        DateTime endTime = DateTime.UtcNow;
        TimeSpan duration = CalculateDuration(testPassed.ExecutionTime);

        RecordTestExecution(
            test: data.Test,
            outcome: TestOutcome.Passed,
            startTime: data.StartTime,
            endTime: endTime,
            duration: duration,
            output: testPassed.Output,
            exceptionType: null,
            errorMessage: null,
            stackTrace: null,
            collectionName: data.CollectionName);
    }

    private void HandleTestFailed(ITestFailed testFailed)
    {
        string testKey = GetTestKey(testFailed.Test);
        if (!_testData.TryRemove(testKey, out TestExecutionData? data))
        {
            return;
        }

        DateTime endTime = DateTime.UtcNow;
        long endTimestamp = Stopwatch.GetTimestamp();

        // Extract exception type - XUnit provides an array of exception types
        string? exceptionType = testFailed.ExceptionTypes?.FirstOrDefault();
        string errorMessage = string.Join(Environment.NewLine, testFailed.Messages);

        // The declared budget gates the classification, so it is resolved before the outcome.
        (_, TimeoutBudgetSource? declaredSource) = ResolveTimeoutBudget(data.Test.TestCase);

        TestOutcome outcome = ClassifyFailure(
            testFailed.ExceptionTypes, errorMessage, budgetDeclared: declaredSource != null);

        TimeSpan duration = ResolveFailureDuration(
            outcome, testFailed.ExecutionTime, data.StartTimestamp, endTimestamp);

        RecordTestExecution(
            test: data.Test,
            outcome: outcome,
            startTime: data.StartTime,
            endTime: endTime,
            duration: duration,
            output: testFailed.Output,
            exceptionType: exceptionType,
            errorMessage: errorMessage,
            stackTrace: string.Join(Environment.NewLine, testFailed.StackTraces),
            collectionName: data.CollectionName);
    }

    private void HandleTestSkipped(ITestSkipped testSkipped)
    {
        string testKey = GetTestKey(testSkipped.Test);
        if (!_testData.TryRemove(testKey, out TestExecutionData? data))
        {
            return;
        }

        DateTime endTime = DateTime.UtcNow;
        long endTimestamp = Stopwatch.GetTimestamp();
        TimeSpan duration = CalculateDuration(data.StartTimestamp, endTimestamp);

        RecordTestExecution(
            test: data.Test,
            outcome: TestOutcome.Skipped,
            startTime: data.StartTime,
            endTime: endTime,
            duration: duration,
            output: string.Empty,
            exceptionType: null,
            errorMessage: $"Test skipped: {testSkipped.Reason}",
            stackTrace: null,
            collectionName: data.CollectionName);
    }

    private void HandleTestAssemblyFinished()
    {
        // Finalize the session when the assembly finishes. FinalizeAsync includes an
        // internal flush and is idempotent, so it is safe to call here even if
        // XpingTestFramework.Dispose later calls DisposeAsync (which also finalizes).
        // This path is the primary safeguard for VSTest adapter runs, where Dispose
        // is not reliably called on custom frameworks.
        try
        {
            XpingContext.FinalizeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing Xping session on assembly finished");
        }
    }

    /// <summary>
    /// Records a single attempt of a test case whose retry loop Xping observes from the inside.
    /// </summary>
    /// <remarks>
    /// Called by <see cref="XpingAttemptMessageBus"/> once per attempt, including the attempts the retry
    /// library discards. The attempt number is supplied by the caller rather than inferred, and the
    /// in-flight slot is opened here because the corresponding <see cref="ITestStarting"/> message is
    /// not routed through this sink's own handlers for a managed test case.
    /// </remarks>
    internal void RecordAttempt(
        ITest test,
        TestOutcome outcome,
        DateTime startTime,
        TimeSpan duration,
        string output,
        string? exceptionType,
        string? errorMessage,
        string? stackTrace,
        int attemptNumber)
    {
        string collectionName =
            test.TestCase?.TestMethod?.TestClass?.TestCollection?.DisplayName ?? string.Empty;

        _executionTracker.RecordTestStart(collectionName);

        RecordTestExecution(
            test: test,
            outcome: outcome,
            startTime: startTime,
            endTime: startTime + duration,
            duration: duration,
            output: output,
            exceptionType: exceptionType,
            errorMessage: errorMessage,
            stackTrace: stackTrace,
            collectionName: collectionName,
            attemptNumber: attemptNumber);
    }

    private void RecordTestExecution(
        ITest test,
        TestOutcome outcome,
        DateTime startTime,
        DateTime endTime,
        TimeSpan duration,
        string output,
        string? exceptionType,
        string? errorMessage,
        string? stackTrace,
        string collectionName,
        int? attemptNumber = null)
    {
        try
        {
            TestExecution execution = CreateTestExecution(
                test,
                outcome,
                startTime,
                endTime,
                duration,
                output,
                exceptionType,
                errorMessage,
                stackTrace,
                collectionName,
                attemptNumber);

            XpingContext.RecordTest(execution);
        }
        catch
        {
            // Swallow exceptions to avoid interfering with test execution
        }
        finally
        {
            // Release the in-flight slot even when record creation failed, so later tests are not
            // reported as having run concurrently with this one.
            _executionTracker.RecordTestEnd(collectionName);
        }
    }

    private TestExecution CreateTestExecution(
        ITest test,
        TestOutcome outcome,
        DateTime startTime,
        DateTime endTime,
        TimeSpan duration,
        string output,
        string? exceptionType,
        string? errorMessage,
        string? stackTrace,
        string collectionName,
        int? attemptNumber)
    {
        ITestCase? testCase = test.TestCase;
        ITestMethod? testMethod = testCase.TestMethod;
        ITestClass? testClass = testMethod.TestClass;

        // Generate stable test identity. The assembly name comes from the AssemblyName the xUnit
        // runner handed to XpingTestFramework.CreateExecutor, not testClass.Class.Assembly.Name —
        // xUnit v2's IAssemblyInfo.Name is the full assembly display name (with Version/Culture/
        // PublicKeyToken), not the simple name.
        string fullyQualifiedName = $"{testClass.Class.Name}.{testMethod.Method.Name}";
        string assemblyName = _assemblyName;
        object[]? parameters = testCase.TestMethodArguments;
        string? displayName = test.DisplayName;

        // Read the pinned fingerprint from [XpingFingerprint] if present on the test method
        string? pinnedFingerprint = ReadPinnedFingerprint(testMethod.Method);

        TestIdentity identity = _identityGenerator.Generate(
            fullyQualifiedName,
            assemblyName,
            parameters,
            displayName,
            testFingerprint: pinnedFingerprint);

        // Extract test metadata
        TestMetadata metadata = ExtractMetadata(test, output);
        // Detect retry metadata first, so the attempt number is available when claiming a position.
        // A caller that drove the retry loop knows which attempt this is; without one, the detector
        // falls back to whatever the retry library left behind on the test case.
        RetryMetadata? retryMetadata = attemptNumber is int attempt && _retryDetector is IXUnitRetryDetector detector
            ? detector.DetectRetryMetadata(test, outcome, attempt)
            : _retryDetector.DetectRetryMetadata(test, outcome);
        (string? configuredStackTrace, bool stackTraceOmitted) = ResolveStackTrace(outcome, stackTrace, captureStackTraces);
        (TimeSpan? timeoutBudget, TimeoutBudgetSource? timeoutBudgetSource) = ResolveTimeoutBudget(testCase);

        // Resolved from the raw trace, before ResolveStackTrace nulls it for a user who opted out of
        // capture. The site is a classification rather than the trace itself, so it survives that.
        (FailureSite? failureSite, string? failureSiteMember) = ResolveFailureSite(
            outcome, testClass.Class.Name, testMethod.Method.Name, exceptionType, errorMessage, stackTrace);
        // xUnit has no separate worker concept, so the collection name doubles as both
        // the concurrency worker key and the record's collection metadata.
        // Pass the attempt number so retried executions reuse the position of the first attempt.
        TestOrchestrationRecord orchestrationRecord = _executionTracker.CreateExecutionContext(
            workerId: collectionName,
            collectionName: collectionName,
            attemptNumber: retryMetadata?.AttemptNumber ?? attemptNumber ?? 1);

        TestExecution testExecution = new TestExecutionBuilder()
            .WithExecutionId(Guid.NewGuid())
            .WithIdentity(identity)
            .WithTestName(test.DisplayName)
            .WithOutcome(outcome)
            .WithDuration(duration)
            .WithStartTime(startTime)
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

        // Record test completion for tracking as previous test
        _executionTracker.RecordTestCompletion(
            workerId: collectionName,
            identity.TestFingerprint,
            test.DisplayName,
            outcome);

        return testExecution;
    }

    /// <summary>The exception xUnit wraps a failing class fixture constructor in.</summary>
    private const string TestClassExceptionType = "Xunit.Sdk.TestClassException";

    /// <summary>The part of that exception's message naming the fixture, common to both scopes.</summary>
    private const string FixtureConstructorMarker = "fixture type '";

    /// <summary>
    /// The framework frame xUnit disposes a test class through. Written by the runner rather than by
    /// the test, so unlike a message it cannot be produced by the code under test.
    /// </summary>
    private const string DisposeTestClassFrame = "ReflectionAbstractionExtensions.DisposeTestClass";

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
    /// Of the three frameworks, only xUnit marks anything: a class fixture whose constructor throws is
    /// wrapped in <c>TestClassException</c> and reported against every test in the class, with the
    /// fixture's type named in the message. A <b>collection</b> fixture is not wrapped — it arrives as
    /// the bare exception — so the two are recognised by different evidence despite being the same
    /// event to a reader.
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

        if (string.Equals(exceptionType, TestClassExceptionType, StringComparison.Ordinal))
        {
            string? fixtureType = ReadFixtureType(errorMessage);
            if (fixtureType != null)
                return (FailureSite.FixtureSetup, StackFrameLookup.Shorten(fixtureType + ConstructorSuffix));
        }

        var frames = new List<string>(StackFrameLookup.Frames(stackTrace));
        if (frames.Count == 0)
            return (FailureSite.Unknown, null);

        bool disposing = frames.Contains(DisposeTestClassFrame);

        foreach (string frame in frames)
        {
            if (!string.Equals(StackFrameLookup.DeclaringType(frame), testClassName, StringComparison.Ordinal))
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
        // is a fixture the test never got past — this is how a collection fixture arrives, since
        // xUnit does not wrap that one.
        foreach (string frame in frames)
        {
            if (frame.EndsWith(ConstructorSuffix, StringComparison.Ordinal))
                return (FailureSite.FixtureSetup, StackFrameLookup.Shorten(frame));
        }

        return (FailureSite.Unknown, null);
    }

    /// <summary>
    /// Reads the fixture's type name out of the message xUnit wraps a failing fixture constructor in.
    /// </summary>
    private static string? ReadFixtureType(string? errorMessage)
    {
        if (errorMessage == null)
            return null;

        int marker = errorMessage.IndexOf(FixtureConstructorMarker, StringComparison.Ordinal);
        if (marker < 0)
            return null;

        int start = marker + FixtureConstructorMarker.Length;
        int end = errorMessage.IndexOf('\'', start);

        return end > start ? errorMessage.Substring(start, end - start) : null;
    }

    /// <summary>The exception xUnit throws when a test overruns its declared timeout.</summary>
    private const string TimeoutExceptionType = "Xunit.Sdk.TestTimeoutException";

    /// <summary>The message that exception carries, minus the duration.</summary>
    private const string TimeoutMessagePrefix = "Test execution timed out after ";

    /// <summary>
    /// Decides whether a failed test failed or was stopped for running too long.
    /// </summary>
    /// <param name="exceptionTypes">The exception types xUnit recorded.</param>
    /// <param name="errorMessage">The combined failure message.</param>
    /// <param name="budgetDeclared">Whether the test declared a timeout budget.</param>
    /// <returns><see cref="TestOutcome.Timeout"/> for a genuine overrun, otherwise <see cref="TestOutcome.Failed"/>.</returns>
    /// <remarks>
    /// <para>
    /// All three conditions are required, and each excludes a case the other two let through.
    /// </para>
    /// <para>
    /// <b>The exception type</b> is xUnit's marker for the event, but it is not proof of one:
    /// <c>Xunit.Sdk.TestTimeoutException</c> is public and takes a duration, so a test can throw it
    /// itself and produce byte-identical evidence. <b>The declared budget</b> is what settles that —
    /// a test that declared no timeout cannot have exceeded one, whatever it throws. This mirrors the
    /// same conjunction the NUnit adapter uses for the same reason.
    /// </para>
    /// <para>
    /// <b>The message</b> excludes a case a declared budget does not: xUnit applies a timeout only to
    /// async tests, and fails a synchronous one immediately with "Tests marked with Timeout are only
    /// supported for async tests". That test *does* declare a budget, so only the message tells it
    /// apart. It is a misconfigured test rather than a hanging one, and calling it a timeout would
    /// point the reader at a deadlock that does not exist.
    /// </para>
    /// </remarks>
    private static TestOutcome ClassifyFailure(string[]? exceptionTypes, string? errorMessage, bool budgetDeclared)
    {
        if (!budgetDeclared || exceptionTypes == null || errorMessage == null)
            return TestOutcome.Failed;

        bool timedOut =
            Array.Exists(exceptionTypes, t => string.Equals(t, TimeoutExceptionType, StringComparison.Ordinal)) &&
            errorMessage.IndexOf(TimeoutMessagePrefix, StringComparison.Ordinal) >= 0;

        return timedOut ? TestOutcome.Timeout : TestOutcome.Failed;
    }

    /// <summary>
    /// Reads the timeout the test declared through <c>[Fact(Timeout = …)]</c>.
    /// </summary>
    /// <param name="testCase">The case that ran.</param>
    /// <returns>The declared budget and its source, or two nulls when the test declared none.</returns>
    private static (TimeSpan? Budget, TimeoutBudgetSource? Source) ResolveTimeoutBudget(ITestCase? testCase)
    {
        // xUnit stores the timeout on the case rather than in an attribute we would have to reflect
        // over, and reports "no timeout" as zero rather than as an absent value.
        if (testCase is not IXunitTestCase xunitTestCase || xunitTestCase.Timeout <= 0)
            return (null, null);

        return (TimeSpan.FromMilliseconds(xunitTestCase.Timeout), TimeoutBudgetSource.Declared);
    }

    private static (string? stackTrace, bool stackTraceOmitted) ResolveStackTrace(
        TestOutcome outcome,
        string? stackTrace,
        bool captureStackTraces)
    {
        string? normalizedStackTrace = string.IsNullOrWhiteSpace(stackTrace) ? null : stackTrace;
        bool stackTraceAvailable = normalizedStackTrace != null;
        bool stackTraceOmitted = !captureStackTraces && outcome.IsFailure() && stackTraceAvailable;

        if (!captureStackTraces)
        {
            return (null, stackTraceOmitted);
        }

        return (normalizedStackTrace, false);
    }

    private static TestMetadata ExtractMetadata(ITest test, string output)
    {
        TestMetadataBuilder builder = new();

        ITestCase? testCase = test.TestCase;
        List<string> categories = [];
        List<string> tags = ["framework:xUnit"];
        Dictionary<string, string> customAttributes = [];
        string? description = null;

        // Extract traits as categories, description, or generic tags.
        // xUnit has no built-in [Description] attribute; the idiomatic equivalent is
        // [Trait("Description", "...")], which is handled here by capturing the first value
        // and excluding it from the tags collection.
        if (testCase.Traits != null)
        {
            foreach (KeyValuePair<string, List<string>> trait in testCase.Traits)
            {
                string? key = trait.Key;
                foreach (string? value in trait.Value)
                {
                    if (key.Equals("Description", StringComparison.OrdinalIgnoreCase))
                    {
                        // Capture only the first Description trait value
                        description ??= value;
                    }
                    else if (key.Equals("Category", StringComparison.OrdinalIgnoreCase))
                    {
                        categories.Add(value);
                    }
                    else
                    {
                        tags.Add($"{key}:{value}");
                    }
                }
            }
        }

        // Add test method parameters if present
        if (testCase.TestMethodArguments is { Length: > 0 })
        {
            string args = string.Join(", ", testCase.TestMethodArguments.Select(a => a?.ToString() ?? "null"));
            customAttributes.Add("Arguments", args);
            tags.Add("type:theory");
        }
        else
        {
            tags.Add("type:fact");
        }

        // Add source file info if available
        if (testCase.SourceInformation != null)
        {
            if (!string.IsNullOrEmpty(testCase.SourceInformation.FileName))
            {
                string fileName = testCase.SourceInformation.FileName;
                customAttributes.Add("SourceFile", fileName);
            }

            if (testCase.SourceInformation.LineNumber.HasValue)
            {
                string lineNumber = testCase.SourceInformation.LineNumber.Value.ToString(CultureInfo.InvariantCulture);
                customAttributes.Add("SourceLine", lineNumber);
            }
        }

        // Add test output if present
        if (!string.IsNullOrEmpty(output))
        {
            customAttributes.Add("Output", output);
        }

        TestMetadata metadata = builder
            .AddCategories(categories)
            .AddTags(tags)
            .WithDescription(description)
            .AddCustomAttributes(customAttributes)
            .Build();

        return metadata;
    }

    /// <summary>
    /// Chooses how long a failed test ran, given how the failure was classified.
    /// </summary>
    /// <param name="outcome">The outcome resolved for this execution.</param>
    /// <param name="reportedExecutionTime">The execution time xUnit reported, in seconds.</param>
    /// <param name="startTimestamp">The stopwatch timestamp taken when the test started.</param>
    /// <param name="endTimestamp">The stopwatch timestamp taken when the result arrived.</param>
    /// <returns>The duration to record.</returns>
    /// <remarks>
    /// xUnit reports an execution time of zero for a test it abandoned, because it never observed
    /// that test finish. Measuring from the start notification instead is what keeps the recorded
    /// duration comparable with the declared timeout budget — and that comparison is the whole reason
    /// a timeout is worth telling apart from a failure. The skipped path measures the same way.
    /// Ordinary failures keep xUnit's own figure, which excludes fixture setup and is the more
    /// accurate reading whenever it is available.
    /// </remarks>
    internal static TimeSpan ResolveFailureDuration(
        TestOutcome outcome, decimal reportedExecutionTime, long startTimestamp, long endTimestamp) =>
        outcome == TestOutcome.Timeout
            ? CalculateDuration(startTimestamp, endTimestamp)
            : CalculateDuration(reportedExecutionTime);

    internal static TimeSpan CalculateDuration(long startTimestamp, long endTimestamp)
    {
        long elapsedTicks = endTimestamp - startTimestamp;
        return TimeSpan.FromTicks(elapsedTicks * TimeSpan.TicksPerSecond / Stopwatch.Frequency);
    }

    internal static TimeSpan CalculateDuration(decimal executionTime)
    {
        // xUnit ExecutionTime is in seconds (decimal); convert to TimeSpan via ticks for precision
        long ticks = (long)(executionTime * TimeSpan.TicksPerSecond);
        return TimeSpan.FromTicks(ticks);
    }

    private static string GetTestKey(ITest test)
    {
        return test.TestCase.UniqueID ?? test.DisplayName;
    }

    /// <summary>
    /// Reads the pinned fingerprint from <see cref="XpingFingerprintAttribute"/> on the test method.
    /// Returns null when the attribute is absent (SHA256 will be computed instead).
    /// </summary>
    private static string? ReadPinnedFingerprint(IMethodInfo method)
    {
        MethodInfo? methodInfo = ResolveMethodInfo(method);
        if (methodInfo == null)
        {
            return null;
        }

        return methodInfo.GetCustomAttribute<XpingFingerprintAttribute>(inherit: false)?.Fingerprint;
    }

    /// <summary>
    /// Resolves an xUnit <see cref="IMethodInfo"/> to a BCL <see cref="MethodInfo"/> by scanning
    /// loaded assemblies. Mirrors the pattern used in XUnitRetryDetector.
    /// </summary>
    private static MethodInfo? ResolveMethodInfo(IMethodInfo method)
    {
        try
        {
            string? typeName = method.Type.Name;
            Type? type = Type.GetType(typeName)
                ?? AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Array.Empty<Type>(); }
                    })
                    .FirstOrDefault(t => t.FullName == typeName);

            return type?.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == method.Name);
        }
        catch
        {
            return null;
        }
    }

    private sealed class TestExecutionData
    {
        public ITest Test { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public long StartTimestamp { get; set; }
        public string CollectionName { get; set; } = string.Empty;
    }
}

#pragma warning restore CA1305
