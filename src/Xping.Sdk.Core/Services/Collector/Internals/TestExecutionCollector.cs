/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Services.Collector.Internals;

/// <inheritdoc/>
internal sealed class TestExecutionCollector(
    IOptions<XpingConfiguration> options) : ITestExecutionCollector
{
    private readonly object _statsLock = new();
    private readonly ConcurrentQueue<TestExecution> _buffer = new();
    private readonly XpingConfiguration _configuration = options.Value;
    private readonly Random _random = new();

    // Collector stats
    private long _totalRecorded;
    private long _totalSampled;
    private volatile bool _disposed;

    public event EventHandler? BufferFull;

    /// <inheritdoc/>
    void ITestExecutionCollector.RecordTest(TestExecution execution)
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
        if (!_configuration.Enabled)
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

        // Check if we need to notify buffer full based on the configured batch size
        if (_buffer.Count >= _configuration.BatchSize)
        {
            BufferFull?.Invoke(this, EventArgs.Empty);
        }
    }

    IReadOnlyList<TestExecution> ITestExecutionCollector.Drain()
    {
        if (_disposed || _buffer.IsEmpty)
        {
            return [];
        }

        // Dequeue items up to batch size
        List<TestExecution> batch = [];
        int batchSize = Math.Min(_configuration.BatchSize, _buffer.Count);

        for (int i = 0; i < batchSize && _buffer.TryDequeue(out TestExecution? execution); i++)
        {
            batch.Add(execution);
        }

        return batch.Count == 0 ? [] : batch;
    }

    /// <inheritdoc/>
    Task<CollectorStats> ITestExecutionCollector.GetStatsAsync()
    {
        lock (_statsLock)
        {
            CollectorStats stats = new()
            {
                TotalRecorded = Interlocked.Read(ref _totalRecorded),
                TotalSampled = Interlocked.Read(ref _totalSampled),
                BufferCount = _buffer.Count
            };

            return Task.FromResult(stats);
        }
    }

    /// <summary>
    /// Disposes the collector and prevents further test recording.
    /// The orchestrator is responsible for draining the buffer via <c>Drain()</c>
    /// before the host disposes this collector.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
    }

    /// <summary>
    /// Determines whether a test should be sampled based on the sampling rate.
    /// Random is used for non-cryptographic sampling decisions only.
    /// </summary>
    /// <returns>True if the test should be sampled; otherwise, false.</returns>
    private bool ShouldSample()
    {
        switch (_configuration.SamplingRate)
        {
            // If the sampling rate is 1.0 (100%), always include
            case >= 1.0:
                return true;
            // If the sampling rate is 0.0 (0%), never include
            case <= 0.0:
                return false;
        }

        // Use thread-safe random sampling
        lock (_random)
        {
#pragma warning disable CA5394 // Random is acceptable for sampling, not security
            return _random.NextDouble() <= _configuration.SamplingRate;
#pragma warning restore CA5394
        }
    }
}
