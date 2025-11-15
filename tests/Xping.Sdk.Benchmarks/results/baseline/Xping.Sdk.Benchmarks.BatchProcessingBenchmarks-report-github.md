```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.5 (24F74) [Darwin 24.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                         | Mean       | Error      | StdDev    | Gen0   | Completed Work Items | Lock Contentions | Gen1   | Allocated |
|------------------------------- |-----------:|-----------:|----------:|-------:|---------------------:|-----------------:|-------:|----------:|
| SmallBatch_10Tests             |   3.641 μs |  0.7625 μs | 0.0418 μs | 0.0687 |               1.0297 |                - | 0.0153 |   7.63 KB |
| MediumBatch_50Tests            |  16.511 μs |  2.5953 μs | 0.1423 μs | 0.3052 |               1.8161 |           0.0000 | 0.1526 |  31.63 KB |
| LargeBatch_100Tests            |  32.086 μs | 17.7046 μs | 0.9705 μs | 0.6104 |               1.9805 |                - | 0.3052 |  60.95 KB |
| ExtraLargeBatch_500Tests       | 156.054 μs | 52.2760 μs | 2.8654 μs | 2.6855 |               1.0369 |                - |      - | 287.42 KB |
| MultipleSmallBatches_5x10Tests |  18.235 μs |  3.9403 μs | 0.2160 μs | 0.3052 |               3.7519 |           0.0001 |      - |  31.37 KB |
| AutoFlush_BatchSize50          |  18.445 μs | 36.3326 μs | 1.9915 μs | 0.2747 |               4.0437 |           0.0002 |      - |  29.67 KB |
| MixedSizeBatches_Variable      |  15.583 μs |  0.7525 μs | 0.0412 μs | 0.2747 |                    - |                - |      - |  29.16 KB |
