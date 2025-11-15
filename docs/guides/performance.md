# Performance Guide

Understanding the performance characteristics of the Xping SDK helps you make informed decisions about test observability. This guide explains what to expect, how to optimize, and when to tune performance settings.

## At a Glance

The Xping SDK is designed for **negligible performance impact** on your test suite:

- **~0.7-0.8 microseconds** per test overhead
- **~1KB memory** per test for rich metadata
- **Zero blocking** on test execution (async upload)
- **Automatic batching** for efficient network usage

**Bottom line:** For most test suites, Xping's overhead is unmeasurable compared to test execution time.

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

## Configuration for Performance

### Batch Size

Controls how many tests are batched before uploading:

```json
{
  "Xping": {
    "BatchSize": 100
  }
}
```

**Recommendations:**
- **Small suites (<500 tests)**: 50-100 (more frequent uploads)
- **Medium suites (500-5k tests)**: 100-250 (balanced)
- **Large suites (>5k tests)**: 250-500 (fewer uploads)

**Trade-offs:**
- ✅ **Larger batches**: Fewer network requests, better throughput
- ⚠️ **Larger batches**: Higher memory during batch, delayed upload
- ✅ **Smaller batches**: Lower memory, faster visibility
- ⚠️ **Smaller batches**: More network requests

### Flush Interval

Maximum time before buffered tests are uploaded:

```json
{
  "Xping": {
    "FlushIntervalSeconds": 30
  }
}
```

**Default: 30 seconds** - ensures data is uploaded even if batch size isn't reached.

**When to adjust:**
- **Long-running tests**: Increase to 60-120s to avoid mid-test uploads
- **Short test suites**: Decrease to 10-15s for faster visibility
- **CI/CD pipelines**: Keep default (30s balances responsiveness and efficiency)

### Sampling

Reduce overhead for very large suites by sampling a subset of tests:

```json
{
  "Xping": {
    "SamplingRate": 1.0
  }
}
```

**Values:**
- **1.0** (default): Track all tests
- **0.5**: Track 50% of tests randomly
- **0.1**: Track 10% of tests

**When to use sampling:**
- ✅ Very large test suites (>50k tests)
- ✅ Performance-sensitive environments
- ✅ Exploratory monitoring of flaky patterns

**When NOT to use sampling:**
- ❌ Small/medium test suites (overhead already negligible)
- ❌ Comprehensive flaky test detection (need all data)
- ❌ Exact pass/fail statistics (sampling introduces variance)

### Offline Mode

Disable network uploads entirely (useful for development):

```json
{
  "Xping": {
    "Enabled": false
  }
}
```

**Overhead when disabled:** ~50ns per test (identity check only)

## Performance Best Practices

### 1. Use Default Settings First

The SDK is pre-configured for excellent performance out of the box. **Don't optimize prematurely.**

**Default configuration:**
- BatchSize: 100
- FlushInterval: 30s
- SamplingRate: 1.0
- Enabled: true

**These defaults work well for 95% of use cases.**

### 2. Profile Before Tuning

If you suspect performance issues:

1. **Measure total test suite time** without Xping
2. **Measure with Xping** enabled
3. **Calculate actual overhead** (should be <0.01%)
4. **Profile if overhead is significant** (usually indicates other issues)

**Tools:**
```bash
# .NET profiling
dotnet-trace collect --process-id <pid>
dotnet-counters monitor --process-id <pid>

# BenchmarkDotNet for precise measurements
cd tests/Xping.Sdk.Benchmarks
dotnet run -c Release
```

### 3. Optimize Batch Size for Your Suite

**Decision tree:**

```
Is your test suite < 500 tests?
├─ Yes → Use default (100) or smaller (50)
└─ No → Is it > 10,000 tests?
    ├─ Yes → Consider 250-500
    └─ No → Use default (100-250)
```

**Validation:**
- Monitor memory usage during test runs
- Check upload frequency in logs
- Adjust if you see memory pressure or too many uploads

### 4. Consider Sampling Only for Massive Suites

**Guideline:**
- <10k tests: **No sampling needed** (overhead already tiny)
- 10k-50k tests: **Consider 50% sampling** if performance is critical
- >50k tests: **Use 10-25% sampling** for exploration, 100% for production

**Remember:** Sampling reduces data completeness for flaky test detection.

### 5. Understand CI/CD Overhead

In CI/CD environments, Xping overhead is even less noticeable:

**Why?**
- CI runners typically have more resources
- Network latency is usually lower (cloud to cloud)
- Test execution time dominates (building, setup, teardown)

**Typical CI/CD test run:**
- Build: 30-60s
- Test setup: 5-10s
- Test execution: 1-10 minutes
- Test teardown: 5-10s
- **Xping overhead: <0.01% of total** (unmeasurable)

### 6. Monitor Long-Term Trends

While Xping has minimal overhead, track performance over time:

```bash
# Run benchmarks periodically
cd tests/Xping.Sdk.Benchmarks
dotnet run -c Release -- --filter '*Collector*'

# Compare with baseline
# (Results stored in results/baseline/)
```

**Watch for:**
- ✅ Stable mean times (<10% variance)
- ✅ Low GC pressure (Gen0 only)
- ⚠️ Increasing execution times (may indicate SDK issue)
- ⚠️ Memory growth (review buffer sizes)

## Troubleshooting Performance Issues

### Issue: Tests Are Running Slower

**99% of the time, Xping is NOT the cause.** But let's verify:

**Step 1: Measure Xping's actual overhead**
```bash
# Run tests without Xping
dotnet test --filter "Category=Performance" --logger "console;verbosity=detailed"

# Run tests with Xping
dotnet test --filter "Category=Performance" --logger "console;verbosity=detailed"

# Compare total execution time
```

**Expected difference: <0.1%** (likely unmeasurable)

**Step 2: If overhead is significant (>1%)**
```bash
# Profile your tests to find the real bottleneck
dotnet-trace collect --process-id <pid> --providers Microsoft-Windows-DotNETRuntime

# Analyze trace with PerfView (Windows) or speedscope.app
```

**Common real causes:**
- Slow test setup/teardown
- Database connections or external API calls
- File I/O or network operations in tests
- Inefficient test code
- Resource contention on CI runners

**Step 3: Verify Xping configuration**
```json
{
  "Xping": {
    "Enabled": true,  // Should be true
    "BatchSize": 100, // Reasonable default
    "SamplingRate": 1.0 // No sampling unless intentional
  }
}
```

**Step 4: Still seeing overhead?**

Open an issue with:
- Test suite size and characteristics
- Measured overhead percentage
- Performance profile or trace file
- Configuration settings

### Issue: High Memory Usage

**Symptom:** Tests consuming more memory than expected

**Diagnosis:**

```bash
# Monitor memory during test run
dotnet-counters monitor --process-id <pid> --counters System.Runtime

# Look for:
# - GC Heap Size growing continuously (leak)
# - Frequent Gen2 collections (memory pressure)
# - High allocation rate (excessive objects)
```

**Likely causes and fixes:**

**1. Large batch size with many tests**
- Symptom: Memory spikes every N tests
- Fix: Reduce `BatchSize` from 500 to 100-250
- Trade-off: More frequent uploads

**2. Long flush interval**
- Symptom: Memory grows over time
- Fix: Reduce `FlushIntervalSeconds` from 60 to 30
- Trade-off: More upload requests

**3. Large test parameters**
- Symptom: Tests with huge inputs use more memory
- Fix: Xping captures parameter summaries, not full data
- Note: This is usually not the issue

**4. Memory leak (rare)**
- Symptom: Continuous growth, never released
- Fix: Update to latest SDK version
- Report: Open GitHub issue with memory profile

**Quick fix for memory-constrained environments:**
```json
{
  "Xping": {
    "BatchSize": 50,          // Smaller batches
    "FlushIntervalSeconds": 15, // More frequent uploads
    "SamplingRate": 0.5       // Track 50% of tests
  }
}
```

### Issue: Network Errors or Timeouts

**Symptom:** Seeing Xping upload errors or warnings in logs

**Important:** Network issues **never fail your tests** - they're logged and handled gracefully.

**Common scenarios:**

**1. API temporarily unavailable**
- SDK retries automatically with exponential backoff
- Tests queue locally until API is reachable
- No action needed - this is expected behavior

**2. Firewall or proxy blocking requests**
- Check network policies allow HTTPS to api.xping.io
- Configure proxy if needed:
  ```json
  {
    "Xping": {
      "ProxyUrl": "http://proxy.company.com:8080"
    }
  }
  ```

**3. API key invalid or expired**
- Verify API key in configuration
- Check key hasn't been revoked in Xping dashboard
- Logs will show authentication errors

**4. Running in offline environment (CI/CD)**
- Disable Xping for offline builds:
  ```json
  {
    "Xping": {
      "Enabled": false
    }
  }
  ```
- Or use environment variable: `XPING_ENABLED=false`

### Issue: Inconsistent Overhead Across Frameworks

**Symptom:** Xping seems faster/slower on NUnit vs xUnit vs MSTest

**Reality:** Overhead is **consistent across frameworks** (~0.7-0.8µs per test)

**Measurement differences come from:**
- Framework-specific test discovery and lifecycle
- Attribute processing (NUnit's `[XpingTrack]` is extremely lightweight)
- Test parallelization strategies
- Fixture setup/teardown costs

**Benchmark data (mean overhead per test):**
- **NUnit**: 700-800ns
- **xUnit**: 720-800ns
- **MSTest**: 670-745ns

**Difference: <100ns** (0.0001ms) - completely negligible

## When to Worry About Performance

### You Should Profile If:

❌ **Total test overhead >1%** (very unusual)
❌ **Memory growing continuously** without bounds
❌ **GC pauses** causing noticeable slowdowns
❌ **Tests timing out** that didn't before

### You Should NOT Worry If:

✅ **Overhead <0.1%** (expected and excellent)
✅ **Memory stable** around batch size * 1KB
✅ **Only Gen0 collections** (fast and cheap)
✅ **Network errors** logged but handled (tests pass)

## Performance Philosophy

**Xping's design principle:** Test observability should be **invisible** to developers.

We achieve this through:
- **Asynchronous everything**: No blocking on I/O
- **Efficient data structures**: Minimal allocations
- **Smart batching**: Reduce network overhead
- **Fail gracefully**: Never impact test reliability

**Our promise:** Xping's overhead will **never** be the reason your tests are slow.

If you experience performance issues, we want to know - open an issue and we'll investigate thoroughly.

## Benchmarks and Validation

Detailed benchmark results and methodology are available in:
- [Performance Overview](../performance/overview.md) - Targets and high-level results
- [Benchmark Results](../performance/benchmark-results.md) - Comprehensive data and analysis

**Key validated metrics:**
- ✅ Test tracking overhead: **700-800ns** (7× better than 5ms target)
- ✅ Memory per test: **700-1,100B** (within 1KB target)
- ✅ Throughput: **1.2-1.4M tests/sec** (100× better than 10k target)
- ✅ Batch upload: **32-42µs** (12,000× better than 500ms target)

All benchmarks use [BenchmarkDotNet](https://benchmarkdotnet.org/) for precise, reliable measurements.

## Questions?

If you have questions about performance:
1. Check [Troubleshooting](../troubleshooting/common-issues.md)
2. Review [Benchmark Results](../performance/benchmark-results.md)
3. Open an issue with the `performance` label

We're committed to maintaining negligible overhead - your feedback helps us deliver on that promise.
