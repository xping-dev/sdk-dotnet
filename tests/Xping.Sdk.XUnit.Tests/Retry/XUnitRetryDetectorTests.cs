/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

#pragma warning disable xUnit1000, xUnit1026, xUnit3000, xUnit3001, CA1812, CA1859

namespace Xping.Sdk.XUnit.Tests.Retry;

using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using Xping.Sdk.Core.Retry;
using Xping.Sdk.XUnit.Retry;

public sealed class XUnitRetryDetectorTests
{
    private readonly XUnitRetryDetector _detector = new();

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
        var test = CreateMockTest("TestMethod");

        // Act
        var result = _detector.DetectRetryMetadata(test);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void DetectRetryMetadata_WithRetryFactAttribute_ReturnsMetadata()
    {
        // Arrange - This test verifies detection of RetryFact from xunit.extensions.retry
        var test = CreateMockTest(nameof(SampleTestClass.TestWithRetryFact));

        // Act
        var result = _detector.DetectRetryMetadata(test);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("RetryFact", result.RetryAttributeName);
        Assert.Equal(3, result.MaxRetries);
    }

    [Fact]
    public void DetectRetryMetadata_WithRetryTheoryAttribute_ReturnsMetadata()
    {
        // Arrange
        var test = CreateMockTest(nameof(SampleTestClass.TestWithRetryTheory));

        // Act
        var result = _detector.DetectRetryMetadata(test);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("RetryTheory", result.RetryAttributeName);
        Assert.Equal(5, result.MaxRetries);
    }

    [Fact]
    public void DetectRetryMetadata_WithCustomRetryAttribute_ReturnsMetadata()
    {
        // Arrange - Register custom retry attribute
        RetryAttributeRegistry.RegisterCustomRetryAttribute("xunit", "CustomRetry");
        var test = CreateMockTest(nameof(SampleTestClass.TestWithCustomRetryAttribute));

        // Act
        var result = _detector.DetectRetryMetadata(test);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("CustomRetry", result.RetryAttributeName);
        Assert.Equal(10, result.MaxRetries);
    }

    [Fact]
    public void DetectRetryMetadata_WithDelayProperty_ExtractsDelay()
    {
        // Arrange
        var test = CreateMockTest(nameof(SampleTestClass.TestWithDelayProperty));

        // Act
        var result = _detector.DetectRetryMetadata(test);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), result.DelayBetweenRetries);
    }

    [Fact]
    public void DetectRetryMetadata_WithReasonProperty_ExtractsReason()
    {
        // Arrange
        var test = CreateMockTest(nameof(SampleTestClass.TestWithReasonProperty));

        // Act
        var result = _detector.DetectRetryMetadata(test);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Flaky test", result.RetryReason);
    }

    [Fact]
    public void HasRetryAttribute_WithRetryAttribute_ReturnsTrue()
    {
        // Arrange
        var test = CreateMockTest(nameof(SampleTestClass.TestWithRetryFact));

        // Act
        var result = _detector.HasRetryAttribute(test);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasRetryAttribute_WithoutRetryAttribute_ReturnsFalse()
    {
        // Arrange
        var test = CreateMockTest("TestMethod");

        // Act
        var result = _detector.HasRetryAttribute(test);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetCurrentAttemptNumber_WithDefaultTest_ReturnsOne()
    {
        // Arrange
        var test = CreateMockTest("TestMethod");

        // Act
        var result = _detector.GetCurrentAttemptNumber(test);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void GetCurrentAttemptNumber_WithRetryAttemptTrait_ReturnsAttemptNumber()
    {
        // Arrange
        var test = CreateMockTestWithTraits("TestMethod", new Dictionary<string, List<string>>
        {
            ["RetryAttempt"] = ["3"]
        });

        // Act
        var result = _detector.GetCurrentAttemptNumber(test);

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public void GetCurrentAttemptNumber_WithAttemptInDisplayName_ReturnsAttemptNumber()
    {
        // Arrange
        var test = CreateMockTestWithDisplayName("TestMethod (attempt 2)");

        // Act
        var result = _detector.GetCurrentAttemptNumber(test);

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void GetCurrentAttemptNumber_WithRetryInDisplayName_ReturnsAttemptNumber()
    {
        // Arrange
        var test = CreateMockTestWithDisplayName("TestMethod [Retry 4]");

        // Act
        var result = _detector.GetCurrentAttemptNumber(test);

        // Assert
        Assert.Equal(4, result);
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
        Assert.Contains("Timeout", result.AdditionalMetadata.Keys);
    }

    // Helper methods to create mock tests
    private static ITest CreateMockTest(string methodName)
    {
        var type = typeof(SampleTestClass);
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);

        if (method == null)
        {
            throw new InvalidOperationException($"Method {methodName} not found on {type.Name}");
        }

        return new MockTest(method);
    }

    private static ITest CreateMockTestWithTraits(string methodName, Dictionary<string, List<string>> traits)
    {
        return new MockTest(methodName, displayName: methodName, traits: traits);
    }

    private static ITest CreateMockTestWithDisplayName(string displayName)
    {
        return new MockTest("TestMethod", displayName);
    }

    // Sample test class with retry attributes
    private sealed class SampleTestClass
    {
        public void TestMethod() { }

        [RetryFact(MaxRetries = 3)]
        public void TestWithRetryFact() { }

        [RetryTheory(MaxRetries = 5)]
        [InlineData(1)]
        public void TestWithRetryTheory(int value) { }

        [CustomRetry(MaxRetries = 10)]
        public void TestWithCustomRetryAttribute() { }

        [RetryFact(MaxRetries = 3, DelayMilliseconds = 1000)]
        public void TestWithDelayProperty() { }

        [RetryFact(MaxRetries = 3, Reason = "Flaky test")]
        public void TestWithReasonProperty() { }

        [RetryFact(MaxRetries = 3, Timeout = 5000)]
        public void TestWithAdditionalProperties() { }
    }

    // Mock retry attributes
    [AttributeUsage(AttributeTargets.Method)]
    private sealed class RetryFactAttribute : FactAttribute
    {
        public int MaxRetries { get; set; }
        public int DelayMilliseconds { get; set; }
        public string? Reason { get; set; }
        public new int Timeout { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    private sealed class RetryTheoryAttribute : TheoryAttribute
    {
        public int MaxRetries { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    private sealed class CustomRetryAttribute : Attribute
    {
        public int MaxRetries { get; set; }
    }

    // Mock implementation of ITest
    private sealed class MockTest : ITest
    {
        private readonly MockTestCase _testCase;

        public MockTest(MethodInfo method)
        {
            _testCase = new MockTestCase(method);
            DisplayName = method.Name;
        }

        public MockTest(string methodName, string displayName, Dictionary<string, List<string>>? traits = null)
        {
            _testCase = new MockTestCase(methodName, traits);
            DisplayName = displayName;
        }

        public ITestCase TestCase => _testCase;
        public string DisplayName { get; }
    }

    private sealed class MockTestCase : ITestCase
    {
        private readonly MockTestMethod _testMethod;
        private readonly Dictionary<string, List<string>> _traits;

        public MockTestCase(MethodInfo method)
        {
            _testMethod = new MockTestMethod(method);
            _traits = new Dictionary<string, List<string>>();
        }

        public MockTestCase(string methodName, Dictionary<string, List<string>>? traits)
        {
            _testMethod = new MockTestMethod(methodName);
            _traits = traits ?? new Dictionary<string, List<string>>();
        }

        public ITestMethod TestMethod => _testMethod;
        public Dictionary<string, List<string>> Traits => _traits;

        // Not used in tests
        public string DisplayName => TestMethod.Method.Name;
        public string? SkipReason => null;
        public ISourceInformation? SourceInformation { get; set; }
        public object?[]? TestMethodArguments => null;
        public string? UniqueID => TestMethod.Method.Name;

        public void Deserialize(IXunitSerializationInfo info) { }
        public void Serialize(IXunitSerializationInfo info) { }
    }

    private sealed class MockTestMethod : ITestMethod
    {
        private readonly MockMethodInfo _method;
        private readonly MockTestClass _testClass;

        public MockTestMethod(MethodInfo method)
        {
            _method = new MockMethodInfo(method);
            _testClass = new MockTestClass(method.DeclaringType!);
        }

        public MockTestMethod(string methodName)
        {
            _method = new MockMethodInfo(methodName);
            _testClass = new MockTestClass(typeof(SampleTestClass));
        }

        public IMethodInfo Method => _method;
        public ITestClass TestClass => _testClass;

        public void Deserialize(IXunitSerializationInfo info) { }
        public void Serialize(IXunitSerializationInfo info) { }
    }

    private sealed class MockTestClass : ITestClass
    {
        private readonly MockTypeInfo _class;
        private readonly MockTestCollection _testCollection;

        public MockTestClass(Type type)
        {
            _class = new MockTypeInfo(type);
            _testCollection = new MockTestCollection();
        }

        public ITypeInfo Class => _class;
        public ITestCollection TestCollection => _testCollection;

        public void Deserialize(IXunitSerializationInfo info) { }
        public void Serialize(IXunitSerializationInfo info) { }
    }

    private sealed class MockTestCollection : ITestCollection
    {
        public string DisplayName => "Test Collection";
        public ITestAssembly TestAssembly => null!;
        public ITypeInfo? CollectionDefinition => null;
        public Guid UniqueID => Guid.NewGuid();

        public void Deserialize(IXunitSerializationInfo info) { }
        public void Serialize(IXunitSerializationInfo info) { }
    }

    private sealed class MockMethodInfo : IMethodInfo
    {
        private readonly MethodInfo? _methodInfo;

        public MockMethodInfo(MethodInfo methodInfo)
        {
            _methodInfo = methodInfo;
            Name = methodInfo.Name;
            Type = new MockTypeInfo(methodInfo.DeclaringType!);
        }

        public MockMethodInfo(string methodName)
        {
            Name = methodName;
            Type = new MockTypeInfo(typeof(SampleTestClass));
        }

        public string Name { get; }
        public ITypeInfo Type { get; }

        // Not used in tests
        public bool IsAbstract => false;
        public bool IsGenericMethodDefinition => false;
        public bool IsPublic => true;
        public bool IsStatic => false;
        public ITypeInfo ReturnType => null!;

        public IEnumerable<IAttributeInfo> GetCustomAttributes(string assemblyQualifiedAttributeTypeName)
        {
            if (_methodInfo != null)
            {
                var attributes = _methodInfo.GetCustomAttributes(true);
                foreach (var attr in attributes)
                {
                    yield return new MockAttributeInfo(attr);
                }
            }
        }

        public IEnumerable<ITypeInfo> GetGenericArguments() => [];
        public IEnumerable<IParameterInfo> GetParameters() => [];
        public IMethodInfo MakeGenericMethod(params ITypeInfo[] typeArguments) => this;
    }

    private sealed class MockTypeInfo : ITypeInfo
    {
        private readonly Type _type;

        public MockTypeInfo(Type type)
        {
            _type = type;
            Name = type.FullName ?? type.Name;
        }

        public string Name { get; }
        public IAssemblyInfo Assembly => new MockAssemblyInfo(_type.Assembly);

        // Not used in tests
        public ITypeInfo? BaseType => null;
        public IEnumerable<ITypeInfo> Interfaces => [];
        public bool IsAbstract => false;
        public bool IsGenericParameter => false;
        public bool IsGenericType => false;
        public bool IsSealed => false;
        public bool IsValueType => false;

        public IEnumerable<IAttributeInfo> GetCustomAttributes(string assemblyQualifiedAttributeTypeName) => [];
        public IEnumerable<ITypeInfo> GetGenericArguments() => [];
        public IMethodInfo? GetMethod(string methodName, bool includePrivateMethod) => null;
        public IEnumerable<IMethodInfo> GetMethods(bool includePrivateMethods) => [];
    }

    private sealed class MockAssemblyInfo : IAssemblyInfo
    {
        public MockAssemblyInfo(Assembly assembly)
        {
            Name = assembly.GetName().Name ?? "TestAssembly";
            AssemblyPath = assembly.Location;
        }

        public string Name { get; }
        public string AssemblyPath { get; }

        public IEnumerable<IAttributeInfo> GetCustomAttributes(string assemblyQualifiedAttributeTypeName) => [];
        public ITypeInfo? GetType(string typeName) => null;
        public IEnumerable<ITypeInfo> GetTypes(bool includePrivateTypes) => [];
    }

    private sealed class MockAttributeInfo : IAttributeInfo
    {
        private readonly object _attribute;

        public MockAttributeInfo(object attribute)
        {
            _attribute = attribute;
        }

        public IEnumerable<object> GetConstructorArguments() => [];

        public IEnumerable<IAttributeInfo> GetCustomAttributes(string assemblyQualifiedAttributeTypeName) => [];

        public TValue GetNamedArgument<TValue>(string argumentName)
        {
            var property = _attribute.GetType().GetProperty(argumentName);
            if (property != null)
            {
                var value = property.GetValue(_attribute);
                if (value is TValue typedValue)
                {
                    return typedValue;
                }
            }

            return default!;
        }
    }
}
