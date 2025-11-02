/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Configuration;

using System;

/// <summary>
/// Provides a fluent API for building <see cref="XpingConfiguration"/> instances.
/// </summary>
public sealed class XpingConfigurationBuilder
{
    private readonly XpingConfiguration _configuration = new();

    /// <summary>
    /// Sets the API endpoint URL.
    /// </summary>
    /// <param name="apiEndpoint">The API endpoint URL.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithApiEndpoint(string apiEndpoint)
    {
        _configuration.ApiEndpoint = apiEndpoint;
        return this;
    }

    /// <summary>
    /// Sets the API key for authentication.
    /// </summary>
    /// <param name="apiKey">The API key.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithApiKey(string apiKey)
    {
        _configuration.ApiKey = apiKey;
        return this;
    }

    /// <summary>
    /// Sets the project ID.
    /// </summary>
    /// <param name="projectId">The project ID.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithProjectId(string projectId)
    {
        _configuration.ProjectId = projectId;
        return this;
    }

    /// <summary>
    /// Sets the batch size for uploading test executions.
    /// </summary>
    /// <param name="batchSize">The batch size.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithBatchSize(int batchSize)
    {
        _configuration.BatchSize = batchSize;
        return this;
    }

    /// <summary>
    /// Sets the flush interval for automatically uploading batches.
    /// </summary>
    /// <param name="flushInterval">The flush interval.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithFlushInterval(TimeSpan flushInterval)
    {
        _configuration.FlushInterval = flushInterval;
        return this;
    }

    /// <summary>
    /// Sets the environment name.
    /// </summary>
    /// <param name="environment">The environment name.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithEnvironment(string environment)
    {
        _configuration.Environment = environment;
        return this;
    }

    /// <summary>
    /// Sets whether to automatically detect CI/CD environments.
    /// </summary>
    /// <param name="autoDetect">Whether to auto-detect CI environments.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithAutoDetectCIEnvironment(bool autoDetect)
    {
        _configuration.AutoDetectCIEnvironment = autoDetect;
        return this;
    }

    /// <summary>
    /// Sets whether the SDK is enabled.
    /// </summary>
    /// <param name="enabled">Whether the SDK is enabled.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithEnabled(bool enabled)
    {
        _configuration.Enabled = enabled;
        return this;
    }

    /// <summary>
    /// Sets whether to capture stack traces for failed tests.
    /// </summary>
    /// <param name="captureStackTraces">Whether to capture stack traces.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithCaptureStackTraces(bool captureStackTraces)
    {
        _configuration.CaptureStackTraces = captureStackTraces;
        return this;
    }

    /// <summary>
    /// Sets whether to enable compression for uploads.
    /// </summary>
    /// <param name="enableCompression">Whether to enable compression.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithEnableCompression(bool enableCompression)
    {
        _configuration.EnableCompression = enableCompression;
        return this;
    }

    /// <summary>
    /// Sets whether to enable offline queue for failed uploads.
    /// </summary>
    /// <param name="enableOfflineQueue">Whether to enable offline queue.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithEnableOfflineQueue(bool enableOfflineQueue)
    {
        _configuration.EnableOfflineQueue = enableOfflineQueue;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of retry attempts for failed uploads.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithMaxRetries(int maxRetries)
    {
        _configuration.MaxRetries = maxRetries;
        return this;
    }

    /// <summary>
    /// Sets the delay between retry attempts.
    /// </summary>
    /// <param name="retryDelay">The retry delay.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithRetryDelay(TimeSpan retryDelay)
    {
        _configuration.RetryDelay = retryDelay;
        return this;
    }

    /// <summary>
    /// Sets the sampling rate.
    /// </summary>
    /// <param name="samplingRate">The sampling rate (0.0 to 1.0).</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithSamplingRate(double samplingRate)
    {
        _configuration.SamplingRate = samplingRate;
        return this;
    }

    /// <summary>
    /// Sets the timeout for upload operations.
    /// </summary>
    /// <param name="uploadTimeout">The upload timeout.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public XpingConfigurationBuilder WithUploadTimeout(TimeSpan uploadTimeout)
    {
        _configuration.UploadTimeout = uploadTimeout;
        return this;
    }

    /// <summary>
    /// Builds the configuration instance.
    /// </summary>
    /// <returns>The configured <see cref="XpingConfiguration"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the configuration is invalid.</exception>
    public XpingConfiguration Build()
    {
        var errors = _configuration.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Invalid configuration: {string.Join(", ", errors)}");
        }

        return _configuration;
    }

    /// <summary>
    /// Attempts to build the configuration instance without throwing exceptions.
    /// </summary>
    /// <param name="configuration">The built configuration if successful.</param>
    /// <param name="errors">The validation errors if unsuccessful.</param>
    /// <returns><c>true</c> if the configuration is valid; otherwise, <c>false</c>.</returns>
    public bool TryBuild(out XpingConfiguration configuration, out string[] errors)
    {
        var validationErrors = _configuration.Validate();
        if (validationErrors.Count > 0)
        {
            configuration = null;
            errors = new string[validationErrors.Count];
            for (int i = 0; i < validationErrors.Count; i++)
            {
                errors[i] = validationErrors[i];
            }
            return false;
        }

        configuration = _configuration;
        errors = Array.Empty<string>();
        return true;
    }
}
