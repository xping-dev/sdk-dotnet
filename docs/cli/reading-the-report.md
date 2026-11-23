---
uid: cli-reading-the-report
title: Reading the report
---

# Reading the report

`xping report` reads the runs in the [local store](../configuration/local-store.md) and prints what
it observed in them. This page explains each part of that output. For the commands and options, see
the [command reference](command-reference.md). For the lines Xping Cloud adds, see
[Xping Cloud data in the CLI](cloud-data.md).

The report describes what happened on this machine. It does not score a test, and it does not say
why a test failed. Each [finding](#finding-kinds) is an observation with a count behind it.

---

## The report

A report covers one test assembly. The findings are inside a fenced code block, so you can paste the
report into a chat, a pull request or a ticket and keep the columns:

<!-- xping:sample docs-checkout -->
````
Xping · Checkout.Tests · 20 runs · 2026-08-05 → 2026-08-19 · main@a3f9c2e
412 tests · 409 healthy · 3 flagged · 41 awaiting more runs · 6 not significant
◷ nothing to measure: concurrency 108 · slower 32

```
NEEDS ATTENTION (1 high, 1 medium, 1 low)              most severe first
────────────────────────────────────────────────────────────────────────

1.  HIGH  flaky
    SummaryTests.GenerateMonthlySummary
    failed 7 of 20 executions (35%) in 5 of 20 runs, 3 failure modes
    evidence adequate | -env-cluster | f_2a91c0de | ...ummaryTests.cs:88

2.  MED   slower
    FlowTests.CheckoutFlow_Completes
    3.51x slower (95% CI 1.94-5.87x), 340ms -> 1.2s on the clock
    evidence robust | -env | f_8c04b71a | .../Checkout/FlowTests.cs:214

3.  LOW   stopped running
    LegacyImport.Roundtrip
    ran in 12 of 17 earlier runs, absent from the last 3
    evidence adequate | -partial | f_1d77e3f5 | .../ImportTests.cs:41
```

rates: the marker on each finding says which runs its percentage was
counted out of; compare two only where the markers match.
https://docs.xping.io/cli/reading-the-report.html#the-population-marker

Detail of row 1: xping report --id f_2a91c0de --assembly Checkout.Tests
````

### The lines above the fence

- **Line 1** says what the report read: the assembly, the number of runs, the dates, and the branch
  and commit of the newest run.
- **Line 2** counts tests. `tests` is `healthy` plus `flagged`. `flagged` counts tests, not
  findings, so the numbers add up. When the newest run failed tests that no finding names,
  `N failed in latest run` follows `flagged`, and `tests` is the sum of all three.
- `healthy` means "no finding and no failure in the newest run". It does not mean that every check
  ran on the test.
- `awaiting more runs` counts possible findings that need more history. `not significant` counts
  possible findings that did not pass their statistical test.
- **Line 3**, when it is there, lists the kinds that could not be measured on some tests at all, with
  the number of tests for each. Those tests are inside `healthy`. `summary.notMeasured` in
  [`--format json`](command-reference.md#--format-json) gives the reason for each kind.

### The rows

`NEEDS ATTENTION` heads the list. It gives the severity counts for every finding the report found,
even when `--top` shows fewer rows. The note on the right says how the rows are ordered.

Each row has four lines:

1. The row number, the severity (`HIGH`, `MED` or `LOW`) and the [finding kind](#finding-kinds).
2. The test name, on its own line, so it is never cut. You can paste it after
   `dotnet test --filter FullyQualifiedName~`.
3. What the report observed.
4. A dim trailer: the [evidence level](#evidence), the [population marker](#the-population-marker),
   the [finding id](#finding-ids) and the [source location](#source-locations). The trailer's parts
   are always joined with ` | `.

Rows are ordered most severe first. One exception: when a test has a second finding, that finding
moves up to sit under the first one, and its header line says `same test as #1`.

A finding about several tests (a `broken fixture` or a `shared failure`) names the lifecycle member
or the failure signature the tests have in common. It lists up to three of the tests, then a count:

<!-- xping:sample docs-cluster rows -->
```
1.  HIGH  broken fixture
    UnprovisionedDatabase..ctor (3 tests)
      FixtureTests.FirstTestNeedingTheDatabase
      FixtureTests.SecondTestNeedingTheDatabase
      FixtureTests.ThirdTestNeedingTheDatabase
    UnprovisionedDatabase..ctor failed, blocking 3 tests in 20 of 20
    runs
    evidence robust | all runs | f_7c905f05
```

### The lines below the fence

- **The legend** (`rates: …`) follows the fence when the report printed a finding. It links to
  [the population marker](#the-population-marker).
- **The truncation line** follows when rows were left out:
  `Showing 10 of 21 findings · all: xping report --all --assembly Checkout.Tests`. It also counts the
  [latest run](#the-latest-run) rows that were left out:
  `Showing 10 of 21 findings, 10 of 23 failures · all: …`. `--all` shows both.
- **The detail command** follows when the report printed a finding and was not narrowed by
  `--kind`: `Detail of row 1: xping report --id f_2a91c0de --assembly Checkout.Tests`. It repeats
  any `--runs`, `--since` or `--directory` you gave, so it runs as printed. Use the id from another
  row to open that row instead.

At a terminal, the CLI can print one line about Xping Cloud on stderr after the report. See
[the Cloud invitation](command-reference.md#the-cloud-invitation).

### When there are no findings

The block says why it is empty:

- `✓ No findings.` The report had enough history and found nothing.
- `◷ Nothing reportable yet: 41 need more runs, 6 could be chance.` The report withheld possible
  findings. The line names each reason: tests that need more runs, possible findings that did not
  pass their test, and kinds that had nothing to measure.

`No findings.` is not printed when the [latest run](#the-latest-run) section lists failures.

### Output that you copy

- Nothing inside the fence is wider than 72 columns. A single identifier that is longer is printed
  whole, so you can still search for it.
- When stdout is not a terminal, the report uses ASCII and no colour, and has no blank lines around
  the block. `xping report | pbcopy` copies the report and nothing else. `--ascii` and `--no-color`
  do the same at a terminal.
- In ASCII, `·` is printed as `|`, `→` as `->`, `✓` as `+` and `✗` as `x`. Inside a `cloud:` line,
  `·` is printed as `-`, `▼` as `v` and `≠` as `!=`.
- Recorded text, such as test names and failure messages, is printed with every control, bidi and
  invisible formatting character replaced by `?`. A recorded escape sequence cannot change your
  terminal. In ASCII output, every other non-ASCII character is replaced too, except inside a printed
  command. `--format json` keeps the recorded values unchanged.

```bash
xping report | pbcopy          # macOS
xping report | clip            # Windows
xping report | wl-copy         # Linux (Wayland)
```

---

## How much history the report needs

| Question | Runs needed |
|---|---|
| Any finding | 5 runs in the window, and 5 runs that contain the test |
| A [latest run](#the-latest-run) row that says what changed | 2 runs: the newest and one before it |
| `deeper retries` | 5 earlier runs in which the test settled green |
| `stopped running` | 8 runs in the window |

A possible finding without enough history is counted as `awaiting more runs` on line 2. It is not
reported until the history is long enough.

The default window is the last 20 runs, cut to the runs from the last 14 days. When no run is that
recent, the window is the last 20 runs. `--runs` and `--since` change the window.

A run with a start time more than a day after your clock is left out of the window. One fast clock,
such as a container or a CI runner with the wrong time, would otherwise date the whole report. The
report counts such runs in `summary.skewedSessions`, says so above the report, and names them on
stderr. When every run in the store is ahead of your clock, none is left out, and the report says
that it is dated by that clock.

---

## The latest run

Findings describe what keeps going wrong over many runs. The `LATEST RUN` section describes the
newest run. When the newest run has failures, the section opens the fence, above the findings:

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

The heading gives the run's start time, its commit when one was recorded, and the number of new
failures. Each row is one test that failed the run. The second line compares the run with the runs
before it:

| Status | Meaning |
|---|---|
| `new` | The test passed in every earlier run it was in |
| `new test` | The window has no earlier run of the test |
| `seen before` | The test failed before. The row gives the count, such as `failed 2 of 4 runs` |

The third line is the exception type and the source location. A row has no evidence level, id or
population marker, because it describes one run.

- These rows are not findings. They have no severity, and they never change the
  [`--fail-on`](command-reference.md#xping-report) exit code.
- A test that a finding already names is not listed again. The closing line counts those tests and
  gives the findings' row numbers.
- A test that failed and then passed on a retry is not listed. The run did not fail on it.
- Runs that look environmental (see [the population marker](#the-population-marker)) are left out of
  a test's history here too.
- `--kind` changes only the closing line. A test whose only finding `--kind` filtered out is listed
  as a row.
- When the newest run had no failures, the section is not shown.
- On a store with one run, every row is a `new test`.
- When most of the run failed, the section is one line, such as
  `Looks environmental: 187 of 210 tests failed. Not itemised.`
- At most ten rows are shown. `Showing 10 of 23` closes a longer list, and `--all` shows every row.
  `--top` does not apply here.

---

## Evidence

The first part of a finding's trailer is its evidence level. It says how many runs the finding's
claim was computed from:

| Level | Runs behind the finding |
|---|---|
| `sparse` | Fewer than 8 |
| `adequate` | 8 to 15 |
| `robust` | More than 15 |

The level counts the runs behind the finding, not every run of the test. A `time sensitive` finding
reads only the runs that recorded a clock. A `slower` finding reads only the runs whose durations
could be compared. So one test can have two findings with two different levels. `evidenceSessions`
in the JSON gives the exact number.

Xping Cloud uses the same three words for its own evidence level, with its own cut-offs (an
effective sample size below 15 is `sparse`, and 40 or more is `robust`). A finding can be
`evidence robust` locally and `cloud evidence adequate` at the same time.

---

## The population marker

The second part of a finding's trailer says which runs the finding's rate was counted out of. Every
finding has one.

| Marker | The rate is over |
|---|---|
| `all runs` | Every execution of the test in the window |
| `-env` | Every execution, except those from environmental runs |
| `-env-cluster` | Every execution, except those from environmental runs, and except failures that a `shared failure` or `broken fixture` finding already reports |
| `-partial` | Every run that covered the whole suite. Runs that covered part of it are set aside |

- **An environmental run** is one run in which at least 10 tests failed, and those tests were at
  least 30% of the tests it ran. Both limits apply. A five-test suite with two failures is not
  environmental, and neither is a thousand-test suite with ten failures.
- **A clustered failure** is a failure whose signature at least three tests share in one run. The
  report publishes it once, as a `shared failure` or `broken fixture` finding, and does not count it
  again against each test.
- **A partial run** covered part of the suite, as `dotnet test --filter` does. On xUnit, the SDK
  records it directly: the run selected fewer test cases than it discovered. Elsewhere, a run whose
  distinct tests are fewer than half of the largest run in the window counts as partial. Only
  `stopped running` sets partial runs aside. A partial run's results are as valid as any other run's,
  but the tests it did not select tell you nothing.

### Compare rates only where the markers match

A test ran 20 times. A shared failure caught it 10 times, and it failed twice on its own. Its
`-env-cluster` finding reports:

```
failed 2 of 12 executions (16.7%)
```

Over every run, the rate is `2 of 22 (9.1%)`. Both numbers are correct, but they answer different
questions. So a `-env-cluster` finding at 16.7% next to an `all runs` finding at 12% is not the
milder of the two.

### The marker states the rule, not whether it removed anything

A `-env-cluster` finding in a window with no environmental runs and no clusters has nothing removed.
To see what a rule removed, read the counts in the JSON evidence:

```bash
xping report --assembly Checkout.Tests --all --format json \
  | jq '.findings[] | select(.population | startswith("excludesEnvironmental"))
        | {kind, population, evidence: (.evidence | {
            executionsConsidered, discountedEnvironmental, discountedClustered })}'
```

The three numbers add up to the number of times the test ran in the window.
`discountedEnvironmental` and `discountedClustered` are separate, so you can see which rule
narrowed a rate. An `all runs` finding has no discount counts.

`excludesPartialRuns` counts runs, not executions. Read it with its own query:

```bash
xping report --assembly Checkout.Tests --all --format json \
  | jq '.findings[] | select(.population == "excludesPartialRuns")
        | {kind, population, evidence: (.evidence | {
            baselineSessions, baselineSessionCount, currentSessionCount,
            partialSessionsSetAside })}'
```

Kinds that count runs name their counts that way. `out of retries` and `deeper retries` publish
`discountedEnvironmentalRuns`. `time sensitive` publishes that and `runsWithoutClock`, the runs that
recorded no clock. `stopped running` publishes `partialSessionsSetAside`.

Each kind uses the population that fits its question. `shared failure` keeps environmental runs,
because that is where shared failures show. `stopped running` keeps them too, because the test was
either in such a run or not.

---

## Finding kinds

Every value `--kind` accepts. The second column is how the report prints the kind.

| Kind | Printed as | What the report observed |
|---|---|---|
| `RetryMasked` | masked by retry | The test failed and then passed on a retry, so the run's result hid the failure |
| `RetryDeepening` | deeper retries | The test needs more attempts to pass in recent runs than in earlier ones |
| `RetryExhausted` | out of retries | The retries ran out and the test still failed the run |
| `Flaky` | flaky | The test both passes and fails, or fails in different ways |
| `AlwaysFailing` | always failing | The test fails almost always, mostly in one way |
| `TimingOut` | timing out | The test mostly times out rather than failing |
| `BrokenFixture` | broken fixture | Several tests failed in the same lifecycle member, and the finding names that member |
| `SharedFailure` | shared failure | Three or more tests failed with the same failure signature in one run |
| `DurationRegression` | slower | The test's recent runs are slower than its earlier runs, by more than its usual variation |
| `DurationUnstable` | unstable timing | The test's duration varies too much to predict |
| `ParallelSensitive` | concurrency | The test's failure rate goes up or down with the number of tests running alongside it |
| `TimeSensitive` | time sensitive | The test's failures gather at one local time of day, at weekends or on weekdays, or on one side of a UTC offset change |
| `Vanished` | stopped running | The test ran in most earlier runs and is absent from the recent ones |

### Retry kinds

A test gets at most one of the three retry kinds. `out of retries` comes first, then
`deeper retries`, then `masked by retry`.

- The report reads only the attempts the adapter recorded. A run is out of retries when its last
  recorded attempt failed and an earlier attempt exists.
- The retry limit in the attribute is published as `maxRetriesAsDeclared`. Nothing is decided by
  it, because retry attributes disagree on whether the number counts the first attempt.
- `deeper retries` compares the recent runs with the earlier ones. It needs five earlier runs in
  which the test settled green.
- A test can be both `out of retries` and `flaky`. They are two claims about the same failed run,
  with two ids.

### `slower`

The report compares the test's three most recent runs with the runs before them. Each run gives one
duration: the median of the test's attempts in that run, divided by the run's own median. A busy
machine slows every test, so dividing by the run's median removes that effect.

A `slower` finding needs all three:

- the test is at least 1.5 times as slow (50% slower);
- it is at least 100 ms slower;
- the comparison passes its statistical test.

The finding gives the change as a factor with a 95% interval, such as
`3.51x slower (95% CI 1.94-5.87x)`. With three recent runs, the interval is the more useful part.

### `time sensitive`

The report tries three splits:

- the local quarter of the day (six hours each);
- weekend against weekday;
- when the window holds two UTC offsets for one time zone, such as a daylight saving change, one
  side of the change against the other.

Each side is counted in runs. The failing side must cover at least three different local days. The
report tests every split that has enough runs on both sides, and corrects for the number of
different ways it divided the window. The finding publishes the probability and the number of
divisions it was corrected for.

### `concurrency`

The report treats the number of tests running at the same time as a dose. It tests whether the
test's failures follow that number across every level it saw. It needs only that the number varied.

- The per-level rates are over executions. The probability is computed over runs, so a run with
  many retries cannot make a weak trend look strong.
- The finding publishes two figures, and both must pass: the probability, and **tau**, a rank
  correlation that says how strong the trend is. Tau depends on how many executions share a level,
  so you cannot compare it between suites with different parallel settings.
- The direction can go either way. A test that fails more when it runs almost alone is reported too.

### `broken fixture` and `shared failure`

Both describe three or more tests that failed with the same signature in one run. The report calls
the group a `broken fixture` when every failure in it was recorded in the same lifecycle member, such
as a `[SetUp]`, a `[TestInitialize]` or a class fixture constructor. Otherwise it is a
`shared failure`. Which failures each adapter can place in a member is in
[known limitations](../troubleshooting/known-limitations.md#fixture-failures).

### `stopped running`

The report does not count absences. It asks how likely it is that every one of the test's
appearances falls in the earlier runs and none in the recent ones, if appearing has nothing to do
with when a run happened. This is Fisher's exact test, one-sided. The finding is reported only at
p ≤ 0.05, and it publishes the p-value and the test's earlier run rate.

- With the default three recent runs, the test must have run in about 71% of the earlier runs:
  twelve of seventeen is enough, eight is not. The bar falls towards 63% as the history gets longer.
- With fewer than eight runs in the window, the recent part is one run, and the kind reports
  nothing.
- Partial runs are set aside. The finding's sentence says `full runs`, a `set aside` metric gives
  the number left out, and the marker is `-partial`.

---

## Finding ids

The `f_…` value in a finding's trailer is a hash of the finding kind and the test or group the
finding is about. Nothing else goes into it.

- The id stays the same from run to run while the finding is still reported, even when its numbers
  change. Two reports a week apart show which findings are the same ones.
- Two findings about the same test have different ids when they are different kinds.
- The id changes when the kind changes. A `shared failure` that is later recorded in one fixture
  member becomes a `broken fixture`, with a new id.

### A finding in detail

`xping report --id <id>` shows one finding. The id is accepted in upper or lower case. The detail
view prints the row, every member of a group with its location, the test's full name and source
location, every metric behind the sentence, and the other findings about the same test. For a
finding about one test, it also prints the command that shows the test's
[history](#test-history), and the [repro command](#reproducing-a-finding) when there is one:

<!-- xping:sample detail-test -->
````
Xping · SampleApp.XUnit · 20 runs · 2026-09-05 09:00 → 16:21 · main@eab9867
16 tests · 10 healthy · 6 flagged

```
FINDING                                                       row 5 of 5
────────────────────────────────────────────────────────────────────────

5.  HIGH  flaky                                          same test as #4
    SampleTests.FlakyTest_EnvironmentState_FailsBasedOnSystemState
    failed 6 of 20 executions (30%) in 6 of 20 runs, 1 failure mode
    evidence robust | -env-cluster | f_445c562e | .../SampleTests.cs:83

    test      SampleApp.XUnit.SampleTests.
              FlakyTest_EnvironmentState_FailsBasedOnSystemState
    assembly  SampleApp.XUnit
    source    src/SampleApp.XUnit/SampleTests.cs:83

    failed         6 of 20 executions (30%)
    runs affected  6 of 20
    failure modes  1

    Also about this test: #4 unstable timing (f_66c10ec3)

FAILURE MODES                                             1 failure mode
────────────────────────────────────────────────────────────────────────

1.  6 failed attempts · since its first run in the window
    System.InvalidOperationException
    expected <num> but was <num>
```

rates: the marker on each finding says which runs its percentage was
counted out of; compare two only where the markers match.
https://docs.xping.io/cli/reading-the-report.html#the-population-marker

Showing 1 of 5 findings · all: xping report --all --assembly SampleApp.XUnit
History: xping report --test 83602ae8 --assembly SampleApp.XUnit
````

When the id is not in the report, the command exits `3` and says so on stderr. When the same test or group
has a finding of another kind, the message names it:

```
Finding f_7c905f05 (shared failure) is not reported in this window (Checkout.Tests, 20 runs). Its subject is reported as f_3ea537e4 (broken fixture, row 3).
```

The CLI does not switch to the new id for you, because it is a different claim. A finding that is no
longer reported also exits `3`, so a script can check whether a finding is still there. With
`--format json`, stdout still carries the envelope, with `findings` empty and `selection.reported`
set to `false`.

Without `--assembly`, on a store with several assemblies, the CLI looks for the id in every
assembly:

```
Finding f_7c905f05 (shared failure) is not reported in any window (every assembly in this store). In Checkout.Tests, its subject is reported as f_3ea537e4 (broken fixture, row 3).
```

There is no single report then, so `--format json` leaves stdout empty. The exit code is `3`.

When more than one assembly reports the id, the command exits `4`. stderr says
`Finding f_7c905f05 is reported in 2 assemblies. Name one:`, followed by the `--id` command for each
assembly.

### Failure modes

`FAILURE MODES` lists the different ways the test failed, the most frequent first. A mode is one
exception type, one message and the stack frames the exception passed through. The message is
normalised so that runs group together: values that change between runs, such as numbers and dates,
become tokens like `<num>`, and text outside quotes is lower-cased.

- A `flaky` finding lists every mode among the failures it counted.
- An `always failing` finding lists its most common mode. Its `dominant mode` line says when that
  mode does not cover every failure.
- A `broken fixture` or `shared failure` lists its one mode.
- Other kinds print no block.

Each mode reads:

```
1.  4 failed attempts · first seen 08-12 09:40 at b71e004, 6 runs ago
    System.InvalidOperationException
    expected <num> but was <num>
      at MyApp.Checkout.Basket.Total()
      at MyApp.Tests.CheckoutTests.Completes()
```

- **The count** is the failures the finding counted. For a `flaky` or `always failing` finding, it
  leaves out the runs its population marker excludes.
- **First seen** says whether the mode is new. It looks at every failure in the window and starts
  from the test's own first run in the window, so a test that joined the window late is not called a
  regression.
  - `since its first run in the window`: the mode was there in the test's first run in the window.
    A group reads `since its tests first ran in the window`. Under `--test`, a test whose only run is
    the newest reads `since its first run, the latest`.
  - `first seen in the latest run`: the mode is new in the newest run.
  - `first seen <date> at <sha>, N runs ago`: the mode appeared after the test's first run. The date
    and commit are those of the oldest failure the finding counted. When the mode's older failures
    are in runs the finding set aside, that failure can be newer than the mode, down to
    `first seen <date> at <sha>, in the latest run`. ` at <sha>` is left out when no commit was
    recorded.
- **The message** is wrapped and stops after 6 lines, ending in `...`.
- **Frames** are your own code, innermost first, up to 5. When none of your code was on the stack,
  the framework's frames are shown with the note
  `(no test code on the stack; framework frames shown)`.
- `exception type not recorded` replaces a type the adapter did not record.
- `the adapter recorded no failure detail` replaces the type, message and frames when the failure
  was recorded without an exception.
- `timed out, no failure detail recorded` replaces them for a timeout recorded without an exception.

Timed-out attempts are always modes of their own, counted apart from failed attempts. At most 5 modes
are printed, then `N more failure modes in --format json`. The heading counts all of them.

---

## Reproducing a finding

`--id` tells you how to see a finding fail on your own machine. The detail block has a `repro` line
that says how many times to run the test. Below the fence, a `Repro:` line gives the command. It is
the last line, after `History:` when the finding has one:

<!-- xping:sample detail-repro-flaky -->
````
Xping · SampleApp.MSTest · 9 runs · 2026-08-20 → 2026-08-21 · main@c670fc3
16 tests · 10 healthy · 6 flagged

```
FINDING                                                       row 1 of 1
────────────────────────────────────────────────────────────────────────

1.  HIGH  flaky
    CalculatorTests.FlakyTest_RaceCondition_FailsIntermittently
    failed 4 of 9 executions (44.4%) in 4 of 9 runs, 1 failure mode
    evidence sparse | -env-cluster | f_1491bf4c | ...lculatorTests.cs:57

    test      SampleApp.MSTest.CalculatorTests.
              FlakyTest_RaceCondition_FailsIntermittently
    assembly  SampleApp.MSTest
    source    tests/SampleApp.MSTest/CalculatorTests.cs:57

    failed         4 of 9 executions (44.4%)
    runs affected  4 of 9
    failure modes  1
    repro          run 6 times: at 4 of 9 runs failing (44.4%), 6 runs
                   show a failure 95% of the time

FAILURE MODES                                             1 failure mode
────────────────────────────────────────────────────────────────────────

1.  4 failed attempts · since its first run in the window
    System.InvalidOperationException
    Race condition detected
```

rates: the marker on each finding says which runs its percentage was
counted out of; compare two only where the markers match.
https://docs.xping.io/cli/reading-the-report.html#the-population-marker

Showing 1 of 1 finding · all: xping report --all --assembly SampleApp.MSTest
History: xping report --test 61d4d46c --assembly SampleApp.MSTest
Repro: dotnet test tests/SampleApp.MSTest/SampleApp.MSTest.csproj -e XPING_ENABLED=false --filter FullyQualifiedName=SampleApp.MSTest.CalculatorTests.FlakyTest_RaceCondition_FailsIntermittently
````

The CLI prints the command and never runs it. The list view shows no `repro`. Open the finding with
`--id`, or its test with [`--test`](#test-history).

### The command

The command depends on the runner that the newest run in the window recorded:

```
VSTest:  dotnet test <path> -e XPING_ENABLED=false --filter <filter>
MTP:     dotnet run --project <path> -e XPING_ENABLED=false -- --filter <filter>
```

- **Run it from the repository root.** `<path>` is the test project's path from the repository root,
  as the SDK recorded it. When no path was recorded, the command has no path, and you run it from the
  project folder. The SDK records the path only inside a Git repository, and not when the project
  sets `GenerateAssemblyInfo=false`.
- **`-e XPING_ENABLED=false`** keeps the reruns out of the store. Otherwise many filtered runs of one
  test would push your real runs out of the window.
- **There is no `--no-build`.** The store does not record the build configuration. Add `-c Release`
  yourself if you build in Release.
- **The filter** is `FullyQualifiedName=` and the stored name, escaped for the runner. On NUnit it
  selects exactly the failing case, with its arguments. On MSTest and xUnit, the rows of a data-driven
  test share one name, so the command runs every row.
- **Quoting follows `XPING_SHELL`, or your OS**, like every command the report prints: PowerShell
  7.3 or later on Windows, bash and zsh elsewhere. A filter that holds parentheses or quotes is put
  in single quotes. Do not paste it into cmd.exe or Windows PowerShell 5.1. See
  [known limitations](../troubleshooting/known-limitations.md#printed-commands-are-quoted-for-one-shell-chosen-by-xping_shell-or-the-os).

### The run count

The count is how many runs you need to see at least one failure 95% of the time, at the rate the test
failed in the window. The rate is the runs it failed in, out of the runs it ran in with a result. A
run that a retry turned green counts as a pass, because it will pass the same way when you rerun it.

- Above 50 runs (a test that fails in fewer than about 6% of runs), no count is given. The `repro`
  line says the failure is too rare to reproduce by rerunning, and how many runs it would take. The
  command is still printed, for debugging one run.
- An always-failing test needs one run.

### Which findings have one

| Kind | The command reruns |
|---|---|
| `flaky`, `always failing`, `timing out` | The test |
| `broken fixture` | The member that failed in the largest share of its runs |
| `shared failure` | Up to three members that failed in the largest share of their runs. The count is for the highest of their rates |

The other kinds have none:

- **Retry kinds** (`masked by retry`, `deeper retries`, `out of retries`): the retry attribute is
  still on when you rerun, so the run stays green.
- **Duration kinds** (`slower`, `unstable timing`): a rerun prints pass or fail, not a duration to
  compare.
- **`concurrency`**: the finding is about the difference between a run alone and a full run, and
  each framework sets parallelism its own way.
- **`time sensitive`**: the failure depends on the time of day.
- **`stopped running`**: `dotnet test --list-tests` answers whether the test is still discovered. A
  filtered rerun does not.

The SDK records `VSTest` or `MTP` as the runner. The xUnit console runner and other hosts record
neither, so their findings have no command.

---

## Test history

`--test` shows every run the store holds for one test, what the test did in each run, and its
findings. It works for any test, including one with no findings.

```bash
xping report --test FlakyTest_PassesOnRetry
```

<!-- xping:sample history-flaky -->
````
Xping · SampleApp.MSTest · 20 runs · 2026-08-20 → 2026-08-21 · main@c670fc3
16 tests · 10 healthy · 6 flagged
⚠ 14 of 20 runs covered part of the suite

```
TEST                                                         fp 3a597eb2
────────────────────────────────────────────────────────────────────────

    test    SampleApp.MSTest.CalculatorTests.FlakyTest_PassesOnRetry
    source  tests/SampleApp.MSTest/CalculatorTests.cs:42

ran in 6 of 20 runs: 0 passed, 6 passed on retry, 0 failed
duration p50 12ms · p95 13ms · range 11-13ms

RUNS                                                        newest first
────────────────────────────────────────────────────────────────────────

               ─     absent from 14 runs, 14 partial
  08-20 15:35  ✗→✓   attempt 2         12ms  c670fc3
  08-20 15:34  ✗→✓   attempt 2         11ms  c670fc3
  08-20 14:02  ✗→✓   attempt 2         13ms  c670fc3
  08-20 14:02  ✗→✓   attempt 2         11ms  c670fc3
  08-20 13:20  ✗→✓   attempt 2         12ms  c670fc3
  08-20 13:19  ✗→✓   attempt 2         12ms  c670fc3

FAILURE MODES                                             1 failure mode
────────────────────────────────────────────────────────────────────────

1.  6 failed attempts · since its first run in the window
    Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException
    assert.areequal failed. expected:<<num>>. actual:<<num>>.
      at SampleApp.MSTest.CalculatorTests.FlakyTest_PassesOnRetry()

FINDINGS                                                      2 findings
────────────────────────────────────────────────────────────────────────

1.  HIGH  flaky
    failed 6 of 12 executions (50%) in 6 of 20 runs, 1 failure mode
    evidence sparse | -env-cluster | f_8f042eab | ...lculatorTests.cs:42

2.  MED   masked by retry
    passed on retry 6 times in 6 of 20 runs, up to attempt 2
    evidence sparse | -env | f_a2b61c2f | .../CalculatorTests.cs:42
```

rates: the marker on each finding says which runs its percentage was
counted out of; compare two only where the markers match.
https://docs.xping.io/cli/reading-the-report.html#the-population-marker

2 findings about this test · all: xping report --all --assembly SampleApp.MSTest --runs 20
Detail of row 1: xping report --id f_8f042eab --assembly SampleApp.MSTest --runs 20
````

- **The two summary lines** count the runs the test was in, by each run's deciding attempt.
  `passed on retry` passed after attempt 1. `failed` includes timeouts. A run in which the test was
  skipped, not run or inconclusive adds `, N other`. The duration line is over the runs that passed
  or failed.
- **Each row under `RUNS`** is one run, newest first, in UTC. It shows:
  - the first attempt's result and, when the deciding attempt differs, an arrow to it (`✗→✓`);
  - `attempt N` when a retry decided the run, or the result when it was not a pass or a fail
    (`timeout`, `skipped`, `not run`, `inconcl.`, with `@N` for a retry);
  - the deciding attempt's duration, and the commit;
  - `partial` when the run covered part of the suite.
- **Runs that do not contain the test** are one line per stretch: `absent from 14 runs, 14 partial`.
  When the partial count equals the run count, the test was not deleted: those runs were filtered to
  other tests.
- **`FAILURE MODES`** is the [same block as under `--id`](#failure-modes), built from all of the
  test's failures. It counts every failed attempt in the window, timeouts included, and removes
  nothing. It can list a mode that `--id` leaves out. A first attempt that passed on a retry counts,
  so a test whose summary says `0 failed` can list modes.
- **`FINDINGS`** lists every finding about the test, including groups it belongs to. Each keeps its
  row number from the full report over the same window. The `all:` command below the fence prints
  that report. When the test has too little history, the block says so:
  `too little history to judge: findings need 5 runs, this test has 3`.

A finding that a rerun can show ends with a `repro` line, and the last line below the fence is its
command, named by its row:

<!-- xping:sample history-repro -->
````
Xping · SampleApp.MSTest · 9 runs · 2026-08-20 → 2026-08-21 · main@c670fc3
16 tests · 10 healthy · 6 flagged

```
TEST                                                         fp 5d1e2a0c
────────────────────────────────────────────────────────────────────────

    test    SampleApp.MSTest.CalculatorTests.
            FlakyTest_RaceCondition_FailsIntermittently
    source  tests/SampleApp.MSTest/CalculatorTests.cs:57

ran in 9 of 9 runs: 5 passed, 0 passed on retry, 4 failed
duration p50 38ms · p95 41ms · range 38-41ms

RUNS                                                        newest first
────────────────────────────────────────────────────────────────────────

  08-21 09:02  ✓                       38ms  c670fc3
  08-21 06:51  ✗                       41ms  c670fc3
  08-21 04:40  ✓                       38ms  c670fc3
  08-21 02:29  ✗                       41ms  c670fc3
  08-21 00:18  ✓                       38ms  c670fc3
  08-20 22:07  ✓                       38ms  c670fc3
  08-20 19:56  ✗                       41ms  c670fc3
  08-20 17:45  ✗                       41ms  c670fc3
  08-20 15:34  ✓                       38ms  c670fc3

FAILURE MODES                                             1 failure mode
────────────────────────────────────────────────────────────────────────

1.  4 failed attempts · first seen 08-20 17:45 at c670fc3, 7 runs ago
    Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException
    assert.areequal failed. expected:<<num>>. actual:<<num>>. the
    counter lost updates.
      at SampleApp.MSTest.CalculatorTests.
         FlakyTest_RaceCondition_FailsIntermittently()

FINDINGS                                                       1 finding
────────────────────────────────────────────────────────────────────────

1.  HIGH  flaky
    failed 4 of 9 executions (44.4%) in 4 of 9 runs, 1 failure mode
    evidence sparse | -env-cluster | f_1491bf4c | ...lculatorTests.cs:57
    repro  run 6 times: at 4 of 9 runs failing (44.4%), 6 runs show a
           failure 95% of the time
```

rates: the marker on each finding says which runs its percentage was
counted out of; compare two only where the markers match.
https://docs.xping.io/cli/reading-the-report.html#the-population-marker

1 finding about this test · all: xping report --all --assembly SampleApp.MSTest --runs 9
Detail of row 1: xping report --id f_1491bf4c --assembly SampleApp.MSTest --runs 9
Repro of row 1: dotnet test tests/SampleApp.MSTest/SampleApp.MSTest.csproj -e XPING_ENABLED=false --filter FullyQualifiedName=SampleApp.MSTest.CalculatorTests.FlakyTest_RaceCondition_FailsIntermittently
````

The command is for the first finding whose `repro` gives a run count, so `Repro of row 2:` can
follow `Detail of row 1:`. A group the test belongs to gets no `repro` here, because its command
reruns the members most likely to fail, which may not include this test. Open the group with `--id`.

### Naming the test

The name is matched in three tiers. The first tier that matches anything wins:

1. The exact fully qualified name.
2. A suffix that starts after a `.`: `Method`, `Class.Method` or `Namespace.Class.Method`. A nested
   type written `Outer+Inner` also matches with a `.`. You can leave out a test's `(args)`: `Add`
   matches every case of `Add(…)`, and `Add(1,2)` matches only that case.
3. The start of the test's fingerprint, at least 8 characters, or the whole fingerprint. The `fp`
   value in the `TEST` heading is the first 8.

Matching is case-sensitive. A fingerprint prefix made only of hex digits is also tried in lower
case. The CLI uses each test's newest name, so it finds a renamed test under its current name.
Without `--assembly`, it searches every assembly. The history is always one assembly's.

When the name matches more than one test, the command prints no history and exits `4`. stderr lists
up to ten candidates with their fingerprints, and the command for the first. With `--format json`,
stdout still carries the envelope, with the candidates in `test.candidates`:

```
'Add' matches 3 tests. Name one by its fingerprint:
  7e41c9a0  SampleApp.MSTest
    SampleApp.MSTest.ParameterizedTests.Add(1,2)
  5b1e09fd  SampleApp.MSTest
    SampleApp.MSTest.ParameterizedTests.Add(2,3)
  c4d2aa10  SampleApp.NUnit
    SampleApp.NUnit.CalculatorTests.Add
xping report --test 7e41c9a0 --assembly SampleApp.MSTest
```

When it matches none, the command exits `3`. stderr says whether the store has no such test, or the
window hides its runs:

```
No test matching 'Ad' in this store (3 assemblies, 48 runs).
No run of 'Login' in this window (--runs 5). It ran in 12 earlier runs; drop --runs to see them.
```

### The window

Under `--test`, the window is every run the store holds for the assembly, not the last 20. `--runs`
and `--since` narrow it, as they do for `report`. The findings are computed over the same window, so
a narrower window can change them. Every command the view prints repeats the window: `--since` as
you gave it, or else `--runs` with the window's size.

`--id` on a finding about one test prints a `History:` line with the `--test` command. When an id is
not reported and its test has no other finding in the window, stderr gives the test's `--test`
command.

---

## Source locations

The last part of a finding's trailer is where the test is declared:

<!-- xping:sample docs-source-location trailer -->
```
    evidence robust | -env | f_2a91c0de | .../Billing/SummaryTests.cs:88
```

- It is the file and the line where the test's body starts, relative to the repository root.
- The SDK reads it from the test assembly's Portable PDB, because no test framework reports it. A
  build with `DebugType=none` has no location, and the trailer ends at the finding id.
- A long path is shortened from the left at a folder boundary, such as
  `.../Billing/SummaryTests.cs:88`.
- A finding about a group names no file, because its members are in different places. The JSON has
  each member's location.

Whether the line is the opening brace or the first statement depends on the build. See
[known limitations](../troubleshooting/known-limitations.md#source-location-comes-from-the-pdb-and-points-at-the-start-of-the-body).

---

## Limits of local analysis

The report reads a short history from one machine. These limits follow from that.

### `time sensitive` uses wide time bins

A test that fails only on Mondays, only at the end of the month, or only within a single day is not
reported. The default window is twenty runs over fourteen days, which holds at most two of any
weekday and one month end. That is not enough for a rate. So the bins are six-hour quarters of the
day, weekend against weekday, and one UTC offset against another, and the failing side must cover
three different local days.

### `time sensitive` is quieter when runs are spread across the day

The report tries several splits and keeps the best one. Trying several splits makes a large gap more
likely by chance, so the report corrects for the number of different splits the window allows. A
window whose runs fall in two quarters of the day makes one comparison. A window spread across the
whole day and both day groups makes four or more, and needs a larger gap. On an even split of two
weeks of runs, with no failures on the other side, five failures out of six are reported and four
are not.

### `concurrency` cannot separate concurrency from duration

A test that got slower can be reported as `concurrency` when nothing about contention is wrong.

The SDK observes how many tests ran alongside each test. It does not set that number. A slow test
overlaps more tests than a fast one, and its place in the run changes how busy the suite was around
it. So a test that became slower also ran alongside more tests, and shows the same rising trend as a
test that fails under contention. Correcting for this needs more executions than a twenty-run window
holds, so the report does not try. The finding publishes the observed range and a table per level:
a rise across levels 11 to 13 is much weaker evidence than the same rise across levels 1 to 14.

The finding also needs enough runs. Eight runs, four of them clearly separated from the other four,
is the smallest window that can produce one. One quiet run against any number of busy runs never
does.

### `stopped running` needs a habit, and eight runs

A test that ran in only some runs, and was then deleted, is not reported. No deleted test is reported on a
store with fewer than eight runs.

Absence is evidence only against a habit. A test that ran in three of three earlier runs and then
stopped had a habit. A test that ran in three of seventeen was mostly absent already. So the report
uses a statistical test, not a count (see [`stopped running`](#stopped-running)). With fewer than
eight runs, the recent part of the window is one run, and one absence is not evidence.

### Outside xUnit, `stopped running` treats a run of less than half the suite as filtered

Under NUnit and MSTest, the SDK cannot tell a filtered run from a run after tests were deleted:

- A run that covered less than half the suite is set aside. So a deletion that removed more than half
  the suite is never reported.
- A `--filter` that selected more than half the suite is counted as a full run. Each test it did not
  select can then be reported as `stopped running`.

The same applies to xUnit tests run from an IDE, and to xUnit v3 tests run on vstest.

Under xUnit v2 with `dotnet test`, and xUnit v3 on Microsoft.Testing.Platform, the SDK records how
many test cases the framework discovered and how many it ran. A run that ran fewer than it discovered
was filtered. These runs are always classified correctly, and the half-suite rule is not used.

The rule leans towards silence on purpose. `stopped running` is always `LOW`, because a test usually
disappears because you just removed it.

Setting partial runs aside shortens the history, so a store of mostly filtered runs can fall below
the eight runs this kind needs. The summary line says how many runs covered part of the suite, and
the finding's marker is `-partial`.

### The duration kinds wait for a full run

While every recent run covered part of the suite, as three `dotnet test --filter` runs in a row do,
the report cannot raise `slower` or `unstable timing` about a test those runs did not select. The
report counts such tests in `summary.notMeasured.DurationRegression.awaitingRuns` and
`summary.notMeasured.DurationUnstable.awaitingRuns`. A test that had already stopped running before
the filtered runs began is reported once, as `stopped running`, and is not counted there.

A run of the whole suite makes the test measurable again. It does not produce a finding by itself:
`slower` needs 7 earlier and 3 recent comparable runs, and `unstable timing` needs more than one
reading.

### `out of retries` reads what happened, not the declared limit

A test whose attribute allows three retries, but which recorded only two attempts, is not reported as
out of retries. A test that failed a fourth attempt is, whatever its attribute says.

Retry attributes disagree on what their number counts. NUnit records `TryCount`, which includes the
first attempt. An xUnit or MSTest retry library records its own limit, which may or may not include
the first attempt. So the report uses only the recorded attempts, and publishes the declared limit as
`maxRetriesAsDeclared`.

The xUnit and MSTest adapters record the delay between retries. NUnit does not, so the
`configured wait` metric is missing on NUnit. The delay is never added to the measured retry time.

### `deeper retries` needs a baseline

A test that needs more attempts than before may be reported only as `masked by retry`, or not at all.
The finding needs five earlier runs in which the test settled green, and two recent ones. A test that
runs under a filter, is less than two weeks old, or fails in most of the window often does not have
that. Below eight runs, the recent part of the window is one run. Both parts also set environmental
runs aside, because one outage in three recent runs would look like a change.

The finding is only as good as the adapter's attempt tracking. Where attempts are not recorded (see
[known limitations](../troubleshooting/known-limitations.md)), a test that needs three attempts
looks like one that needs one.

### `slower` can fire when the recent runs vary more, not when they are slower

When a test's three most recent runs vary much more than its history, the report can call the test
`slower` although its typical duration did not change. The comparison is exact only when both sides
vary alike, and three recent runs are too few to correct for a difference.

How often this happens, for a test whose typical duration did not change, at the report's
significance level:

| How the spreads compare | Reported as slower |
|---|---|
| Earlier runs four times as wide as the recent ones | 0.02% |
| Both alike | 1.0% |
| Recent runs twice as wide as the earlier ones | 2.0% |
| Recent runs four times as wide as the earlier ones | 6.5% |

A history of two weeks usually varies more than three runs do, so in the common case the finding
fires less often than 1 in 100.

When it happens, the recent runs did change: in how much they vary, not in how long they take. That
is what `unstable timing` reports, and a `slower` finding hides `unstable timing` for the same test.

To check, read both sides' spread in `--format json`:

```json
"evidence": {
  "current":  { "comparedSessions": 3, "comparedDispersion": 1.048 },
  "baseline": { "comparedSessions": 7, "comparedDispersion": 0 }
}
```

Each spread is relative to its own side's median. A spread of 0.2 means a typical run was a fifth
away from that side's median. A recent spread much larger than the baseline spread is the case
described here.

---

## See also

- [Command reference](command-reference.md)
- [Xping Cloud data in the CLI](cloud-data.md)
- [Local store](../configuration/local-store.md)
- [Run without an account](../getting-started/local-first.md)
