/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Extensions;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Collector;
using Xping.Sdk.Core.Services.Environment;
using Xping.Sdk.Core.Services.Upload;
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
#pragma warning restore CA2213

    private int _disposed;
    private int _finalized;

    /// <summary>Gets the unique identifier for this test session.</summary>
    public Guid SessionId { get; }

    /// <summary>Gets the UTC timestamp when the session was initialized.</summary>
    public DateTime StartedAt { get; private set; }

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

        _collector = services.GetRequiredService<ITestExecutionCollector>();
        _flushLock = new SemaphoreSlim(1, 1);

        XpingConfiguration configuration;
        try
        {
            // Resolving IXpingUploader triggers HttpClient creation, which in turn accesses
            // IOptions<XpingConfiguration>.Value and runs the registered Validate delegate.
            // An OptionsValidationException here means the caller supplied invalid configuration.
            _uploader = services.GetRequiredService<IXpingUploader>();
            _environmentDetector = services.GetRequiredService<IEnvironmentDetector>();
            configuration = services.GetRequiredService<IOptions<XpingConfiguration>>().Value;
        }
        catch (OptionsValidationException ex)
        {
            _logger.LogError("Configuration validation failed: {Errors}", string.Join("; ", ex.Failures));
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to initialize Xping SDK: {Message}", ex.Message);
            throw;
        }

        // Set up the automatic flush timer if enabled
        if (configuration.Enabled && configuration.FlushInterval > TimeSpan.Zero)
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
        }

        // Subscribe to the buffer full event to flush the collector if enabled.
        // The handler must be async void (EventHandler delegate returns void); the inner
        // try/catch prevents unhandled exceptions from reaching the thread pool and crashing the process.
        if (configuration.Enabled)
            _collector.BufferFull += async (_, _) =>
            {
                try { await FlushSessionAsync().ConfigureAwait(false); }
                catch { /* BufferFull is an event; exceptions cannot propagate to callers */ }
            };

        _logger.LogInformation("Initialized. Project: {ProjectId}, Environment: {Environment}, Enabled: {Enabled}",
            configuration.ProjectId, configuration.Environment, configuration.Enabled);
    }

    /// <summary>
    /// Drains the collector buffer, builds a test session snapshot, and uploads it to the Xping platform.
    /// Concurrent flush calls are serialized via an internal lock.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the upload operation.</returns>
    protected async Task<UploadResult> FlushSessionAsync(CancellationToken cancellationToken = default)
    {
        // Ensure only one flush happens at a time
        await _flushLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            TestSession session = await BuildSessionAsync(cancellationToken).ConfigureAwait(false);
            UploadResult result = await UploadSessionAsync(session, cancellationToken).ConfigureAwait(false);

            return result;
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

        _collector.RecordTest(execution);
        OnTestExecutionRecorded(execution);
    }

    /// <summary>
    /// Finalizes the test session by flushing all buffered executions and uploading the session to the Xping platform.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the upload operation.</returns>
    protected async Task<UploadResult> FinalizeSessionAsync(CancellationToken cancellationToken = default)
    {
        // Idempotent: lifecycle hooks and upload run exactly once even if called multiple times.
        if (Interlocked.Exchange(ref _finalized, 1) != 0)
            return new UploadResult { Success = true };

        await OnSessionFinalizingAsync(cancellationToken).ConfigureAwait(false);

        // Loop until the buffer is fully drained. The sentinel for "nothing left to drain" is
        // { Success = true, TotalRecordsCount = 0 }, which the uploader returns when Drain() gave
        // it an empty session. A failed upload also returns TotalRecordsCount = 0, but Success =
        // false — items were already dequeued, so we keep looping to drain remaining batches
        // rather than silently abandoning them.
        UploadResult uploadResult;
        do
        {
            uploadResult = await FlushSessionAsync(cancellationToken).ConfigureAwait(false);
        } while (uploadResult.TotalRecordsCount > 0 || !uploadResult.Success);

        await OnSessionFinalizedAsync(uploadResult, cancellationToken).ConfigureAwait(false);

        return uploadResult;
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

    private async Task<TestSession> BuildSessionAsync(CancellationToken cancellationToken)
    {
        _builder.Reset();

        EnvironmentInfo? environmentInfo = await CreateEnvironmentInfoAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TestExecution> executions = _collector.Drain();
        int? totalTestsExpected = GetTotalTestsExpected();

        return _builder
            .WithSessionId(SessionId)
            .WithStartedAt(StartedAt)
            .WithEndedAt(DateTime.UtcNow)
            .WithEnvironmentInfo(environmentInfo)
            .AddExecutions(executions)
            .WithTotalTestsExpected(totalTestsExpected)
            .Build();
    }

    private Task<UploadResult> UploadSessionAsync(
        TestSession session,
        CancellationToken cancellationToken)
    {
        return _uploader.UploadAsync(session, cancellationToken);
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
