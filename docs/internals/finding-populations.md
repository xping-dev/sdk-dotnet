# Which executions, or runs, each finding kind counts

Every kind that publishes a rate publishes a count for that rate to be taken over, and the kinds do
not all count the same thing. Most count executions; `Vanished` counts runs, because it reads
appearances rather than outcomes. The report ranks findings of different kinds against each other,
so a reader comparing two rates is comparing two denominators — and until this was recorded, the
per-kind decision existed only as six separate remarks in six provider files, which is how the
inconsistency arose in the first place.

**This file is the record.** A kind added to `FindingKind` records its decision here and in
`PopulationRules.For`, and its provider's remark points back here rather than arguing the case a
seventh time.

## The matrix

| kind | environmental sessions | clustered failures | partial runs | rule |
|---|---|---|---|---|
| `RetryMasked` | excluded (whole run) | kept | kept | `ExcludesEnvironmental` |
| `RetryDeepening` | excluded (whole run) | kept | kept | `ExcludesEnvironmental` |
| `RetryExhausted` | excluded (whole run) | kept | kept | `ExcludesEnvironmental` |
| `Flaky` | excluded | excluded | kept | `ExcludesEnvironmentalAndClustered` |
| `AlwaysFailing` | excluded | excluded | kept | `ExcludesEnvironmentalAndClustered` |
| `TimingOut` | excluded | excluded | kept | `ExcludesEnvironmentalAndClustered` |
| `BrokenFixture` | **kept** | n/a | kept | `AllExecutions` |
| `SharedFailure` | **kept** | n/a | kept | `AllExecutions` |
| `DurationRegression` | excluded | kept | kept | `ExcludesEnvironmental` |
| `DurationUnstable` | excluded | kept | kept | `ExcludesEnvironmental` |
| `ParallelSensitive` | excluded | kept | kept | `ExcludesEnvironmental` |
| `TimeSensitive` | excluded (whole run) | kept | kept | `ExcludesEnvironmental` |
| `Vanished` | **kept** | kept | **set aside** | `ExcludesPartialRuns` |

A session is *environmental* when at least ten of its tests failed and they are at least three in
ten of the tests it ran — `SessionView.For`, against `EnvironmentalSessionFailureRate` and
`EnvironmentalSessionMinFailures`. A failure is *clustered* when its signature is shared across
enough tests to be reported once as a `SharedFailure` or `BrokenFixture`. A run is *partial* when
its distinct tests are under `PartialSessionShare` — a half — of the largest run in the window,
which is what a `dotnet test --filter` produces; `SessionView.IsPartial`, set on the whole window at
once because the classification is a comparison rather than a measurement.

**Only one kind sets a partial run aside, and the asymmetry is the reason.** A filtered run's
*outcomes* are as true as any other run's — a test that failed in one failed — so a kind reading
outcomes must count it in full or it would throw away real evidence. Its *silences* mean nothing: it
did not fail to see the tests it excluded, it never looked for them. So the rule applies to the
kinds reasoning from absence, which today is `Vanished` alone.

That is also why this is a fourth rule rather than a second, orthogonal marker on each finding. The
two dimensions are mutually exclusive in practice: a kind either reads execution outcomes, and then
discounting executions is its whole question and partiality is none of its business, or it reads
appearances, and then the reverse. There is no `ExcludesEnvironmentalAndClusteredAndPartial` waiting
to be written. A second marker would also double the comparison a marker exists to make cheap, and
spend the trailer columns the finding's source path needs.

The rule reaches the JSON envelope as `population` on every finding, and the rendered report as a
marker in each finding's trailer — `all runs`, `-env`, `-env-cluster`, `-partial` — with a legend
below the fence that states the comparison rule and links the definitions in
`docs/cli/command-reference.md`. The legend deliberately does not define the markers: it could not
do so in the space, and the only thing a reader has to do with one is decide whether two percentages
can be compared.

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
| `Vanished` | `baselineSessionCount`, `currentSessionCount` | `partialSessionsSetAside` |

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

## What each kind's evidence level counts

The `evidence low|moderate|high` label on a finding is banded on the **runs that finding was
computed from** — not on the runs its test appeared in. They are different numbers for every kind in
the table above, and for the same reasons: a discounted run is not an occasion the test's own
behaviour was observed on, and a run the kind could not read at all is not an occasion either.

| kind | runs the level counts |
|---|---|
| `Flaky`, `AlwaysFailing`, `TimingOut` | runs of the test, less the environmental ones |
| `RetryMasked` | the same |
| `RetryDeepening` | the settled runs in both arms of the comparison |
| `RetryExhausted` | `runsConsidered` |
| `SharedFailure`, `BrokenFixture` | runs the cluster's best-evidenced member ran in — nothing is set aside |
| `DurationRegression` | `current.comparedSessions` + `baseline.comparedSessions` |
| `DurationUnstable` | the runs behind the normalised readings the dispersion was taken over |
| `ParallelSensitive` | `trend.sessions` |
| `TimeSensitive` | `worse.sessions` + `other.sessions` |
| `Vanished` | `baselineSessions` — the habit the absence is a change from |

Wherever the kind already publishes that figure, the level is banded on the published one, so the
label and the counts a reader can check it against cannot drift apart. The number itself reaches the
JSON as `evidenceSessions` on every finding, beside `evidenceLevel`.

**Two findings about one test may therefore carry different levels.** That is the same statement the
population marker already makes, one layer up: the kinds do not count the same runs, so they do not
have the same amount of evidence either. A `TimeSensitive` split over the ten runs that recorded a
clock is not better evidenced because the test also ran in ten that did not.

**What is *not* banded this way is whether the finding is reported at all.** The reporting floor —
`MinimumSessionsPerTestToReport`, applied in `FindingCoordinator` — reads the runs the subject
appeared in, for every kind alike. Emission has to be one rule, or a test flagged by one metric is
silently dropped by another with nothing on screen to explain it. So a claim resting on two runs of
a test with twenty runs of history is still printed. It is printed saying `evidence low`, which is
what the level is for.

**A filtered run cannot push a test under that floor, and this is arithmetic rather than judgement.**
`EvidenceLevelResolver.MeetsReportingFloor` has two clauses — the window's run count against
`MinimumSessionsToReport`, and the subject's own against `MinimumSessionsPerTestToReport`. The
subject's runs are drawn from the same window, so that count can never exceed the window's; while
the two constants are both 5, every window the first clause would reject the second has rejected
already, and the first can decide nothing on its own. The count a filtered run inflates is therefore
not the count that decides. The per-test clause self-corrects by construction: a test absent from a
filtered run simply has one fewer run of its own.

That holds *while* the constants are equal. Raising `MinimumSessionsToReport` above its per-test
counterpart would make the window clause live, and would then be a decision about filtered runs
whether or not it were taken as one — `TheWindowArmOfTheFloorNeverDecidesOnItsOwn` fails at that
point rather than leaving it to be discovered.

## What each kind could not measure

A finding's population says which executions — or, for `Vanished`, which runs — its rate was counted
over. This section says something prior to that: which **tests the kind could not be computed for at
all**, and why. Until it was
recorded, a provider that declined for want of data left no trace anywhere — the summary's excluded
tally counts only the candidates the coordinator itself dropped at the reporting floor — so a test no
statistic could be taken of fell through into `healthy` and was reported to a reader as fine.

The tally reaches the JSON as `summary.notMeasured`, keyed by kind, and the rendered report as one
line above the fence naming the three largest with `+n more`.

### The two reasons, and the question that separates them

| reason | meaning | what a reader does |
|---|---|---|
| `awaitingRuns` | the window does not hold enough of this test yet | wait; the store fills |
| `unreadable` | nothing this kind reads was recorded | change what is recorded, or accept it |

The question at every decline site is whether **another run of the same shape as the ones already
recorded** would fix it. Nothing subtler is needed: the two differ in what the reader should do next,
not in how the gate was written. Six baseline runs where the comparison needs seven is `awaitingRuns`;
a test whose every run recorded a zero median normalises nothing, and the eighth such run normalises
nothing either, so it is `unreadable`.

**This is not the reporting floor.** That floor asks whether a test has been around long enough to be
judged and is applied centrally, to a candidate a provider already computed. This counts tests for
which no candidate was ever computed. A candidate that reaches the coordinator has been measured by
definition, so the two can never both fire on one claim.

### The matrix

| kind | `awaitingRuns` | `unreadable` |
|---|---|---|
| `DurationRegression` | either arm under 7 / 3 comparable runs, or a current slice of nothing but partial runs | an arm that held runs and normalised none of them |
| `DurationUnstable` | one normalised reading, or a current slice of nothing but partial runs | nothing normalisable, or no run with a usable median |
| `ParallelSensitive` | structurally none — this kind has no session floor | fewer than two distinct concurrency levels |
| `TimeSensitive` | fewer than two arms' worth of runs on a clock | no clock at all, two time zones, or no split the runs admit |
| `Vanished` | baseline under `VanishedMinBaselineSessions`, or no baseline slice | structurally none — an appearance is always readable |
| `RetryMasked`, `RetryDeepening`, `RetryExhausted` | every run of the test was discounted as environmental | structurally none — an attempt number is always recorded |
| `Flaky`, `AlwaysFailing`, `TimingOut` | every execution of the test was discounted | structurally none — an outcome is always readable |
| `SharedFailure`, `BrokenFixture` | **absent** | **absent** |

A published zero and an absence are different statements. A zero means the kind was offered tests and
could read every one; an absence means the kind keeps no such tally.

### Why five kinds publish nothing

**`SharedFailure` and `BrokenFixture` are counted in signature groups, not tests.** Every other entry
in the table is a count of fingerprints, and a count of groups published under the same field is not
a number a reader can compare with the one beside it.

**The three retry kinds share one figure rather than each publishing their own.** They read one
reduction of a test's runs, and the chain that picks between them — exhausted, then deepening, then
masked — stops at the first that fires. Whether a later kind could have been measured on a test an
earlier one claimed is a question the algorithm never asks, and answering it for the tally alone would
mean running all three on every fingerprint to fill in a number nobody reads. So the count is taken at
the one precondition the three share: a test whose every run was an outage.

### Two skips that are charged to nothing

A fingerprint the index holds but cannot resolve to a test is an inconsistency inside the index, not a
measurement the data declined; every provider passes over it silently.

A test absent from the recent slice is passed over by `DurationProvider` for the reason the population
matrix already gives — its absence belongs to `Vanished` — and counting it here would state one
disappearance twice, in a line whose whole purpose is to name questions whose answers are missing.

**Unless nothing in the recent slice asked about it.** When every run in the current slice covered
part of the suite, there is no disappearance to state twice: `Vanished` sets those runs aside
precisely so it makes no claim about them, so deferring to it charged the test to a kind that is
also silent, and the summary went on reporting that duration had read every test it was offered — a
local `dotnet test --filter` loop reaches this the moment three filtered runs land in a row. Both
duration kinds then count the test as `awaitingRuns`, which is what it is: a measurement waiting on
a run of the whole suite. The predicate is the slice, not the window — one full run in the current
slice did ask, so a test missing from it has genuinely stopped and belongs to `Vanished` again.

### Why it is per kind and never a total

Summing across kinds counts one test once per question its data could not answer. Intersecting them
collapses to nothing, because a test's pass and fail are always readable and so nearly every test is
measured by something. And either total would move with `--kind`, which is the one thing a count of
what could not be measured must not do: a reader comparing a full report with a narrowed one would
read the difference as the suite improving. Per kind it cannot move, because the six providers own
disjoint kind sets — a kind's figure comes from its own provider or the kind is absent from the map.

### What the terms do not sum to

For a kind that publishes both figures:

```
hypothesesTested[k] + notMeasured[k].AwaitingRuns + notMeasured[k].Unreadable
    = the fingerprints that kind was offered
```

and the tests it measured and had nothing to say about are inside `hypothesesTested`, not published
separately. That term is the common case — it is very nearly the suite — and publishing it would make
the number the test count.

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
