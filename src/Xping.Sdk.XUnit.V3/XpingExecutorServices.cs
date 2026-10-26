/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.Core.Services.Statistics;
using Xunit.v3;

namespace Xping.Sdk.XUnit.V3;

/// <summary>
/// Holds the resolved services required to construct <see cref="XpingTestFrameworkExecutor"/>.
/// Obtained once from the DI container when the framework creates its executor.
/// </summary>
internal sealed class XpingExecutorServices(
    IExecutionTracker executionTracker,
    IRetryDetector<IXunitTestCase> retryDetector,
    ITestIdentityGenerator identityGenerator,
    bool captureStackTraces,
    IRunningStatisticsAccumulator statisticsAccumulator)
{
    /// <summary>Gets the execution tracker.</summary>
    public IExecutionTracker ExecutionTracker { get; } = executionTracker;

    /// <summary>Gets the retry detector.</summary>
    public IRetryDetector<IXunitTestCase> RetryDetector { get; } = retryDetector;

    /// <summary>Gets the test identity generator.</summary>
    public ITestIdentityGenerator IdentityGenerator { get; } = identityGenerator;

    /// <summary>Gets a value indicating whether stack traces should be captured.</summary>
    public bool CaptureStackTraces { get; } = captureStackTraces;

    /// <summary>Gets the statistics accumulator the executor reports discovered and selected test cases to.</summary>
    public IRunningStatisticsAccumulator StatisticsAccumulator { get; } = statisticsAccumulator;
}
