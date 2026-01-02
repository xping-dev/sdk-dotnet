# Performance Overview

Understanding the performance characteristics of the Xping SDK helps you make informed decisions about test observability. This guide explains what to expect from the SDK's performance impact on your test suite.

---

## At a Glance

The Xping SDK is designed for **negligible performance impact** on your test suite:

- **~0.7-0.8 microseconds** per test overhead
- **~1KB memory** per test for rich metadata
- **Zero blocking** on test execution (async upload)
- **Automatic batching** for efficient network usage

**Bottom line:** For most test suites, Xping's overhead is unmeasurable compared to test execution time.

---

## Understanding SDK Overhead

### What Gets Tracked?

Every test execution captures rich metadata:

- **Test Identity**: Framework, class, method, parameters
- **Execution Data**: Start time, duration, outcome, error details
- **Environment Context**: OS, runtime, CI/CD platform, git commit
- **Custom Metadata**: Tags, annotations, flaky detection signals

This metadata enables powerful analysis while maintaining minimal overhead.

### Performance Impact by Test Suite Size

Here's what Xping overhead looks like in practice:

| Test Suite Size | Test Execution Time | Xping Overhead | Impact |
|----------------|---------------------|----------------|--------|
| **10 tests** | 5 seconds | ~8 µs | 0.0002% |
| **100 tests** | 30 seconds | ~75 µs | 0.0003% |
| **1,000 tests** | 5 minutes | ~750 µs | 0.0003% |
| **10,000 tests** | 30 minutes | ~7.5 ms | 0.0004% |

**Real-world example:**
- Test suite: 5,000 tests @ 10ms each = 50 seconds
- Xping overhead: ~4ms total
- **Impact: <0.01%** - completely negligible

### Where Time Is Spent

Breaking down the 0.7-0.8µs per test:

1. **Data Collection** (~300ns)
   Capture test identity and outcome, record execution timing, and gather environment context

2. **Buffering** (~200ns)
   Add to in-memory buffer, check batch threshold, minimal allocation (Gen0 only)

3. **Processing** (~200-300ns)
   Apply sampling rules, enrich with metadata, and prepare for upload

**Network upload happens asynchronously** - it never blocks your tests.

---

## Memory Usage

### Per-Test Memory

Each test consumes approximately **700-1,100 bytes** of memory:

- **Test metadata**: ~400B (identity, parameters, attributes)
- **Environment data**: ~200B (OS, runtime, CI context, git info)
- **Execution data**: ~100B (timing, outcome, error summary)
- **Framework metadata**: ~200B (adapter-specific attributes)

**Total: ~1KB per test** - less memory than a small JSON object.

### Memory Characteristics

✅ **Short-lived allocations**: Most memory is Gen0 (cheapest GC)
✅ **Automatic cleanup**: Batches are released after upload
✅ **Bounded growth**: Buffer limits prevent runaway memory
✅ **No leaks**: Comprehensive testing validates cleanup

**Example memory footprint:**
- 1,000 tests buffered: ~1MB
- 10,000 tests buffered: ~10MB
- Typical peak (500 batch size): ~500KB

Even large test suites have minimal memory impact.

---

## Network Performance

### Asynchronous Upload

**Key principle:** Xping **never blocks** your test execution for network I/O.

**How it works:**
1. Tests complete and data is buffered locally
2. When batch size is reached, upload starts **asynchronously**
3. Tests continue running while upload happens in background
4. No waiting, no blocking, no delays

### Batch Upload Efficiency

Xping batches tests before uploading to minimize network overhead:

| Batch Size | Tests/Request | Typical Upload Time |
|-----------|---------------|---------------------|
| **100** (default) | 100 | 32-42µs |
| **500** | 500 | ~150µs |
| **1,000** | 1,000 | ~300µs |

**Upload time is measured for the API call itself**, not including network latency. Network requests happen in the background and don't affect test execution.

### Network Resilience

- **Retry logic**: Automatic retry with exponential backoff
- **Graceful degradation**: SDK continues if network fails
- **No test failures**: Network issues never fail your tests

---

## Performance Philosophy

**Xping's design principle:** Test observability should be **invisible** to developers.

We achieve this through:
- **Asynchronous everything**: No blocking on I/O
- **Efficient data structures**: Minimal allocations
- **Smart batching**: Reduce network overhead
- **Fail gracefully**: Never impact test reliability

**Our promise:** Xping's overhead will **never** be the reason your tests are slow.

If you experience performance issues, we want to know - open an issue and we'll investigate thoroughly.

---

## Benchmarks and Validation

Detailed benchmark results and methodology are available in the benchmark project (`tests/Xping.Sdk.Benchmarks/README.md`) and [Performance Benchmark Results](../../performance/benchmark-results.md).

**Key validated metrics:**
- ✅ Test tracking overhead: **700-800ns** (7× better than 5ms target)
- ✅ Memory per test: **700-1,100B** (within 1KB target)
- ✅ Throughput: **1.2-1.4M tests/sec** (100× better than 10k target)
- ✅ Batch upload: **32-42µs** (12,000× better than 500ms target)

All benchmarks use [BenchmarkDotNet](https://benchmarkdotnet.org/) for precise, reliable measurements.

---

## See Also

- [Performance Configuration](./performance-configuration.md) - Tuning performance settings
- [Performance Troubleshooting](./performance-troubleshooting.md) - Diagnosing performance issues
- [Performance Benchmark Results](../../performance/benchmark-results.md) - Detailed benchmark data
