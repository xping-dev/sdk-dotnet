# Performance Troubleshooting

This guide helps you diagnose and fix performance issues related to the Xping SDK. In most cases, Xping is NOT the cause of slow tests, but this guide will help you verify and address any SDK-related performance concerns.

---

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

---

## Issue: Tests Are Running Slower

**99% of the time, Xping is NOT the cause.** But let's verify:

### Step 1: Measure Xping's Actual Overhead

```bash
# Run tests without Xping
dotnet test --filter "Category=Performance" --logger "console;verbosity=detailed"

# Run tests with Xping
dotnet test --filter "Category=Performance" --logger "console;verbosity=detailed"

# Compare total execution time
```

**Expected difference: <0.1%** (likely unmeasurable)

### Step 2: If Overhead is Significant (>1%)

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

### Step 3: Verify Xping Configuration

```json
{
  "Xping": {
    "Enabled": true,  // Should be true
    "BatchSize": 100, // Reasonable default
    "SamplingRate": 1.0 // No sampling unless intentional
  }
}
```

### Step 4: Still Seeing Overhead?

Open an issue with:
- Test suite size and characteristics
- Measured overhead percentage
- Performance profile or trace file
- Configuration settings

---

## Issue: High Memory Usage

**Symptom:** Tests consuming more memory than expected

### Diagnosis

```bash
# Monitor memory during test run
dotnet-counters monitor --process-id <pid> --counters System.Runtime

# Look for:
# - GC Heap Size growing continuously (leak)
# - Frequent Gen2 collections (memory pressure)
# - High allocation rate (excessive objects)
```

### Likely Causes and Fixes

#### 1. Large Batch Size with Many Tests

- **Symptom**: Memory spikes every N tests
- **Fix**: Reduce `BatchSize` from 500 to 100-250
- **Trade-off**: More frequent uploads

```json
{
  "Xping": {
    "BatchSize": 100
  }
}
```

#### 2. Long Flush Interval

- **Symptom**: Memory grows over time
- **Fix**: Reduce `FlushIntervalSeconds` from 60 to 30
- **Trade-off**: More upload requests

```json
{
  "Xping": {
    "FlushIntervalSeconds": 30
  }
}
```

#### 3. Large Test Parameters

- **Symptom**: Tests with huge inputs use more memory
- **Fix**: Xping captures parameter summaries, not full data
- **Note**: This is usually not the issue

#### 4. Memory Leak (Rare)

- **Symptom**: Continuous growth, never released
- **Fix**: Update to latest SDK version
- **Report**: Open GitHub issue with memory profile

### Quick Fix for Memory-Constrained Environments

```json
{
  "Xping": {
    "BatchSize": 50,          // Smaller batches
    "FlushIntervalSeconds": 15, // More frequent uploads
    "SamplingRate": 0.5       // Track 50% of tests
  }
}
```

---

## Issue: Network Errors or Timeouts

**Symptom:** Seeing Xping upload errors or warnings in logs

**Important:** Network issues **never fail your tests** - they're logged and handled gracefully.

### Common Scenarios

#### 1. API Temporarily Unavailable

- SDK retries automatically with exponential backoff
- Tests queue locally until API is reachable
- **No action needed** - this is expected behavior

#### 2. Firewall or Proxy Blocking Requests

- Check network policies allow HTTPS to upload.xping.io

#### 3. API Key Invalid or Expired

- Verify API key in configuration
- Check key hasn't been revoked in Xping dashboard
- Logs will show authentication errors

#### 4. Running in Offline Environment (CI/CD)

Disable Xping for offline builds:

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
```

---

## Issue: Inconsistent Overhead Across Frameworks

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

---

## Diagnostic Commands

### Monitor Test Execution

```bash
# Basic monitoring
dotnet test --logger "console;verbosity=detailed"

# Memory monitoring
dotnet-counters monitor --process-id <pid> --counters System.Runtime

# Performance trace
dotnet-trace collect --process-id <pid> --providers Microsoft-Windows-DotNETRuntime
```

### Analyze Traces

**Windows:**
```bash
# Install PerfView
choco install perfview

# Open trace file
perfview <trace-file>.nettrace
```

**Cross-platform:**
```bash
# Convert to speedscope format
dotnet-trace convert <trace-file>.nettrace --format speedscope

# Upload to https://www.speedscope.app/
```

### Run Benchmarks

```bash
# Navigate to benchmark project
cd tests/Xping.Sdk.Benchmarks

# Run all benchmarks
dotnet run -c Release

# Run specific benchmark
dotnet run -c Release -- --filter '*Collector*'

# Export results
dotnet run -c Release -- --exporters json
```

---

## Performance Checklist

When investigating performance issues:

- [ ] Measure test suite time without Xping
- [ ] Measure test suite time with Xping
- [ ] Calculate actual overhead percentage
- [ ] Check Xping configuration (batch size, flush interval)
- [ ] Monitor memory usage during test run
- [ ] Review GC collections (should be mostly Gen0)
- [ ] Check for memory leaks (continuous growth)
- [ ] Verify network connectivity (errors logged but tests pass)
- [ ] Profile test execution if overhead >1%
- [ ] Compare with benchmark results

---

## Getting Help

If you've followed this guide and still have performance concerns:

1. **Check [Common Issues](../../troubleshooting/common-issues.md)** for known problems
2. **Review [Performance Overview](./performance-overview.md)** for expected characteristics
3. **Open a GitHub issue** with:
   - Test suite size and framework
   - Measured overhead percentage
   - Configuration settings
   - Memory profile or trace (if available)
   - Steps to reproduce

**We're committed to negligible overhead** - your feedback helps us deliver on that promise.

---

## See Also

- [Performance Overview](./performance-overview.md) - Understanding SDK performance
- [Performance Configuration](./performance-configuration.md) - Tuning settings
- [Common Issues](../../troubleshooting/common-issues.md) - General troubleshooting
- [Performance Benchmark Results](../../performance/benchmark-results.md) - Detailed benchmarks
