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

Based on the SDK implementation plan, we aim to meet these targets:

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
- `UploadBenchmarks` - API client and upload performance
- `ConfigurationBenchmarks` - Configuration loading and validation

### Integration Benchmarks
- `IntegrationBenchmarks` - End-to-end test lifecycle
- `StressTestBenchmarks` - High volume and sustained load

### Adapter Benchmarks
- `NUnitAdapterBenchmarks` - NUnit adapter overhead
- `XUnitAdapterBenchmarks` - xUnit adapter overhead
- `MSTestAdapterBenchmarks` - MSTest adapter overhead

## CI/CD Integration

Benchmarks run automatically on:
- PRs labeled with `performance`
- Release branches
- Weekly scheduled runs

Performance regression alerts are posted to PRs if significant degradation is detected (>20%).

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
- [Xping SDK Implementation Plan](../../SDK-Implementation-Plan.md)
- [Performance Testing Guidelines](../../docs/performance/guidelines.md)
