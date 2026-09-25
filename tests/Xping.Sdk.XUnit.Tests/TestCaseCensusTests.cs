/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Moq;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.Core.Services.Statistics;
using Xunit.Abstractions;

namespace Xping.Sdk.XUnit.Tests;

public sealed class TestCaseCensusTests
{
    // ---------------------------------------------------------------------------
    // TestCaseCensus
    // ---------------------------------------------------------------------------

    [Fact]
    public void Discovered_BeforeAnyDiscoveryCompletes_IsNullNotZero()
    {
        Assert.Null(new TestCaseCensus().Discovered);
    }

    [Fact]
    public void Discovered_CompletedWithNothingFound_IsZeroNotNull()
    {
        var census = new TestCaseCensus();
        census.CompleteDiscovery(0);

        Assert.Equal(0, census.Discovered);
    }

    [Fact]
    public void Discovered_ALaterDiscoveryReplacesAnEarlierOne()
    {
        // Each search reports every case once, so a second search is a recount, not more cases.
        var census = new TestCaseCensus();
        census.CompleteDiscovery(17);
        census.CompleteDiscovery(17);

        Assert.Equal(17, census.Discovered);
    }

    [Fact]
    public void Selected_AddsUpAcrossRuns()
    {
        var census = new TestCaseCensus();
        census.AddSelected(3);
        census.AddSelected(2);

        Assert.Equal(5, census.Selected);
    }

    // ---------------------------------------------------------------------------
    // XpingTestFrameworkDiscoverer
    // ---------------------------------------------------------------------------

    [Fact]
    public void Find_WholeAssembly_CountsDiscoveredCasesAndForwardsEveryMessage()
    {
        // Arrange
        var census = new TestCaseCensus();
        var runnerSink = new RecordingSink();
        IMessageSinkMessage[] messages = [Discovered("a"), Discovered("b"), Discovered("c"), DiscoveryComplete()];
        using var discoverer = new XpingTestFrameworkDiscoverer(Emitting(messages), census);

        // Act
        discoverer.Find(false, runnerSink, Mock.Of<ITestFrameworkDiscoveryOptions>());

        // Assert
        Assert.Equal(3, census.Discovered);
        Assert.Equal(messages, runnerSink.Messages);
    }

    [Fact]
    public void Find_StoppedByTheRunner_RecordsNoCount()
    {
        // xUnit stops enumerating classes once the runner answers false, but still reports discovery
        // complete. What it found by then is part of the assembly, and a short count would be read
        // as a fact where a missing one falls back to the threshold.
        var census = new TestCaseCensus();
        using var discoverer = new XpingTestFrameworkDiscoverer(
            Emitting([Discovered("a"), Discovered("b"), DiscoveryComplete()]), census);

        discoverer.Find(false, new RecordingSink(stopAfter: 1), Mock.Of<ITestFrameworkDiscoveryOptions>());

        Assert.Null(census.Discovered);
    }

    [Fact]
    public void Find_OneClass_IsNotACensusOfTheAssembly()
    {
        // Arrange
        var census = new TestCaseCensus();
        using var discoverer = new XpingTestFrameworkDiscoverer(
            Emitting([Discovered("a"), DiscoveryComplete()]), census);

        // Act
        discoverer.Find("Some.Class", false, new RecordingSink(), Mock.Of<ITestFrameworkDiscoveryOptions>());

        // Assert
        Assert.Null(census.Discovered);
    }

    // ---------------------------------------------------------------------------
    // XpingTestFrameworkExecutor
    // ---------------------------------------------------------------------------

    [Fact]
    public void RunTests_AfterWholeAssemblyDiscovery_ReportsDiscoveredAndSelected()
    {
        // Arrange
        var census = new TestCaseCensus();
        census.CompleteDiscovery(3);
        var accumulator = new Mock<IRunningStatisticsAccumulator>();
        using XpingTestFrameworkExecutor executor = Executor(accumulator.Object, census);

        // Act
        executor.RunTests([], new RecordingSink(), Mock.Of<ITestFrameworkExecutionOptions>());

        // Assert
        accumulator.Verify(a => a.RecordTestCases(AssemblyName.Name!, 3, 0), Times.Once);
    }

    [Fact]
    public void RunTests_WithoutDiscoveryInThisProcess_ReportsSelectedAlone()
    {
        // Arrange
        var accumulator = new Mock<IRunningStatisticsAccumulator>();
        using XpingTestFrameworkExecutor executor = Executor(accumulator.Object, new TestCaseCensus());

        // Act
        executor.RunTests([], new RecordingSink(), Mock.Of<ITestFrameworkExecutionOptions>());

        // Assert
        accumulator.Verify(a => a.RecordTestCases(AssemblyName.Name!, null, 0), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static readonly AssemblyName AssemblyName = typeof(TestCaseCensusTests).Assembly.GetName();

    private static XpingTestFrameworkExecutor Executor(IRunningStatisticsAccumulator accumulator, TestCaseCensus census) =>
        new(
            AssemblyName,
            Mock.Of<ISourceInformationProvider>(),
            new RecordingSink(),
            Mock.Of<IExecutionTracker>(),
            Mock.Of<IRetryDetector<ITest>>(),
            Mock.Of<ITestIdentityGenerator>(),
            captureStackTraces: false,
            accumulator,
            census);

    private static ITestFrameworkDiscoverer Emitting(IMessageSinkMessage[] messages)
    {
        var inner = new Mock<ITestFrameworkDiscoverer>();
        void Emit(IMessageSink sink)
        {
            foreach (IMessageSinkMessage message in messages)
                sink.OnMessage(message);
        }

        inner.Setup(d => d.Find(It.IsAny<bool>(), It.IsAny<IMessageSink>(), It.IsAny<ITestFrameworkDiscoveryOptions>()))
            .Callback<bool, IMessageSink, ITestFrameworkDiscoveryOptions>((_, sink, _) => Emit(sink));
        inner.Setup(d => d.Find(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<IMessageSink>(), It.IsAny<ITestFrameworkDiscoveryOptions>()))
            .Callback<string, bool, IMessageSink, ITestFrameworkDiscoveryOptions>((_, _, sink, _) => Emit(sink));
        return inner.Object;
    }

    private static ITestCaseDiscoveryMessage Discovered(string uniqueId) =>
        Mock.Of<ITestCaseDiscoveryMessage>(m => m.TestCase == Mock.Of<ITestCase>(c => c.UniqueID == uniqueId));

    private static IDiscoveryCompleteMessage DiscoveryComplete() => Mock.Of<IDiscoveryCompleteMessage>();

    private sealed class RecordingSink(int stopAfter = int.MaxValue) : LongLivedMarshalByRefObject, IMessageSink
    {
        public List<IMessageSinkMessage> Messages { get; } = [];

        public bool OnMessage(IMessageSinkMessage message)
        {
            Messages.Add(message);
            return Messages.Count < stopAfter;
        }
    }
}
