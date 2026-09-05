# Which executions each finding kind counts

Every kind that publishes a rate publishes a count for that rate to be taken over, and the kinds do
not all count the same executions. The report ranks findings of different kinds against each other,
so a reader comparing two rates is comparing two denominators — and until this was recorded, the
per-kind decision existed only as six separate remarks in six provider files, which is how the
inconsistency arose in the first place.

**This file is the record.** A kind added to `FindingKind` records its decision here and in
`PopulationRules.For`, and its provider's remark points back here rather than arguing the case a
seventh time.

## The matrix

| kind | environmental sessions | clustered failures | rule |
|---|---|---|---|
| `RetryMasked` | excluded (whole run) | kept | `ExcludesEnvironmental` |
| `RetryDeepening` | excluded (whole run) | kept | `ExcludesEnvironmental` |
| `RetryExhausted` | excluded (whole run) | kept | `ExcludesEnvironmental` |
| `Flaky` | excluded | excluded | `ExcludesEnvironmentalAndClustered` |
| `AlwaysFailing` | excluded | excluded | `ExcludesEnvironmentalAndClustered` |
| `TimingOut` | excluded | excluded | `ExcludesEnvironmentalAndClustered` |
| `BrokenFixture` | **kept** | n/a | `AllExecutions` |
| `SharedFailure` | **kept** | n/a | `AllExecutions` |
| `DurationRegression` | excluded | kept | `ExcludesEnvironmental` |
| `DurationUnstable` | excluded | kept | `ExcludesEnvironmental` |
| `ParallelSensitive` | excluded | kept | `ExcludesEnvironmental` |
| `TimeSensitive` | excluded (whole run) | kept | `ExcludesEnvironmental` |
| `Vanished` | **kept** | kept | `AllExecutions` |

A session is *environmental* when at least ten of its tests failed and they are at least three in
ten of the tests it ran — `SessionView.For`, against `EnvironmentalSessionFailureRate` and
`EnvironmentalSessionMinFailures`. A failure is *clustered* when its signature is shared across
enough tests to be reported once as a `SharedFailure` or `BrokenFixture`.

The rule reaches the JSON envelope as `population` on every finding, and the rendered report as a
marker in each finding's trailer — `all runs`, `-env`, `-env-cluster` — expanded by a two-line
legend below the fence.

## What each kind publishes

| kind | denominator | discounts published |
|---|---|---|
| `Flaky`, `AlwaysFailing`, `TimingOut` | `executionsConsidered`, `sessionsConsidered` | `discountedEnvironmental`, `discountedClustered` |
| `RetryMasked` | `executionsConsidered`, `sessionsConsidered` | `discountedEnvironmental` |
| `RetryDeepening` | `current.runs`, `baseline.runs` | `discountedEnvironmentalRuns` |
| `RetryExhausted` | `runsConsidered`, `retriedRuns`, `sessionsConsidered` | `discountedEnvironmentalRuns` |
| `DurationRegression` | `current.executionsConsidered`, `baseline.executionsConsidered` | `discountedEnvironmental`, per slice |
| `DurationUnstable` | `executionsConsidered`, `sessionsConsidered` | `discountedEnvironmental` |
| `ParallelSensitive` | `levels[].executionsConsidered` | `discountedEnvironmental`, `executionsWithoutConcurrency` |
| `TimeSensitive` | `worse.sessions`, `other.sessions` | `discountedEnvironmentalRuns`, `runsWithoutClock` |
| `SharedFailure`, `BrokenFixture` | `failures`, `sessionsAffected`, `sessions` | none — nothing is set aside |
| `Vanished` | `baselineSessionCount`, `currentSessionCount` | none — nothing is set aside |

### `sessions` and `sessionsConsidered`

At the **top level of an evidence record**, `sessions` is the window's run count, and it appears
only on the kinds that discount nothing. Everywhere else the top-level field is
`sessionsConsidered`: the analysed runs less the environmental ones, which is what "in 5 of 18 runs"
has to be counted out of, because the numerator beside it already is. Publishing a considered
numerator over a window denominator understates the finding in exactly the way discounting only the
numerator would.

**Nested `sessions` is a different quantity and keeps the plain name.** `worse.sessions`,
`levels[].sessions` and `current.sessions` count the runs behind *that* arm, level or slice, not the
window — and each is drawn from executions its kind already filtered, so the exclusions counted
elsewhere in the same payload are gone from them too. They are not renamed to `sessionsConsidered`
because they are not that figure: an arm's size and "the analysed runs less the discounted ones" are
different numbers, and giving them one name would be the confusion this file exists to prevent. The
path is what disambiguates them, so read `sessions` as scoped to whatever it hangs off.

The counts reconcile. For `Flaky`, `AlwaysFailing` and `TimingOut`:

```
executionsConsidered + discountedEnvironmental + discountedClustered
    = the number of times the test ran in the window
```

Clustered failures do not shorten `sessionsConsidered`: they remove a failure, not a run — the test
still ran in that session and still did not fail there on its own account.

An execution that qualifies for both discounts is charged to the environment, so the two never
double-count. This is the arithmetic the field names exist to make possible: a test with twenty
executions, ten clustered failures and two of its own publishes `2 of 12`, and `12` is not how many
times it ran.

## Why the exceptions are exceptions

**`SharedFailure` and `BrokenFixture` keep environmental sessions.** An environmental session *is* a
shared cause seen from underneath. Discounting one here would silence the finding that explains it
and leave a reader with an unexplained gap in every other kind's counts.

**`Vanished` keeps them.** It counts session appearances, not failures. An environmental run is
still a run the test either was or was not in, and dropping it would shorten the very history the
absence is measured against.

## Discounting is not the same as an unreadable measurement

The rule describes **discounting** — a judgement the report makes about a run — and never data
availability. Two kinds also drop executions they could not read at all:

- `ParallelSensitive` drops executions whose adapter recorded no concurrency, and publishes
  `executionsWithoutConcurrency`.
- `TimeSensitive` drops runs whose session recorded no UTC offset, and publishes `runsWithoutClock`.

Neither earns a value in the enum. Folding them in would let `ExcludesEnvironmental` mean two
different things, and a reader would have no way to tell a suite the report declined to judge from
one it could not read. They are published for the same reason the discounts are: without them the
levels and arms cannot be reconciled with how many times the test ran, and a curve built on a third
of a test's executions reads exactly like one built on all of them.

## The one rule that is not per-kind

`DurationRegression` and `DurationUnstable` also normalise every duration by the median of the run
it came from, before any cross-run comparison. That is a separate mechanism and it does not replace
discounting: normalisation removes a machine that was uniformly slow, because the run's median moves
with every test in it, and it does not remove a run in which a third of the suite fell over — there
the median was taken over whichever tests survived, and every survivor is measured against a scale
the failures moved.

The cost is real. The recent slice is three runs and the comparison needs three, so one outage
inside it takes the regression away entirely. The published arm counts are what say so.
