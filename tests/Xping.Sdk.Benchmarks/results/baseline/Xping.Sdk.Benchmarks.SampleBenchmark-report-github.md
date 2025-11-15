```

BenchmarkDotNet v0.14.0, macOS Sequoia 15.5 (24F74) [Darwin 24.5.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.203
  [Host]   : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD
  ShortRun : .NET 9.0.4 (9.0.425.16305), Arm64 RyuJIT AdvSIMD

WarmupCount=3  

```
| Method           | Job      | Toolchain              | IterationCount | LaunchCount | Mean       | Error      | StdDev    | Ratio  | RatioSD | Completed Work Items | Lock Contentions | Allocated | Alloc Ratio |
|----------------- |--------- |----------------------- |--------------- |------------ |-----------:|-----------:|----------:|-------:|--------:|---------------------:|-----------------:|----------:|------------:|
| SimpleOperation  | .NET 9.0 | InProcessEmitToolchain | 10             | Default     |   1.158 ns |  0.0111 ns | 0.0066 ns |   1.00 |    0.01 |                    - |                - |         - |          NA |
| ComplexOperation | .NET 9.0 | InProcessEmitToolchain | 10             | Default     | 231.127 ns |  1.0022 ns | 0.5964 ns | 199.63 |    1.19 |                    - |                - |         - |          NA |
|                  |          |                        |                |             |            |            |           |        |         |                      |                  |           |             |
| SimpleOperation  | ShortRun | Default                | 3              | 1           |   1.264 ns |  1.6267 ns | 0.0892 ns |   1.00 |    0.09 |                    - |                - |         - |          NA |
| ComplexOperation | ShortRun | Default                | 3              | 1           | 236.795 ns | 43.0387 ns | 2.3591 ns | 187.97 |   11.28 |                    - |                - |         - |          NA |
