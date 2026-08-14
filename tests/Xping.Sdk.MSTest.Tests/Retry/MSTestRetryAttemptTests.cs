/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Retry;

namespace Xping.Sdk.MSTest.Tests.Retry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.MSTest.Retry;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Tests for the attempt numbering the MSTest detector derives by counting the executions already
/// recorded for a test identity.
/// </summary>
/// <remarks>
/// MSTest re-runs <c>[TestInitialize]</c> / <c>[TestCleanup]</c> for every retried attempt, so the
/// adapter records one execution per attempt but has nothing telling it which attempt it is. Counting
/// per fingerprint supplies the number; these tests pin the rules that counting follows.
/// </remarks>
public sealed class MSTestRetryAttemptTests
{
    private static readonly int[] _sequentialAttempts = [1, 2, 3, 4];

    private const string Fingerprint = "8a1c6f5b9d2e4c3a7f0b6e5d4c3b2a19";
    private const string OtherFingerprint = "0f9e8d7c6b5a49382716059483726150";

    private readonly IMSTestRetryDetector _detector = new MSTestRetryDetector();

    public MSTestRetryAttemptTests()
    {
        RetryAttributeRegistry.RegisterCustomRetryAttribute("mstest", "FieldConfiguredRetry");
    }

    [Fact]
    public void DetectRetryMetadata_SecondExecutionOfSameTest_IsAttemptTwo()
    {
        var first = Detect(TestOutcome.Failed);
        var second = Detect(TestOutcome.Passed);

        Assert.NotNull(first);
        Assert.Equal(1, first.AttemptNumber);
        Assert.False(first.PassedOnRetry);

        Assert.NotNull(second);
        Assert.Equal(2, second.AttemptNumber);
        Assert.True(second.PassedOnRetry);
    }

    [Fact]
    public void DetectRetryMetadata_RepeatedFailures_NumbersEveryAttempt()
    {
        Assert.Equal(1, Detect(TestOutcome.Failed)!.AttemptNumber);
        Assert.Equal(2, Detect(TestOutcome.Failed)!.AttemptNumber);

        var third = Detect(TestOutcome.Passed);

        Assert.Equal(3, third!.AttemptNumber);
        Assert.True(third.PassedOnRetry);
    }

    [Fact]
    public void DetectRetryMetadata_AfterAPassingAttempt_StartsANewChain()
    {
        // Two [DataRow] rows carrying identical values share a fingerprint. The second is a genuine
        // repeat, not a retry of the first, and must not be reported as one.
        Detect(TestOutcome.Passed);

        var repeat = Detect(TestOutcome.Passed);

        Assert.Equal(1, repeat!.AttemptNumber);
        Assert.False(repeat.PassedOnRetry);
    }

    [Fact]
    public void DetectRetryMetadata_DifferentTests_AreCountedIndependently()
    {
        Detect(TestOutcome.Failed);
        Detect(TestOutcome.Failed);

        var other = Detect(TestOutcome.Passed, OtherFingerprint);

        Assert.Equal(1, other!.AttemptNumber);
        Assert.False(other.PassedOnRetry);
    }

    [Fact]
    public void DetectRetryMetadata_WithoutRetryAttribute_ReturnsNullAndCountsNothing()
    {
        var withoutRetry = Detect(TestOutcome.Failed, Fingerprint, nameof(RetriedTests.TestMethodWithoutRetry));
        Assert.Null(withoutRetry);

        // The un-retried test shared this fingerprint, so had it been counted the retried test would
        // start at attempt 2.
        Assert.Equal(1, Detect(TestOutcome.Failed)!.AttemptNumber);
    }

    [Fact]
    public void DetectRetryMetadata_WithPublishedAttemptNumber_PrefersThePublishedValue()
    {
        var testContext = CreateTestContext(nameof(RetriedTests.TestMethodWithRetry));
        testContext.Properties["RetryAttempt"] = 4;

        var result = _detector.DetectRetryMetadata(testContext, TestOutcome.Passed, Fingerprint);

        Assert.Equal(4, result!.AttemptNumber);
        Assert.True(result.PassedOnRetry);
    }

    [Fact]
    public void DetectRetryMetadata_WithoutAFingerprint_FallsBackToInference()
    {
        IRetryDetector<TestContext> detector = _detector;
        var testContext = CreateTestContext(nameof(RetriedTests.TestMethodWithRetry));

        // The fingerprint-less overload has no identity to count against, so repeated calls stay on
        // attempt 1 unless the test itself publishes an attempt number.
        Assert.Equal(1, detector.DetectRetryMetadata(testContext, TestOutcome.Failed)!.AttemptNumber);
        Assert.Equal(1, detector.DetectRetryMetadata(testContext, TestOutcome.Passed)!.AttemptNumber);
    }

    [Fact]
    public async Task DetectRetryMetadata_ForTestsRunningInParallel_KeepsEachChainCorrect()
    {
        const int testCount = 16;
        const int attemptsPerTest = 4;

        var fingerprints = Enumerable.Range(0, testCount).Select(i => $"fingerprint-{i}").ToArray();

        var results = await Task.WhenAll(fingerprints.Select(fingerprint => Task.Run(() =>
            Enumerable.Range(0, attemptsPerTest)
                .Select(_ => Detect(TestOutcome.Failed, fingerprint)!.AttemptNumber)
                .ToArray())));

        foreach (int[] attempts in results)
        {
            Assert.Equal(_sequentialAttempts, attempts);
        }
    }

    [Fact]
    public void DetectRetryMetadata_ReadsConfigurationDeclaredAsFields()
    {
        var testContext = CreateTestContext(nameof(RetriedTests.TestMethodWithFieldConfiguredRetry));

        var result = _detector.DetectRetryMetadata(testContext, TestOutcome.Passed, Fingerprint);

        Assert.NotNull(result);
        Assert.Equal(7, result.MaxRetries);
        Assert.Equal(TimeSpan.FromMilliseconds(250), result.DelayBetweenRetries);
    }

    [Fact]
    public void DetectRetryMetadata_WithSuffixedAttributeName_MatchesTheRegistry()
    {
        // The registry entry is "RetryAttribute" while the attribute is used as [Retry]; both
        // spellings have to resolve or the attribute is invisible to the detector.
        Assert.True(RetryAttributeRegistry.IsRegisteredForFramework("mstest", "RetryAttribute"));
        Assert.True(RetryAttributeRegistry.IsRegisteredForFramework("mstest", "Retry"));
    }

    private RetryMetadata? Detect(
        TestOutcome outcome,
        string fingerprint = Fingerprint,
        string testName = nameof(RetriedTests.TestMethodWithRetry)) =>
        _detector.DetectRetryMetadata(CreateTestContext(testName), outcome, fingerprint);

    private static AttemptTestContext CreateTestContext(string testName) =>
        new(testName, typeof(RetriedTests).FullName!);

    /// <summary>
    /// Minimal <see cref="TestContext"/> carrying only what the detector reads.
    /// </summary>
    private sealed class AttemptTestContext(string testName, string fullClassName) : TestContext
    {
#pragma warning disable CS8609 // Nullability of reference types in return type doesn't match overridden member
        public override System.Collections.IDictionary Properties { get; } = new Dictionary<string, object?>();
#pragma warning restore CS8609

        public override string TestName => testName;

        public override string? FullyQualifiedTestClassName => fullClassName;

        public override UnitTestOutcome CurrentTestOutcome => UnitTestOutcome.Passed;

        public override void AddResultFile(string fileName) { }

        public override void WriteLine(string? message) { }

        public override void WriteLine(string format, params object?[]? args) { }

        public override void Write(string? message) { }

        public override void Write(string format, params object?[]? args) { }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Class is used via reflection in tests")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "MSTEST0003:Test method signature is invalid", Justification = "Test helper class used via reflection")]
    private sealed class RetriedTests
    {
        public static void TestMethodWithoutRetry() { }

        [Retry(3)]
        public static void TestMethodWithRetry() { }

        [FieldConfiguredRetry]
        public static void TestMethodWithFieldConfiguredRetry() { }
    }

    /// <summary>
    /// Stand-in for the community retry attributes that expose their configuration as properties.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    private sealed class RetryAttribute(int maxRetries) : Attribute
    {
        public int MaxRetries { get; } = maxRetries;
    }

    /// <summary>
    /// Stand-in for the retry attributes that expose their configuration as public readonly fields,
    /// which a property-only lookup silently misses.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    private sealed class FieldConfiguredRetryAttribute : Attribute
    {
        public readonly int MaxRetries = 7;

        public readonly int DelayBetweenRetriesMs = 250;
    }
}
