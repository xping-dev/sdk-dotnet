/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;
using Xunit.Abstractions;

namespace Xping.Sdk.Core.Tests.LocalStore;

/// <summary>
/// Guards the local store's performance budget.
/// </summary>
/// <remarks>
/// <para>
/// The store's whole justification is that it is invisible: no per-test cost, and a once-per-run
/// write small enough that nobody notices it. These assertions carry generous headroom over the
/// measured figures so they catch an order-of-magnitude regression without becoming flaky on a
/// loaded CI agent. The precise numbers are logged rather than asserted.
/// </para>
/// <para>
/// This exists because BenchmarkDotNet 0.14.0 cannot resolve a net10.0 toolchain, which makes the
/// benchmark project unrunnable on this repository's current target framework.
/// </para>
/// </remarks>
[Collection("Sequential")]
public sealed class LocalStorePerformanceTests : IDisposable
{
    private const int SuiteSize = 2000;

    private readonly string _root;
    private readonly ITestOutputHelper _output;

    public LocalStorePerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _root = Path.Combine(Path.GetTempPath(), "xping-perf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        System.Environment.SetEnvironmentVariable(
            LocalStorePathResolver.EnvironmentVariableName, _root);
    }

    public void Dispose()
    {
        System.Environment.SetEnvironmentVariable(
            LocalStorePathResolver.EnvironmentVariableName, null);

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static TestSession BuildSession(int count, DateTime startedAt)
    {
        var executions = Enumerable.Range(0, count).Select(i => new TestExecutionBuilder()
            .WithExecutionId(Guid.NewGuid())
            .WithIdentity(new TestIdentityBuilder()

                // Real fingerprints are SHA256 hashes. Using a repeated character here would
                // compress to almost nothing and make the on-disk figures meaningless, so generate
                // high-entropy hex that behaves like the real thing under gzip.
                .WithTestFingerprint(PseudoRandomHex(i))
                .WithAssembly("MyCompany.Product.Tests")
                .WithFullyQualifiedName($"MyCompany.Product.Tests.SomeFixture.Test_Method_Name_{i}")
                .WithDisplayName($"Test_Method_Name_{i}")
                .Build())
            .WithTestName($"MyCompany.Product.Tests.SomeFixture.Test_Method_Name_{i}")
            .WithOutcome(i % 20 == 0 ? TestOutcome.Failed : TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(10 + (i % 500)))
            .Build())
            .ToList();

        return new TestSessionBuilder()
            .WithSessionId(Guid.NewGuid())
            .WithStartedAt(startedAt)
            .WithEndedAt(startedAt.AddSeconds(38))
            .WithEnvironmentInfo(new EnvironmentInfoBuilder()
                .WithMachineName("dev-box")
                .WithEnvironmentName("Local")
                .Build())
            .AddExecutions(executions)
            .WithSessionState(TestSessionState.Finalized)
            .Build();
    }

    [Fact]
    public void WritingATwoThousandTestRunStaysWellInsideBudget()
    {
        // Arrange
        var store = new JsonSessionStore(new LocalStoreOptions(), NullLogger.Instance);
        var session = BuildSession(SuiteSize, DateTime.UtcNow);

        store.Write(BuildSession(SuiteSize, DateTime.UtcNow.AddMinutes(-1))); // warm the path

        // Act
        var sw = Stopwatch.StartNew();
        store.Write(session);
        sw.Stop();

        // Assert
        _output.WriteLine(FormatMs("Write 2,000-test run", sw.Elapsed.TotalMilliseconds));
        Assert.True(
            sw.ElapsedMilliseconds < 1000,
            $"Write took {sw.ElapsedMilliseconds}ms for {SuiteSize} tests.");
    }

    [Fact]
    public void BuildingASessionIsCheapEnoughToRunAtFinalization()
    {
        // Arrange
        var executions = Enumerable.Range(0, SuiteSize)
            .Select(i => new TestExecutionBuilder()
                .WithTestName($"Namespace.Class.Test_{i}")
                .WithOutcome(TestOutcome.Passed)
                .WithDuration(TimeSpan.FromMilliseconds(10))
                .Build())
            .ToList();

        // Act
        var sw = Stopwatch.StartNew();
        TestSession session = new TestSessionBuilder()
            .WithSessionId(Guid.NewGuid())
            .WithStartedAt(DateTime.UtcNow)
            .AddExecutions(executions)
            .WithSessionState(TestSessionState.Finalized)
            .Build();
        sw.Stop();

        // Assert
        _output.WriteLine(FormatMs("Assemble a 2,000-test session", sw.Elapsed.TotalMilliseconds));
        Assert.Equal(SuiteSize, session.Executions.Count);
        Assert.True(
            sw.ElapsedMilliseconds < 100,
            $"Assembly took {sw.ElapsedMilliseconds}ms for {SuiteSize} executions.");
    }

    [Fact]
    public void ReadingAFullWindowIsFastEnoughForTheCli()
    {
        // Arrange — a full analysis window of large runs, the worst realistic read.
        // This cost is paid by `xping report`, not by a test run: the SDK only writes.
        var store = new JsonSessionStore(new LocalStoreOptions(), NullLogger.Instance);
        for (int i = 0; i < 12; i++)
            store.Write(BuildSession(SuiteSize, DateTime.UtcNow.AddMinutes(-i)));

        // Act
        var sw = Stopwatch.StartNew();
        var sessions = store.ReadRecent(12).Sessions;
        sw.Stop();

        // Assert
        _output.WriteLine(FormatMs("Read 12 x 2,000-test runs", sw.Elapsed.TotalMilliseconds));
        Assert.Equal(12, sessions.Count);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Read took {sw.ElapsedMilliseconds}ms.");
    }

    [Fact]
    public void StoreSizeStaysReasonableForALargeSuite()
    {
        // Arrange
        var store = new JsonSessionStore(new LocalStoreOptions(), NullLogger.Instance);

        // Act
        for (int i = 0; i < 12; i++)
            store.Write(BuildSession(SuiteSize, DateTime.UtcNow.AddMinutes(-i)));

        long total = new DirectoryInfo(LocalStorePathResolver.GetSessionsDirectory(_root))
            .GetFiles()
            .Sum(f => f.Length);

        // Assert — disk bloat is the failure mode developers would actually notice and resent.
        _output.WriteLine(string.Format(
            CultureInfo.InvariantCulture,
            "12 x 2,000-test runs on disk: {0:0.0} KB ({1:0} bytes per execution)",
            total / 1024.0,
            total / (double)(12 * SuiteSize)));

        Assert.True(total < 12L * 1024 * 1024, $"Store grew to {total} bytes.");
    }

    private static string FormatMs(string label, double ms) =>
        string.Format(CultureInfo.InvariantCulture, "{0}: {1:0.00} ms", label, ms);

    /// <summary>
    /// Produces a deterministic 64-character hex string with hash-like entropy, so compression
    /// measurements reflect what real fingerprints cost on disk.
    /// </summary>
    private static string PseudoRandomHex(int seed)
    {
        // A real SHA256 of the seed: hash-like entropy without an insecure RNG.
        byte[] hash = System.Security.Cryptography.SHA256.HashData(BitConverter.GetBytes(seed));

        var sb = new System.Text.StringBuilder(64);
        foreach (byte b in hash)
            sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));

        return sb.ToString();
    }
}
