```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.5 (24F74) [Darwin 24.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                     | Mean          | Error       | StdDev     | Gen0   | Completed Work Items | Lock Contentions | Allocated |
|--------------------------- |--------------:|------------:|-----------:|-------:|---------------------:|-----------------:|----------:|
| NUnitPatternWithCategories |      6.812 μs |   2.8067 μs |  0.1538 μs | 0.1755 |                    - |                - |  18.07 KB |
| XUnitTheoryPattern         |      5.349 μs |   0.2954 μs |  0.0162 μs | 0.1068 |                    - |                - |   11.2 KB |
| MSTestPatternWithContext   |      5.613 μs |   1.5681 μs |  0.0860 μs | 0.1373 |                    - |                - |  13.81 KB |
| ParallelAdapterExecution   | 11,031.292 μs | 480.2421 μs | 26.3237 μs |      - |               6.0000 |                - |  18.83 KB |
| TestsWithRetryMetadata     |      2.928 μs |   0.7216 μs |  0.0396 μs | 0.0610 |                    - |                - |   6.09 KB |
| TestsWithSessionContext    |      6.097 μs |   0.4054 μs |  0.0222 μs | 0.1144 |                    - |                - |  11.73 KB |
