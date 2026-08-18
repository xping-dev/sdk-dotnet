# Local Analysis Specification

**Status:** authoritative · **Version:** 1.7 · **Applies to:** `Xping.Cli` local analysis (`xping report` and subcommands)

This document is the single source of truth for local analysis. Every implementation session reads it and treats it as immutable. Sessions cite sections by number (`§5.3`). If an implementer believes a section is wrong, incomplete, or unimplementable, they **stop and report** — they do not adapt around it. Amendments follow §12.

---

## 1. Scope and non-goals

### 1.1 What local analysis is

`xping report` reads test sessions persisted locally by `Xping.Sdk`, computes **findings**, and renders them for a human or a machine. It runs entirely offline. It requires no account, no API key, and no network access.

### 1.2 Non-goals — do not implement these

| Non-goal | Reason |
|---|---|
| A confidence score | Scoring is owned by Xping.Dashboard. Two implementations would drift and eventually contradict each other in front of a customer. Local analysis emits **evidence**; the Dashboard emits **scores**. |
| Causal verdicts | The report states what was observed. Naming a cause anchors the reader (human or LLM) onto a guess and stops further investigation. See §7.4. |
| A configuration surface | v1 uses named constants only. Configuration is a later decision, taken once we know which thresholds users actually disagree with. |
| Cloud data access | A later capability of `Xping.Cli`, gated on authentication. It must not leak into local analysis code paths. |
| Mutation of the local store | Analysis is strictly read-only. |

### 1.3 Consistency constraint with the Dashboard

Where a concept exists in both places, local analysis uses the **same name and the same bands** as the Dashboard. Specifically `EvidenceLevel` (§4.4). Divergence here produces a product that contradicts itself.

---

## 2. The local store

### 2.1 Location and layout

```
<store-root>/.xping/
  .gitignore
  runs/
    run-{StartedAtUtc.Ticks:D19}-{sessionId[0..8]}.jsonl.gz
  sessions/
    session-{StartedAt.Ticks:D19}-{sessionId[0..8]}.json.gz
```

The store has **two tiers**, written together at the end of every run and pruned independently.

**`runs/` — the summary tier.** A gzip-compressed JSON Lines file: the first line is a
`LocalRunHeader` (session id, start timestamp, duration, environment, assembly, branch, commit SHA, CI flag, connected flag, schema version), followed by one `LocalTestRecord` per line. `LocalTestRecord` is a deliberately lossy projection — fingerprint, display name, outcome, duration, attempt, passed-on-retry, error hash — sized for a fast summary. The schema version travels with each run's header (`LocalRunHeader.Version`, currently `1`) so a reader can skip a file written by a newer schema without failing the whole store. This tier is **not** the analysis substrate; it exists for the SDK's own end-of-run retry hint and for cloud import.

**`sessions/` — the analysis tier.** One gzip-compressed JSON document holding one whole `TestSession`, serialised through the SDK's existing `XpingSerializerOptions`. Nothing is dropped: raw error text, stack traces, exception types, source locations, orchestration data and network metrics all survive, because §4.2, §5 and §7 all depend on fields the summary projection discards. **This is what `xping report` reads.**

A single JSON document rather than JSON Lines is what makes truncation *detectable*: a partial document fails to parse and is counted in `summary.unreadableSessions`, where a partial JSON Lines file parses cleanly and silently under-reports its executions. Analysis that counts executions cannot tolerate the second failure mode.

Resolution order for `<store-root>` (`Xping.Sdk.Core.Services.LocalStore.Internals.LocalStorePathResolver`):

1. `XPING_LOCAL_STORE` env var, when set — used verbatim.
2. The nearest ancestor of the running assembly's directory containing `.git`, `*.sln`, or `*.slnx` (walking up at most 32 levels), provided `<that-root>/.xping` is actually writable (probed with a real file write, not just directory creation).
3. A per-repository folder under the OS local application-data directory: `{LocalApplicationData}/Xping/stores/{sha256(origin)[0..16]}`, keyed by a short hash of the assembly's starting directory so unrelated repos that both fall back to the profile don't share history.

The walk starts from the test assembly's directory, not the current working directory, because `dotnet test` working directories vary across the CLI, IDEs, and CI runners while the assembly always lives inside the repository. If no location resolves (or none is writable), the store reports itself unavailable and the SDK silently records no local history — it never fails the test run.

The SDK writes `.xping/.gitignore` (containing `*`) inside the store directory itself on first write, rather than editing the repository's own `.gitignore`. Nested git ignore files are honoured by git, so the store disappears from `git status` without an unexplained diff in the developer's own `.gitignore`.

### 2.2 Contract

| Property | Rule |
|---|---|
| Append-only | Analysis never writes, moves, or deletes. Pruning is a separate explicit operation. |
| Format | Per §2.1: a slim JSON Lines projection in `runs/`, one whole `TestSession` per gzipped JSON document in `sessions/`. Analysis reads the latter. |
| Retention | 50 runs, 50 MB total store size, or 30 days, whichever is reached first (`LocalStoreOptions` defaults). All three limits are enforced together after each write, **per tier**; the newest file in a tier is never pruned. Oldest pruned on write, not on read. |
| Corruption | An unreadable or unparseable file is **skipped with a warning on stderr**. It never fails the command. The count of skipped files appears in `summary.unreadableSessions`. |
| Empty sessions | A session with no executions is skipped and is **not** counted as unreadable — it is not damage, but letting it occupy a window slot would dilute every rate computed against the session count. |
| Partial sessions | A session is written only after it finalises, so a test host killed mid-run leaves **no file** rather than a partial one. `summary.incompleteSessions` therefore reads 0 until the store gains a way to persist a run that did not finish; a truncated file is reported as unreadable instead. |

### 2.3 Session identity and ordering

Sessions are ordered by `StartedAt` descending. `SessionId` breaks ties. Ordering must be total and stable — see §10.

---

## 3. Window resolution

### 3.1 Definitions

- **Window** — the ordered set of finalised sessions under analysis, plus its boundaries as data.
- **Current slice** — the most recent *k* sessions in the window, used as the "now" side of any delta.
- **Baseline slice** — the remaining sessions in the window, used as the "before" side.

Findings that are not deltas (e.g. `Flaky`) operate on the whole window. Findings that are deltas (e.g. `DurationRegression`) operate on the two slices.

### 3.2 Selection

| Flag | Behaviour |
|---|---|
| *(default)* | Most recent 20 finalised sessions, or all sessions within 14 days, whichever yields fewer. |
| `--runs N` | Most recent N finalised sessions. |
| `--since <sha>` | All sessions from the **oldest** session whose commit matches, to now. The commit is read from `EnvironmentInfo.CustomProperties["Git.SHA"]` (§11.1); matching is a case-insensitive prefix of 4–40 hex characters. Unmatched SHA → exit code 2 with a clear message. |
| `--since <date>` | All sessions with `StartedAt >= date`. |

`--since` is read as a date when it parses as one *and* contains a `-`, `/` or `:`; otherwise as a commit when it is 4–40 hex characters; otherwise it is rejected with exit code 2. The separator requirement keeps a bare hex run such as `20260810` from being taken for a date.

The oldest match anchors `--since <sha>`, not the newest: a commit tested several times should include all of those runs.

`--runs` and `--since` are mutually exclusive.

`CurrentSliceSize = 3`, or 1 if the window contains fewer than 8 sessions.

When the default window's age bound would select nothing — every session is older than `DefaultWindowDays` — the session bound applies instead. A developer returning from leave has old history, which the window boundaries already declare; reporting "no history" would be wrong.

### 3.3 Window is data, not a filter

The resolved window is passed to providers as an object carrying: the sessions, `From`, `To`, `SessionCount`, `CurrentSlice`, `BaselineSlice`, and the resolution method used. Renderers and the JSON envelope report these verbatim. A reader must never have to infer what "recent" meant.

---

## 4. The finding model

### 4.1 Finding

Every output of local analysis is a `Finding`:

| Field | Type | Notes |
|---|---|---|
| `Id` | string | `f_` + stable short hash of (Kind, subject identity, window). Stable across runs on unchanged data. |
| `Kind` | `FindingKind` | §5 |
| `Severity` | `Severity` | §4.3 |
| `EvidenceLevel` | `EvidenceLevel` | §4.4 |
| `Subject` | `FindingSubject` | Either a single test or a group of tests. §4.2 |
| `Evidence` | kind-specific payload | §5, §8 |
| `DrillDownCommand` | string | The exact CLI invocation that expands this finding. Non-optional. |

### 4.2 Subject

A finding is about either one test or a set of tests.

**Single test** carries: `TestFingerprint`, `FullyQualifiedName`, `DisplayName`, `SourceFile`, `SourceLineNumber`, `Assembly`.

`SourceFile` and `SourceLineNumber` are **required in output whenever the SDK populated them**. They are what turns a report into something an agent can act on. Never strip them for brevity.

**Group** carries a `GroupId`, a member count, and the member list (each a single-test reference).

### 4.3 Severity

`Severity ∈ { High, Medium, Low }`, derived from an impact score in [0,1]:

```
impact = 0.40 × unreliability
       + 0.25 × runFrequency
       + 0.20 × blockingRate
       + 0.15 × recency
```

| Term | Definition |
|---|---|
| `unreliability` | Kind-specific, defined per finding in §5. Always in [0,1]. |
| `runFrequency` | executions of this test in window ÷ session count in window, capped at 1. |
| `blockingRate` | fraction of this test's failures that caused their session to have `Failed > 0` at final attempt. |
| `recency` | `0.5 ^ (sessionsSinceLastOccurrence / 5)` |

Bands: `High >= 0.60`, `Medium >= 0.30`, otherwise `Low`.

Group findings use the maximum impact across members, not the mean — one high-impact member makes the cluster worth looking at.

**Severity ceiling.** A provider may declare the most severe band its kind is allowed to reach, and the coordinator caps the banded result at it. Some kinds score highly on every term the formula measures while still not deserving the top of the report — `Vanished` is the case in point (§5.10): a test that ran constantly and then stopped scores `High`, but is usually a deliberate deletion. The ceiling is a per-kind property declared once by the provider, not a per-finding judgement.

### 4.4 Evidence level and the reporting floor

Reuses the Dashboard's bands verbatim, measured in **executions of the subject test within the window**:

| Level | Executions |
|---|---|
| `Low` | < 15 |
| `Moderate` | 15 – 40 |
| `High` | > 40 |

**Reporting floor:** a finding is emitted only if the subject has at least `MinimumExecutionsToReport = 5` executions in a window of at least `MinimumSessionsToReport = 5` sessions. Findings below the floor are **excluded from `findings[]`** and counted in `summary.excludedLowEvidence`.

The floor governs *findings*, not the command. A window of 1–4 sessions still renders a full report — window, context, summary and truncation are all meaningful — with an empty `findings[]` and a populated `excludedLowEvidence`. Exit code 2 is reserved for having no usable sessions at all (§8.4).

Note for implementers: local windows are small. Most local findings will legitimately be `Low` or `Moderate`. That is correct and must be surfaced honestly, not hidden.

### 4.5 Provider abstraction

One provider per finding kind (or per closely-related group of kinds, per §5's ownership column). A provider:

- receives a read-only `AnalysisContext` (window, sessions, shared derived indexes)
- returns findings
- never reads disk, never writes, never calls another provider
- **if it throws, the report still renders.** The failure is recorded in `summary.failedProviders[]` and the command proceeds. One broken metric must not take down `xping report`.

---

## 5. Finding kinds

All kinds are declared in the enum from session 0 onward, including unimplemented ones. Thresholds are named constants (§9).

| Kind | Owner session | Subject |
|---|---|---|
| `RetryMasked` | 1 | test |
| `Flaky` | 2 | test |
| `AlwaysFailing` | 2 | test |
| `SharedFailure` | 2 | group |
| `DurationRegression` | 3 | test |
| `DurationUnstable` | 3 | test |
| `OrderDependent` | 4 (declared) | test |
| `ParallelSensitive` | 4 | test |
| `NetworkDependent` | 5 (declared) | test |
| `Vanished` | 6 | test |
| `NeverRun` | 6 | test |

### 5.1 `RetryMasked`

**Condition:** ≥ 1 execution in the window with `Retry.AttemptNumber > 1 && Retry.PassedOnRetry == true`, **and** the test never contributed a failure to a session's final outcome.

**Rationale:** these tests are invisible in a green build. They are the cheapest genuine flakiness signal available — they require no history at all.

`unreliability` = masked-failure count ÷ execution count.

**Evidence:** masked occurrences with attempt numbers, retry attribute name, cumulative wall-clock spent on retry attempts, `MaxRetries` configured.

### 5.2 `Flaky`

**Condition:** either
- `0 < failureRate < AlwaysFailingRate (0.90)`, or
- `distinctSignatureCount > 1` (failure mode varies between runs)

`unreliability` = `1 - |2 × failureRate - 1|` (peaks at 0.5 failure rate, which is maximally uninformative and therefore maximally disruptive).

### 5.3 `AlwaysFailing`

**Condition:** `failureRate >= AlwaysFailingRate (0.90)` **and** `distinctSignatureCount == 1`.

This is a broken test, not a flaky one. It is reported separately because the remedy is entirely different, and because letting it sit in the flaky bucket is how a real regression gets ignored.

`unreliability` = `failureRate`.

### 5.4 `SharedFailure`

**Condition:** ≥ `SharedFailureMinTests (3)` distinct tests fail with the same signature within the same session.

**Replacement rule:** member tests do **not** additionally appear as individual `Flaky` / `AlwaysFailing` findings *for the failures belonging to that cluster*. Failures of a member test outside the cluster still count normally. This is the rule that turns "47 failures" into "3 causes" and it is the single highest-value behaviour in the report.

Ranked by `memberCount × recency`.

### 5.5 `DurationRegression`

**Condition, all of:**
- normalised p50 increase ≥ `DurationRegressionPct (0.50)`
- absolute p50 increase ≥ `DurationRegressionMinMs (100)`
- baseline coefficient of variation ≤ `DurationStableCvMax (0.50)`
- ≥ 5 baseline executions and ≥ 3 current executions

**Normalisation is required, not optional.** Each execution's duration is divided by the median test duration of its own session before comparison. This cancels machine-level noise — thermal throttling, background builds, a CI runner under load — which on a developer machine is otherwise the dominant signal. An implementation using raw durations is incorrect.

The CV gate exists so that a test with historically huge variance is not reported as having "regressed" when it happens to run slow.

`unreliability` = `min(1, p50IncreasePct / 2)`.

### 5.6 `DurationUnstable`

**Condition:** CV ≥ `DurationUnstableCvMin (0.50)` and baseline p50 ≥ `DurationTrivialMs (50)`.

The floor excludes trivially fast tests, where CV is dominated by scheduler noise and means nothing.

### 5.7 `OrderDependent`

**Condition:** for some predecessor P,
- failure rate given P ≥ `OrderDependentConditionalMin (0.70)`
- unconditional failure rate < `OrderDependentUnconditionalMax (0.30)`
- ≥ `OrderDependentMinPairings (5)` executions preceded by P

**Restriction:** computed over executions with `WasParallelized == false` only. Under parallel execution `PreviousTestId` describes thread-local sequencing and does not imply the state-sharing relationship this finding claims.

**`OrderDependent` is not implementable and is declared only.** Session 4's verification found `PreviousTestId` in better shape than §11.2 suspected — it is the predecessor's `TestFingerprint`, populated by all three adapters, and every one of 3,112 values in the verification store resolved to a test in the same session. The obstacle is the opposite of the one anticipated. NUnit, xUnit and MSTest each execute an assembly in a **deterministic order**, so the test that ran before T is a constant function of T rather than something that varies between runs: across the store's serial universe, all 48 tests had exactly one predecessor, and the predecessor chain was identical in all 23 runs.

That makes the condition unsatisfiable rather than merely unmet. When T has a single predecessor P, every execution of T is a pairing with P, so `failureRate given P ≡ unconditional failureRate` and the two rate gates reduce to `x >= 0.70 && x < 0.30`. Ten `(test, predecessor)` pairs cleared `OrderDependentMinPairings`; none can ever clear the rates.

The only mechanism that varies a predecessor is parallel interleaving, which the restriction above excludes — the restriction removes exactly the case the finding needs. Scoping to a subset of adapters does not help, because deterministic ordering holds on all three. This is not a defect to fix: implementing the kind requires an opt-in randomised execution order in the SDK, at which point the condition becomes meaningful.

**Re-verified at v1.3 and unchanged.** The three SDK defects that blocked §5.8 ([#120](https://github.com/xping-dev/sdk-dotnet/issues/120)–[#122](https://github.com/xping-dev/sdk-dotnet/issues/122)) are fixed, but none of them bore on this section: `PreviousTestId` was never the broken part. Re-measured against a fresh store, every test still had exactly one predecessor.

**§5.7 does not define an `unreliability` formula**, alone among the implementable kinds in §5. The gap is left open deliberately — it is settled by whichever amendment makes the kind implementable, on the data that amendment makes available, rather than chosen now against a measurement nothing produces. Until then the kind stays in the enum, unimplemented.

### 5.8 `ParallelSensitive`

**Condition**, over the test's executions that carry a `TestOrchestrationRecord`, after environmental discounting (§6):

```
m        = median ConcurrentTestCount across those executions   (nearest rank)
low arm  = executions where ConcurrentTestCount <= m
high arm = executions where ConcurrentTestCount >  m
```

- ≥ `ParallelSensitiveMinArmExecutions (5)` executions in **each** arm
- `|failureRate(high) - failureRate(low)| >= ParallelSensitivityDelta (0.30)`

**`unreliability` = `|failureRate(high) - failureRate(low)|`.** In [0,1] by construction, and the same quantity the condition thresholds.

**Split on the level, never on `WasParallelized`.** The boolean is correct since [#120](https://github.com/xping-dev/sdk-dotnet/issues/120), but it is the wrong measurement for this kind and must not be used to form the arms. Concurrency *level* varies freely between runs; the flag derived from it does not, because the variation happens among values that are all `> 1`. Measured at v1.3 over 770 tests × 3 runs of a genuinely parallel assembly: **360 tests ran at more than one concurrency level** (spreads as wide as 8→14), while **not one test was ever in both boolean arms** — 646 were parallel in every run and 123 serial in every run. Binarising throws the signal away.

Splitting at the test's *own* median rather than a fixed boundary is what makes the comparison a property of the test instead of a property of the suite it lives in, and it subsumes the parallel-versus-serial comparison this section originally described: a suite whose parallelisation setting changed inside the window puts its concurrency-1 executions and its concurrency-*n* executions either side of that median automatically, with no special case.

**Either direction qualifies.** The condition is an absolute value, so a test that fails more when it runs nearly alone is reported alongside one that fails more when the suite is crowded — isolation sensitivity is as real a defect as contention, and gating on sign would leave it reported by nothing. The evidence carries the **signed** delta so a reader never has to derive the direction (§8.3).

**Overlaps `Flaky` by design.** A concurrency-sensitive test both passes and fails, so §5.2 will usually claim it too. That is additive, not duplicative: `Flaky` says the test is unreliable, this says under what conditions the unreliability concentrates. No cross-provider suppression is implied.

A test whose concurrency never varied leaves the high arm empty and yields no finding. That is the common case and it is correct — the question was asked and the data answered it.

### 5.9 `NetworkDependent`

**Condition**, over the sessions containing an execution of the test:

```
impaired session = EnvironmentInfo.NetworkMetrics.IsOnline == false
                    || LatencyMs is high against the window's own latency
healthy session   = every other session containing an execution of this test
```

- ≥ `NetworkDependentMinAffectedSessions (3)` impaired sessions containing an execution of this test
- ≥ `NetworkDependentMinAffectedSessions (3)` healthy sessions containing an execution of this test
- `|failureRate(impaired) - failureRate(healthy)| >= NetworkSensitivityDelta (0.30)`

**Granularity limit:** `NetworkMetrics` lives on `EnvironmentInfo`, which is session-scoped. Correlation is therefore session-level only: the arms are formed from sessions, not executions, and every execution within an impaired session falls in the impaired arm regardless of when in the session it ran. Do not construct per-execution network attribution — the data does not support it.

**`NetworkDependent` is not implementable and is declared only.** Session 5's verification found three defects. The first two are fixable and the third is not.

**Coverage.** `EnvironmentInfo.NetworkMetrics` is null on every session a zero-config user records, and not because a probe failed. `ResolveMode()` returns `LocalOnly` whenever no credentials are configured, and `AddXpingEnvironment` then force-sets `CollectNetworkMetrics = false` — deliberately, because the collector performs a DNS lookup and four sequential ICMP pings per session build and local-only mode must not touch the network. All 19 sessions of the verification store carried no network state at all (§11.2). The kind could therefore only ever exist for users who configured credentials.

**The percentile was unsatisfiable.** Nearest rank takes index `ceil(0.9n) - 1`, so with a strictly-greater boundary at most `n - ceil(0.9n)` sessions can be impaired by latency: 0 for `n ≤ 9`, 1 for `n ∈ [10, 19]`, 2 for `n ∈ [20, 29]`, and 3 only at `n ≥ 30`. `NetworkDependentMinAffectedSessions` is 3 and `DefaultWindowSessions` is 20, so on the default window the latency limb could never fill the impaired arm however bad the network was. Measured against the real probe target, it was worse still: the collector truncates its four-ping average to an `int`, and twenty samples spanning 25.0–30.4 ms produced a p90 of 30 with three values tied at 30, leaving nothing strictly above it. A relative threshold (a multiple of the window's median, with an absolute floor) repairs this, and was drafted at v1.5 before the third defect withdrew the section.

**The comparison measures nothing, and that is the one that cannot be repaired.** Two independent problems compound.

*The arms cannot isolate the network.* An impaired arm is a set of whole sessions, so it does not select for network conditions — it selects for **those particular runs**. Every other property of them is perfectly correlated with the split: a laptop on battery, a build running alongside the suite, thermal throttling, an upstream dependency having a bad hour. A test that never opens a socket is fully eligible for the finding, and nothing in the data distinguishes it from one that does. This is the opposite of §5.8's situation, where concurrency varies *within* a run and the arms are therefore not confounded with when the run happened.

*The gate has no statistical power at the sizes it operates on.* Because the minimum arm is three **sessions**, `|delta| >= 0.30` reduces to "a one-failure difference". At the realistic operating point — impairment is rare, so the impaired arm sits at the minimum while the healthy arm holds the rest of a 20-run window — a **single failure in the entire window fires the finding** if it happens to land in an impaired session. For a test with no network dependence whatsoever, failing independently at rate `p`:

| test fails | fires anyway (3 impaired, 17 healthy) |
|---|---|
| 10% of runs | 7.2% |
| 20% of runs | 16.3% |
| 30% of runs | 31.4% |

In a 400-test suite with twenty mildly flaky tests, that is roughly 1.4 fabricated findings per report — and they land on precisely the tests a reader is most likely to be looking at. Raising the arm minimum to where the delta carries information needs about fifteen sessions a side, which a window capped at `DefaultWindowSessions (20)` cannot supply.

A tightened rule — failed in *every* impaired session and in *no* healthy one — does have power, at under 0.02% false positives across the same range. It was rejected because it does not touch the first problem: it reports a striking coincidence over three sessions and labels it with a cause, which is what §1.2 and §8.3 exist to prevent.

Implementing the kind requires a signal the store does not carry: whether the test under analysis uses the network at all. That is an SDK change, not an analysis one. Until then the kind stays in the enum, unimplemented.

**§5.9 does not define an `unreliability` formula.** The absolute failure-rate delta drafted at v1.4 is withdrawn with the condition it thresholded. The gap is left open on the same terms as §5.7's: it is settled by whichever amendment makes the kind implementable, on the data that amendment makes available.

### 5.10 `Vanished` / `NeverRun`

- `Vanished` — fingerprint present in ≥ `VanishedMinBaselineSessions (3)` sessions of the baseline slice, absent from every session of the current slice. `unreliability` = baseline appearances ÷ baseline session count.
- `NeverRun` — `TotalTestsExpected - Executions.Count > 0` for a session, or a test with `Outcome == NotExecuted`.

Both are reported at `Low` severity, enforced by the severity ceiling (§4.3). A test that silently stopped running is a finding, but it is usually a deliberate deletion.

**`NeverRun` is not implementable and is declared only.** Verification found `TotalTestsExpected` is `null` on every run — `XpingContextOrchestrator.GetTotalTestsExpected()` returns `null` and no adapter overrides it — and that no adapter emits `TestOutcome.NotExecuted` as "expected but did not run"; it appears only as the fallback arm of outcome-mapping switches. Implementing it requires an SDK change to populate the expected count from each framework's discovered-test list. Until then the kind stays in the enum, unimplemented.

**`Vanished` is session 0's reference provider.** It is implementable from the session tier with no new fields and exercises the whole pipeline — window slices, shared index, reporting floor, severity ceiling, ranking, both renderers — which is what makes the seams verifiable before any real metric lands.

**A vanished fingerprint is treated as deleted, never as renamed.** `TestFingerprint` is defined (§11.2) to change whenever a test's fully-qualified name or parameters change, by design — a rename is, for this purpose, a new test with no history rather than a continuation of the old one under a new identity. `Vanished` therefore never attempts rename detection against the current slice's new fingerprints, and none should be added: doing so would require guessing at name similarity, which is exactly the kind of causal inference §1.2 and §8.3 rule out. A renamed test is correctly reported as one `Vanished` finding for the old name and, once it clears the reporting floor again, ordinary low-evidence silence for the new one — not a single correlated finding.

---

## 6. Environmental session discounting

A session is marked `IsLikelyEnvironmental` when its failure rate ≥ `EnvironmentalSessionFailureRate (0.30)` **and** ≥ `EnvironmentalSessionMinFailures (10)` tests failed.

Session 2 computes and exposes this flag on the analysis context. Later sessions consume it. Without it, one broken Docker daemon poisons every test's history and the whole report becomes noise.

Discounting rule: executions from environmental sessions are excluded from `failureRate` denominators **and** numerators for `Flaky` / `AlwaysFailing` / `ParallelSensitive`, but retained for `SharedFailure` (where they are precisely the signal). The count of discounted sessions appears in `summary.environmentalSessions`.

`ParallelSensitive` (§5.8) is included because an outage lands in whichever concurrency arm its sessions happen to occupy and manufactures a delta out of a bad afternoon — the arms are formed from a per-test median, so nothing about the split protects against it.

The rule governs failure-rate numerators and denominators. It does not govern **baselines**: §5.5's per-session duration median is computed over every session, because it describes the machine the suite ran on and remains a true measurement whether or not the suite fell over.

---

## 7. Failure signatures

### 7.1 Composition

`signature = stableHash(exceptionType + normalisedMessage + topUserFrames)`

The readable components are stored alongside the hash. The hash exists only for grouping; renderers and LLM consumers need the readable form.

### 7.2 Message normalisation

Applied in this order:

1. Trim; collapse internal whitespace runs to a single space
2. GUIDs → `<guid>`
3. Absolute file paths and URIs → `<path>` / `<uri>`
4. ISO-8601 timestamps and common date/time formats → `<time>`
5. Hex literals and long hex runs → `<hex>`
6. Standalone integers and decimals → `<num>`
7. Lowercase

**Do not normalise:** type names, member names, or quoted string literals containing no digits. Those carry the diagnostic signal — normalising them collapses genuinely different failures into one signature.

### 7.3 Frame extraction

Top `SignatureFrameCount (5)` frames from assemblies belonging to the test project or its transitive project references. Method signature only — no file paths, no line numbers (those vary with edits and would fragment signatures).

If zero user frames remain, fall back to the top 5 frames of any origin and set `degradedSignature: true`.

### 7.4 Local analysis does not use the SDK hashes

`ErrorMessageHash` and `StackTraceHash` exist for cloud upload, where raw text cannot be sent. Locally, the raw `ErrorMessage` and `StackTrace` are available and are what we normalise. Using the SDK hashes locally discards the entire advantage of running on the developer's machine.

---

## 8. Output contract

### 8.1 Envelope

```json
{
  "schemaVersion": "1.0",
  "window": {
    "from": "2026-08-03T09:12:00Z",
    "to": "2026-08-10T16:40:00Z",
    "sessionCount": 14,
    "resolution": "default",
    "currentSliceSize": 3,
    "sessionIds": ["..."]
  },
  "context": {
    "sha": "a3f9c2e",
    "branch": "main",
    "assembly": "MyApp.Tests"
  },
  "summary": {
    "tests": 1284,
    "findings": 5,
    "healthy": 1279,
    "excludedLowEvidence": 41,
    "environmentalSessions": 1,
    "incompleteSessions": 0,
    "unreadableSessions": 0,
    "failedProviders": []
  },
  "findings": [ /* §8.2 */ ],
  "truncated": {
    "shown": 5,
    "total": 5,
    "command": "xping report --all --format json"
  }
}
```

`context` is null when nothing is known about the revision (§11.1). It is never fabricated. There is deliberately no `dirty` field — see §11.1.

`window` additionally carries `resolutionArgument`: the flag value that produced the window, or null for the default. Without it a reader can see that `--runs` was used but not what was asked for.

### 8.2 Finding

```json
{
  "id": "f_2a91",
  "kind": "DurationRegression",
  "severity": "high",
  "evidenceLevel": "moderate",
  "subject": {
    "type": "test",
    "fingerprint": "…",
    "fullyQualifiedName": "MyApp.Tests.ReportingTests.GenerateMonthlySummary",
    "displayName": "GenerateMonthlySummary",
    "sourceFile": "Reporting.Tests/ReportingTests.cs",
    "sourceLineNumber": 41,
    "assembly": "MyApp.Tests"
  },
  "evidence": {
    "current":  { "p50Ms": 1240, "p95Ms": 1890, "executions": 4 },
    "baseline": { "p50Ms": 340,  "p95Ms": 410,  "executions": 10 },
    "delta":    { "p50Pct": 264.7, "p50Ms": 900 },
    "normalisedDelta": { "p50Pct": 251.2 },
    "baselineCv": 0.11,
    "firstSeenAt": "a3f9c2e",
    "exemplars": [
      { "durationMs": 1198, "sha": "a3f9c2e", "outcome": "Passed", "startedAt": "…" }
    ],
    "contrast": { "durationMs": 338, "sha": "9c1de40", "outcome": "Passed", "startedAt": "…" }
  },
  "drillDown": "xping test GenerateMonthlySummary --format json"
}
```

### 8.3 Universal evidence rules

| Rule | Detail |
|---|---|
| Denominators always | Never `"passRate": 0.87` alone. Always accompanied by the execution and session counts it was computed from. |
| Deltas pre-computed | Emit `current`, `baseline`, `delta`. Consumers must never have to derive a trend from an array — that is where an LLM invents a direction. |
| Contrast exemplar | Any finding about a *change* includes one exemplar of the prior behaviour. The pair is what makes the difference reasonable about. |
| Exemplar budget | ≤ 3 exemplars per finding. Raw message text up to `ExemplarCharBudget (500)`, then elided with an explicit marker. |
| Observations, not causes | `"distinctSignatures": 3` is an observation. `"cause": "shared static state"` is a verdict — never emit one. Classification into a `kind` against a defined threshold is permitted; free-form causal prose is not. |
| No nulls for absent analysis | If a provider could not evaluate a test, it emits no finding. It does not emit a finding with null evidence. |
| `drillDown` on every finding | Non-optional. It is how both a human and an agent navigate without documentation. Every emitted command must be one the tool accepts **today** — a drill-down that fails is worse than none, because it is the field a reader is most likely to run. |

### 8.4 Command surface and exit codes

| Flag | Behaviour |
|---|---|
| `--runs N` | Window size (§3.2). Alias: `--last`, retained for scripts written against the earlier surface. |
| `--since <sha\|date>` | Window start (§3.2). Mutually exclusive with `--runs`. |
| `--top N` | Findings to show; default `DefaultTopFindings (10)`. Mutually exclusive with `--all`. |
| `--all` | Show every finding. This is the meaning `truncated.command` refers to — **not** "every assembly". |
| `--kind <Kind>...` | Restrict to one or more `FindingKind` values. An unknown value is rejected naming the kinds that exist. |
| `--format text\|json` | Output format; default `text`. Alias: `--json`. |
| `--fail-on high\|medium\|low\|none` | Least severity that fails the command; default `none`. |
| `--assembly <name>` | Scope to one test assembly. Defaults to the newest assembly in the store, announced in the text report. |
| `--directory <path>` | Resolve the store from here. |
| `--ascii` | Force the ASCII glyph set. |

Exit codes:

| Code | Meaning |
|---|---|
| 0 | A report was produced and nothing reached `--fail-on`. Includes a valid report with an empty `findings[]`. |
| 1 | A report was produced and at least one finding reached `--fail-on`. Judged against **every** finding produced, not only those shown — `--top` must not decide whether a build fails. |
| 2 | No report could be produced: no store, no readable sessions, an unmatched `--since <sha>`, an uninterpretable `--since`, or any parse error. |

The 1/2 distinction is load-bearing: a build step has to tell "I looked and found problems" apart from "I could not look".

Warnings — unreadable files, failed providers — go to **stderr**, so `--format json` on stdout stays parsable.

---

## 9. Constants

All thresholds live in a single `internal static class LocalAnalysisConstants`, each with an XML doc comment stating the chosen value's justification. No configuration surface in v1 (§1.2).

Values are **provisional but binding**: an implementer who thinks a value is wrong raises it as a spec amendment (§12) rather than changing it inline.

| Constant | Value | §  |
|---|---|---|
| `MinimumSessionsToReport` | 5 | 4.4 |
| `MinimumExecutionsToReport` | 5 | 4.4 |
| `DefaultWindowSessions` | 20 | 3.2 |
| `DefaultWindowDays` | 14 | 3.2 |
| `CurrentSliceSize` | 3 | 3.2 |
| `AlwaysFailingRate` | 0.90 | 5.3 |
| `SharedFailureMinTests` | 3 | 5.4 |
| `DurationRegressionPct` | 0.50 | 5.5 |
| `DurationRegressionMinMs` | 100 | 5.5 |
| `DurationStableCvMax` | 0.50 | 5.5 |
| `DurationUnstableCvMin` | 0.50 | 5.6 |
| `DurationTrivialMs` | 50 | 5.6 |
| `OrderDependentConditionalMin` | 0.70 | 5.7 |
| `OrderDependentUnconditionalMax` | 0.30 | 5.7 |
| `OrderDependentMinPairings` | 5 | 5.7 |
| `ParallelSensitivityDelta` | 0.30 | 5.8 |
| `ParallelSensitiveMinArmExecutions` | 5 | 5.8 |
| `NetworkDependentMinAffectedSessions` | 3 | 5.9 |
| `NetworkSensitivityDelta` | 0.30 | 5.9 |
| `EnvironmentalSessionFailureRate` | 0.30 | 6 |
| `EnvironmentalSessionMinFailures` | 10 | 6 |
| `SignatureFrameCount` | 5 | 7.3 |
| `ExemplarCharBudget` | 500 | 8.3 |
| `SeverityHighThreshold` | 0.60 | 4.3 |
| `SeverityMediumThreshold` | 0.30 | 4.3 |
| `SmallWindowSessionCount` | 8 | 3.2 |
| `EvidenceModerateExecutions` | 15 | 4.4 |
| `EvidenceHighExecutions` | 40 | 4.4 |
| `RecencyHalfLifeSessions` | 5 | 4.3 |
| `DefaultTopFindings` | 10 | 8.4 |
| `VanishedMinBaselineSessions` | 3 | 5.10 |

---

## 10. Determinism

Two invocations over an unchanged store **must** produce byte-identical JSON.

This requires:

- Total, stable ordering everywhere. Every sort has an explicit tiebreaker, ultimately `TestFingerprint` ordinal.
- No dependence on `DateTime.Now` in analysis. The only clock read is at window resolution for `--since <date>`, and the resolved boundary is then carried as data.
- No dependence on dictionary or set enumeration order.
- Stable hashing — not `string.GetHashCode()`, which is randomised per process in .NET Core.
- Floating-point values rounded to a fixed precision before serialisation (percentages to 1 decimal, rates to 3).

Determinism is a testable property. Session 0 ships the test; every later session extends it.

---

## 11. Known data gaps

Implementers must verify rather than assume. These are the known-suspect areas.

### 11.1 The local commit anchor (corrected in v1.1)

`TestSession` carries `PullRequestContext`, which exists only in PR builds. But a local commit anchor **does** exist: `EnvironmentDetector.CollectLocalGitMetadata` reads `.git/HEAD` directly and populates `Git.SHA`, `Git.Branch`, `IsDetachedHead` and `HasStagedChanges` into `EnvironmentInfo.CustomProperties`, which the session tier persists verbatim.

`--since <sha>` and a non-null `context` are therefore implemented against those properties, and `DurationRegression.firstSeenAt` is available to session 3. No `RevisionContext` type is needed on `TestSession`, and `PullRequestContext`'s validation must still not be relaxed.

Two real limits remain:

- **CI runs carry no commit.** Collection is gated on *not* running in CI (`!ciPlatform.HasValue`), so a session recorded on a CI agent has no `Git.SHA` and can never match `--since <sha>`. The failure message says so.
- **There is no `dirty` flag, and none is emitted.** The SDK records `HasStagedChanges`, which answers a different question — a working tree full of unstaged edits reports clean. A field that is wrong in the common case is worse than one that is absent, so `context.dirty` does not exist (§8.1).

### 11.2 Fields that may not be populated by all adapters

Each is verified in its owning session before any analysis is written on top of it. A verification result of "this field is not reliably populated" is a **successful** outcome that scopes the finding down — not a blocker to work around.

| Field | Suspicion |
|---|---|
| `Retry.PassedOnRetry`, `Retry.AttemptNumber` | Requires each of NUnit/xUnit/MSTest adapters to hook retry attributes. At least one likely does not. |
| `TestOrchestrationRecord.PreviousTestId` | **Verified v1.2, re-confirmed v1.3: populated and resolvable, but constant per test.** It is the predecessor's `TestFingerprint`, written by all three adapters, and all 3,112 values in the verification store resolved to a test in the same session. The recorded suspicion was not the problem: all three frameworks order an assembly deterministically, so each test has exactly one predecessor and the chain repeats run for run. Blocks `OrderDependent` (§5.7), which needs the predecessor to vary. |
| `TestOrchestrationRecord.WasParallelized` / `ConcurrentTestCount` | **Verified v1.3: fixed and usable.** v1.2 found the count was of distinct worker keys ever seen, never decremented, so the flag latched true at the second test class even with parallelisation disabled. [#120](https://github.com/xping-dev/sdk-dotnet/issues/120) tracks in-flight tests instead and is merged. Re-measured: `ConcurrentTestCount` spans 1…14 over 2,313 executions and varies run to run for 360 of 770 tests. `WasParallelized` is now correct but remains the wrong measurement for §5.8 — see that section. |
| `TestOrchestrationRecord.CollectionName` | **Verified v1.3: fixed.** v1.2 found it null on every xUnit execution, the sink having passed the collection as `workerId` and omitted the `collectionName` argument. [#121](https://github.com/xping-dev/sdk-dotnet/issues/121) is merged; re-measured, all three adapters populate it. |
| `ErrorMessage`, `StackTrace` | Confirm populated for failures in all three adapters; confirm `ExceptionType` is the real cause and not a wrapper such as `TargetInvocationException`. |
| `TestIdentity.SourceFile` / `SourceLineNumber` | **Verified v1.1: never populated.** No adapter passes them to `ITestIdentityGenerator.Generate`; the xUnit sink puts a `SourceFile` entry into `TestMetadata.CustomAttributes` instead. §4.2's requirement is satisfied vacuously — they are emitted whenever present, and they are never present. Making a report navigable needs an adapter change. |
| `QuickStatistics` | **Verified v1.1: populated for local runs.** `BuildSessionAsync(isFinalizing: true)` sets it regardless of mode; it is not gated on `Connected`. |
| `EnvironmentInfo.NetworkMetrics` | **Verified v1.5: null on every session of a zero-config store, by design rather than by failure.** All 19 sessions of the verification store carried no `networkMetrics` at all. `ResolveMode()` returns `LocalOnly` whenever no credentials are configured, and `AddXpingEnvironment` then force-sets `CollectNetworkMetrics = false` — the collector performs a DNS lookup and four sequential ICMP pings per session build, which a mode whose contract is that it does not touch the network must not do. `EnvironmentDetector` returns null before the collector is reached. The property's own default is `true`, so the suppression is invisible from the model. Populated in `Connected` mode only, which scopes §5.9 to users who configured credentials. Independently, `LatencyMs` can be null while `IsOnline == true`: it is measured only when online, nulled on any throw, and nulled when all four probes fail — reachable on a host that blocks ICMP *and* filters TCP 443, which the fallback path uses. `EnvironmentDetector` is a singleton caching by endpoint, so the probe runs once per process and the value is genuinely one per session. |
| `TestSession.TotalTestsExpected` | **Verified v1.1: always null.** `GetTotalTestsExpected()` returns null and no adapter overrides it. Blocks `NeverRun` (§5.10). |
| `TestIdentity.TestFingerprint` | **Verified v1.1: stable for plain and parameterized tests, not across renames.** It is SHA-256 of `"{FQN}\|{paramHash}"`, so renaming or moving a test breaks its history; `[XpingFingerprint]` is the escape hatch. It does **not** include the assembly, so identical FQNs in two assemblies collide — any cross-assembly grouping must key on `(assembly, fingerprint)`. A parameter whose `ToString()` is identity-based or time-derived produces a new fingerprint every run, which surfaces as a `Vanished` finding. |

### 11.3 Absent by design (candidates for a later SDK change)

Process resource counters (peak working set, gen-2 GC count) would turn "this test got slower" into "this test started allocating". Not in scope for v1; noted so that no session invents a substitute.

---

## 12. Amendment protocol

1. An implementer who finds a section wrong stops and reports, citing the section number.
2. The spec is amended, `Version` is incremented, and the change is noted in §13.
3. Affected sessions restart from the amended spec.

Sessions never resolve a spec conflict locally. A silently adapted spec is how six sessions end up with three incompatible severity models.

---

## 13. Changelog

| Version | Change |
|---|---|
| 1.0 | Initial specification. |
| 1.1 | Session 0 verification and the amendments it forced. **§2.1/§2.2** — the store has two tiers; §2.2's "one `TestSession` per file" now describes the new `sessions/` tier and no longer contradicts §2.1's `runs/` layout. Analysis reads whole sessions, because the slim projection cannot carry §4.2, §5 or §7. **§2.2** — partial sessions are not persisted at all, so `incompleteSessions` reads 0; empty sessions are skipped without being called unreadable. **§3.2** — `--since` disambiguation, oldest-match anchoring, `--runs`/`--since` exclusivity, and the age-bound fallback. **§4.3** — severity ceiling. **§4.4** — the reporting floor governs findings, not the command. **§5.10** — `NeverRun` is declared but unimplementable (`TotalTestsExpected` is always null); `Vanished` is session 0's reference provider. **§8.1** — `context.dirty` removed; `window.resolutionArgument` added. **§8.4** — command surface and exit codes, replacing the earlier `report` flags; `--all` now means "do not truncate". **§9** — constants that were used but unlisted. **§11.1** — corrected: a local commit anchor already exists in `EnvironmentInfo.CustomProperties`, so `--since <sha>` and `context` are supported; CI runs carry no commit and `dirty` is not collected. **§11.2** — verification results recorded for source locations (never populated), `QuickStatistics` (populated), `TotalTestsExpected` (always null) and fingerprint stability. |
| 1.2 | Session 4 verification and the amendments it forced. **§5.7** — `OrderDependent` is declared but unimplementable: `PreviousTestId` is well populated and resolvable, but all three frameworks order an assembly deterministically, so each test has exactly one predecessor and the conditional and unconditional failure rates are the same number — the two rate gates cannot both be satisfied. Needs an opt-in randomised execution order in the SDK, not a bug fix. **§5.8** — `ParallelSensitive` is declared but unimplementable: `WasParallelized` counts worker keys ever seen rather than tests in flight, so arm membership is an artefact of report order and no test in the verification store had five executions in both arms. Blocked on [#120](https://github.com/xping-dev/sdk-dotnet/issues/120). **§5** — the kinds table marks both as declared. **§5.7/§5.8** — the missing `unreliability` formulae are left open deliberately, to be settled by whichever amendment makes either kind implementable. **§11.2** — verification results recorded for `PreviousTestId` (populated but constant per test), `WasParallelized`/`ConcurrentTestCount` (does not measure concurrency) and `CollectionName` (null on xUnit, [#121](https://github.com/xping-dev/sdk-dotnet/issues/121)). Also filed, outside this document's scope: [#122](https://github.com/xping-dev/sdk-dotnet/issues/122), an unsynchronised shared builder in `ExecutionTracker`. |
| 1.3 | Session 4 resumed after [#120](https://github.com/xping-dev/sdk-dotnet/issues/120)–[#122](https://github.com/xping-dev/sdk-dotnet/issues/122) were fixed. **§5.8** — `ParallelSensitive` is implementable again and is redesigned: the arms are now formed by splitting a test's executions at its own median `ConcurrentTestCount`, never on `WasParallelized`, because concurrency level varies between runs while the boolean derived from it does not (360 of 770 tests varied by level; none was ever in both boolean arms). Its `unreliability` formula is defined, and either direction of the delta qualifies. **§5.7** — `OrderDependent` re-verified and unchanged; still declared only, and its `unreliability` gap stays open. **§6** — discounting extended to `ParallelSensitive`. **§9** — `ParallelSensitiveMinArmExecutions` added. **§11.2** — `WasParallelized`/`ConcurrentTestCount` and `CollectionName` re-verified as fixed. |
| 1.7 | Pre-session-6 amendment. **§5.10** — records that a vanished fingerprint is treated as a deleted test, never as a renamed one: `Vanished` performs no rename detection against the current slice, by design, because `TestFingerprint` (§11.2) is defined to change on any name or parameter change, and inferring a rename from name similarity would be exactly the causal guessing §1.2/§8.3 rule out. No code changes — `VanishedProvider` (shipped in session 0) already behaves this way; this closes the open question session 0 left for a later session. |
| 1.6 | Session 5 concluded. **§5.9** — `NetworkDependent` is declared but unimplementable, and v1.5's repair is withdrawn along with the condition it repaired. Fixing the latency threshold fixed a symptom: the comparison itself measures nothing. The arms are whole sessions, so an impaired arm does not select for network conditions but for *those particular runs*, and every other property of them — a laptop on battery, a build running alongside the suite, an upstream dependency having a bad hour — is perfectly correlated with the split; a test that never opens a socket is fully eligible. Compounding it, a three-**session** arm makes `NetworkSensitivityDelta` reduce to "a one-failure difference", so at the realistic operating point a single failure in the whole window fires the finding, and a test with no network dependence at all fires 7.2%/16.3%/31.4% of the time when it fails 10%/20%/30% of runs — roughly 1.4 fabricated findings per report in a 400-test suite. Restoring power needs about fifteen sessions a side, which `DefaultWindowSessions (20)` cannot supply. Implementing the kind requires the store to carry whether a test uses the network at all, which is an SDK change. Its `unreliability` formula is withdrawn and left open on §5.7's terms. **§5** — the kinds table marks it declared. **§6** — `NetworkDependent` removed from discounting, which only applied to it as an implemented kind; the clarification that the rule does not govern baselines is kept for §5.5. **§9** — `NetworkImpairedLatencyMultiple` and `NetworkImpairedMinLatencyMs` removed with the design that introduced them. **§11.2** — the `NetworkMetrics` verification result is retained; it is what scoped the kind down. |
| 1.5 | Session 5 verification and the amendments it forced. **§5.9** — the p90 latency threshold is replaced by a relative-plus-absolute one (`NetworkImpairedLatencyMultiple` × the window's median `LatencyMs`, floored at `NetworkImpairedMinLatencyMs`). The percentile was unsatisfiable: nearest rank with a strictly-greater boundary admits at most `n - ceil(0.9n)` sessions, which is 2 at the default window of 20 against a gate of 3, so the latency limb could never fill the impaired arm. A rank is the wrong instrument for an absolute physical condition. Also settled in the same section: exemplars come from the arm with the higher failure rate rather than from the impaired arm by name, since the condition's absolute value admits either direction and the impaired arm may hold no failures; a `NetworkMetrics` record with `IsOnline == null` and `LatencyMs == null` is excluded from both arms rather than treated as healthy; and the kind is recorded as reachable in `Connected` mode only. **§6** — discounting extended to `NetworkDependent`, whose impaired arm collects an outage's collateral failures by construction; the rule is also stated not to govern baselines. **§9** — `NetworkImpairedLatencyMultiple` and `NetworkImpairedMinLatencyMs` added. **§11.2** — `EnvironmentInfo.NetworkMetrics` verified null on every session of a zero-config store, because `LocalOnly` mode suppresses collection; `LatencyMs` independently nullable while online. |
| 1.4 | Pre-session-5 amendment. **§5.9** — `NetworkDependent` was missing an `unreliability` formula and a named threshold constant, alone among the implementable kinds; both gaps are closed before implementation rather than left for the session to invent. The condition is redesigned as a two-arm comparison in the shape of §5.8: sessions split into impaired (`IsOnline == false` or `LatencyMs` above the window's p90) versus healthy, each arm requiring a minimum size, `unreliability` = the absolute failure-rate delta between them. **§9** — `NetworkDependentMinAffectedSessions` and `NetworkSensitivityDelta` added. |
