/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Services.Retry;

/// <summary>
/// Defines functionality for detecting retry attributes and metadata from test methods.
/// </summary>
/// <typeparam name="TTest">The framework-specific test representation type.</typeparam>
/// <remarks>
/// Implementations of this interface provide framework-specific logic to detect
/// retry configuration from test attributes and extract metadata about retry behavior.
/// Each test framework (XUnit, NUnit, MSTest) has its own implementation that understands
/// the framework's retry attribute patterns and test context APIs.
/// </remarks>
public interface IRetryDetector<in TTest>
{
    /// <summary>
    /// Detects retry metadata from the given test.
    /// </summary>
    /// <param name="test">The test to analyze for retry attributes.</param>
    /// <param name="testOutcome">
    /// The outcome of the test execution used to determine if the test passed on a retry attempt.
    /// </param>
    /// <returns>
    /// RetryMetadata if retry attributes are found; otherwise, null.
    /// </returns>
    /// <remarks>
    /// This method performs comprehensive analysis of the test to extract all available
    /// retry-related information including attempt number, max retries, delays, and reasons.
    /// Returns null if the test does not have any retry configuration.
    /// </remarks>
    RetryMetadata? DetectRetryMetadata(TTest test, TestOutcome testOutcome);
}
