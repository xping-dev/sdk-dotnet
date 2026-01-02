# CI/CD Benchmark Integration Plan

**Document Version:** 1.0  
**Date:** November 15, 2025  
**Status:** Design Phase  
**Owner:** Performance Team

---

## Executive Summary

This document outlines the strategy for integrating performance benchmarks into the CI/CD pipeline to enable continuous performance monitoring, regression detection, and performance-aware development workflows.

**Goals:**
1. **Automate benchmark execution** on key events (PRs, releases, scheduled runs)
2. **Detect performance regressions** before they reach production
3. **Track performance trends** across SDK versions
4. **Maintain performance baselines** for comparison
5. **Provide actionable feedback** to developers

**Non-Goals:**
- Replace manual performance analysis (automation complements, not replaces)
- Run full benchmark suite on every commit (too slow)
- Block deployments based solely on benchmarks (advisory, not blocking)

---

## Table of Contents

1. [Overview](#overview)
2. [Benchmark Execution Strategy](#benchmark-execution-strategy)
3. [Baseline Management](#baseline-management)
4. [Regression Detection](#regression-detection)
5. [GitHub Actions Workflows](#github-actions-workflows)
6. [Reporting and Visualization](#reporting-and-visualization)
7. [Implementation Phases](#implementation-phases)
8. [Infrastructure Requirements](#infrastructure-requirements)
9. [Monitoring and Alerting](#monitoring-and-alerting)
10. [Rollout Plan](#rollout-plan)

---

## Overview

### Current State

- ✅ Comprehensive benchmark suite exists (`tests/Xping.Sdk.Benchmarks/`)
- ✅ Benchmarks validated across all SDK components (Phases 2-5 complete)
- ✅ Baseline directory structure defined (`results/baseline/`)
- ❌ No automated benchmark execution in CI/CD
- ❌ No regression detection mechanism
- ❌ No performance tracking across versions

### Target State

- ✅ Automated benchmark execution on PR label trigger
- ✅ Scheduled nightly benchmark runs on main branch
- ✅ Baseline comparison with regression detection
- ✅ Performance results posted to PRs automatically
- ✅ Historical performance data tracked and visualized
- ✅ Performance alerts for significant regressions

### Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Time to detect regression** | <24 hours | Time from merge to detection |
| **False positive rate** | <10% | Regression alerts that aren't real issues |
| **Developer adoption** | >80% | PRs with performance checks |
| **Benchmark execution time** | <15 minutes | Time to run key benchmarks |
| **Baseline update frequency** | Every minor release | Baselines updated on schedule |

---

## Benchmark Execution Strategy

### Trigger Conditions

**1. Pull Request with `performance` Label**
- **When:** Developer adds `performance` label to PR
- **What:** Run focused benchmark suite (fast benchmarks only)
- **Why:** Validate performance-sensitive changes before merge
- **Duration:** ~5-10 minutes

**2. Scheduled Nightly Runs**
- **When:** Daily at 2 AM UTC on `main` branch
- **What:** Run comprehensive benchmark suite
- **Why:** Track long-term performance trends, catch slow regressions
- **Duration:** ~30-45 minutes

**3. Release Candidate Builds**
- **When:** Version tag pushed (e.g., `v1.2.0-rc1`)
- **What:** Run full benchmark suite + stress tests
- **Why:** Validate release performance, update baselines
- **Duration:** ~45-60 minutes

**4. Manual Trigger**
- **When:** Maintainer manually triggers workflow
- **What:** Run specified benchmark subset
- **Why:** Ad-hoc performance investigation
- **Duration:** Variable

### Benchmark Subsets

**Fast Suite** (5-10 minutes)
- Core component benchmarks (Collector, Upload, Configuration)
- Basic integration benchmarks
- Single framework adapter (NUnit only)
- Use `--job short` for faster execution

**Standard Suite** (30-45 minutes)
- All fast suite benchmarks
- All framework adapters (NUnit, xUnit, MSTest)
- Integration benchmarks with multiple scenarios
- Use `--job default`

**Comprehensive Suite** (45-60 minutes)
- All standard suite benchmarks
- Stress and load tests (volume, concurrency)
- Memory pressure tests
- Use `--job long` with detailed profiling

**Configuration:**

```bash
# Fast suite (PR checks)
dotnet run -c Release -- --filter '*Collector*|*Upload*|*Configuration*|*NUnit*' --job short --memory

# Standard suite (nightly)
dotnet run -c Release -- --filter '*' --exclude-category 'Stress' --job default --memory

# Comprehensive suite (releases)
dotnet run -c Release -- --filter '*' --job long --memory --profiler ETW
```

---

## Baseline Management

### Baseline Structure

```
tests/Xping.Sdk.Benchmarks/results/
├── baseline/
│   ├── v1.0/                          # Major version baselines
│   │   ├── CollectorBenchmarks.json
│   │   ├── UploadBenchmarks.json
│   │   ├── IntegrationBenchmarks.json
│   │   └── summary.json
│   ├── v1.1/
│   │   └── ...
│   ├── current -> v1.1/               # Symlink to current baseline
│   └── README.md
├── history/                           # Historical results
│   ├── 2025-11-15-main-abc1234.json
│   ├── 2025-11-14-main-def5678.json
│   └── ...
└── reports/                           # Generated comparison reports
    ├── pr-123-comparison.md
    └── ...
```

### Baseline Update Process

**When to Update:**

1. **Minor/Major Version Releases** (Required)
   - New baseline for v1.1.0, v2.0.0, etc.
   - Captures performance characteristics of released version
   - Manual review and approval required

2. **Performance Optimizations** (Optional)
   - After verified performance improvements
   - Only if improvement is >10% on key metrics
   - Prevents regression detection on intentional improvements

3. **Architecture Changes** (Required)
   - After significant refactoring
   - When old baseline is no longer comparable
   - Documented with rationale

**Update Workflow:**

```bash
# 1. Run comprehensive benchmarks
cd tests/Xping.Sdk.Benchmarks
dotnet run -c Release -- --filter '*' --job long --memory --exporters json

# 2. Review results (compare with current baseline)
./scripts/compare-benchmarks.sh BenchmarkDotNet.Artifacts/results/ results/baseline/current/

# 3. If acceptable, create new baseline version
mkdir -p results/baseline/v1.2/
cp BenchmarkDotNet.Artifacts/results/*.json results/baseline/v1.2/

# 4. Update symlink
ln -sf v1.2 results/baseline/current

# 5. Commit with version tag
git add results/baseline/
git commit -m "chore: update performance baselines for v1.2.0"
git tag -a v1.2.0-baseline -m "Performance baseline for v1.2.0"
git push origin v1.2.0-baseline
```

### Baseline Retention Policy

- **Current version:** Keep indefinitely
- **Last 3 minor versions:** Keep indefinitely
- **Older baselines:** Keep major versions only, archive others
- **Historical results:** Keep 90 days (nightly runs), compress older
- **PR comparison reports:** Keep 30 days

---

## Regression Detection

### Detection Algorithm

**Step 1: Load Baseline**
```csharp
var baseline = LoadBaseline("results/baseline/current/");
var current = LoadBenchmarkResults("BenchmarkDotNet.Artifacts/results/");
```

**Step 2: Compare Key Metrics**

For each benchmark, compare:
- **Mean execution time** (primary metric)
- **Memory allocation** (secondary metric)
- **Standard deviation** (stability metric)

**Step 3: Calculate Performance Delta**

```csharp
var meanDelta = (current.Mean - baseline.Mean) / baseline.Mean * 100;
var memoryDelta = (current.Allocated - baseline.Allocated) / baseline.Allocated * 100;
```

**Step 4: Apply Threshold Rules**

| Severity | Mean Time Delta | Memory Delta | Action |
|----------|----------------|--------------|--------|
| 🟢 **Improvement** | <-10% | <-10% | Comment with praise |
| ⚪ **Acceptable** | -10% to +10% | -10% to +15% | No comment |
| 🟡 **Warning** | +10% to +20% | +15% to +30% | Comment with warning |
| 🔴 **Regression** | >+20% | >+30% | Comment + label PR |
| 💥 **Critical** | >+50% | >+100% | Block merge (manual override) |

**Step 5: Statistical Validation**

To reduce false positives:
1. **Check StdDev:** High variance may indicate unstable results
2. **Historical trend:** Compare with last 5 runs, not just baseline
3. **Multiple runs:** For critical regressions, re-run benchmark 3x
4. **Statistical significance:** Use t-test to validate difference (p < 0.05)

### Handling Variance

**Acceptable Variance:**
- ±5% on cloud CI runners (due to noisy neighbors)
- ±2% on dedicated runners
- ±10% on memory allocations (GC timing variations)

**Variance Reduction Techniques:**
1. Use consistent hardware (GitHub Actions runner labels)
2. Run benchmarks multiple times, use median
3. Disable background services during benchmarks
4. Use BenchmarkDotNet's statistical outlier removal

### Example Regression Report

```markdown
## 🔴 Performance Regression Detected

### CollectorBenchmarks.RecordSingleTest

| Metric | Baseline | Current | Delta | Status |
|--------|----------|---------|-------|--------|
| **Mean** | 309.0 ns | 425.3 ns | **+37.6%** | 🔴 Regression |
| **Memory** | 328 B | 456 B | **+39.0%** | 🔴 Regression |
| **StdDev** | 12.4 ns | 18.7 ns | +50.8% | ⚠️ Less stable |

**Possible Causes:**
- Additional allocations in hot path
- New logging or tracing code
- Inefficient collection usage

**Recommendations:**
1. Profile the benchmark to identify bottleneck
2. Review recent changes to TestExecutionCollector
3. Consider reverting changes or optimizing
4. Run full benchmark suite to assess impact scope

**Historical Context:**
This benchmark has been stable at ~310ns for the last 14 days. This is the largest regression detected in this component.
```

---

## GitHub Actions Workflows

### Workflow 1: PR Performance Check

**File:** `.github/workflows/benchmark-pr.yml`

**Trigger:** PR labeled with `performance`

**Steps:**
1. Checkout code
2. Setup .NET (9.0)
3. Restore dependencies
4. Build in Release mode
5. Run fast benchmark suite
6. Load baseline results
7. Compare current vs. baseline
8. Generate comparison report (markdown)
9. Post report as PR comment
10. Label PR if regression detected

**Estimated Duration:** 8-12 minutes

### Workflow 2: Nightly Performance Tracking

**File:** `.github/workflows/benchmark-nightly.yml`

**Trigger:** Scheduled (cron: `0 2 * * *`)

**Steps:**
1. Checkout main branch
2. Setup .NET (9.0)
3. Build in Release mode
4. Run standard benchmark suite
5. Export results (JSON, Markdown)
6. Compare with baseline
7. Store results in history directory
8. Generate trend report
9. Upload results as artifacts
10. Post to Slack/Discord if regression detected
11. Create GitHub issue for critical regressions

**Estimated Duration:** 35-45 minutes

### Workflow 3: Release Performance Validation

**File:** `.github/workflows/benchmark-release.yml`

**Trigger:** Tag pushed matching `v*.*.*-rc*` or `v*.*.*`

**Steps:**
1. Checkout tag
2. Setup .NET (9.0)
3. Build in Release mode
4. Run comprehensive benchmark suite
5. Export results with profiling data
6. Compare with previous release baseline
7. Generate detailed performance report
8. Upload report and results as release artifacts
9. Update baseline if release (not RC)
10. Post summary to release PR

**Estimated Duration:** 50-70 minutes

### Workflow 4: Manual Benchmark Run

**File:** `.github/workflows/benchmark-manual.yml`

**Trigger:** Manual workflow dispatch

**Inputs:**
- `benchmark-filter`: Filter pattern (default: `*`)
- `job-type`: short/default/long (default: `default`)
- `compare-with-baseline`: true/false (default: `true`)

**Steps:**
1. Checkout specified branch
2. Setup .NET (9.0)
3. Build in Release mode
4. Run benchmarks with specified filter and job
5. Compare with baseline if requested
6. Upload results as artifacts
7. Post summary as workflow comment

**Estimated Duration:** Variable (5-60 minutes)

---

## Reporting and Visualization

### PR Comment Format

```markdown
## ⚡ Performance Benchmark Results

**Benchmark Suite:** Fast  
**Duration:** 8m 23s  
**Commit:** abc1234  
**Compared to:** Baseline v1.1.0

### Summary

✅ **18 benchmarks passed** (within ±10% threshold)
🟡 **2 benchmarks with warnings** (+10-20% slower)
🔴 **1 benchmark with regression** (>20% slower)

### Regressions Detected

#### 🔴 CollectorBenchmarks.RecordSingleTest
- Mean: 309ns → 425ns (+37.6%) 🔴
- Memory: 328B → 456B (+39.0%) 🔴
- [View detailed results](link-to-artifact)

### Warnings

#### 🟡 UploadBenchmarks.UploadBatch100
- Mean: 1.32µs → 1.51µs (+14.4%) 🟡
- Memory: 1312B → 1289B (-1.8%) ✅

### Top Improvements

#### 🟢 ConfigurationBenchmarks.LoadConfiguration
- Mean: 246ns → 195ns (-20.7%) 🟢
- Memory: 504B → 472B (-6.3%) 🟢

---

**📊 [View Full Report](link-to-artifact)** | **📈 [Historical Trends](link-to-dashboard)** | **🔄 [Re-run Benchmarks](link-to-workflow)**

<details>
<summary>How to interpret these results</summary>

- 🟢 **Improvement**: Performance better than baseline
- ⚪ **Acceptable**: Performance within normal variance
- 🟡 **Warning**: Noticeable slowdown, review recommended
- 🔴 **Regression**: Significant slowdown, investigation needed
- 💥 **Critical**: Severe regression, merge blocked

See [Performance Guide](link) for optimization tips.
</details>
```

### Dashboard Requirements

**GitHub Pages Dashboard** (Optional Phase 2)

Display:
1. **Performance Trends** (line charts)
   - Mean execution time over time (per benchmark)
   - Memory allocation trends
   - Throughput trends

2. **Regression History**
   - Timeline of detected regressions
   - Resolution status
   - Impact severity

3. **Baseline Comparison**
   - Current vs. baseline comparison table
   - Sparklines for quick visual assessment
   - Filter by benchmark category

4. **Statistics**
   - Total benchmarks run
   - Average performance delta
   - Regression rate (per month)

**Technology Stack:**
- Static site generator: Jekyll or Gatsby
- Charts: Chart.js or Recharts
- Data source: JSON artifacts from GitHub Actions
- Hosting: GitHub Pages
- Update: Nightly job pushes new data

---

## Implementation Phases

### Phase 1: Foundation (Week 1-2)

**Goals:** Basic automated benchmark execution

**Tasks:**
- [ ] Create `benchmark-pr.yml` workflow
- [ ] Create `benchmark-nightly.yml` workflow
- [ ] Create comparison script (`scripts/compare-benchmarks.sh`)
- [ ] Establish initial baseline (v1.0)
- [ ] Test workflows on feature branch

**Deliverables:**
- ✅ PR-triggered benchmarks working
- ✅ Nightly benchmarks running
- ✅ Basic comparison logic functional
- ✅ Documentation updated

**Success Criteria:**
- Workflow completes in <15 minutes
- Results are comparable and consistent
- No false positive regressions

### Phase 2: Regression Detection (Week 3-4)

**Goals:** Automated regression detection and reporting

**Tasks:**
- [ ] Implement regression detection algorithm
- [ ] Create PR comment template
- [ ] Add threshold-based severity classification
- [ ] Integrate statistical validation
- [ ] Test with synthetic regressions

**Deliverables:**
- ✅ Automated regression comments on PRs
- ✅ Severity-based alerting
- ✅ Historical trend comparison
- ✅ False positive filtering

**Success Criteria:**
- <10% false positive rate
- Regressions detected within 24 hours
- Actionable feedback in PR comments

### Phase 3: Release Integration (Week 5-6)

**Goals:** Release performance validation

**Tasks:**
- [ ] Create `benchmark-release.yml` workflow
- [ ] Implement baseline update automation
- [ ] Add release performance report
- [ ] Version baseline management
- [ ] Test on RC builds

**Deliverables:**
- ✅ Release benchmarks automated
- ✅ Baseline versioning working
- ✅ Release performance reports
- ✅ Baseline update process documented

**Success Criteria:**
- Release benchmarks complete before release
- Baselines updated accurately
- Performance reports included in release notes

### Phase 4: Visualization & Monitoring (Week 7-8)

**Goals:** Performance tracking dashboard and alerts

**Tasks:**
- [ ] Create GitHub Pages dashboard
- [ ] Implement trend visualization
- [ ] Setup Slack/Discord notifications
- [ ] Add performance badges to README
- [ ] Historical data retention

**Deliverables:**
- ✅ Live performance dashboard
- ✅ Slack notifications for regressions
- ✅ Performance badges in README
- ✅ 90-day historical data available

**Success Criteria:**
- Dashboard updates nightly
- Alerts sent within 1 hour of detection
- Developers can access trends easily

---

## Infrastructure Requirements

### GitHub Actions Runner

**Recommended:** GitHub-hosted Ubuntu runner (latest)

**Specs:**
- 2-core CPU (4-core for comprehensive suite)
- 7 GB RAM
- 14 GB SSD storage
- Consistent performance profile

**Considerations:**
- Use runner labels for consistency (`runs-on: ubuntu-latest`)
- Avoid Windows runners (higher variance)
- Consider self-hosted runner for better consistency (Phase 2+)

### Storage Requirements

**GitHub Artifacts:**
- Retention: 90 days
- Per-run size: ~50 MB (results + reports)
- Monthly storage: ~1.5 GB

**Repository Storage:**
- Baselines: ~10 MB per version (keep 10 versions = 100 MB)
- Historical data: ~500 KB per day × 90 days = 45 MB
- Total: ~150 MB

**GitHub Pages (optional):**
- Dashboard: ~5 MB
- Historical charts: ~50 MB
- Total: ~55 MB

### API Rate Limits

**GitHub API:**
- Comment posting: ~1 request per PR benchmark
- Artifact uploads: ~3 requests per workflow
- Estimate: <100 requests/day (well under limit)

**External APIs:**
- Slack notifications: ~5 per day (nightly + critical alerts)
- No rate limit concerns

---

## Monitoring and Alerting

### Workflow Health Monitoring

**Metrics to Track:**
1. **Workflow success rate** (target: >95%)
2. **Workflow duration** (target: <15 min for PR, <45 min for nightly)
3. **Comparison failure rate** (target: <5%)
4. **False positive rate** (target: <10%)

**Alerts:**
- Workflow failures: Notify maintainers via GitHub issues
- Duration exceeds threshold: Investigate runner performance
- High false positive rate: Adjust thresholds

### Performance Alert Rules

**Critical Regression:**
- Condition: >50% slowdown on any core benchmark
- Action: Create GitHub issue, Slack alert, block PR merge
- Response: Investigate within 4 hours

**Warning Regression:**
- Condition: >20% slowdown on any benchmark
- Action: PR comment, optional Slack notification
- Response: Review before merge, consider profiling

**Consistent Degradation:**
- Condition: >10% slowdown on same benchmark for 3+ consecutive days
- Action: Create GitHub issue with trend analysis
- Response: Schedule performance review

### Dashboard Health

**Metrics:**
- Dashboard build success rate (target: 100%)
- Data freshness (target: <24 hours old)
- Chart rendering errors (target: 0%)

---

## Rollout Plan

### Pre-Rollout Checklist

- [ ] Benchmark suite validated and stable
- [ ] Baseline v1.0 established and committed
- [ ] Workflows tested on feature branch
- [ ] Comparison script produces accurate results
- [ ] Documentation complete and reviewed
- [ ] Team training on interpreting results

### Rollout Schedule

**Week 1: Soft Launch**
- Enable PR benchmarks (opt-in with label)
- Monitor for false positives
- Gather team feedback
- Adjust thresholds if needed

**Week 2: Nightly Runs**
- Enable scheduled nightly benchmarks
- Verify historical data collection
- Test alert notifications
- Validate baseline comparisons

**Week 3: PR Integration**
- Make performance checks recommended for all PRs
- Add workflow status badge to README
- Document best practices
- Train team on optimization workflows

**Week 4: Release Integration**
- Enable release benchmark workflows
- Test baseline update process
- Validate release reports
- Document release runbook

**Week 5+: Optimization**
- Monitor workflow performance
- Adjust thresholds based on data
- Implement dashboard (Phase 4)
- Continuous improvement

### Success Criteria

**Week 4 Goals:**
- ✅ 80% of PRs run performance checks
- ✅ <10% false positive rate
- ✅ Zero critical regressions missed
- ✅ Workflows complete reliably (<5% failure rate)
- ✅ Team actively using performance data

### Rollback Plan

If issues arise:
1. Disable failing workflows (comment out trigger)
2. Preserve historical data and baselines
3. Analyze failure cause (logs, runner issues, script bugs)
4. Fix issue on feature branch
5. Re-test thoroughly before re-enabling
6. Communicate status to team

---

## Appendix

### A. Script: Compare Benchmarks

**Location:** `scripts/compare-benchmarks.sh`

```bash
#!/bin/bash
# Compare benchmark results with baseline
# Usage: ./compare-benchmarks.sh <current-results-dir> <baseline-dir>

CURRENT_DIR=$1
BASELINE_DIR=$2
OUTPUT_FILE="benchmark-comparison.md"

# Load and compare results (pseudocode)
# - Parse JSON results
# - Calculate deltas
# - Apply threshold rules
# - Generate markdown report
```

### B. Workflow Example Snippets

See implementation phases for detailed workflow YAML.

### C. Threshold Tuning Guide

**Initial Thresholds (Conservative):**
- Warning: +15% (catch most issues)
- Regression: +25% (clear problems)
- Critical: +100% (severe)

**After 30 Days (Data-Driven):**
- Analyze variance in CI environment
- Calculate 95th percentile variance
- Set warning at 2× variance
- Set regression at 3× variance
- Set critical at 10× variance

**Example Calculation:**
- Observed variance: ±8%
- Warning: 2 × 8% = 16%
- Regression: 3 × 8% = 24%
- Critical: 10 × 8% = 80%

### D. Resources

**Documentation:**
- [BenchmarkDotNet Docs](https://benchmarkdotnet.org/)
- [GitHub Actions Docs](https://docs.github.com/en/actions)
- [Performance Testing Guide](../guides/performance.md)

**Tools:**
- BenchmarkDotNet CLI
- GitHub CLI (`gh`)
- jq (JSON processing)
- Python/Node.js (for dashboard)

**References:**
- [.NET Performance Best Practices](https://learn.microsoft.com/en-us/dotnet/framework/performance/)
- [Performance Regression Detection](https://abseil.io/resources/swe-book/html/ch19.html)

---

**Document Status:** ✅ Complete - Ready for Implementation

**Next Steps:**
1. Review and approve plan
2. Create GitHub project for tracking
3. Assign implementation tasks
4. Begin Phase 1 (Foundation)

**Feedback:** Open an issue with label `performance` for questions or suggestions.
