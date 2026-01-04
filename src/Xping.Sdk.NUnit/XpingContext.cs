/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Core.Collection;
using Core.Configuration;
using Core.Diagnostics;
using Core.Models;
using Core.Upload;
using Diagnostics;

/// <summary>
/// Global context for managing Xping SDK lifecycle in NUnit tests.
/// </summary>
public static class XpingContext
{
    private static readonly object _lock = new();
    private static TestExecutionCollector? _collector;
    private static ITestResultUploader? _uploader;
    private static XpingConfiguration? _configuration;
    private static HttpClient? _httpClient;
    private static TestSession? _currentSession;
    private static ExecutionTracker? _executionTracker;
    private static bool _configErrorsLogged;
    private static bool _initializedLogged;

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
    /// Gets the execution tracker for order and parallelization tracking.
    /// </summary>
    internal static ExecutionTracker? ExecutionTracker => _executionTracker;

    /// <summary>
    /// Initializes the Xping context with the default configuration.
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
            _initializedLogged = false;
            _configErrorsLogged = false;
            _collector = null;
            _uploader = null;
            _httpClient?.Dispose();
            _httpClient = null;
            _configuration = null;
            _currentSession = null;
            _executionTracker?.Clear();
            _executionTracker = null;
        }
    }

    private static TestExecutionCollector InitializeInternal(XpingConfiguration configuration)
    {
        _configuration = configuration;

        // Create a logger based on configuration
        // Use NUnit-specific logger for proper test output integration
        var logger = configuration.Logger ?? (configuration.LogLevel == XpingLogLevel.None
            ? XpingNullLogger.Instance
            : new XpingNUnitLogger(configuration.LogLevel));

        // Validate configuration and log any issues
        var errors = configuration.Validate();
        if (!_configErrorsLogged && errors.Count > 0)
        {
            logger.LogError("Invalid configuration:");
            foreach (var error in errors)
            {
                logger.LogError($"  - {error}");
            }
            _configErrorsLogged = true;
        }

        // Check for common misconfigurations
        if (!configuration.Enabled)
        {
            logger.LogWarning("SDK is disabled (Enabled=false) - test executions will not be tracked");
        }

        if (string.IsNullOrWhiteSpace(configuration.ApiKey))
        {
            logger.LogWarning("API Key not configured - uploads will be skipped");
        }

        if (string.IsNullOrWhiteSpace(configuration.ProjectId))
        {
            logger.LogWarning("Project ID not configured - uploads will be skipped");
        }

        _httpClient = new HttpClient();

        _uploader = new XpingApiClient(_httpClient, configuration, serializer: null, logger);
        _collector = new TestExecutionCollector(_uploader, configuration, logger);
        _executionTracker = new ExecutionTracker();

        // Initialize the test session and associate it with the collector
        _currentSession = new TestSession();

        // Ensure environment info is populated in the session (only once)
        if (string.IsNullOrEmpty(_currentSession.EnvironmentInfo.MachineName))
        {
            var detector = new Core.Environment.EnvironmentDetector();
            _currentSession.EnvironmentInfo = detector.Detect(_configuration);
        }

        IsInitialized = true;

        if (!_initializedLogged)
        {
            // Log successful initialization
            logger.LogInfo(
                $"Project: {configuration.ProjectId} | " +
                $"Environment: {_currentSession.EnvironmentInfo.EnvironmentName}");
            logger.LogDebug($"Endpoint: {configuration.ApiEndpoint}");
            logger.LogDebug($"Batch Size: {configuration.BatchSize} | Sampling: {configuration.SamplingRate:P0}");

            if (configuration.SamplingRate < 1.0)
            {
                logger.LogInfo(
                    $"Sampling Rate: {configuration.SamplingRate:P0} (approximately {(int)(configuration.SamplingRate * 100)} out of 100 tests will be tracked)");
            }
            _initializedLogged = true;
        }

        return _collector;
    }
}
