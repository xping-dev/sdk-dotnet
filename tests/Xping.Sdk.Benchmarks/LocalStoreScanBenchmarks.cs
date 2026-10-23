/*
 * © 2026 Xping.io. All Rights Reserved.
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
/// The reads <c>xping report</c> makes when no assembly is named, on a store shared by many suites.
/// </summary>
/// <remarks>
/// <para>
/// Each pair is the read before #283 and the read after it, at the store level, where nearly all of
/// the cost is: deserializing sessions. The CLI's indexing on top is negligible next to it.
/// </para>
/// <para>
/// BenchmarkDotNet reports time and allocation, not what is held at once. Setup logs the retained
/// managed heap while the old lookup's sessions are held, which is the number that decided whether a
/// large solution's report ran out of memory. The new lookup holds one session at a time.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 1, iterationCount: 3)]
public class LocalStoreScanBenchmarks
{
    private const int RunsPerAssembly = 50;
    private const int TestsPerRun = 2000;

    private string _root = string.Empty;
    private JsonSessionStore _store = null!;

    /// <summary>Gets or sets how many test assemblies share the store.</summary>
    [Params(10, 25)]
    public int Assemblies { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-bench-scan", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable(LocalStorePathResolver.EnvironmentVariableName, _root);

        // No size limit: at 25 assemblies the default 100 MB would prune the store below what the
        // run limit allows, and the benchmark would measure a smaller store than it claims.
        _store = new JsonSessionStore(new LocalStoreOptions { MaxBytes = long.MaxValue }, NullLogger.Instance);

        DateTime start = DateTime.UtcNow.AddHours(-1);
        for (int run = 0; run < RunsPerAssembly; run++)
        {
            for (int assembly = 0; assembly < Assemblies; assembly++)
                _store.Write(BuildSession(AssemblyName(assembly), start.AddSeconds((run * Assemblies) + assembly)));
        }

        long bytes = Directory
            .GetFiles(_store.SessionsPath!)
            .Sum(f => new FileInfo(f).Length);

        Console.WriteLine($"// {Assemblies} assemblies x {RunsPerAssembly} runs: {bytes / (1024 * 1024)} MB on disk");
        Console.WriteLine($"// Retained by the old lookup: {Retained(() => _store.ReadRecent(1000).Sessions) / (1024 * 1024)} MB");
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

    /// <summary>
    /// Choosing the scope before #283: load the newest 200 runs, keep the first naming an assembly.
    /// </summary>
    [Benchmark(Baseline = true)]
    public string? ScopeAssembly_LoadSessions() =>
        _store.ReadRecent(200).Sessions
            .Select(SessionAssemblies.Of)
            .FirstOrDefault(a => a.Count > 0)?[0];

    /// <summary>Choosing the scope after #283: the first header naming an assembly.</summary>
    [Benchmark]
    public string? ScopeAssembly_ReadHeaders() =>
        _store.ReadHeaders().FirstOrDefault(h => h.Assemblies.Count > 0)?.Assemblies[0];

    /// <summary>The <c>--test</c> lookup before #283: one unscoped read of up to 1000 runs.</summary>
    [Benchmark]
    public int Lookup_Unscoped() =>
        _store.ReadRecent(1000).Sessions.Sum(s => s.Executions.Count);

    /// <summary>The <c>--test</c> lookup after #283: one pass over every run, one held at a time.</summary>
    [Benchmark]
    public int Lookup_OnePass() =>
        _store.ReadEach().Sum(f => f.Session?.Executions.Count ?? 0);

    private static long Retained(Func<IReadOnlyList<TestSession>> read)
    {
        long before = GC.GetTotalMemory(forceFullCollection: true);
        IReadOnlyList<TestSession> sessions = read();
        long after = GC.GetTotalMemory(forceFullCollection: true);

        GC.KeepAlive(sessions);
        return after - before;
    }

    private static string AssemblyName(int index) => $"Suite{index:D2}.Tests";

    private static TestSession BuildSession(string assembly, DateTime startedAt) =>
        new TestSessionBuilder()
            .WithSessionId(Guid.NewGuid())
            .WithStartedAt(startedAt)
            .WithEndedAt(startedAt.AddSeconds(38))
            .WithEnvironmentInfo(new EnvironmentInfoBuilder()
                .WithMachineName("dev-box")
                .WithEnvironmentName("Local")
                .Build())
            .AddExecutions(Enumerable.Range(0, TestsPerRun).Select(i => BuildExecution(assembly, i)).ToList())
            .WithSessionState(TestSessionState.Finalized)
            .Build();

    private static TestExecution BuildExecution(string assembly, int i) =>
        new TestExecutionBuilder()
            .WithIdentity(new TestIdentityBuilder()
                .WithTestFingerprint(PseudoRandomHex(assembly, i))
                .WithAssembly(assembly)
                .WithFullyQualifiedName($"{assembly}.Class.Test_{i}")
                .WithDisplayName($"Test_{i}")
                .Build())
            .WithTestName($"{assembly}.Class.Test_{i}")
            .WithOutcome(i % 20 == 0 ? TestOutcome.Failed : TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(10 + (i % 500)))
            .Build();

    // Hash-like, so compression behaves as it does for real SHA-256 fingerprints.
    private static string PseudoRandomHex(string assembly, int seed) =>
        Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{assembly}:{seed}")));
}
