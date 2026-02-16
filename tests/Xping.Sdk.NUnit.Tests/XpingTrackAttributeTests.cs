/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit.Tests;

using global::NUnit.Framework;
using System;
using System.Threading.Tasks;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Tests for XpingTrackAttribute.
/// Note: Deep integration testing with actual NUnit test execution is done in sample projects.
/// These unit tests verify the attribute's contract and error handling.
/// </summary>
[Collection("XpingContext")]
public sealed class XpingTrackAttributeTests : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        return XpingContext.ShutdownAsync().AsTask();
    }

    public Task DisposeAsync()
    {
        return XpingContext.ShutdownAsync().AsTask();
    }

    [Fact]
    public void Targets_ReturnsTestLevel()
    {
        var attribute = new XpingTrackAttribute();

        var targets = ((ITestAction)attribute).Targets;

        // ActionTargets.Test has value 1
        Assert.Equal(1, (int)targets);
    }

    [Fact]
    public void BeforeTest_WithNullTest_ThrowsArgumentNullException()
    {
        var attribute = new XpingTrackAttribute();

        Assert.Throws<ArgumentNullException>(() => ((ITestAction)attribute).BeforeTest(null!));
    }

    [Fact]
    public void AfterTest_WithNullTest_DoesNotThrow()
    {
        var attribute = new XpingTrackAttribute();

        // AfterTest returns early when test is null
        var exception = Record.Exception(() => ((ITestAction)attribute).AfterTest(null!));

        Assert.Null(exception);
    }

    [Fact]
    public void AfterTest_WithoutBeforeTest_DoesNotThrow()
    {
        var attribute = new XpingTrackAttribute();

        var exception = Record.Exception(() => ((ITestAction)attribute).AfterTest(null!));

        Assert.Null(exception);
    }

    [Fact]
    public void AttributeUsage_CanBeAppliedToMethod()
    {
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        Assert.True((usage.ValidOn & AttributeTargets.Method) == AttributeTargets.Method);
    }

    [Fact]
    public void AttributeUsage_CanBeAppliedToClass()
    {
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        Assert.True((usage.ValidOn & AttributeTargets.Class) == AttributeTargets.Class);
    }

    [Fact]
    public void AttributeUsage_CanBeAppliedToAssembly()
    {
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        Assert.True((usage.ValidOn & AttributeTargets.Assembly) == AttributeTargets.Assembly);
    }

    [Fact]
    public void AttributeUsage_IsInherited()
    {
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        Assert.True(usage.Inherited);
    }

    [Fact]
    public void AttributeUsage_AllowsOnlyOneInstance()
    {
        var attributeType = typeof(XpingTrackAttribute);
        var attributes = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false);
        var usage = (AttributeUsageAttribute)attributes[0];

        Assert.False(usage.AllowMultiple);
    }
}
