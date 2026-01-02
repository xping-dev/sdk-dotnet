/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest.Tests;

using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Additional tests for XpingTestBase class functionality.
/// </summary>
public class XpingTestBaseAdditionalTests
{
    [Fact]
    public void XpingTestBase_HasTestContextProperty()
    {
        // Arrange
        var type = typeof(XpingTestBase);

        // Act
        var property = type.GetProperty("TestContext");

        // Assert
        Assert.NotNull(property);
        Assert.Equal(typeof(TestContext), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.True(property.CanWrite);
    }

    [Fact]
    public void XpingTestInitialize_HasTestInitializeAttribute()
    {
        // Arrange
        var method = typeof(XpingTestBase).GetMethod("XpingTestInitialize");

        // Act & Assert
        Assert.NotNull(method);
        var attr = method.GetCustomAttribute<TestInitializeAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void XpingTestInitialize_IsPublic()
    {
        // Arrange
        var method = typeof(XpingTestBase).GetMethod("XpingTestInitialize");

        // Act & Assert
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
    }

    [Fact]
    public void XpingTestCleanup_HasTestCleanupAttribute()
    {
        // Arrange
        var method = typeof(XpingTestBase).GetMethod("XpingTestCleanup");

        // Act & Assert
        Assert.NotNull(method);
        var attr = method.GetCustomAttribute<TestCleanupAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void XpingTestCleanup_IsPublic()
    {
        // Arrange
        var method = typeof(XpingTestBase).GetMethod("XpingTestCleanup");

        // Act & Assert
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
    }

    [Fact]
    public void XpingTestBase_IsAbstract()
    {
        // Arrange
        var type = typeof(XpingTestBase);

        // Act & Assert
        Assert.True(type.IsAbstract);
    }

    [Fact]
    public void XpingTestBase_IsPublic()
    {
        // Arrange
        var type = typeof(XpingTestBase);

        // Act & Assert
        Assert.True(type.IsPublic);
    }

    [Fact]
    public void XpingTestBase_CanBeInherited()
    {
        // Arrange & Act
        var derivedType = typeof(TestDerivedClass);

        // Assert
        Assert.True(typeof(XpingTestBase).IsAssignableFrom(derivedType));

        // Actually instantiate it to satisfy CA1812
        var instance = new TestDerivedClass();
        instance.TestMethod();
        Assert.NotNull(instance);
    }

    [Fact]
    public void ExtractMetadata_MethodExists()
    {
        // Arrange
        var method = typeof(XpingTestBase).GetMethod(
            "ExtractMetadata",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act & Assert
        Assert.NotNull(method);
    }

    [Fact]
    public void MapOutcome_MethodExists()
    {
        // Arrange
        var method = typeof(XpingTestBase).GetMethod(
            "MapOutcome",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act & Assert
        Assert.NotNull(method);
    }

    [Fact]
    public void CalculateDuration_MethodExists()
    {
        // Arrange
        var method = typeof(XpingTestBase).GetMethod(
            "CalculateDuration",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act & Assert
        Assert.NotNull(method);
    }

    [Fact]
    public void CreateTestExecution_MethodExists()
    {
        // Arrange
        var method = typeof(XpingTestBase).GetMethod(
            "CreateTestExecution",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act & Assert
        Assert.NotNull(method);
    }

    [Fact]
    public void ExtractAssemblyName_MethodExists()
    {
        // Arrange
        var method = typeof(XpingTestBase).GetMethod(
            "ExtractAssemblyName",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act & Assert
        Assert.NotNull(method);
    }

    [Fact]
    public void ExtractDataRowParameters_MethodExists()
    {
        // Arrange
        var method = typeof(XpingTestBase).GetMethod(
            "ExtractDataRowParameters",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act & Assert
        Assert.NotNull(method);
        Assert.NotNull(method.ReturnType);
        Assert.Equal(typeof(object[]), method.ReturnType);
    }

    [Fact]
    public void ExtractDataRowParameters_WithNullContext_ReturnsNull()
    {
        // Arrange
        var method = typeof(XpingTestBase).GetMethod(
            "ExtractDataRowParameters",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = method!.Invoke(null, new object?[] { null });

        // Assert
        Assert.Null(result);
    }

    // Test helper class
    private sealed class TestDerivedClass : XpingTestBase
    {
        public void TestMethod()
        {
            // Method to avoid CA1812
        }
    }
}
