---
uid: troubleshooting-debugging
title: Debugging
---

# Debugging

This page shows how to see what the SDK does during a test run: where its output appears, what each
line means, and how to get more detail. For fixes to specific errors, see
[Common issues](common-issues.md).

---

## See the SDK's output

The SDK writes its messages to the test process's standard output and standard error. Messages at
`Warning` and above go to standard error, and the rest to standard output.

Where you see them depends on the runner:

| Runner | Where the SDK's lines appear |
|---|---|
| vstest (`dotnet test`), NUnit, MSTest and xUnit v2 | Only at `normal` verbosity or higher: `dotnet test --logger "console;verbosity=normal"`. The default verbosity hides them. |
| vstest, xUnit v3 | Not in the `dotnet test` output. Run the test project with `dotnet run`, or run its executable directly, such as `./bin/Release/net10.0/MyApp.Tests`. |
| Microsoft.Testing.Platform | In the test executable's output, at the default verbosity. |

```bash
dotnet test --logger "console;verbosity=normal"
```

A line from the SDK's log starts with `[Xping`, the time and the level:

```
[Xping 13:16:21 INF] Initialized. SessionId: "12048a9a-1213-4aa9-8558-a3b850b2a631", Environment: Local
```

The levels print as `DBG`, `INF`, `WRN`, `ERR` and `FTL` (`Critical`). A few messages start with
`[Xping]` and no time. The SDK writes them directly, without its log:

- `[Xping] SDK initialization failed: …`, when the SDK cannot start;
- `[Xping] Strict mode configuration error: …` and `[Xping] Xping network error in strict mode: …`,
  when [`StrictMode`](../configuration/configuration-reference.md#strictmode) fails the run;
- `[Xping] 1 test flaked on retry this run - …`, the retry hint.

### Filter the output

Every SDK line contains `[Xping`:

```bash
dotnet test --logger "console;verbosity=normal" 2>&1 | grep '\[Xping'

# Warnings and errors only
dotnet test --logger "console;verbosity=normal" 2>&1 | grep -E '\[Xping [0-9:]+ (WRN|ERR)\]'
```

In PowerShell:

```powershell
dotnet test --logger "console;verbosity=normal" 2>&1 | Select-String '\[Xping'

# Warnings and errors only
dotnet test --logger "console;verbosity=normal" 2>&1 | Select-String '\[Xping [0-9:]+ (WRN|ERR)\]'
```

---

## Read the SDK's output

A run in `Cloud` mode that uploads without errors prints these lines:

```
[Xping 13:16:21 INF] Initialized. SessionId: "12048a9a-1213-4aa9-8558-a3b850b2a631", Environment: Local
[Xping 13:16:22 INF] Published 15 tests in 23ms (21.4 KB) · receipt 3f9a1c2e
[Xping 13:16:22 INF] Finalization receipt 3f9a1c2e (1ms, 1.7 KB)
[Xping 13:16:22 INF] Total tests recorded: 15 · 10 passed, 4 failed, 1 timed out · exec: 581ms · wall: 651ms (+70ms overhead)
```

### The start line

The first line says how the SDK started. Check it first when the SDK does not do what you expect.

| Line | Meaning |
|---|---|
| `Initialized. SessionId: …, Environment: …` | `Cloud` mode. Each test assembly reports into its own project. |
| `Initialized. SessionId: …, Project: <id> (pinned), Environment: …` | `Cloud` mode, with every result going to the [`ProjectId`](../configuration/configuration-reference.md#projectid) you set. |
| `Initialized in local-only mode. … No API key configured - results stay on this machine.` | `LocalOnly` mode, because the SDK found no API key. |
| `Initialized in local-only mode. … Mode is set to LocalOnly - results stay on this machine.` | `LocalOnly` mode, because you set it. |
| `SDK is disabled via configuration (Enabled=false). …` or `(Mode=Disabled)` | The SDK records nothing. |
| `Configuration validation failed: … SDK disabled - …` | A setting is invalid, and the SDK records nothing. See [Validation rules](../configuration/configuration-reference.md#validation-rules). |

`Environment` is the [environment name](../configuration/configuration-reference.md#environment):
the deployment that the tests run against. It is `Default` when you set none. The examples on this
page come from a shell with `XPING_ENVIRONMENT=Local`.

### Upload lines

In `Cloud` mode, the SDK prints a line for each upload it makes:

- `Published 15 tests in 23ms (21.4 KB) · receipt 3f9a1c2e`: a batch of test results. The size is the
  size of the JSON before compression.
- `Finalization receipt 3f9a1c2e (1ms, 1.7 KB)`: the final upload, which closes the session.

The receipt is the first 8 characters of the receipt ID that Xping Cloud returns. Keep it when you
contact support about a run.

`LocalOnly` mode makes no uploads, so it prints no upload lines.

### The summary line

At the end of the session, the SDK prints a summary of what it recorded:

```
[Xping 13:15:19 INF] Total tests recorded: 25 (24 tests, 1 retry) · 17 passed, 5 failed, 2 timed out · exec: 2.6s · wall: 2.7s (+161ms overhead)
```

- `25 (24 tests, 1 retry)`: 25 executions of 24 tests. The part in brackets appears only when a test
  was retried. The outcomes then count each test by its last attempt.
- `exec`: the total of each test's own duration.
- `wall`: the time from the SDK's start to the end of the session.
- `(+161ms overhead)`: `wall` minus `exec`. It includes the framework's setup and teardown and the
  time between tests, not only the SDK's work. When tests run in parallel, `exec` can be larger than
  `wall`, and the overhead is left out.

`exec` is often much smaller than the duration the test framework reports. The framework's duration
also includes loading the assemblies and discovering the tests.

When the final upload fails, the SDK prints `Session finalized.` instead of the summary.

### The retry hint

When a test passes on a retry in a local run, the SDK prints a hint after the summary:

```
[Xping] 1 test flaked on retry this run - run `xping report` for local history
```

It does not print the hint in CI. To hide it everywhere, set `XPING_NO_BANNER` to any value.

### Repeated errors

The SDK prints an upload error in full the first time. When the same error repeats in the same
session, it prints a short line instead:

```
[Xping 13:16:24 ERR] Same HTTP 401 error (2nd occurrence, batch size: 0)
```

`batch size` is the number of test results in that upload. The final upload carries none, so it shows
`0`. A successful upload resets the count.

---

## Get more detail

Set [`LogLevel`](../configuration/configuration-reference.md#loglevel) to `Debug`:

```bash
XPING_LOGLEVEL=Debug dotnet test --logger "console;verbosity=normal"
```

`Debug` adds the steps that the SDK skips or absorbs so that it never breaks the test run. For
example:

```
[Xping 13:21:01 DBG] GitHub PR detection skipped: GITHUB_EVENT_NAME='null' (expected 'pull_request' or 'pull_request_target').
[Xping 13:21:01 DBG] Finalizing session
```

Other Debug messages include `Local session store write skipped: …` and `Local session not stored:
…`, when the SDK could not write the [local store](../configuration/local-store.md).

`LogLevel` takes `Trace`, `Debug`, `Information` (the default), `Warning`, `Error`, `Critical` and
`None`. Each level includes the levels above it. `None` silences the SDK's log. It does not hide the
lines that start with `[Xping]` and no time.

---

## Warnings and errors

These are the warnings and errors that the SDK logs, apart from the upload errors. For the upload
errors, see [Upload errors](common-issues.md#upload-errors).

| Message starts with | Meaning |
|---|---|
| `FlushInterval '…' is not a duration and was ignored.` | The value is neither whole seconds (`30`) nor a time with colons (`00:00:30`). The setting keeps its default. The message names the setting as you wrote it, such as `XPING_FLUSHINTERVAL` or `Xping:FlushInterval`. The same applies to `RetryDelay` and `UploadTimeout`. |
| `Duplicate test fingerprint detected: …` | Two different tests have the same `[XpingFingerprint]` value. Their results can overwrite each other in Xping Cloud. Give one of them another value. |
| `N executions carry no test assembly and cannot be attributed to a project.` | The SDK could not tell which test assembly these results came from. With no `ProjectId`, Xping Cloud cannot place them. |
| `No test assembly could be resolved for this session, …` | No result named a test assembly. Set `ProjectId` for Cloud. Report it as an SDK bug. |
| `GITHUB_EVENT_NAME is not set, …` | On GitHub Actions, the test process did not get `GITHUB_EVENT_NAME`, often because the tests run in a container. Pass the `GITHUB_*` variables into the container. |
| `GitHub PR detection skipped: …` | The SDK could not read the pull request from GitHub's variables or event file. The message names what is missing. |
| `Sending the configured default branch '…' …` | You set [`DefaultBranch`](../configuration/configuration-reference.md#defaultbranch), and it differs from the branch the CI reports. |
| `[Xping] Process exiting with session … still active: …` | MSTest only. No `[AssemblyCleanup]` finalized the session. See [Known limitations](known-limitations.md#mstest-a-session-finalized-by-the-process-exit-safety-net-loses-its-upload-on-vstest). |
| `Failed to initialize Xping SDK: … SDK disabled - …` | The SDK could not start, and records nothing. The message gives the reason. |
| `Error finalizing Xping session` | The end of the session failed. The exception follows the message. |

---

## Check the configuration

1. **Read the start line.** It shows the mode, the project pin and the environment name. See
   [The start line](#the-start-line).
2. **List the `XPING_*` variables that the shell sets**, without their values:

   ```bash
   env | grep '^XPING_' | cut -d= -f1
   ```

   ```powershell
   Get-ChildItem Env:XPING_* | Select-Object Name
   ```

   The names use one underscore, such as `XPING_APIKEY`. They win over `appsettings.json` and over a
   configuration object passed in code.
3. **Find the local store** with `xping where`. It shows the folder and the runs in it. See
   [Local store](../configuration/local-store.md).
4. **Rule out the upload.** Run once with `XPING_MODE=LocalOnly`. If the problem stays, it is not the
   upload.

For the configuration sources and their order, see
[Configuration methods](../configuration/configuration-reference.md#configuration-methods).

---

## Check the network

The SDK uploads to `https://upload.xping.io/v1` over HTTPS (port 443), unless you set
[`ApiEndpoint`](../configuration/configuration-reference.md#apiendpoint).

Check that the machine reaches it:

```bash
curl -sS -o /dev/null -w '%{http_code}\n' https://upload.xping.io/v1
```

```powershell
Test-NetConnection -ComputerName upload.xping.io -Port 443
```

Any HTTP status code from `curl` means the machine reaches the endpoint. A connection error or a
timeout means a firewall, a proxy or DNS blocks it.

The SDK uses the standard .NET proxy settings. It honours the `HTTPS_PROXY`, `HTTP_PROXY` and
`NO_PROXY` environment variables, and the system proxy settings on Windows and macOS.

### Inspect the requests

To see the requests, route them through a debugging proxy such as Fiddler or mitmproxy: set
`HTTPS_PROXY` to the proxy's address, and trust the proxy's root certificate on the machine. Each
upload is a `POST` to `<ApiEndpoint>?sessionId=<id>`, with these headers:

| Header | Value |
|---|---|
| `X-API-Key` | The API key |
| `X-Project-Id` | The `ProjectId`, only when you set one |
| `User-Agent` | `Xping-SDK-DotNet/<version>` |
| `Content-Encoding` | `gzip`, when the body is larger than 1 KB and [`EnableCompression`](../configuration/configuration-reference.md#enablecompression) is on |

Remove the API key before you share a capture.

---

## Collect information for support

When you report a problem, include:

1. **The SDK version:**

   ```bash
   dotnet list package | grep Xping
   ```

2. **The .NET SDK, the runtime and the OS:** `dotnet --info`.
3. **The test framework, its version and the runner** (vstest or Microsoft.Testing.Platform):

   ```bash
   dotnet list package | grep -E -i 'nunit|xunit|mstest'
   ```

4. **The SDK's output, with Debug detail:**

   ```bash
   XPING_LOGLEVEL=Debug dotnet test --logger "console;verbosity=normal" > test-output.log 2>&1
   ```

5. **The session ID and the receipts** from the output, for a run that reached Xping Cloud.
6. **Your configuration, without the API key.**
7. **A small project that reproduces the problem**, if you can make one.

Email support@xping.io, or open an issue at
[github.com/xping-dev/sdk-dotnet](https://github.com/xping-dev/sdk-dotnet/issues).

---

## See also

- [Common issues](common-issues.md)
- [Configuration reference](../configuration/configuration-reference.md)
- [Performance](../performance/performance.md)
- [Known limitations](known-limitations.md)
