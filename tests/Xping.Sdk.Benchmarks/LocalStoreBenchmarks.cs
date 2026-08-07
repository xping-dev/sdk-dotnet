/*
 * © 2026 Xping.io. All Rights Reserved.
 * This file is part of the Xping SDK.
 *
 * License: [MIT]
 */

using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Xping.Sdk.Core.Models.Builders;
using Xping.Sdk.Core.Models.Executions;
using Xping.Sdk.Core.Models.Local;
using Xping.Sdk.Core.Services.LocalStore;
using Xping.Sdk.Core.Services.LocalStore.Internals;

namespace Xping.Sdk.Benchmarks;

/// <summary>
/// Benchmarks for the local run store.
/// </summary>
/// <remarks>
/// <para>
/// The store's cost is paid once per test run, during finalization, after the last test has
/// finished. Nothing here runs on the per-test hot path — executions are projected onto slim records
/// at drain time, not in <c>RecordTestExecution</c>.
/// </para>
/// <para>
/// Performance target: the whole write path stays under 15 ms for a 2,000-test suite, so the store
/// is never a visible part of a developer's test run.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class LocalStoreBenchmarks
{
    private string _root = string.Empty;
    private JsonLinesRunStore _store = null!;
    private List<TestExecution> _executions2000 = null!;
    private LocalRun _run2000 = null!;
    private LocalRun _run200 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "xping-bench", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable(LocalStorePathResolver.EnvironmentVariableName, _root);

        _store = new JsonLinesRunStore(new LocalStoreOptions(), NullLogger.Instance);

        _executions2000 = Enumerable.Range(0, 2000).Select(BuildExecution).ToList();

        _run2000 = BuildRun(2000);
        _run200 = BuildRun(200);

        // Populate history so the read benchmark measures a realistic full window.
        for (int i = 0; i < 12; i++)
            _store.Write(BuildRun(2000, DateTime.UtcNow.AddMinutes(-i)));
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
            .WithTestName($"Namespace.Class.Test_{i}")
            .WithOutcome(i % 20 == 0 ? TestOutcome.Failed : TestOutcome.Passed)
            .WithDuration(TimeSpan.FromMilliseconds(10 + (i % 500)))
            .Build();

    private static LocalRun BuildRun(int count, DateTime? startedAt = null)
    {
        var records = Enumerable.Range(0, count).Select(i => new LocalTestRecord
        {
            Fingerprint = new string((char)('a' + (i % 16)), 64),
            Name = $"Namespace.Class.Test_{i}",
            Outcome = i % 20 == 0 ? OutcomeCodes.Failed : OutcomeCodes.Passed,
            DurationMs = 10 + (i % 500),
            Attempt = 1
        }).ToList();

        var header = new LocalRunHeader
        {
            SessionId = Guid.NewGuid().ToString("N"),
            StartedAtUtc = startedAt ?? DateTime.UtcNow,
            DurationMs = 38000,
            Environment = "Local"
        };

        return new LocalRun(header, records);
    }

    /// <summary>
    /// Projection of drained executions onto slim records: the only new work that touches
    /// per-execution data, performed once per flush.
    /// </summary>
    [Benchmark]
    public int ProjectExecutions_2000()
    {
        var records = new List<LocalTestRecord>(_executions2000.Count);

        foreach (TestExecution execution in _executions2000)
            records.Add(LocalTestRecord.FromExecution(execution));

        return records.Count;
    }

    /// <summary>Full write path for a 2,000-test run: serialize, gzip, write, rotate.</summary>
    [Benchmark]
    public bool WriteRun_2000Tests() => _store.Write(_run2000);

    /// <summary>Full write path for a more typical 200-test suite.</summary>
    [Benchmark]
    public bool WriteRun_200Tests() => _store.Write(_run200);

    /// <summary>Reading a full 12-run analysis window of 2,000-test runs.</summary>
    [Benchmark]
    public int ReadRecent_12Runs() => _store.ReadRecent(12).Count;

    /// <summary>Analysis over a full window, which is what the report renders.</summary>
    [Benchmark]
    public int Analyze_12Runs()
    {
        var runs = _store.ReadRecent(12);
        return LocalFlakinessAnalyzer.Analyze(runs).UnstableTests.Count;
    }
}
