/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.PullRequests;
using Xping.Sdk.Core.Models.Statistics;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Collector.Internals;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Environment.Internals;
using Xping.Sdk.Core.Services.PullRequest;
using Xping.Sdk.Core.Services.PullRequest.Internals;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.Reporting.Internals;
using Xping.Sdk.Core.Services.Statistics;
using Xping.Sdk.Core.Services.Statistics.Internals;
using Xping.Sdk.Core.Services.Upload;
using Xping.Sdk.Core.Services.Upload.Internals;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core;

/// <summary>
/// Abstract base class that orchestrates the lifecycle of a test session, including initialization,
/// test execution recording, periodic flushing, and finalization with upload to the Xping platform.
/// </summary>
/// <remarks>
/// Derived classes implement framework-specific behavior by overriding the virtual lifecycle hooks.
/// Automatic flushing is triggered either on a configurable interval or when the collector buffer is full.
/// </remarks>
public abstract class XpingContextOrchestrator : IAsyncDisposable
{
    private readonly SemaphoreSlim _flushLock;
    private readonly Timer? _flushTimer;
    private readonly TestSessionBuilder _builder = new();
    private readonly IHost _host;
    private readonly ILogger<XpingContextOrchestrator> _logger;

    // _Host manages lifetime — disposing the host disposes all registered services.
#pragma warning disable CA2213
    private readonly ITestExecutionCollector _collector;
    private readonly IXpingUploader _uploader;
    private readonly IEnvironmentDetector _environmentDetector;
    private readonly IPullRequestContextDetector _prDetector;
    private readonly IRunningStatisticsAccumulator _statisticsAccumulator;
#pragma warning restore CA2213

    private readonly bool _isHealthy;
    private readonly XpingMode _mode;
    private readonly ILocalSessionStore? _localSessionStore;

    // Every execution drained during this session. Accumulated at drain time rather than in
    // RecordTestExecution, so the per-test hot path stays free of any local-store work.
    //
    // The executions are held whole rather than projected on arrival because the local store keeps
    // the untouched TestExecution: analysis that has to explain why a test is unreliable needs the
    // error text, stack traces and orchestration data a projection would drop.
    private readonly List<TestExecution> _localExecutions = [];
    private readonly object _localExecutionsLock = new();

    // Every test assembly this session has recorded an execution for, and the count of executions
    // that named none.
    //
    // Accumulated across flushes rather than derived per batch. A single flush sees only the
    // executions it drained, and the finalizing flush - the one carrying QuickStatistics and
    // closing the run - is assembled after the drain loop has emptied the collector, so it sees
    // none at all. A per-batch value would be empty on exactly the upload the platform needs it on.
    //
    // Mutated only inside BuildSessionAsync, which both call sites (FlushOnceAsync and
    // FinalFlushAsync) enter holding _flushLock, so this needs no lock of its own.
    private readonly SortedSet<string> _sessionAssemblies = new(StringComparer.Ordinal);
    private int _unattributedExecutionCount;

    // Whether a project was pinned via configuration. Captured in the constructor because the
    // finalize-time diagnostic below needs it and IOptions is not read again after startup.
    private bool _hasProjectPin;

    private PullRequestContext? _pullRequestContext;
    private EnvironmentInfo? _lastEnvironmentInfo;
    private QuickStatistics? _finalizedStatistics;
    private int _disposed;
    private int _finalized;
    private int _firstFlushDone;
    private volatile string? _strictModeNetworkError;

    // Fingerprint → first-seen display name. Used to detect duplicate [XpingFingerprint] values
    // across test methods within a single session.
    private readonly ConcurrentDictionary<string, string> _seenFingerprints = new();

    /// <summary>Gets the unique identifier for this test session.</summary>
    public Guid SessionId { get; }

    /// <summary>Gets the UTC timestamp when the session was initialized.</summary>
    public DateTime StartedAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the SDK initialized successfully with valid configuration.
    /// When false, all operations are no-ops and tests run without tracking.
    /// </summary>
    protected bool IsHealthy => _isHealthy;

    /// <summary>
    /// Gets the resolved operating mode for this session.
    /// </summary>
    protected XpingMode Mode => _mode;

    /// <summary>
    /// Gets a value indicating whether test executions are being collected.
    /// True in both <see cref="XpingMode.LocalOnly"/> and <see cref="XpingMode.Cloud"/> modes.
    /// </summary>
    /// <remarks>
    /// Collection is deliberately decoupled from upload. A missing API key must not stop the SDK from
    /// gathering data — that is precisely the case local-only mode exists to serve.
    /// </remarks>
    protected bool IsCollecting => _isHealthy && _mode != XpingMode.Disabled;

    /// <summary>
    /// Gets a value indicating whether the session will be uploaded to the Xping platform.
    /// True only in <see cref="XpingMode.Cloud"/> mode.
    /// </summary>
    protected bool IsUploading => _isHealthy && _mode == XpingMode.Cloud;

    /// <summary>
    /// Gets the service provider from the underlying host.
    /// Derived classes use this to resolve framework-specific services at composition time.
    /// </summary>
    protected IServiceProvider Services => _host.Services;

    /// <summary>
    /// Initializes a new instance of <see cref="XpingContextOrchestrator"/> from a configured <see cref="IHost"/>.
    /// Resolves all required services from the host's service container and stores the host for lifetime management.
    /// </summary>
    /// <param name="host">The configured host whose services satisfy all orchestrator dependencies.</param>
    protected XpingContextOrchestrator(IHost host)
    {
        _host = host.RequireNotNull();

        // Initialize session metadata
        SessionId = Guid.NewGuid();
        StartedAt = DateTime.UtcNow;

        var services = _host.Services;

        // Resolve the logger first, so it is available for error reporting during the rest of initialization.
        _logger = services.GetRequiredService<ILogger<XpingContextOrchestrator>>();
        _flushLock = new SemaphoreSlim(1, 1);

        try
        {
            // Resolving IXpingUploader triggers HttpClient creation, which in turn accesses
            // IOptions<XpingConfiguration>.Value and runs the registered Validate delegate.
            // An OptionsValidationException here means the caller supplied invalid configuration.
            _collector = services.GetRequiredService<ITestExecutionCollector>();
            _uploader = services.GetRequiredService<IXpingUploader>();
            _environmentDetector = services.GetRequiredService<IEnvironmentDetector>();
            _prDetector = services.GetRequiredService<IPullRequestContextDetector>();
            _statisticsAccumulator = services.GetRequiredService<IRunningStatisticsAccumulator>();
            XpingConfiguration configuration = services.GetRequiredService<IOptions<XpingConfiguration>>().Value;

            _mode = configuration.ResolveMode();
            _isHealthy = _mode != XpingMode.Disabled;
            _hasProjectPin = configuration.ProjectPin != null;

            // Optional so that hosts composed without the local-store feature still work.
            _localSessionStore = _mode == XpingMode.Disabled
                ? null
                : services.GetService<ILocalSessionStore>();

            // Only set up timer and events if healthy and enabled
            if (_isHealthy)
            {
                if (configuration.EnablePullRequestDetection)
                    _pullRequestContext = _prDetector.Detect();

                // The periodic flush exists to stream batches to the cloud during long runs.
                // In local-only mode there is nothing to stream: the session is written once at
                // finalization, so the timer would only wake up to drain into a no-op uploader.
                if (_mode == XpingMode.Cloud && configuration.FlushInterval > TimeSpan.Zero)
                {
                    _flushTimer = new Timer(
                        async void (_) =>
                        {
                            try { await FlushSessionAsync().ConfigureAwait(false); }
                            catch { /* Timer callbacks cannot propagate exceptions to callers */ }
                        },
                        state: null,
                        dueTime: configuration.FlushInterval,
                        period: configuration.FlushInterval);

                    _collector.BufferFull += async (_, _) =>
                    {
                        try { await FlushSessionAsync().ConfigureAwait(false); }
                        catch { /* BufferFull is an event; exceptions cannot propagate to callers */ }
                    };
                }

                if (_mode == XpingMode.LocalOnly)
                {
                    // Distinguish "fell back because nothing was configured" from "was told to stay
                    // local". Reporting a missing API key when one was supplied is actively confusing.
                    string reason = configuration.Mode == XpingMode.LocalOnly
                        ? "Mode is set to LocalOnly"
                        : "No API key configured";

                    _logger.LogInformation(
                        "Initialized in local-only mode. SessionId: {SessionId}, Environment: {Environment}. " +
                        "{Reason} - results stay on this machine.",
                        SessionId, configuration.Environment, reason);
                }
                else if (configuration.ProjectPin is { } projectPin)
                {
                    _logger.LogInformation(
                        "Initialized. SessionId: {SessionId}, Project: {ProjectId} (pinned), Environment: {Environment}",
                        SessionId, projectPin, configuration.Environment);
                }
                else
                {
                    // State which routing the run will get. Without this the developer cannot tell
                    // a derived project from a misconfigured one until results appear in Cloud.
                    _logger.LogInformation(
                        "Initialized. SessionId: {SessionId}, Environment: {Environment}. " +
                        "One project per test assembly - set ProjectId to report everything into one project instead.",
                        SessionId, configuration.Environment);
                }
            }
            else
            {
                _logger.LogWarning(
                    "SDK is disabled via configuration (Enabled=false). Tests will run without observability tracking.");
            }
        }
        catch (OptionsValidationException ex)
        {
            string message = $"Xping configuration invalid: {string.Join(", ", ex.Failures)}";

            if (IsStrictModeEnabled(services))
            {
                throw new XpingConfigurationException(message, ex);
            }

            _logger.LogError(
                "Configuration validation failed: {Errors}. SDK disabled - tests will run without observability tracking. ",
                string.Join("; ", ex.Failures));

            _isHealthy = false;
            _collector = new NoOpTestExecutionCollector();
            _uploader = new NoOpXpingUploader();
            _environmentDetector = new NoOpEnvironmentDetector();
            _prDetector = new NoOpPullRequestContextDetector();
            _statisticsAccumulator = new NoOpRunningStatisticsAccumulator();
        }
        catch (Exception ex)
        {
            if (IsStrictModeEnabled(services))
            {
                string message = $"Failed to initialize Xping SDK: {ex.Message}";
                throw new XpingConfigurationException(message, ex);
            }

            _logger.LogError(
                "Failed to initialize Xping SDK: {Message}. SDK disabled - tests will run without observability tracking.",
                ex.Message);

            _isHealthy = false;
            _collector = new NoOpTestExecutionCollector();
            _uploader = new NoOpXpingUploader();
            _environmentDetector = new NoOpEnvironmentDetector();
            _prDetector = new NoOpPullRequestContextDetector();
            _statisticsAccumulator = new NoOpRunningStatisticsAccumulator();
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when strict mode is enabled via either the <c>XPING_STRICTMODE</c>
    /// environment variable or the <c>Xping:StrictMode</c> configuration key
    /// (e.g. from appsettings.json or the <c>Xping__StrictMode</c> environment variable).
    /// This is checked at initialization time when configuration validation has already failed
    /// and <c>IOptions&lt;XpingConfiguration&gt;</c> cannot be used directly.
    /// </summary>
    private static bool IsStrictModeEnabled(IServiceProvider services)
    {
        // 1. Check XPING_STRICTMODE env var (PostConfigure-applied legacy format).
        string? envValue = System.Environment.GetEnvironmentVariable("XPING_STRICTMODE");
        if (bool.TryParse(envValue, out bool strictFromEnv) && strictFromEnv)
            return true;

        // 2. Check Xping:StrictMode in IConfiguration (appsettings.json, Xping__StrictMode env var, etc.).
        //    Reading IConfiguration does not trigger options validation, so this is safe even after
        //    an OptionsValidationException.
        IConfiguration? configuration = services.GetService<IConfiguration>();
        string? configValue = configuration?["Xping:StrictMode"];
        return bool.TryParse(configValue, out bool strictFromConfig) && strictFromConfig;
    }

    /// <summary>
    /// Drains the collector buffer, builds a test session snapshot, and uploads it to the Xping platform.
    /// Concurrent flush calls are serialized via an internal lock.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the upload operation.</returns>
    protected async Task<UploadResult> FlushSessionAsync(CancellationToken cancellationToken = default)
    {
        // Short-circuit when SDK is unhealthy or disabled
        if (!IsCollecting)
            return new UploadResult { Success = true, TotalRecordsCount = 0 };

        (UploadResult result, _) = await FlushOnceAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Performs a single drain-and-upload cycle, reporting both the upload result and the number of
    /// executions actually drained from the collector.
    /// </summary>
    /// <remarks>
    /// The drained count is reported separately because it is the only reliable measure of remaining
    /// work. <see cref="UploadResult.TotalRecordsCount"/> reflects what the *server* acknowledged, and
    /// is always zero for <c>NoOpXpingUploader</c> — so a caller that loops on it would drain a single
    /// batch and silently discard the rest of the buffer in any non-uploading mode.
    /// </remarks>
    private async Task<(UploadResult Result, int DrainedCount)> FlushOnceAsync(CancellationToken cancellationToken)
    {
        // Ensure only one flush happens at a time
        await _flushLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            TestSession session = await BuildSessionAsync(isFinalizing: false, cancellationToken).ConfigureAwait(false);
            int drainedCount = session.Executions.Count;
            UploadResult result = await UploadSessionAsync(session, cancellationToken).ConfigureAwait(false);

            // Only mark the first flush as done once data actually reaches the cloud.
            // Empty short-circuited flushes (e.g., timer firing before any tests run) must
            // not consume the Initial state, so the first real upload is always marked Initial.
            if (result.TotalRecordsCount > 0)
                Interlocked.CompareExchange(ref _firstFlushDone, 1, 0);

            return (result, drainedCount);
        }
        finally { _flushLock.Release(); }
    }

    /// <summary>
    /// Records a completed test execution into the collector buffer.
    /// </summary>
    /// <param name="execution">The test execution result to record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="execution"/> is <c>null</c>.</exception>
    protected void RecordTestExecution(TestExecution execution)
    {
        if (execution == null)
            throw new ArgumentNullException(nameof(execution));

        // Warn when two distinct test methods report the same fingerprint in a single session.
        // Retry attempts of the same test share both fingerprint and display name, so the
        // display-name equality check naturally excludes them from producing false warnings.
        string fingerprint = execution.Identity.TestFingerprint;
        string displayName = execution.Identity.DisplayName;

        if (!_seenFingerprints.TryAdd(fingerprint, displayName))
        {
            string existingName = _seenFingerprints[fingerprint];

            if (!string.Equals(existingName, displayName, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Duplicate test fingerprint detected: '{Fingerprint}'. " +
                    "First seen on '{FirstTest}', now reported for '{CurrentTest}'. " +
                    "On the Xping platform, results may overwrite each other. " +
                    "Rename one [XpingFingerprint] attribute value to make them unique.",
                    fingerprint, existingName, displayName);
            }
        }

        _collector.RecordTest(execution);
        OnTestExecutionRecorded(execution);
    }

    /// <summary>
    /// Finalizes the test session by flushing all buffered executions and uploading the session to the Xping platform.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the upload operation.</returns>
    /// <exception cref="XpingNetworkException">
    /// Thrown when the final upload fails and strict mode is enabled, to ensure CI pipelines fail fast
    /// when test observability data cannot be transmitted.
    /// </exception>
    protected async Task<UploadResult> FinalizeSessionAsync(CancellationToken cancellationToken = default)
    {
        // Idempotent: lifecycle hooks and upload run exactly once even if called multiple times.
        // If the previous attempt failed in strict mode, surface that failure to the caller
        // rather than masking it with Success=true.
        if (Interlocked.Exchange(ref _finalized, 1) != 0)
        {
            string? previousError = _strictModeNetworkError;
            if (previousError != null)
                return new UploadResult { Success = false, ErrorMessage = previousError };
            return new UploadResult { Success = true };
        }

        if (!IsCollecting)
            return new UploadResult { Success = true, TotalRecordsCount = 0 };

        await OnSessionFinalizingAsync(cancellationToken).ConfigureAwait(false);

        // Loop until the buffer is fully drained or an upload fails.
        // Retries are handled by the Polly resilience pipeline on the HttpClient
        // (configured via AddResilienceHandler in XpingServiceCollectionExtensions);
        // if Polly exhausts its budget, we stop immediately rather than re-draining.
        //
        // The loop tracks the *drained* count rather than the uploaded count: the two diverge in
        // local-only mode, where the no-op uploader always acknowledges zero records.
        UploadResult uploadResult;
        int drainedCount;
        do
        {
            (uploadResult, drainedCount) = await FlushOnceAsync(cancellationToken).ConfigureAwait(false);
        } while (drainedCount > 0 && uploadResult.Success);

        WarnAboutUnattributableExecutions();

        // Send the finalized session with QuickStatistics so the cloud can post the PR comment.
        uploadResult = await FinalFlushAsync(cancellationToken).ConfigureAwait(false);

        await OnSessionFinalizedAsync(uploadResult, cancellationToken).ConfigureAwait(false);

        WriteLocalHistory();

        if (!uploadResult.Success && IsStrictModeEnabled(Services))
        {
            string errorDetail = string.IsNullOrEmpty(uploadResult.ErrorMessage)
                ? "Upload failed with no additional details"
                : uploadResult.ErrorMessage!;
            string errorMessage = $"Xping network error in strict mode: {errorDetail}";
            _strictModeNetworkError = errorMessage;
            throw new XpingNetworkException(errorMessage);
        }

        return uploadResult;
    }

    /// <summary>
    /// Warns when executions cannot be attributed to a project.
    /// </summary>
    /// <remarks>
    /// Lives here rather than in the adapters because the assembly name is resolved per test in
    /// three different framework packages, none of which can reach the core SDK's logging, and
    /// because the interesting condition - "nothing in this whole run named an assembly" - is only
    /// knowable once the run is over.
    /// </remarks>
    private void WarnAboutUnattributableExecutions()
    {
        if (_mode != XpingMode.Cloud || _hasProjectPin)
            return;

        if (_unattributedExecutionCount > 0)
        {
            _logger.LogWarning(
                "{Count} executions carry no test assembly and cannot be attributed to a project.",
                _unattributedExecutionCount);
        }

        if (_sessionAssemblies.Count == 0 && _unattributedExecutionCount > 0)
        {
            _logger.LogWarning(
                "No test assembly could be resolved for this session, so no project can be derived " +
                "and the results will be rejected. Set ProjectId to pin a project, or report this " +
                "as an SDK defect.");
        }
    }

    /// <summary>
    /// Returns diagnostic statistics from the collector, such as the number of buffered and uploaded executions.
    /// </summary>
    /// <returns>A <see cref="CollectorStats"/> snapshot of current collector metrics.</returns>
    public Task<CollectorStats> GetCollectorStatsAsync()
    {
        return _collector.GetStatsAsync();
    }

    /// <summary>
    /// Builds the <see cref="EnvironmentInfo"/> describing the runtime environment for the current session.
    /// Override to provide additional or custom environment data.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>An <see cref="EnvironmentInfo"/> instance populated by the environment detector.</returns>
    protected virtual async Task<EnvironmentInfo> CreateEnvironmentInfoAsync(CancellationToken cancellationToken)
    {
        EnvironmentInfo environmentInfo = await _environmentDetector
            .BuildEnvironmentInfoAsync(cancellationToken)
            .ConfigureAwait(false);

        return environmentInfo;
    }

    /// <summary>
    /// Returns the total number of tests expected in this session, used for progress tracking.
    /// Returns <c>null</c> by default; override to provide a concrete value.
    /// </summary>
    /// <returns>The expected test count, or <c>null</c> if unknown.</returns>
    protected virtual int? GetTotalTestsExpected()
    {
        return null;
    }

    /// <summary>
    /// Called before the session is finalized and flushed. Override to perform pre-finalization work.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    protected virtual Task OnSessionFinalizingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called after the session has been finalized and uploaded. Override to handle post-upload logic,
    /// such as logging or reporting the upload result.
    /// </summary>
    /// <param name="result">The result returned by the upload operation.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    protected virtual Task OnSessionFinalizedAsync(
        UploadResult result,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called immediately after a test execution is recorded. Override to react to individual test results
    /// in real time, for example, to update progress counters or emit events.
    /// </summary>
    /// <param name="execution">The test execution that was just recorded.</param>
    protected virtual void OnTestExecutionRecorded(TestExecution execution)
    {
        _statisticsAccumulator.Record(execution);
    }

    /// <summary>
    /// Creates a pre-configured <see cref="IHostBuilder"/> with Xping services registered.
    /// Derived classes call this to get a base builder and optionally add framework-specific services
    /// before calling <see cref="IHostBuilder.Build"/>.
    /// </summary>
    /// <param name="configuration">
    /// Optional configuration to apply. When <c>null</c>, configuration is loaded from
    /// appsettings.json and environment variables.
    /// </param>
    /// <returns>A configured <see cref="IHostBuilder"/> ready to build.</returns>
    protected static IHostBuilder CreateHostBuilder(XpingConfiguration? configuration = null)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                if (configuration != null)
                    services.AddXping(configuration);
                else
                    services.AddXping();
            });
    }

    private async Task<TestSession> BuildSessionAsync(bool isFinalizing, CancellationToken cancellationToken)
    {
        _builder.Reset();

        TestSessionState sessionState = isFinalizing
            ? TestSessionState.Finalized
            : (_firstFlushDone == 0
                ? TestSessionState.Initial
                : TestSessionState.Partial);

        EnvironmentInfo? environmentInfo = await CreateEnvironmentInfoAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TestExecution> executions = _collector.Drain();
        int? totalTestsExpected = GetTotalTestsExpected();

        for (int i = 0; i < executions.Count; i++)
        {
            string assembly = executions[i].Identity.Assembly;

            if (string.IsNullOrEmpty(assembly))
                _unattributedExecutionCount++;
            else
                _sessionAssemblies.Add(assembly);
        }

        _lastEnvironmentInfo = environmentInfo;
        AccumulateLocalExecutions(executions);

        _builder
            .WithSessionId(SessionId)
            .WithStartedAt(StartedAt)
            .WithEndedAt(DateTime.UtcNow)
            .WithEnvironmentInfo(environmentInfo)
            .AddExecutions(executions)
            .WithAssemblies(_sessionAssemblies)
            .WithTotalTestsExpected(totalTestsExpected)
            .WithSessionState(sessionState)
            .WithPullRequestContext(_pullRequestContext);

        if (isFinalizing)
        {
            // Prefer the wall-clock-aware overload when the resolved accumulator supports it
            // (both built-in implementations do); this keeps IRunningStatisticsAccumulator's
            // public surface unchanged for any external implementers.
            QuickStatistics stats = _statisticsAccumulator is IWallClockAwareStatisticsAccumulator wallClockAware
                ? wallClockAware.GetSnapshot(DateTime.UtcNow - StartedAt)
                : _statisticsAccumulator.GetSnapshot();

            _builder.WithQuickStatistics(stats);
        }

        return _builder.Build();
    }

    private async Task<UploadResult> FinalFlushAsync(CancellationToken cancellationToken)
    {
        await _flushLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TestSession session = await BuildSessionAsync(isFinalizing: true, cancellationToken).ConfigureAwait(false);

            // Kept for the local store, which rebuilds its own session from the accumulated
            // executions and has no other source for the accumulated statistics.
            _finalizedStatistics = session.QuickStatistics;

            UploadResult result = await UploadSessionAsync(session, cancellationToken).ConfigureAwait(false);
            result.QuickStatistics = session.QuickStatistics;
            return result;
        }
        finally { _flushLock.Release(); }
    }

    private Task<UploadResult> UploadSessionAsync(
        TestSession session,
        CancellationToken cancellationToken)
    {
        return _uploader.UploadAsync(session, cancellationToken);
    }

    /// <summary>
    /// Retains drained executions for the local store.
    /// </summary>
    /// <remarks>
    /// Called once per flush rather than once per test. Doing this in
    /// <see cref="RecordTestExecution"/> would add cost to the per-test hot path, which is the one
    /// place the SDK cannot afford to grow.
    /// <para>
    /// This buffer is why the local store sees a complete run at all. The session built by
    /// <c>FinalFlushAsync</c> is assembled <i>after</i> the drain loop has emptied the collector, so
    /// its own <c>Executions</c> collection is empty; everything the local store writes comes from
    /// here.
    /// </para>
    /// </remarks>
    private void AccumulateLocalExecutions(IReadOnlyList<TestExecution> executions)
    {
        if (!IsLocalStoreEnabled || executions.Count == 0)
            return;

        lock (_localExecutionsLock)
        {
            for (int i = 0; i < executions.Count; i++)
                _localExecutions.Add(executions[i]);
        }
    }

    /// <summary>
    /// Persists the finished run to the local store and, when this run produced a retry flake,
    /// prints a single line pointing at the CLI.
    /// </summary>
    /// <remarks>
    /// The SDK does not analyse history or render reports. It states one fact about the run it just
    /// observed — how many tests failed and then passed on a retry — which is available from the
    /// executions already in memory and needs no store read. Everything historical is the CLI's job.
    /// </remarks>
    private void WriteLocalHistory()
    {
        if (!IsLocalStoreEnabled)
            return;

        List<TestExecution> executions;
        lock (_localExecutionsLock)
        {
            executions = [.. _localExecutions];
        }

        if (executions.Count == 0)
            return;

        EnvironmentInfo? environment = _lastEnvironmentInfo;

        bool stored = WriteLocalSession(executions, environment, DateTime.UtcNow);

        // Gated on the write because the hint points at `xping report`, and a run that was
        // never stored is a run that report cannot show.
        if (stored)
            WriteRetryFlakeHint(executions, environment?.IsCIEnvironment ?? false);
    }

    /// <summary>
    /// Persists the run as a complete <see cref="TestSession"/> for local analysis.
    /// </summary>
    /// <remarks>
    /// Rebuilt from the accumulated executions rather than reusing the session that was uploaded:
    /// that one is assembled after the collector has been drained and therefore carries no
    /// executions at all. Only <see cref="QuickStatistics"/> and the environment survive from it,
    /// and both are captured at finalization for exactly this purpose.
    /// </remarks>
    /// <returns><see langword="true"/> when the session was written.</returns>
    private bool WriteLocalSession(
        List<TestExecution> executions, EnvironmentInfo? environment, DateTime endedAt)
    {
        if (_localSessionStore == null)
            return false;

        try
        {
            TestSession session = new TestSessionBuilder()
                .WithSessionId(SessionId)
                .WithStartedAt(StartedAt)
                .WithEndedAt(endedAt)
                .WithEnvironmentInfo(WithRecordingMode(environment ?? new EnvironmentInfo()))
                .AddExecutions(executions)
                .WithAssemblies(_sessionAssemblies)
                .WithTotalTestsExpected(GetTotalTestsExpected())
                .WithSessionState(TestSessionState.Finalized)
                .WithPullRequestContext(_pullRequestContext)
                .WithQuickStatistics(_finalizedStatistics)
                .Build();

            return _localSessionStore.Write(session);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Local history is a side channel. It must never be the reason a test run fails.
            _logger.LogDebug("Local session not stored: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Returns a copy of <paramref name="environment"/> carrying the mode this session was recorded
    /// under.
    /// </summary>
    /// <remarks>
    /// The CLI needs to know whether a project is already cloud-connected so it does not pitch the
    /// cloud to people who pay for it, and the stored session is the only record of it. Applied here
    /// rather than in the environment detector so it lands on the local copy alone — the uploaded
    /// session keeps the environment exactly as detected.
    /// </remarks>
    private EnvironmentInfo WithRecordingMode(EnvironmentInfo environment)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);

        if (environment.CustomProperties != null)
        {
            foreach (KeyValuePair<string, string> property in environment.CustomProperties)
                properties[property.Key] = property.Value;
        }

        properties[LocalSessionProperties.Mode] = _mode.ToString();

        return new EnvironmentInfo
        {
            MachineName = environment.MachineName,
            OperatingSystem = environment.OperatingSystem,
            RuntimeVersion = environment.RuntimeVersion,
            Framework = environment.Framework,
            EnvironmentName = environment.EnvironmentName,
            IsCIEnvironment = environment.IsCIEnvironment,
            NetworkMetrics = environment.NetworkMetrics,
            UtcOffset = environment.UtcOffset,
            TimeZoneId = environment.TimeZoneId,
            CustomProperties = new ReadOnlyDictionary<string, string>(properties)
        };
    }

    /// <summary>
    /// Gets a value indicating whether anything is listening for local history this session.
    /// </summary>
    private bool IsLocalStoreEnabled => _localSessionStore != null;

    private static void WriteRetryFlakeHint(IReadOnlyList<TestExecution> executions, bool isCi)
    {
        string? hint = RetryFlakeHint.Build(executions, isCi, RetryFlakeHint.IsSuppressed());

        if (hint != null)
            TerminalWriter.Write(hint + System.Environment.NewLine);
    }

    /// <summary>
    /// Finalizes the session and releases all resources. Calls <see cref="FinalizeSessionAsync"/> to upload
    /// any remaining buffered executions before disposing the flush lock and timer.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Atomically claim disposal; subsequent calls are no-ops.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            // Stop the timer before finalizing so that no new periodic callbacks are
            // scheduled after this point. Any callback that is actively executing at
            // this moment is protected by _flushLock: it will either complete before
            // FinalizeSessionAsync acquires the lock, or block waiting for it and then
            // drain an already-empty buffer once FinalizeSessionAsync releases it.
            _flushTimer?.Dispose();

            await FinalizeSessionAsync().ConfigureAwait(false);
        }
        finally
        {
            _flushLock.Dispose();

            if (_host is IAsyncDisposable asyncHost)
                await asyncHost.DisposeAsync().ConfigureAwait(false);
            else
                _host.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
