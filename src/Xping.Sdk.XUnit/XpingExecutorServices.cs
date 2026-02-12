/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xunit.Abstractions;

namespace Xping.Sdk.XUnit;

/// <summary>
/// Holds the resolved services required to construct <see cref="XpingTestFrameworkExecutor"/>.
/// Obtained once from the DI container during framework initialization.
/// </summary>
public sealed class XpingExecutorServices
{
    /// <summary>Gets the execution tracker.</summary>
    public IExecutionTracker ExecutionTracker { get; }

    /// <summary>Gets the retry detector.</summary>
    public IRetryDetector<ITest> RetryDetector { get; }

    /// <summary>Gets the test identity generator.</summary>
    public ITestIdentityGenerator IdentityGenerator { get; }

    /// <summary>Gets the logger for <see cref="XpingMessageSink"/>.</summary>
    public ILogger<XpingMessageSink> Logger { get; }

    internal XpingExecutorServices(
        IExecutionTracker executionTracker,
        IRetryDetector<ITest> retryDetector,
        ITestIdentityGenerator identityGenerator,
        ILogger<XpingMessageSink> logger)
    {
        ExecutionTracker  = executionTracker;
        RetryDetector     = retryDetector;
        IdentityGenerator = identityGenerator;
        Logger            = logger;
    }
}
