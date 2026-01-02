/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents metadata about test retry behavior and configuration.
/// </summary>
/// <remarks>
/// This metadata helps identify flaky tests that pass only after retry,
/// tests with timing issues that consistently pass on the Nth retry,
/// and patterns indicating test reliability problems.
/// </remarks>
public sealed class RetryMetadata
{
    /// <summary>
    /// Gets or sets the current attempt number for this test execution.
    /// </summary>
    /// <remarks>
    /// 1 = first run (no retry), 2 = first retry, 3 = second retries, etc.
    /// This enables analysis of "pass on Nth attempt" patterns.
    /// </remarks>
    public int AttemptNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum number of retries configured for this test.
    /// </summary>
    /// <remarks>
    /// This is the total number of retry attempts allowed, not including the initial attempt.
    /// For example, MaxRetries = 3 means: 1 initial attempt + 3 retry attempts = 4 total attempts.
    /// </remarks>
    public int MaxRetries { get; set; }

    /// <summary>
    /// Gets or sets whether this test passed after a retry (not on the first attempt).
    /// </summary>
    /// <remarks>
    /// False = passed on the first attempt (no retry needed)
    /// True = passed after one or more retries (indicates flakiness)
    /// </remarks>
    public bool PassedOnRetry { get; set; }

    /// <summary>
    /// Gets or sets the configured delay between retry attempts.
    /// </summary>
    /// <remarks>
    /// This is the delay configured in the retry attribute.
    /// Actual delay may vary based on framework implementation.
    /// </remarks>
    public TimeSpan DelayBetweenRetries { get; set; }

    /// <summary>
    /// Gets or sets the reason for the retry configuration.
    /// </summary>
    /// <remarks>
    /// Examples: "Timeout", "NetworkError", "DatabaseContention", "RaceCondition"
    /// This is typically extracted from custom retry attributes or attribute properties.
    /// May be null if not specified by the test author.
    /// </remarks>
    public string? RetryReason { get; set; }

    /// <summary>
    /// Gets or sets the name of the retry attribute or strategy used.
    /// </summary>
    /// <remarks>
    /// Examples: "RetryFact", "Retry", "TestRetry", "CustomRetry"
    /// Helps identify which retry mechanism is being used.
    /// </remarks>
    public string? RetryAttributeName { get; set; }

    /// <summary>
    /// Gets the additional retry-related metadata.
    /// </summary>
    /// <remarks>
    /// Used for framework-specific or custom retry attribute properties.
    /// Examples: retry filter types, exception types to retry on, custom conditions.
    /// </remarks>
    public Dictionary<string, string> AdditionalMetadata { get; } = new Dictionary<string, string>();
}
