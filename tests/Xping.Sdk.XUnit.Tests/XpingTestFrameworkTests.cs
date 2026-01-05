/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit.Tests;

using System;
using System.Reflection;
using global::Xunit.Abstractions;

#pragma warning disable xUnit3000 // Test classes must derive from LongLivedMarshalByRefObject

/// <summary>
/// Tests for XpingTestFramework.
/// Note: Deep integration testing with actual xUnit execution is done in sample projects.
/// These unit tests verify the framework's contract and basic behavior.
/// </summary>
#pragma warning disable CA1515 // Test classes must be public for NUnit
public sealed class XpingTestFrameworkTests
#pragma warning restore CA1515
{
    [SetUp]
    public void Setup()
    {
        XpingContext.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        XpingContext.Reset();
    }

    [Test]
    public void Constructor_InitializesContext()
    {
        // Arrange
        var messageSink = new MockMessageSink();

        // Act
        using var framework = new XpingTestFramework(messageSink);

        // Assert
        Assert.That(XpingContext.IsInitialized, Is.True);
    }

    [Test]
    public void Dispose_CleansUpContext()
    {
        // Arrange
        var messageSink = new MockMessageSink();
        using var framework = new XpingTestFramework(messageSink);
        Assert.That(XpingContext.IsInitialized, Is.True);

        // Act
        framework.Dispose();

        // Assert
        Assert.That(XpingContext.IsInitialized, Is.False);
    }

    [Test]
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
        Assert.That(executor, Is.Not.Null);
        Assert.That(executor, Is.InstanceOf<XpingTestFrameworkExecutor>());

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

