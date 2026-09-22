<!--
  This README is specifically designed for NuGet package display.
  It uses absolute URLs and formatting optimized for NuGet.org rendering.
  For the GitHub repository README, see /README.md in the root directory.
-->

# Xping CLI

**Find out which of your tests you can trust — with no account and no network access.**

`xping` reads the local run store written by the [Xping SDK](https://www.nuget.org/packages/Xping.Sdk.Core/) and reports which of your tests are unreliable, based on your own recent test runs.

## Install

> **Requires the .NET 10 SDK or runtime.** The tool targets `net10.0` and will not launch on
> older runtimes. The Xping SDK packages themselves target `netstandard2.0` and are unaffected —
> your test projects can stay on any supported .NET version.

```bash
dotnet tool install -g Xping.Cli
```

This puts an `xping` command on your `PATH`. To pin the version alongside a repo instead, install it into a tool manifest (`dotnet new tool-manifest && dotnet tool install Xping.Cli`) and invoke it as `dotnet xping`.

## Use

Add an Xping SDK adapter to your test project, run `dotnet test` a few times, then:

```bash
xping report
```

<!-- xping:sample docs-xunit -->
````
Xping · MyTestProject · 9 runs · 2026-08-20 07:52 → 09:02 · main@bdbafba
17 tests · 15 healthy · 2 flagged

```
NEEDS ATTENTION (2 high)                               most severe first
────────────────────────────────────────────────────────────────────────

1.  HIGH  flaky
    SampleTests.FlakyTest_PassesOnRetry
    failed 9 of 18 executions (50%) in 9 of 9 runs, 1 failure mode
    evidence moderate | -env-cluster | f_2b84a621 | ...SampleTests.cs:96

2.  HIGH  always failing
    SampleTests.ThrowingTestIsTracked
    failed 9 of 9 executions (100%), one failure mode:
    System.InvalidOperationException
    evidence low | -env-cluster | f_c1774d82 | .../SampleTests.cs:65
```

rates: the marker on each finding says which runs its percentage was
counted out of; compare two only where the markers match.
https://docs.xping.io/cli/command-reference.html#the-population-marker
````

The two lines above the fence answer what was analysed and what state the suite is in; `tests` is `healthy` plus `flagged`. Each finding is numbered, names the test on a line of its own, states what was observed, and ends with a dim trailer carrying the evidence level, which runs the rate was counted over, the finding's id and where the test is declared. The block is fenced so it pastes into Slack or a pull request with its columns intact.

Findings need a few runs of history. What the newest run did that the runs before it did not needs only two, and when it has failures a `LATEST RUN` section opens the block, above the findings:

<!-- xping:sample latest-run latest-run -->
```
LATEST RUN  16:21 · eab9867                               2 new failures
────────────────────────────────────────────────────────────────────────

    new          CartTests.Checkout_AppliesDiscount
                 passed the previous 19 runs, failed just now
                 NullReferenceException | CartTests.cs:112

    new test     CartTests.Checkout_RejectsExpiredCoupon
                 first seen this run, failed
                 EqualException | CartTests.cs:140

    Also failing: 5 tests explained by findings below (#1, #2, #3).
```

## Commands

| Command | Description |
|---------|-------------|
| `xping report` | Report flakiness from recent local runs |
| `xping report --all` | Show every finding and every latest-run row, not only the first ten of each |
| `xping report --json` | Emit a versioned JSON document for scripting and CI |
| `xping where` | Show where local runs are stored |
| `xping clear` | Delete recorded runs |

## Privacy

Everything stays on your machine. The CLI makes no network calls and requires no account. Test history lives in a `.xping` folder at your repository root, which hides itself from git automatically.

## Documentation

- [Running Without an Account](https://docs.xping.io/getting-started/local-first.html)
- [CLI Command Reference](https://docs.xping.io/cli/command-reference.html)
- [Local Store](https://docs.xping.io/configuration/local-store.html)

## License

MIT
