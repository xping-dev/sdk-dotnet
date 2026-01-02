```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.5 (24F74) [Darwin 24.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                         | Mean       | Error       | StdDev    | Gen0     | Completed Work Items | Lock Contentions | Gen1    | Allocated |
|----------------------------------------------- |-----------:|------------:|----------:|---------:|---------------------:|-----------------:|--------:|----------:|
| LargeBatch_10000Tests                          | 2,938.7 μs |   376.92 μs |  20.66 μs |  50.7813 |              12.8398 |                - |  7.8125 |   5.07 MB |
| VeryLargeBatch_25000Tests                      | 7,453.5 μs | 2,994.48 μs | 164.14 μs | 132.8125 |              17.8516 |                - | 39.0625 |  13.15 MB |
| ComplexObjects_1000TestsWithLargeData          |   853.5 μs |    27.18 μs |   1.49 μs |  21.4844 |              10.5117 |                - |       - |   2.24 MB |
| RepeatedCycles_50Cycles_200TestsEach           | 3,293.6 μs |   250.61 μs |  13.74 μs |  54.6875 |              98.9688 |                - | 23.4375 |   5.66 MB |
| AllocationStress_5000RapidTests                | 1,532.2 μs |    19.01 μs |   1.04 μs |  27.3438 |             177.7949 |           0.0117 |  3.9063 |   2.71 MB |
| StringHeavyWorkload_2000Tests                  |   910.4 μs |    21.78 μs |   1.19 μs |  70.3125 |               8.4902 |                - |  5.8594 |   6.95 MB |
| ConcurrentMemoryPressure_8Threads_500TestsEach | 1,477.6 μs |   203.76 μs |  11.17 μs |  31.2500 |              35.6680 |           0.0039 |  3.9063 |    3.1 MB |
