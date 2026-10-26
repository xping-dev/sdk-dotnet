/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Retry;
using Xunit.v3;

namespace Xping.Sdk.XUnit.V3.Retry;

/// <summary>
/// xUnit v3 extension of <see cref="IRetryDetector{T}"/> that accepts an attempt number known by the
/// caller instead of assuming the first.
/// </summary>
/// <remarks>
/// The attempt is learned from the retry library's announcement on the message bus (see
/// <see cref="XpingMessageSink"/>), which the base interface has no way to carry.
/// </remarks>
internal interface IXUnitRetryDetector : IRetryDetector<IXunitTestCase>
{
    /// <summary>
    /// Detects retry metadata for a test case, using the supplied attempt number verbatim.
    /// </summary>
    /// <param name="testCase">The test case being recorded.</param>
    /// <param name="testOutcome">The outcome of this attempt.</param>
    /// <param name="attemptNumber">The 1-based attempt number this result belongs to.</param>
    /// <returns>The retry metadata, or null when the test carries no known retry attribute.</returns>
    RetryMetadata? DetectRetryMetadata(IXunitTestCase testCase, TestOutcome testOutcome, int attemptNumber);
}
