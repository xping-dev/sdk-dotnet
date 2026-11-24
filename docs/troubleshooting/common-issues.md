---
uid: troubleshooting-common-issues
title: Common issues
---

# Common issues

This page lists the problems people run into most often, and how to fix them. Most fixes start with
the SDK's own output. To see it, and to read each line, see [Debugging](debugging.md).

---

## Tests don't appear in Xping Cloud

Read the SDK's start line first. See [The start line](debugging.md#the-start-line).

### The SDK runs in local-only mode

```
[Xping 13:14:41 INF] Initialized in local-only mode. SessionId: "…", Environment: Default. No API key configured - results stay on this machine.
```

The SDK found no API key, so it keeps the results on this machine and uploads nothing. This is not an
error: `xping report` reads these runs. To upload, set the key in the environment that runs the
tests:

```bash
export XPING_APIKEY="<your key>"
```

When the line says `Mode is set to LocalOnly` instead, the
[`Mode`](../configuration/configuration-reference.md#mode) setting keeps the SDK local. Remove it, or
set it to `Cloud`.

In Xping Cloud, an Owner or an Admin creates the key in **Settings → API keys**. Create it with only
the Upload scope. See [API keys](../cloud/connect.md#api-keys).

### The SDK is off

```
[Xping 13:14:41 WRN] SDK is disabled via configuration (Enabled=false). Tests will run without observability tracking.
```

[`Enabled`](../configuration/configuration-reference.md#enabled) is `false`, or `Mode` is `Disabled`
(the line then says `Mode=Disabled`). Remove the setting, for example `XPING_ENABLED`.

### A setting is invalid

```
[Xping 10:43:01 ERR] Configuration validation failed: ApiKey is required in Cloud mode. SDK disabled - tests will run without observability tracking.
```

The SDK records nothing. The line names each setting that breaks a rule. See
[Validation rules](../configuration/configuration-reference.md#validation-rules).

### The session never ends

The SDK sends the last results when the session ends. On NUnit and MSTest, your own code ends it:

- **NUnit:** the `[OneTimeTearDown]` method of a `[SetUpFixture]` must call
  `XpingContext.FinalizeAndShutdownAsync()`. See the
  [NUnit quickstart](../getting-started/quickstart-nunit.md).
- **MSTest:** an `[AssemblyCleanup]` method in the test assembly must call
  `XpingContext.FinalizeAndShutdownAsync()`. See the
  [MSTest quickstart](../getting-started/quickstart-mstest.md). Without it, the SDK ends the session
  as the process exits, and on vstest the upload is usually lost. See
  [Known limitations](known-limitations.md#mstest-a-session-finalized-by-the-process-exit-safety-net-loses-its-upload-on-vstest).

xUnit v2 and v3 end the session themselves, through the `[assembly: TestFramework(...)]` attribute.
See the [xUnit quickstart](../getting-started/quickstart-xunit.md).

### The upload failed

The SDK logs an upload error, and the tests pass or fail as usual. Look for `ERR` lines. See
[Upload errors](#upload-errors) and [Network errors](#network-errors).

---

## Upload errors

When Xping Cloud rejects an upload, the SDK logs one line in this form:

```
<category> (HTTP <status>) for <endpoint> [<code>]: <reason> Action: <action>
```

For example:

```
[Xping 13:16:24 ERR] Authentication failed (HTTP 401) for https://upload.xping.io/v1 [Error.ApiKey.InvalidApiKey]: The provided API key is not recognized. Action: Verify the ApiKey value matches a key issued for this workspace at https://upload.xping.io/v1
```

- The `<code>` in brackets is Xping Cloud's error code. It says what went wrong more exactly than the
  status code.
- The `<reason>` is Xping Cloud's explanation, here `The provided API key is not recognized.` For a
  plan limit, it says which limit the run reached.
- The `Action:` is what the SDK suggests for that code.

When the same error repeats in the session, the SDK prints a short line, such as
`Same HTTP 401 error (2nd occurrence, batch size: 0)`.

The SDK retries a 5xx response and a 429. It does not retry any other 4xx response.

### The API key

| Code | Action the SDK prints |
|---|---|
| `Error.ApiKey.MissingApiKey` | Set the ApiKey in your Xping configuration; it is sent as the X-API-Key header |
| `Error.ApiKey.InvalidApiKey` | Verify the ApiKey value matches a key issued for this workspace at `<endpoint>` |
| `Error.ApiKey.ExpiredApiKey` | The key has passed its expiry date - issue a new API key and update your configuration |
| `Error.ApiKey.RevokedApiKey` | This key was revoked - issue a new API key and update your configuration |

These come with HTTP 401 (`Authentication failed`). Check that `XPING_APIKEY` holds the whole key,
with no space or line break. To issue a new key, see [API keys](../cloud/connect.md#api-keys).

### What the key may do

| Code | Action the SDK prints |
|---|---|
| `Error.ApiKey.InsufficientScope` | Grant the API key the UPLOAD scope, or use a key that already has it |
| `Error.ApiKey.IpNotAllowed` | The key restricts client IP addresses - add this runner's public IP to the key's allow list |
| `Authorization.InsufficientAccess` | This endpoint requires an API key principal with upload access |

These come with HTTP 403 (`Authorization failed`). A key works only from its **allowed addresses**,
when the key has any. A CI agent's public address can change between runs.

### Plan and usage limits

| Code | Action the SDK prints |
|---|---|
| `Error.Subscription.ProjectLimitReached` | This run names a project the workspace does not have yet and the project limit is reached - upgrade the plan, or set ProjectId to report into an existing project. A solution-wide run reports one project per test assembly, so it can request several at once |
| `Error.Subscription.TestRunLimitReached`, `Error.Subscription.UsageLimitExceeded` | Upgrade the plan to continue ingesting test results this billing period |
| `Error.Billing.PaymentFailed`, `Error.Billing.CardDeclined`, `Error.Billing.InsufficientFunds` | Update the workspace payment method to restore uploads |
| `Error.ApiKey.UsageLimitReached` | This key reached its configured usage cap - raise the cap or use a different key |
| `Error.ApiKey.RateLimitExceeded` | Back off and retry later, or reduce upload frequency by batching more executions per upload |

`ProjectLimitReached` often comes from a solution-wide `dotnet test`. Each test assembly reports into
its own project, so one run can ask for several new projects. To send them all to one project, set
[`ProjectId`](../configuration/configuration-reference.md#projectid). See
[My tests created several projects](#my-tests-created-several-projects).

For a rate limit, a larger [`BatchSize`](../configuration/configuration-reference.md#batchsize) makes
fewer uploads. For what each plan includes, see
[Plans and retention](../cloud/plans-and-retention.md).

### The project

| Code | Action the SDK prints |
|---|---|
| `Error.Project.ProjectDisabled` | The target project is disabled and rejects uploads - re-enable it in Xping Cloud |

### The request

| Code | Action the SDK prints |
|---|---|
| `Error.Uploads.MissingProjectKey` | The upload named no project: no ProjectId is configured and no test assembly could be resolved for this session. Set ProjectId to pin one, or check that your tests report an assembly name |
| `Error.Uploads.BatchSizeExceeded` | Reduce the number of executions per upload - the server accepts at most 1000 per batch |
| `Error.Uploads.UnrecognizedPayload`, `Error.Uploads.UnrecognizedState` | The server could not read this payload - update Xping.Sdk to a version compatible with the API |
| `Error.Uploads.MissingSessionId` | The upload was sent without a session id - this indicates an SDK defect, please report it |

### A response with no error code

A proxy or a load balancer can answer instead of Xping Cloud. The response then has no Xping error
code, and the SDK suggests an action from the status code:

| Status | Category | Action the SDK prints |
|---|---|---|
| 400, 422 | Upload rejected | Check the upload configuration (ApiEndpoint) and the SDK version |
| 401 | Authentication failed | Verify the ApiKey is set and valid for `<endpoint>` |
| 402 | Subscription limit reached | Review the workspace subscription - a plan limit or billing issue is blocking uploads |
| 403 | Authorization failed | Verify the API key has the UPLOAD scope and that this client's IP is permitted |
| 404 | API endpoint not found | Verify the ApiEndpoint configuration matches your deployment |
| 409 | Upload conflicts with the current project state | The target project rejected the upload in its current state - check it in Xping Cloud |
| 413 | Payload too large | Reduce the number of executions per upload |
| 429 | Rate limit exceeded | Reduce test execution frequency, or retry after the interval the server indicates |
| 5xx | Server error | Retry later; if this persists, contact support with the receipt of a failing run |

The `<reason>` is then the response body, or `No additional error details provided` when it is
empty. A 404 often means that [`ApiEndpoint`](../configuration/configuration-reference.md#apiendpoint)
is wrong. Its default is `https://upload.xping.io/v1`.

---

## Network errors

When the SDK cannot reach the endpoint, or the request takes too long, it logs one of these:

| Message | Meaning |
|---|---|
| `Network error occurred → HttpRequestException: <reason>` | The connection failed, for example `Connection refused` or a DNS error. |
| `Request timeout after 30s` | One attempt took longer than [`UploadTimeout`](../configuration/configuration-reference.md#uploadtimeout). |
| `Upload timed out after 60s` | The whole upload, with its retries, took longer than twice `UploadTimeout`. |
| `Circuit breaker is open: Too many consecutive failures` | Uploads are paused. See [Uploads pause for 30 seconds](#uploads-pause-for-30-seconds). |

The SDK retries a network error and a timeout, up to
[`MaxRetries`](../configuration/configuration-reference.md#maxretries) times.

Check that the machine reaches `https://upload.xping.io/v1` over HTTPS on port 443. See
[Check the network](debugging.md#check-the-network).

### Behind a proxy

The SDK has no proxy setting of its own. It uses the standard .NET proxy settings: the
`HTTPS_PROXY`, `HTTP_PROXY` and `NO_PROXY` environment variables, and the system proxy settings on
Windows and macOS. Set them in the environment that runs the tests:

```bash
export HTTPS_PROXY="http://proxy.example.com:8080"
```

For a proxy that needs a sign-in, put the user name and password in the URL:
`http://user:password@proxy.example.com:8080`.

### Slow uploads

When uploads time out on a slow connection, raise `UploadTimeout`:

```bash
export XPING_UPLOADTIMEOUT="60"
```

Leave [`UploadStackTraces`](../configuration/configuration-reference.md#uploadstacktraces) off, which
is the default, to keep stack trace text out of the uploads.

### Uploads pause for 30 seconds

```
[Xping 13:20:11 ERR] Circuit breaker is open: Too many consecutive failures → BrokenCircuitException: …
[Xping 13:20:11 WRN] Status: Upload attempts will resume after circuit breaker resets (30 seconds)
```

The SDK stops uploading for 30 seconds when, in 30 seconds:

- there were at least 10 upload attempts, retries included, and
- at least half of them failed with a 5xx response, a network error or a timeout.

A 4xx response, such as 401, 403 or 429, does not count. While uploads are paused, each upload fails
at once with the message above. After 30 seconds, the SDK tries again.

The pause means that Xping Cloud or the network failed many times in a short time. Look for the
errors logged before it.

---

## An upload failure fails the run

**Symptom:** the test run fails at its end with an upload error, although the tests passed. On NUnit
with vstest, `dotnet test` reports the run as aborted:

```
The active test run was aborted. Reason: Test host process crashed : …
[Xping] Xping network error in strict mode: HTTP request failed: Connection refused (127.0.0.1:9)
```

By default, an upload failure never fails your tests. The SDK retries the upload, logs the error,
and the local store still gets the run. With [`StrictMode`](../configuration/configuration-reference.md#strictmode)
on (`XPING_STRICTMODE=true`), the same failure fails the run, so a CI pipeline notices that it could
not record its tests. Every test that ran stays in the TRX file and the other reports.

**Solution:**

- Fix the cause of the upload failure. The SDK logs it. See [Upload errors](#upload-errors) and
  [Network errors](#network-errors).
- For a build with no network, set `XPING_MODE=LocalOnly`. The SDK then keeps the run on the machine
  and makes no uploads. Do not just leave the API key unset: with `StrictMode` on, a missing key fails
  the run before any test runs. `XPING_ENABLED=false` also turns off the local history.

---

## My tests created several projects

That is the default. Each test assembly reports into its own project, so a solution-wide
`dotnet test` that covers `Billing.Tests`, `Api.Tests` and `Web.Tests` produces three projects. Xping
Cloud creates each one on its first upload.

To report them all as one project, pin it:

```bash
export XPING_PROJECTID="my-app"
```

---

## The configuration is not loading

**Symptoms:** the SDK uses its defaults, or ignores an environment variable or `appsettings.json`.
The start line shows what the SDK used. See [Check the configuration](debugging.md#check-the-configuration).

### Environment variable names

The SDK's environment variables are `XPING_` with one underscore, then the setting name, such as
`XPING_APIKEY` or `XPING_BATCHSIZE`. They win over every other source, including a configuration
object passed in code.

`Xping__ApiKey`, with two underscores, is the .NET configuration form. The SDK reads it only when it
starts with `XpingContext.Initialize()`, without a configuration object. Use `XPING_APIKEY`.

### `appsettings.json` is not read

- The SDK reads `appsettings.json` only when it starts with `XpingContext.Initialize()`, without a
  configuration object. With an object, it reads only `StrictMode` from the file.
- The SDK reads the file from the current directory. Under `dotnet test`, that is the test project's
  output folder, so copy the file there:

  ```xml
  <ItemGroup>
    <None Update="appsettings.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
  ```

- The SDK also reads `appsettings.{environment}.json`. The environment comes from
  `ASPNETCORE_ENVIRONMENT` or `DOTNET_ENVIRONMENT`, and is `Production` when neither is set.
- A file that does not parse stops the SDK from starting. The SDK then prints
  `[Xping] SDK initialization failed: …`, and the tests run unrecorded.

The keys in `appsettings.json` are not case-sensitive: `apikey` and `ApiKey` both work.

### Durations

`FlushInterval`, `RetryDelay` and `UploadTimeout` take whole seconds (`30`) or a time with colons
(`00:00:30`). The SDK ignores any other value, keeps the default, and logs a warning:

```
FlushInterval '1.5' is not a duration and was ignored. Use whole seconds (30) or a time with colons (00:00:30).
```

The warning names the setting as you wrote it, such as `XPING_FLUSHINTERVAL` or
`Xping:FlushInterval`.

For every setting and its rules, see the
[Configuration reference](../configuration/configuration-reference.md).

---

## CI is not detected

**Symptoms:** a pipeline run is recorded as local. `IsCIEnvironment` is `false`, and the run has no
build number or branch from the CI.

The SDK detects the CI from the platform's own environment variables. It needs no setting. It
detects:

| Platform | Variable it reads |
|---|---|
| GitHub Actions | `GITHUB_ACTIONS=true` |
| Azure Pipelines | `TF_BUILD` |
| Jenkins | `JENKINS_URL` |
| GitLab CI/CD | `GITLAB_CI=true` |
| CircleCI | `CIRCLECI=true` |
| Travis CI | `TRAVIS=true` |
| TeamCity | `TEAMCITY_VERSION` |
| Bitbucket Pipelines | `BITBUCKET_PIPELINE_UUID` |
| AppVeyor | `APPVEYOR=True` |
| Any other CI | `CI=true` or `CI=True` |

Detection fails only when the test process does not get these variables. This happens most often
when the tests run in a container. Pass the CI's variables into the container. On another CI, set
`CI=true` in the pipeline.

The environment name `Default` on a CI run is not a symptom. The
[environment name](../configuration/configuration-reference.md#environment) names the deployment
that the tests run against, not the machine. Do not set `XPING_ENVIRONMENT=CI`: it splits each
test's history away from the local runs of the same suite.

For each platform's setup, see [Set up CI](../getting-started/ci-cd-setup.md).

---

## The API key in CI

Store the key as a secret in your CI platform, and pass it to the test step as `XPING_APIKEY`. Do
not put it in `appsettings.json` or in source control.

```yaml
# GitHub Actions
- name: Run tests
  env:
    XPING_APIKEY: ${{ secrets.XPING_APIKEY }}
  run: dotnet test
```

For every platform, see [Set up CI](../getting-started/ci-cd-setup.md).

---

## Flaky tests are not flagged in Xping Cloud

Xping Cloud needs at least 10 runs of a test from the last 14 days, and often more, before it can
decide whether the test is flaky. A test needs the same identity in every run, so a renamed test
starts a new history. See
[When Cloud can decide](../flaky-tests/finding-flaky-tests.md#when-cloud-can-decide).

To find flaky tests from the runs on your machine, run `xping report`. See
[Finding flaky tests](../flaky-tests/finding-flaky-tests.md#on-your-machine).

---

## The run is slow, or uses a lot of memory

To find out whether the SDK slows the run, run once with `XPING_ENABLED=false` and compare. A pause
after the last test is usually the uploads at the end of the session. Each upload stops after twice
`UploadTimeout`, but a failing endpoint can hold up the end of the run for several of these waits.

The SDK keeps every test execution of the run in memory until the session ends, so memory grows with
the size of the run. `BatchSize` does not limit it.

See [Performance](../performance/performance.md).

---

## CLI sign-in (`xping login`)

Start with `xping auth status`. It makes no network call and says which credential the CLI would
use. Add `--verbose` to any command for the details. See the
[CLI reference](../cli/command-reference.md#xping-login).

### "Cloud data unavailable: your sign-in is no longer valid"

The session was revoked, on Settings → Security → CLI sessions or when a workspace key was
withdrawn, or it expired. The CLI has already removed it. Run `xping login` again.

### `xping login` says it needs an interactive terminal

`login` runs only in your own shell. It refuses when stdin or stderr is not a terminal, which is the
case for every pipeline agent and coding agent, and when `CI` is `true`, `1` or `yes`. CI uses an API
key that can read Cloud data (`XPING_APIKEY`). An agent should ask you to run `xping login` yourself.

### The browser never returns to the CLI

The sign-in page sends the browser back to `127.0.0.1` on the machine that runs the CLI. Over SSH, in
a container, or in WSL with localhost forwarding off, that address is not the CLI's. Press Ctrl+C and
run `xping login --device`, which works with a browser on any device.

### "Refusing to read ~/.xping/credentials.json because other users can read it"

The CLI uses the sign-in file only when no OS credential store is available. Restore its
permissions:

```bash
chmod 700 ~/.xping
chmod 600 ~/.xping/credentials.json
```

### "This API key cannot read Cloud data"

The key is upload-only, or the workspace's plan does not include API access. Sign in with
`xping login` on your machine, or give CI a key with the Read scope. The report itself is not
affected.

### "Could not write to Windows Credential Manager (error 8…)"

Windows refused to store any credential for your account: `cmdkey /generic:test /user:x /pass:y`
fails with the same message. `xping login` keeps the sign-in in `~/.xping/credentials.json` instead,
so the CLI works. The cause is usually a damaged Windows user profile. Other apps that save passwords
on this account are affected too.

### macOS asks for keychain access after every upgrade

The `xping` tool is not signed by Apple, so macOS asks once for each installed version. Choose
**Always Allow**.

---

## Installation issues

### Wrong xUnit adapter (XPING001, XPING002)

**Symptoms:** the build of your test project stops with one of:

```
error XPING001: Xping.Sdk.XUnit supports xUnit v2, but this project references xUnit v3 (xunit.v3). Replace it with Xping.Sdk.XUnit.V3.
error XPING002: Xping.Sdk.XUnit.V3 supports xUnit v3, but this project references xUnit v2 (xunit). Replace it with Xping.Sdk.XUnit.
```

**Cause:** Xping has one adapter for each xUnit version, and the one installed does not match the
xUnit package that the project references.

**Solution:** swap the package that the error names, and use the matching attribute:

```bash
# XPING001: the project is on xUnit v3
dotnet remove package Xping.Sdk.XUnit
dotnet add package Xping.Sdk.XUnit.V3 --prerelease

# XPING002: the project is on xUnit v2
dotnet remove package Xping.Sdk.XUnit.V3
dotnet add package Xping.Sdk.XUnit --prerelease
```

See [Step 2: Turn on the Xping test framework](../getting-started/quickstart-xunit.md#step-2-turn-on-the-xping-test-framework)
for the attribute each one needs.

**`CS0433: The type 'FactAttribute' exists in both 'xunit.core' and 'xunit.v3.core'`** is the same
mistake in a build that skips the check above, for example one that does not import NuGet's
`buildTransitive` files. The fix is the same.

---

## Get help

If a problem remains:

1. Collect the details that support needs. See
   [Collect information for support](debugging.md#collect-information-for-support).
2. Email support@xping.io, or open an issue at
   [github.com/xping-dev/sdk-dotnet](https://github.com/xping-dev/sdk-dotnet/issues). Remove the API
   key from anything you send.

---

## See also

- [Debugging](debugging.md)
- [Known limitations](known-limitations.md)
- [Configuration reference](../configuration/configuration-reference.md)
- [Set up CI](../getting-started/ci-cd-setup.md)
- [Performance](../performance/performance.md)
