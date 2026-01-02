```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.5 (24F74) [Darwin 24.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                     | Mean        | Error       | StdDev    | Gen0   | Completed Work Items | Lock Contentions | Gen1   | Allocated |
|--------------------------- |------------:|------------:|----------:|-------:|---------------------:|-----------------:|-------:|----------:|
| SingleTestEndToEnd         |    339.6 ns |   438.35 ns |  24.03 ns | 0.0062 |                    - |                - |      - |     648 B |
| TenPassingTests            |  2,895.8 ns |   133.95 ns |   7.34 ns | 0.0572 |                    - |                - |      - |    5984 B |
| MixedOutcomeTests          |  5,780.1 ns |   130.84 ns |   7.17 ns | 0.1068 |                    - |                - |      - |   11584 B |
| FailedTestsWithStackTraces |  2,892.6 ns |    49.34 ns |   2.70 ns | 0.0572 |                    - |                - |      - |    5904 B |
| RapidTestExecution         | 28,598.8 ns | 7,774.70 ns | 426.16 ns | 0.5493 |                    - |                - | 0.0305 |   56016 B |
