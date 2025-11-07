/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit;

using System.Net.Http;
using System.Threading.Tasks;
using Xping.Sdk.Core.Collection;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Persistence;
using Xping.Sdk.Core.Upload;

/// <summary>
/// Global context for managing Xping SDK lifecycle in xUnit test assemblies.
/// Provides initialization, test recording, and cleanup functionality.
/// </summary>
public static class XpingContext
{
    private static readonly object _initializationLock = new();
    private static TestExecutionCollector? _collector;
    private static ITestResultUploader? _uploader;
    private static IOfflineQueue? _offlineQueue;
    private static HttpClient? _httpClient;
    private static XpingConfiguration? _configuration;
    private static TestSession? _currentSession;

    /// <summary>
    /// Gets a value indicating whether the context has been initialized.
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
    /// Loads configuration from appsettings.json or environment variables.
    /// </summary>
    /// <returns>The test execution collector instance.</returns>
    public static TestExecutionCollector Initialize()
    {
        lock (_initializationLock)
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
    /// Initializes the Xping context with custom configuration.
    /// </summary>
    /// <param name="configuration">The configuration to use.</param>
    /// <returns>The test execution collector instance.</returns>
    public static TestExecutionCollector Initialize(XpingConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        lock (_initializationLock)
        {
            if (IsInitialized && _collector != null)
            {
                return _collector;
            }

            return InitializeInternal(configuration);
        }
    }

    /// <summary>
    /// Records a test execution to the collector.
    /// </summary>
    /// <param name="execution">The test execution to record.</param>
    public static void RecordTest(TestExecution execution)
    {
        if (!IsInitialized || _collector == null)
        {
            return;
        }

        _collector.RecordTest(execution);
    }

    /// <summary>
    /// Flushes all pending test executions to the API.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task FlushAsync()
    {
        if (!IsInitialized || _collector == null)
        {
            return;
        }

        await _collector.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes all resources and resets the context.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async ValueTask DisposeAsync()
    {
        if (!IsInitialized)
        {
            return;
        }

        if (_collector != null)
        {
            try
            {
                await _collector.FlushAsync().ConfigureAwait(false);
                await _collector.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Ignore errors during disposal
            }
        }

        _uploader = null;
        _offlineQueue = null;
        _httpClient?.Dispose();
        _httpClient = null;
        _configuration = null;
        _collector = null;
        _currentSession = null;
        IsInitialized = false;
    }

    /// <summary>
    /// Resets the context (primarily for testing purposes).
    /// </summary>
    public static void Reset()
    {
        lock (_initializationLock)
        {
            _collector = null;
            _uploader = null;
            _offlineQueue = null;
            _httpClient?.Dispose();
            _httpClient = null;
            _configuration = null;
            _currentSession = null;
            IsInitialized = false;
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
