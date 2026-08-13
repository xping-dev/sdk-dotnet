/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Retry;
using Xunit.Abstractions;

namespace Xping.Sdk.XUnit.Retry;

/// <summary>
/// xUnit-specific extension of <see cref="IRetryDetector{T}"/> that accepts an attempt number known
/// by the caller instead of inferring one from the test case.
/// </summary>
/// <remarks>
/// <see cref="XpingRetryTestCase"/> drives the retry library's loop and therefore knows exactly which
/// attempt produced a result. The base interface has no way to carry that, and ambient state would race
/// with xUnit's asynchronous message bus, so the attempt travels as an explicit argument instead.
/// </remarks>
internal interface IXUnitRetryDetector : IRetryDetector<ITest>
{
    /// <summary>
    /// Detects retry metadata for a test, using the supplied attempt number verbatim.
    /// </summary>
    /// <param name="test">The test being recorded.</param>
    /// <param name="testOutcome">The outcome of this attempt.</param>
    /// <param name="attemptNumber">The 1-based attempt number this result belongs to.</param>
    /// <returns>The retry metadata, or null when the test carries no known retry attribute.</returns>
    RetryMetadata? DetectRetryMetadata(ITest test, TestOutcome testOutcome, int attemptNumber);
}
