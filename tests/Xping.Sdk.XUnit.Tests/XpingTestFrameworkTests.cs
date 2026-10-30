/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Moq;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Identity;
using Xping.Sdk.Core.Services.Retry;
using Xping.Sdk.Core.Services.Statistics;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xping.Sdk.XUnit.Tests;

public sealed class XpingTestFrameworkTests
{
    [Fact]
    public void GetExecutor_ServicesFailedToStart_ReturnsXunitsOwnExecutor()
    {
        using var framework = new XpingTestFramework(
            new NullMessageSink(), () => throw new InvalidOperationException("host failed to start"));

        using ITestFrameworkExecutor executor = framework.GetExecutor(typeof(XpingTestFrameworkTests).Assembly.GetName());

        Assert.IsType<XunitTestFrameworkExecutor>(executor);
    }

    [Fact]
    public void GetExecutor_ServicesStarted_ReturnsXpingExecutor()
    {
        var services = new XpingExecutorServices(
            Mock.Of<IExecutionTracker>(),
            Mock.Of<IRetryDetector<ITest>>(),
            Mock.Of<ITestIdentityGenerator>(),
            captureStackTraces: false,
            Mock.Of<IRunningStatisticsAccumulator>());
        using var framework = new XpingTestFramework(new NullMessageSink(), () => services);

        using ITestFrameworkExecutor executor = framework.GetExecutor(typeof(XpingTestFrameworkTests).Assembly.GetName());

        Assert.IsType<XpingTestFrameworkExecutor>(executor);
    }
}
