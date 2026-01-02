```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.5 (24F74) [Darwin 24.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                                  | Mean         | Error          | StdDev      | Ratio  | RatioSD | Gen0   | Completed Work Items | Lock Contentions | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------- |-------------:|---------------:|------------:|-------:|--------:|-------:|---------------------:|-----------------:|-------:|----------:|------------:|
| &#39;Load configuration from JSON&#39;          | 4,310.449 ns | 15,547.3521 ns | 852.2028 ns | 769.07 |  132.55 | 0.0916 |                    - |                - | 0.0305 |   11075 B |       98.88 |
| &#39;Create configuration programmatically&#39; |     5.638 ns |      0.5581 ns |   0.0306 ns |   1.01 |    0.02 | 0.0011 |                    - |                - |      - |     112 B |        1.00 |
| &#39;Validate configuration&#39;                |     3.347 ns |      0.9331 ns |   0.0511 ns |   0.60 |    0.01 |      - |                    - |                - |      - |         - |        0.00 |
| &#39;Instantiate default configuration&#39;     |     5.607 ns |      2.3308 ns |   0.1278 ns |   1.00 |    0.03 | 0.0011 |                    - |                - |      - |     112 B |        1.00 |
