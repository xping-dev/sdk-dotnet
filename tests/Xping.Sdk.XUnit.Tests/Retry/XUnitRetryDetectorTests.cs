/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics.CodeAnalysis;
using Moq;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.Retry;
using Xunit.Abstractions;

namespace Xping.Sdk.XUnit.Tests.Retry;

/// <summary>
/// Tests for the internal <c>XUnitRetryDetector</c>, exercised via
/// <see cref="XpingContext.GetExecutorServices()"/>.
/// </summary>
[Collection("XpingContext")]
public sealed class XUnitRetryDetectorTests : IAsyncLifetime
{
    private IRetryDetector<ITest> _detector = null!;

    public async Task InitializeAsync()
    {
        await XpingContext.ShutdownAsync().ConfigureAwait(false);
        XpingContext.Initialize();
        _detector = XpingContext.GetExecutorServices().RetryDetector;

        // Register the inner RetryAttribute so the detector recognises it.
        // "RetryAttribute" → stripped to "Retry" by the detector.
        RetryAttributeRegistry.RegisterCustomRetryAttribute("xunit", "Retry");
    }

    public Task DisposeAsync() => XpingContext.ShutdownAsync().AsTask();

    // ---------------------------------------------------------------------------
    // Guard clauses — null/missing test structure
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectRetryMetadata_NullTestCase_ReturnsNull()
    {
        var mockTest = new Mock<ITest>();
        mockTest.Setup(t => t.TestCase).Returns((ITestCase)null!);

        var result = _detector.DetectRetryMetadata(mockTest.Object, TestOutcome.Passed);

        Assert.Null(result);
    }

    [Fact]
    public void DetectRetryMetadata_NullTestMethod_ReturnsNull()
    {
        var mockTestCase = new Mock<ITestCase>();
        mockTestCase.Setup(tc => tc.TestMethod).Returns((ITestMethod)null!);

        var mockTest = new Mock<ITest>();
        mockTest.Setup(t => t.TestCase).Returns(mockTestCase.Object);

        var result = _detector.DetectRetryMetadata(mockTest.Object, TestOutcome.Passed);

        Assert.Null(result);
    }

    [Fact]
    public void DetectRetryMetadata_NullMethod_ReturnsNull()
    {
        var mockTestMethod = new Mock<ITestMethod>();
        mockTestMethod.Setup(tm => tm.Method).Returns((IMethodInfo)null!);

        var mockTestCase = new Mock<ITestCase>();
        mockTestCase.Setup(tc => tc.TestMethod).Returns(mockTestMethod.Object);

        var mockTest = new Mock<ITest>();
        mockTest.Setup(t => t.TestCase).Returns(mockTestCase.Object);

        var result = _detector.DetectRetryMetadata(mockTest.Object, TestOutcome.Passed);

        Assert.Null(result);
    }

    [Fact]
    public void DetectRetryMetadata_UnknownTypeName_ReturnsNull()
    {
        // GetMethodInfo returns null when the type is not found in any loaded assembly.
        var mockTest = CreateMockTest("NonExistent.Fully.Qualified.Type", "SomeMethod");

        var result = _detector.DetectRetryMetadata(mockTest, TestOutcome.Passed);

        Assert.Null(result);
    }

    // ---------------------------------------------------------------------------
    // Attribute detection
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectRetryMetadata_WithRetryAttribute_ReturnsMetadata()
    {
        var mockTest = CreateMockTest(
            typeof(TestClass).FullName!,
            nameof(TestClass.TestMethodWithRetry));

        var result = _detector.DetectRetryMetadata(mockTest, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal("Retry", result.RetryAttributeName);
        Assert.Equal(3, result.MaxRetries);
    }

    [Fact]
    public void DetectRetryMetadata_WithoutRetryAttribute_ReturnsNull()
    {
        var mockTest = CreateMockTest(
            typeof(TestClass).FullName!,
            nameof(TestClass.TestMethodWithoutRetry));

        var result = _detector.DetectRetryMetadata(mockTest, TestOutcome.Passed);

        Assert.Null(result);
    }

    [Fact]
    public void DetectRetryMetadata_WithDelayProperty_ExtractsDelay()
    {
        var mockTest = CreateMockTest(
            typeof(TestClass).FullName!,
            nameof(TestClass.TestMethodWithDelay));

        var result = _detector.DetectRetryMetadata(mockTest, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromMilliseconds(200), result.DelayBetweenRetries);
    }

    // ---------------------------------------------------------------------------
    // Attempt number — first attempt default
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectRetryMetadata_FirstAttempt_AttemptNumberIsOne()
    {
        var mockTest = CreateMockTest(
            typeof(TestClass).FullName!,
            nameof(TestClass.TestMethodWithRetry));

        var result = _detector.DetectRetryMetadata(mockTest, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal(1, result.AttemptNumber);
        Assert.False(result.PassedOnRetry);
    }

    // ---------------------------------------------------------------------------
    // Attempt number — from traits
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectRetryMetadata_WithRetryAttemptTrait_ExtractsAttemptNumber()
    {
        var traits = new Dictionary<string, List<string>>
        {
            ["RetryAttempt"] = ["2"]
        };
        var mockTest = CreateMockTest(
            typeof(TestClass).FullName!,
            nameof(TestClass.TestMethodWithRetry),
            traits: traits);

        var result = _detector.DetectRetryMetadata(mockTest, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal(2, result.AttemptNumber);
        Assert.True(result.PassedOnRetry);
    }

    [Fact]
    public void DetectRetryMetadata_WithRetryAttemptTrait_InvalidValue_DefaultsToOne()
    {
        var traits = new Dictionary<string, List<string>>
        {
            ["RetryAttempt"] = ["not-a-number"]
        };
        var mockTest = CreateMockTest(
            typeof(TestClass).FullName!,
            nameof(TestClass.TestMethodWithRetry),
            traits: traits);

        var result = _detector.DetectRetryMetadata(mockTest, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal(1, result.AttemptNumber);
    }

    // ---------------------------------------------------------------------------
    // Attempt number — from display name
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectRetryMetadata_WithDisplayName_AttemptPattern_ExtractsAttemptNumber()
    {
        // Format: "TestName (attempt 3)"
        var mockTest = CreateMockTest(
            typeof(TestClass).FullName!,
            nameof(TestClass.TestMethodWithRetry),
            displayName: "TestMethodWithRetry (attempt 3)");

        var result = _detector.DetectRetryMetadata(mockTest, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal(3, result.AttemptNumber);
    }

    [Fact]
    public void DetectRetryMetadata_WithDisplayName_RetryPattern_ExtractsAttemptNumber()
    {
        // Format: "TestName [retry 2]"
        var mockTest = CreateMockTest(
            typeof(TestClass).FullName!,
            nameof(TestClass.TestMethodWithRetry),
            displayName: "TestMethodWithRetry [retry 2]");

        var result = _detector.DetectRetryMetadata(mockTest, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal(2, result.AttemptNumber);
    }

    [Fact]
    public void DetectRetryMetadata_WithDisplayName_NoPattern_DefaultsToOne()
    {
        var mockTest = CreateMockTest(
            typeof(TestClass).FullName!,
            nameof(TestClass.TestMethodWithRetry),
            displayName: "TestMethodWithRetry");

        var result = _detector.DetectRetryMetadata(mockTest, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal(1, result.AttemptNumber);
    }

    // ---------------------------------------------------------------------------
    // PassedOnRetry
    // ---------------------------------------------------------------------------

    [Fact]
    public void DetectRetryMetadata_WithFailedOutcome_PassedOnRetry_IsFalse()
    {
        var traits = new Dictionary<string, List<string>>
        {
            ["RetryAttempt"] = ["2"]
        };
        var mockTest = CreateMockTest(
            typeof(TestClass).FullName!,
            nameof(TestClass.TestMethodWithRetry),
            traits: traits);

        var result = _detector.DetectRetryMetadata(mockTest, TestOutcome.Failed);

        Assert.NotNull(result);
        Assert.False(result.PassedOnRetry);
    }

    [Fact]
    public void DetectRetryMetadata_FirstAttemptPassed_PassedOnRetry_IsFalse()
    {
        var mockTest = CreateMockTest(
            typeof(TestClass).FullName!,
            nameof(TestClass.TestMethodWithRetry));

        var result = _detector.DetectRetryMetadata(mockTest, TestOutcome.Passed);

        Assert.NotNull(result);
        Assert.Equal(1, result.AttemptNumber);
        Assert.False(result.PassedOnRetry);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static ITest CreateMockTest(
        string typeName,
        string methodName,
        string? displayName = null,
        Dictionary<string, List<string>>? traits = null)
    {
        var mockTypeInfo = new Mock<ITypeInfo>();
        mockTypeInfo.Setup(ti => ti.Name).Returns(typeName);

        var mockMethodInfo = new Mock<IMethodInfo>();
        mockMethodInfo.Setup(mi => mi.Name).Returns(methodName);
        mockMethodInfo.Setup(mi => mi.Type).Returns(mockTypeInfo.Object);

        var mockTestMethod = new Mock<ITestMethod>();
        mockTestMethod.Setup(tm => tm.Method).Returns(mockMethodInfo.Object);

        var mockTestCase = new Mock<ITestCase>();
        mockTestCase.Setup(tc => tc.TestMethod).Returns(mockTestMethod.Object);
        mockTestCase.Setup(tc => tc.Traits).Returns(
            traits ?? new Dictionary<string, List<string>>());

        var mockTest = new Mock<ITest>();
        mockTest.Setup(t => t.TestCase).Returns(mockTestCase.Object);
        mockTest.Setup(t => t.DisplayName).Returns(displayName ?? methodName);

        return mockTest.Object;
    }

    // ---------------------------------------------------------------------------
    // Test helper class (accessed via reflection by the detector)
    // ---------------------------------------------------------------------------

    [SuppressMessage("Performance", "CA1812", Justification = "Instantiated via reflection")]
    private sealed class TestClass
    {
        public static void TestMethodWithoutRetry() { }

        [Retry(3)]
        public static void TestMethodWithRetry() { }

        [Retry(3, DelayMilliseconds = 200)]
        public static void TestMethodWithDelay() { }
    }

    /// <summary>Mock retry attribute applied in <see cref="TestClass"/>.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    private sealed class RetryAttribute(int maxRetries) : Attribute
    {
        public int MaxRetries { get; } = maxRetries;
        public int DelayMilliseconds { get; set; }
    }
}
