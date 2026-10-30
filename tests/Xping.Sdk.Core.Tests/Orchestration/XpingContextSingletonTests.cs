/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Upload;
using Xping.Sdk.Core.Tests.Helpers;

namespace Xping.Sdk.Core.Tests.Orchestration;

public sealed class XpingContextSingletonTests
{
    private sealed class Harness(IHost host) : XpingContextOrchestrator(host)
    {
        public Action? OnFinalizing { get; set; }

        public int FinalizeCount { get; private set; }

        protected override string TestFramework => "Harness";

        public void Record() =>
            RecordTestExecution(new TestExecutionBuilder().WithTestName("Test").WithOutcome(TestOutcome.Passed).Build());

        protected override Task OnSessionFinalizingAsync(CancellationToken cancellationToken)
        {
            FinalizeCount++;
            OnFinalizing?.Invoke();
            return base.OnSessionFinalizingAsync(cancellationToken);
        }
    }

    private readonly List<XpingConfiguration?> _created = [];
    private readonly List<IHost> _hosts = [];
    private readonly Mock<IEnvironmentVariableProvider> _environment = new();

    // How many builds fail before one succeeds, as when appsettings.json does not parse.
    private int _failingBuilds;

    private XpingContextSingleton<Harness> CreateSingleton() => new(configuration =>
    {
        if (_failingBuilds > 0)
        {
            _failingBuilds--;
            throw new InvalidOperationException("host failed to build");
        }

        _created.Add(configuration);
        var uploader = new Mock<IXpingUploader>();
        var detector = new Mock<IEnvironmentDetector>();
        ServiceHelper.SetupDefaultMocks(uploader, detector);
        IHost host = ServiceHelper.BuildOrchestratorHost(uploader, detector);
        _hosts.Add(host);
        return new Harness(host);
    }, _environment.Object);

    [Fact]
    public void Initialize_DoesNotBuildTheContext()
    {
        var singleton = CreateSingleton();

        singleton.Initialize(null);

        Assert.True(singleton.IsInitialized);
        Assert.Null(singleton.Built);
        Assert.Empty(_created);
    }

    [Fact]
    public async Task Initialize_WhenAlreadyInitialized_KeepsTheInstalledContext()
    {
        var singleton = CreateSingleton();
        var first = new XpingConfiguration();
        singleton.Initialize(first);

        singleton.Initialize(new XpingConfiguration());
        _ = singleton.Instance;

        Assert.Same(first, Assert.Single(_created));
        await singleton.ShutdownAsync();
    }

    [Fact]
    public async Task Initialize_WithoutConfiguration_ReusesTheOneTheLastSessionRanWith()
    {
        var singleton = CreateSingleton();
        var configuration = new XpingConfiguration();
        singleton.Initialize(configuration);
        _ = singleton.Instance;
        await singleton.EndSessionAsync();

        singleton.Initialize(null);
        _ = singleton.Instance;
        await singleton.EndSessionAsync();

        Assert.Equal([configuration, configuration], _created);
    }

    [Fact]
    public async Task Initialize_WithConfigurationWhileAContextIsInstalled_DoesNotRememberIt()
    {
        var singleton = CreateSingleton();
        singleton.Initialize(null);
        singleton.Initialize(new XpingConfiguration());
        _ = singleton.Instance;
        await singleton.EndSessionAsync();

        singleton.Initialize(null);
        _ = singleton.Instance;
        await singleton.EndSessionAsync();

        Assert.Equal([null, null], _created);
    }

    [Fact]
    public async Task ShutdownAsync_ForgetsTheRememberedConfiguration()
    {
        var singleton = CreateSingleton();
        singleton.Initialize(new XpingConfiguration());
        _ = singleton.Instance;
        await singleton.ShutdownAsync();

        singleton.Initialize(null);
        _ = singleton.Instance;
        await singleton.ShutdownAsync();

        Assert.Null(_created[1]);
    }

    [Fact]
    public async Task Initialize_WithoutConfiguration_BeforeAnyConfigurationBuildsFromDefaults()
    {
        var singleton = CreateSingleton();

        singleton.Initialize(null);
        _ = singleton.Instance;
        await singleton.ShutdownAsync();

        Assert.Null(Assert.Single(_created));
    }

    [Fact]
    public void Instance_BeforeInitialize_ThrowsSayingToInitialize()
    {
        var singleton = CreateSingleton();

        var ex = Assert.Throws<InvalidOperationException>(() => singleton.Instance);

        Assert.Contains("Initialize()", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RecordTest_BeforeInitialize_ThrowsInvalidOperationException()
    {
        var singleton = CreateSingleton();

        Assert.Throws<InvalidOperationException>(() => singleton.RecordTest(
            new TestExecutionBuilder().WithTestName("Test").WithOutcome(TestOutcome.Passed).Build()));
    }

    [Fact]
    public async Task Instance_BuildsTheContextOnce()
    {
        var singleton = CreateSingleton();
        singleton.Initialize(null);

        Harness first = singleton.Instance;
        Harness second = singleton.Instance;

        Assert.Same(first, second);
        Assert.Same(first, singleton.Built);
        Assert.Single(_created);
        await singleton.ShutdownAsync();
    }

    [Fact]
    public async Task FinalizeAsync_WithAnUnbuiltContext_DoesNotBuildIt()
    {
        var singleton = CreateSingleton();
        singleton.Initialize(null);

        await singleton.FinalizeAsync();

        Assert.Empty(_created);
        Assert.True(singleton.IsInitialized);
    }

    [Fact]
    public async Task FinalizeAsync_FinalizesTheBuiltContextAndKeepsItInstalled()
    {
        var singleton = CreateSingleton();
        singleton.Initialize(null);
        singleton.RecordTest(new TestExecutionBuilder().WithTestName("Test").WithOutcome(TestOutcome.Passed).Build());

        await singleton.FinalizeAsync();

        Assert.Equal(1, singleton.Instance.FinalizeCount);
        Assert.True(singleton.IsInitialized);
        await singleton.ShutdownAsync();
    }

    [Fact]
    public async Task EndSessionAsync_WithAnUnbuiltContext_UninstallsItWithoutBuildingIt()
    {
        var singleton = CreateSingleton();
        singleton.Initialize(null);

        var runFailure = await singleton.EndSessionAsync();

        Assert.Null(runFailure);
        Assert.False(singleton.IsInitialized);
        Assert.Empty(_created);
    }

    [Fact]
    public async Task EndSessionAsync_FinalizesTheBuiltContextAndUninstallsIt()
    {
        var singleton = CreateSingleton();
        singleton.Initialize(null);
        Harness context = singleton.Instance;
        context.Record();

        var runFailure = await singleton.EndSessionAsync();

        Assert.Null(runFailure);
        Assert.Equal(1, context.FinalizeCount);
        Assert.False(singleton.IsInitialized);
    }

    [Fact]
    public async Task EndSessionAsync_ClaimsTheContextBeforeFinalizing_SoAContextInstalledMeanwhileSurvives()
    {
        var singleton = CreateSingleton();
        singleton.Initialize(null);
        Harness context = singleton.Instance;
        context.Record();
        context.OnFinalizing = () => singleton.Initialize(null);

        await singleton.EndSessionAsync();

        Assert.True(singleton.IsInitialized);
        Assert.NotSame(context, singleton.Instance);
        await singleton.ShutdownAsync();
    }

    [Fact]
    public async Task ShutdownAsync_DisposesTheContextAndAllowsInitializingAgain()
    {
        var singleton = CreateSingleton();
        singleton.Initialize(null);
        Harness first = singleton.Instance;

        await singleton.ShutdownAsync();

        Assert.False(singleton.IsInitialized);
        Assert.Throws<ObjectDisposedException>(() => Assert.Single(_hosts).Services.GetService<IXpingUploader>());

        singleton.Initialize(null);
        Assert.NotSame(first, singleton.Instance);
        await singleton.ShutdownAsync();
    }

    [Fact]
    public async Task Start_InitializesBuildsAndResolves()
    {
        var singleton = CreateSingleton();

        Harness? resolved = singleton.Start(context => context, report: null, retryNextRun: false);

        Assert.Same(singleton.Instance, resolved);
        await singleton.ShutdownAsync();
    }

    [Fact]
    public void Start_HostFailsToBuild_ReturnsNullAndReportsWhy()
    {
        var singleton = CreateSingleton();
        _failingBuilds = 1;
        var reports = new List<string>();

        Harness? resolved = singleton.Start(context => context, reports.Add, retryNextRun: false);

        Assert.Null(resolved);
        Assert.Contains("[Xping] SDK initialization failed: host failed to build", Assert.Single(reports), StringComparison.Ordinal);
    }

    /// <summary>
    /// NUnit and MSTest start before every test, and their session still ends: a failure must hold until
    /// then, not be rebuilt and reported for each test.
    /// </summary>
    [Fact]
    public async Task Start_AfterAFailedStart_ReturnsNullWithoutRetryingUntilTheSessionEnds()
    {
        var singleton = CreateSingleton();
        _failingBuilds = 1;
        var reports = new List<string>();
        singleton.Start(context => context, reports.Add, retryNextRun: false);

        Harness? again = singleton.Start(context => context, reports.Add, retryNextRun: false);

        Assert.Null(again);
        Assert.Single(reports);
        Assert.True(singleton.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => singleton.Instance);
        Assert.Null(singleton.Built);

        Assert.Null(await singleton.EndSessionAsync());
        Assert.NotNull(singleton.Start(context => context, reports.Add, retryNextRun: false));
        await singleton.ShutdownAsync();
    }

    /// <summary>
    /// xUnit's session ends in a sink that only a started SDK installs, so nothing else would drop the
    /// failed context, and every later run in a long-lived process would stay untracked.
    /// </summary>
    [Fact]
    public async Task Start_RetryingNextRun_UninstallsTheFailedContextSoTheNextStartBuildsAgain()
    {
        var singleton = CreateSingleton();
        var configuration = new XpingConfiguration();
        singleton.Initialize(configuration);
        _failingBuilds = 1;

        Assert.Null(singleton.Start(context => context, report: null, retryNextRun: true));
        Assert.False(singleton.IsInitialized);

        Assert.NotNull(singleton.Start(context => context, report: null, retryNextRun: true));
        Assert.Same(configuration, Assert.Single(_created));
        await singleton.ShutdownAsync();
    }

    [Fact]
    public void Start_ResolvingFailsAfterTheHostBuilt_LeavesNoContextToFinalize()
    {
        var singleton = CreateSingleton();

        XpingConfiguration? resolved = singleton.Start<XpingConfiguration>(
            _ => throw new InvalidOperationException("service missing"), report: null, retryNextRun: false);

        Assert.Null(resolved);
        Assert.Null(singleton.Built);
    }

    [Fact]
    public void Start_HostFailsToBuildWithStrictModeSetInCode_Throws()
    {
        var singleton = CreateSingleton();
        singleton.Initialize(new XpingConfiguration { StrictMode = true });
        _failingBuilds = 1;

        var ex = Assert.Throws<XpingConfigurationException>(
            () => singleton.Start(context => context, report: null, retryNextRun: false));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Theory]
    [InlineData("XPING_STRICTMODE")]
    [InlineData("Xping__StrictMode")]
    public void Start_HostFailsToBuildWithStrictModeFromTheEnvironment_Throws(string variable)
    {
        var singleton = CreateSingleton();
        _environment.Setup(e => e.GetVariable(variable)).Returns("true");
        _failingBuilds = 1;

        Assert.Throws<XpingConfigurationException>(
            () => singleton.Start(context => context, report: null, retryNextRun: false));
    }

    [Fact]
    public void Start_ContextRejectsItsConfiguration_RethrowsTheConfigurationError()
    {
        var expected = new XpingConfigurationException("invalid");
        var singleton = new XpingContextSingleton<Harness>(_ => throw expected, _environment.Object);

        var ex = Assert.Throws<XpingConfigurationException>(
            () => singleton.Start(context => context, report: null, retryNextRun: false));

        Assert.Same(expected, ex);
    }
}
