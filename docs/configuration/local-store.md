---
uid: configuration-local-store
title: Local Store
---

# Local Store

The Xping SDK records every test run to a local store on disk. This happens in **both** local-only and Cloud mode, so the [CLI](../cli/command-reference.md) works whether or not you have an account.

---

## Location

The store is resolved in this order:

1. The `XPING_LOCAL_STORE` environment variable, or the `LocalStorePath` configuration setting, if set.
2. A `.xping` folder at your **repository root** — the nearest ancestor of the test assembly containing `.git`, `*.sln`, or `*.slnx`.
3. A per-repository folder under your local application data, when no repository root is found or the root is not writable.

To see where it resolved to:

```bash
xping where
```

The repository root is preferred because flakiness history is only meaningful per repository. A single shared folder would blend unrelated projects and break running the CLI inside a repo and getting *that repo's* answer.

> The walk starts from the test assembly's location rather than the current directory, because the working directory during `dotnet test` varies between the CLI, IDE runners, and CI agents.

---

## Layout

```
<repo-root>/.xping/
├── .gitignore          # contains `*`
├── sessions/
│   ├── session-638912345678901234-a3f5e9c2.json.gz
│   └── …
└── state.json
```

Each run is one gzipped JSON document holding the whole session. One file per run means concurrent test projects never contend for a lock, and a run interrupted by a killed test host leaves no file at all rather than a partial one.

---

## Git

**The store hides itself.** `.xping/.gitignore` contains a single `*`, which git honours for the whole directory. Nothing appears in `git status`, and your repository's own `.gitignore` is never modified — adding an entry there would show up as an unexplained diff in someone's next commit.

You do not need to do anything. If you prefer an explicit entry, adding `.xping/` to your own `.gitignore` is harmless.

---

## Retention

Applied after every write:

| Limit | Default | Applies to |
|---|---|---|
| Maximum runs | 50 | Each test project |
| Maximum total size | 100 MB | The whole store |
| Maximum age | 30 days | The whole store |

Runs past the age limit go first. Then each test project keeps its newest 50 runs, so a busy project never pushes another project's runs out. A run that covered several projects counts for each of them and is kept while any of them still needs it. Last, if the store is over 100 MB, the oldest runs go, starting with runs outside every project's newest 20. Those 20 are touched only if the store is still too big without the older runs. The run that was just written is never deleted.

A 2,000-test suite stores roughly 170 KB per run, so 50 runs is about 8.5 MB. A 200-test suite is well under 1 MB.

`xping report` reads the newest 20 runs from the last 14 days by default. The store keeps more than that so `--runs`, `--since` and `--test` can look further back.

To delete history manually, use [`xping clear`](../cli/command-reference.md#xping-clear).

---

## Performance

The store is written **once per run**, during finalization, after the last test has finished. Nothing is added to the per-test path.

Measured on a 2,000-test suite:

| Operation | Cost | When |
|---|---|---|
| Assembling the session | 1.4 ms | Once per run |
| Writing the run | 9.4 ms | Once per run |
| Per-test overhead | **none** | — |

Reading and analysing history is paid by the CLI, not by your test run.

---

## Environment variables

### `XPING_LOCAL_STORE`

Overrides the store location.

```bash
export XPING_LOCAL_STORE=/var/tmp/xping-store
```

Both the SDK and the CLI read this variable, and they must agree: if you set it for your test run, set it for `xping report` too, or the CLI will look somewhere else and find nothing.

The SDK reads it once, when it initializes. A test that changes the variable during the run, for example to point a tool it tests at a fixture store, does not move where the run itself is recorded.

Useful for read-only checkouts, containers where the repository root is not writable, and keeping history outside a workspace that gets wiped between builds.

The same setting is available in code and `appsettings.json` as `LocalStorePath`; the environment variable wins when both are set. A path set that way reaches only the SDK — export the variable for the CLI.

### `XPING_NO_BANNER`

Suppresses the one-line retry-flake hint the SDK prints after a test run, and the cloud invitation in the CLI report. Set it to any non-empty value.

```bash
export XPING_NO_BANNER=1
```

---

## Failure behaviour

**The local store never fails your test run.** A read-only checkout, a full disk, or a locked file degrades to "no local history" — the failure is logged at debug level and nothing else happens.

Corrupt or unreadable run files are skipped when reading, not treated as fatal. A file written by a newer SDK than the one reading it is also skipped, so upgrading and downgrading costs some history rather than breaking the report.

---

## What is stored

A deliberately reduced projection of each test execution:

- Test fingerprint and display name
- Outcome and duration
- Retry attempt, and whether the test passed on a retry
- A short hash of the error message, for grouping recurring failures

Plus per-run metadata: session id, timestamps, environment name, test assembly, branch, commit SHA, and whether the run was in CI.

Each run also records how it was recorded, under the environment's custom properties. The CLI uses these. They are written to the local store only, never uploaded.

| Property | Value | Absent when |
|----------|-------|-------------|
| `Xping.Mode` | `LocalOnly` or `Cloud` | Never |
| `Xping.ProjectId` | The pinned project id | No project is pinned |
| `Xping.TestFramework` | `NUnit`, `MSTest`, `xUnit` (v2) or `xUnit.v3` | Never |
| `Xping.Runner` | `VSTest` or `MTP` (Microsoft.Testing.Platform) | The tests ran in another host, such as the xUnit console runner or an xUnit v3 test executable run without Microsoft.Testing.Platform |
| `Xping.ProjectPath` | The test project's path from the repository root, e.g. `tests/Shop.Tests/Shop.Tests.csproj` | The project was built outside a Git repository, with `GenerateAssemblyInfo=false`, or the run covered test assemblies from more than one project |

The project path comes from an MSBuild file the Xping packages add to your test project's build. It stamps the path into the test assembly as `[assembly: AssemblyMetadata("Xping.ProjectPath", ...)]`. In a Git submodule the path is relative to the submodule, not the repository around it.

**Stack traces, error message text, exception types, and source locations are not stored.** The store holds what local analysis needs and nothing more, which keeps it roughly ten times smaller than the uploaded payload.

A consequence worth knowing: because the local record is reduced, it **cannot be re-uploaded** as a full session. It is a source of local analysis, not a backup of your uploaded data.

---

## See Also

- [Running Without an Account](../getting-started/local-first.md)
- [Xping CLI Reference](../cli/command-reference.md)
- [Configuration Reference](configuration-reference.md)
