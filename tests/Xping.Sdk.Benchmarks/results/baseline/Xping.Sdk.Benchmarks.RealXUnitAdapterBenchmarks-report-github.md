```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.5 (24F74) [Darwin 24.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                            | Mean       | Error      | StdDev    | Gen0   | Completed Work Items | Lock Contentions | Gen1   | Allocated |
|---------------------------------- |-----------:|-----------:|----------:|-------:|---------------------:|-----------------:|-------:|----------:|
| MinimalTestRecording              |   722.0 ns | 1,977.3 ns | 108.38 ns | 0.0019 |               0.8000 |           0.0009 |      - |     746 B |
| TestRecording_WithTraits          |   802.2 ns | 1,395.5 ns |  76.49 ns | 0.0010 |               1.0099 |           0.0010 | 0.0010 |    1009 B |
| BatchRecording_10Tests            | 6,711.6 ns | 4,325.4 ns | 237.09 ns | 0.0153 |               6.3640 |           0.0046 |      - |    8885 B |
| TheoryTestRecording               | 2,087.0 ns | 6,489.7 ns | 355.72 ns | 0.0038 |               0.9005 |           0.0012 |      - |    2730 B |
| FailedTestRecording_WithException |   801.5 ns | 2,652.2 ns | 145.38 ns |      - |               0.8572 |           0.0007 |      - |     768 B |
| SkippedTestRecording              |   775.4 ns | 1,068.5 ns |  58.57 ns | 0.0010 |               1.0100 |           0.0015 | 0.0010 |     850 B |
| TestRecording_WithFixture         |   712.4 ns |   197.4 ns |  10.82 ns | 0.0010 |               0.8939 |           0.0011 |      - |    1080 B |
