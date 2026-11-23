---
uid: configuration-reference
title: Configuration reference
---

# Configuration reference

This page lists every setting of the Xping SDK and the `xping` CLI, with its environment variable,
its default and its rules. It is for developers who configure the SDK in a test project or in CI.

---

## Configuration methods

How you initialize decides which sources the SDK reads.

**`XpingContext.Initialize()`**, without an argument, reads these sources. The first one that sets
a value wins:

1. `XPING_*` environment variables, for example `XPING_APIKEY`
2. `Xping__*` environment variables, the .NET nested format, for example `Xping__ApiKey`
3. The `"Xping"` section of `appsettings.{environment}.json`
4. The `"Xping"` section of `appsettings.json`
5. The defaults

**`XpingContext.Initialize(configuration)`** reads these sources:

1. `XPING_*` environment variables
2. The `XpingConfiguration` object you pass
3. The defaults

On this path, the SDK reads only one setting from `appsettings.json` and `Xping__*`:
[StrictMode](#strictmode). It turns strict mode on when the object, `XPING_STRICTMODE`,
`Xping__StrictMode` or `"Xping": { "StrictMode": true }` in `appsettings.json` asks for it.
`XPING_STRICTMODE=false` turns it off whatever the other sources say.

`XPING_*` variables win on both paths. A pipeline can change a value without a rebuild, even when
the code passes a configuration object. The SDK applies them to its own copy, so your object does
not change.

Once a process has called `Initialize(configuration)`, a later `Initialize()` in the same process
reuses that configuration. This matters in a process that runs several sessions, such as an IDE's
test server. `XpingContext.ShutdownAsync()` forgets it.

You cannot combine a configuration object with `appsettings.json`. To use the file, call
`Initialize()` and override values with `XPING_*` variables.

### Files and environment variables

- The SDK reads `appsettings.json` and `appsettings.{environment}.json` from the current directory
  of the test process. With `dotnet test`, that is the test project's output folder, so set
  `<CopyToOutputDirectory>` on the files.
- `{environment}` is the value of `ASPNETCORE_ENVIRONMENT`, else `DOTNET_ENVIRONMENT`, else
  `Production`. These variables only choose the file. They do not set the
  [Environment](#environment) setting.
- Section and key names are not case-sensitive: `"Xping"`, `"xping"` and `"XPING"` are the same
  section.
- Write a `TimeSpan` as whole seconds (`30`) or as `"hh:mm:ss"` (`"00:00:30"`), in JSON, in an
  `Xping__*` variable and in an `XPING_*` variable alike. A value in another form, such as `"1.5"` or
  `"30s"`, is ignored and the setting keeps its default. The SDK prints a warning that names it:
  `[Xping 16:39:12 WRN] XPING_RETRYDELAY '1.5' is not a duration and was ignored. Use whole seconds (30) or a time with colons (00:00:30).`
  From `appsettings.json` or an `Xping__*` variable, the warning names the setting as
  `Xping:RetryDelay`.
- An `XPING_*` variable that is blank, or whose value does not parse, is ignored. So a missing
  secret, such as `XPING_APIKEY: ${{ secrets.XPING_APIKEY }}`, does not clear a key set elsewhere.

---

## Quick reference

### SDK settings

| Setting | Type | Default | Environment variable | Description |
|---|---|---|---|---|
| [`ApiEndpoint`](#apiendpoint) | string | `https://upload.xping.io/v1` | `XPING_APIENDPOINT` | The upload address of Xping Cloud |
| [`ApiKey`](#apikey) | string | *(none)* | `XPING_APIKEY` | The key that authorizes uploads. Without it, the SDK runs local-only. |
| [`ProjectId`](#projectid) | string | *(none)* | `XPING_PROJECTID` | Sends every test assembly to one project |
| [`Mode`](#mode) | `XpingMode` | `Auto` | `XPING_MODE` | `Auto`, `LocalOnly`, `Cloud` or `Disabled` |
| [`BatchSize`](#batchsize) | int | `100` | `XPING_BATCHSIZE` | Cloud mode only. Executions in each upload during the session. |
| [`FlushInterval`](#flushinterval) | TimeSpan | `00:00:30` | `XPING_FLUSHINTERVAL` | Cloud mode only. Time between uploads during the session. |
| [`UploadTimeout`](#uploadtimeout) | TimeSpan | `00:00:30` | `XPING_UPLOADTIMEOUT` | Time limit of one upload attempt |
| [`MaxRetries`](#maxretries) | int | `3` | `XPING_MAXRETRIES` | Retries of a failed upload |
| [`RetryDelay`](#retrydelay) | TimeSpan | `00:00:02` | `XPING_RETRYDELAY` | Base delay between retries |
| [`EnableCompression`](#enablecompression) | bool | `true` | `XPING_ENABLECOMPRESSION` | Compresses uploads with gzip |
| [`Environment`](#environment) | string | `Default` | `XPING_ENVIRONMENT` | The deployed environment the tests target |
| [`DefaultBranch`](#defaultbranch) | string | *(none)* | `XPING_DEFAULTBRANCH` | The branch Xping Cloud treats as the default branch |
| [`Enabled`](#enabled) | bool | `true` | `XPING_ENABLED` | Turns the SDK on or off |
| [`UploadStackTraces`](#uploadstacktraces) | bool | `false` | `XPING_UPLOADSTACKTRACES` | Uploads the stack trace text of failed tests |
| [`EnablePullRequestDetection`](#enablepullrequestdetection) | bool | `true` | `XPING_ENABLEPULLREQUESTDETECTION` | Records the pull request a CI build belongs to |
| [`CollectLocalGitAuthor`](#collectlocalgitauthor) | bool | `false` | `XPING_COLLECTLOCALGITAUTHOR` | Records the git author name on local runs |
| [`CollectCiActorEmail`](#collectciactoremail) | bool | `false` | `XPING_COLLECTCIACTOREMAIL` | Records the email of the person behind a CI run |
| [`LogLevel`](#loglevel) | `LogLevel` | `Information` | `XPING_LOGLEVEL` | The lowest level of SDK message that is printed |
| [`StrictMode`](#strictmode) | bool | `false` | `XPING_STRICTMODE` | Fails the test run when the SDK cannot record or upload |
| [`LocalStorePath`](#localstorepath) | string | *(none)* | `XPING_LOCAL_STORE` | Moves the [local store](local-store.md) |

### CLI and shared settings

| Setting | Read by | Default | Description |
|---|---|---|---|
| [`CloudUrl`](#cloudurl) / `XPING_CLOUDURL` | CLI | `https://app.xping.io` | The Xping Cloud address for `xping login` and for Xping Cloud data in `xping report` |
| `XPING_APIKEY`, `XPING_PROJECTID` | SDK and CLI | *(none)* | The CLI reads them for Xping Cloud data. See [Credentials](../cli/command-reference.md#credentials). |
| [`XPING_LOCAL_STORE`](#localstorepath) | SDK and CLI | *(none)* | Moves the local store for both |
| [`XPING_SHELL`](#xping_shell) | CLI | `powershell` on Windows, `posix` elsewhere | The shell syntax of the commands `xping report` prints |
| `XPING_NO_BANNER` | SDK and CLI | *(unset)* | Any non-empty value hides the SDK's retry hint and the CLI's Xping Cloud invitation |

---

## Mode

### Mode

**Type:** `XpingMode`
**Default:** `Auto`
**Environment variable:** `XPING_MODE` (not case-sensitive)

The SDK resolves the mode once, when it starts. It decides whether the SDK uploads, writes the
local store, or does nothing.

| Value | What the SDK does |
|---|---|
| `Auto` | Resolves to one of the modes below. See the next table. |
| `LocalOnly` | Writes the [local store](local-store.md). It makes no network calls and creates no HTTP client. |
| `Cloud` | Writes the local store and uploads to Xping Cloud |
| `Disabled` | Records nothing. The same as `Enabled = false`. |

`Auto` resolves in this order. The first row that matches wins:

| Condition | Resolved mode |
|---|---|
| `Enabled` is `false` | `Disabled` |
| `StrictMode` is `true` | `Cloud` |
| `ApiKey` is set | `Cloud` |
| Otherwise | `LocalOnly` |

A `Mode` other than `Auto` wins over `StrictMode` and over the key. To keep a run local while a key
is set in your environment, set `XPING_MODE=LocalOnly`.

**A missing key is not an error in `Auto` mode.** The SDK runs local-only, as on a developer
machine without an account. See [Run without an account](../getting-started/local-first.md).

**A missing key is an error in two cases:**

- `Mode` is `Cloud`. The SDK logs the error and records nothing, not even to the local store.
- `StrictMode` is on and `Mode` is `Auto`. The test run fails. Strict mode does not fall back to
  local-only, so a pipeline that must upload fails when its key is missing.

---

## Upload settings

### ApiEndpoint

**Type:** `string`
**Default:** `https://upload.xping.io/v1`
**Environment variable:** `XPING_APIENDPOINT`

The address the SDK uploads to. Change it only when you were given another Xping Cloud address. It
must be an absolute `http` or `https` URL.

```bash
export XPING_APIENDPOINT="https://upload.xping.io/v1"
```

### ApiKey

**Type:** `string`
**Default:** *(none)*
**Environment variable:** `XPING_APIKEY`

The key that authorizes uploads to Xping Cloud. Without a key, the SDK runs [local-only](#mode).

To create one, open **Settings → API keys** in Xping Cloud. Create it with only the Upload scope.

Set the key as an environment variable or a CI secret:

```bash
export XPING_APIKEY="your-api-key"
```

Do not put it in `appsettings.json` or in code: the file is committed and copied into the build
output.

**In the `xping` CLI:** the CLI uses the key to read Xping Cloud data when you have not signed in
with `xping login`. `--api-key` wins over a sign-in. `XPING_APIKEY` and `appsettings.json` do not,
because on a developer machine they usually hold the upload key. A key with only the Upload scope
cannot read. See [Credentials](../cli/command-reference.md#credentials).

### ProjectId

**Type:** `string`
**Default:** *(none: one project for each test assembly)*
**Environment variable:** `XPING_PROJECTID`

You usually do not set this. Without it, Xping Cloud creates one project for each test assembly, on
its first upload:

| Test assembly | Project |
|---|---|
| `Billing.Tests` | `billing-tests` |
| `Api.Tests` | `api-tests` |

Set `ProjectId` to send every test assembly of the session to one project, for example in a
monorepo where several test assemblies test one product:

```bash
export XPING_PROJECTID="payment-service"
```

Xping Cloud applies these rules to the value:

- Only ASCII letters, digits, `-` and `_`. It starts with a letter or a digit.
- At most 128 characters. No spaces.
- It is converted to lowercase, so `My-App` and `my-app` are one project.
- A blank value counts as unset.

Xping Cloud identifies a project by its name. Renaming a test assembly, or changing `ProjectId`,
starts a new project, and the old history stays in the old one. On a plan with a low project
limit, a solution-wide run can reach the limit on its first upload. Set `ProjectId` to use one
project instead.

**In the `xping` CLI:** the CLI uses the same value to find the Xping Cloud project of a local run.

### BatchSize

**Type:** `int`
**Default:** `100`
**Valid range:** `1` to `1000`
**Environment variable:** `XPING_BATCHSIZE`

**Cloud mode only.** In Cloud mode, the SDK uploads during the session as well as at its end:

1. When `BatchSize` executions are waiting
2. When the [FlushInterval](#flushinterval) timer fires
3. When the session ends: the rest, in the final upload

A smaller batch shows results in Xping Cloud sooner and makes more uploads. A larger batch makes
fewer uploads. In every mode, the SDK keeps every execution in memory until the session ends,
because it writes the whole session to the local store.

```bash
export XPING_BATCHSIZE="200"
```

### FlushInterval

**Type:** `TimeSpan`
**Default:** `00:00:30`
**Valid range:** greater than zero
**Environment variable:** `XPING_FLUSHINTERVAL` (whole seconds or `hh:mm:ss`)

**Cloud mode only.** The time between uploads during the session. The upload at the end of the
session does not wait for it. In `LocalOnly` mode, the SDK starts no timer.

```bash
export XPING_FLUSHINTERVAL="60"
```

### UploadTimeout

**Type:** `TimeSpan`
**Default:** `00:00:30`
**Valid range:** 1 second to 1 day
**Environment variable:** `XPING_UPLOADTIMEOUT` (whole seconds or `hh:mm:ss`)

The time limit of one upload attempt. An attempt that takes longer is retried, as
[MaxRetries](#maxretries) allows. The whole upload, retries and delays included, stops after
`2 × UploadTimeout` (60 seconds by default), so a failing upload cannot hold up the end of a test
run.

```bash
export XPING_UPLOADTIMEOUT="60"
```

### MaxRetries

**Type:** `int`
**Default:** `3`
**Valid range:** `0` to `10`
**Environment variable:** `XPING_MAXRETRIES`

How many times the SDK retries a failed upload. `0` turns off retries.

The SDK retries:

- HTTP 5xx responses and HTTP 429
- Network errors and timeouts

It does not retry any other 4xx response, such as 400, 401 or 403.

When at least half of the upload attempts in 30 seconds fail with a 5xx response, a network error
or a timeout, and there were at least 10 attempts, the SDK stops uploading for 30 seconds. Retries
count as attempts. It logs
`Circuit breaker is open: Too many consecutive failures`.

```bash
export XPING_MAXRETRIES="5"
```

### RetryDelay

**Type:** `TimeSpan`
**Default:** `00:00:02`
**Valid range:** 0 to 1 day
**Environment variable:** `XPING_RETRYDELAY` (whole seconds or `hh:mm:ss`)

The base delay between retries. The delay doubles with each retry, and the SDK adds a random part,
so that parallel runs do not retry at the same moment. With the defaults, the retries come after
about 2, 4 and 8 seconds. The `2 × UploadTimeout` limit still applies, so a long delay can leave
fewer retries than `MaxRetries`.

```bash
export XPING_RETRYDELAY="5"
```

### EnableCompression

**Type:** `bool`
**Default:** `true`
**Environment variable:** `XPING_ENABLECOMPRESSION`

Compresses each upload with gzip. Turn it off only to read the uploaded requests while you debug a
network problem, or when a proxy rejects compressed requests.

```bash
export XPING_ENABLECOMPRESSION="false"
```

---

## Environment and branches

### Environment

**Type:** `string`
**Default:** `Default`
**Environment variable:** `XPING_ENVIRONMENT`

The **deployed environment your tests target**, such as `Staging` or `Production`. Xping Cloud
scores each test separately for each environment.

It is not a name for where the tests ran. A developer's run and the CI run of the same suite
against the same deployment belong to one environment. The SDK records whether a run is on CI, and
which branch and commit it tested, in other fields. See
[Data Xping collects](collected-data.md#the-environment).

- Set it only when a pipeline tests a different deployment.
- Use a few stable names. Do not use a name that changes from run to run, such as one for each pull
  request, branch or build agent: each new name splits the history of every test.
- When you test one deployment, leave it unset.

```bash
export XPING_ENVIRONMENT="Staging"
```

**The SDK does not read `ASPNETCORE_ENVIRONMENT` or `DOTNET_ENVIRONMENT` for this setting.** These
variables tell the application how to configure itself, and a container image often sets them. To
use one, pass it in the shell of the test step:

```yaml
- name: Run tests
  shell: bash
  run: XPING_ENVIRONMENT="$ASPNETCORE_ENVIRONMENT" dotnet test
```

On GitHub Actions, `${{ env.ASPNETCORE_ENVIRONMENT }}` does not work for this. The `env` context
holds only the variables that the workflow declares, not the ones the runner image or a container
sets.

### DefaultBranch

**Type:** `string`
**Default:** *(none)*
**Environment variable:** `XPING_DEFAULTBRANCH`

The branch Xping Cloud treats as the repository's default branch for this pipeline's runs. Xping
Cloud counts a CI push to the default branch as a Canonical run, the strongest evidence behind a
confidence score.

The default branch of a run comes from the first of these that has a value:

1. `DefaultBranch` / `XPING_DEFAULTBRANCH`
2. The value the CI platform reports. Only GitHub Actions and GitLab CI/CD report one.
3. The project's default branch setting in Xping Cloud
4. `main` or `master`

- The SDK sends it only from CI.
- The SDK removes a leading `refs/heads/`, `refs/remotes/origin/` or `origin/`, so you can pass
  Jenkins' `GIT_BRANCH` (`origin/main`) as it is. Branch names are case-sensitive.
- On TeamCity it has no effect, because the SDK reports no branch there.
- It applies to new runs only.

**Use it with care.** It replaces the value GitHub Actions and GitLab CI/CD report, and it wins
over the project's setting in Xping Cloud. A stale value, for example in a shared pipeline
template, stops every Canonical run. When the value differs from what the platform reports, the SDK
logs a warning that names both.

```bash
export XPING_DEFAULTBRANCH="develop"
```

---

## What the SDK records

[Data Xping collects](collected-data.md) lists every field, and which of these switches controls
it.

### Enabled

**Type:** `bool`
**Default:** `true`
**Environment variable:** `XPING_ENABLED`

Turns the SDK off without removing it from the test project. With `false`, the tests run as usual.
The SDK records nothing and prints
`SDK is disabled via configuration (Enabled=false). Tests will run without observability tracking.`
With `Mode = Disabled`, the line says `(Mode=Disabled)` instead.

```bash
export XPING_ENABLED="false"
```

### UploadStackTraces

**Type:** `bool`
**Default:** `false`
**Environment variable:** `XPING_UPLOADSTACKTRACES`

Uploads the stack trace text of failed tests to Xping Cloud. It is off by default, because a stack
trace can contain source paths, file names and details of your code.

- The local store always keeps the full stack trace. `xping report` needs it to group failures.
- The upload always carries a hash of the stack trace. The hash covers the method names of up to
  five of your own frames, without file paths or line numbers. Xping Cloud groups failures by it.
- With `false`, a failed test with a trace is uploaded with `stackTraceOmitted: true` and no trace
  text.
- With `true`, the SDK uploads the trace text too. The Xping Cloud features that read it, such as
  the failure frames in pull request comments and AI failure summaries, need it.

```bash
export XPING_UPLOADSTACKTRACES="true"
```

### EnablePullRequestDetection

**Type:** `bool`
**Default:** `true`
**Environment variable:** `XPING_ENABLEPULLREQUESTDETECTION`

Records the pull request a CI build belongs to: its number, branches, head commit and author. Xping
Cloud uses it to file the run under the PR and to post the
[pull request comment](../cloud/reading-the-pr-comment.md). Turn it off to send no PR context.

The SDK detects PR context on:

- GitHub Actions, on github.com, GHE.com and GitHub Enterprise Server
- GitLab CI/CD merge request pipelines, on gitlab.com and self-managed instances
- Azure Pipelines PR builds, for Azure Repos and GitHub repositories
- Jenkins multibranch PR builds, for GitHub, GitLab and Azure DevOps repositories

[Which commit is recorded](collected-data.md#which-commit-is-recorded) lists the variables each
platform reads. [Set up CI](../getting-started/ci-cd-setup.md#jenkins) says what Jenkins needs.

```bash
export XPING_ENABLEPULLREQUESTDETECTION="false"
```

### CollectLocalGitAuthor

**Type:** `bool`
**Default:** `false`
**Environment variable:** `XPING_COLLECTLOCALGITAUTHOR`

On a run outside CI, inside a git repository, records the git author name as the `Git.Actor`
property. The SDK reads `[user] name` from the repository's `.git/config`, or else from your global
git config (`~/.gitconfig`, then `$XDG_CONFIG_HOME/git/config`).

It is off by default, because the git author name is usually a person's full name. Turn it on only
when your team agrees to share it.

It has no effect on CI, where the person behind the run comes from the CI platform, or outside a
git repository.

```bash
export XPING_COLLECTLOCALGITAUTHOR="true"
```

### CollectCiActorEmail

**Type:** `bool`
**Default:** `false`
**Environment variable:** `XPING_COLLECTCIACTOREMAIL`

On a CI run, records the email address that the platform gives for the person behind the run, as
the `CI.ActorEmail` property. The SDK reads it from `BUILD_REQUESTEDFOREMAIL` (Azure Pipelines),
`GITLAB_USER_EMAIL` (GitLab), `CHANGE_AUTHOR_EMAIL` or `ghprbPullAuthorEmail` (Jenkins) and
`APPVEYOR_REPO_COMMIT_AUTHOR_EMAIL` (AppVeyor).

It is off by default, because an email address is personal data. Xping Cloud does not count
committers by it. It uses it only to link a committer to a workspace member automatically on Azure
Pipelines, GitLab, Jenkins and AppVeyor. Without it, an Owner or Admin links them by hand. GitHub
Actions is not affected. See [Who is behind the run](collected-data.md#who-is-behind-the-run).

It has no effect on local runs, or on `CI.Actor` and `CI.ActorId`, which the SDK sends either way.

```bash
export XPING_COLLECTCIACTOREMAIL="true"
```

---

## Logging and errors

### LogLevel

**Type:** `Microsoft.Extensions.Logging.LogLevel`
**Default:** `Information`
**Environment variable:** `XPING_LOGLEVEL` (not case-sensitive)

The lowest level of SDK message that is printed: `Trace`, `Debug`, `Information`, `Warning`,
`Error`, `Critical` or `None`. The SDK prints messages below `Warning` to stdout, and `Warning` and
higher to stderr.

Set it to `Debug` when the SDK is quieter than you expect. Debug messages explain failures that the
SDK absorbs so that it never breaks your test run, such as a local store that it could not write.
`None` silences the SDK.

```bash
export XPING_LOGLEVEL="Debug"
```

```csharp
using Microsoft.Extensions.Logging;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.NUnit; // your adapter's namespace

XpingContext.Initialize(new XpingConfiguration { LogLevel = LogLevel.Debug });
```

### StrictMode

**Type:** `bool`
**Default:** `false`
**Environment variable:** `XPING_STRICTMODE`

By default, the SDK never fails your test run. When it cannot start or cannot upload, it logs the
error and the tests run as usual. In strict mode, the same errors fail the test run, so a CI
pipeline fails when it cannot record its tests.

| What goes wrong | Default | Strict mode |
|---|---|---|
| No API key, `Mode` is `Auto` | The SDK runs local-only | The test run fails before any test runs |
| No API key, `Mode` is `Cloud` | The SDK logs the error and records nothing | The test run fails before any test runs |
| An invalid setting, such as `BatchSize = 2000` | The SDK logs the error and records nothing. See [What the SDK prints](#what-the-sdk-prints). | The test run fails before any test runs |
| The SDK fails to start, for example because `appsettings.json` does not parse | The SDK prints `[Xping] SDK initialization failed`, and the tests run unrecorded | The test run fails before any test runs |
| An upload fails | The SDK logs the error. The local store still gets the session. | The test run fails at its end |

When the SDK cannot start in strict mode, the test process stops (`Environment.FailFast`) before
any test runs. Strict mode must then come from `XPING_STRICTMODE`, `Xping__StrictMode` or code,
because the SDK cannot read it from a file that does not parse.

When an upload fails in strict mode, each adapter fails the run in its own way. Every test that ran
stays in the TRX file and the other reports, and `dotnet test` exits with a non-zero code.

| Adapter and runner | What you see |
|---|---|
| xUnit v2 | An error from the runner: `Catastrophic failure: XpingNetworkException` |
| xUnit v3 on vstest | An error from the runner, which fails the run |
| xUnit v3 on Microsoft.Testing.Platform | The runner reports the error, and the test executable exits with code 1 after it writes its reports |
| MSTest | `[AssemblyCleanup]` fails, and MSTest reports it on the last test result |
| NUnit on vstest | The test process stops about two seconds after the upload fails, once every result has reached `dotnet test`. `dotnet test` reports the run as aborted. |
| NUnit on Microsoft.Testing.Platform | The test executable exits with code 1 after it writes its reports |

On NUnit and MSTest, the upload error appears when the session ends, so the `[OneTimeTearDown]` or
`[AssemblyCleanup]` method must call `XpingContext.FinalizeAndShutdownAsync()`. On NUnit, that call
is what fails the run: NUnit reports an exception from `[OneTimeTearDown]` only as a warning.

```bash
XPING_STRICTMODE=true dotnet test
```

### What the SDK prints

These lines come from an NUnit project on vstest, with
`dotnet test --logger "console;verbosity=normal"`:

```
# Default: a configuration error, here XPING_MODE=Cloud without a key
[Xping 10:43:01 ERR] Configuration validation failed: ApiKey is required in Cloud mode. SDK disabled - tests will run without observability tracking.

# Strict mode: a configuration error
[Xping] Strict mode configuration error: Xping configuration invalid: ApiKey is required when StrictMode is enabled.

# Default: an upload error
[Xping 10:00:08 ERR] Network error occurred → HttpRequestException: Connection refused (127.0.0.1:9)

# Strict mode: an upload error
[Xping] Xping network error in strict mode: HTTP request failed: Connection refused (127.0.0.1:9)
```

An invalid value in the configuration object you pass to `Initialize(configuration)` gives another
line, because the SDK rejects the object before it starts:

```
[Xping] SDK initialization failed: Xping configuration invalid: BatchSize cannot exceed 1000. Tests run without Xping tracking.
```

An invalid value from an `XPING_*` variable or from `appsettings.json` gives the
`Configuration validation failed:` line above.

When the SDK fails to start for any other reason, and strict mode is off, it prints this line once
and the tests run unrecorded:

```
[Xping] SDK initialization failed: <reason>. Tests run without Xping tracking.
```

Where that line appears depends on the runner:

| Runner | Where to look |
|---|---|
| NUnit | The `dotnet test` output, at the default verbosity |
| xUnit (v2 and v3) | `dotnet test -v n`, or the runner's diagnostics with `"diagnosticMessages": true` in `xunit.runner.json` |
| MSTest | The output of the first test that ran, in the TRX file (`dotnet test --logger trx`) |

The next run in the same process tries again, so an IDE's test server recovers once you fix the
cause.

### Validation rules

The SDK checks the configuration when it starts. It reports every rule that fails, in one line:

| Setting | Rule | Message |
|---|---|---|
| `Mode` | A defined value | `Mode has an undefined value (N). Valid values are Auto, LocalOnly, Cloud, and Disabled.` |
| `LogLevel` | A defined value | `LogLevel has an undefined value (N). Valid values are Trace, Debug, Information, Warning, Error, Critical, and None.` |
| `ApiKey` | Set, when the mode resolves to `Cloud` | `ApiKey is required in Cloud mode.` With `StrictMode` on and `Mode` set to `Auto`: `ApiKey is required when StrictMode is enabled.` |
| `ApiEndpoint` | Set, and an absolute `http` or `https` URL | `ApiEndpoint is required.` / `ApiEndpoint must be a valid HTTP or HTTPS URL.` |
| `BatchSize` | 1 to 1000 | `BatchSize must be greater than zero.` / `BatchSize cannot exceed 1000.` |
| `FlushInterval` | Greater than zero | `FlushInterval must be greater than zero.` |
| `MaxRetries` | 0 to 10 | `MaxRetries cannot be negative.` / `MaxRetries cannot exceed 10.` |
| `RetryDelay` | 0 to 1 day | `RetryDelay cannot be negative.` / `RetryDelay cannot exceed 1 day.` |
| `UploadTimeout` | 1 second to 1 day | `UploadTimeout must be at least 1 second.` / `UploadTimeout cannot exceed 1 day.` |

To check a configuration in code, use `XpingConfigurationBuilder.TryBuild`:

```csharp
using System;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.NUnit; // your adapter's namespace

var builder = new XpingConfigurationBuilder()
    .WithMode(XpingMode.Cloud)
    .WithBatchSize(2000);

if (builder.TryBuild(out XpingConfiguration? configuration, out string[] errors))
{
    XpingContext.Initialize(configuration!);
}
else
{
    foreach (string error in errors)
        Console.WriteLine(error);
}

// Prints:
// ApiKey is required in Cloud mode.
// BatchSize cannot exceed 1000.
```

`TryBuild` and `Build` check only the values you set on the builder. They do not read `XPING_*`
variables, which the SDK applies later. So `Build` fails for a builder that sets
`WithStrictMode(true)` and relies on `XPING_APIKEY` for the key.

---

## Local store and CLI

### LocalStorePath

**Type:** `string`
**Default:** *(none: `.xping/` at the repository root)*
**Environment variable:** `XPING_LOCAL_STORE`

Moves the [local store](local-store.md). Leave it unset to use `.xping/` at the repository root,
where `xping report` looks. Set it when the repository is not writable, or when the history must
live outside a workspace that is deleted between builds.

```bash
export XPING_LOCAL_STORE="/var/tmp/xping-store"
```

- The SDK reads `XPING_LOCAL_STORE` once, when it starts. A change during the run does not move
  where the run is recorded.
- The CLI reads the same variable, so an exported `XPING_LOCAL_STORE` moves both.
- An absolute path is used as it is. A relative path, such as `.xping-ci`, is read from the
  repository root: the nearest folder above the test assembly that holds `.git`, a `*.sln` file or
  a `*.slnx` file. Without a repository root, it is read from the nearest folder that holds a
  `.csproj`, `.fsproj` or `.vbproj` file, and without one of those, from the current directory. So
  the SDK and the CLI find the same folder when the CLI runs anywhere inside the repository.
- On Windows, a path is absolute only with a drive and a separator (`C:\xping-store`) or as a UNC
  path. `\xping-store` and `C:xping-store` are relative, and are read as above.
- When the SDK cannot write to the folder, the run has no local history: the SDK does not fall
  back to another folder.
- A path set in code or in `appsettings.json` moves only the SDK. Export the variable for the CLI
  too. The CLI's `--directory` option changes where the CLI starts looking for the repository, and
  so the folder a relative path is read from.

### CloudUrl

**Type:** `string`
**Default:** `https://app.xping.io`
**Environment variable:** `XPING_CLOUDURL`

CLI only. The Xping Cloud address that `xping login` signs in to, and that `xping report` reads
Xping Cloud data from. Leave it unset unless you were given another address.

The first of these that has a value wins: the `--cloud-url` option, `XPING_CLOUDURL`,
`Xping__CloudUrl`, then `Xping:CloudUrl` in `appsettings.{environment}.json` and `appsettings.json`.
The CLI reads the files from the current directory, or from the folder that `--directory` names.

The value must be an absolute `https` URL without a path, query or fragment. `http` is accepted
only for `localhost` and `127.0.0.1`. Case, a default port and a trailing slash are ignored.

```bash
export XPING_CLOUDURL="https://app.xping.io"
```

### XPING_SHELL

CLI only. The shell syntax of the commands that `xping report` prints, such as the command that
runs a test again: `posix` or `powershell`. Without it, the CLI uses PowerShell syntax on Windows
and POSIX syntax elsewhere. Set it when your shell differs, for example Git Bash on Windows. For
any other value, the CLI prints a warning and uses the default. See
[Known limitations](../troubleshooting/known-limitations.md).

```bash
export XPING_SHELL="posix"
```

---

## Examples

### appsettings.json

The SDK reads this file only with `XpingContext.Initialize()`, without an argument. Keep the API key
out of it.

```json
{
  "Xping": {
    "ProjectId": "my-application",
    "Environment": "Staging",
    "BatchSize": 200,
    "FlushInterval": "00:01:00",
    "UploadStackTraces": false,
    "LogLevel": "Information"
  }
}
```

### CI variables (GitHub Actions)

```yaml
env:
  XPING_APIKEY: ${{ secrets.XPING_APIKEY }}
  XPING_PROJECTID: "my-application"
  XPING_STRICTMODE: "true"
```

[Set up CI](../getting-started/ci-cd-setup.md) has a full pipeline for each CI platform.

### In code

```csharp
using System;
using Xping.Sdk.Core.Configuration;
using Xping.Sdk.NUnit; // your adapter's namespace

// XPING_APIKEY still applies on top of this object.
XpingContext.Initialize(new XpingConfiguration
{
    ProjectId = "my-application",
    BatchSize = 200,
    FlushInterval = TimeSpan.FromMinutes(1),
});
```

`XpingContext` is in your adapter's namespace: `Xping.Sdk.NUnit`, `Xping.Sdk.XUnit`,
`Xping.Sdk.XUnit.V3` or `Xping.Sdk.MSTest`. Each [quick start](../getting-started/quickstart-nunit.md)
shows where to call it.

---

## Troubleshooting

**The SDK ignores `appsettings.json`:**

- Check that the SDK starts with `XpingContext.Initialize()`, without an argument. With a
  configuration object, it reads only `StrictMode` from the file.
- Check that the file is in the test project's output folder (`<CopyToOutputDirectory>`).
- Check that the JSON parses. A file that does not parse stops the SDK from starting, and the SDK
  prints `[Xping] SDK initialization failed`.

**An environment variable has no effect:**

- Check the name: `XPING_` with one underscore, then the setting name in capitals, such as
  `XPING_BATCHSIZE`. The local store variable is `XPING_LOCAL_STORE`.
- Check the value. A value that does not parse, such as `XPING_ENABLED=yes`, is ignored. Use `true`
  or `false`.
  A duration that does not parse is ignored too, and the SDK prints a warning that names it.
- Check that the test process gets it. An IDE reads the environment when it starts.

**The SDK disabled itself:** the line after `Configuration validation failed:`, or after
`Xping configuration invalid:` for a configuration object, names each setting that broke a rule.
See [Validation rules](#validation-rules).

---

## See also

- [Data Xping collects](collected-data.md)
- [Local store](local-store.md)
- [Set up CI](../getting-started/ci-cd-setup.md)
- [Common issues](../troubleshooting/common-issues.md)
