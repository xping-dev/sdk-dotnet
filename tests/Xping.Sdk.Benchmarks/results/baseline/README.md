# Baseline Results

This directory contains baseline performance benchmark results used for regression detection.

## Purpose

Baseline results provide a reference point for comparing performance across SDK versions. When benchmarks run in CI/CD, they compare against these baselines to detect performance regressions.

## Structure

```
baseline/
├── CollectorBenchmarks-report.html
├── CollectorBenchmarks-report.md
├── UploadBenchmarks-report.html
├── UploadBenchmarks-report.md
├── IntegrationBenchmarks-report.html
├── IntegrationBenchmarks-report.md
└── summary.json
```

## Updating Baselines

Baselines should be updated when:
1. **Major version releases** - New baseline for new major version
2. **Performance improvements** - After verified performance optimizations
3. **Architecture changes** - After significant refactoring

### How to Update

```bash
# Run benchmarks
cd tests/Xping.Sdk.Benchmarks
dotnet run -c Release

# Copy results to baseline (after review)
cp -r BenchmarkDotNet.Artifacts/results/* results/baseline/

# Commit the new baseline
git add results/baseline/
git commit -m "chore: update performance baselines for v1.x.x"
```

## Version History

| Version | Date | Notes |
|---------|------|-------|
| 1.0.0 | TBD | Initial baseline (MVP release) |

## Notes

- Baselines are committed to the repository for easy access in CI/CD
- Results are platform-specific (macOS/Linux/Windows may differ)
- Always run benchmarks on the same hardware configuration when updating baselines
