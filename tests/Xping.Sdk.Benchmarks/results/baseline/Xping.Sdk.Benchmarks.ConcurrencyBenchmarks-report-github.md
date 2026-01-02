```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.5 (24F74) [Darwin 24.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                        | Mean        | Error       | StdDev    | Gen0   | Completed Work Items | Lock Contentions | Allocated |
|---------------------------------------------- |------------:|------------:|----------:|-------:|---------------------:|-----------------:|----------:|
| LowParallelism_4Threads_100TestsEach          |    144.7 μs |   114.51 μs |   6.28 μs | 2.1973 |              28.0435 |           0.0012 | 233.79 KB |
| MediumParallelism_8Threads_100TestsEach       |    261.4 μs |    39.61 μs |   2.17 μs | 4.3945 |              26.6772 |           0.0015 | 461.26 KB |
| HighParallelism_16Threads_100TestsEach        |    515.9 μs |    73.11 μs |   4.01 μs | 8.7891 |              32.0156 |           0.0010 | 901.53 KB |
| ExtremeParallelism_32Threads_50TestsEach      |    519.1 μs |    44.32 μs |   2.43 μs | 8.7891 |              52.1016 |           0.0049 | 916.06 KB |
| ContendedFlush_8Threads_FlushAfterEach10Tests |    133.0 μs |     7.43 μs |   0.41 μs | 2.1973 |               8.0066 |                - | 245.84 KB |
| MixedOperations_RecordAndFlush_12Threads      | 56,777.6 μs | 4,295.37 μs | 235.44 μs |      - |             866.1111 |                - | 497.49 KB |
| RapidShortBursts_20Threads_20TestsEach        |    154.7 μs |   173.27 μs |   9.50 μs | 2.1973 |              44.1882 |           0.0005 | 244.83 KB |
