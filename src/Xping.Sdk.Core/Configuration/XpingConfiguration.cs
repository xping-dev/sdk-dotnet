/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.ComponentModel.DataAnnotations;

namespace Xping.Sdk.Core.Configuration;

/// <summary>
/// Configuration options for the Xping SDK.
/// </summary>
public sealed class XpingConfiguration
{
    /// <summary>
    /// Represents the default environment setting for the Xping SDK if none is specified.
    /// </summary>
    public const string DefaultEnvironment = "Local";

    /// <summary>
    /// Gets or sets the Xping API endpoint URL.
    /// </summary>
    [Required(ErrorMessage = "ApiEndpoint is required")]
    [Url(ErrorMessage = "ApiEndpoint must be a valid HTTP or HTTPS URL")]
    public string ApiEndpoint { get; set; } = "https://upload.xping.io/v1";

    /// <summary>
    /// Gets or sets the API key for authentication.
    /// </summary>
    [Required(ErrorMessage = "ApiKey is required")]
    [MinLength(1, ErrorMessage = "ApiKey cannot be empty")]
    public string ApiKey { get; set; } = null!;

    /// <summary>
    /// Gets or sets the project ID.
    /// </summary>
    [Required(ErrorMessage = "ProjectId is required")]
    [MinLength(1, ErrorMessage = "ProjectId cannot be empty")]
    public string ProjectId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the batch size for uploading test executions.
    /// </summary>
    [Range(1, 1000, ErrorMessage = "BatchSize must be between 1 and 1000")]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the sampling rate (0.0 to 1.0, where 1.0 means 100% of tests are tracked).
    /// </summary>
    [Range(0.0, 1.0, ErrorMessage = "SamplingRate must be between 0.0 and 1.0")]
    public double SamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the flush interval for automatically uploading batches.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the environment name (e.g., "Local", "CI", "Staging", "Production").
    /// </summary>
    public string Environment { get; set; } = DefaultEnvironment;

    /// <summary>
    /// Gets or sets a value indicating whether to automatically detect CI/CD environments.
    /// </summary>
    public bool AutoDetectCIEnvironment { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the SDK is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to capture stack traces for failed tests.
    /// </summary>
    public bool CaptureStackTraces { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable compression for uploads.
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed uploads.
    /// </summary>
    [Range(0, 10, ErrorMessage = "MaxRetries must be between 0 and 10")]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the timeout for upload operations.
    /// </summary>
    public TimeSpan UploadTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether to collect network reliability metrics.
    /// Network metrics include latency, connection type, and online status.
    /// </summary>
    public bool CollectNetworkMetrics { get; set; } = true;

    /// <summary>
    /// Validates the configuration and returns a list of validation errors.
    /// </summary>
    /// <returns>A list of validation error messages, or an empty list if valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            errors.Add("ApiKey is required.");
        }

        if (string.IsNullOrWhiteSpace(ProjectId))
        {
            errors.Add("ProjectId is required.");
        }

        if (string.IsNullOrWhiteSpace(ApiEndpoint))
        {
            errors.Add("ApiEndpoint is required.");
        }
        else if (!Uri.TryCreate(ApiEndpoint, UriKind.Absolute, out var uri) ||
                 (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("ApiEndpoint must be a valid HTTP or HTTPS URL.");
        }

        if (BatchSize <= 0)
        {
            errors.Add("BatchSize must be greater than zero.");
        }

        if (BatchSize > 1000)
        {
            errors.Add("BatchSize cannot exceed 1000.");
        }

        if (FlushInterval <= TimeSpan.Zero)
        {
            errors.Add("FlushInterval must be greater than zero.");
        }

        if (MaxRetries < 0)
        {
            errors.Add("MaxRetries cannot be negative.");
        }

        if (MaxRetries > 10)
        {
            errors.Add("MaxRetries cannot exceed 10.");
        }

        if (RetryDelay < TimeSpan.Zero)
        {
            errors.Add("RetryDelay cannot be negative.");
        }

        if (SamplingRate < 0.0 || SamplingRate > 1.0)
        {
            errors.Add("SamplingRate must be between 0.0 and 1.0.");
        }

        if (UploadTimeout <= TimeSpan.Zero)
        {
            errors.Add("UploadTimeout must be greater than zero.");
        }

        return errors;
    }

    /// <summary>
    /// Determines whether the configuration is valid.
    /// </summary>
    /// <returns><c>true</c> if the configuration is valid; otherwise, <c>false</c>.</returns>
    public bool IsValid()
    {
        return Validate().Count == 0;
    }
}
