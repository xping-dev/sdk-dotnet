/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Retry;

namespace Xping.Sdk.MSTest.Retry;

/// <summary>
/// MSTest-specific extension of <see cref="IRetryDetector{T}"/> that identifies the test being
/// recorded, so the detector can number its attempts.
/// </summary>
/// <remarks>
/// MSTest re-runs the whole per-test lifecycle for every retried attempt — a retry attribute derived
/// from <c>TestMethodAttribute</c> invokes the test method again, which builds a fresh test class
/// instance and runs <c>[TestInitialize]</c>, the method, and <c>[TestCleanup]</c> once more. The
/// adapter therefore already observes each attempt separately; what it cannot see is which attempt an
/// observation belongs to, because nothing in <see cref="TestContext"/> carries that. Counting the
/// executions already recorded for the same test identity supplies it, and the identity has to come
/// from the caller since it is the caller that computes the fingerprint.
/// </remarks>
internal interface IMSTestRetryDetector : IRetryDetector<TestContext>
{
    /// <summary>
    /// Detects retry metadata for a test, numbering this execution against the ones already recorded
    /// for the same test identity in this session.
    /// </summary>
    /// <param name="testContext">The context of the test being recorded.</param>
    /// <param name="testOutcome">The outcome of this attempt.</param>
    /// <param name="testFingerprint">The fingerprint identifying the test across attempts.</param>
    /// <returns>The retry metadata, or null when the test carries no known retry attribute.</returns>
    RetryMetadata? DetectRetryMetadata(TestContext testContext, TestOutcome testOutcome, string testFingerprint);
}
