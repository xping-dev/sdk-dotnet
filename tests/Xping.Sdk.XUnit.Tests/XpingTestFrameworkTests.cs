/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit.Tests;

using System;
using System.Reflection;
using global::Xunit.Abstractions;
using Xunit;
using Assert = Xunit.Assert;

#pragma warning disable xUnit3000 // Test classes must derive from LongLivedMarshalByRefObject

/// <summary>
/// Tests for XpingTestFramework.
/// Note: Deep integration testing with actual xUnit execution is done in sample projects.
/// These unit tests verify the framework's contract and basic behavior.
/// </summary>
public sealed class XpingTestFrameworkTests : IDisposable
{
    public XpingTestFrameworkTests()
    {
        XpingContext.Reset();
    }

    public void Dispose()
    {
        XpingContext.Reset();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_InitializesContext()
    {
        // Arrange
        var messageSink = new MockMessageSink();

        // Act
        using var framework = new XpingTestFramework(messageSink);

        // Assert
        Assert.True(XpingContext.IsInitialized);
    }

    [Fact]
    public void Dispose_CleansUpContext()
    {
        // Arrange
        var messageSink = new MockMessageSink();
        using var framework = new XpingTestFramework(messageSink);
        Assert.True(XpingContext.IsInitialized);

        // Act
        framework.Dispose();

        // Assert
        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public void CreateExecutor_ReturnsXpingTestFrameworkExecutor()
    {
        // Arrange
        var messageSink = new MockMessageSink();
        using var framework = new XpingTestFramework(messageSink);
        var assemblyName = Assembly.GetExecutingAssembly().GetName();

        // Act - Using reflection to call protected method
        var method = typeof(XpingTestFramework).GetMethod(
            "CreateExecutor",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var executor = method!.Invoke(framework, new object[] { assemblyName });

        // Assert
        Assert.NotNull(executor);
        Assert.IsType<XpingTestFrameworkExecutor>(executor);

        // Cleanup executor
        if (executor is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private sealed class MockMessageSink : IMessageSink
    {
        public bool OnMessage(IMessageSinkMessage message) => true;
    }
}

#pragma warning restore xUnit3000

