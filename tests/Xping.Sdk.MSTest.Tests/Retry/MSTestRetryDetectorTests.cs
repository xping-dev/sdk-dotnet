/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Retry;

namespace Xping.Sdk.MSTest.Tests.Retry;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.MSTest.Retry;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Tests for the MSTestRetryDetector class.
/// </summary>
public sealed class MSTestRetryDetectorTests
{
    private readonly IRetryDetector<TestContext> _detector;

    public MSTestRetryDetectorTests()
    {
        _detector = new MSTestRetryDetector();

        // Register common MSTest retry attributes for testing
        RetryAttributeRegistry.RegisterCustomRetryAttribute("mstest", "Retry");
        RetryAttributeRegistry.RegisterCustomRetryAttribute("mstest", "RetryFact");
        RetryAttributeRegistry.RegisterCustomRetryAttribute("mstest", "RetryTest");
    }

    [Fact]
    public void DetectRetryMetadata_WithNullTestContext_ReturnsNull()
    {
        var result = _detector.DetectRetryMetadata(null!, TestOutcome.Passed);

        Assert.Null(result);
    }

    [Fact]
    public void DetectRetryMetadata_WithRetryAttribute_ReturnsMetadata()
    {
        var testContext = CreateMockTestContext(
            nameof(TestClass.TestMethodWithRetry),
            typeof(TestClass).FullName!);

        var result = _detector.DetectRetryMetadata(testContext, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal("Retry", result.RetryAttributeName);
        Assert.Equal(3, result.MaxRetries);
    }

    [Fact]
    public void DetectRetryMetadata_WithRetryAttributeAndProperties_ExtractsAllMetadata()
    {
        var testContext = CreateMockTestContext(
            nameof(TestClass.TestMethodWithFullRetry),
            typeof(TestClass).FullName!);
        testContext.Properties["RetryAttempt"] = 2;

        var result = _detector.DetectRetryMetadata(testContext, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal("Retry", result.RetryAttributeName);
        Assert.Equal(5, result.MaxRetries);
        Assert.Equal(2, result.AttemptNumber);
        Assert.True(result.PassedOnRetry);
    }

    [Fact]
    public void DetectRetryMetadata_WithRetryAttribute_PassedOnRetry_IsTrue_WhenAttemptAboveOne()
    {
        var testContext = CreateMockTestContext(
            nameof(TestClass.TestMethodWithRetry),
            typeof(TestClass).FullName!);
        testContext.Properties["RetryAttempt"] = 2;

        var result = _detector.DetectRetryMetadata(testContext, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.True(result.PassedOnRetry);
    }

    [Fact]
    public void DetectRetryMetadata_WithRetryAttribute_PassedOnRetry_IsFalse_WhenFirstAttempt()
    {
        var testContext = CreateMockTestContext(
            nameof(TestClass.TestMethodWithRetry),
            typeof(TestClass).FullName!);

        var result = _detector.DetectRetryMetadata(testContext, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.False(result.PassedOnRetry);
        Assert.Equal(1, result.AttemptNumber);
    }

    [Fact]
    public void DetectRetryMetadata_WithoutRetryAttribute_ReturnsNull()
    {
        var testContext = CreateMockTestContext(
            nameof(TestClass.TestMethodWithoutRetry),
            typeof(TestClass).FullName!);

        var result = _detector.DetectRetryMetadata(testContext, TestOutcome.Passed);

        Assert.Null(result);
    }

    [Fact]
    public void DetectRetryMetadata_WithRetryCountProperty_ExtractsAttemptNumber()
    {
        var testContext = CreateMockTestContext(
            nameof(TestClass.TestMethodWithRetry),
            typeof(TestClass).FullName!);
        testContext.Properties["RetryCount"] = 2; // 0-indexed, so attempt 3

        var result = _detector.DetectRetryMetadata(testContext, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal(3, result.AttemptNumber);
    }

    [Fact]
    public void DetectRetryMetadata_WithRetryInTestName_ExtractsAttemptNumber()
    {
        var testContext = CreateMockTestContext(
            "TestMethodWithRetry (Retry 2)",
            typeof(TestClass).FullName!);

        var result = _detector.DetectRetryMetadata(testContext, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal(2, result.AttemptNumber);
    }

    [Fact]
    public void DetectRetryMetadata_WithFailedOutcome_PassedOnRetry_IsFalse()
    {
        var testContext = CreateMockTestContext(
            nameof(TestClass.TestMethodWithRetry),
            typeof(TestClass).FullName!);
        testContext.Properties["RetryAttempt"] = 2;

        var result = _detector.DetectRetryMetadata(testContext, TestOutcome.Failed);

        Assert.NotNull(result);
        Assert.False(result.PassedOnRetry);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Must return TestContext for API compatibility with detector")]
    private static TestContext CreateMockTestContext(string testName, string? fullClassName = null)
    {
        return new MockTestContext(testName, fullClassName);
    }

    /// <summary>
    /// Mock implementation of TestContext for testing.
    /// </summary>
    private sealed class MockTestContext : TestContext
    {
        private readonly string _testName;
        private readonly string? _fullClassName;

        public MockTestContext(string testName, string? fullClassName = null)
        {
            _testName = testName;
            _fullClassName = fullClassName;
        }

#pragma warning disable CS8609 // Nullability of reference types in return type doesn't match overridden member
        public override System.Collections.IDictionary Properties { get; } = new Dictionary<string, object?>();
#pragma warning restore CS8609

        public override string TestName => _testName;

        public override string? FullyQualifiedTestClassName => _fullClassName;

        public override UnitTestOutcome CurrentTestOutcome => UnitTestOutcome.Passed;

        public override void AddResultFile(string fileName) { }

        public override void WriteLine(string? message) { }

        public override void WriteLine(string format, params object?[]? args) { }

        public override void Write(string? message) { }

        public override void Write(string format, params object?[]? args) { }
    }

    // Test class with various retry scenarios
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Class is used via reflection in tests")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "MSTEST0003:Test method signature is invalid", Justification = "Test helper class used via reflection")]
    private sealed class TestClass
    {
        [TestMethod]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "MSTEST0003:Test method signature is invalid", Justification = "Test helper method")]
        public void TestMethodWithoutRetry() { }

        [TestMethod]
        [Retry(3)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "MSTEST0003:Test method signature is invalid", Justification = "Test helper method")]
        public void TestMethodWithRetry() { }

        [TestMethod]
        [Retry(5)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "MSTEST0003:Test method signature is invalid", Justification = "Test helper method")]
        public void TestMethodWithFullRetry() { }
    }

    /// <summary>
    /// Mock retry attribute for testing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    private sealed class RetryAttribute : Attribute
    {
        public int MaxRetries { get; }

        public RetryAttribute(int maxRetries)
        {
            MaxRetries = maxRetries;
        }
    }
}
