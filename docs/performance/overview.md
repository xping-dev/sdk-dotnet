---
uid: performance-overview
title: Performance Testing
---

# Performance Testing

This section contains documentation and guidelines for performance testing the Xping SDK.

> **Looking for practical performance guidance?** See the [Performance Guide](../guides/performance.md) for user-facing documentation on SDK overhead, configuration, and optimization tips.

## Overview

Performance testing ensures the SDK meets its performance targets and maintains minimal overhead on test execution. We use [BenchmarkDotNet](https://benchmarkdotnet.org/) for precise, reliable performance measurements.

## Performance Targets

The Xping SDK is designed to have minimal impact on test execution. Our performance targets are:

| Metric | Target | Rationale |
|--------|--------|-----------|
| **Test Tracking Overhead** | <5ms per test | Negligible impact on test suite execution time |
| **Memory per Test Execution** | <1KB per test | Efficient memory usage for rich metadata capture |
| **Collection Throughput** | >10,000 tests/sec | Handle high-volume test execution |
| **Batch Upload (100 tests)** | <500ms | Efficient network utilization |
| **Memory Footprint (10k tests)** | <50MB | Reasonable memory consumption |
| **CPU Overhead** | <5% | Minimal CPU usage during test execution |

## Running Benchmarks

See the benchmark project README (`tests/Xping.Sdk.Benchmarks/README.md`) for detailed instructions.

Quick start:
```bash
cd tests/Xping.Sdk.Benchmarks
dotnet run -c Release
```

## Benchmark Categories

### 1. Core Component Benchmarks
Measure performance of core SDK components:
- **TestExecutionCollector** - Recording and buffering overhead
- **XpingApiClient** - Upload and network performance
- **Configuration** - Configuration loading and validation
- **Environment Detection** - Platform and CI detection

### 2. Integration Benchmarks
End-to-end performance measurements:
- Complete test lifecycle (record → buffer → batch → upload)
- Sampling performance at various rates

### 3. Adapter Benchmarks
Framework-specific overhead:
- NUnit adapter (`[XpingTrack]` attribute processing)
- xUnit adapter (test framework integration)
- MSTest adapter (base class overhead)

### 4. Stress & Load Tests
High-volume scenarios:
- 10,000+ test executions
- Concurrent recording from multiple threads
- Sustained load over time
- Memory leak detection

## Performance Analysis

### Key Metrics

**Execution Time:**
- **Mean** - Average execution time
- **Median** - Middle value (50th percentile)
- **StdDev** - Standard deviation (consistency indicator)
- **P95/P99** - 95th/99th percentile (worst-case scenarios)

**Memory:**
- **Gen0/Gen1/Gen2** - Garbage collection frequency
- **Allocated** - Total memory allocated
- **Peak Working Set** - Maximum memory usage

**Throughput:**
- **Operations/sec** - Throughput under load
- **Scalability** - Performance under concurrent load

### Interpreting Results

✅ **Good Performance Indicators:**
- Mean time within target
- Low standard deviation (<10% of mean)
- Minimal GC pressure (low Gen2 collections)
- Linear scalability with load

⚠️ **Warning Signs:**
- High standard deviation (>25% of mean)
- Frequent Gen2 collections
- Memory growth over time
- Performance degradation under load

❌ **Performance Issues:**
- Mean time exceeds target by >20%
- Memory leaks (continuous growth)
- Lock contention (poor concurrency)
- High CPU usage (>10%)

## Baseline Results

Baseline performance results are stored in `tests/Xping.Sdk.Benchmarks/results/baseline/`.

To establish a new baseline:
```bash
cd tests/Xping.Sdk.Benchmarks
dotnet run -c Release
cp BenchmarkDotNet.Artifacts/results/* results/baseline/
```

## Performance Regression Detection

### CI/CD Integration

Benchmarks run automatically on PRs labeled with `performance`. Results are compared against the baseline, and comments are posted if regressions are detected.

**Regression Thresholds:**
- ⚠️ Warning: >10% slower than baseline
- ❌ Failure: >20% slower than baseline
- 💥 Critical: >50% slower than baseline

### Manual Comparison

Compare current results with baseline:
```bash
# Run benchmarks
dotnet run -c Release

# Compare with baseline (requires BenchmarkDotNet.Tool)
dotnet benchmark compare baseline current --threshold 10%
```

## Optimization Guidelines

### Code-Level Optimizations

1. **Avoid Allocations in Hot Paths**
   - Use object pooling for frequently allocated objects
   - Reuse buffers and collections
   - Use `stackalloc` for small arrays

2. **Minimize Lock Contention**
   - Use lock-free data structures where possible
   - Reduce lock scope
   - Consider reader-writer locks for read-heavy scenarios

3. **Optimize String Operations**
   - Use `StringBuilder` for concatenation
   - Cache computed strings
   - Use `Span<char>` for string manipulation

4. **Efficient Collections**
   - Use `ConcurrentQueue` for thread-safe queues
   - Pre-size collections when size is known
   - Use `ValueTask` for potentially synchronous operations

### Profiling Tools

**Recommended Tools:**
- **BenchmarkDotNet** - Microbenchmarking and memory profiling
- **dotnet-trace** - Performance trace collection
- **dotnet-counters** - Real-time performance counters
- **PerfView** (Windows) - Advanced performance analysis
- **Instruments** (macOS) - Time profiler and memory allocations

**Example Usage:**
```bash
# Collect performance trace
dotnet-trace collect --process-id <pid> --providers Microsoft-Windows-DotNETRuntime

# Monitor performance counters
dotnet-counters monitor --process-id <pid>
```

## Troubleshooting Performance Issues

### High Memory Usage

1. Check for memory leaks with memory profiler
2. Review buffer sizes and limits
3. Verify proper disposal of resources
4. Monitor GC behavior

### Slow Test Execution

1. Profile to identify bottlenecks
2. Check for synchronous I/O in async code
3. Review lock contention
4. Verify configuration settings

### Poor Throughput

1. Check batch sizes
2. Review concurrent collection limits
3. Profile async/await patterns
4. Monitor CPU and I/O utilization

## Best Practices

1. **Always run benchmarks in Release mode** - Debug builds have significant overhead
2. **Close background applications** - Minimize interference from other processes
3. **Run multiple iterations** - Single runs can be misleading
4. **Use representative data** - Benchmark with realistic test scenarios
5. **Monitor trends over time** - Track performance across releases
6. **Profile before optimizing** - Don't guess where the bottleneck is
7. **Validate optimizations** - Measure impact of changes

## References

- [BenchmarkDotNet Best Practices](https://benchmarkdotnet.org/articles/guides/good-practices.html)
- [.NET Performance Tips](https://learn.microsoft.com/en-us/dotnet/framework/performance/)
- [Async Programming Best Practices](https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)
- [Memory Management in .NET](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/)

## Contributing

When making performance-sensitive changes:

1. Run benchmarks before and after changes
2. Document performance impact in PR description
3. Include benchmark results in PR comments
4. Update baselines if improvements are significant
5. Add new benchmarks for new features

---

For questions or issues related to performance testing, please open an issue with the `performance` label.
