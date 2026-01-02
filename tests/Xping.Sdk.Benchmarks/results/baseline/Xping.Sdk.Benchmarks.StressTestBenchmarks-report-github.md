```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.5 (24F74) [Darwin 24.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                | Mean       | Error       | StdDev   | Gen0    | Completed Work Items | Lock Contentions | Gen1   | Allocated  |
|-------------------------------------- |-----------:|------------:|---------:|--------:|---------------------:|-----------------:|-------:|-----------:|
| SustainedLoad_1000Tests               |   297.2 μs |   280.00 μs | 15.35 μs |  5.3711 |              18.0200 |                - | 0.4883 |  541.72 KB |
| HighVolumeLoad_5000Tests              | 1,507.3 μs | 1,266.57 μs | 69.42 μs | 25.3906 |               5.1133 |                - | 3.9063 | 2746.41 KB |
| BurstLoad_2000Tests                   |   598.2 μs |    57.09 μs |  3.13 μs |  9.7656 |              30.8184 |           0.0107 | 0.9766 | 1055.52 KB |
| MixedWorkload_1500Tests               |   481.4 μs |    55.04 μs |  3.02 μs |  9.7656 |              18.4873 |                - | 0.9766 |  986.91 KB |
| MemoryStability_10Cycles_100TestsEach |   335.5 μs |    38.35 μs |  2.10 μs |  5.8594 |              19.9023 |                - | 2.9297 |  639.71 KB |
| ContinuousPressure_3000Tests          |   899.7 μs |    32.34 μs |  1.77 μs | 15.6250 |              40.7188 |                - |      - | 1698.32 KB |
