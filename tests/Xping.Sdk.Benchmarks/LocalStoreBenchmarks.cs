/*
 * © 2026 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Sdk.Core.Models;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Sdk.Benchmarks;

/// <summary>
/// Benchmarks for the local session store.
/// </summary>
/// <remarks>
/// <para>
/// The store's cost is paid once per test run, during finalization, after the last test has
/// finished. Nothing here runs on the per-test hot path — executions are retained at drain time, not
/// in <c>RecordTestExecution</c>.
/// </para>
/// <para>
/// Performance target: the whole write path stays fast enough that the store is never a visible part
/// of a developer's test run.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class LocalStoreBenchmarks
{
    private string _root = string.Empty;
    private JsonSessionStore _store = null!;
    private List<TestExecution> _executions2000 = null!;
    private TestSession _session2000 = null!;
    private TestSession _session200 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-bench", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable(LocalStorePathResolver.EnvironmentVariableName, _root);

        _store = new JsonSessionStore(new LocalStoreOptions(), NullLogger.Instance);

        _executions2000 = Enumerable.Range(0, 2000).Select(BuildExecution).ToList();

        _session2000 = BuildSession(2000);
        _session200 = BuildSession(200);

        // Populate history so the read benchmark measures a realistic full window.
        for (int i = 0; i < 12; i++)
            _store.Write(BuildSession(2000, DateTime.UtcNow.AddMinutes(-i)));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable(LocalStorePathResolver.EnvironmentVariableName, null);

        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static TestExecution BuildExecution(int i) =>
        new TestExecutionBuilder()
            .WithIdentity(new TestIdentityBuilder()

                // A repeated character compresses to almost nothing, which would make the write
                // benchmark measure an unrealistically small payload. Real fingerprints are SHA256.
                .WithTestFingerprint(PseudoRandomHex(i))
                .WithAssembly("Namespace.Tests")
                .WithFullyQualifiedName($"Namespace.Class.Test_{i}")
                .WithDisplayName($"Test_{i}")
                .Build())
            .WithTestName($"Namespace.Class.Test_{i}")
            .WithOutcome(i % 20 == 0 ? TestOutcome.Failed : TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(10 + (i % 500)))
            .Build();

    private static TestSession BuildSession(int count, DateTime? startedAt = null)
    {
        DateTime start = startedAt ?? DateTime.UtcNow;

        return new TestSessionBuilder()
            .WithSessionId(Guid.NewGuid())
            .WithStartedAt(start)
            .WithEndedAt(start.AddSeconds(38))
            .WithEnvironmentInfo(new EnvironmentInfoBuilder()
                .WithMachineName("dev-box")
                .WithEnvironmentName("Local")
                .Build())
            .AddExecutions(Enumerable.Range(0, count).Select(BuildExecution).ToList())
            .WithSessionState(TestSessionState.Finalized)
            .Build();
    }

    /// <summary>
    /// Produces a deterministic 64-character hex string with hash-like entropy, so compression
    /// behaves as it does for real fingerprints.
    /// </summary>
    private static string PseudoRandomHex(int seed)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(BitConverter.GetBytes(seed));

        var sb = new System.Text.StringBuilder(64);
        foreach (byte b in hash)
            sb.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));

        return sb.ToString();
    }

    /// <summary>
    /// Assembling the session from drained executions: the only work that touches per-execution
    /// data, performed once per run.
    /// </summary>
    [Benchmark]
    public int BuildSession_2000()
    {
        TestSession session = new TestSessionBuilder()
            .WithSessionId(Guid.NewGuid())
            .WithStartedAt(DateTime.UtcNow)
            .AddExecutions(_executions2000)
            .WithSessionState(TestSessionState.Finalized)
            .Build();

        return session.Executions.Count;
    }

    /// <summary>Full write path for a 2,000-test run: serialize, gzip, write, rotate.</summary>
    [Benchmark]
    public bool WriteRun_2000Tests() => _store.Write(_session2000);

    /// <summary>Full write path for a more typical 200-test suite.</summary>
    [Benchmark]
    public bool WriteRun_200Tests() => _store.Write(_session200);

    /// <summary>Reading a full 12-run analysis window of 2,000-test runs.</summary>
    [Benchmark]
    public int ReadRecent_12Runs() => _store.ReadRecent(12).Sessions.Count;
}
