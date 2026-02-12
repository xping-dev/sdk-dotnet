/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Environments;
using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.MSTest.Tests;

using System;
using System.Threading.Tasks;
using Xping.Sdk.Core.Models;
using Xunit;

/// <summary>
/// Tests for XpingContext lifecycle management.
/// </summary>
public sealed class XpingContextTests : IDisposable
{
    public XpingContextTests()
    {
        // Reset context before each test
        XpingContext.Reset();
    }

    public void Dispose()
    {
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
    public void Initialize_SecondCall_ReturnsSameInstance()
    {
        var collector1 = XpingContext.Initialize();
        var collector2 = XpingContext.Initialize();

        Assert.Same(collector1, collector2);
    }

    [Fact]
    public void IsInitialized_BeforeInitialize_ReturnsFalse()
    {
        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public void IsInitialized_AfterInitialize_ReturnsTrue()
    {
        XpingContext.Initialize();

        Assert.True(XpingContext.IsInitialized);
    }

    [Fact]
    public void RecordTest_BeforeInitialize_DoesNotThrow()
    {
        var execution = CreateTestExecution();

        var exception = Record.Exception(() => XpingContext.RecordTest(execution));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordTest_AfterInitialize_DoesNotThrow()
    {
        XpingContext.Initialize();
        var execution = CreateTestExecution();

        var exception = Record.Exception(() => XpingContext.RecordTest(execution));

        Assert.Null(exception);
    }

    [Fact]
    public async Task FlushAsync_BeforeInitialize_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(async () => await XpingContext.FlushAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task FlushAsync_AfterInitialize_DoesNotThrow()
    {
        XpingContext.Initialize();

        var exception = await Record.ExceptionAsync(async () => await XpingContext.FlushAsync());

        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeAsync_ResetsContext()
    {
        XpingContext.Initialize();
        Assert.True(XpingContext.IsInitialized);

        await XpingContext.DisposeAsync();

        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task DisposeAsync_MultipleCallsSafe()
    {
        XpingContext.Initialize();

        await XpingContext.DisposeAsync();
        var exception = await Record.ExceptionAsync(async () => await XpingContext.DisposeAsync());

        Assert.Null(exception);
    }

    [Fact]
    public void Reset_ClearsInitialized()
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
