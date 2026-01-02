```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.5 (24F74) [Darwin 24.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                             | Mean       | Error      | StdDev    | Gen0   | Completed Work Items | Lock Contentions | Gen1   | Allocated |
|----------------------------------- |-----------:|-----------:|----------:|-------:|---------------------:|-----------------:|-------:|----------:|
| MinimalTestRecording               |   745.0 ns |   857.8 ns |  47.02 ns | 0.0010 |               1.0090 |           0.0013 | 0.0010 |     827 B |
| TestRecording_WithCategories       |   669.8 ns |   874.4 ns |  47.93 ns | 0.0010 |               1.0080 |           0.0041 | 0.0010 |     995 B |
| BatchRecording_10Tests             | 7,151.2 ns | 3,397.4 ns | 186.22 ns | 0.0153 |               6.3385 |           0.0070 |      - |    8890 B |
| DataRowTestRecording               | 1,754.1 ns | 2,435.0 ns | 133.47 ns | 0.0038 |               0.7923 |           0.0007 |      - |    2513 B |
| FailedTestRecording_WithException  |   707.1 ns |   725.9 ns |  39.79 ns |      - |               0.7710 |           0.0006 |      - |     735 B |
| IgnoredTestRecording               |   746.0 ns |   120.7 ns |   6.62 ns | 0.0010 |               1.0096 |           0.0015 | 0.0010 |     831 B |
| TestRecording_WithCustomProperties |   687.5 ns |   763.2 ns |  41.83 ns | 0.0010 |               0.8016 |           0.0008 |      - |    1046 B |
