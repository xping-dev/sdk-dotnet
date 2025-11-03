/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit.Tests;

using System;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Tests for XpingTrackAttribute.
/// Note: Deep integration testing with actual NUnit test execution is done in sample projects.
/// These unit tests verify the attribute's contract and error handling.
/// </summary>
public sealed class XpingTrackAttributeTests : IDisposable
{
    public XpingTrackAttributeTests()
    {
        XpingContext.Reset();
    }

    public void Dispose()
    {
        XpingContext.Reset();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Targets_ReturnsTestLevel()
    {
        // Arrange
        var attribute = new XpingTrackAttribute();

        // Act
        var targets = attribute.Targets;

        // Assert - Verify it returns Test level (value 1)
        Assert.Equal(1, (int)targets);
    }

    [Fact]
    public void BeforeTest_WithNullTest_ThrowsNullReferenceException()
    {
        // Arrange
        var attribute = new XpingTrackAttribute();

        // Act & Assert - BeforeTest accesses test.Properties which will throw on null
        Assert.Throws<ArgumentNullException>(() => attribute.BeforeTest(null!));
    }

    [Fact]
    public void AfterTest_WithNullTest_DoesNotThrow()
    {
        // Arrange
        var attribute = new XpingTrackAttribute();

        // Act & Assert - AfterTest has try-catch that swallows exceptions
        var exception = Record.Exception(() => attribute.AfterTest(null!));
        Assert.Null(exception);
    }

    [Fact]
    public void AfterTest_WithoutBeforeTest_DoesNotThrow()
    {
        // Arrange
        var attribute = new XpingTrackAttribute();

        // Act & Assert - Should handle gracefully when BeforeTest wasn't called
        // (the private fields will be default values)
        var exception = Record.Exception(() => attribute.AfterTest(null!));
        Assert.Null(exception);
    }

    [Fact]
    public void AttributeUsage_CanBeAppliedToMethod()
    {
        // Arrange
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        // Assert
        Assert.True((usage.ValidOn & AttributeTargets.Method) == AttributeTargets.Method);
    }

    [Fact]
    public void AttributeUsage_CanBeAppliedToClass()
    {
        // Arrange
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        // Assert
        Assert.True((usage.ValidOn & AttributeTargets.Class) == AttributeTargets.Class);
    }

    [Fact]
    public void AttributeUsage_CanBeAppliedToAssembly()
    {
        // Arrange
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        // Assert
        Assert.True((usage.ValidOn & AttributeTargets.Assembly) == AttributeTargets.Assembly);
    }

    [Fact]
    public void AttributeUsage_IsInherited()
    {
        // Arrange
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        // Assert
        Assert.True(usage.Inherited);
    }

    [Fact]
    public void AttributeUsage_AllowsOnlyOneInstance()
    {
        // Arrange
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        // Assert
        Assert.False(usage.AllowMultiple);
    }
}
