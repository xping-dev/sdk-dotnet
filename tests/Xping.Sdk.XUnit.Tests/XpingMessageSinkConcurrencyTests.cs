/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;

namespace Xping.Sdk.XUnit.Tests;

/// <summary>
/// Regression tests for issue #120: parallelization used to be derived from the number of distinct
/// test collections seen so far, so every xUnit run with more than one test class reported
/// <c>WasParallelized = true</c> even when collection parallelization was disabled.
/// </summary>
[Collection("XpingContext")]
public sealed class XpingMessageSinkConcurrencyTests : IAsyncLifetime
{
    // Keep the shared XpingContext singleton shut down: the sink records every execution it builds
    // through XpingContext.RecordTest, and a live session would collect these fakes and try to
    // upload them (swallowed, but slow). The orchestration records are captured before that call.
    public Task InitializeAsync() => XpingContext.ShutdownAsync().AsTask();

    public Task DisposeAsync() => XpingContext.ShutdownAsync().AsTask();

    [Fact]
    public void Serial_TwoTestsInDifferentCollections_ShouldNotReportParallelization()
    {
        // Arrange
        (XpingMessageSink sink, RecordingExecutionTracker tracker) = CreateSink();
        ITest testA = BuildFakeTest("test-a", "CalculatorTests.Add", "SampleApp.XUnit.CalculatorTests");
        ITest testB = BuildFakeTest("test-b", "StringTests.Trim", "SampleApp.XUnit.StringTests");

        // Act — each test finishes before the next one starts
        Send(sink, Starting(testA));
        Send(sink, Passed(testA));
        Send(sink, Starting(testB));
        Send(sink, Passed(testB));

        // Assert
        Assert.Equal(2, tracker.Records.Count);
        Assert.All(tracker.Records, record =>
        {
            Assert.Equal(1, record.ConcurrentTestCount);
            Assert.False(record.WasParallelized);
        });
    }

    [Fact]
    public void Overlapping_TwoCollections_ShouldReportParallelization()
    {
        // Arrange
        (XpingMessageSink sink, RecordingExecutionTracker tracker) = CreateSink();
        ITest testA = BuildFakeTest("test-a", "CalculatorTests.Add", "SampleApp.XUnit.CalculatorTests");
        ITest testB = BuildFakeTest("test-b", "StringTests.Trim", "SampleApp.XUnit.StringTests");

        // Act — both collections are in flight at the same time
        Send(sink, Starting(testA));
        Send(sink, Starting(testB));
        Send(sink, Passed(testA));
        Send(sink, Passed(testB));

        // Assert
        Assert.Equal(2, tracker.Records.Count);
        Assert.All(tracker.Records, record =>
        {
            Assert.Equal(2, record.ConcurrentTestCount);
            Assert.True(record.WasParallelized);
        });
    }

    private static void Send(XpingMessageSink sink, IMessageSinkMessage message) =>
        ((IMessageSink)sink).OnMessage(message);

    /// <summary>
    /// Builds a sink backed by the real <see cref="IExecutionTracker"/> implementation, wrapped so the
    /// orchestration records it produces can be inspected.
    /// </summary>
    private static (XpingMessageSink sink, RecordingExecutionTracker tracker) CreateSink()
    {
        IExecutionTracker inner = new ServiceCollection()
            .AddXpingCollectors()
            .BuildServiceProvider()
            .GetRequiredService<IExecutionTracker>();

        RecordingExecutionTracker tracker = new(inner);

        var identityGenerator = new Mock<ITestIdentityGenerator>();
        identityGenerator
            .Setup(g => g.Generate(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object[]?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>()))
            .Returns(new TestIdentity());

        XpingMessageSink sink = new(
            Mock.Of<IMessageSink>(),
            tracker,
            new Mock<IRetryDetector<ITest>>().Object,
            identityGenerator.Object,
            NullLogger<XpingMessageSink>.Instance,
            captureStackTraces: true,
            assemblyName: "SampleApp.XUnit");

        return (sink, tracker);
    }

    private static ITestStarting Starting(ITest test)
    {
        var message = new Mock<ITestStarting>();
        message.SetupGet(m => m.Test).Returns(test);
        return message.Object;
    }

    private static ITestPassed Passed(ITest test)
    {
        var message = new Mock<ITestPassed>();
        message.SetupGet(m => m.Test).Returns(test);
        message.SetupGet(m => m.ExecutionTime).Returns(0.01m);
        message.SetupGet(m => m.Output).Returns(string.Empty);
        return message.Object;
    }

    /// <summary>
    /// Builds a fake xUnit <see cref="ITest"/> carrying the members the sink reads: the unique ID it
    /// keys pending tests by, and the collection display name it uses as the worker identifier.
    /// </summary>
    private static ITest BuildFakeTest(string uniqueId, string displayName, string collectionName)
    {
        var assemblyInfo = new Mock<IAssemblyInfo>();
        assemblyInfo.SetupGet(a => a.Name).Returns("SampleApp.XUnit");

        var classInfo = new Mock<ITypeInfo>();
        classInfo.SetupGet(c => c.Name).Returns(collectionName);
        classInfo.SetupGet(c => c.Assembly).Returns(assemblyInfo.Object);

        var methodInfo = new Mock<IMethodInfo>();
        methodInfo.SetupGet(m => m.Name).Returns(displayName);
        methodInfo.SetupGet(m => m.Type).Returns(classInfo.Object);

        var testCollection = new Mock<ITestCollection>();
        testCollection.SetupGet(c => c.DisplayName).Returns(collectionName);

        var testClass = new Mock<ITestClass>();
        testClass.SetupGet(c => c.Class).Returns(classInfo.Object);
        testClass.SetupGet(c => c.TestCollection).Returns(testCollection.Object);

        var testMethod = new Mock<ITestMethod>();
        testMethod.SetupGet(m => m.Method).Returns(methodInfo.Object);
        testMethod.SetupGet(m => m.TestClass).Returns(testClass.Object);

        var testCase = new Mock<ITestCase>();
        testCase.SetupGet(c => c.TestMethod).Returns(testMethod.Object);
        testCase.SetupGet(c => c.UniqueID).Returns(uniqueId);
        testCase.SetupGet(c => c.DisplayName).Returns(displayName);

        var test = new Mock<ITest>();
        test.SetupGet(t => t.TestCase).Returns(testCase.Object);
        test.SetupGet(t => t.DisplayName).Returns(displayName);

        return test.Object;
    }

    /// <summary>
    /// Delegates to a real <see cref="IExecutionTracker"/> while capturing every orchestration record
    /// it hands back, in the order the sink requested them.
    /// </summary>
    private sealed class RecordingExecutionTracker : IExecutionTracker
    {
        private readonly IExecutionTracker _inner;
        private readonly List<TestOrchestrationRecord> _records = [];

        public RecordingExecutionTracker(IExecutionTracker inner)
        {
            _inner = inner;
        }

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

        public int GlobalPosition => _inner.GlobalPosition;

        public int ActiveWorkerCount => _inner.ActiveWorkerCount;

        public TestOrchestrationRecord CreateExecutionContext(
            string? workerId = null,
            string? collectionName = null,
            int attemptNumber = 1)
        {
            TestOrchestrationRecord record = _inner.CreateExecutionContext(workerId, collectionName, attemptNumber);

            lock (_records)
            {
                _records.Add(record);
            }

            return record;
        }

        public int RecordTestStart(string? workerId = null) => _inner.RecordTestStart(workerId);

        public void RecordTestEnd(string? workerId = null) => _inner.RecordTestEnd(workerId);

        public void RecordTestCompletion(string? workerId, string testFingerprint, string testName, TestOutcome outcome) =>
            _inner.RecordTestCompletion(workerId, testFingerprint, testName, outcome);

        public int GetWorkerPosition(string? workerId = null) => _inner.GetWorkerPosition(workerId);

        public PrecedingTestRecord? GetPreviousTest(string? workerId = null) => _inner.GetPreviousTest(workerId);

        public void Clear() => _inner.Clear();
    }
}
