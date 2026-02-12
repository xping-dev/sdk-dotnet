/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Collections.ObjectModel;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Shared;

namespace Xping.Sdk.Core.Models.Builders;

/// <summary>
/// Builder for constructing immutable <see cref="RetryMetadata"/> instances.
/// </summary>
public sealed class RetryMetadataBuilder
{
    private int _attemptNumber;
    private int _maxRetries;
    private bool _passedOnRetry;
    private TimeSpan _delayBetweenRetries;
    private string? _retryReason;
    private string _retryAttributeName;
    private readonly Dictionary<string, string> _additionalMetadata;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryMetadataBuilder"/> class.
    /// </summary>
    public RetryMetadataBuilder()
    {
        _attemptNumber = 1;
        _maxRetries = 0;
        _passedOnRetry = false;
        _delayBetweenRetries = TimeSpan.Zero;
        _retryAttributeName = string.Empty;
        _additionalMetadata = [];
    }

    /// <summary>
    /// Sets the attempt number.
    /// </summary>
    public RetryMetadataBuilder WithAttemptNumber(int attemptNumber)
    {
        _attemptNumber = attemptNumber;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of retries.
    /// </summary>
    public RetryMetadataBuilder WithMaxRetries(int maxRetries)
    {
        _maxRetries = maxRetries;
        return this;
    }

    /// <summary>
    /// Sets whether the test passed on retry.
    /// </summary>
    public RetryMetadataBuilder WithPassedOnRetry(bool passedOnRetry)
    {
        _passedOnRetry = passedOnRetry;
        return this;
    }

    /// <summary>
    /// Sets the delay between retries.
    /// </summary>
    public RetryMetadataBuilder WithDelayBetweenRetries(TimeSpan delayBetweenRetries)
    {
        _delayBetweenRetries = delayBetweenRetries;
        return this;
    }

    /// <summary>
    /// Sets the retry reason.
    /// </summary>
    public RetryMetadataBuilder WithRetryReason(string retryReason)
    {
        _retryReason = retryReason;
        return this;
    }

    /// <summary>
    /// Sets the retry attribute name.
    /// </summary>
    public RetryMetadataBuilder WithRetryAttributeName(string retryAttributeName)
    {
        _retryAttributeName = retryAttributeName;
        return this;
    }

    /// <summary>
    /// Adds a single metadata entry.
    /// </summary>
    public RetryMetadataBuilder AddMetadata(string key, string value)
    {
        if (!string.IsNullOrEmpty(key))
        {
            _additionalMetadata[key] = value;
        }

        return this;
    }

    /// <summary>
    /// Adds multiple metadata entries.
    /// </summary>
    public RetryMetadataBuilder AddMetadata(IDictionary<string, string> metadata)
    {
        foreach (var kvp in metadata.RequireNotNull())
        {
            _additionalMetadata[kvp.Key] = kvp.Value;
        }

        return this;
    }

    /// <summary>
    /// Builds an immutable <see cref="RetryMetadata"/> instance.
    /// </summary>
    public RetryMetadata Build()
    {
        return new RetryMetadata(
            attemptNumber: _attemptNumber,
            maxRetries: _maxRetries,
            passedOnRetry: _passedOnRetry,
            delayBetweenRetries: _delayBetweenRetries,
            retryReason: _retryReason,
            retryAttributeName: _retryAttributeName,
            additionalMetadata: new ReadOnlyDictionary<string, string>(_additionalMetadata));
    }

    /// <summary>
    /// Resets the builder to its initial state.
    /// </summary>
    public RetryMetadataBuilder Reset()
    {
        _attemptNumber = 1;
        _maxRetries = 0;
        _passedOnRetry = false;
        _delayBetweenRetries = TimeSpan.Zero;
        _retryReason = null;
        _retryAttributeName = string.Empty;
        _additionalMetadata.Clear();
        return this;
    }
}
