/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit.Tests;

using System;
using System.Threading.Tasks;
using Xping.Sdk.Core.Models;

/// <summary>
/// Tests for XpingContext lifecycle management.
/// </summary>
#pragma warning disable CA1515 // Test classes must be public for NUnit
public sealed class XpingContextTests
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
    public void Initialize_FirstCall_ReturnsCollector()
    {
        // Act
        var collector = XpingContext.Initialize();

        // Assert
        Assert.That(collector, Is.Not.Null);
        Assert.That(XpingContext.IsInitialized, Is.True);
    }

    [Test]
    public void Initialize_SecondCall_ReturnsSameInstance()
    {
        // Arrange
        var first = XpingContext.Initialize();

        // Act
        var second = XpingContext.Initialize();

        // Assert
        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void IsInitialized_BeforeInitialize_ReturnsFalse()
    {
        // Assert
        Assert.That(XpingContext.IsInitialized, Is.False);
    }

    [Test]
    public void IsInitialized_AfterInitialize_ReturnsTrue()
    {
        // Act
        XpingContext.Initialize();

        // Assert
        Assert.That(XpingContext.IsInitialized, Is.True);
    }

    [Test]
    public void RecordTest_BeforeInitialize_DoesNotThrow()
    {
        // Arrange
        var execution = CreateTestExecution();

        // Act & Assert
        Assert.DoesNotThrow(() => XpingContext.RecordTest(execution));
    }

    [Test]
    public void RecordTest_AfterInitialize_DoesNotThrow()
    {
        // Arrange
        XpingContext.Initialize();
        var execution = CreateTestExecution();

        // Act & Assert
        Assert.DoesNotThrow(() => XpingContext.RecordTest(execution));
    }

    [Test]
    public void FlushAsync_BeforeInitialize_DoesNotThrow()
    {
        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await XpingContext.FlushAsync());
    }

    [Test]
    public void FlushAsync_AfterInitialize_DoesNotThrow()
    {
        // Arrange
        XpingContext.Initialize();

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await XpingContext.FlushAsync());
    }

    [Test]
    public void DisposeAsync_BeforeInitialize_DoesNotThrow()
    {
        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await XpingContext.DisposeAsync());
    }

    [Test]
    public async Task DisposeAsync_AfterInitialize_ResetsContext()
    {
        // Arrange
        XpingContext.Initialize();

        // Act
        await XpingContext.DisposeAsync();

        // Assert
        Assert.That(XpingContext.IsInitialized, Is.False);
    }

    [Test]
    public async Task DisposeAsync_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        XpingContext.Initialize();
        await XpingContext.DisposeAsync();

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await XpingContext.DisposeAsync());
    }

    [Test]
    public void Reset_ClearsInitializedState()
    {
        // Arrange
        XpingContext.Initialize();
        Assert.That(XpingContext.IsInitialized, Is.True);

        // Act
        XpingContext.Reset();

        // Assert
        Assert.That(XpingContext.IsInitialized, Is.False);
    }

    private static TestExecution CreateTestExecution()
    {
        return new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            Identity = new TestIdentity
            {
                TestId = "test-1",
                FullyQualifiedName = "Namespace.Class.TestMethod",
                Assembly = "TestAssembly",
                Namespace = "Namespace"
            },
            TestName = "TestMethod",
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(100),
            StartTimeUtc = DateTime.UtcNow.AddMilliseconds(-100),
            EndTimeUtc = DateTime.UtcNow,
            SessionContext = new TestSession
            {
                SessionId = Guid.NewGuid().ToString(),
                StartedAt = DateTime.UtcNow,
                EnvironmentInfo = new EnvironmentInfo()
            },
            Metadata = new TestMetadata()
        };
    }
}
