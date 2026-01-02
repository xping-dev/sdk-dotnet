```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.5 (24F74) [Darwin 24.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                       | Mean         | Error        | StdDev       | Completed Work Items | Lock Contentions | Gen0   | Gen1   | Gen2   | Allocated |
|--------------------------------------------- |-------------:|-------------:|-------------:|---------------------:|-----------------:|-------:|-------:|-------:|----------:|
| &#39;Record single test (overhead measurement)&#39;  |     308.9 ns |   1,601.4 ns |     87.78 ns |               0.4588 |           0.0035 | 0.0002 | 0.0001 |      - |     335 B |
| &#39;Record 100 tests sequentially&#39;              |  46,215.9 ns | 389,352.3 ns | 21,341.71 ns |              42.9023 |           0.3309 | 0.0153 | 0.0076 |      - |   34850 B |
| &#39;Record 1000 tests concurrently (4 threads)&#39; | 128,039.5 ns |  69,146.2 ns |  3,790.14 ns |              60.5691 |           0.1685 | 0.2441 | 0.2441 | 0.2441 |   45465 B |
| &#39;Create TestExecution object&#39;                |     453.0 ns |     161.4 ns |      8.85 ns |                    - |                - | 0.0048 |      - |      - |     528 B |
| &#39;Sampling with 100% rate&#39;                    |           NA |           NA |           NA |                   NA |               NA |     NA |     NA |     NA |        NA |

Benchmarks with issues:
  CollectorBenchmarks.'Sampling with 100% rate': ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3)
