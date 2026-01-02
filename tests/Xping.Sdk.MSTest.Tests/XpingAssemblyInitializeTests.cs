/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.MSTest.Tests;

using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Tests for XpingAssemblyInitialize class.
/// </summary>
public class XpingAssemblyInitializeTests
{
    [Fact]
    public void XpingAssemblyInitialize_HasCorrectAttributes()
    {
        // Arrange
        var type = typeof(XpingAssemblyInitialize);

        // Act & Assert
        var testClassAttr = type.GetCustomAttribute<TestClassAttribute>();
        Assert.NotNull(testClassAttr);
    }

    [Fact]
    public void AssemblyInit_HasAssemblyInitializeAttribute()
    {
        // Arrange
        var method = typeof(XpingAssemblyInitialize).GetMethod("AssemblyInit");

        // Act & Assert
        Assert.NotNull(method);
        var attr = method.GetCustomAttribute<AssemblyInitializeAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void AssemblyInit_IsPublicStatic()
    {
        // Arrange
        var method = typeof(XpingAssemblyInitialize).GetMethod("AssemblyInit");

        // Act & Assert
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
        Assert.True(method.IsStatic);
    }

    [Fact]
    public void AssemblyInit_HasCorrectSignature()
    {
        // Arrange
        var method = typeof(XpingAssemblyInitialize).GetMethod("AssemblyInit");

        // Act & Assert
        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(TestContext), parameters[0].ParameterType);
    }

    [Fact]
    public void AssemblyCleanup_HasAssemblyCleanupAttribute()
    {
        // Arrange
        var method = typeof(XpingAssemblyInitialize).GetMethod("AssemblyCleanup");

        // Act & Assert
        Assert.NotNull(method);
        var attr = method.GetCustomAttribute<AssemblyCleanupAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void AssemblyCleanup_IsPublicStatic()
    {
        // Arrange
        var method = typeof(XpingAssemblyInitialize).GetMethod("AssemblyCleanup");

        // Act & Assert
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
        Assert.True(method.IsStatic);
    }

    [Fact]
    public void AssemblyCleanup_ReturnsTask()
    {
        // Arrange
        var method = typeof(XpingAssemblyInitialize).GetMethod("AssemblyCleanup");

        // Act & Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method.ReturnType);
    }

    [Fact]
    public void AssemblyCleanup_HasNoParameters()
    {
        // Arrange
        var method = typeof(XpingAssemblyInitialize).GetMethod("AssemblyCleanup");

        // Act & Assert
        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Empty(parameters);
    }

    [Fact]
    public void XpingAssemblyInitialize_IsStaticClass()
    {
        // Arrange
        var type = typeof(XpingAssemblyInitialize);

        // Act & Assert
        Assert.True(type.IsAbstract && type.IsSealed); // Static classes are abstract and sealed in IL
    }

    [Fact]
    public void XpingAssemblyInitialize_IsPublic()
    {
        // Arrange
        var type = typeof(XpingAssemblyInitialize);

        // Act & Assert
        Assert.True(type.IsPublic);
    }
}
