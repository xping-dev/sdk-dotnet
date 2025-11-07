/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xping.Sdk.Core.Collection;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Persistence;
using Xping.Sdk.Core.Upload;

/// <summary>
/// Global context for managing Xping SDK lifecycle in NUnit tests.
/// </summary>
public static class XpingContext
{
    private static readonly object _lock = new();
    private static TestExecutionCollector? _collector;
    private static ITestResultUploader? _uploader;
    private static IOfflineQueue? _offlineQueue;
    private static XpingConfiguration? _configuration;
    private static HttpClient? _httpClient;
    private static TestSession? _currentSession;

    /// <summary>
    /// Gets a value indicating whether the context is initialized.
    /// </summary>
    public static bool IsInitialized { get; private set; }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    internal static XpingConfiguration? Configuration => _configuration;

    /// <summary>
    /// Gets the current test session.
    /// </summary>
    internal static TestSession? CurrentSession => _currentSession;

    /// <summary>
    /// Initializes the Xping context with default configuration.
    /// </summary>
    /// <returns>The test execution collector.</returns>
    public static TestExecutionCollector Initialize()
    {
        lock (_lock)
        {
            if (IsInitialized && _collector != null)
            {
                return _collector;
            }

            var config = XpingConfigurationLoader.Load();
            return InitializeInternal(config);
        }
    }

    /// <summary>
    /// Initializes the Xping context with the specified configuration.
    /// </summary>
    /// <param name="configuration">The configuration to use.</param>
    /// <returns>The test execution collector.</returns>
    public static TestExecutionCollector Initialize(XpingConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        lock (_lock)
        {
            if (IsInitialized && _collector != null)
            {
                return _collector;
            }

            return InitializeInternal(configuration);
        }
    }

    /// <summary>
    /// Records a test execution.
    /// </summary>
    /// <param name="execution">The test execution to record.</param>
    public static void RecordTest(TestExecution execution)
    {
        if (!IsInitialized)
        {
            return;
        }

        _collector?.RecordTest(execution);
    }

    /// <summary>
    /// Flushes all pending test executions and uploads them.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            return;
        }

        if (_collector != null)
        {
            await _collector.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes all resources and resets the context.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task DisposeAsync()
    {
        if (!IsInitialized)
        {
            return;
        }

        lock (_lock)
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;
        }

        try
        {
            if (_collector != null)
            {
                await _collector.FlushAsync().ConfigureAwait(false);
                await _collector.DisposeAsync().ConfigureAwait(false);
                _collector = null;
            }

            (_uploader as IDisposable)?.Dispose();
            _uploader = null;

            (_offlineQueue as IDisposable)?.Dispose();
            _offlineQueue = null;

            _httpClient?.Dispose();
            _httpClient = null;

            _configuration = null;
            _currentSession = null;
        }
        catch
        {
            // Suppress exceptions during disposal
        }
    }

    /// <summary>
    /// Resets the context (primarily for testing purposes).
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            IsInitialized = false;
            _collector = null;
            _uploader = null;
            _offlineQueue = null;
            _httpClient = null;
            _configuration = null;
            _currentSession = null;
        }
    }

    private static TestExecutionCollector InitializeInternal(XpingConfiguration configuration)
    {
        _configuration = configuration;
        _httpClient = new HttpClient();
        // Initialize offline queue if enabled
        if (_configuration.EnableOfflineQueue)
        {
            _offlineQueue = new FileBasedOfflineQueue();
        }
        _uploader = new XpingApiClient(_httpClient, configuration, _offlineQueue);
        _collector = new TestExecutionCollector(_uploader, configuration);

        // Initialize the test session and associate it with the collector
        _currentSession = new TestSession();
        // Ensure environment info is populated in the session (only once)
        if (string.IsNullOrEmpty(_currentSession.EnvironmentInfo.MachineName))
        {
            var detector = new Core.Environment.EnvironmentDetector();
            var collectNetworkMetrics = _configuration.CollectNetworkMetrics;
            var apiEndpoint = _configuration.ApiEndpoint;
            _currentSession.EnvironmentInfo = detector.Detect(collectNetworkMetrics, apiEndpoint);
        }

        IsInitialized = true;

        return _collector;
    }
}
