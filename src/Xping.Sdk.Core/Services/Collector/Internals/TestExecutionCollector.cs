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

    // Collector stats
    private long _totalRecorded;
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

        // Every execution that reaches here is buffered. There is no sampling, and no other
        // path that drops one, so the recorded count and the buffered count never diverge.
        Interlocked.Increment(ref _totalRecorded);
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
}
