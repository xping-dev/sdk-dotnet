/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit;

using System.Net.Http;
using System.Threading.Tasks;
using Core.Collection;
using Core.Configuration;
using Core.Diagnostics;
using Core.Models;
using Core.Upload;
using Xping.Sdk.XUnit.Diagnostics;

/// <summary>
/// Global context for managing Xping SDK lifecycle in xUnit test assemblies.
/// Provides initialization, test recording, and cleanup functionality.
/// </summary>
public static class XpingContext
{
    private static readonly object _initializationLock = new();
    private static TestExecutionCollector? _collector;
    private static ITestResultUploader? _uploader;
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
    /// Initializes the Xping context with the default configuration.
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

        // Create logger based on configuration
        // Use xUnit-specific logger for proper test output integration
        var logger = configuration.Logger ?? (configuration.LogLevel == XpingLogLevel.None
            ? XpingNullLogger.Instance
            : new XpingXUnitLogger(configuration.LogLevel));

        // Validate configuration and log any issues
        var errors = configuration.Validate();
        if (errors.Count > 0)
        {
            logger.LogError("Invalid configuration:");
            foreach (var error in errors)
            {
                logger.LogError($"  - {error}");
            }
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

        // Initialize the test session and associate it with the collector
        _currentSession = new TestSession();

        // Ensure environment info is populated in the session (only once)
        if (string.IsNullOrEmpty(_currentSession.EnvironmentInfo.MachineName))
        {
            var detector = new Core.Environment.EnvironmentDetector();
            _currentSession.EnvironmentInfo = detector.Detect(_configuration);
        }

        IsInitialized = true;

        // Log successful initialization
        logger.LogInfo(
            $"Project: {configuration.ProjectId} | " +
            $"Environment: {_currentSession.EnvironmentInfo.EnvironmentName}");
        logger.LogDebug($"Endpoint: {configuration.ApiEndpoint}");
        logger.LogDebug($"Batch Size: {configuration.BatchSize} | Sampling: {configuration.SamplingRate:P0}");

        if (configuration.SamplingRate < 1.0)
        {
            logger.LogInfo($"Sampling Rate: {configuration.SamplingRate:P0} (approximately {(int)(configuration.SamplingRate * 100)} out of 100 tests will be tracked)");
        }

        return _collector;
    }
}
