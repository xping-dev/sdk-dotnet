# Performance Configuration

This guide explains how to configure the Xping SDK for optimal performance in your specific environment. While the default settings work well for 95% of use cases, you may need to tune configuration for very large test suites or resource-constrained environments.

---

## Configuration Options

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

---

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

---

### Completeness

Xping records every execution. There is no sampling knob, and that is deliberate: retry detection,
flake analysis, and vanished-test findings all reason over the full execution set. A dropped
attempt does not read as "missing" — it reads as a test that failed, or as a test that stopped
running.

Overhead is ~300 ns per recorded test (~15 ms for a 50k-test suite), so there is nothing to trade
away.

---

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

---

## Performance Best Practices

### 1. Use Default Settings First

The SDK is pre-configured for excellent performance out of the box. **Don't optimize prematurely.**

**Default configuration:**
- BatchSize: 100
- FlushInterval: 30s
- Enabled: true

**These defaults work well for 95% of use cases.**

---

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

---

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

---

### 4. Understand CI/CD Overhead

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

---

### 5. Monitor Long-Term Trends

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

---

## Example Configurations

### Small Test Suite (<500 tests)

```json
{
  "Xping": {
    "BatchSize": 50,
    "FlushIntervalSeconds": 15
  }
}
```

**Benefits:**
- Faster visibility in Xping Cloud
- Lower memory footprint
- More frequent uploads

---

### Large Test Suite (>10,000 tests)

```json
{
  "Xping": {
    "BatchSize": 500,
    "FlushIntervalSeconds": 60
  }
}
```

**Benefits:**
- Fewer network requests
- Higher throughput
- Efficient for large volumes

---

### Memory-Constrained Environment

```json
{
  "Xping": {
    "BatchSize": 50,
    "FlushIntervalSeconds": 15
  }
}
```

**Benefits:**
- Minimal memory usage
- Reduced overhead
- Still provides valuable insights

---

### Massive Test Suite (>50,000 tests)

```json
{
  "Xping": {
    "BatchSize": 1000,
    "FlushIntervalSeconds": 120
  }
}
```

**Benefits:**
- Manageable overhead at scale
- Efficient resource usage

---

### Development/Offline Mode

```json
{
  "Xping": {
    "Enabled": false
  }
}
```

Or use environment variable:
```bash
export XPING_ENABLED=false
dotnet test
```

**Benefits:**
- No network calls
- Minimal overhead (<50ns per test)
- Useful for local development

---

## See Also

- [Performance Overview](./performance-overview.md) - Understanding SDK performance characteristics
- [Performance Troubleshooting](./performance-troubleshooting.md) - Diagnosing performance issues
- [Configuration Reference](../../configuration/configuration-reference.md) - Complete configuration options
