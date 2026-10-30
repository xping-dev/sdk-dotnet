/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Reflection;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.Core.Exceptions;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;

namespace Xping.Sdk.XUnit.V3.Tests;

/// <summary>
/// Tests for <see cref="XpingContext"/> lifecycle management.
/// </summary>
[Collection(XpingContextCollection.Name)]
public sealed class XpingContextTests : IAsyncLifetime
{
    // A store of its own, thrown away with the fixture, so nothing recorded here lands in the
    // repository's .xping/.
    private readonly string _scratchStore = Path.Combine(
        Path.GetTempPath(), "xping-tests", Guid.NewGuid().ToString("N"));

    public ValueTask InitializeAsync() => XpingContext.ShutdownAsync();

    public async ValueTask DisposeAsync()
    {
        await XpingContext.ShutdownAsync().ConfigureAwait(true);

        if (Directory.Exists(_scratchStore))
            Directory.Delete(_scratchStore, recursive: true);
    }

    [Fact]
    public void IsInitialized_BeforeInitialize_ReturnsFalse()
    {
        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public void Initialize_SecondCall_IsIdempotent()
    {
        XpingContext.Initialize(new XpingConfiguration { Mode = XpingMode.LocalOnly, LocalStorePath = _scratchStore });
        XpingContext.Initialize();

        Assert.True(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_BeforeInitialize_ReturnsNoError()
    {
        Assert.Null(await XpingContext.FinalizeAndShutdownAsync());
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_StrictModeUploadFails_ReturnsTheNetworkError()
    {
        XpingContext.Initialize(UnreachableCloudConfiguration(strictMode: true));
        XpingContext.RecordTest(CreateTestExecution());

        XpingNetworkException? runFailure = await XpingContext.FinalizeAndShutdownAsync();

        Assert.NotNull(runFailure);
        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task FinalizeAndShutdownAsync_UploadFailsOutsideStrictMode_ReturnsNoError()
    {
        XpingContext.Initialize(UnreachableCloudConfiguration(strictMode: false));
        XpingContext.RecordTest(CreateTestExecution());

        Assert.Null(await XpingContext.FinalizeAndShutdownAsync());
        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task FinalizeAsync_BeforeTheHostIsBuilt_DoesNothing()
    {
        XpingContext.Initialize(LocalConfiguration());

        await XpingContext.FinalizeAsync();

        Assert.False(Directory.Exists(_scratchStore));
    }

    [Fact]
    public async Task FinalizeAsync_LeavesTheContextForShutdownToEnd()
    {
        XpingContext.Initialize(LocalConfiguration());
        XpingContext.RecordTest(CreateTestExecution());

        await XpingContext.FlushAsync();
        await XpingContext.FinalizeAsync();

        Assert.True(XpingContext.IsInitialized);
    }

    /// <summary>
    /// A test recorded twice is a retry. The summary counts it once, which only finalizing a session
    /// with a retry in it reaches.
    /// </summary>
    [Fact]
    public async Task FinalizeAndShutdownAsync_ASessionWithARetry_EndsWithoutAnError()
    {
        XpingContext.Initialize(LocalConfiguration());
        XpingContext.RecordTest(CreateTestExecution());
        XpingContext.RecordTest(CreateTestExecution());

        Assert.Null(await XpingContext.FinalizeAndShutdownAsync());
        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public async Task ShutdownAsync_AfterTheHostWasBuilt_EndsTheContext()
    {
        XpingContext.Initialize(LocalConfiguration());
        XpingContext.StartExecutorServices();

        await XpingContext.ShutdownAsync();

        Assert.False(XpingContext.IsInitialized);
    }

    [Fact]
    public void Initialize_WithAConfigurationWhileInitialized_KeepsTheFirst()
    {
        XpingContext.Initialize(LocalConfiguration());
        XpingContext.Initialize(UnreachableCloudConfiguration(strictMode: true));

        Assert.NotNull(XpingContext.StartExecutorServices());
    }

    /// <summary>
    /// On vstest the error message already failed the run; the exit code is left alone.
    /// </summary>
    [Fact]
    public void FailRun_OnVSTest_LeavesTheExitCode()
    {
        FieldInfo runner = typeof(XpingContext).GetField("_runner", BindingFlags.NonPublic | BindingFlags.Static)!;
        object? before = runner.GetValue(null);
        int exitCode = Environment.ExitCode;
        try
        {
            runner.SetValue(null, LocalSessionProperties.VSTestRunner);

            XpingContext.FailRun(new XpingNetworkException("unreachable"));

            Assert.Equal(exitCode, Environment.ExitCode);
        }
        finally
        {
            runner.SetValue(null, before);
        }
    }

    [Theory]
    [InlineData(LocalSessionProperties.VSTestRunner, false)]
    [InlineData(LocalSessionProperties.TestingPlatformRunner, true)]
    [InlineData(null, true)]
    public void FailsThroughExitCode_EveryRunnerButVSTest(string? runner, bool expected)
    {
        Assert.Equal(expected, XpingContext.FailsThroughExitCode(runner));
    }

    /// <summary>
    /// xUnit can build the framework again off the runner's call stack, where detection cannot tell.
    /// </summary>
    [Fact]
    public void RecordRunner_ADetectionThatCouldNotTell_KeepsTheRunnerAlreadyDetected()
    {
        FieldInfo runner = typeof(XpingContext).GetField("_runner", BindingFlags.NonPublic | BindingFlags.Static)!;
        object? before = runner.GetValue(null);
        try
        {
            XpingContext.RecordRunner(LocalSessionProperties.TestingPlatformRunner);
            XpingContext.RecordRunner(null);

            Assert.Equal(LocalSessionProperties.TestingPlatformRunner, XpingContext.DetectedRunner);
        }
        finally
        {
            runner.SetValue(null, before);
        }
    }

    private XpingConfiguration LocalConfiguration() => new() { Mode = XpingMode.LocalOnly, LocalStorePath = _scratchStore };

    // Port 9 (discard) is closed on loopback, so the upload is refused immediately.
    private XpingConfiguration UnreachableCloudConfiguration(bool strictMode) => new()
    {
        Mode = XpingMode.Cloud,
        ApiKey = "test-key",
        ProjectId = "test-project",
        ApiEndpoint = "http://127.0.0.1:9/v1",
        MaxRetries = 0,
        StrictMode = strictMode,
        LocalStorePath = _scratchStore,
    };

    private static TestExecution CreateTestExecution() => new TestExecutionBuilder()
        .WithTestName("TestMethod")
        .WithOutcome(TestOutcome.Passed)
        .WithDuration(TimeSpan.FromMilliseconds(100))
        .WithStartTime(DateTime.UtcNow.AddMilliseconds(-100))
        .WithEndTime(DateTime.UtcNow)
        .Build();
}
