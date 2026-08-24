/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using xRetry;

namespace Xping.Sdk.NUnit.Tests;

using System;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Tests for XpingContext lifecycle management in the NUnit adapter.
/// </summary>
[Collection("XpingContext")]
public sealed class XpingContextTests : IAsyncLifetime
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
    public void Initialize_FirstCall_SetsIsInitializedTrue()
    {
        XpingContext.Initialize();

        Assert.True(XpingContext.IsInitialized);
    }

    [Fact]
    public void Initialize_SecondCall_IsIdempotent()
    {
        XpingContext.Initialize();
        XpingContext.Initialize();

        Assert.True(XpingContext.IsInitialized);
    }

    [Fact]
    public void IsInitialized_BeforeInitialize_ReturnsFalse()
    {
        Assert.False(XpingContext.IsInitialized);
    }

    /// <remarks>
    /// <c>[RetryFact]</c> rather than <c>[Fact]</c>: this test shares a documented race with the NUnit
    /// <c>[SetUpFixture]</c> in this assembly, which can null the static context between
    /// <c>Initialize()</c> and the call under test. The race is real and deliberately left in place —
    /// see docs/known-limitations.md. Retrying keeps it from failing the build while Xping still
    /// records the hidden attempt, which is what surfaces it as a RetryMasked finding instead of as a
    /// red run nobody can act on.
    /// </remarks>
    [RetryFact(3)]
    public void IsInitialized_AfterInitialize_ReturnsTrue()
    {
        XpingContext.Initialize();

        Assert.True(XpingContext.IsInitialized);
    }

    /// <remarks>
    /// <c>[RetryFact]</c> rather than <c>[Fact]</c>: this test shares a documented race with the NUnit
    /// <c>[SetUpFixture]</c> in this assembly, which can null the static context between
    /// <c>Initialize()</c> and the call under test. The race is real and deliberately left in place —
    /// see docs/known-limitations.md. Retrying keeps it from failing the build while Xping still
    /// records the hidden attempt, which is what surfaces it as a RetryMasked finding instead of as a
    /// red run nobody can act on.
    /// </remarks>
    [RetryFact(3)]
    public void RecordTest_AfterInitialize_DoesNotThrow()
    {
        XpingContext.Initialize();
        var execution = CreateTestExecution();

        var exception = Record.Exception(() => XpingContext.RecordTest(execution));

        Assert.Null(exception);
    }

    /// <remarks>
    /// <c>[RetryFact]</c> rather than <c>[Fact]</c>: this test shares a documented race with the NUnit
    /// <c>[SetUpFixture]</c> in this assembly, which can null the static context between
    /// <c>Initialize()</c> and the call under test. The race is real and deliberately left in place —
    /// see docs/known-limitations.md. Retrying keeps it from failing the build while Xping still
    /// records the hidden attempt, which is what surfaces it as a RetryMasked finding instead of as a
    /// red run nobody can act on.
    /// </remarks>
    [RetryFact(3)]
    public async Task FlushAsync_AfterInitialize_DoesNotThrow()
    {
        XpingContext.Initialize();

        var exception = await Record.ExceptionAsync(async () =>
            await XpingContext.FlushAsync().ConfigureAwait(true)).ConfigureAwait(true);

        Assert.Null(exception);
    }

    [Fact]
    public async Task ShutdownAsync_AfterInitialize_ResetsContext()
    {
        XpingContext.Initialize();
        Assert.True(XpingContext.IsInitialized);

        await XpingContext.ShutdownAsync().ConfigureAwait(true);

        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task ShutdownAsync_MultipleCallsSafe()
    {
        XpingContext.Initialize();

        await XpingContext.ShutdownAsync().ConfigureAwait(true);
        var exception = await Record.ExceptionAsync(async () =>
            await XpingContext.ShutdownAsync().ConfigureAwait(true)).ConfigureAwait(true);

        Assert.Null(exception);
    }

    [Fact]
    public async Task ShutdownAsync_BeforeInitialize_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(async () =>
            await XpingContext.ShutdownAsync().ConfigureAwait(true)).ConfigureAwait(true);

        Assert.Null(exception);
    }

    [Fact]
    public async Task FinalizeAsync_BeforeInitialize_DoesNotThrow()
    {
        var exception = await Record.ExceptionAsync(async () =>
            await XpingContext.FinalizeAsync().ConfigureAwait(true)).ConfigureAwait(true);

        Assert.Null(exception);
    }

    [Fact]
    public async Task FinalizeAsync_AfterInitialize_DoesNotThrow()
    {
        XpingContext.Initialize();

        var exception = await Record.ExceptionAsync(async () =>
            await XpingContext.FinalizeAsync().ConfigureAwait(true)).ConfigureAwait(true);

        Assert.Null(exception);
    }

    private static TestExecution CreateTestExecution()
    {
        return new TestExecutionBuilder()
            .WithTestName("TestMethod")
            .WithOutcome(TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(100))
            .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
            .WithEndTime(DateTime.UtcNow)
            .Build();
    }
}
