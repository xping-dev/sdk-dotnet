/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.NUnit.Tests;

using System;
using System.Threading.Tasks;
using Xping.Sdk.Core.Models;
using Xunit;

/// <summary>
/// Unit tests for <see cref="XpingContext"/>.
/// </summary>
public sealed class XpingContextTests : IDisposable
{
    public XpingContextTests()
    {
        // Ensure clean state before each test
        XpingContext.Reset();
    }

    public void Dispose()
    {
        // Clean up after each test
        XpingContext.Reset();
    }

    [Fact]
    public void Initialize_FirstCall_ReturnsCollector()
    {
        var collector = XpingContext.Initialize();

        Assert.NotNull(collector);
        Assert.True(XpingContext.IsInitialized);
    }

    [Fact]
    public void Initialize_SecondCall_ReturnsSameCollector()
    {
        var collector1 = XpingContext.Initialize();
        var collector2 = XpingContext.Initialize();

        Assert.Same(collector1, collector2);
    }

    [Fact]
    public void IsInitialized_BeforeInit_ReturnsFalse()
    {
        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public void IsInitialized_AfterInit_ReturnsTrue()
    {
        XpingContext.Initialize();

        Assert.True(XpingContext.IsInitialized);
    }

    [Fact]
    public void RecordTest_BeforeInit_DoesNotThrow()
    {
        var execution = CreateTestExecution();

        // Should not throw, just silently ignore
        var exception = Record.Exception(() => XpingContext.RecordTest(execution));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordTest_AfterInit_RecordsSuccessfully()
    {
        XpingContext.Initialize();
        var execution = CreateTestExecution();

        var exception = Record.Exception(() => XpingContext.RecordTest(execution));

        Assert.Null(exception);
    }

    [Fact]
    public async Task FlushAsync_BeforeInit_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(async () =>
            await XpingContext.FlushAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task FlushAsync_AfterInit_CompletesSuccessfully()
    {
        XpingContext.Initialize();

        var exception = await Record.ExceptionAsync(async () =>
            await XpingContext.FlushAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_BeforeInit_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(async () =>
            await XpingContext.DisposeAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_AfterInit_ResetsContext()
    {
        XpingContext.Initialize();
        Assert.True(XpingContext.IsInitialized);

        await XpingContext.DisposeAsync();

        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task DisposeAsync_MultipleCalls_DoesNotThrow()
    {
        XpingContext.Initialize();

        await XpingContext.DisposeAsync();
        var exception = await Record.ExceptionAsync(async () =>
            await XpingContext.DisposeAsync());

        Assert.Null(exception);
    }

    [Fact]
    public void Reset_ClearsInitializedState()
    {
        XpingContext.Initialize();
        Assert.True(XpingContext.IsInitialized);

        XpingContext.Reset();

        Assert.False(XpingContext.IsInitialized);
    }

    private static TestExecution CreateTestExecution()
    {
        return new TestExecution
        {
            ExecutionId = Guid.NewGuid(),
            TestId = "test-1",
            TestName = "TestMethod",
            FullyQualifiedName = "Namespace.Class.TestMethod",
            Outcome = TestOutcome.Passed,
            Duration = TimeSpan.FromMilliseconds(100),
            StartTimeUtc = DateTime.UtcNow,
            EndTimeUtc = DateTime.UtcNow,
        };
    }
}
