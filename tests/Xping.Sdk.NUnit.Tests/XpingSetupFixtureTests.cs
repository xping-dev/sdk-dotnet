/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit.Tests;

using System.Reflection;
using System.Threading.Tasks;
using global::NUnit.Framework;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Tests for XpingSetupFixture class.
/// </summary>
public class XpingSetupFixtureTests
{
    [Fact]
    public void XpingSetupFixture_HasSetUpFixtureAttribute()
    {
        // Arrange
        var type = typeof(XpingSetupFixture);

        // Act
        var attr = type.GetCustomAttribute<SetUpFixtureAttribute>();

        // Assert
        Assert.NotNull(attr);
    }

    [Fact]
    public void XpingSetupFixture_IsPublic()
    {
        // Arrange
        var type = typeof(XpingSetupFixture);

        // Act & Assert
        Assert.True(type.IsPublic);
    }

    [Fact]
    public void BeforeAllTests_HasOneTimeSetUpAttribute()
    {
        // Arrange
        var method = typeof(XpingSetupFixture).GetMethod("BeforeAllTests");

        // Act & Assert
        Assert.NotNull(method);
        var attr = method.GetCustomAttribute<OneTimeSetUpAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void BeforeAllTests_IsPublic()
    {
        // Arrange
        var method = typeof(XpingSetupFixture).GetMethod("BeforeAllTests");

        // Act & Assert
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
    }

    [Fact]
    public void BeforeAllTests_ReturnsVoid()
    {
        // Arrange
        var method = typeof(XpingSetupFixture).GetMethod("BeforeAllTests");

        // Act & Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(void), method.ReturnType);
    }

    [Fact]
    public void AfterAllTests_HasOneTimeTearDownAttribute()
    {
        // Arrange
        var method = typeof(XpingSetupFixture).GetMethod("AfterAllTests");

        // Act & Assert
        Assert.NotNull(method);
        var attr = method.GetCustomAttribute<OneTimeTearDownAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void AfterAllTests_IsPublic()
    {
        // Arrange
        var method = typeof(XpingSetupFixture).GetMethod("AfterAllTests");

        // Act & Assert
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
    }

    [Fact]
    public void AfterAllTests_ReturnsTask()
    {
        // Arrange
        var method = typeof(XpingSetupFixture).GetMethod("AfterAllTests");

        // Act & Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method.ReturnType);
    }

    [Fact]
    public void AfterAllTests_HasNoParameters()
    {
        // Arrange
        var method = typeof(XpingSetupFixture).GetMethod("AfterAllTests");

        // Act & Assert
        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Empty(parameters);
    }

    [Fact]
    public void BeforeAllTests_HasNoParameters()
    {
        // Arrange
        var method = typeof(XpingSetupFixture).GetMethod("BeforeAllTests");

        // Act & Assert
        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Empty(parameters);
    }

    [Fact]
    public void XpingSetupFixture_HasDefaultConstructor()
    {
        // Arrange
        var type = typeof(XpingSetupFixture);

        // Act
        var constructors = type.GetConstructors();

        // Assert
        Assert.Single(constructors);
        Assert.Empty(constructors[0].GetParameters());
    }

    [Fact]
    public void XpingSetupFixture_CanBeInstantiated()
    {
        // Arrange & Act
        var instance = new XpingSetupFixture();

        // Assert
        Assert.NotNull(instance);
    }

    [Fact]
    public void BeforeAllTests_CanBeCalled()
    {
        // Arrange
        var instance = new XpingSetupFixture();

        // Act & Assert - should not throw
        instance.BeforeAllTests();
    }

    [Fact]
    public async Task AfterAllTests_CanBeCalled()
    {
        // Arrange
        var instance = new XpingSetupFixture();
        instance.BeforeAllTests(); // Initialize first

        // Act & Assert - should not throw
        await instance.AfterAllTests();
    }

    [Fact]
    public async Task BeforeAllTests_ThenAfterAllTests_ExecutesSuccessfully()
    {
        // Arrange
        var instance = new XpingSetupFixture();

        // Act
        instance.BeforeAllTests();
        await instance.AfterAllTests();

        // Assert - completed without exceptions
        Assert.True(true);
    }

    [Fact]
    public void BeforeAllTests_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var instance = new XpingSetupFixture();

        // Act & Assert - should handle multiple calls gracefully
        instance.BeforeAllTests();
        instance.BeforeAllTests();
    }
}
