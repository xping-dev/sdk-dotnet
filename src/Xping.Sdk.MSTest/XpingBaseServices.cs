/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;

namespace Xping.Sdk.MSTest;

/// <summary>
/// Holds the resolved services required by <see cref="XpingTestBase"/> to record test executions.
/// Obtained once from the DI container after <see cref="XpingContext.Initialize()"/> has been called.
/// </summary>
public sealed class XpingBaseServices
{
    /// <summary>Gets the execution tracker.</summary>
    public IExecutionTracker ExecutionTracker { get; }

    /// <summary>Gets the MSTest-specific retry detector.</summary>
    public IRetryDetector<TestContext> RetryDetector { get; }

    /// <summary>Gets the test identity generator.</summary>
    public ITestIdentityGenerator IdentityGenerator { get; }

    internal XpingBaseServices(
        IExecutionTracker executionTracker,
        IRetryDetector<TestContext> retryDetector,
        ITestIdentityGenerator identityGenerator)
    {
        ExecutionTracker  = executionTracker;
        RetryDetector     = retryDetector;
        IdentityGenerator = identityGenerator;
    }
}
