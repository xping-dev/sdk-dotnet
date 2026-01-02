```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.5 (24F74) [Darwin 24.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean       | Error      | StdDev    | Gen0   | Completed Work Items | Lock Contentions | Allocated |
|----------------------------------- |-----------:|-----------:|----------:|-------:|---------------------:|-----------------:|----------:|
| MinimalTestRecording               |   779.1 ns | 1,107.0 ns |  60.68 ns | 0.0010 |               0.7139 |           0.0006 |     712 B |
| TestRecording_WithCategories       |   711.5 ns |   705.8 ns |  38.69 ns | 0.0010 |               0.8010 |           0.0006 |     925 B |
| BatchRecording_10Tests             | 6,379.3 ns | 8,809.1 ns | 482.86 ns | 0.0153 |               7.5740 |           0.0059 |    9376 B |
| ParameterizedTestRecording         | 2,263.1 ns | 2,832.6 ns | 155.26 ns | 0.0038 |               1.3431 |           0.0005 |    2699 B |
| FailedTestRecording_WithException  |   705.2 ns |   585.2 ns |  32.08 ns | 0.0010 |               0.9264 |           0.0010 |     798 B |
| SkippedTestRecording               |   729.2 ns |   951.6 ns |  52.16 ns | 0.0010 |               0.8194 |           0.0012 |     756 B |
| TestRecording_WithCustomAttributes |   691.7 ns |   915.9 ns |  50.21 ns | 0.0010 |               0.5484 |           0.0005 |     942 B |
