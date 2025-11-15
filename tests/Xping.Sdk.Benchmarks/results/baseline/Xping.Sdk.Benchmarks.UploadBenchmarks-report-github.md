```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.5 (24F74) [Darwin 24.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                        | Mean       | Error       | StdDev    | Completed Work Items | Lock Contentions | Gen0   | Gen1   | Gen2   | Allocated |
|---------------------------------------------- |-----------:|------------:|----------:|---------------------:|-----------------:|-------:|-------:|-------:|----------:|
| &#39;Serialize 10 test executions to JSON&#39;        |   3.585 μs |   0.4497 μs | 0.0247 μs |                    - |                - | 0.0916 |      - |      - |   9.07 KB |
| &#39;Serialize 100 test executions to JSON&#39;       |  41.416 μs |  19.3832 μs | 1.0625 μs |                    - |                - | 5.0659 | 5.0659 | 5.0659 |  88.32 KB |
| &#39;Serialize 1000 test executions to JSON&#39;      | 440.840 μs | 143.3533 μs | 7.8577 μs |                    - |                - | 9.2773 | 9.2773 | 9.2773 | 886.18 KB |
| &#39;Memory allocation for serializing 100 tests&#39; |  33.080 μs |   1.7655 μs | 0.0968 μs |                    - |                - | 0.4272 |      - |      - |  44.32 KB |
| &#39;Calculate payload size for 100 tests&#39;        |  41.944 μs |   2.2138 μs | 0.1213 μs |                    - |                - | 5.0659 | 5.0659 | 5.0659 |  88.32 KB |
