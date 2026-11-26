# Xping SDK Benchmarks

BenchmarkDotNet benchmarks for the SDK's own code paths. They are for contributors who change the
SDK and want to check the cost of a change. For what the SDK does during a test run, see
[Performance](https://docs.xping.io/performance/performance.html).

No CI workflow runs these benchmarks, and no baseline is committed. The numbers depend on the
machine.

## Run the benchmarks

Run them from this folder. BenchmarkDotNet writes its results to `BenchmarkDotNet.Artifacts/` in the
current folder.

```bash
cd tests/Xping.Sdk.Benchmarks

# One class
dotnet run -c Release -- --filter '*CollectorBenchmarks*'

# One method
dotnet run -c Release -- --filter '*CollectorBenchmarks.RecordSingleTest*'

# Everything (takes a long time)
dotnet run -c Release -- --filter '*'
```

Without `--filter`, BenchmarkDotNet asks which benchmarks to run.

Useful options, after `--`:

- `--iterationCount 5` runs fewer iterations, for a quick check.
- `--profiler EP` adds an EventPipe trace of each benchmark.

### Isolation

Benchmarks never upload and never touch your local history. `Program.Main` clears every `XPING_*`
variable before BenchmarkDotNet starts, and BenchmarkDotNet's child processes inherit that
environment. The adapter benchmarks run in `LocalOnly` mode against a temporary store that is deleted
afterwards.

## Read the results

Each run writes HTML, GitHub Markdown and JSON reports to `BenchmarkDotNet.Artifacts/results/`.
`Program.cs` adds the memory and threading diagnosers, so the reports have these columns:

- **Mean** and **StdDev**: the time per operation, and how much it varied.
- **Gen0**, **Gen1**, **Gen2**: garbage collections per 1,000 operations.
- **Allocated**: managed memory allocated per operation.
- **Lock Contentions** and **Completed Work Items**: from the threading diagnoser.

`Allocated` is what one operation allocates, not what the SDK keeps. The SDK keeps every execution of
a run until the session ends, so its retained memory grows with the run.

## Compare two versions

Run the affected benchmarks on `main` and on your branch, on the same machine, and compare the two
reports. Move the first results out of the repository, so they are not committed:

```bash
git switch main
dotnet run -c Release -- --filter '*Collector*'
mv BenchmarkDotNet.Artifacts "${TMPDIR:-/tmp}/xping-benchmarks-main"

git switch my-branch
dotnet run -c Release -- --filter '*Collector*'
```

Treat a mean more than 10% slower, or new Gen1 or Gen2 collections, as worth a closer look. Close
other applications while the benchmarks run, and run a noisy benchmark twice.

## The benchmarks

| Class | What it measures |
|---|---|
| `CollectorBenchmarks` | Recording into `TestExecutionCollector` |
| `BatchProcessingBenchmarks` | Recording and draining at different batch sizes |
| `ConcurrencyBenchmarks` | Recording and draining from many threads |
| `MemoryPressureBenchmarks` | Large runs and large test payloads |
| `UploadBenchmarks` | Serializing sessions to JSON. No benchmark makes a network call. |
| `ConfigurationBenchmarks` | Loading and validating configuration |
| `EnvironmentDetectionBenchmarks` | Detecting the OS, the runtime and the CI |
| `IntegrationBenchmarks` | Building and recording tests end to end, passing, failing and with stack traces |
| `AdapterIntegrationBenchmarks` | Recording patterns shaped like each adapter's |
| `StressTestBenchmarks` | High volume and sustained load |
| `RealNUnitAdapterBenchmarks`, `RealXUnitAdapterBenchmarks`, `RealMSTestAdapterBenchmarks` | Each adapter's `XpingContext`: building a record and calling `RecordTest`. They do not run a test framework. |
| `LocalStoreBenchmarks` | Writing and reading a session in the local store |
| `LocalStoreScanBenchmarks` | The store reads that `xping report` makes without `--assembly` |

`LocalStoreScanBenchmarks` on Apple Silicon, 50 runs of 2,000 tests per assembly (#283):

| Read | 10 assemblies (81 MB on disk) | 25 assemblies (201 MB on disk) |
|---|---|---|
| Choose scope, loading 200 sessions (before) | 975 ms, 2.1 GB allocated | 983 ms, 2.1 GB allocated |
| Choose scope from file headers (after) | 0.4 ms, 0.4 MB allocated | 0.9 ms, 0.8 MB allocated |
| `--test` lookup, one unscoped read of up to 1000 (before) | 2.5 s, 2.1 GB retained | 7.0 s, 4.3 GB retained, oldest 250 runs cut off |
| `--test` lookup, one pass, one run held at a time (after) | 2.3 s, ~4 MB retained | 5.7 s, ~4 MB retained, every run read |

"Retained" is the managed heap held while the read's sessions are alive, logged by the benchmark's
setup for the old lookup. The new lookup holds a single run, about 1/50 of one assembly's history.
It still deserializes every run once. What changes is how many runs it holds at once.

## Add a benchmark

1. Put it in the class for its area, or add a class named `<Area>Benchmarks`.
2. Add XML documentation that says what it measures.
3. Use a realistic payload: real test names, error messages and stack traces for failing tests.
4. Keep it local: `Mode = LocalOnly` and a temporary `LocalStorePath`, as the adapter benchmarks do.
5. Add it to the table above.
6. Run it on `main` and on your branch, and put both results in the PR.

## References

- [BenchmarkDotNet documentation](https://benchmarkdotnet.org/)
- [Performance](https://docs.xping.io/performance/performance.html)
