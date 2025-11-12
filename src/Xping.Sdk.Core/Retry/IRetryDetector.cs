/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Retry;

using Xping.Sdk.Core.Models;

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
    /// <returns>
    /// RetryMetadata if retry attributes are found; otherwise, null.
    /// </returns>
    /// <remarks>
    /// This method performs comprehensive analysis of the test to extract all available
    /// retry-related information including attempt number, max retries, delays, and reasons.
    /// Returns null if the test does not have any retry configuration.
    /// </remarks>
    RetryMetadata? DetectRetryMetadata(TTest test);

    /// <summary>
    /// Checks if the specified test has any retry attributes.
    /// </summary>
    /// <param name="test">The test to check.</param>
    /// <returns>True if the test has retry attributes; otherwise, false.</returns>
    /// <remarks>
    /// This is a lightweight check that can be used for quick filtering without
    /// the overhead of extracting full metadata. Useful for early detection
    /// of tests that require retry tracking.
    /// </remarks>
    bool HasRetryAttribute(TTest test);

    /// <summary>
    /// Gets the current attempt number for the test execution.
    /// </summary>
    /// <param name="test">The test being executed.</param>
    /// <returns>
    /// The attempt number (1 for first attempt, 2+ for retries).
    /// Returns 1 if attempt tracking is not available.
    /// </returns>
    /// <remarks>
    /// Different test frameworks expose attempt information in different ways:
    /// - NUnit: TestContext.CurrentContext.CurrentRepeatCount
    /// - XUnit: Custom traits or test case properties
    /// - MSTest: Custom properties in TestContext
    /// This method abstracts these framework differences.
    /// </remarks>
    int GetCurrentAttemptNumber(TTest test);
}
