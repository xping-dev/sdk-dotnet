/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.Core.Services.Statistics;
using Xunit.Abstractions;

namespace Xping.Sdk.XUnit;

/// <summary>
/// Holds the resolved services required to construct <see cref="XpingTestFrameworkExecutor"/>.
/// Obtained once from the DI container during framework initialization.
/// </summary>
internal sealed class XpingExecutorServices
{
    /// <summary>Gets the execution tracker.</summary>
    public IExecutionTracker ExecutionTracker { get; }

    /// <summary>Gets the retry detector.</summary>
    public IRetryDetector<ITest> RetryDetector { get; }

    /// <summary>Gets the test identity generator.</summary>
    public ITestIdentityGenerator IdentityGenerator { get; }

    /// <summary>Gets a value indicating whether stack traces should be captured.</summary>
    public bool CaptureStackTraces { get; }

    /// <summary>Gets the statistics accumulator the executor reports discovered and selected test cases to.</summary>
    public IRunningStatisticsAccumulator StatisticsAccumulator { get; }

    internal XpingExecutorServices(
        IExecutionTracker executionTracker,
        IRetryDetector<ITest> retryDetector,
        ITestIdentityGenerator identityGenerator,
        bool captureStackTraces,
        IRunningStatisticsAccumulator statisticsAccumulator)
    {
        ExecutionTracker  = executionTracker;
        RetryDetector     = retryDetector;
        IdentityGenerator = identityGenerator;
        CaptureStackTraces = captureStackTraces;
        StatisticsAccumulator = statisticsAccumulator;
    }
}
