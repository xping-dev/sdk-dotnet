/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using NUnit.Framework.Interfaces;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;

namespace Xping.Sdk.NUnit;

/// <summary>
/// Holds the resolved services required by <see cref="XpingTrackAttribute"/> to record test executions.
/// Obtained once from the DI container after <see cref="XpingContext.Initialize()"/> has been called.
/// </summary>
public sealed class XpingAttributeServices
{
    /// <summary>Gets the execution tracker.</summary>
    public IExecutionTracker ExecutionTracker { get; }

    /// <summary>Gets the NUnit-specific retry detector.</summary>
    public IRetryDetector<ITest> RetryDetector { get; }

    /// <summary>Gets the test identity generator.</summary>
    public ITestIdentityGenerator IdentityGenerator { get; }

    internal XpingAttributeServices(
        IExecutionTracker executionTracker,
        IRetryDetector<ITest> retryDetector,
        ITestIdentityGenerator identityGenerator)
    {
        ExecutionTracker  = executionTracker;
        RetryDetector     = retryDetector;
        IdentityGenerator = identityGenerator;
    }
}
