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
dotnet new tool-manifest        # if your repo has no manifest yet
dotnet tool install Xping.Cli
```

Then run it as `dotnet xping`. To install for your user account instead, use `dotnet tool install -g Xping.Cli` and run it as `xping`.

## Use

Add an Xping SDK adapter to your test project, run `dotnet test` a few times, then:

```bash
dotnet xping report
```

```
──────────────────────────────────────────────────────────────────────────
  Xping · local run summary                             412 tests · 38.2s
──────────────────────────────────────────────────────────────────────────
  ✓ 405 passed     ✗ 4 failed     ○ 3 skipped

  ⚠  2 unstable tests · last 12 local runs

     ●●○●●●○●●●●○   Checkout.AppliesDiscount_WhenCouponValid         9/12
                    passed 9 of 12 runs · inconsistent

     ●●●●●●●●●●●○   Db.MigratesSchema_OnStartup                     11/12
                    newly failing · first failure in this window

  ✗  1 test failed in all 12 runs - not flaky, likely real bugs
     Auth.RejectsExpiredToken
──────────────────────────────────────────────────────────────────────────
```

The sparkline reads left to right, oldest run to newest — `●` passed, `○` failed. A test that flips between the two is flaky; one that has never passed is a real bug, and is listed separately.

## Commands

| Command | Description |
|---------|-------------|
| `xping report` | Report flakiness from recent local runs |
| `xping report --all` | Report across every test assembly in the solution |
| `xping report --json` | Emit a versioned JSON document for scripting and CI |
| `xping report --details` | Print per-test run history |
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
