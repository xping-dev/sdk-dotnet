/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Collection;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Upload;

/// <summary>
/// Thread-safe collector for test execution data with automatic batching and flushing.
/// </summary>
public sealed class TestExecutionCollector : ITestExecutionCollector
{
    private readonly ConcurrentQueue<TestExecution> _buffer;
    private readonly ITestResultUploader _uploader;
    private readonly XpingConfiguration _config;
    private readonly Timer? _flushTimer;
    private readonly SemaphoreSlim _flushLock;
    private readonly Random _random;
    private readonly object _statsLock = new();
    private readonly ConcurrentDictionary<string, bool> _uploadedSessions; // Track uploaded sessions

    private TestSession? _currentSession;
    private long _totalRecorded;
    private long _totalUploaded;
    private long _totalFailed;
    private long _totalSampled;
    private long _totalFlushes;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestExecutionCollector"/> class.
    /// </summary>
    /// <param name="uploader">The uploader for sending test results.</param>
    /// <param name="config">The configuration settings.</param>
    /// <exception cref="ArgumentNullException">Thrown when uploader or config is null.</exception>
    public TestExecutionCollector(ITestResultUploader uploader, XpingConfiguration config)
    {
        _uploader = uploader ?? throw new ArgumentNullException(nameof(uploader));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _buffer = new ConcurrentQueue<TestExecution>();
        _flushLock = new SemaphoreSlim(1, 1);
        _random = new Random();
        _uploadedSessions = new ConcurrentDictionary<string, bool>();

        // Set up automatic flush timer if enabled
        if (_config.Enabled && _config.FlushInterval > TimeSpan.Zero)
        {
            _flushTimer = new Timer(
                _ => _ = FlushAsync().ConfigureAwait(false),
                null,
                _config.FlushInterval,
                _config.FlushInterval);
        }
    }

    /// <summary>
    /// Sets the current test session for this collector.
    /// </summary>
    /// <param name="session">The test session to associate with this collector.</param>
    public void SetSession(TestSession session)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TestExecutionCollector));
        }

        _currentSession = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>
    /// Records a test execution for later upload.
    /// </summary>
    /// <param name="execution">The test execution data to record.</param>
    /// <exception cref="ArgumentNullException">Thrown when execution is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the collector has been disposed.</exception>
    public void RecordTest(TestExecution execution)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TestExecutionCollector));
        }

        if (execution == null)
        {
            throw new ArgumentNullException(nameof(execution));
        }

        // Check if SDK is disabled
        if (!_config.Enabled)
        {
            return;
        }

        // Always count as recorded
        Interlocked.Increment(ref _totalRecorded);

        // Apply sampling - if not sampled, drop the test
        if (!ShouldSample())
        {
            return;
        }

        // Test passed sampling - count it and add to buffer
        Interlocked.Increment(ref _totalSampled);
        _buffer.Enqueue(execution);

        // Check if we need to flush based on batch size
        if (_buffer.Count >= _config.BatchSize)
        {
            // Fire and forget - don't wait for flush
            _ = Task.Run(() => FlushAsync());
        }
    }

    /// <summary>
    /// Manually triggers a flush of buffered test executions to the uploader.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous flush operation.</returns>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        if (_buffer.IsEmpty)
        {
            return;
        }

        // Ensure only one flush happens at a time
        await _flushLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await FlushInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _flushLock.Release();
        }
    }

    /// <summary>
    /// Retrieves statistics about the collector's current state.
    /// </summary>
    /// <returns>A task containing the collector statistics.</returns>
    public Task<CollectorStats> GetStatsAsync()
    {
        lock (_statsLock)
        {
            var stats = new CollectorStats
            {
                TotalRecorded = Interlocked.Read(ref _totalRecorded),
                TotalUploaded = Interlocked.Read(ref _totalUploaded),
                TotalFailed = Interlocked.Read(ref _totalFailed),
                TotalSampled = Interlocked.Read(ref _totalSampled),
                TotalFlushes = Interlocked.Read(ref _totalFlushes),
                BufferCount = _buffer.Count
            };

            return Task.FromResult(stats);
        }
    }

    /// <summary>
    /// Disposes the collector and performs a final flush.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        // Dispose timer first to stop automatic flushes
        _flushTimer?.Dispose();

        // Perform final flush before marking as disposed
        try
        {
            if (!_buffer.IsEmpty)
            {
                await _flushLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await FlushInternalAsync(CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    _flushLock.Release();
                }
            }
        }
        catch
        {
            // Swallow exceptions during disposal
        }

        _disposed = true;
        _flushLock?.Dispose();
    }

    /// <summary>
    /// Uploads the current test session metadata to the API.
    /// Uses the session set via SetSession().
    /// </summary>
    private async Task UploadSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _currentSession == null)
        {
            return;
        }

        // Check if SDK is disabled
        if (!_config.Enabled)
        {
            return;
        }

        // Only upload each session once
        if (_uploadedSessions.ContainsKey(_currentSession.SessionId))
        {
            return;
        }

        try
        {
            var result = await _uploader.UploadSessionAsync(_currentSession, cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                _uploadedSessions.TryAdd(_currentSession.SessionId, true);
            }
            else if (_config.EnableOfflineQueue)
            {
                // Could queue session for retry, but since it's typically small,
                // we can retry on next flush
            }
        }
        catch
        {
            if (!_config.EnableOfflineQueue)
            {
                throw;
            }
            // If offline queue is enabled, swallow and allow retry
        }
    }

    private async Task FlushInternalAsync(CancellationToken cancellationToken)
    {
        if (_buffer.IsEmpty)
        {
            return;
        }

        // Upload session first if not already uploaded
        if (_currentSession != null && !_uploadedSessions.ContainsKey(_currentSession.SessionId))
        {
            // Set completion time on first flush (when tests finish)
            if (_currentSession.CompletedAt == null)
            {
                _currentSession.CompletedAt = DateTime.UtcNow;
            }

            await UploadSessionAsync(cancellationToken).ConfigureAwait(false);
        }

        // Dequeue items up to batch size
        var batch = new List<TestExecution>();
        var batchSize = Math.Min(_config.BatchSize, _buffer.Count);

        for (int i = 0; i < batchSize && _buffer.TryDequeue(out var execution); i++)
        {
            batch.Add(execution);
        }

        if (batch.Count == 0)
        {
            return;
        }

        Interlocked.Increment(ref _totalFlushes);

        try
        {
            var result = await _uploader.UploadAsync(batch, cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                Interlocked.Add(ref _totalUploaded, batch.Count);
            }
            else
            {
                Interlocked.Add(ref _totalFailed, batch.Count);

                // Re-enqueue failed items if offline queue is enabled
                if (_config.EnableOfflineQueue)
                {
                    foreach (var execution in batch)
                    {
                        _buffer.Enqueue(execution);
                    }
                }
            }
        }
        catch
        {
            Interlocked.Add(ref _totalFailed, batch.Count);

            // Re-enqueue on exception if offline queue is enabled
            if (_config.EnableOfflineQueue)
            {
                foreach (var execution in batch)
                {
                    _buffer.Enqueue(execution);
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Determines whether a test should be sampled based on the sampling rate.
    /// Random is used for non-cryptographic sampling decisions only.
    /// </summary>
    /// <returns>True if the test should be sampled; otherwise, false.</returns>
#pragma warning disable CA5394 // Random is acceptable for sampling, not security
    private bool ShouldSample()
    {
        // If sampling rate is 1.0 (100%), always include
        if (_config.SamplingRate >= 1.0)
        {
            return true;
        }

        // If sampling rate is 0.0 (0%), never include
        if (_config.SamplingRate <= 0.0)
        {
            return false;
        }

        // Use thread-safe random sampling
        lock (_random)
        {
            return _random.NextDouble() <= _config.SamplingRate;
        }
    }
#pragma warning restore CA5394
}
