/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest;

using System.Net.Http;
using System.Threading.Tasks;
using Xping.Sdk.Core.Collection;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Persistence;
using Xping.Sdk.Core.Upload;

/// <summary>
/// Global context for managing Xping SDK lifecycle in MSTest test assemblies.
/// Provides initialization, test recording, and cleanup functionality.
/// </summary>
public static class XpingContext
{
    private static readonly object InitializationLock = new object();
    private static TestExecutionCollector? _collector;
    private static ITestResultUploader? _uploader;
    private static IOfflineQueue? _offlineQueue;
    private static HttpClient? _httpClient;
    private static XpingConfiguration? _configuration;

    /// <summary>
    /// Gets a value indicating whether the context has been initialized.
    /// </summary>
    public static bool IsInitialized { get; private set; }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    internal static XpingConfiguration? Configuration => _configuration;

    /// <summary>
    /// Initializes the Xping context with default configuration.
    /// Loads configuration from appsettings.json or environment variables.
    /// </summary>
    /// <returns>The test execution collector instance.</returns>
    public static TestExecutionCollector Initialize()
    {
        lock (InitializationLock)
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
        lock (InitializationLock)
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
            await _collector.FlushAsync().ConfigureAwait(false);
            await _collector.DisposeAsync().ConfigureAwait(false);
        }

        _uploader = null;
        _offlineQueue = null;
        _httpClient?.Dispose();
        _httpClient = null;
        _configuration = null;
        _collector = null;
        IsInitialized = false;
    }

    /// <summary>
    /// Resets the context (primarily for testing purposes).
    /// </summary>
    public static void Reset()
    {
        lock (InitializationLock)
        {
            _collector = null;
            _uploader = null;
            _offlineQueue = null;
            _httpClient?.Dispose();
            _httpClient = null;
            _configuration = null;
            IsInitialized = false;
        }
    }

    private static TestExecutionCollector InitializeInternal(XpingConfiguration configuration)
    {
        _configuration = configuration;
        _httpClient = new HttpClient();
        _offlineQueue = new FileBasedOfflineQueue();
        _uploader = new XpingApiClient(_httpClient, configuration, _offlineQueue);
        _collector = new TestExecutionCollector(_uploader, configuration);
        IsInitialized = true;

        return _collector;
    }
}
