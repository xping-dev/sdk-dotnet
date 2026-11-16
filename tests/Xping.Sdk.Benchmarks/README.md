# Xping SDK Benchmarks

This project contains performance benchmarks for the Xping SDK using BenchmarkDotNet.

## Purpose

The benchmarks measure critical performance characteristics of the SDK including:
- Test execution tracking overhead
- Memory allocation and footprint
- Collection throughput and concurrency
- Upload and batching performance
- Framework adapter overhead

## Running Benchmarks

### Run All Benchmarks

```bash
cd tests/Xping.Sdk.Benchmarks
dotnet run -c Release
```

### Run Specific Benchmark

```bash
# Run specific benchmark class
dotnet run -c Release --filter *CollectorBenchmarks*

# Run specific method
dotnet run -c Release --filter *CollectorBenchmarks.RecordSingleTest*
```

### Run with Memory Profiler

```bash
dotnet run -c Release --profiler EP
```

### Isolation

Benchmarks never upload and never touch your local history. `Program.Main` clears every `XPING_*`
variable before BenchmarkDotNet starts, and the adapter benchmarks run in `LocalOnly` mode against
a temporary store that is deleted afterwards.

## Benchmark Results

Results are exported to `BenchmarkDotNet.Artifacts/` in multiple formats:
- **HTML** - Interactive results with charts
- **Markdown** - GitHub-friendly reports
- **JSON** - Machine-readable data for analysis

### Interpreting Results

Key metrics to monitor:
- **Mean** - Average execution time
- **StdDev** - Standard deviation (stability indicator)
- **Gen0/Gen1/Gen2** - Garbage collection counts
- **Allocated** - Total memory allocated

## Performance Targets

Targets the SDK aims to meet:

| Metric | Target | Status |
|--------|--------|--------|
| Test tracking overhead | <5ms per test | ⏱️ TBD |
| Memory per test | <100 bytes | 💾 TBD |
| Collection throughput | >10,000 tests/sec | 🚀 TBD |
| Batch upload (100 tests) | <500ms | 📤 TBD |
| Memory footprint (10k tests) | <50MB | 💾 TBD |

## Benchmark Categories

### Core Benchmarks
- `CollectorBenchmarks` - TestExecutionCollector performance
- `BatchProcessingBenchmarks` - recording and draining at different batch sizes
- `ConcurrencyBenchmarks` - recording and draining from many threads
- `MemoryPressureBenchmarks` - large suites and large test payloads
- `UploadBenchmarks` - API client and upload performance
- `ConfigurationBenchmarks` - Configuration loading and validation
- `EnvironmentDetectionBenchmarks` - CI and environment detection

### Integration Benchmarks
- `IntegrationBenchmarks` - End-to-end test lifecycle
- `AdapterIntegrationBenchmarks` - adapter-shaped recording patterns
- `StressTestBenchmarks` - High volume and sustained load

### Adapter Benchmarks
- `RealNUnitAdapterBenchmarks` - NUnit `XpingContext` overhead
- `RealXUnitAdapterBenchmarks` - xUnit `XpingContext` overhead
- `RealMSTestAdapterBenchmarks` - MSTest `XpingContext` overhead

### Local Store Benchmarks
- `LocalStoreBenchmarks` - session write and read
- `LocalStoreScanBenchmarks` - the reads `xping report` makes without `--assembly`, before and after #283

`LocalStoreScanBenchmarks` on Apple Silicon, 50 runs of 2,000 tests per assembly (#283):

| Read | 10 assemblies (81 MB on disk) | 25 assemblies (201 MB on disk) |
|---|---|---|
| Choose scope, loading 200 sessions (before) | 975 ms, 2.1 GB allocated | 983 ms, 2.1 GB allocated |
| Choose scope from file headers (after) | 0.4 ms, 0.4 MB allocated | 0.9 ms, 0.8 MB allocated |
| `--test` lookup, one unscoped read of up to 1000 (before) | 2.5 s, 2.1 GB retained | 7.0 s, 4.3 GB retained, oldest 250 runs cut off |
| `--test` lookup, one pass, one run held at a time (after) | 2.3 s, ~4 MB retained | 5.7 s, ~4 MB retained, every run read |

"Retained" is the managed heap held while the read's sessions are alive, logged by the benchmark's
setup for the old lookup; the new one holds a single run, about 1/50 of one assembly's history.
The lookup still deserializes every run once; what changes is how many are held at once.

## Comparing Runs

No CI workflow runs the benchmarks, and no baseline is committed: numbers depend on the machine.
To check a change, run the affected benchmarks on `main` and on your branch, on the same machine,
and compare the two reports in `BenchmarkDotNet.Artifacts/`.

## Troubleshooting

### Benchmarks Take Too Long
- Reduce iteration count: `dotnet run -c Release -- --iterationCount 5`
- Run single benchmark: Use `--filter` parameter

### Out of Memory
- Reduce batch sizes in stress tests
- Run benchmarks individually

### Inconsistent Results
- Close other applications
- Disable background processes
- Run multiple times and compare

## Contributing

When adding new benchmarks:
1. Follow existing naming conventions
2. Add XML documentation
3. Include realistic test scenarios
4. Update this README with new categories
5. Verify targets are met

## References

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [Performance overview](../../docs/performance/overview.md)
