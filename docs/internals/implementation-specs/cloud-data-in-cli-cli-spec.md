# Cloud data in the CLI — CLI implementation specification

**Status:** Draft for review, written 2026-11-01 from the CLI inventory of the same day.
**Applies to:** `xping-dev/sdk-dotnet`, `src/Xping.Cli/**` and `tests/Xping.Cli.Tests/**`. No change
in `src/Xping.Sdk.Core` (§14).
**Implements:** `xping-dev/dashboard:specs/cloud-data-in-cli/cloud-data-in-cli-contract-spec.md`,
approved 2026-11-01. Section references written as "contract §n" point at that document. Bare "§n"
points at this one. "cli-auth §n" points at `cli-auth-cli-spec.md` in this folder.
**Sibling:** the Cloud implementation spec in `xping-dev/dashboard`
(`specs/cloud-data-in-cli/cloud-data-in-cli-cloud-spec.md`), written from the same contract.
**Schema:** `1.27` → `1.28` (§6).
**Supersedes, in part:** cli-auth §10 and §11 (the Cloud API client and report enrichment), and the
Cloud amendments of four report specs (§15). Those sections stay in their documents with a pointer
here.

## Amendment protocol

This document says HOW the CLI implements the contract. It does not change WHAT crosses the wire
or what a value means. If a statement here contradicts the contract, the contract wins and this
document is amended. If a statement here contradicts the codebase, or cannot be implemented as
written: **stop and report.** Do not adapt in code. §19 lists the open questions; an
implementation session that needs one of them answered stops and asks.

The key words MUST, MUST NOT, SHOULD, SHOULD NOT and MAY are to be read as in RFC 2119.

### Amendment 1 — from phase 1

Phase 1 read §3 against the code and found two conflicts. Both are resolved here, decided by the
owner on 2026-11-02, and committed before the code that depends on them.

| # | Was | Now | Changed |
|---|---|---|---|
| A | §3.3: the credential slice covers a proactive token refresh, with the cross-process lock's 5 s `Patience` cut by the slice | The refresh is not part of the credential step. `BearerTokenHandler` refreshes inside the first DataGateway request, so it runs under that request's `AttemptTimeout` (3 s) and the total budget, not the 1 s slice. The slice covers credential resolution only: settings, the keychain probe, choosing a credential. `CrossProcessLock.Patience` drops from 5 s to 2 s, so the "proceed without the lock" rule (cli-auth §9.4) can still apply inside a 3 s attempt. A refresh that does not finish in time costs that run's Cloud data (`unavailable`, `timeout`); the refresh itself runs to the end in the background as today. Putting the refresh under the 1 s slice was rejected: a network refresh often takes longer, and the first report after every 15-minute token lifetime would read `credential-timeout`. **Contract §7.3 lists token refresh under `CredentialBudgetMs`; it needs the matching amendment in the dashboard repo** | §3.3, cli-auth §9.4 |
| B | §3.2: the retry keeps `HttpClientResiliencePredicates.IsTransient` (5xx, 408, network errors, the attempt timeout) | Contract §7.3 wins: one retry on **5xx only**, and only when `budget.HasAtLeast(1 s)`. 408, network errors and the attempt timeout are not retried; after a 3 s attempt timeout at most 1 s is left anyway. The retry waits `CloudResilience.RetryDelay` = 200 ms, no backoff, no jitter | §3.2 |
| C | §2.2: the digest DTO is `Digest`, in `Cloud/Digest/` | `TestDigest`: a type named like its namespace (`Xping.Cli.Cloud.Digest`) cannot be referred to by its simple name inside `Xping.Cli.Cloud` | §2.2 |
| D | §19 Q-2: verify on macOS whether the keychain probe tolerates the slice's cancellation | It does not: `MacOsKeychainStore` probes and reads with synchronous `SecItemCopyMatching` calls that never see the token, so a keychain waiting on an access prompt would hold the report. As Q-2 prescribed, the slice applies around the resolution: `CloudEnricher` runs `CredentialResolver.ResolveAsync` on the thread pool and stops waiting when the slice ends. The abandoned call ends with the process | §3.3, §19 |
| E | §2.2: `GetProjectAsync` stays | Removed. Its one caller checked a configured key that found no tests; the digest's 404 is that answer now (contract §6.1), so nothing calls it | §2.2 |

### Amendment 2 — from phase 2

Phase 2 found no conflict with the contract or the code. It found cases §3–§6 leave open, and
decided them here before the code that depends on them.

| # | Was | Now | Changed |
|---|---|---|---|
| A | §3.4: `not-attempted` for "no credential" | Also `not-attempted` when a credential exists and there is nothing to ask (no single-test subject, a clean run): the digest needs 1–100 tests. `credential` and `workspaceId` are then filled. Settings that cannot be read are `not-attempted` with `cloudUrl` null and the settings error as the hint. A defect caught by the fail-open wrapper is `unavailable` / `none`. `elapsedMs` is 0 whenever nothing was asked, so the local-layer tests of §8.3 stay deterministic | §3.4, §3.5 |
| B | §3.4: the signal table names exceptions loosely | `CloudApiClient` answers a 5xx after the retry with `CloudApiException` (it was `AuthFailureException` "HTTP 503"), so `server` is told from `network` by type. A 401 on an API key is `login-required` (no sign-in to be over; the key was refused), with its own hint naming the key rather than the sign-in. A fallback key that answers nothing, for whatever reason, leaves the report `login-required`. A plain 401 on a stored login, without `invalid_token`, costs its batch as `network` (a proxy's page: nothing says the sign-in is over, cli-auth §8.2). 426 is `server`. `login-required` and `forbidden` end every batch of the attempt; every other failure costs only its batch | §3.4 |
| C | §4.2: hashes and the latest session | A hash is sent only in the shape the digest accepts (64 lowercase hex); a fingerprint longer than 128 is not a target. Either would cost its whole batch a 400 (contract §6.1), and the store is a file anyone can edit. "The latest local session" is every session sharing the newest session's key (one host run writes one per assembly). A test's final attempt is its highest attempt number, the latest start on a tie | §4.1, §4.2 |
| D | §4.3: session keys per request | They ride on each project's first batch only: the answer is the same for every batch. Assemblies bound to one project key are one project, asked once. `MaxEnrichedTests`, `MaxHashesPerTest` and the new `MaxFingerprintLength` = 128 live on `TargetPlanner`, which applies them | §3.2, §4.3 |
| E | §5.3: `Match(string? errorMessageHash, CloudDigestDto? digest, string? algorithmReason)` | `Match(…, bool algorithmMismatch)`: the matcher needs whether, not why | §5.3 |
| F | §6.3: the digest mirrors contract §6.2 one to one | `CloudDigestDto` leaves out `fingerprint`: the subject beside it carries it. An item in a state other than `ok` or `unknown`, or without a state, is stitched as `unknown` and makes its project `partial` / `invalid-response` (contract F4). `CloudApiClient` therefore rejects a digest only when it has no `tests` list; an item it cannot read is the enricher's to attribute. A requested fingerprint the answer leaves out is lost as `invalid-response` | §6.3, §2.2 |
| G | §6.5: `runsState` | A test Cloud does not know has no runs: `ok`, empty, nothing asked; so is a runs 404. A runs request that fails is `unavailable` (`budget` when the total ran out) and never changes `context.cloud`. Without a credential, `test.cloud` is present with `runsState` `not-attempted` | §6.5, §3.3 |
| H | §3.4, §11: the hint | Built once from the outcome. `login-required` wins even when the API key answered in its place, because the next run without the key would show nothing. `partial` reads `Cloud data partial: {n} of {m} tests answered ({reason}).` | §3.4, §11 |
| I | §3.4: precedence | When several assemblies disagree and nothing answered, the row with the higher status by the precedence wins, `no-project` over `unavailable`. When something answered and a row is not `ok`, the report is `partial` with the first such row's reason (contract §9.4) | §3.4 |
| J | §4.1: step 2 takes every ranked finding | Under `--id` and `--test` the page holds the selection only, so step 2 takes the ranked findings the envelope still shows. Asking about up to 200 tests nobody sees would spend the budget the subject's run history needs (§3.3). Contract §7.2's "before `--top`" is about truncation, which a selection is not | §4.1 |

### Amendment 3 — from phase 3

Phase 3 read §7, §8 and §11 against the contract and the renderer and found the conflicts below.
Decided by the owner on 2026-11-03, committed before the code that depends on them.

| # | Was | Now | Changed |
|---|---|---|---|
| A | §7.5, §8: `broken on main since …` | The digest does not name the canonical branch (contract §4.2, §6.2), so no branch is named. The detail view and the `--test` block say `broken on the canonical branch since {date}`, `unstable on the canonical branch`, `passing on the canonical branch`, `known flaky`, `no canonical history`. The latest-run Cloud line uses the short forms `broken since {date}`, `unstable`, `passing`, `known flaky`, `no canonical history` | §7.5, §8 |
| B | §7.6: `not in workspace Acme: project billing` | The envelope carries `workspaceId`, not the name contract §7.4 asks for (sdk #335 adds it). Until then: `cloud  not in this workspace: project {key} · pass --project <key>`, or `no project in this workspace for {assembly}` when no key was found | §7.6 |
| C | §7.3: segment `confidence 0.22 ▼0.61 · evidence adequate (ESS 34) · 35 d`; fit order drops the window, the delta, ESS, the segment | A value never loses its window, and the Cloud evidence level never loses its ESS (contract §4.4, §4.8). Segment: `confidence 0.22 ▼0.61 (35 d) · evidence adequate (ESS 34, 14 d)`, `· pending` after the confidence when `freshness == pending`. Fit order: drop the delta, then the whole evidence token, then the whole segment. `insufficient-evidence`: `evidence sparse (ESS 4 of 10, 14 d)` alone. `not-scored` or no confidence block: no segment | §7.3 |
| D | §7.2: `passing` always takes the label; `≠` "between the two labels" | Contract L3 wins: a finding with `disagreesWithLocal` keeps its local label first and reads `local {kind} ≠ cloud {verdict}`. Its second line is `cloud: {verdict phrase} · {flaky phrase}` | §7.2 |
| E | §7.2: `cloud` null → `local {kind}` and the reason line | Contract R1 and §7.1: with `context.cloud` null or `not-attempted`, a row is exactly today's. The layered label applies only when the Cloud layer was attempted. Group findings never carry a digest and get `local {kind}` with no second line | §7.1, §7.2 |
| F | §7.2, §7.6: the reason per row | A report-wide status (`unavailable`, `login-required`, `forbidden`, `no-project`) is said once, on the reason line; its rows read `local {kind}` with no second line. Rows get a second line only for `partial` with no digest (`cloud: not answered`) and for `state == unknown` (`cloud: not in project {key}`) | §7.2, §7.6 |
| G | §7.5: the Cloud line is one line, dropped right to left | At 55 columns the verdict and its date leave no room for the match phrase. The line wraps at segment boundaries; continuations align after `cloud  `. Only the confidence token is dropped, when it does not fit the last line | §7.5 |
| H | §8: `RUNS IN CLOUD` row and heading | Both overflowed the fence. Heading annotation: `newest {n} runs in Cloud (retention {d} days)`; `hasMore` is a dim line under the rows, `more runs in Cloud`. Row: `MM-dd HH:mm`, outcome glyph and word, tier, `CI` or `—`, machine, branch, short SHA, sized to the widest value in the list and capped at 10 (machine) and 8 (branch), cut from the left. The short hash and `(attempt {n})` follow when the row still fits. `runsState` other than `ok` is the annotation `not fetched ({runsState})` | §8 |
| I | §7.2: where the second line goes | Under the name (and a cluster's members), above the local headline, so the name keeps its own line (report-format D2). `cloud name differs: {testName}` (§10) follows it | §7.2, §10 |
| J | §7.4: a trailing token per failure mode | A line of its own under the mode's count line, `cloud: …`: the count line is already near the fence | §7.4 |
| K | §7.4, §8: `1 812 runs` | Counts print as the local report prints them, `1812 runs`. Cloud times print in UTC as sent: `yyyy-MM-dd HH:mm` in pairs, `yyyy-MM-dd` for a `since` | §7.4, §8 |
| L | §7.2–§8: `≠`, `▼`, `▲`, `—`, `…` | Each is a `ReportGlyphs` pair (`!=`, `v`, `^`, `-`, `...`): the golden charset sweep rejects a non-ASCII character no pair defines | §7 |
| M | §7.6: the line per status | `unavailable ({reason}) · showing local results only`; `login required · run xping login`; `forbidden · API keys need the Team plan; run xping login instead` with an API key, else `forbidden · showing local results only`; `partial, {n} of {m} tests answered ({reason})` (a comma, not a colon, after the `cloud:` of Amendment 3R); a status this build does not know prints verbatim with its reason | §7.6 |
| N | §8: the block when no digest | `CLOUD` heading with the §7.6 reason as its only line when the status is report-wide; `not answered` under `partial`; `not in project {key}` for `unknown`. Labels inside the block are unprefixed (the heading says `CLOUD`); the detail view's are prefixed `cloud` | §8 |
| O | §7.3, 3C: confidence is a trailer segment, fitted into the room the local segments leave | The confidence score is Xping's headline signal and gets a fixed line. The trailer is local only; it has no Cloud segment. Every finding with an `ok` digest gets, first under its name: `cloud confidence {score} {▼▲delta} · {category} · {window}`, plus `· pending` when `freshness == pending`. `insufficient-evidence`: `cloud evidence sparse (ESS 4 of 10, 14 d)`. `not-scored` or no confidence block: no line. The line wraps and is never cut. The §7.2 second line and `cloud name differs` follow it. The category is Cloud's own label, printed as sent (contract R6). Supersedes 3C | §7.2, §7.3 |
| P | 3G: the latest-run confidence token is the one dropped | It leads the line, `cloud  confidence 0.22 (35 d) · broken since …`, and nothing is dropped: every segment wraps | §7.5 |
| Q | §7.4, §8: `verdict` is the first pair | `confidence` is the first pair, in the detail view and the `--test` block | §7.4, §8 |
| R | `cloud confidence …`, `cloud evidence …`, `cloud name differs: …`, the reason line and the latest-run line as `cloud  …` | Every line that says where its value came from starts `cloud:` or `local:`, so a reader reads the source the same way on every line: `cloud: confidence 0.22 ▼0.61 · …`, `cloud: evidence sparse (…)`, `cloud: known as {testName}`, `cloud: unavailable (timeout) · …` above the fence, `cloud: confidence 0.22 (35 d) · broken since …` on a latest-run row. Labels (`cloud broken`, `local flaky`), the header token and the `cloud …` pair labels are names, not sources, and keep no colon | §7.2, §7.5, §7.6, §10 |
| S | §7.2: the verdict takes the label of every kind but contract L4's seven | `DurationRegression`, `RetryDeepening`, `ParallelSensitive` and `TimeSensitive` keep their local label too, with Cloud's flaky state on the line under the name. They describe how a test behaves on this machine (time, retries, concurrency, clock), not whether it passes, and §12 already calls duration regression a local observation: a `cloud passing` label over "slower" answers a question the row did not ask. Only `Flaky` and `AlwaysFailing`, the two kinds a verdict speaks to, take the verdict's label. **Contract L4 lists seven observation kinds; it needs the matching amendment in the dashboard repo** | §7.2 |

### Amendment 4 — from #335

Contract §7.4 puts the workspace **name** in `context.cloud`, and §9.5 names it in the
`no-project` hint, but nothing on the wire carried it: the access token had `workspace_id` only
(cli-auth §3.2) and no DataGateway answer names the workspace. Decided by the owner on
2026-11-03.

| # | Was | Now | Changed |
|---|---|---|---|
| A | §6.2: `context.cloud` has `workspaceId` and no name | `workspace` (string or null) follows `workspaceId`. Its source is a new access-token claim, `workspace_name`, issued by the Portal next to `workspace_id`. `AccessTokenClaims` reads it, and `CredentialRecord.WorkspaceName` stores it at sign-in and on every refresh, so a renamed workspace shows its new name within one token lifetime. It is null for an API key (no token) and for a token without the claim. A claim was chosen over a digest field because `no-project` is exactly the case where the digest answers 404 with no body; and over a workspace endpoint because it costs no request inside the 4 s budget. Schema stays `1.28`: the field lands before 1.28 leaves the feature branch. **Contract §6.1's claim list needs `workspace_name`, and the Portal needs to issue it: a follow-up in the dashboard repo** | §6.2, cli-auth §3.2, §7.2 |
| B | §7.6 and Amendment 3B: `not in this workspace: project {key}` | With a name the reason line reads `cloud: not in workspace {name}: project {key} · pass --project <key>`, `cloud: no project in workspace {name} for {assembly} · …`, or `cloud: no project in workspace {name} · …` with no row. The hint for a key from the flag, the environment or the settings that names no project is contract §9.5's: `project {key} is not in workspace {name}; run xping login to choose another workspace.`; it never says whether the project exists elsewhere. The name-match hint gains the name: `no matching Cloud project for {assembly} in workspace {name}. Use --project <key>.` Without a name (an API key, or a token without the claim), Amendment 3B's line and today's hints stand. The name passes through `TerminalText.Printable` like every Cloud value | §7.6, §11 |

---

## 1. Purpose, scope, traceability

### 1.1 Purpose

`xping report` and `xping report --test` gain a Cloud layer: one batch digest per project instead
of one read per test, a verdict and context per test, a signature match per local failure, a
session-state answer for the latest local run, and a Cloud run history for `--test`. The local
layer is unchanged in meaning and in exit codes. This document names the classes, DTOs, renderer
slots, tests and phases.

### 1.2 In scope

- `ICloudApiClient` gains the two contract endpoints; the per-test read is removed (§2, §3).
- `CloudEnricher` is rewritten around targets per project, batches, the contract budget and the
  contract status model (§3, §4).
- Signature matching on the SDK's `errorMessageHash` (§5).
- The JSON envelope `1.28` (§6).
- Text rendering under the layering rules, for findings, the latest run, detail, and `--test` (§7,
  §8).
- stderr rules, including the move of the banner and the scope notice (§11).
- The window decision and its trade-offs (§12); the agent surface (§13).
- Amendments to the existing report specs (§15), docs (§16), tests (§17), phases (§18).

### 1.3 Out of scope

Everything in contract §10, and:

- `xping login`, `logout`, `auth status`, the credential stores and the token pipeline. They are
  cli-auth's and are reused as they are.
- The MCP server and the Xping skill themselves (§13 says what they read).
- Any change to local analysis, providers, scoring, the local window, local retention or `--fail-on`.
- `CtaThrottle`'s use of `DateTime.UtcNow` and `Environment` directly (§19 Q-3).

### 1.4 Traceability

| Contract | Requirement | This document |
|---|---|---|
| §2 R1–R6 | Layered system rules | §7.1, §9, §10 |
| §3 | Questions per failure and their wire fields | §6, §7, §8 |
| §4.1 | Source label on every value | §6.2, §7 |
| §4.2, §4.3 | Verdict and flaky state, never derived locally | §7.2, §7.3 |
| §4.4, §4.5 | Window and freshness labels on every window-bound value | §6.3, §7.4 |
| §4.7 | Signature match states | §5 |
| §4.8 | Two evidence scales never on one key | §6.3, §7.4 |
| §5 | Signature identity: `errorMessageHash`, read as stored, algorithm id | §5 |
| §6.1, §6.2 | Digest request and response | §2.2, §4 |
| §6.3 | Run history for `--test` | §2.2, §8 |
| §7.1 | When enrichment runs | §3.1 |
| §7.2 | Targets, hashes per test, session keys, batching, concurrency | §4 |
| §7.3 | Budget, timeouts, retries, 429 | §3.2, §3.3 |
| §7.4 | Envelope additions | §6 |
| §7.5 L1–L6 | Label layering | §7 |
| §7.6 | Exit codes unchanged | §9 |
| §7.7 | Action table, not a field | §13 |
| §8.1 | `context.cloud.status` and `reason` | §3.4 |
| §8.2 F1–F5 | Fail open, one stderr line, banner to stderr, hostile digest | §3.5, §11 |
| §8.3 | Determinism of the local layer | §17.1 |
| §9.1–§9.7 | Edge cases | §10 |
| §11 | Unknown members, algorithm id mismatch, schema bump | §5.4, §6.1 |

---

## 2. Component architecture

### 2.1 Overview

```
 ReportCommand.EnrichAndRenderAsync
   │  (local analysis done; envelope built; exit code already decidable)
   ▼
 CloudEnricher.EnrichAsync(envelope, request)          ── CloudBudget (4 s total)
   ├─ CredentialResolver (unchanged)                   ── 1 s slice
   ├─ TargetPlanner      : envelope → targets per assembly, hashes, session keys
   ├─ ProjectResolver    : assembly → project key (unchanged order, 2 pages max)
   ├─ ICloudApiClient.GetDigestAsync(projectKey, DigestRequest)   × batches, ≤ 4 in flight
   ├─ ICloudApiClient.GetRunsAsync(projectKey, fingerprint, limit) (--test only, if time remains)
   ├─ SignatureMatcher   : local errorMessageHash × digest.signatures → match
   └─ EnvelopeStitcher   : digest → findings[].cloud, latestRun.failures[].cloud,
                           test.cloud, context.cloud
```

### 2.2 Components

| Component | File | Change |
|---|---|---|
| `ICloudApiClient` | `Cloud/ICloudApiClient.cs` | Remove `GetTestAsync`. Add `Task<DigestResponse?> GetDigestAsync(string projectKey, DigestRequest request, CancellationToken ct)` and `Task<RunsResponse?> GetRunsAsync(string projectKey, string fingerprint, int limit, CancellationToken ct)`. `ListProjectsAsync` stays; `GetProjectAsync` is removed (Amendment 1E). |
| `CloudApiClient` | `Cloud/CloudApiClient.cs` | Implement the two calls. `POST {gw}/v1/projects/{key}/tests/digest` with a JSON body; `GET {gw}/v1/projects/{key}/tests/{fp}/runs?limit=`. 404 → null (project or test not in the workspace). The body is serialised with the SDK `IXpingSerializer` `ApiOptions` (camelCase), as today's responses are read. |
| Cloud DTOs | `Cloud/Digest/*.cs` (new folder) | `DigestRequest`, `DigestRequestTest`, `DigestResponse`, `TestDigest`, `DigestConfidence`, `DigestFlaky`, `DigestCanonical`, `DigestTransition`, `DigestSignature`, `DigestWindow`, `DigestSession`, `RunsResponse`, `CloudRun`. Records, one file each or one file per group. These are the wire shapes of contract §6; they are not the envelope DTOs (§6). |
| `CloudTest` | `Cloud/CloudTest.cs` | Deleted. |
| `CloudHttp`, `CloudResilience` | `Cloud/` | Constants replaced per §3.2. |
| `CloudBudget` | `Cloud/CloudBudget.cs` (new) | Owns the total deadline and the credential slice; answers `Remaining` and `HasAtLeast(TimeSpan)`. Built on `TimeProvider`. |
| `TargetPlanner` | `Cloud/TargetPlanner.cs` (new) | Pure: envelope + local sessions → `IReadOnlyList<Target>` per assembly in contract §7.2 order, each with its hashes; plus session keys per assembly. |
| `SignatureMatcher` | `Cloud/SignatureMatcher.cs` (new) | Pure: a local failure's `errorMessageHash` and a digest → `CloudMatchDto`. |
| `CloudEnricher` | `Cloud/CloudEnricher.cs` | Rewritten: §3, §4. Keeps the name, the `EnrichmentRequest` / `EnrichmentResult` shape, and the fail-open wrapper at `CloudEnricher.cs:97-105`. |
| `ProjectResolver` | `Cloud/Projects/ProjectResolver.cs` | `MaxPages` 20 → `MaxProjectListPages = 2` (contract §7.3). Nothing else. |
| Envelope DTOs | `Report/Contract/CloudDtos.cs` | Rewritten: §6. |
| `ReportEnvelope` | `Report/Contract/ReportEnvelope.cs` | `TestHistoryDto` gains `Cloud`; `FailureModeDto` gains `ErrorMessageHash` and `Cloud`; `LatestRunFailureDto.Cloud` changes type; `CurrentSchemaVersion = "1.28"`. |
| `FailureModes` | `Report/Contract/FailureModes.cs` | Carries the SDK hash into the DTO (§5.2). |
| `CloudText` | `Report/Rendering/CloudText.cs` | Rewritten: §7. |
| `TextReportRenderer` | `Report/Rendering/TextReportRenderer.cs` | The slots in §7 and §8. |
| `ReportCommand` | `Commands/ReportCommand.cs` | Banner and scope notice to stderr (§11); passes local sessions to the enricher for hashes and session keys (§4.2). |

Dependency direction is unchanged: `Cloud/` depends on `Report/Contract` DTOs and on Core's
`TestSession` model; the renderer depends on the envelope only.

---

## 3. Enrichment client and budget

### 3.1 When it runs

Exactly contract §7.1. The code gate is `ReportCommand.cs:432` (`Format != Summary`) plus the
credential check inside the enricher. `--test` runs it too, with the subject as the first target
(§4.1). A future `doctor` does not call the enricher.

### 3.2 Constants

All in `Cloud/CloudHttp.cs` unless noted. Today's values in brackets are replaced.

| Constant | Value | Contract |
|---|---|---|
| `CloudBudget.Total` | 4 000 ms [10 s `CloudEnricher.Budget`] | §7.3 `CloudTotalBudgetMs` |
| `CloudBudget.Credential` | 1 000 ms [none] | §7.3 `CredentialBudgetMs` |
| `CloudHttp.AttemptTimeout` | 3 000 ms [10 s] | §7.3 `CloudRequestTimeoutMs` |
| `CloudHttp.TotalTimeout` | equals `CloudBudget.Total` [30 s] | the `HttpClient.Timeout` can never outlive the budget |
| `CloudResilience.MaxRetryAttempts` | 1 [3], on 5xx only, and only when `budget.HasAtLeast(1 s)` (Amendment 1B) | §7.3 |
| `CloudResilience.RetryDelay` | 200 ms [2 s exponential, jittered] (Amendment 1B) | — |
| `CrossProcessLock.Patience` | 2 s [5 s] (Amendment 1A) | cli-auth §9.4 |
| `CloudResilience` 429 | never retried, never waited for [once if `Retry-After ≤ 10 s`] | §7.3 |
| `CloudEnricher.MaxConcurrency` | 4 (unchanged) | §7.2 `MaxConcurrentRequests` |
| `CloudEnricher.MaxEnrichedTests` | 200 | §7.2 |
| `CloudEnricher.MaxDigestBatchSize` | 100 | §6.1 |
| `CloudEnricher.MaxHashesPerTest` | 3 | §6.1 |
| `CloudEnricher.MaxSessionKeysPerRequest` | 5 | §6.1 |
| `CloudEnricher.RunsLimit` | 50 | §6.3 `DefaultRunsPerHistoryRequest` |
| `CloudEnricher.MinimumForRuns` | 1 000 ms | §7.3: the `--test` history call is made only with at least this much budget left |
| `ProjectResolver.MaxProjectListPages` | 2 [20] | §7.3 |
| `SignatureAlgorithmId` | `"emh-1"` | contract §5.2 |

The retry predicate is a 5xx answer and nothing else (Amendment 1B): not 408, not a network error,
not the attempt timeout, not 429. The `Retry-After` reader (`CloudResilience.cs:63-79`) is deleted.
The retry is skipped, not shortened, when less than one second of budget remains: `CloudResilience`
receives the `CloudBudget` and checks it in `ShouldHandle`.

### 3.3 Budget mechanics

- `CloudBudget` starts when `ReportCommand` calls `EnrichAsync`, after local analysis and after the
  envelope exists. It creates the linked `CancellationTokenSource` that today sits at
  `CloudEnricher.cs:144`, with `Total` as its delay.
- The credential step (`CredentialResolver`, the keychain probe, settings parsing) runs under a
  second token linked to the first with the `Credential` delay, and on the thread pool, because
  the keychain backends are synchronous and never see the token (Amendment 1D). On its expiry the
  enricher returns `unavailable` / `credential-timeout`. A token refresh is not part of it: it
  runs inside the first DataGateway request, under that request's 3 s attempt timeout, and the
  cross-process lock's `Patience` is 2 s so its "proceed without the lock" rule still applies
  there (Amendment 1A).
- The `login-required` fallback to the ambient API key (`CloudEnricher.cs:150-163`) stays and runs
  inside the same total. It re-sends only the batches that did not answer.
- The `--test` runs request is sent after every digest batch has answered or failed, and only if
  `HasAtLeast(MinimumForRuns)`. Otherwise `test.cloud.runsState = "budget"`.
- Every `elapsedMs` in `context.cloud` is measured by the same `TimeProvider`.

### 3.4 Status and reason

`context.cloud.status` and `reason` are computed exactly as contract §8.1. The mapping from today's
exceptions (`CloudApiClient.FailureAsync`, `CloudApiClient.cs:146-188`):

| Signal | `status` | `reason` |
|---|---|---|
| no credential | `not-attempted` | `none` |
| credential slice expired | `unavailable` | `credential-timeout` |
| `LoginRequiredException` (401 `MissingCredentials`, or a second 401 after refresh), no API-key fallback or the fallback also failed | `login-required` | `none` |
| 403 `InsufficientScope` or `FeatureNotAvailable` (API key on a plan without `ApiAccess`) | `forbidden` | `none` |
| every assembly unresolved, or every digest request 404 on the project | `no-project` | `not-found` |
| total budget expired before any batch answered | `unavailable` | `budget` |
| attempt timeout, network, 5xx after the one retry, 429 | `unavailable` when nothing answered, else `partial` | `timeout` / `network` / `server` / `rate-limited` |
| a batch answered with an unreadable body | `partial` | `invalid-response` |
| `signatureAlgorithm` mismatch | `ok` or `partial` as otherwise; every `match` is `unknown` | `signature-algorithm` |
| all batches answered | `ok` | `none` |

`partial` also covers a report with several assemblies where one project row is not `ok`
(contract §9.4). The per-project rows are `context.cloud.projects[]` (§6.2); today's single
`Project` member is replaced.

`Attempt.Status` (`CloudEnricher.cs:449-458`) is replaced by a small `CloudOutcome` record
(`Status`, `Reason`, `Hint`) built once at the end of the enrichment; the ad-hoc "first hint wins"
and "Fail overwrites" rules (`CloudEnricher.cs:434-447`) go away. Precedence when several
signals exist: `login-required` > `forbidden` > `no-project` > `unavailable` > `partial` > `ok`.

### 3.5 Fail-open

Unchanged in principle (`CloudEnricher.cs:97-105`): any exception except the caller's cancellation
returns the envelope unchanged, with `context.cloud` set to `unavailable` and a reason, never
without `context.cloud`. The one change: today a report without a credential returns **no**
`context.cloud` (`CloudEnricher.cs:127-134`). Under contract §8.1 it returns
`status: "not-attempted"`, so an agent always finds the object (contract R5).

---

## 4. Targets and request building

### 4.1 Target order (contract §7.2)

`TargetPlanner.Plan(envelope, findings, latestRun, localSessions)` returns, per assembly, an
ordered distinct list capped at `MaxEnrichedTests` across all assemblies:

1. `envelope.Test.Test` (the `--test` subject) when present. Today `Targets()` skips it
   (`CloudEnricher.cs:324-336`); that is the gap contract §3 Q7 closes.
2. Every `Finding.Subject` with a fingerprint, in the **pre-truncation** order. The planner takes
   `AnalysisResult.Findings` ranked by the coordinator, not `envelope.Findings`, because the envelope
   is already cut by `--top` (`EnvelopeBuilder.cs:92-98`). `ReportCommand` passes the full list.
3. `LatestRunDto.Failures[].Subject`.
4. Every other test that failed in the latest local session, read from that session's executions
   (final attempt `Failed` or `Timeout`), in test-name order.

Group subjects (`Subject.Type` group, no fingerprint) are skipped as today; their members arrive
through 2–4.

### 4.2 Hashes and session keys

For each target, up to `MaxHashesPerTest` values of `TestExecution.ErrorMessageHash`
(`Xping.Sdk.Core/Models/Executions/TestExecution.cs:179`), distinct, non-null:

1. the hashes of the target's failed attempts in the latest local session, most recent first;
2. then the most frequent hashes of the target's failures across the local window.

Session keys: the `TestSession.SessionId` (`TestSession.cs:70`) of the newest local session per
assembly, as a Guid string, up to `MaxSessionKeysPerRequest` across assemblies (the same key appears
for every assembly of one host run; it is sent once per project).

`ReportCommand` already holds the window's sessions (`ReportCommand.cs:434` builds pins from them);
it passes the same sessions to the enricher through `EnrichmentRequest`, which gains
`IReadOnlyList<TestSession> Sessions`. The planner reads executions from them; no second store read.

### 4.3 Batching

Targets grouped by project binding (unchanged resolution, §2.2). Per project,
ceil(n / `MaxDigestBatchSize`) requests, built in target order so batch 1 holds the highest-ranked
tests. `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = MaxConcurrency` over all batches of
all projects (today's shape, `CloudEnricher.cs:284-303`). A failed batch loses only its tests.

### 4.4 Stitching

`EnvelopeStitcher` keys the `tests[]` answers by fingerprint and writes:

- `FindingDto.Cloud` for single-test subjects (`with`, as `Apply` does at `CloudEnricher.cs:346-361`).
- `LatestRunFailureDto.Cloud` with digest and match.
- `FailureModeDto.Cloud` on every mode of an enriched finding (§5.3).
- `TestHistoryDto.Cloud` with digest, runs and `runsState`.
- `ContextDto.Cloud`.

A finding whose fingerprint is not in any answer (batch lost, cap exceeded, project unresolved)
keeps `Cloud = null`; the reason is in `context.cloud` (contract R5). A digest with `state ==
"unknown"` is stitched as a `CloudDigestDto` with `State = "unknown"` and null blocks, so the
renderer can say `not in project {key}` (contract §9.7).

---

## 5. Signature matching

### 5.1 Key

`errorMessageHash` as stored in the local session file (contract §5.1, §5.4). The CLI MUST NOT
recompute it; `FailureSignatureFactory` keeps computing the local composite from raw text as it
does (`FailureSignatureFactory.cs:57`) and the comment at `FailureSignature.cs:18-21` is rewritten
to say: the composite is the local key; the SDK's `ErrorMessageHash` is carried beside it for the
Cloud match and is never an input to the composite.

### 5.2 Carrying the hash

`FailureSignature` gains `string? ErrorMessageHash`, set from the execution in
`FailureSignatureFactory.Create`. A mode groups executions by the composite; the mode's
`ErrorMessageHash` is the hash shared by its executions, or, when a composite groups executions
with different message hashes (possible when the normaliser and the composite disagree on a
literal), the most frequent one, and on a tie the ordinally smallest, so the value does not
depend on enumeration order. `FailureModes.From(SignatureView)` (`FailureModes.cs:47-49`) writes
it into `FailureModeDto.ErrorMessageHash`. It is a local field, labelled local, present with or
without Cloud.

### 5.3 Match

`SignatureMatcher.Match(string? errorMessageHash, CloudDigestDto? digest, string? algorithmReason)`:

| Condition | `match` | `signature` |
|---|---|---|
| `algorithmReason != null` | `unknown` | null |
| `digest == null` or `digest.State != "ok"` | `unknown` | null |
| `errorMessageHash == null` | `unsignable` | null |
| a `signatures[]` item with that hash and `runs > 0` | `matched` | that item |
| a `signatures[]` item with that hash and `runs == 0` (requested, unseen) | `unseen` | null |
| no item with that hash | `unseen` | null; this only happens for a hash that was not requested (beyond `MaxHashesPerTest`), and the renderer says `unseen in the top 5` for it (§7.5) |

A requested hash is always in `signatures[]` (contract §6.2), so the last row is the only
approximation, and it is labelled.

### 5.4 Algorithm id

`DigestResponse.SignatureAlgorithm` is compared with `SignatureAlgorithmId` once per response. On
mismatch the stitcher sets `context.cloud.reason = "signature-algorithm"` and every match in that
project is `unknown`; everything else in the digest is used (contract §11).

---

## 6. JSON envelope `1.28`

### 6.1 Version

`ReportEnvelope.CurrentSchemaVersion = "1.28"` (`ReportEnvelope.cs:106`). One sentence is added to
the `<remarks>` history at `ReportEnvelope.cs:57-105` in its prose style: "1.28 replaced the
nine-field `cloud` object with the Cloud digest (verdict, confidence, flaky, canonical, signatures),
added `cloud` to `test` and to every failure mode, gave every latest-run failure a signature
match, and made `context.cloud` always present with projects and sessions." Property declaration
order is serialised order (`ReportEnvelope.cs:21`); new members go last in each record.

The CLI MUST ignore unknown JSON members in Cloud responses (the SDK serializer does not throw on
them) and MUST write unknown enum strings verbatim.

### 6.2 `context.cloud` (`CloudContextDto`, rewritten)

```json
"cloud": {
  "status": "partial", "reason": "timeout", "elapsedMs": 2140,
  "cloudUrl": "https://app.xping.io", "credential": "login", "workspaceId": "01J…", "workspace": "Acme",
  "fetchedAt": "2026-11-01T10:02:11Z", "signatureAlgorithm": "emh-1", "retentionDays": 365,
  "requestedTests": 14, "enrichedTests": 9,
  "projects": [
    { "assembly": "Payments.Tests", "projectKey": "payments", "source": "session", "status": "ok", "reason": "none" },
    { "assembly": "Billing.Tests",  "projectKey": null, "source": null, "status": "no-project", "reason": "not-found" }
  ],
  "sessions": [
    { "sessionKey": "4f1c0c9e-…", "projectKey": "payments", "state": "complete", "scored": true, "flakyExamined": true }
  ]
}
```

`credential` is `login` or `api-key` (today's `stored-login` becomes `login`, contract §9.6).
`workspace` is the name contract §7.4 asks for, read from the access token's `workspace_name`
claim (Amendment 4A); `workspaceId` and `workspace` are null for an API key.
`projects[].status` uses the same vocabulary as the root `status`. `sessions[]` is empty when no
project answered.

### 6.3 Per-test digest (`CloudDigestDto` and children)

The envelope mirrors contract §6.2 one to one, camelCase, with these CLI-side additions:

| Member | Type | Note |
|---|---|---|
| `state` | `ok` / `unknown` | |
| `fetchedAt` | CLI clock | contract §4.5 |
| `disagreesWithLocal` | bool | contract L3; set by the stitcher when the finding kind is `Flaky` and `verdict != known-flaky` with `flaky.state == not-flaky`, or the kind is `AlwaysFailing` and `verdict == passing` |
| `identity` | `null` / `name-mismatch` / `shared` | contract §9.3 |
| `verdict`, `testName`, `lastExecutedAt`, `totalRuns` | as the wire | |
| `confidence` | `CloudConfidenceDto` with `state`, numbers, `evidenceLevel`, `window`, `evidenceWindow`, `computedAt`, `freshness` | `evidenceLevel` is the Cloud scale, never copied to `findings[].evidenceLevel` (contract §4.8) |
| `flaky` | `CloudFlakyDto` | |
| `canonical` | `CloudCanonicalDto` or null | |
| `lastTransition` | `CloudTransitionDto` or null | |
| `signatures[]` | `CloudSignatureDto[]` | the wire items, unchanged |

Windows are `CloudWindowDto(string Kind, int Length, DateTimeOffset? End, DateTimeOffset? OldestAt)`.

### 6.4 Per-failure match

`CloudMatchDto(string Match, CloudSignatureDto? Signature)`, on `FailureModeDto.Cloud` and inside
`LatestRunFailureDto.Cloud` (`CloudFailureDto(CloudDigestDto Digest, string Match,
CloudSignatureDto? Signature)`).

### 6.5 `test.cloud`

`CloudTestHistoryDto(CloudDigestDto? Digest, IReadOnlyList<CloudRunDto> Runs, string RunsState,
int? RetentionDays, bool HasMore)`. `RunsState` is `ok` / `budget` / `unavailable` /
`not-attempted`. `CloudRunDto` mirrors contract §6.3's row.

### 6.6 What does not change

`findings[].kind`, `severity`, `evidenceLevel`, `evidenceSessions`, `metrics`, `repro`, `headline`,
`latestRun.failures[].status`, `contrast`, `priorSessions`, `priorFailures`, every `test.*` local
member, `summary`, `window`, `truncated`, `selection`. The local `headline` string stays the local
headline; the layered label of §7.2 is a renderer composition, not a field, so JSON consumers read
`cloud.verdict` and `kind` separately (contract R3, R4).

---

## 7. Text rendering

There is one text renderer with two capability profiles (`OutputCapabilities`: decorated at a
terminal, ASCII and undecorated on a pipe or with `--ascii`). "Rich" and "plain" in the brief are
these two profiles; every rule below applies to both, with the glyph set deciding separators.
Width and charset discipline follow `cli-report-format-spec.md` D10. JSON is §6.

### 7.1 Principle

Every Cloud string is composed by the renderer from `cloud` objects (report-format Amendment 4
already requires this). The builder and the coordinator never see Cloud data. A null `cloud` emits
nothing Cloud-specific except the reason line of §7.6, so the existing goldens change only where
§11 moves a line to stderr.

### 7.2 Finding rows: the layered label (contract L1–L3, L6)

The row keeps its local position, severity glyph, id and local trailer. The **label** token is the
first word after the severity:

| `cloud.verdict` | Label | Second line |
|---|---|---|
| `known-flaky`, `broken`, `unstable`, `passing` | the verdict, as `cloud known-flaky` | `local: {kind} in {evidenceSessions} runs here` |
| `no-history` | `local {kind}` | `cloud: no canonical history` |
| `cloud` null, digest `unknown`, or `state != ok` | `local {kind}` | the reason (§7.6) |

The local kind word stays exactly `ReportVocabulary`'s word for the kind; only its position moves.
`disagreesWithLocal` adds the marker `≠` (`!=` in ASCII) between the two labels. Rank and order are
untouched (L6).

A `--kind` filter still filters on the local kind. `--fail-on` still reads local severity.

### 7.3 Finding rows: the Cloud trailer

Replaces `CloudText.Trailer` (`CloudText.cs:41-48`). Segment, inserted at the same index 1 as today
(`TextReportRenderer.cs:1631-1640`):

```
confidence 0.22 ▼0.61 · evidence adequate (ESS 34) · 35 d
```

- `confidence {score:0.00}`; `▼`/`▲` with `|delta|` when `delta` is non-null and
  `|delta| ≥ 0.01`; ASCII `v`/`^`.
- `evidence {evidenceLevel} (ESS {ess:0})` from `confidence.evidenceLevel` and
  `effectiveSampleSize`.
- The window token `{length} d` from `confidence.window` (contract invariant 3). Never the lifetime
  `totalRuns` here.
- `confidence.state == insufficient-evidence`: `evidence sparse (ESS 4 of 10)`, no score.
- `freshness == pending`: the token `· pending` is appended.
- Fit order when the budget is short (report-format D10): drop the window token, then the delta,
  then ESS, then the whole segment. Nothing local is cut for it (Amendment 4's rule stands).

The flaky state is **not** in the trailer; it is the label (§7.2) or, for observation kinds (L4),
the second line `cloud: {flaky.state}` with `since {date}` when `flaky`.

### 7.4 Detail view pairs (`--id`)

Replaces `CloudText.Pairs` (`CloudText.cs:60-75`) in `WriteDetail` (`TextReportRenderer.cs:558-562`).
Every pair is prefixed `cloud` and names its window:

| Pair | Value |
|---|---|
| `cloud verdict` | `{verdict}` plus `(≠ local)` when `disagreesWithLocal` |
| `cloud confidence` | `0.22 ▼0.61 · highly unreliable · 35 d window · computed 2026-10-31 09:00` |
| `cloud evidence` | `adequate · ESS 34 · 34 canonical, 0 speculative, 2 untracked · 14 d window` |
| `cloud flaky` | `not flaky · examined 2026-10-31 09:00 · 14 d window`, or `not examined in 14 d`, or `flaky (timing, major) since 2026-10-11` |
| `cloud canonical` | `failed 10/10 · 34 in a row since 2026-10-11 09:14 · newest failed 2026-10-31 08:59`, or `no canonical history` |
| `cloud transition` | `reliability degraded reliable → highly unreliable at 2026-10-11 09:14` |
| `cloud lifetime` | `1 812 runs · last 2026-10-31 08:59` |
| `cloud fetched` | `2026-11-01 10:02 (pending: newer runs not yet scored)` when `freshness == pending` |

`FAILURE MODES` rows (failure-mode spec D-rules) gain one trailing token per mode:
`cloud: same error on 6 machines · 34 CI · 2 local · since 2026-10-11` for `matched`;
`cloud: unseen` for `unseen`; nothing for `unsignable`; `cloud: unknown` for `unknown`.

### 7.5 Latest run (contract L5, reverses latest-run spec Amendment 6)

`WriteLatestRunFailure` (`TextReportRenderer.cs:1404`) keeps the local contrast sentence and adds a
second, Cloud line under it. `CloudText.Contrast` (`CloudText.cs:54-55`) is deleted. The Cloud line:

```
cloud  broken on main since 2026-10-11 · same error on 6 machines and CI · confidence 0.22 (35 d)
```

Composition: verdict phrase (`broken on main`, `unstable on main`, `passing on main`,
`known flaky`, `no canonical history`) with `since {canonical.failingSince}` for `broken`; then
the match phrase (`same error on {machines} machines and CI` when `ciRuns > 0`, `same error on
{machines} machines, not in CI`, `this error unseen in Cloud`, or nothing for `unsignable`); then
the confidence token with its window. Dropped right to left when the line does not fit
`LatestRunBudget`. The local sentence is never shortened for it.

The header provenance token (`TextReportRenderer.cs:291-297`) becomes `· cloud` when
`context.cloud.status` is `ok` or `partial`, not when a score exists; `partial` renders `· cloud
(partial)`.

### 7.6 The reason line

When `context.cloud.status` is not `ok` and not `not-attempted`, one line is printed below the
header, before the fence, in the dim style:

```
cloud  unavailable (timeout) · showing local results only
cloud  login required · run xping login
cloud  not in workspace Acme: project billing · pass --project <key>
```

With `not-attempted` and a terminal, nothing is printed (the invitation banner on stderr covers it,
§11). Per-row, a finding with `cloud == null` while the status is `partial` prints `cloud: not
answered` as its second line; with `state == unknown`, `cloud: not in project {key}`.

### 7.7 Summary format

Unchanged. `--format summary` never enriches (contract §7.1).

---

## 8. `report --test`: the Cloud section

`WriteHistory` (`TextReportRenderer.cs:805`) gains one block between the local counts and the
timeline, headed `CLOUD` with the heading machinery of report-format D5:

```
CLOUD  payments · fetched 2026-11-01 10:02
  verdict      broken on main since 2026-10-11 09:14  (≠ local: flaky)
  confidence   0.22 ▼0.61 · highly unreliable · 35 d window · computed 2026-10-31 09:00
  evidence     adequate · ESS 34 · 34 canonical, 0 speculative, 2 untracked · 14 d
  flaky        not flaky · examined 2026-10-31 09:00 · 14 d
  signatures   ab12… InvalidOperationException · 36 runs · 6 machines · 34 CI · since 2026-10-11
               c9d0… TimeoutException · 2 runs · 1 machine · local only
  lifetime     1 812 runs · last 2026-10-31 08:59
  last run     complete, scored  (session 4f1c0c9e)

  RUNS IN CLOUD  newest 50 of retention 365 d · all machines and branches
  2026-10-31 08:59  ✗ failed   canonical  CI   gh-runner-17   main   0053b0e  ab12…
  2026-10-31 07:10  ✓ passed   untracked  —    adrian-mbp     feat/x c1f2a9d
  …
```

- The `signatures` rows are the digest's `signatures[]`, requested ones first, marked `(seen here)`
  when the hash is among this machine's modes.
- `RUNS IN CLOUD` prints `runs[]` from §6.5, newest first, one line per run: time, outcome glyph
  and word, tier, `CI` or `—`, machine, branch, short SHA, short hash. `attempts > 1` appends
  `(attempt {attempts})`. The window label is the contract's `newest N runs in Cloud (retention
  {d} days)` wording; `hasMore` adds `· more in Cloud`.
- `runsState` other than `ok` prints `RUNS IN CLOUD  not fetched ({runsState})`.
- The block is omitted entirely when `test.cloud == null` and status is `not-attempted`; with any
  other status the §7.6 reason line appears in its place.
- The local timeline, counts, durations and failure modes are unchanged, and the local
  `FAILURE MODES` rows get the per-mode Cloud token of §7.4.

JSON: `test.cloud` (§6.5). The `--test` exit codes are unchanged (0 / 3 / 4).

---

## 9. Exit codes

No change. `ExitCodes.ForReport(analysis.Findings, options.FailOn)` (`ExitCodes.cs:53`) runs over
the local findings at `ReportCommand.cs:480-483`, after enrichment in time but with no Cloud input.
A test asserts that the exit code of a fixed store is identical with Cloud `ok`, `partial`,
`unavailable`, `login-required` and `not-attempted` (§17.1).

Housekeeping in the same change: the remark at `AuthExitCodes.cs:13` says report uses 0–3; it is
corrected to 0–4 (cli-auth §13 is amended by a one-line note pointing here).

---

## 10. Unknown here, known there, and the reverse

Exactly contract §9.7:

- A test Cloud knows and the local store does not is never looked up. `--test <name>` for it exits
  3 as today, and the message is unchanged.
- A local test Cloud does not know gets `cloud.state = "unknown"` and renders `cloud: not in
  project {key}` (§7.6); its label stays local (L2).
- Name mismatch and shared fingerprints (contract §9.3): `identity` is set by the stitcher by
  comparing `digest.testName` with `Subject.FullyQualifiedName` ordinally, and by counting local
  subjects per fingerprint. Text adds `cloud name differs: {testName}` as a second line.

---

## 11. stderr and the banner

- Hints: at most one line per report, written by `WriteCloudHints` (`ReportCommand.cs:493-501`).
  The `Always` flag is set for `login-required` and `forbidden` only (contract F2); every other hint
  is printed only when stderr is a terminal or `--verbose` is set. The `Unavailable` prefix constant
  stays.
- The scope notice (`ReportCommand.cs:779-795`) and the Cloud invitation (`ReportCommand.cs:810-828`)
  move from `io.Output` to `io.Error` (contract F3). The gating (`Decorate`, `CtaThrottle`,
  `XPING_NO_BANNER`) is unchanged; the stream changes. Goldens that captured those lines on stdout
  change accordingly (§17.3).
- `IsConnected` in the invitation keeps reading `context.cloud.status` `ok` or `partial`.

---

## 12. The window decision

Contract §10 keeps the local defaults and retention. This section records the trade-offs the brief
asked for, so they are not re-opened in code review.

| Claim | Where it lives after this spec | Why |
|---|---|---|
| "failed N of the last 20 runs here", local flaky pattern, duration regression, retry masking, timing out, broken fixture, shared failure, vanished | Local, 20 runs / 14 days (`ReportWindow.cs:22, 27`), store 50 runs / 30 days / 100 MB (`LocalStoreOptions.cs:33-43`) | Observations about this machine; the local window is sized for statistical power on one machine (sdk #281) and is deterministic offline. |
| "flaky" as a verdict, "broken on main", "same error on N machines and CI", "since {date}" beyond the store, confidence and its evidence | Cloud, 35-day scoring window, 14-day evidence and flaky windows, canonical history 10 runs, retention per plan | They need breadth (machines, tiers, branches) and a ledger; the local store has neither. Contract §4.4 fixes the windows, and the CLI labels them. |
| Run history beyond 30 days | Cloud, `--test` only, 50 rows by default | One request; a person reads it, an agent reads the digest. |

Alternatives considered and rejected here: widening the local default window so local
"always failing" has more depth (costs offline determinism nothing, but sdk #281 showed the power
gain is small and the staleness cost real); shrinking the local window because Cloud covers it
(breaks the free product, contract R1). Nothing changes in `LocalStoreOptions` or `ReportWindow`.

---

## 13. The agent surface

cli-auth §16 stands: agents run the CLI as a child process, call `auth status --json` first, never
run `login`, and set `XPING_NO_BANNER=1`. What they read from `report --format json` under `1.28`:

| Question | Field(s) |
|---|---|
| Can I trust the Cloud layer? | `context.cloud.status`, `reason`; per project `context.cloud.projects[].status` |
| Did the last run reach Cloud? | `context.cloud.sessions[]` (`state`, `scored`) |
| Which tests failed here? | `latestRun.failures[]` (local, unchanged) |
| Was it already broken? | `latestRun.failures[].cloud.digest.verdict`, `.canonical.failingSince`, `.match` |
| Is it flaky by the ledger? | `….digest.flaky.state`; **never** `….digest.confidence.score` |
| Everywhere or only here? | `….signature.machines`, `.ciRuns`, `.localRuns` |
| What do I do? | contract §7.7 action table over `verdict` × `match`, then `findings[].repro` |
| Which Cloud data is window-bound? | every block's `window` / `evidenceWindow`; `totalRuns` is the one lifetime number |

Rules for the skill and the MCP tool, to be copied into them when they are written:

- Read `verdict` and `flaky.state` as given. Never compute a verdict from `confidence`.
- When `cloud == null` or `state == "unknown"`, use only local fields and say so.
- Treat `disagreesWithLocal == true` as a signal to show both, not to pick one.
- Never change a decision because of `context.cloud.status`; it changes only what is known.

---

## 14. SDK changes

None required. Verified against the contract:

- `errorMessageHash` and `stackTraceHash` are on every local execution
  (`TestExecution.cs:179, 192`); `sessionId` is on every session (`TestSession.cs:70`); the project
  pin is written by the orchestrator (`XpingContextOrchestrator.cs:1181-1182`).
- The normaliser is frozen by the contract (§5.2). Any change to `MessageNormaliser` is a contract
  amendment first; a test in `Xping.Sdk.Core.Tests` pins its output for a fixed set of messages so a
  change cannot land silently (§17.1, added by phase 0).

---

## 15. Amendments to existing specs

Each is one short amendment section added to the named document in phase 0, with a pointer here;
the superseded text stays for history.

| Document | Amendment | What it says |
|---|---|---|
| `cli-auth-cli-spec.md` | Amendment 1 — §10, §11 superseded | "The Cloud API client, enrichment targets, budget, envelope `cloud` objects and hint lines are now specified by `cloud-data-in-cli-cli-spec.md` §2–§7, §11. §16 stands." Also §13's "0–3" → "0–4". |
| `cli-report-format-spec.md` | Amendment 5 — Cloud layering | §8's two slots are refilled by §7.2–§7.3 and §7.6 here: the label token and the trailer. Amendment 4's "renderer composes, builder never resolves Cloud text" stands. |
| `cli-latest-run-spec.md` | Amendment 7 — Cloud line added, not substituted | Reverses Amendment 6's replacement: the local contrast sentence stays; the Cloud line is §7.5 here. |
| `cli-test-history-spec.md` | Amendment 11 — `CLOUD` block and `RUNS IN CLOUD` | Fills §8 "Reserved for Cloud" with §8 here; `TestHistoryDto.Cloud`. |
| `cli-failure-mode-spec.md` | Amendment 8 — per-mode Cloud token | §8 "nothing is reserved" is replaced by §5.2 and §7.4 here: `errorMessageHash` on every mode, a `cloud` match per mode. |
| `cli-finding-detail-spec.md` | Amendment 9 — Cloud pairs | The `cloud *` pairs of §7.4 here replace the three pairs of cli-auth §11.3 (`cloud confidence`, `cloud evidence`, `cloud trend`). |

---

## 16. Configuration and documentation

No new configuration key. No new environment variable. Documentation rows in the same PR as the
code they describe:

- `docs/cli/command-reference.md`: `### Cloud data` (`:874`) rewritten for the digest, the label
  layering, the reason line, the `--test` block; `### --format json` (`:911`) sample regenerated for
  `1.28` by `DocumentedSampleTests`; exit codes section unchanged but re-read.
- `docs/configuration/configuration-reference.md:65`: `XPING_NO_BANNER` row says the invitation is
  on stderr.
- `README.md` roadmap lines that mention Cloud enrichment.

---

## 17. Testing strategy

### 17.1 Unit tests (`tests/Xping.Cli.Tests`, hand-written fakes as today)

| Test class | Proves |
|---|---|
| `TargetPlannerTests` | order (subject, findings pre-truncation, latest-run failures, other latest failures), distinct by fingerprint, cap at 200, hashes per test (latest session first, then frequency, max 3), session keys (newest per assembly, max 5), group subjects skipped. |
| `SignatureMatcherTests` | every row of §5.3, including the "not requested" approximation and the algorithm mismatch. |
| `CloudBudgetTests` (with `TimerTrackingTimeProvider`) | total 4 s; credential slice 1 s; `HasAtLeast`; the runs call skipped under 1 s; retry skipped under 1 s. |
| `CloudResilienceTests` | one retry on 5xx, none on 429, none on 4xx, `Retry-After` ignored. |
| `CloudApiClientTests` | digest POST body shape, camelCase, 404 → null, unreadable body → `invalid-response`, runs GET with `limit`. |
| `CloudEnricherTests` (rewritten) | status precedence table of §3.4; `not-attempted` object present without a credential; `partial` when one batch fails; `no-project` per assembly; login-required fallback resends only unanswered batches; stitching of every slot; `disagreesWithLocal`; `identity`. |
| `ExitCodeIndependenceTests` | the same store yields the same exit code under every `context.cloud.status` (contract §7.6, §8.3). |
| `CloudRenderingTests` (rewritten) | label token per verdict, trailer composition and fit order, detail pairs, latest-run two lines, reason line, per-mode token, ASCII variants. |
| `HostileEnvelopeTests` (extended) | unknown enum strings and missing blocks in a digest render without throwing (contract F4). |
| `MessageNormaliserPinTests` (`Xping.Sdk.Core.Tests`) | fixed inputs → fixed hashes; a change fails the test and names contract §5.2. |

Every renderer test runs in both charsets as today.

### 17.2 The in-process fake Cloud

`FakeCloud.Gateway.cs` (`:190-194`) gains `POST v1/projects/{key}/tests/digest` and
`GET v1/projects/{key}/tests/{fp}/runs`, scripted per fingerprint, with knobs for: delay per
request, 429, 5xx once then 200, a wrong `signatureAlgorithm`, an unknown fingerprint, a project
404, a session key `unknown`. `ReportEnrichmentTests` run `xping report` and `xping report --test`
end to end against it:

`Report_Digest_Ok`, `Report_Digest_PartialBatch`, `Report_Digest_Timeout_LocalIntact`,
`Report_Digest_RateLimited_NoWait`, `Report_NoCredential_NotAttemptedObject`,
`Report_ApiKey_Forbidden_Hint`, `Report_TwoAssemblies_OneProjectMissing`,
`ReportTest_CloudBlock_And_Runs`, `ReportTest_Runs_SkippedOnBudget`, `Report_Banner_OnStderr`.

### 17.3 Goldens

`tests/Xping.Cli.Tests/Report/Goldens/` gains Cloud fixtures (`cloud-broken.*`,
`cloud-known-flaky.*`, `cloud-disagree.*`, `cloud-partial.*`, `cloud-login-required.*`,
`history-cloud.*`), generated with `XPING_UPDATE_GOLDENS` and reviewed by hand in the PR. Existing
goldens change only by the lines §11 moves to stderr; the PR lists those files.

### 17.4 Manual verification

Against production with a test account, after the Cloud phases are deployed: one `report` on a
store that has uploaded sessions (`ok`, label layering visible, banner on stderr), one with a
revoked login (`login-required`), one `--test` with the `CLOUD` block and `RUNS IN CLOUD`, one with
the network cut (`unavailable`, exit code unchanged). Recorded in the final PR.

---

## 18. Implementation phases

Integration branch `feat/cloud-digest`, created from `main` in phase 0; one sub-branch per phase,
`feat/cloud-digest-NN-<name>`, merged by PR when its exit criterion holds. Every merge leaves the
branch green and behaving exactly as `main` for anyone without a credential.

Phases 0–4 run entirely against `FakeCloud` and do not depend on the Cloud side. Phase 5 needs
the Cloud spec's digest endpoint in production. Local-only work (sdk #141's local half, #143,
#144, #138, #145, #296) is untouched by phases 0–4 and can merge to `main` in parallel; the only
shared files are `ReportEnvelope.cs` and `TextReportRenderer.cs`, merged by ordinary rebase.

| # | Branch | Work | Exit criterion |
|---|---|---|---|
| 0 | `feat/cloud-digest-00-amendments` | §15 amendments to the six specs; `AuthExitCodes.cs:13` remark; `MessageNormaliserPinTests`. No behaviour change. | Build green; pin test green; amendments merged. |
| 1 | `feat/cloud-digest-01-wire` | `Cloud/Digest/*` DTOs, `ICloudApiClient` change, `CloudApiClient` digest and runs calls, `CloudHttp`/`CloudResilience` constants, `CloudBudget`, `ProjectResolver.MaxProjectListPages`, `FakeCloud` routes. `CloudEnricher` temporarily calls the digest for today's targets with today's stitching so the branch stays green. | `CloudApiClientTests`, `CloudBudgetTests`, `CloudResilienceTests` green; `CloudTest.cs` deleted. |
| 2 | `feat/cloud-digest-02-envelope` | `CloudDtos.cs` rewrite, `ReportEnvelope` members, `1.28`, `FailureSignature.ErrorMessageHash`, `FailureModes`, `TargetPlanner`, `SignatureMatcher`, `EnvelopeStitcher`, `CloudEnricher` rewrite with status precedence, `EnrichmentRequest.Sessions`. | `TargetPlannerTests`, `SignatureMatcherTests`, `CloudEnricherTests`, `ExitCodeIndependenceTests` green; `DocumentedSampleTests` regenerated. |
| 3 | `feat/cloud-digest-03-render` | `CloudText` rewrite, label token, trailer, detail pairs, latest-run second line, reason line, per-mode token, `--test` `CLOUD` and `RUNS IN CLOUD` blocks, header token; banner and scope notice to stderr. | `CloudRenderingTests`, `HostileEnvelopeTests` green; new goldens reviewed; existing goldens changed only by the stderr move. |
| 4 | `feat/cloud-digest-04-e2e-docs` | `ReportEnrichmentTests` of §17.2; `docs/cli/command-reference.md`, configuration reference row, README lines. | All ten end-to-end tests green; docs build green. |
| 5 | `feat/cloud-digest-05-verification` | §17.4 against production once the Cloud spec's phases are live; fix-ups. | Verification recorded in the PR; `feat/cloud-digest` ready for one PR to `main`. |

Each PR names the contract sections it implements and the tests that prove them. A phase that
finds a contract or spec conflict stops and reports.

---

## 19. Open questions

| # | Question | Proposed answer |
|---|---|---|
| Q-1 | Does the `--test` subject count against `MaxEnrichedTests` when it is also a finding subject? | It is one target; the cap is on distinct fingerprints. No contract change. |
| Q-2 | **Answered by Amendment 1D.** The credential slice cancels a keychain probe mid-call on macOS; does the keychain API tolerate cancellation? | The probe already runs with a token (cli-auth §7.3). If a backend ignores the token, the slice still expires the enrichment and the probe finishes in the background of the process, which exits normally. Verify in phase 1 on macOS; if the process hangs, the slice must apply at the backend selector instead. |
| Q-3 | `CtaThrottle` reads `DateTime.UtcNow` and `Environment` directly, so the banner cannot be tested with the injected clock. | Out of scope here; leave as is, test the stderr move with a redirected `io.Error` only. |
| Q-4 | Should the reason line (§7.6) print under `not-attempted` on a pipe, where there is no banner? | No. A pipe reader has the JSON or is a script; contract F2 keeps pipes quiet. |

### Contract observations recorded while writing (no conflict found)

- Contract §7.2 step 4 ("every test that failed in the latest local session beyond the
  `LatestRunMaxRows`") needs the session's executions, which the enricher did not receive before.
  `EnrichmentRequest.Sessions` (§4.2) is the implementation; nothing crosses the wire differently.
- Contract §6.1 says duplicate fingerprints are a 400; the planner's `DistinctBy` guarantees the CLI
  never sends one, and a test proves it.
