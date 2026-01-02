/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest.Tests.Retry;

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xping.Sdk.Core.Retry;
using Xping.Sdk.MSTest.Retry;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Tests for the MSTestRetryDetector class.
/// </summary>
public sealed class MSTestRetryDetectorTests
{
    private readonly MSTestRetryDetector _detector;

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
        // Act
        var result = _detector.DetectRetryMetadata(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void HasRetryAttribute_WithNullTestContext_ReturnsFalse()
    {
        // Act
        var result = _detector.HasRetryAttribute(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetCurrentAttemptNumber_WithNullTestContext_ReturnsOne()
    {
        // Act
        var result = _detector.GetCurrentAttemptNumber(null!);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void GetCurrentAttemptNumber_WithRetryAttemptProperty_ReturnsCorrectNumber()
    {
        // Arrange
        var testContext = CreateMockTestContext("TestMethod");
        testContext.Properties["RetryAttempt"] = 3;

        // Act
        var result = _detector.GetCurrentAttemptNumber(testContext);

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public void GetCurrentAttemptNumber_WithRetryCountProperty_ReturnsCorrectNumber()
    {
        // Arrange
        var testContext = CreateMockTestContext("TestMethod");
        testContext.Properties["RetryCount"] = 2; // 0-indexed, so attempt 3

        // Act
        var result = _detector.GetCurrentAttemptNumber(testContext);

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public void GetCurrentAttemptNumber_WithRetryInTestName_ReturnsCorrectNumber()
    {
        // Arrange
        var testContext = CreateMockTestContext("TestMethod (Retry 2)");

        // Act
        var result = _detector.GetCurrentAttemptNumber(testContext);

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void GetCurrentAttemptNumber_WithAttemptInTestName_ReturnsCorrectNumber()
    {
        // Arrange
        var testContext = CreateMockTestContext("TestMethod [Attempt 4]");

        // Act
        var result = _detector.GetCurrentAttemptNumber(testContext);

        // Assert
        Assert.Equal(4, result);
    }

    [Fact]
    public void GetCurrentAttemptNumber_WithNoRetryInfo_ReturnsOne()
    {
        // Arrange
        var testContext = CreateMockTestContext("TestMethod");

        // Act
        var result = _detector.GetCurrentAttemptNumber(testContext);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void DetectRetryMetadata_WithRetryAttribute_ReturnsMetadata()
    {
        // Arrange
        var testContext = CreateMockTestContext(
            nameof(TestClass.TestMethodWithRetry),
            typeof(TestClass).FullName!);

        // Act
        var result = _detector.DetectRetryMetadata(testContext);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Retry", result.RetryAttributeName);
        Assert.Equal(3, result.MaxRetries);
    }

    [Fact]
    public void DetectRetryMetadata_WithRetryAttributeAndProperties_ExtractsAllMetadata()
    {
        // Arrange
        var testContext = CreateMockTestContext(
            nameof(TestClass.TestMethodWithFullRetry),
            typeof(TestClass).FullName!);
        testContext.Properties["RetryAttempt"] = 2;

        // Act
        var result = _detector.DetectRetryMetadata(testContext);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Retry", result.RetryAttributeName);
        Assert.Equal(5, result.MaxRetries);
        Assert.Equal(2, result.AttemptNumber);
        Assert.True(result.PassedOnRetry);
    }

    [Fact]
    public void HasRetryAttribute_WithRetryAttribute_ReturnsTrue()
    {
        // Arrange
        var testContext = CreateMockTestContext(
            nameof(TestClass.TestMethodWithRetry),
            typeof(TestClass).FullName!);

        // Act
        var result = _detector.HasRetryAttribute(testContext);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasRetryAttribute_WithoutRetryAttribute_ReturnsFalse()
    {
        // Arrange
        var testContext = CreateMockTestContext(
            nameof(TestClass.TestMethodWithoutRetry),
            typeof(TestClass).FullName!);

        // Act
        var result = _detector.HasRetryAttribute(testContext);

        // Assert
        Assert.False(result);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Must return TestContext for API compatibility with detector")]
    private static TestContext CreateMockTestContext(string testName, string? fullClassName = null)
    {
        return new MockTestContext(testName, fullClassName);
    }

    /// <summary>
    /// Mock implementation of TestContext for testing.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Must return TestContext for API compatibility")]
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

        // Override other required members with minimal implementations
        public override UnitTestOutcome CurrentTestOutcome => UnitTestOutcome.Passed;

        public override void AddResultFile(string fileName)
        {
            // Not used in tests
        }

        public override void WriteLine(string? message)
        {
            // Not used in tests
        }

        public override void WriteLine(string format, params object?[]? args)
        {
            // Not used in tests
        }

        public override void Write(string? message)
        {
            // Not used in tests
        }

        public override void Write(string format, params object?[]? args)
        {
            // Not used in tests
        }
    }

    // Test class with various retry scenarios
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Class is used via reflection in tests")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "MSTEST0003:Test method signature is invalid", Justification = "Test helper class used via reflection")]
    private sealed class TestClass
    {
        [TestMethod]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "MSTEST0003:Test method signature is invalid", Justification = "Test helper method")]
        public void TestMethodWithoutRetry()
        {
        }

        [TestMethod]
        [Retry(3)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "MSTEST0003:Test method signature is invalid", Justification = "Test helper method")]
        public void TestMethodWithRetry()
        {
        }

        [TestMethod]
        [Retry(5)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "MSTEST0003:Test method signature is invalid", Justification = "Test helper method")]
        public void TestMethodWithFullRetry()
        {
        }
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
