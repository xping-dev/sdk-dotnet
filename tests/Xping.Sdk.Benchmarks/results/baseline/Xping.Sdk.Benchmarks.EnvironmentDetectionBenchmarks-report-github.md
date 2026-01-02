```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.5 (24F74) [Darwin 24.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                         | Mean        | Error      | StdDev    | Ratio | RatioSD | Completed Work Items | Lock Contentions | Gen0   | Allocated | Alloc Ratio |
|------------------------------- |------------:|-----------:|----------:|------:|--------:|---------------------:|-----------------:|-------:|----------:|------------:|
| &#39;Detect OS information&#39;        |   1.4668 ns |  0.0551 ns | 0.0030 ns |     ? |       ? |                    - |                - |      - |         - |           ? |
| &#39;Detect runtime version&#39;       |  13.6899 ns |  3.7803 ns | 0.2072 ns |     ? |       ? |                    - |                - | 0.0006 |      64 B |           ? |
| &#39;Detect CI environment&#39;        | 588.8486 ns | 32.1803 ns | 1.7639 ns |     ? |       ? |                    - |                - |      - |         - |           ? |
| &#39;Collect all environment info&#39; | 427.6316 ns | 74.4067 ns | 4.0785 ns |     ? |       ? |                    - |                - | 0.0019 |     232 B |           ? |
| &#39;Get processor count&#39;          |   0.0000 ns |  0.0000 ns | 0.0000 ns |     ? |       ? |                    - |                - |      - |         - |           ? |
