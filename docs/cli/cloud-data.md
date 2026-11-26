---
uid: cli-cloud-data
title: Xping Cloud data in the CLI
---

# Xping Cloud data in the CLI

This page explains the Xping Cloud data that `xping report` adds to the local report. It is for
developers who use the CLI with a Xping Cloud account.

`xping report` works without an account. When the CLI has a Xping Cloud credential, the report also
shows what Xping Cloud knows about the tests it lists, and which other tests in your window Cloud
flags. This is how you see Cloud's verdicts, such as `cloud known-flaky` or
`flaky (cause: timing, major)`, from your terminal.

Cloud adds lines to the report. It never changes the local rows, their order, the counts in the
header, or the exit code.

---

## What you need

A credential that can read Cloud data. The CLI uses the first of these it finds:

| Order | Credential | Typical use |
|---|---|---|
| 1 | `--api-key <key>` | One command with another key |
| 2 | The sign-in that [`xping login`](command-reference.md#xping-login) stored | Your own machine |
| 3 | `XPING_APIKEY`, then `Xping__ApiKey` or `XPING__APIKEY`, then `Xping:ApiKey` in `appsettings*.json` | CI, where nobody signs in |

- The stored sign-in comes before `XPING_APIKEY`. On a developer machine, that variable usually holds
  the SDK's upload key, which cannot read.
- When the sign-in is revoked or expired, the CLI deletes it, prints the
  `Run xping login` line, and tries the key from row 3 if there is one. It does not do this after a
  network error.
- An API key can read only when it has the Read scope and the workspace's plan includes API access.
  Otherwise the report says so and shows local results.
- `xping auth status` shows which credential the CLI would use, without a network call.

Without a credential, the CLI makes no network calls.

### The Cloud project

The report finds the Cloud project from the first of these:

1. `--project <key>`;
2. `XPING_PROJECTID`, or `Xping:ProjectId` in `appsettings*.json` (read from `--directory` when you
   give it);
3. the project id the SDK recorded with the runs;
4. a Cloud project whose name is the test assembly's name.

---

## What Cloud adds to the report

Cloud values belong to fixed windows, and each value names its window: `35 d`, `14 d` or `10 runs`.
`lifetime` is the only all-time total. Cloud times are in UTC.

```
1.  HIGH  cloud broken
    SampleTests.ExceptionTest_ThrowsOnMissingDependency
    cloud: confidence 0.22 ▼0.61 · highly unreliable · 35 d
    local: always failing in 20 runs here
    failed 20 of 20 executions (100%), one failure mode: System.IO.FileNotFoundException
    evidence robust | -env-cluster | f_5b0e7a21 | .../SampleTests.cs:52
```

- **The header** ends in `· cloud` when anything on the page came from Cloud, and in
  `· cloud (partial)` when only some tests were answered.
- **The suite line.** On a full report, a `cloud:` line under the counts gives Cloud's verdicts for
  the tests in your window, shown on the page or not: `cloud: 1 broken · 2 known flaky`, or
  `cloud: no test flagged`. It ends in `(partial)` when Cloud did not finish reading the suite. The
  [overview](command-reference.md#scoping) prints the same line for each assembly.
- **The confidence line** comes first under the name of every finding that Cloud has scored: the
  score, its change, Cloud's [score category](../cloud/confidence-scores.md#score-categories) and the window. With too little evidence, it reads
  `cloud: evidence sparse (ESS 4 of 10, 14 d)`. The score and the verdict are Cloud's own. The
  verdict is not a threshold on the score.
- **The label** says whose word it is.
  - A `flaky` or `always failing` finding takes Cloud's verdict: `cloud known-flaky`, `cloud broken`,
    `cloud unstable` or `cloud passing`. The local kind moves to the `local:` line.
  - Every other kind keeps its local label, such as `local timing out`, because it describes this
    machine. Cloud's flaky state goes on a `cloud:` line.
  - When the two disagree, the label shows both: `local always failing ≠ cloud passing`.
- **A latest-run failure** gets a line under its local sentence, such as
  `cloud: confidence 0.22 (35 d) · broken since 2026-10-11`, and then whether Cloud has seen the same
  error: `same error on 6 machines and CI`, or `this error unseen in Cloud`.
- **A test Cloud does not know** reads `cloud: not in project payments`. A test Cloud did not answer
  for reads `cloud: not answered`. Both keep their local label.
- **A test Cloud knows by another name** gets `cloud: known as <name>`.

### Under `--id` and `--test`

- `--id` adds `cloud confidence`, `cloud verdict`, `cloud evidence`, `cloud flaky`, `cloud canonical`,
  `cloud transition`, `cloud lifetime` and `cloud fetched` to the detail block. When Cloud knows the
  test by another name, it adds `cloud name`. Each failure mode gets a `cloud:` line that says
  whether Cloud has seen that error.
- `--test` adds a `CLOUD` block with the same values and the test's error signatures, and a
  `RUNS IN CLOUD` block with the test's newest runs on every machine: result, tier, CI or not,
  machine, branch and commit. The CLI reads those runs only when at least one second of its
  four-second limit is left. Otherwise the block says `not fetched (budget)`.

---

## Flagged in Cloud

A test can pass on your machine while Cloud calls it known flaky, broken or unstable from other
machines' runs. A full report asks Cloud once per project for the tests it flags. It lists the ones in
your window that the page does not already name, in a `FLAGGED IN CLOUD` section after the findings:

<!-- xping:sample cloud-health -->
````
Xping · MyApp.Tests · 20 runs · 2026-08-05 → 2026-08-19 · main@a3f9c2e · cloud
412 tests · 412 healthy · 0 flagged
cloud: 1 broken · 1 known flaky · 1 unstable

```
FLAGGED IN CLOUD                                 3 with no local finding
────────────────────────────────────────────────────────────────────────

    broken       LedgerTests.Post_RejectsADuplicateEntry
                 cloud: confidence 0.22 (35 d) · broken since 2026-10-11
                        not flaky in 14 d

    known flaky  CheckoutTests.Checkout_CompletesWithinTheServiceBudget
                 cloud: confidence 0.41 (35 d)
                        flaky (cause: timing, major)

    unstable     InventoryTests.Reserve_ReleasesOnTimeout
                 cloud: confidence 0.81 (35 d) · not flaky in 14 d
```
````

- Each row gives Cloud's verdict, the test's name and a `cloud:` line: the confidence, the date for
  `broken since`, and Cloud's flaky state.
- A test already on the page, as a finding, a member of a group or a latest-run row, is not listed
  again. The section closes with `Also flagged in Cloud: 2 tests shown above.` A finding that `--top`
  left out, or a failure past the latest run's limit, is not on the page, so it is listed.
- At most ten rows are shown, the worst verdict first (`broken`, `known flaky`, `unstable`). `--all`
  shows every row. `--top` does not apply.
- Tests that Cloud knows but that are not in your local window are not listed.
- The section changes nothing local. The header counts, `--fail-on` and the exit code are this
  machine's. A listed test stays in `healthy`, which means "no local finding". The report does not
  print `No findings.` when this section has rows.
- `--id` and `--test` do not ask for the suite's flagged tests.

---

## When Cloud cannot answer

Cloud is never required. When it cannot answer, you get the local report. The CLI waits at most four
seconds for Cloud in total.

**Above the block**, one dim `cloud:` line says why the report has less Cloud data. It is part of the
report, so it is copied with it:

| Line | Meaning |
|---|---|
| `cloud: unavailable (budget) · showing local results only` | The four seconds ran out. Other reasons are `timeout`, `network`, `server`, `rate-limited`, `invalid-response` and `not-found` |
| `cloud: login required · run xping login` | The sign-in is revoked or expired |
| `cloud: forbidden · API keys need the Team plan; run xping login instead` | The workspace's plan does not let an API key read |
| `cloud: forbidden · showing local results only` | Cloud refused to answer for this sign-in |
| `cloud: partial, 9 of 14 tests answered (timeout)` | Cloud answered for some tests |
| `cloud: no project in workspace Acme for Search.Tests · pass --project <key>` | No Cloud project matched the assembly. With an API key, the line says `this workspace` |
| `cloud: not in workspace Acme: project checkout · pass --project <key>` | The project key you gave is not in the workspace |

**On stderr**, the CLI prints at most one more line, and only at a terminal or with `--verbose`. Two
lines are printed in every case, because only you can fix them: the sign-in line
(`Cloud data unavailable: your sign-in is no longer valid. Run xping login.`) and the line for a key
that Cloud refuses.

When some tests were answered, the stderr line is
`Cloud data partial: 9 of 14 tests answered (timeout).` When every test was answered but Cloud did
not finish reading the suite's flagged tests, it is
`Cloud data partial: suite health not complete (budget).`

The other stderr lines start with `Cloud data unavailable: `:

| Rest of the line | What to do |
|---|---|
| `could not reach https://app.xping.io (timeout). Showing local results only.` | Nothing. The next report tries again |
| `no matching Cloud project for Checkout.Tests. Use --project <key>.` | Pass `--project`, or set `XPING_PROJECTID` |
| `no matching Cloud project for Checkout.Tests in workspace Acme. Use --project <key>.` | The same, when you are signed in |
| `no Cloud project 'checkout' (from XPING_PROJECTID). Check the key, or pass --project <key>.` | Fix the project key |
| `project checkout (from XPING_PROJECTID) is not in workspace Acme. Check the key, or run xping login to choose another workspace.` | Fix the key, or sign in to the workspace that has the project |
| ``This API key cannot read Cloud data (its scope does not include read). Sign in with `xping login` instead.`` | The key has only the Upload scope. Sign in, or use a key with the Read scope |
| ``This API key cannot read Cloud data (the workspace's plan does not include API access). Sign in with `xping login` instead.`` | Sign in. The workspace's plan does not let API keys read |
| `Xping Cloud is rate limiting requests; try again in a minute.` | Wait |
| `Xping Cloud no longer accepts this version of the CLI. Please upgrade the xping CLI.` | `dotnet tool update -g Xping.Cli --prerelease` |

One line has no prefix. It appears when the sign-in file can be read by other users:

```
Refusing to read ~/.xping/credentials.json because other users can read it. Run `chmod 600 ~/.xping/credentials.json` (and `chmod 700 ~/.xping`) and try again.
```

`--summary` never asks Cloud.

---

## Cloud data in `--format json`

`context.cloud` is present in every JSON report except under `--summary`.

- Read `status` first: `ok`, `partial`, `unavailable`, `login-required`, `forbidden`, `no-project`,
  or `not-attempted` when there is no credential or nothing to ask.
- Then `reason`: `none`, `credential-timeout`, `budget`, `timeout`, `network`, `server`,
  `rate-limited`, `invalid-response`, `not-found`, or `signature-algorithm` when Cloud groups failures
  with a different hash than this CLI.
- `credential` is `login` or `api-key`. `workspaceId` and `workspace` name the signed-in workspace.
  Both are `null` for an API key, and `workspace` is also `null` when the sign-in has no name stored.
- `elapsedMs`, `fetchedAt`, `requestedTests` (tests asked about) and `enrichedTests` (tests
  answered).
- `projects`: one row per assembly, with its `projectKey`, `source` (`flag`, `env`, `config`,
  `session` or `name-match`), `status` and `reason`.
- `sessions`: whether Cloud has received and scored this machine's newest runs.
- `health`: the suite's [flagged tests](#flagged-in-cloud).
  - `state` is `ok`, `partial`, `unavailable` or `not-attempted`. It is always `not-attempted` under
    `--id` and `--test`.
  - `totalTests` is the number of tests Cloud classified, and `unhealthyTests` the number in your
    window.
  - `verdicts` counts `broken`, `knownFlaky`, `unstable`, and `flagged` for any other verdict.
  - `shownElsewhere` counts the flagged tests the page already names. `tests` holds the rest, each
    with a local `subject` and Cloud's `digest`. `testsShown` and `testsTotal` give the limit.
  - `summary` never counts these tests.

A finding about one test carries a `cloud` object, or `null` when Cloud did not answer for its test:

- `state` is `unknown` when Cloud does not know the test.
- `verdict` is `known-flaky`, `broken`, `unstable`, `passing` or `no-history`.
- `confidence` has the score, its own evidence level and its window. `flaky.state` is the flaky
  state. `canonical` holds the newest runs on the default branch, and `lastTransition` the last
  change. `signatures` lists the error signatures Cloud has seen for the test.
- `disagreesWithLocal` is `true` when a local `Flaky` or `AlwaysFailing` finding reads differently in
  Cloud. `identity` is `name-mismatch` or `shared` when one fingerprint names different tests.

Every failure mode carries the SDK's `errorMessageHash`. Under a finding with Cloud data, it also
carries a `cloud` match: `matched` with the signature, `unseen`, `unsignable` (no message) or
`unknown`. A latest-run failure's `cloud` holds the `digest`, its `match` and `signature`.
`test.cloud` holds the digest and the test's newest `runs` in Cloud, with `runsState`. `metrics`,
`headline` and `contrast` are always local.

The [envelope sample](command-reference.md#--format-json) shows these fields.

---

## See also

- [Command reference](command-reference.md)
- [Reading the report](reading-the-report.md)
