/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Persistence;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xping.Sdk.Core.Models;

/// <summary>
/// File-based implementation of offline queue for storing test executions.
/// Stores each batch as a separate JSON file with timestamp-based naming.
/// </summary>
public class FileBasedOfflineQueue : IOfflineQueue, IDisposable
{
    private const string DefaultQueueDirectory = "xping-sdk/queue";
    private const int DefaultMaxQueueSize = 10000;
    private const int DefaultCleanupAgeDays = 7;
    private const string FileExtension = ".json";
    private const string FilePrefix = "xping_";

    private readonly string _queueDirectory;
    private readonly int _maxQueueSize;
    private readonly int _cleanupAgeDays;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileBasedOfflineQueue"/> class.
    /// </summary>
    /// <param name="queueDirectory">Optional custom queue directory. Defaults to system temp directory.</param>
    /// <param name="maxQueueSize">Maximum number of executions to store. Default is 10,000.</param>
    /// <param name="cleanupAgeDays">Age in days after which queue files are deleted. Default is 7.</param>
    public FileBasedOfflineQueue(
        string queueDirectory = null,
        int maxQueueSize = DefaultMaxQueueSize,
        int cleanupAgeDays = DefaultCleanupAgeDays)
    {
        _queueDirectory = queueDirectory ?? Path.Combine(Path.GetTempPath(), DefaultQueueDirectory);
        _maxQueueSize = maxQueueSize;
        _cleanupAgeDays = cleanupAgeDays;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        EnsureQueueDirectoryExists();
    }

    /// <inheritdoc/>
    public async Task EnqueueAsync(IEnumerable<TestExecution> executions, CancellationToken cancellationToken = default)
    {
        var executionsList = executions?.ToList();
        if (executionsList == null || executionsList.Count == 0)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Check queue size limit - note this is approximate and may allow slight overage
            // in concurrent scenarios, but ensures we don't exceed dramatically
            var currentSize = await GetQueueSizeInternalAsync(cancellationToken).ConfigureAwait(false);
            var newCount = executionsList.Count;

            if (currentSize + newCount > _maxQueueSize)
            {
                throw new InvalidOperationException(
                    $"Queue size limit reached. Current: {currentSize}, Attempting to add: {newCount}, Max: {_maxQueueSize}");
            }

            // Create unique filename with timestamp and random component for concurrency
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var random = Guid.NewGuid().ToString("N").Substring(0, 8);
            var fileName = $"{FilePrefix}{timestamp}_{random}{FileExtension}";
            var filePath = Path.Combine(_queueDirectory, fileName);

            // Serialize and write to file
            var json = JsonSerializer.Serialize(executionsList, _jsonOptions);
            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                await writer.WriteAsync(json).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<TestExecution>> DequeueAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        if (maxCount <= 0)
        {
            return Enumerable.Empty<TestExecution>();
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var files = GetQueueFilesOrderedByAge().ToList();
            if (files.Count == 0)
            {
                return Enumerable.Empty<TestExecution>();
            }

            var result = new List<TestExecution>();
            var filesToDelete = new List<string>();

            foreach (var file in files)
            {
                if (result.Count >= maxCount)
                {
                    break;
                }

                try
                {
                    string json;
                    using (var reader = new StreamReader(file, Encoding.UTF8))
                    {
                        json = await reader.ReadToEndAsync().ConfigureAwait(false);
                    }

                    var executions = JsonSerializer.Deserialize<List<TestExecution>>(json, _jsonOptions);

                    if (executions != null)
                    {
                        var remaining = maxCount - result.Count;
                        var toTake = Math.Min(remaining, executions.Count);
                        result.AddRange(executions.Take(toTake));

                        // If we took all executions from this file, mark for deletion
                        if (toTake == executions.Count)
                        {
                            filesToDelete.Add(file);
                        }
                        else
                        {
                            // Write remaining executions back
                            var remainingExecutions = executions.Skip(toTake).ToList();
                            var updatedJson = JsonSerializer.Serialize(remainingExecutions, _jsonOptions);
                            using (var writer = new StreamWriter(file, false, Encoding.UTF8))
                            {
                                await writer.WriteAsync(updatedJson).ConfigureAwait(false);
                            }
                        }
                    }
                    else
                    {
                        // Corrupted file, mark for deletion
                        filesToDelete.Add(file);
                    }
                }
                catch (JsonException)
                {
                    // Corrupted file, mark for deletion and continue
                    filesToDelete.Add(file);
                }
                catch (Exception)
                {
                    // Other I/O errors, skip this file and continue
                    continue;
                }
            }

            // Delete processed files
            foreach (var file in filesToDelete)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetQueueSizeAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await GetQueueSizeInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var files = GetQueueFilesOrderedByAge();
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-_cleanupAgeDays);
            var files = GetQueueFilesOrderedByAge();

            foreach (var file in files)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTimeUtc < cutoffDate)
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private void EnsureQueueDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(_queueDirectory))
            {
                Directory.CreateDirectory(_queueDirectory);
            }
        }
        catch
        {
            // If directory creation fails, operations will fail later with more specific errors
        }
    }

    private IEnumerable<string> GetQueueFilesOrderedByAge()
    {
        try
        {
            if (!Directory.Exists(_queueDirectory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.GetFiles(_queueDirectory, $"{FilePrefix}*{FileExtension}")
                .OrderBy(f => f);
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    private async Task<int> GetQueueSizeInternalAsync(CancellationToken cancellationToken)
    {
        var files = GetQueueFilesOrderedByAge().ToList();
        var totalCount = 0;

        foreach (var file in files)
        {
            try
            {
                string json;
                using (var reader = new StreamReader(file, Encoding.UTF8))
                {
                    json = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                var executions = JsonSerializer.Deserialize<List<TestExecution>>(json, _jsonOptions);
                if (executions != null)
                {
                    totalCount += executions.Count;
                }
            }
            catch
            {
                // Skip corrupted or inaccessible files
            }
        }

        return totalCount;
    }

    /// <summary>
    /// Disposes the resources used by the queue.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by the queue.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lock?.Dispose();
        }
    }
}
