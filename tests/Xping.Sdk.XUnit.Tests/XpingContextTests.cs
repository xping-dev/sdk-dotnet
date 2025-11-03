/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.XUnit.Tests;

using System;
using System.Threading.Tasks;
using Xping.Sdk.Core.Models;
using Xunit;
using Assert = Xunit.Assert;

/// <summary>
/// Tests for XpingContext lifecycle management.
/// </summary>
public sealed class XpingContextTests : IDisposable
{
    public XpingContextTests()
    {
        XpingContext.Reset();
    }

    public void Dispose()
    {
        XpingContext.Reset();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Initialize_FirstCall_ReturnsCollector()
    {
        // Act
        var collector = XpingContext.Initialize();

        // Assert
        Assert.NotNull(collector);
        Assert.True(XpingContext.IsInitialized);
    }

    [Fact]
    public void Initialize_SecondCall_ReturnsSameInstance()
    {
        // Arrange
        var first = XpingContext.Initialize();

        // Act
        var second = XpingContext.Initialize();

        // Assert
        Assert.Same(first, second);
    }

    [Fact]
    public void IsInitialized_BeforeInitialize_ReturnsFalse()
    {
        // Assert
        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public void IsInitialized_AfterInitialize_ReturnsTrue()
    {
        // Act
        XpingContext.Initialize();

        // Assert
        Assert.True(XpingContext.IsInitialized);
    }

    [Fact]
    public void RecordTest_BeforeInitialize_DoesNotThrow()
    {
        // Arrange
        var execution = CreateTestExecution();

        // Act & Assert
        var exception = Record.Exception(() => XpingContext.RecordTest(execution));
        Assert.Null(exception);
    }

    [Fact]
    public void RecordTest_AfterInitialize_DoesNotThrow()
    {
        // Arrange
        XpingContext.Initialize();
        var execution = CreateTestExecution();

        // Act & Assert
        var exception = Record.Exception(() => XpingContext.RecordTest(execution));
        Assert.Null(exception);
    }

    [Fact]
    public async Task FlushAsync_BeforeInitialize_DoesNotThrow()
    {
        // Act & Assert
        var exception = await Record.ExceptionAsync(async () => await XpingContext.FlushAsync());
        Assert.Null(exception);
    }

    [Fact]
    public async Task FlushAsync_AfterInitialize_DoesNotThrow()
    {
        // Arrange
        XpingContext.Initialize();

        // Act & Assert
        var exception = await Record.ExceptionAsync(async () => await XpingContext.FlushAsync());
        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_BeforeInitialize_DoesNotThrow()
    {
        // Act & Assert
        var exception = await Record.ExceptionAsync(async () => await XpingContext.DisposeAsync());
        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_AfterInitialize_ResetsContext()
    {
        // Arrange
        XpingContext.Initialize();

        // Act
        await XpingContext.DisposeAsync();

        // Assert
        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task DisposeAsync_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        XpingContext.Initialize();
        await XpingContext.DisposeAsync();

        // Act & Assert
        var exception = await Record.ExceptionAsync(async () => await XpingContext.DisposeAsync());
        Assert.Null(exception);
    }

    [Fact]
    public void Reset_ClearsInitializedState()
    {
        // Arrange
        XpingContext.Initialize();
        Assert.True(XpingContext.IsInitialized);

        // Act
        XpingContext.Reset();

        // Assert
        Assert.False(XpingContext.IsInitialized);
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
        };
    }
}
