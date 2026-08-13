/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.XUnit.Retry;
using Xunit.Abstractions;
using Xunit.Sdk;
using xRetry;

namespace Xping.Sdk.XUnit.Tests.Retry;

/// <summary>
/// Tests for issue #115: retry libraries discard the messages of every attempt they retry, so the
/// xUnit adapter never saw a second attempt and recorded <c>AttemptNumber = 1</c> for every execution.
/// </summary>
/// <remarks>
/// These tests drive a real <see cref="RetryTestCase"/> from the xRetry package, so the reflection hook
/// in <c>RetryTestCaseHook</c> is exercised against the library it targets rather than a stand-in.
/// </remarks>
[Collection("XpingContext")]
public sealed class XpingRetryTestCaseTests : IAsyncLifetime
{
    // Keep the shared XpingContext singleton shut down: the sink records every execution it builds
    // through XpingContext.RecordTest, and a live session would collect these fakes and try to upload
    // them. Everything asserted here is captured before that call.
    public Task InitializeAsync() => XpingContext.ShutdownAsync().AsTask();

    public Task DisposeAsync() => XpingContext.ShutdownAsync().AsTask();

    [Fact]
    public async Task RunAsync_TestFailsThenPasses_RecordsBothAttempts()
    {
        // Arrange
        RetryFixture.Reset(failuresBeforePass: 1);
        (XpingMessageSink sink, RecordingExecutionTracker tracker, RecordingIdentityGenerator identity) = CreateSink();
        using RetryTestCase retryCase = CreateRetryTestCase(nameof(RetryFixture.FailsThenPasses), maxRetries: 3);

        XpingRetryTestCase? wrapped = XpingRetryTestCase.TryWrap(retryCase, sink);
        Assert.NotNull(wrapped);

        // Act
        (RunSummary summary, RecordingMessageBus bus) = await RunAsync(wrapped!);

        // Assert — both attempts were recorded, in order, with their own outcomes
        Assert.Equal(2, RetryFixture.Invocations);
        Assert.Equal([(1, TestOutcome.Failed), (2, TestOutcome.Passed)], tracker.Attempts);

        // The failure the retry hid is preserved rather than lost with the discarded attempt
        Assert.Contains(identity.ErrorMessages, message => message?.Contains("first attempt fails", StringComparison.Ordinal) == true);
        Assert.Contains(identity.StackTraces, stackTrace => !string.IsNullOrWhiteSpace(stackTrace));

        // xRetry still owns the outcome: the runner is told the test passed
        Assert.Equal(0, summary.Failed);
        Assert.Single(bus.Messages.OfType<ITestPassed>());
        Assert.Empty(bus.Messages.OfType<ITestFailed>());
    }

    [Fact]
    public async Task RunAsync_RetriedTest_ReusesSuitePositionOfFirstAttempt()
    {
        // Arrange
        RetryFixture.Reset(failuresBeforePass: 1);
        (XpingMessageSink sink, RecordingExecutionTracker tracker, _) = CreateSink();
        using RetryTestCase retryCase = CreateRetryTestCase(nameof(RetryFixture.FailsThenPasses), maxRetries: 3);

        // Act
        await RunAsync(XpingRetryTestCase.TryWrap(retryCase, sink)!);

        // Assert — a retried test must not consume two positions in the suite
        Assert.Equal(2, tracker.Records.Count);
        Assert.Equal(tracker.Records[0].PositionInSuite, tracker.Records[1].PositionInSuite);
        Assert.Equal(tracker.Records[0].GlobalPosition, tracker.Records[1].GlobalPosition);
    }

    [Fact]
    public async Task RunAsync_TestAlwaysFails_RecordsEveryAttempt()
    {
        // Arrange
        RetryFixture.Reset(failuresBeforePass: int.MaxValue);
        (XpingMessageSink sink, RecordingExecutionTracker tracker, _) = CreateSink();
        using RetryTestCase retryCase = CreateRetryTestCase(nameof(RetryFixture.FailsThenPasses), maxRetries: 2);

        // Act
        (RunSummary summary, RecordingMessageBus bus) = await RunAsync(XpingRetryTestCase.TryWrap(retryCase, sink)!);

        // Assert
        Assert.Equal([(1, TestOutcome.Failed), (2, TestOutcome.Failed)], tracker.Attempts);
        Assert.Equal(1, summary.Failed);
        Assert.Single(bus.Messages.OfType<ITestFailed>());
    }

    [Fact]
    public async Task RunAsync_PassingTest_RecordsSingleFirstAttempt()
    {
        // Arrange
        RetryFixture.Reset(failuresBeforePass: 0);
        (XpingMessageSink sink, RecordingExecutionTracker tracker, _) = CreateSink();
        using RetryTestCase retryCase = CreateRetryTestCase(nameof(RetryFixture.FailsThenPasses), maxRetries: 3);

        // Act
        (RunSummary summary, _) = await RunAsync(XpingRetryTestCase.TryWrap(retryCase, sink)!);

        // Assert — a test that never needed a retry looks exactly as it did before
        Assert.Equal([(1, TestOutcome.Passed)], tracker.Attempts);
        Assert.Equal(0, summary.Failed);
    }

    [Fact]
    public async Task RunAsync_TheoryDiscoveredAtRuntime_RecordsEveryAttempt()
    {
        // A theory whose data is enumerated at run time is run by a different xUnit runner, so the
        // attempt delegate has to pick the matching one or the data is never resolved.
        RetryFixture.Reset(failuresBeforePass: 1);
        (XpingMessageSink sink, RecordingExecutionTracker tracker, _) = CreateSink();
        using RetryTheoryDiscoveryAtRuntimeCase theoryCase = new(
            Mock.Of<IMessageSink>(),
            TestMethodDisplay.ClassAndMethod,
            TestMethodDisplayOptions.None,
            CreateTestMethod(nameof(RetryFixture.TheoryFailsThenPasses)),
            maxRetries: 3,
            delayBetweenRetriesMs: 0,
            skipOnExceptions: []);

        // Act
        (RunSummary summary, _) = await RunAsync(XpingRetryTestCase.TryWrap(theoryCase, sink)!);

        // Assert
        Assert.Equal([(1, TestOutcome.Failed), (2, TestOutcome.Passed)], tracker.Attempts);
        Assert.Equal(0, summary.Failed);
    }

    [Fact]
    public void TryWrap_NonRetryTestCase_ReturnsNull()
    {
        // Arrange
        (XpingMessageSink sink, _, _) = CreateSink();
        using var testCase = new XunitTestCase(
            Mock.Of<IMessageSink>(),
            TestMethodDisplay.ClassAndMethod,
            TestMethodDisplayOptions.None,
            CreateTestMethod(nameof(RetryFixture.FailsThenPasses)));

        // Act & Assert — an ordinary test case must be left untouched
        Assert.Null(XpingRetryTestCase.TryWrap(testCase, sink));
    }

    [Fact]
    public void Decorator_DelegatesIdentityMembersToWrappedCase()
    {
        // Arrange
        (XpingMessageSink sink, _, _) = CreateSink();
        using RetryTestCase retryCase = CreateRetryTestCase(nameof(RetryFixture.FailsThenPasses), maxRetries: 3);

        // Act
        XpingRetryTestCase wrapped = XpingRetryTestCase.TryWrap(retryCase, sink)!;

        // Assert — identity and correlation must be indistinguishable from the wrapped case
        Assert.Equal(retryCase.UniqueID, wrapped.UniqueID);
        Assert.Equal(retryCase.DisplayName, wrapped.DisplayName);
        Assert.Equal(retryCase.SkipReason, wrapped.SkipReason);
        Assert.Same(retryCase.TestMethod, wrapped.TestMethod);
        Assert.Same(retryCase.Method, wrapped.Method);
        Assert.Equal(retryCase.Timeout, wrapped.Timeout);
        Assert.Equal(retryCase.Traits, wrapped.Traits);
    }

    private static async Task<(RunSummary summary, RecordingMessageBus bus)> RunAsync(XpingRetryTestCase testCase)
    {
        RecordingMessageBus bus = new();
        using CancellationTokenSource cancellationTokenSource = new();

        RunSummary summary = await testCase.RunAsync(
                Mock.Of<IMessageSink>(),
                bus,
                constructorArguments: [],
                new ExceptionAggregator(),
                cancellationTokenSource)
            .ConfigureAwait(false);

        return (summary, bus);
    }

    private static RetryTestCase CreateRetryTestCase(string methodName, int maxRetries) =>
        new(
            Mock.Of<IMessageSink>(),
            TestMethodDisplay.ClassAndMethod,
            TestMethodDisplayOptions.None,
            CreateTestMethod(methodName),
            maxRetries,
            delayBetweenRetriesMs: 0,
            skipOnExceptions: []);

    private static TestMethod CreateTestMethod(string methodName)
    {
        var testAssembly = new TestAssembly(Reflector.Wrap(typeof(RetryFixture).Assembly));
        var testCollection = new TestCollection(testAssembly, collectionDefinition: null, "retry-collection");
        var testClass = new TestClass(testCollection, Reflector.Wrap(typeof(RetryFixture)));

        return new TestMethod(testClass, Reflector.Wrap(typeof(RetryFixture).GetMethod(methodName)!));
    }

    /// <summary>
    /// Builds a sink backed by the real <see cref="IExecutionTracker"/> and retry detector, wrapped so
    /// the attempts it records can be inspected.
    /// </summary>
    private static (XpingMessageSink sink, RecordingExecutionTracker tracker, RecordingIdentityGenerator identity)
        CreateSink()
    {
        IExecutionTracker inner = new ServiceCollection()
            .AddXpingCollectors()
            .BuildServiceProvider()
            .GetRequiredService<IExecutionTracker>();

        RecordingExecutionTracker tracker = new(inner);
        RecordingIdentityGenerator identity = new();

        XpingMessageSink sink = new(
            Mock.Of<IMessageSink>(),
            tracker,
            new XUnitRetryDetector(),
            identity,
            NullLogger<XpingMessageSink>.Instance,
            captureStackTraces: true,
            assemblyName: "Xping.Sdk.XUnit.Tests");

        return (sink, tracker, identity);
    }

    /// <summary>
    /// A test method driven directly by these tests rather than by the runner.
    /// </summary>
    /// <remarks>
    /// A <see cref="FactAttribute"/> is required because xUnit's <see cref="XunitTestCase"/> reads its
    /// display name and skip reason from one; <see cref="RetryFactAttribute"/> is used so the retry
    /// detector sees the same attribute it would in a real suite. The class is private so that xUnit's
    /// discoverer — which only scans public types — never picks this deliberately failing method up as
    /// a test of its own.
    /// </remarks>
    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated by the xUnit test runner.")]
    [SuppressMessage(
        "Usage",
        "xUnit1000:Test classes must be public",
        Justification = "Not a test class: kept private so xUnit never discovers this fixture as a test.")]
    private sealed class RetryFixture
    {
        private static int _failuresBeforePass;
        private static int _invocations;

        internal static int Invocations => _invocations;

        internal static void Reset(int failuresBeforePass)
        {
            _failuresBeforePass = failuresBeforePass;
            _invocations = 0;
        }

        [RetryFact]
        public void FailsThenPasses()
        {
            int invocation = Interlocked.Increment(ref _invocations);

            Assert.True(
                invocation > _failuresBeforePass,
                $"the first attempt fails on purpose (invocation {invocation})");
        }

        /// <summary>Theory data that cannot be pre-enumerated, so it is resolved at run time.</summary>
        public static IEnumerable<object[]> RuntimeData()
        {
            yield return [new object()];
        }

        [RetryTheory]
        [MemberData(nameof(RuntimeData))]
        public void TheoryFailsThenPasses(object value)
        {
            Assert.NotNull(value);
            FailsThenPasses();
        }
    }

    /// <summary>Captures the messages the retry library reports to the runner.</summary>
    private sealed class RecordingMessageBus : IMessageBus
    {
        private readonly List<IMessageSinkMessage> _messages = [];

        public IReadOnlyList<IMessageSinkMessage> Messages
        {
            get
            {
                lock (_messages)
                {
                    return [.. _messages];
                }
            }
        }

        public bool QueueMessage(IMessageSinkMessage message)
        {
            lock (_messages)
            {
                _messages.Add(message);
            }

            return true;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Delegates to a real <see cref="IExecutionTracker"/> while capturing the attempt number and
    /// outcome of every execution the sink builds.
    /// </summary>
    private sealed class RecordingExecutionTracker(IExecutionTracker inner) : IExecutionTracker
    {
        private readonly List<TestOrchestrationRecord> _records = [];
        private readonly List<int> _attemptNumbers = [];
        private readonly List<TestOutcome> _outcomes = [];

        public IReadOnlyList<TestOrchestrationRecord> Records
        {
            get
            {
                lock (_records)
                {
                    return [.. _records];
                }
            }
        }

        /// <summary>The attempt number and outcome of each recorded execution, in order.</summary>
        public IReadOnlyList<(int AttemptNumber, TestOutcome Outcome)> Attempts
        {
            get
            {
                lock (_records)
                {
                    return [.. _attemptNumbers.Zip(_outcomes, (attempt, outcome) => (attempt, outcome))];
                }
            }
        }

        public int GlobalPosition => inner.GlobalPosition;

        public int ActiveWorkerCount => inner.ActiveWorkerCount;

        public TestOrchestrationRecord CreateExecutionContext(
            string? workerId = null,
            string? collectionName = null,
            int attemptNumber = 1)
        {
            TestOrchestrationRecord record = inner.CreateExecutionContext(workerId, collectionName, attemptNumber);

            lock (_records)
            {
                _records.Add(record);
                _attemptNumbers.Add(attemptNumber);
            }

            return record;
        }

        public void RecordTestCompletion(string? workerId, string testFingerprint, string testName, TestOutcome outcome)
        {
            lock (_records)
            {
                _outcomes.Add(outcome);
            }

            inner.RecordTestCompletion(workerId, testFingerprint, testName, outcome);
        }

        public int RecordTestStart(string? workerId = null) => inner.RecordTestStart(workerId);

        public void RecordTestEnd(string? workerId = null) => inner.RecordTestEnd(workerId);

        public int GetWorkerPosition(string? workerId = null) => inner.GetWorkerPosition(workerId);

        public PrecedingTestRecord? GetPreviousTest(string? workerId = null) => inner.GetPreviousTest(workerId);

        public void Clear() => inner.Clear();
    }

    /// <summary>
    /// Captures the failure details handed to the identity generator, which is the last point they pass
    /// through before being written into the execution record.
    /// </summary>
    private sealed class RecordingIdentityGenerator : ITestIdentityGenerator
    {
        private readonly List<string?> _errorMessages = [];
        private readonly List<string?> _stackTraces = [];

        public IReadOnlyList<string?> ErrorMessages
        {
            get
            {
                lock (_errorMessages)
                {
                    return [.. _errorMessages];
                }
            }
        }

        public IReadOnlyList<string?> StackTraces
        {
            get
            {
                lock (_stackTraces)
                {
                    return [.. _stackTraces];
                }
            }
        }

        public TestIdentity Generate(
            string fullyQualifiedName,
            string assembly,
            object[]? parameters = null,
            string? displayName = null,
            string? testCaseName = null,
            int? repeatIndex = null,
            string? testFingerprint = null) => new();

        public string GenerateTestFingerprint(string fullyQualifiedName, string? parameterHash = null) =>
            fullyQualifiedName;

        public string GenerateParameterHash(object[] parameters) => string.Empty;

        public string? GenerateErrorMessageHash(string? errorMessage)
        {
            lock (_errorMessages)
            {
                _errorMessages.Add(errorMessage);
            }

            return errorMessage == null ? null : "error-hash";
        }

        public string? GenerateStackTraceHash(string? stackTrace)
        {
            lock (_stackTraces)
            {
                _stackTraces.Add(stackTrace);
            }

            return stackTrace == null ? null : "stack-hash";
        }
    }
}
