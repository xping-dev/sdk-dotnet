/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Upload;

namespace Xping.Sdk.Core.Tests.Helpers;

/// <summary>
/// Shared factory helpers for building DI containers and host instances in tests.
/// </summary>
internal static class ServiceHelper
{
    /// <summary>
    /// Creates a <see cref="ServiceCollection"/> pre-configured with a valid
    /// <see cref="XpingConfiguration"/> and the collector services registered.
    /// </summary>
    public static ServiceCollection CreateCollectorServices(Action<XpingConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.Configure<XpingConfiguration>(o =>
        {
            o.ApiKey = "test-key";
            o.ProjectId = "test-project";
            o.BatchSize = 100;
            o.SamplingRate = 1.0;
            o.Enabled = true;
            configure?.Invoke(o);
        });
        services.AddXpingCollectors();
        return services;
    }

    /// <summary>
    /// Builds an <see cref="ITestExecutionCollector"/> from DI with the supplied configuration override.
    /// </summary>
    public static ITestExecutionCollector BuildCollector(Action<XpingConfiguration>? configure = null)
    {
        var provider = CreateCollectorServices(configure).BuildServiceProvider();
        return provider.GetRequiredService<ITestExecutionCollector>();
    }

    /// <summary>
    /// Builds an <see cref="IExecutionTracker"/> from DI.
    /// </summary>
    public static IExecutionTracker BuildTracker()
    {
        var provider = CreateCollectorServices().BuildServiceProvider();
        return provider.GetRequiredService<IExecutionTracker>();
    }

    /// <summary>
    /// Builds an <see cref="IHost"/> wired with mock uploader and environment detector.
    /// The flush interval is set to zero by default to suppress the background timer.
    /// </summary>
    public static IHost BuildOrchestratorHost(
        Mock<IXpingUploader> uploaderMock,
        Mock<IEnvironmentDetector> envDetectorMock,
        Action<XpingConfiguration>? configure = null)
    {
        return new HostBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<XpingConfiguration>(o =>
                {
                    o.ApiKey = "test-key";
                    o.ProjectId = "test-project";
                    o.BatchSize = 100;
                    o.SamplingRate = 1.0;
                    o.Enabled = true;
                    o.FlushInterval = TimeSpan.Zero; // disable background timer by default
                    configure?.Invoke(o);
                });
                services.AddXpingCollectors();
                services.AddXpingInfrastructure();
                services.AddSingleton(uploaderMock.Object);
                services.AddSingleton(envDetectorMock.Object);
            })
            .Build();
    }

    /// <summary>
    /// Configures standard mock returns: uploader returns success with execution count
    /// based on the number of executions in the session; environment detector returns
    /// an empty <see cref="EnvironmentInfo"/>.
    /// </summary>
    public static void SetupDefaultMocks(
        Mock<IXpingUploader> uploaderMock,
        Mock<IEnvironmentDetector> envDetectorMock)
    {
        envDetectorMock
            .Setup(e => e.BuildEnvironmentInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnvironmentInfo());

        // Return success with ExecutionCount == session.Executions.Count so the
        // finalization drain loop terminates correctly.
        uploaderMock
            .Setup(u => u.UploadAsync(It.IsAny<Models.TestSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Models.TestSession s, CancellationToken _) => new UploadResult
            {
                Success = true,
                TotalRecordsCount = s.Executions.Count
            });
    }

    /// <summary>
    /// Builds an <see cref="IHost"/> wired with mock uploader and environment detector,
    /// but with deliberately invalid configuration (missing ApiKey and ProjectId) so that
    /// <see cref="Microsoft.Extensions.Options.OptionsValidationException"/> is thrown when
    /// <c>IOptions&lt;XpingConfiguration&gt;.Value</c> is accessed during orchestrator construction.
    /// </summary>
    public static IHost BuildInvalidConfigHost()
    {
        var uploaderMock = new Mock<IXpingUploader>();
        var envDetectorMock = new Mock<IEnvironmentDetector>();
        SetupDefaultMocks(uploaderMock, envDetectorMock);

        return new HostBuilder()
            .ConfigureServices(services =>
            {
                // Configure with empty/invalid values — ApiKey and ProjectId are left null.
                services.Configure<XpingConfiguration>(o =>
                {
                    o.ApiKey = null!;
                    o.ProjectId = null!;
                    o.Enabled = true;
                    o.FlushInterval = TimeSpan.Zero;
                });

                // Register validation so OptionsValidationException fires on .Value access.
                services.AddOptions<XpingConfiguration>().ValidateDataAnnotations();

                services.AddXpingCollectors();
                services.AddXpingInfrastructure();
                services.AddSingleton(uploaderMock.Object);
                services.AddSingleton(envDetectorMock.Object);
            })
            .Build();
    }
}
