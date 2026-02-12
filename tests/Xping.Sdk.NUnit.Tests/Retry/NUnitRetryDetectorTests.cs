/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Services.Retry;

#pragma warning disable NUnit1028, CA1812, CA1859, CA1515

namespace Xping.Sdk.NUnit.Tests.Retry;

using System;
using System.Reflection;
using global::NUnit.Framework;
using global::NUnit.Framework.Internal;
using Xping.Sdk.NUnit.Retry;
using Xunit;
using Assert = Xunit.Assert;

public sealed class NUnitRetryDetectorTests
{
    private readonly NUnitRetryDetector _detector = new();

    [Fact]
    public void DetectRetryMetadata_WithNullTest_ReturnsNull()
    {
        // Act
        var result = _detector.DetectRetryMetadata(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void DetectRetryMetadata_WithTestWithoutRetryAttribute_ReturnsNull()
    {
        // Arrange
        var test = CreateMockTest(nameof(SampleTestClass.TestMethod));

        // Act
        var result = _detector.DetectRetryMetadata(test);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void DetectRetryMetadata_WithRetryAttribute_ReturnsMetadata()
    {
        // Arrange - NUnit's native [Retry] attribute
        var test = CreateMockTest(nameof(SampleTestClass.TestWithRetry));

        // Act
        var result = _detector.DetectRetryMetadata(test);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Retry", result.RetryAttributeName);
        Assert.Equal(3, result.MaxRetries);
    }

    [Fact]
    public void DetectRetryMetadata_WithCustomRetryAttribute_ReturnsMetadata()
    {
        // Arrange - Register custom retry attribute
        RetryAttributeRegistry.RegisterCustomRetryAttribute("nunit", "CustomRetry");
        var test = CreateMockTest(nameof(SampleTestClass.TestWithCustomRetry));

        // Act
        var result = _detector.DetectRetryMetadata(test);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("CustomRetry", result.RetryAttributeName);
        Assert.Equal(5, result.MaxRetries);
    }

    [Fact]
    public void HasRetryAttribute_WithRetryAttribute_ReturnsTrue()
    {
        // Arrange
        var test = CreateMockTest(nameof(SampleTestClass.TestWithRetry));

        // Act
        var result = _detector.HasRetryAttribute(test);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasRetryAttribute_WithoutRetryAttribute_ReturnsFalse()
    {
        // Arrange
        var test = CreateMockTest(nameof(SampleTestClass.TestMethod));

        // Act
        var result = _detector.HasRetryAttribute(test);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetCurrentAttemptNumber_WithDefaultTest_ReturnsOne()
    {
        // Arrange
        var test = CreateMockTest(nameof(SampleTestClass.TestMethod));

        // Act
        var result = _detector.GetCurrentAttemptNumber(test);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void GetCurrentAttemptNumber_WithRetryCountProperty_ReturnsAttemptNumber()
    {
        // Arrange
        var test = CreateMockTest(nameof(SampleTestClass.TestMethod));
        test.Properties.Set("_RETRY_COUNT", 2);

        // Act
        var result = _detector.GetCurrentAttemptNumber(test);

        // Assert
        Assert.Equal(3, result); // _RETRY_COUNT is 0-indexed
    }

    [Fact]
    public void GetCurrentAttemptNumber_WithRetryInTestName_ReturnsAttemptNumber()
    {
        // Arrange
        var test = CreateMockTestWithName("TestMethod (Retry 3)");

        // Act
        var result = _detector.GetCurrentAttemptNumber(test);

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public void DetectRetryMetadata_PassedOnRetry_SetsPassedOnRetryFlag()
    {
        // Arrange
        var test = CreateMockTest(nameof(SampleTestClass.TestWithRetry));
        test.Properties.Set("_RETRY_COUNT", 1); // Second attempt

        // Act
        var result = _detector.DetectRetryMetadata(test);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.AttemptNumber);
        Assert.True(result.PassedOnRetry);
    }

    [Fact]
    public void DetectRetryMetadata_WithAdditionalProperties_ExtractsAdditionalMetadata()
    {
        // Arrange
        var test = CreateMockTest(nameof(SampleTestClass.TestWithAdditionalProperties));

        // Act
        var result = _detector.DetectRetryMetadata(test);

        // Assert
        Assert.NotNull(result);
        // Additional properties would be stored in AdditionalMetadata if they exist on the attribute
        Assert.NotNull(result.AdditionalMetadata);
    }

    // Helper methods
    private static TestMethod CreateMockTest(string methodName)
    {
        var type = typeof(SampleTestClass);
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);

        if (method == null)
        {
            throw new InvalidOperationException($"Method {methodName} not found on {type.Name}");
        }

        var testMethod = new TestMethod(new MethodWrapper(type, method))
        {
            Name = methodName
        };

        return testMethod;
    }

    private static TestMethod CreateMockTestWithName(string testName)
    {
        var type = typeof(SampleTestClass);
        var method = type.GetMethod(nameof(SampleTestClass.TestMethod), BindingFlags.Public | BindingFlags.Instance);

        if (method == null)
        {
            throw new InvalidOperationException("TestMethod not found");
        }

        var testMethod = new TestMethod(new MethodWrapper(type, method))
        {
            Name = testName
        };

        return testMethod;
    }

    // Sample test class with retry attributes
    private sealed class SampleTestClass
    {
        public void TestMethod() { }

        [Retry(3)]
        public void TestWithRetry() { }

        [CustomRetry(Count = 5)]
        public void TestWithCustomRetry() { }

        [Retry(3)]
        public void TestWithAdditionalProperties() { }
    }

    // Mock retry attributes
    [AttributeUsage(AttributeTargets.Method)]
    private sealed class CustomRetryAttribute : Attribute
    {
        public int Count { get; set; }
    }
}
