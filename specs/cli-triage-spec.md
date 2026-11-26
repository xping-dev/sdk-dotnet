# CLI — Pull Request Triage (`xping triage`)

**Status:** Draft for review, written 2026-11-22. Binding once merged. Not yet approved: §16 lists the
questions the owner decides before P1 starts.
**Applies to:** `xping-dev/sdk-dotnet`: `src/Xping.Cli/**`, `src/Xping.Sdk.Core/**` (§12),
`tests/Xping.Cli.Tests/**`, `tests/Xping.Sdk.Core.Tests/**`, `docs/**`. One tracked item in
`xping-dev/dashboard` (§8.6). The future skill lives in `xping-dev/skills` and is not written here (§11).
**Schema:** triage envelope `1.0` (new, its own version line, §10.2). The report envelope stays at
`1.33` (`ReportEnvelope.CurrentSchemaVersion`, `src/Xping.Cli/Report/Contract/ReportEnvelope.cs`).
**Depends on:** `cli-repro-spec.md` D3, D4 and Amendments 2, 4D, 7, 8 (the command, the filter escape,
shell quoting). `cli-test-history-spec.md` D2, D3 (name resolution, ambiguity). `cli-report-format-spec.md`
D7, D10 (charset and width discipline). `cloud-data-in-cli-cli-spec.md` §2.2, §3.2, §5 (client, budget,
signature match). `cli-auth-cli-spec.md` §2.2, §7 (credentials, redaction). All five are in
`docs/internals/implementation-specs/`.
**Blocks:** the `xping-triage` skill in `xping-dev/skills`.
**Issue:** to be opened; every phase PR references it.

---

## Amendment protocol

Identical to `cli-report-format-spec.md`. This document is ground truth. An implementer who finds a
conflict between this document and the source, or between this document and a provider API, stops and
reports. They do not adapt in code. Decisions (**D**) and the data contract (§10) change by amendment
only, committed before dependent code. **OQ** sections (§16) are gates: a phase that depends on an
open OQ does not start.

Amendments are added here as `### Amendment N — from <source>` with a table
`# | Was | Now | Changed`, as in `cli-repro-spec.md`.

Words used with a fixed meaning:

- **Strict observation**: one pass or fail of one test at a known commit, on a clean checkout (§7.2).
- **Hint**: evidence that is shown and never decides a next step alone.
- **Head**: the commit the author pushed to the PR/MR. **Tested commit**: the commit CI checked out,
  which is often a merge commit (§1.3).
- **PR** means pull request or merge request.

---

## 0. Summary

`xping triage` answers one question: *CI failed on my pull request. For each failed test, what do I do
next?*

For each failed test it prints what failed, the evidence from every source it could read, and one of
three next steps:

| Next step | Meaning | Decided by |
|---|---|---|
| `RE-RUN` | The test passed and failed on identical code | A strict pass and a strict fail at the same commit |
| `INVESTIGATE` | The failure reproduces. The evidence says whether this change caused it | Repeated strict failures at the head, and strict results at the merge-base |
| `INCONCLUSIVE` | The evidence does not decide | Everything else. The output suggests `--rerun N` |

The command reads five sources: the CI provider API, the CI artifact that holds the job's Xping local
store, the developer's local store, `git`, and Xping Cloud when a credential exists. It works fully
without Xping Cloud.

The command is deterministic. It computes no score and no flaky verdict. A verdict from Xping Cloud is
shown and labelled as Cloud's.

### 0.1 Goals

1. One command from "CI is red" to a next step per failed test, on GitHub, GitLab and Azure DevOps.
2. A versioned JSON contract that a coding agent can act on without reading CI logs.
3. Explainable output: every next step lists the evidence lines that produced it.
4. Works with no account. Xping Cloud adds evidence and never is required.
5. `--rerun N` produces strict observations locally without touching the working tree.

### 0.2 Non-goals

- No local confidence score, no local flaky verdict, no threshold that names a test flaky.
- No quarantine, no skip list, no change to any test.
- No LLM output and no fix suggestion. The agent does the reasoning.
- No write to the PR/MR: no comment, no label, no status check, no automatic re-run of CI.
- No support for CI providers other than the three named (Jenkins, CircleCI and others are detected
  by the SDK but not triaged).
- No mixed hosting in this version: a repository on one provider with CI on another (§16 OQ-9).
- No change to `xping report` behaviour or to its envelope.

---

## 1. Ground truth — P0 inventory

Read on `feat/triage` at `558f9dd`. Every claim names the file and the type or member. Where a line
number is given it was correct at that commit; the member name is the authority.

### 1.1 CLI structure

| Fact | Where |
|---|---|
| Entry point `Program.Main` owns Ctrl+C through `CancelKeyHandler` and calls `Program.Run(args, output, error, input, isTerminal, configureServices, cancelKeys)`. `Run` is the test seam | `src/Xping.Cli/Program.cs` |
| A host is built per invocation (`BuildHost`): configuration sources and logging providers are cleared, `VerboseLoggerProvider` is added, then `AddXpingCliServices`, then the test hook `configureServices` | `Program.BuildHost` |
| Commands are registered by hand in `BuildRootCommand`: `report`, `where`, `clear`, `login`, `logout`, `auth`. Each has a `Build<Name>Command(IServiceProvider …)` method that declares options, validators and `SetAction` | `Program.BuildRootCommand`, `Program.BuildReportCommand` |
| Global recursive options: `--cloud-url`, `--api-key`, `--verbose` (`GlobalOptionSet`), copied into the DI singleton `GlobalOptions` after parsing | `Program.GlobalOptionSet`, `Hosting/GlobalOptions.cs` |
| Parse errors print the scrubbed message and a usage hint to stderr and return the literal `2`. This is global, for every command | `Program.Run` |
| An exception after Ctrl+C prints `Cancelled.` and returns `AuthExitCodes.Cancelled` (130). Any other escaping exception prints `xping: <scrubbed message>` and returns `ExitCodes.Crashed` (70) | `Program.Run` |
| A command stops on Ctrl+C only when it runs inside `RunCooperatively` (`CancelKeyHandler.EnterCooperative`). `login`, `logout` and `auth status` do. `report`, `where`, `clear` do not: Ctrl+C ends the process | `Program.RunCooperatively`, `Hosting/CancelKeyHandler.cs` |
| Output format: `enum ReportFormat { Text, Json, Summary }`; option `--format` with default `Text`; `--json` and `--summary` are conveniences. Validators reject `--json` with `--summary`, and either with a different explicit `--format` | `Commands/ReportOptions.cs`, `Program.BuildReportCommand` |
| JSON output uses `ReportJsonOptions.Default`: camelCase, indented, nulls written, enums as camelCase strings, `JavaScriptEncoder.Default`. It is the documented exception to "serialize through `IXpingSerializer`" | `Report/Rendering/ReportJsonOptions.cs`, `AGENTS.md` |
| Streams come from DI as `ConsoleIO` (`Output`, `Error`, `Input`, `IsTerminal`, `IsInputTerminal`, `IsErrorTerminal`). Warnings and hints go to `io.Error` | `Hosting/ConsoleIO.cs`, `ReportCommand.WriteCloudHints`, `ReportCommand.WriteUnreadableWarning` |
| Glyphs, colour and charset: `OutputCapabilities.Resolve(forceAscii, noColor, redirected, getVariable)`; recorded strings are printed through `TerminalText.Printable` | `Report/Rendering/OutputCapabilities.cs`, `Reporting/TerminalText.cs` |
| Exit codes: `ExitCodes` 0–4 and 70 (`Success`, `FindingsAtThreshold`, `InsufficientData`, `NotFound`, `TestAmbiguous`, `Crashed`); `AuthExitCodes` 10–17 and 130 | `Report/ExitCodes.cs`, `Auth/AuthExitCodes.cs` |
| DI: `AddXpingCliServices` registers `ConsoleIO`, `GlobalOptions`, `ILocalSessionStoreFactory`, the analysis services, the commands (transient) and `AddXpingCliAuth` (HTTP clients, credential stores, `CloudApiClientFactory`, `ProjectResolver`, `CloudEnricher`, `TimeProvider.System`, `IEnvironmentVariableProvider` → `ProcessEnvironment`) | `Hosting/ServiceCollectionExtensions.cs` |
| The shell a printed command is quoted for: `ShellDialects.Resolve(XPING_SHELL, HostOs)`; quoting through `ShellQuote.Of` | `Report/ShellDialect.cs`, `Report/ShellQuote.cs` |
| Secrets: `Redaction.AddSecret` and `Redaction.Scrub` (header values of `Authorization` and `X-API-Key`, OAuth form fields, JSON token members, JWTs). Every parse error, crash message and verbose line passes through `Scrub` | `Auth/Redaction.cs`, `Program.Run`, `Hosting/VerboseLoggerProvider.cs` |
| **The CLI never runs `git`.** The only external process it starts is the browser launcher, through `ILauncherProcess` (`IsOnPath`, `TryRun` returning `bool`, `TryShellOpen`). There is no process abstraction that captures output | `Auth/Browser/BrowserLauncher.cs` |
| The SDK does run `git` in one place: `GitRepositoryReader.GetFirstParentFromGit` (`git rev-parse`, 5 s timeout, executable injectable) | `src/Xping.Sdk.Core/Services/PullRequest/Internals/GitRepositoryReader.cs` |

### 1.2 Local session store

| Fact | Where |
|---|---|
| API: `ILocalSessionStore` has `IsAvailable`, `StorePath`, `SessionsPath`, `Write`, `ReadRecent(maxSessions, assembly)`, `Summarize()`, `ReadEach(assembly)`, `Delete(assembly)`. **There is no `ReadHeaders` member.** The header read exists only as the internal `SessionFileHeader.ReadAssemblies` behind `Summarize` | `src/Xping.Sdk.Core/Services/LocalStore/ILocalSessionStore.cs`, `Internals/SessionFileHeader.cs` |
| The CLI creates stores through `ILocalSessionStoreFactory.Create(options, startDirectory)`, which passes `XPING_LOCAL_STORE` from the injected environment. `LocalSessionStore.Create(options, logger, startDirectory, storePath)` opens a store at an explicit root | `src/Xping.Cli/Services/LocalSessionStoreFactory.cs`, `Services/LocalStore/LocalSessionStore.cs` |
| The CLI reads through `ISessionSource` / `LocalSessionSource` (`Read`, `ReadEach`, `KnownAssemblies`) | `src/Xping.Cli/Report/Store/SessionSource.cs` |
| Format: one gzip JSON file per session, `<root>/sessions/session-{ticks:D19}-{id8}[-{tags}].json.gz`. Tags are FNV-1a hashes of the assembly names | `Internals/SessionFileName.cs`, `Internals/LocalStorePathResolver.cs` |
| Location: `XPING_LOCAL_STORE` or `XpingConfiguration.LocalStorePath`; else `<repository root>/.xping`, where the root is the nearest ancestor of the **test assembly** that holds `.git`, `*.sln` or `*.slnx`; else `<LocalApplicationData>/Xping/stores/<hash>` when the root is not writable | `LocalStorePathResolver.Resolve`, `Services/Diagnostics/RepositoryRoot.cs` |
| The store writes its own `.gitignore` containing `*`. The store root may hold sibling state next to `sessions/` | `LocalStorePathResolver.EnsureCreated`, remarks of `GetSessionsDirectory` |
| Retention after each write: 30 days, then 50 runs **per assembly**, then 100 MB | `Services/LocalStore/LocalStoreOptions.cs` |
| A session is a whole `TestSession`: `SessionId`, `StartedAt`, `EndedAt`, `EnvironmentInfo`, `Executions`, `Assemblies`, `PullRequestContext`, statistics, `SdkVersion` | `src/Xping.Sdk.Core/Models/TestSession.cs` |
| Per test: `TestExecution` has `Identity`, `Outcome` (`Passed`, `Failed`, `Skipped`, `Inconclusive`, `NotExecuted`, `Timeout`), `Duration`, `ExceptionType`, `ErrorMessage`, `StackTrace`, `ErrorMessageHash`, `StackTraceHash`, `Retry` (`AttemptNumber`, `MaxRetries`, `PassedOnRetry`), `Site`, `FailureSiteMember`, `TimeoutBudget` | `Models/Executions/TestExecution.cs`, `RetryMetadata.cs`, `TestOutcome.cs` |
| **Assertion expected and actual values are not recorded.** Only the message text is | `TestExecution` (no such member) |
| Identity: `TestIdentity` has `TestFingerprint`, `FullyQualifiedName`, `Assembly`, `Namespace`, `ClassName`, `MethodName`, `DisplayName`, `ParameterHash`, `SourceFile`, `SourceLineNumber`. The fingerprint is SHA-256 of the fully qualified name and the parameter hash, or the pinned `[XpingFingerprint]` value. It does not depend on machine or path | `Models/Executions/TestIdentity.cs`, `Services/Identity/Internals/TestIdentityGenerator.cs` |
| **Stack traces are kept in the local store.** `UploadStackTraces` (#369) removes the trace only from the uploaded copy: `TestExecution.WithoutStackTrace()`; the doc of `StackTraceOmitted` says "Never set in the local store" | `Models/Executions/TestExecution.cs` |
| The run of a test in a session is its **deciding attempt**: the highest `Retry.AttemptNumber` for the fingerprint in that session | `src/Xping.Cli/Report/Indexes/TestIndex.cs` (`RunsOf`), `Cloud/TargetPlanner.cs` |
| How a session was recorded travels in `EnvironmentInfo.CustomProperties`: `Xping.Mode`, `Xping.ProjectId`, `Xping.Runner` (`VSTest`, `MTP`), `Xping.TestFramework` (`NUnit`, `MSTest`, `xUnit`, `xUnit.v3`), `Xping.ProjectPath` (repository-relative, `/` separators, stamped by `buildTransitive/Xping.Sdk.Core.targets`) | `Services/LocalStore/LocalSessionProperties.cs` |
| Local runs record `Git.SHA`, `Git.Branch`, `IsDetachedHead`, `HasStagedChanges`, read from `.git` files. **This runs only when no CI platform is detected** | `Services/Environment/Internals/EnvironmentDetector.cs` (`CollectLocalGitMetadata`, and its call site guarded by `!ciPlatform.HasValue`) |
| **There is no "working tree clean" flag.** `HasStagedChanges` ignores unstaged edits. `RevisionContext` documents this and refuses to publish a `dirty` field | `src/Xping.Cli/Report/RevisionContext.cs` remarks |
| CI runs record `CIPlatform` and `CI.*` properties (§1.3) and `PullRequestContext` (`Platform`, `ServerUrl`, `RepositoryOwner`, `RepositoryName`, `PullRequestNumber`, `CommitSha` = head, `BaseBranch`, `HeadBranch`) | `EnvironmentDetector`, `Models/PullRequests/PullRequestContext.cs` |

### 1.3 The SDK in CI

**The SDK writes the local store in CI today, on every provider.** `AddXpingLocalStore(mode)` registers
the store for every mode except `Disabled`, and `XpingContextOrchestrator.WriteLocalHistory` runs at
finalization, before the first upload, with no CI condition
(`src/Xping.Sdk.Core/Extensions/XpingServiceCollectionExtensions.cs`, `XpingContextOrchestrator.cs`).

The store path in CI follows §1.2. On a normal checkout that is `<checkout root>/.xping/sessions/`:

| Provider | Checkout root | Store |
|---|---|---|
| GitHub Actions | `$GITHUB_WORKSPACE` | `$GITHUB_WORKSPACE/.xping/sessions/` |
| GitLab CI | `$CI_PROJECT_DIR` | `$CI_PROJECT_DIR/.xping/sessions/` |
| Azure Pipelines | `$(Build.SourcesDirectory)` | `$(Build.SourcesDirectory)/.xping/sessions/` |

`docs/getting-started/ci-cd-setup.md` ("Gating the Build on Findings") already relies on this: it runs
`xping report` in the job after `dotnet test`. No workflow in this repository and no docs page uploads
`.xping` as an artifact.

`CI.*` properties per provider (`EnvironmentDetector`, the `switch` on `CIPlatform`):

| Property | GitHub | GitLab | Azure DevOps |
|---|---|---|---|
| `CI.RunId` | `GITHUB_RUN_ID` | — | — |
| `CI.RunNumber` | `GITHUB_RUN_NUMBER` | — | — |
| `CI.PipelineId` | — | `CI_PIPELINE_ID` | — |
| `CI.JobId` | — | `CI_JOB_ID` | — |
| `CI.BuildId` | — | — | `BUILD_BUILDID` |
| `CI.BuildNumber` | — | — | `BUILD_BUILDNUMBER` |
| `CI.CommitSha` (tested commit) | `GITHUB_SHA` (not on `pull_request_target`) | `CI_COMMIT_SHA` | `BUILD_SOURCEVERSION` |
| `CI.IsPullRequest` | yes | yes | yes |
| `CI.Branch`, `CI.ServerUrl` | yes | yes | yes |
| `CI.Repository` / `CI.ProjectPath` | `GITHUB_REPOSITORY` | `CI_PROJECT_PATH` | `BUILD_REPOSITORY_NAME` |

Three facts follow, and the design depends on each:

1. **No attempt number is recorded.** `GITHUB_RUN_ATTEMPT`, `GITHUB_JOB`, `SYSTEM_STAGEATTEMPT`,
   `SYSTEM_JOBATTEMPT`, `SYSTEM_JOBID`, `CI_JOB_NAME` are not read. A session cannot say which attempt
   wrote it. §12.1 adds them.
2. **`CI.CommitSha` is the tested commit, not the head.** On a GitHub `pull_request` build, an Azure
   PR build and a GitLab merged-results pipeline it is a merge commit. The head is
   `PullRequestContext.CommitSha` (documented on `EnvironmentInfo.CustomProperties` and
   `PullRequestContext.CommitSha`). Two runs on the same head can test different merge commits.
3. **CI sessions carry no `Git.SHA`**, and local sessions carry no `CI.CommitSha`. Triage reads the
   commit of a session through one function (§7.2), not through `RevisionContext.ReadSha`.

### 1.4 Xping Cloud in the CLI

| Fact | Where |
|---|---|
| `ICloudApiClient` has four calls: `ListProjectsAsync`, `GetDigestAsync(projectKey, DigestRequest)`, `GetHealthAsync(projectKey, sessionKeys, cursor)`, `GetRunsAsync(projectKey, fingerprint, limit)` | `src/Xping.Cli/Cloud/ICloudApiClient.cs` |
| Routes: `GET {gw}/v1/projects`, `POST {gw}/v1/projects/{key}/tests/digest`, `GET {gw}/v1/projects/{key}/tests/health`, `GET {gw}/v1/projects/{key}/tests/{fingerprint}/runs?limit=` | `Cloud/CloudApiClient.cs` |
| **No call looks up a session or a run by commit SHA, CI run id, branch or PR number, and no call returns the test results of one session.** The runs call is per fingerprint and its only filter is `limit` | `CloudApiClient.GetRunsAsync` |
| A runs row (`CloudRun`) has `SessionKey`, `StartedAt`, `Outcome`, `Attempts`, `FirstAttemptOutcome`, `DurationMs`, `Tier`, `IsCi`, `MachineName`, `Branch`, `CommitSha`, `PullRequestNumber`, `ExceptionType`, `ErrorMessageHash`. It has no CI run id, no attempt of the CI run, no tested commit, no message text and no stack | `Cloud/Digest/RunsResponse.cs` |
| The digest (`TestDigest`) has `Verdict` (`known-flaky`, `broken`, `unstable`, `passing`, `no-history`), `Confidence` (`State`, `Score`, bounds, `EvidenceLevel` = `sparse`/`adequate`/`robust`/`no-data`), `Flaky` (`State` = `flaky`/`not-flaky`/`inconclusive`/`not-examined`, `Category`, `Severity`, `Since`), `Canonical` (`Runs`, `Failed`, `ConsecutiveFailures`, `FailingSince`, `LatestOutcome`, `LatestAt`), `LastTransition`, `Signatures[]`. It has no "last passed" member | `Cloud/Digest/DigestResponse.cs` |
| Tests are identified to Cloud by fingerprint and up to three SDK `ErrorMessageHash` values. Nothing else is sent | `Cloud/TargetPlanner.cs` |
| `SignatureMatcher.Match(errorMessageHash, digest, algorithmMismatch)` returns `matched`, `unseen`, `unsignable` or `unknown`, comparing the SDK's hash with `digest.Signatures[].ErrorMessageHash` | `Cloud/SignatureMatcher.cs` |
| The Cloud project is resolved **per test assembly**: configured `ProjectId`, then the session pin `Xping.ProjectId`, then a 24 h cache, then a match of the assembly name against the project list | `Cloud/Projects/ProjectResolver.cs`, `ProjectCache.cs` |
| Budget: `CloudBudget.Total` = 4 s and `Credential` = 1 s, both constants; `CloudHttp.AttemptTimeout` = 3 s; one retry, on 5xx only, when 1 s is left; 429 is never retried; 4 concurrent requests | `Cloud/CloudBudget.cs`, `CloudResilience.cs`, `CloudHttp.cs`, `CloudEnricher.MaxConcurrency` |
| Fail-open: every Cloud failure becomes a status (`ok`, `partial`, `unavailable`, `login-required`, `forbidden`, `no-project`, `not-attempted`), a reason, and at most one stderr hint. It never changes the report's exit code. The classification (`CredentialFailure`, `IsReadFailure`, `Classify`) and the hint texts are **private members of `CloudEnricher`** | `Cloud/CloudEnricher.cs`, `Report/Contract/CloudDtos.cs` (`CloudContextDto`) |
| `CloudEnricher` is bound to `ReportEnvelope` (`EnrichAsync(ReportEnvelope, EnrichmentRequest, …)`). It cannot enrich another envelope | `Cloud/CloudEnricher.cs` |
| Credentials: `CredentialResolver.ResolveAsync` returns `--api-key`, then a stored sign-in, then an ambient `XPING_APIKEY`, then none. "Signed in" is a stored `CredentialRecord` valid for the Cloud URL | `Auth/CredentialResolver.cs`, `Auth/Store/CredentialRecord.cs` |
| Cloud receives the stack trace text only when `UploadStackTraces` is set (#369); the hashes are always sent | `Models/Executions/TestExecution.cs` |

### 1.5 Existing code that provider clients can reuse

| Need | Reuse |
|---|---|
| HTTP client without redirects or cookies, no HTTP logging | The registration pattern of `CloudHttp.ClientName` in `Hosting/ServiceCollectionExtensions.cs` (`SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }`, `.RemoveAllLoggers()`) |
| Retry and timeout | `Microsoft.Extensions.Http.Resilience` through a `ResilienceHandler`, as `CloudResilience.Build` and `CloudApiClientFactory.CreateAsync` do |
| Network failure text | `Auth/NetworkFailure.Describe` |
| User agent | `Auth/CliVersion.UserAgent` |
| Secret scrubbing | `Auth/Redaction.cs` |
| Owner-only files, atomic writes | `Auth/PrivateFiles.cs` (`WriteAtomically`) |
| Per-user state directory | `Auth/XpingHome.cs` (`~/.xping`, with `cache/`) |
| Server URL normalisation shared with the SDK | `PullRequestEnvironment.TryNormalizeServerUrl`, `TryGetServerRoot` (`src/Xping.Sdk.Core/Services/PullRequest/Internals/PullRequestEnvironment.cs`) |
| Provider names | `PullRequestPlatform` (`GitHub`, `GitLab`, `AzureDevOps`) |

Nothing exists for: running a process and reading its output, parsing a git remote URL, reading a
provider API, or reading a zip archive of sessions.

### 1.6 Failure description, repro, name resolution

| Fact | Where |
|---|---|
| `FailureSignatureFactory.Create(execution, fingerprint)` builds `FailureSignature(Hash, ExceptionType, NormalisedMessage, Frames, Degraded, Unavailable, TimedOut, ErrorMessageHash)` from the raw message and stack | `src/Xping.Cli/Report/Signatures/` |
| `FailureModes` maps `SignatureView` to `FailureModeDto`. **It holds no category such as timeout, cancellation, network, I/O or assertion.** The only classification is the `TimedOut` flag and the recorded `ExceptionType` | `Report/Contract/FailureModes.cs` |
| `ReproCommand.For(Finding, ReproTarget, ShellDialect)` builds the command. The command text is built by the private `Command(names, runner, projectPath, shell)` and the filter escape by the private `Escape`. The command carries `-e XPING_ENABLED=false` so repro runs are not recorded. No `--no-build` (Amendment 4D of `cli-repro-spec.md`) | `Report/ReproCommand.cs` |
| `ReproTarget.From(session)` reads `Xping.Runner` and `Xping.ProjectPath`. No runner recorded means no command | `Report/Model/ReproTarget.cs` |
| `TestResolver.Resolve(IReadOnlyList<TestSession> sessions, string query, string? assembly)` resolves a name over given sessions in three tiers: exact name, segment suffix, fingerprint prefix (8+ hex). It returns `Found`, `Ambiguous` or `NotFound` | `Report/TestResolver.cs` |
| Frames and source: `StackFrameExtractor.Extract`, `StackFrameLookup.Frames`, `SourceLocationLookup.Relativize` | `src/Xping.Sdk.Core/Services/Diagnostics/` |

### 1.7 Specs, docs, tests

| Fact | Where |
|---|---|
| CLI specs live in `docs/internals/implementation-specs/`. Their format: header (`Status`, `Applies to`, `Schema`, `Depends on`, `Blocks`), amendment protocol, §1 ground truth, §2 decisions `D1…`, target output, data contract, `OQ` gates, implementation sequence, out of scope. `cloud-data-in-cli-cli-spec.md` uses `Q-n` instead of `OQ-n` | the eight files there |
| `specs/` at the repository root holds `docs-overhaul.md` only. This document is placed in `specs/` as instructed (OQ-12) | `specs/` |
| docfx builds `docs/**/*.{md,yml}` and excludes `internals/**`. The site deploys only from `release.yml` (`publish-docs`) | `docs/docfx.json`, `.github/workflows/release.yml` |
| `docs/cli/command-reference.md` documents each command as: heading, one sentence, `bash` synopsis, options table, samples, one `**Exit codes:**` line; then a global `## Exit codes` table and `## Environment variables` | `docs/cli/command-reference.md` |
| `docs/guides/pull-requests/` holds one page, `reading-the-xping-comment.md`, listed under "Pull Requests" in `docs/guides/toc.yml` | `docs/guides/` |
| Documented samples are generated by the renderer and guarded by `<!-- xping:sample … -->` markers | `tests/Xping.Cli.Tests/Report/DocumentedSampleTests.cs` |
| CLI tests: xUnit 2.9.2, no Moq, hand-written fakes. End-to-end tests call `Program.Run` with `StringWriter`s and `OfflineCli.Store(root)` | `tests/Xping.Cli.Tests/OfflineCli.cs`, `Commands/ReportCommandTests.cs` |
| Sessions for tests: `TestSessionFactory.Session(…)`, `Execution(…)`; in-memory `FakeSessionSource` | `tests/Xping.Cli.Tests/Report/TestSessionFactory.cs` |
| HTTP fake: `FakeCloud`, an in-process Kestrel app on `127.0.0.1:0`, with fault injection | `tests/Xping.Cli.Tests/Cloud/FakeCloud.cs` |
| Time: `FakeTimeProvider`, `TimerTrackingTimeProvider`, `ClockDriver` | `tests/Xping.Cli.Tests/Cloud/` |
| **There is no recorded-response pattern.** The only file fixtures are the report goldens, loaded from the source tree (`GoldenReportTests.GoldenPath`), and one embedded session in Core tests | `tests/Xping.Cli.Tests/Report/GoldenReportTests.cs` |
| Budget guards run on the fake clock and on recorded call counts (`CloudBudgetTests`, `CloudEnricherTests.NoMoreThanFourDigestRequestsAreInFlightAtOnce`, `TestResolverTests` read counts). No test measures wall time | `tests/Xping.Cli.Tests/Cloud/`, `Report/TestResolverTests.cs` |
| Child-process tests: `SampleRunner.RunProcessAsync` (strips `XPING_*`, `MSBUILD*`, `VSTEST_*`, `TESTINGPLATFORM_*`; kills the process tree on timeout). Real-`git` fixtures: `GitRepositoryReaderTests` | `tests/Xping.Sdk.Integration.Tests/Infrastructure/SampleRunner.cs`, `tests/Xping.Sdk.Core.Tests/Services/PullRequest/GitRepositoryReaderTests.cs` |
| Traits: `Category=Calibration`, `Category=CredentialStore`. CI skips the integration project on PRs | `.github/workflows/ci.yml` |

### 1.8 Conflicts between the agreed design and the source

Each is resolved by a decision below or left to an OQ. None is adapted silently.

| # | Agreed design | Found | Resolution |
|---|---|---|---|
| X1 | Cloud provides "the CI session for the PR head SHA, which removes the need for the CI artifact" | No Cloud call finds a session by commit, run or PR, and none returns a session's failures (§1.4) | Not possible today. D12 uses what exists; §8.6 proposes the contract; the CLI part is phase P6b |
| X2 | "Failed and also passed on the same SHA" may come from **local store history** | A local session has no clean-tree flag (§1.2). Two local runs at one `Git.SHA` can have run different code | Local store history is a **hint**, never decisive (D6). Sessions written by `--rerun` are strict |
| X3 | "Same SHA" | CI tests a merge commit that differs from the head, and it changes when the base moves (§1.3) | D5 defines identical code by the **tested commit**. Same head with different tested commits is a hint |
| X4 | Artifact naming "including the attempt number" | The SDK records no attempt (§1.3) | §12.1 records it. The session property decides; the artifact name is the fallback (D10) |
| X5 | "Reuse the existing failure-mode classification in `FailureModes.cs`" for timeout, cancellation, socket/IO versus assertion | That file has no such classification (§1.6) | Triage reuses `FailureSignature` and adds one small table, `FailureKind` (D8) |
| X6 | "Assertion expected/actual when available" | Not recorded (§1.2) | Parsed from the message for known formats, else null (D9, OQ-7) |
| X7 | "`ReadEach` / `ReadHeaders`" | `ReadHeaders` does not exist | `ReadEach` and `Summarize` are used |
| X8 | Reuse `repro` | `ReproCommand.For` takes a `Finding` and adds `-e XPING_ENABLED=false`; the builder is private | §13 changes `ReproCommand` to expose the builder |
| X9 | Follow the `report` output convention | `ReportFormat` is named for `report` and lives in `Commands/ReportOptions.cs` | §13 renames it `OutputFormat` |
| X10 | Reuse `CancelKeyHandler` | `report` is not cooperative; only the auth commands are | `triage` runs cooperatively (D15) |
| X11 | Exit codes distinct from existing ones | The parser already returns `2` for any usage error, on every command | Triage keeps `2` for usage and uses 20–25 for its own outcomes (D14) |
| X12 | "Integration tests with recorded responses" | No such pattern exists (§1.7) | §14.3 defines one. Recordings for GitLab and Azure DevOps need sandbox projects (OQ-10) |
| X13 | Upload the store with an "always" condition | `.xping` is a hidden directory. `actions/upload-artifact` v4 skips hidden files unless `include-hidden-files: true` | The GitHub snippet sets it (§12.2). Verified in P0 of phase P2 (OQ-1) |

---

## 2. Decisions

Decisions are binding. Each names the requirement it implements.

### D1 — Command surface

```bash
xping triage [<pr>] [options]
```

| Argument / option | Default | Meaning |
|---|---|---|
| `<pr>` | the PR of the current branch | A number (`123`, `!123`, `#123`) or a PR/MR URL. A URL also sets the provider and repository |
| `--rerun <N>` | not set | Run each failed test `N` times at the head and, when needed, at the merge-base, in temporary worktrees (§9). `1 ≤ N ≤ 20` |
| `--rerun-timeout <minutes>` | `10` | Limit for one build or one test invocation of `--rerun` |
| `--test <name>` | not set | Triage one failed test, by name or fingerprint (`TestResolver` tiers) |
| `--provider <name>` | detected | `github`, `gitlab` or `azure-devops` |
| `--repo <value>` | detected | `owner/name` (GitHub), `group/sub/project` (GitLab), `org/project/repo` (Azure DevOps), or a repository URL |
| `--remote <name>` | upstream remote of the branch, else `origin` | The git remote to detect the provider and repository from |
| `--no-cloud` | not set | Do not read Xping Cloud |
| `--project <key>` | not set | Xping Cloud project key, as in `report` (same parser, `Program.ParseProjectKey`) |
| `--top <N>` / `--all` | `--top 25` | Failed tests listed in full (§10.5) |
| `--format <f>`, `--json`, `--summary` | `text` | As `report`: same names, same validators |
| `--directory <path>` | current directory | Where the repository and the local store are resolved from |
| `--ascii`, `--no-color` | not set | As `report` |

Global options `--cloud-url`, `--api-key`, `--verbose` apply.

Validators (parse errors, exit 2): `--json` with `--summary`; either with a different explicit
`--format`; `--all` with `--top`; `--rerun` outside 1–20; `--rerun-timeout` without `--rerun`;
`--repo` that does not parse for the provider; `<pr>` that is neither a number nor a URL.

The command never prompts. It reads nothing from stdin.

### D2 — Sources are independent and stated

Each source is read by its own component (§3). A source that fails does not stop the others. The
envelope lists every source with a status (§10.3 `sources`). A next step is computed from the
observations that were read, whatever their source.

### D3 — Two roles in one provider abstraction

A provider implements two interfaces: `IChangeRequestHost` (PR resolution) and `IPipelineHost` (runs,
attempts, artifacts). In this version one provider supplies both. The split exists so that mixed
hosting (OQ-9) is an addition and not a redesign. All three providers are implemented against the
abstraction from the first provider phase; the abstraction is designed from the three API shapes in
§5–§6, not from GitHub alone.

### D4 — The subject set

The tests triaged are the **current failures**: tests whose deciding attempt (§1.2) is `Failed` or
`Timeout` in a session of the **latest completed attempt** of a failed job, in any pipeline run for
the PR head.

- A test that failed in an earlier attempt and passed in the latest is not a subject. It is counted in
  `summary.passedOnLaterAttempt` and named in `resolvedTests` (§10.3).
- A test with two failing sessions in the subject set (two jobs, a matrix) is one subject. Its
  `failure` block comes from the first session in the order: job name ordinal, then session start,
  then session id. The other sessions are observations.
- `Skipped`, `Inconclusive` and `NotExecuted` are never subjects.

### D5 — Identical code

Every observation has a **code id**: the commit whose tree the test ran against.

| Observation source | Code id | Strict |
|---|---|---|
| CI artifact session | `CI.CommitSha` (tested commit). When absent (GitHub `pull_request_target`): `PullRequestContext.CommitSha`, marked `headOnly` | yes |
| `--rerun` session | the worktree commit | yes |
| Xping Cloud run row, CI, no PR number | `CommitSha` | yes |
| Xping Cloud run row, CI, with PR number | `CommitSha` (the head), marked `headOnly`, until Cloud returns the tested commit (§8.6 C3) | yes, `headOnly` |
| Xping Cloud run row, not CI | `CommitSha` | no |
| Developer's local store session | `Git.SHA` | no (X2) |

Two observations are **on identical code** when both are strict, their code ids are equal, and neither
is `headOnly` — or both are `headOnly` and come from attempts of the same CI run (same `CI.RunId`,
`CI.PipelineId` or `CI.BuildId`), because attempts of one run always check out the same ref.

Two strict observations with the same head and different or unknown tested commits are
**same head, different merge result**. That is a hint (`ci.sameHeadDifferentMerge`).

The **head side** of a PR is the set of code ids `{head} ∪ {tested commits of runs for that head}`.
The **merge-base** is one commit (§6.1).

### D6 — Classification is a decision table

§7.4 is the whole rule set. It is evaluated top to bottom; the first row that matches decides. Only
strict observations decide. Hints never decide, alone or together. No rate, score or weighting is
computed. The two constants in the table are named in `TriageConstants` and are evidence-sufficiency
rules, not verdict thresholds.

### D7 — Xping Cloud never decides alone

A Cloud flaky classification (`flaky.state = flaky`, or `verdict = known-flaky`) is shown as Cloud
evidence with its severity, confidence and evidence level. **It cannot make a test `RE-RUN`.**

Reason: the classification describes the test's history. It says nothing about this failure. A test
that Cloud calls flaky can also be broken by this change, and a `RE-RUN` on the flag alone would hide
exactly that regression behind a green re-run. `RE-RUN` therefore always rests on a pass and a fail on
identical code, which Cloud run rows can supply when they are strict (D5).

What the flag changes: the `INCONCLUSIVE` sentence names it and puts the CI re-run first
(§7.5). OQ-4 offers the alternative.

### D8 — Failure kind is a hint from one table

`FailureKind` is `timeout`, `cancellation`, `network`, `io`, `assertion`, `fixture` or `other`,
resolved in this order:

1. `Outcome == Timeout` or `FailureSignature.TimedOut` → `timeout`.
2. `Site.IsLifecycle()` (`FailureSiteExtensions`) → `fixture`.
3. `ExceptionType` (simple name, ordinal, after the last `.`) in the table:

| Kind | Exception types |
|---|---|
| `cancellation` | `TaskCanceledException`, `OperationCanceledException` |
| `timeout` | `TimeoutException`, `TimeoutRejectedException` |
| `network` | `HttpRequestException`, `SocketException`, `WebException`, `HttpIOException`, `WebSocketException` |
| `io` | `IOException` and names ending in `IOException`, `UnauthorizedAccessException` |
| `assertion` | names ending in `AssertionException`, `AssertFailedException`, `XunitException`, or in namespaces `Xunit.Sdk`, `NUnit.Framework`, `Shouldly`, `FluentAssertions` |

4. Otherwise `other`.

The table lives in `Triage/Evidence/FailureKinds.cs`. It is a label for the reader. It appears as the
evidence line `failure.kind` and never in §7.4.

### D9 — What a failure block contains

Identity, outcome, message, stack, location, duration and retries, as §10.3 defines. Three derived
members:

- **Location.** `failureFrame`: the first stack line of the form `in <path>:line <n>` whose path maps
  to a file in the head tree. The path recorded in CI is absolute on the CI machine; it is mapped by
  the longest path-segment suffix that matches exactly one path of
  `git ls-tree -r --name-only <head>`. No unique match means no `failureFrame`. `testDefinition`:
  `Identity.SourceFile` and `SourceLineNumber`, mapped the same way.
- **Assertion.** `expected` and `actual` are parsed from `ErrorMessage` by `AssertionParser` for the
  formats in §7.6. An unknown format gives `assertion: null`. The parser never guesses.
- **Stack.** The raw recorded text, bounded (§10.5).

### D10 — The CI artifact is the failure record, and there is no TRX fallback

The exact record of a CI failure is the job's Xping local store, uploaded as an artifact (§12.2).
When no artifact with sessions exists for a failed job, triage reports that job in `run.jobs` with
`store: "missing"` (or `"expired"`), and when no failed job has a store it exits `23` with the setup
instructions for the provider.

TRX files and provider test reports are **not** a fallback:

- They do not carry the Xping fingerprint, so results cannot be merged with the local store, with
  Cloud, or across parameterised rows (MSTest and xUnit rows share a name).
- They do not carry `Xping.Runner`, `Xping.ProjectPath` or `Xping.TestFramework`, so there is no
  repro command, no verify command and no `--rerun`.
- Availability differs per provider: Azure DevOps has a test results API, GitLab has JUnit reports
  only when the job declares them, GitHub has none. Three different degraded modes would be three
  products.
- A fallback that produces `INCONCLUSIVE` for every test adds nothing over the provider's own page.

The setup is one step per provider and is printed with the error. OQ-3 keeps the alternative open.

Attempt attribution of a session: `CI.RunAttempt` / `CI.JobAttempt` (§12.1) when present; else the
artifact name (§6.5); else unknown, and the session is used as an observation of its code id without an
attempt number.

### D11 — Merge by assembly and fingerprint

The identity of a test is `(Assembly, TestFingerprint)`, as `TestResolver` keys it. Observations from
all sources are merged on it. §7.3 gives precedence and conflict rules.

### D12 — Cloud mode

With a credential (`CredentialResolver`) and without `--no-cloud`, triage reads, for the subject
tests only:

1. The **digest** of each subject fingerprint with its `ErrorMessageHash`: verdict, flaky state,
   confidence, evidence level, canonical-branch state, signatures.
2. The **runs** of each subject fingerprint (`limit` 50), filtered on the client into: rows at a
   head-side commit, rows at the merge-base, and the newest passing row.

It attributes all of it to Cloud (§8.4) and fails open (§8.3). It uses the existing client, factory,
credential resolution, project resolution and signature match. §8.6 lists what Cloud must add before
it can replace the artifact.

### D13 — `--rerun` produces strict observations through the SDK itself

Rerun runs are ordinary test runs recorded by the Xping SDK into a store that triage owns
(`.xping/triage/reruns/<sha>/`). Triage reads them with the same reader as any store. Nothing is
written to the developer's `sessions/` directory, so `xping report` is unaffected. §9 is the design.

### D14 — Exit codes

| Code | Name | Meaning |
|---|---|---|
| `0` | `NoFailedTests` | The latest attempts for the PR head have no failed test |
| `20` | `AllRerun` | Failed tests found; every one is `RE-RUN` |
| `21` | `ActionNeeded` | At least one failed test is `INVESTIGATE` or `INCONCLUSIVE` |
| `22` | `NotReady` | A pipeline for the head is still running and no completed failed job has a store yet. Run again later |
| `23` | `NothingToTriage` | Triage could not find its input: not a git repository, provider not detected, PR/MR not found, no pipeline run for the head, or failed jobs without any store |
| `24` | `ProviderAuth` | No provider token, the token was refused, or it lacks a scope |
| `25` | `ProviderFailure` | The provider API, the network, `git`, or a rate limit failed |
| `2` | — | Usage error (parser), as every command |
| `3` | `ExitCodes.NotFound` | `--test` matches no failed test |
| `4` | `ExitCodes.TestAmbiguous` | `--test` matches more than one failed test |
| `70` | `ExitCodes.Crashed` | Unhandled exception |
| `130` | `AuthExitCodes.Cancelled` | Ctrl+C |

Why these numbers:

- 20–29 is a new decade, clear of `report` (0–4), the sign-in commands (10–17), `70` and `130`. It
  leaves 18–19 to the sign-in commands.
- `0` means "nothing for you to do", the only outcome a script treats as success.
- `20` and `21` separate "re-run CI and continue" from "someone must look". That is the branch a
  pipeline or an agent takes first.
- `22` is separate from `23` because the action differs: wait and call again, versus fix the setup.
- `24` and `25` separate "give me a token" from "try again later".
- `3` and `4` reuse the `report --test` meanings exactly, so one rule covers both commands.
- Xping Cloud never changes the exit code, as in `report`.
- `--rerun` failures (a build that fails, a timeout) never change the exit code class: they are
  evidence lines and warnings, and the test stays `INCONCLUSIVE` (21).

The constants live in `Triage/TriageExitCodes.cs`.

### D15 — Process rules

- `triage` runs inside `RunCooperatively`. Ctrl+C cancels the token; the command stops child
  processes, removes its worktrees and temporary files, and exits 130.
- stdout holds only the result in the chosen format. Progress, warnings, hints and errors go to
  stderr. In JSON mode stdout is one JSON document and nothing else, with no ANSI sequence.
- Progress lines (`Reading pipeline runs…`) are written to stderr only when stderr is a terminal
  (`ConsoleIO.IsErrorTerminal`) or `--verbose` is set.
- Provider responses are parsed with `System.Text.Json.JsonDocument`, reading only the members
  named in §6. They are not bound through `IXpingSerializer`: the three providers use three naming
  styles and none is the SDK wire format. `AGENTS.md` gains this as a second documented exception, in
  the phase that adds the first provider.

---

## 3. Architecture

### 3.1 Components and data flow

```
Program.BuildTriageCommand
  └─ TriageCommand.RunAsync(TriageOptions, ct)
       1. RepositoryLocator      git: repo root, remote URL, branch, HEAD      → RepositoryContext
       2. RemoteUrlParser        remote URL (or --repo/--provider/<pr> URL)    → RepositoryRef
       3. ProviderTokenResolver  provider CLI, then environment                → ProviderToken
       4. ICiProviderFactory     RepositoryRef + token                         → ICiProvider
       5. ChangeRequestResolver  <pr> or current branch                        → ChangeRequest (head, base, merge-base)
       6. PipelineReader         runs + attempts + jobs for the head           → PipelineSnapshot
       7. ArtifactStoreReader    list, download (cached), extract, read        → CI sessions per attempt
       8. SubjectSelector        D4                                            → subjects, resolved tests
       9. LocalHistoryReader     developer store, ReadEach                     → local observations
      10. DiffReader             git diff merge-base..head, csproj references  → ChangedFile[] per subject
      11. TriageCloudReader      digest + runs (P6)                            → Cloud evidence, Cloud observations
      12. RerunCoordinator       worktrees, build, run, read (P7, --rerun)     → rerun observations
      13. EvidenceMerger         §7.3                                          → EvidenceSet per subject
      14. Classifier             §7.4                                          → Decision per subject
      15. TriageEnvelopeBuilder  §10                                           → TriageEnvelope
      16. ITriageRenderer        text | json | summary                         → stdout
      17. TriageExitCodes.For    D14                                           → exit code
```

Steps 9–12 are independent of each other and each fails open. Steps 1–8 are required: a failure there
ends in an error envelope (§10.6) and exit 22–25.

`Classifier` and `EvidenceMerger` are pure: no I/O, no clock, no environment. They take records and
return records. That is what makes §7.4 testable as a table.

### 3.2 File structure

```
src/Xping.Cli/
  Commands/
    TriageCommand.cs                 orchestration (steps 1–17), stderr messages
    TriageOptions.cs
    OutputFormat.cs                  renamed from ReportFormat (§13)
  Triage/
    TriageExitCodes.cs
    TriageConstants.cs               every limit and bound in this document, named
    TriageException.cs               TriageFailure(code, exitCode, message, nextStep)
    Git/
      IGitClient.cs, GitClient.cs    typed git operations over IProcessRunner
      RepositoryLocator.cs           RepositoryContext
      RemoteUrlParser.cs             RepositoryRef
    Processes/
      IProcessRunner.cs, ProcessRunner.cs
    Providers/
      CiProviderKind.cs
      ProviderModels.cs              ChangeRequest, PipelineRun, PipelineAttempt, PipelineJob, CiArtifact, RerunInstruction
      IChangeRequestHost.cs, IPipelineHost.cs, ICiProvider.cs, ICiProviderFactory.cs
      CiProviderFactory.cs
      ProviderHttp.cs                client name, timeouts, resilience, paging helpers, redirect rule
      ProviderException.cs           ProviderFailureKind
      ProviderToken.cs, ProviderTokenResolver.cs
      GitHub/GitHubProvider.cs
      GitLab/GitLabProvider.cs
      AzureDevOps/AzureDevOpsProvider.cs
    Artifacts/
      ArtifactNaming.cs              the convention of §6.5, both directions
      ArtifactCache.cs               ~/.xping/cache/triage
      ArtifactStoreReader.cs         zip → sessions
    Evidence/
      Observation.cs, CodeId.cs, EvidenceLine.cs, EvidenceCodes.cs
      SessionCommit.cs               one place that reads a session's commit (D5)
      SubjectSelector.cs
      LocalHistoryReader.cs
      DiffReader.cs, ChangedFile.cs, ProjectReferenceReader.cs
      FailureKinds.cs, AssertionParser.cs, StackLocation.cs
      EvidenceMerger.cs
    Classification/
      Classifier.cs, Decision.cs, NextStep.cs, ObservationKind.cs
    Cloud/
      TriageCloudReader.cs           (P6)
    Rerun/                           (P7)
      RerunCoordinator.cs, RerunPlan.cs, WorktreeManager.cs, RerunStore.cs, TestInvocation.cs
    Contract/
      TriageEnvelope.cs              every DTO of §10
      TriageEnvelopeBuilder.cs
    Rendering/
      ITriageRenderer.cs, TriageTextRenderer.cs, TriageSummaryRenderer.cs, TriageJsonRenderer.cs
```

Tests mirror this under `tests/Xping.Cli.Tests/Triage/` and `tests/Xping.Cli.Tests/Commands/`.

### 3.3 Signatures

All types are `internal sealed` unless they are interfaces. Records are positional. Names and member
names are binding; private helpers are the implementer's.

```csharp
// Triage/Processes
internal sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory,
    IReadOnlyDictionary<string, string?>? Environment,   // null value removes an inherited variable
    TimeSpan Timeout,
    int MaxOutputBytes = 1024 * 1024);

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut, bool Truncated);

internal interface IProcessRunner
{
    /// Returns null when the executable is not on PATH. Never throws for a non-zero exit.
    Task<ProcessResult?> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}

// Triage/Git
internal sealed record RepositoryContext(
    string Root, string? Branch, string HeadSha, string RemoteName, string RemoteUrl, bool IsDirty);

internal sealed record ChangedPath(char Status, string Path, string? OldPath);   // A M D R C T

internal interface IGitClient
{
    Task<RepositoryContext> LocateAsync(string directory, string? remote, CancellationToken ct);
    Task<bool> HasCommitAsync(string root, string sha, CancellationToken ct);
    Task<bool> FetchCommitAsync(string root, string remote, string sha, IReadOnlyList<string> fallbackRefs, CancellationToken ct);
    Task<string?> MergeBaseAsync(string root, string a, string b, CancellationToken ct);
    Task<IReadOnlyList<ChangedPath>> DiffAsync(string root, string from, string to, CancellationToken ct);
    Task<IReadOnlyList<string>> ListTreeAsync(string root, string sha, CancellationToken ct);
    Task<string?> ShowFileAsync(string root, string sha, string path, CancellationToken ct);
    Task AddWorktreeAsync(string root, string path, string sha, CancellationToken ct);
    Task RemoveWorktreeAsync(string root, string path, CancellationToken ct);
    Task PruneWorktreesAsync(string root, CancellationToken ct);
}

// Triage/Git/RemoteUrlParser
internal sealed record RepositoryRef(
    CiProviderKind Provider,
    string ServerUrl,        // normalised as PullRequestContext.ServerUrl
    string ApiBaseUrl,       // §6
    string Owner,            // GitHub owner; GitLab namespace path; Azure DevOps organization
    string? Project,         // Azure DevOps project; null elsewhere
    string Name);            // repository name

internal static class RemoteUrlParser
{
    public static bool TryParse(string remoteUrl, CiProviderKind? providerOverride, out RepositoryRef? repository, out string? error);
    public static bool TryParseChangeRequestUrl(string url, out RepositoryRef? repository, out int number);
    public static bool TryParseRepoOption(string value, CiProviderKind provider, string? serverUrl, out RepositoryRef? repository);
}

// Triage/Providers
internal enum CiProviderKind { GitHub, GitLab, AzureDevOps }

internal sealed record ChangeRequest(
    int Number, string Url, string State,            // open | closed | merged
    string HeadSha, string HeadRef, string? HeadRepository,
    string BaseRef, string BaseSha,                  // BaseSha: tip of the target branch as the provider reports it
    string? MergeBaseSha,                            // from the provider when it reports one
    bool IsFork, IReadOnlyList<string> FetchRefs);   // refs that hold the head on the base remote

internal enum PipelineStatus { Queued, Running, Completed }
internal enum PipelineResult { Succeeded, Failed, Cancelled, Skipped, Other }

internal sealed record PipelineJob(
    string Id, string Name, string? Stage, int Attempt,
    PipelineStatus Status, PipelineResult? Result, string Url);

internal sealed record PipelineAttempt(
    int Number, PipelineStatus Status, PipelineResult? Result,
    DateTimeOffset? StartedAt, string Url, IReadOnlyList<PipelineJob> Jobs);

internal sealed record PipelineRun(
    string Id, string Name, string Url, string HeadSha, string? TestedSha,
    string? Event, IReadOnlyList<PipelineAttempt> Attempts);

internal sealed record CiArtifact(
    string Id, string Name, long SizeBytes, bool Expired, DateTimeOffset? ExpiresAt,
    string RunId, int? RunAttempt, string? JobId, int? JobAttempt);

internal sealed record RerunInstruction(string? Command, string Url, string Description);

internal interface IChangeRequestHost
{
    Task<ChangeRequest> GetAsync(int number, CancellationToken ct);
    /// Null when the branch has no open change request. More than one is a ProviderException(Ambiguous).
    Task<ChangeRequest?> FindForBranchAsync(string branch, string headSha, CancellationToken ct);
}

internal interface IPipelineHost
{
    /// Every run for the head, each with every attempt and every job of every attempt.
    Task<IReadOnlyList<PipelineRun>> ListRunsAsync(ChangeRequest changeRequest, CancellationToken ct);
    Task<IReadOnlyList<CiArtifact>> ListArtifactsAsync(PipelineRun run, CancellationToken ct);
    Task DownloadAsync(CiArtifact artifact, Stream destination, CancellationToken ct);
    RerunInstruction RerunInstructionFor(PipelineRun run, IReadOnlyList<PipelineJob> failedJobs, ShellDialect shell);
}

internal interface ICiProvider : IChangeRequestHost, IPipelineHost, IDisposable
{
    CiProviderKind Kind { get; }
    RepositoryRef Repository { get; }
}

internal interface ICiProviderFactory
{
    ICiProvider Create(RepositoryRef repository, ProviderToken token);
}

internal enum ProviderFailureKind
{
    Unauthorized, Forbidden, MissingScope, NotFound, Ambiguous, RateLimited, Server, Network, Timeout, InvalidResponse
}

internal sealed class ProviderException(ProviderFailureKind kind, string message, string? nextStep = null) : Exception(message);

internal enum ProviderTokenScheme { Bearer, Basic, GitLabJobToken }
internal sealed record ProviderToken(string Value, ProviderTokenScheme Scheme, string Source);   // Source: "gh", "GITHUB_TOKEN", …

internal sealed class ProviderTokenResolver(IProcessRunner processes, IEnvironmentVariableProvider environment)
{
    public Task<ProviderToken?> ResolveAsync(RepositoryRef repository, CancellationToken ct);
}

// Triage/Evidence
internal enum ObservationSource { CiArtifact, Rerun, Cloud, LocalStore }
internal enum ObservedOutcome { Passed, Failed }           // Timeout counts as Failed; others are not observations

internal sealed record CodeId(string Sha, bool HeadOnly);

internal sealed record Observation(
    string Assembly, string Fingerprint,
    ObservationSource Source, bool Strict, CodeId Code, ObservedOutcome Outcome,
    string? RunId, int? RunAttempt, string? JobName, Guid? SessionId,
    DateTimeOffset At, string? SignatureHash, string? ErrorMessageHash,
    IReadOnlyList<ObservedOutcome> InRunAttempts);        // outcomes of every attempt in the session, in order

internal sealed record EvidenceLine(
    string Code,              // EvidenceCodes
    string Source,            // ci | rerun | cloud | local | git
    bool Decisive,
    string Text,              // one sentence, built by the envelope builder
    IReadOnlyDictionary<string, object?> Data);

// Triage/Classification
internal enum NextStep { Rerun, Investigate, Inconclusive }
internal enum ObservationKind { FlippedOnIdenticalCode, FailsOnlyWithChange, NewTestFails, FailsAtMergeBase, Undetermined }

internal sealed record Decision(
    NextStep NextStep, ObservationKind Observation, bool? CausedByChange,
    string Rule,              // R1 … R6 of §7.4
    IReadOnlyList<string> DecisiveEvidence);   // EvidenceLine codes

internal static class Classifier
{
    public static Decision Classify(ClassificationInput input);
}

internal sealed record ClassificationInput(
    string HeadSha, IReadOnlySet<string> HeadSideShas, string? MergeBaseSha,
    IReadOnlyList<Observation> Observations,     // of one test
    RerunFacts? Rerun);                          // null without --rerun

internal sealed record RerunFacts(
    int Requested, int HeadRuns, int HeadFailed, int HeadPassed,
    int? BaseRuns, int? BaseFailed, int? BasePassed,
    bool AbsentAtBase, bool HeadBuildFailed, bool BaseBuildFailed);
```

### 3.4 DI registration

In `Hosting/ServiceCollectionExtensions.cs`, a new private `AddXpingTriage`, called from
`AddXpingCliServices`:

```csharp
services.AddTransient<TriageCommand>();
services.AddSingleton<IProcessRunner, ProcessRunner>();
services.AddSingleton<IGitClient, GitClient>();
services.AddSingleton<ProviderTokenResolver>();
services.AddSingleton<ICiProviderFactory, CiProviderFactory>();
services.AddSingleton<ArtifactCache>();
services.AddSingleton<ArtifactStoreReader>();
services.AddSingleton<TriageCloudReader>();      // P6
services.AddSingleton<RerunCoordinator>();       // P7

services
    .AddHttpClient(ProviderHttp.ClientName)       // "xping-ci"
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
    .RemoveAllLoggers();
```

`Classifier`, `EvidenceMerger`, `SubjectSelector`, `RemoteUrlParser`, `ArtifactNaming`,
`TriageEnvelopeBuilder` and the renderers are static or constructed directly. They hold no state.

Tests replace `IProcessRunner`, `IGitClient`, `ICiProviderFactory`, `IEnvironmentVariableProvider` and
`TimeProvider` through `configureServices`, as the auth tests replace the browser.

---

## 4. Repository and provider detection

### 4.1 Locating the repository

`GitClient.LocateAsync(directory, remote)`:

1. `git -C <directory> rev-parse --show-toplevel` → root. Failure → `TriageFailure(not-a-repository, 23)`.
   `git` not on PATH → `TriageFailure(git-missing, 25)`.
2. `git rev-parse HEAD` → head of the working tree; `git symbolic-ref --quiet --short HEAD` → branch,
   or null when detached.
3. Remote name: `--remote`; else `git config branch.<branch>.remote`; else `origin`; else the only
   remote; else failure `no-remote` (23) naming `--remote` and `--repo`.
4. `git remote get-url <name>` → URL.
5. `git status --porcelain --untracked-files=no` → `IsDirty` (used for warnings only).

Every `git` call sets `GIT_TERMINAL_PROMPT=0`, `GIT_OPTIONAL_LOCKS=0`, `LC_ALL=C`, uses
`ArgumentList`, and has a 10 s timeout (`TriageConstants.GitTimeout`), except fetch and worktree
operations (120 s).

### 4.2 Remote URL forms

`RemoteUrlParser.TryParse` accepts these forms. `.git` suffixes and trailing slashes are dropped.
Hosts compare case-insensitively. User info (`user@`, `user:token@`) is dropped and a token found
there is passed to `Redaction.AddSecret` and never used.

| Provider | Form | Result |
|---|---|---|
| GitHub | `https://github.com/{owner}/{repo}` | server `https://github.com`, API `https://api.github.com` |
| GitHub | `git@github.com:{owner}/{repo}`, `ssh://git@github.com/{owner}/{repo}` | same |
| GitHub Enterprise | `https://{host}/{owner}/{repo}`, `git@{host}:{owner}/{repo}` with `--provider github` or a host listed in `GH_HOST` | API `https://{host}/api/v3` |
| GitLab | `https://gitlab.com/{group}/{sub…}/{project}`, `git@gitlab.com:{group}/{sub…}/{project}` | API `https://gitlab.com/api/v4`; `Owner` = full namespace path, `Name` = project |
| GitLab self-hosted | `https://{host}[:port][/prefix]/{group}/…/{project}`, `git@{host}:{group}/…/{project}`, `ssh://git@{host}:{port}/{group}/…/{project}` | API `{server}/api/v4` |
| Azure DevOps | `https://dev.azure.com/{org}/{project}/_git/{repo}`, `https://{user}@dev.azure.com/{org}/{project}/_git/{repo}` | API `https://dev.azure.com/{org}` |
| Azure DevOps | `git@ssh.dev.azure.com:v3/{org}/{project}/{repo}` | same |
| Azure DevOps (old) | `https://{org}.visualstudio.com/[DefaultCollection/]{project}/_git/{repo}` | API `https://dev.azure.com/{org}` |
| Azure DevOps (old) | `{org}@vs-ssh.visualstudio.com:v3/{org}/{project}/{repo}` | same |
| Azure DevOps Server | `https://{host}/{collection}/{project}/_git/{repo}` with `--provider azure-devops` | API `https://{host}/{collection}` |

Detection order when `--provider` is absent:

1. Host `github.com` → GitHub. Host `gitlab.com` → GitLab. Host `dev.azure.com`, `ssh.dev.azure.com`,
   `*.visualstudio.com`, `vs-ssh.visualstudio.com` → Azure DevOps.
2. Path contains `/_git/` → Azure DevOps.
3. Host equals the host of `GITLAB_HOST`, `CI_SERVER_HOST` or a host in the `glab` configuration
   lookup of §5.2 → GitLab. Host equals `GH_HOST` → GitHub.
4. Host starts with `gitlab.` → GitLab. Host starts with `github.` → GitHub.
5. Otherwise failure `provider-not-detected` (23): the message names the host and says to pass
   `--provider`.

`--repo` replaces the path; with a URL it replaces host and path. `<pr>` given as a URL replaces
provider, host, repository and number, and wins over `--repo` and the remote.

URL forms accepted for `<pr>`:
`https://{host}/{owner}/{repo}/pull/{n}`, `https://{host}/{namespace…}/-/merge_requests/{n}`,
`https://dev.azure.com/{org}/{project}/_git/{repo}/pullrequest/{n}` and the `visualstudio.com` form.

The parsed `ServerUrl` is normalised with `PullRequestEnvironment.TryNormalizeServerUrl`, so it
compares equal to `PullRequestContext.ServerUrl` of a session recorded on that server.

### 4.3 Session identity check

A CI session read from an artifact is used only when its `PullRequestContext` agrees with the resolved
PR: same `Platform`, `ServerUrl`, repository and `CommitSha == ChangeRequest.HeadSha`; or, when the
session has no PR context, `CI.CommitSha` is in the head side. A session that disagrees is skipped
with warning `W-ARTIFACT-MISMATCH`. This keeps a mislabelled or reused artifact from supplying
evidence about other code.

---

## 5. Provider authentication

### 5.1 Rules for all providers

- Resolution order: **provider CLI**, then **environment variables** in the order listed. The first
  value found is used. There is no flag that takes a token: a token on a command line reaches shell
  history and process lists.
- A provider CLI is asked with a 5 s timeout through `IProcessRunner`. Not installed, not signed in,
  or a non-zero exit means "no token from this source", with a `--verbose` line and no error.
- Every token is registered with `Redaction.AddSecret` before any request. No token is written to
  disk, to the artifact cache, to the envelope or to a log. `sources.ci.tokenSource` holds the
  **name** of the source (`gh`, `GITHUB_TOKEN`).
- `Authorization`, `PRIVATE-TOKEN` and `JOB-TOKEN` are sent only to the API base URL of the
  repository. A redirect is never followed with credentials (§6.5).
- No token → failure `provider-token-missing` (24). The message lists the sources tried, in order,
  and the minimal scopes.
- HTTP 401 → `Unauthorized` (24). HTTP 403 with a scope message → `MissingScope` (24); the message
  names the scope. HTTP 404 on a repository that the token cannot see is reported as `NotFound`
  with the sentence "or the token cannot read this repository", because the three providers answer
  404 for a private repository without access.

### 5.2 Per provider

**GitHub**

| Order | Source | Scheme |
|---|---|---|
| 1 | `gh auth token --hostname <host>` | Bearer |
| 2 | `GH_TOKEN` | Bearer |
| 3 | `GITHUB_TOKEN` | Bearer |
| 4 | `GH_ENTERPRISE_TOKEN`, `GITHUB_ENTERPRISE_TOKEN` (only when the host is not `github.com`) | Bearer |

Minimal access. Fine-grained token or GitHub App: `Actions: read`, `Pull requests: read`,
`Contents: read`, `Metadata: read`. Classic token: `repo` for a private repository, no scope for a
public one. `GITHUB_TOKEN` inside a workflow: `permissions: { actions: read, pull-requests: read,
contents: read }`.

**GitLab**

| Order | Source | Scheme |
|---|---|---|
| 1 | `glab config get token --host <host>` | Bearer |
| 2 | `GITLAB_TOKEN` | Bearer |
| 3 | `GITLAB_ACCESS_TOKEN` | Bearer |
| 4 | `CI_JOB_TOKEN` | `JOB-TOKEN` header |

Minimal scope: `read_api`. A job token can download job artifacts but cannot read merge requests or
pipelines of the API used here; with only `CI_JOB_TOKEN` the command works when `<pr>` and the
pipeline can be read anonymously (public project), else it fails with `MissingScope` and names
`GITLAB_TOKEN` with `read_api`. The exact `glab` invocation is verified in OQ-2.

**Azure DevOps**

| Order | Source | Scheme |
|---|---|---|
| 1 | `az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv` | Bearer |
| 2 | `AZURE_DEVOPS_EXT_PAT` | Basic, user name empty |
| 3 | `SYSTEM_ACCESSTOKEN` | Bearer |

Minimal PAT scopes: `Code (Read)`, `Build (Read)`. For `SYSTEM_ACCESSTOKEN` the pipeline must map it
(`env: SYSTEM_ACCESSTOKEN: $(System.AccessToken)`) and the build service identity needs read access to
the repository and builds. Azure DevOps Server does not accept the `az` token; source 1 is skipped
when the host is not `dev.azure.com`.

---

## 6. Provider implementations

### 6.0 Rules for all providers

**HTTP.** One named client, `ProviderHttp.ClientName` (`xping-ci`). Headers: `User-Agent:
CliVersion.UserAgent`, `Accept` per provider. No cookies, no automatic redirect.

**Budget** (`TriageConstants`, all on the injected `TimeProvider`):

| Name | Value | Meaning |
|---|---|---|
| `ProviderAttemptTimeout` | 15 s | One API request |
| `ProviderTotal` | 60 s | All API requests of one invocation, downloads excluded |
| `DownloadTimeout` | 120 s | One artifact download |
| `DownloadTotal` | 300 s | All downloads of one invocation |
| `ProviderMaxRetries` | 2 | On 5xx, 408 and connection failures. Constant 500 ms delay |
| `RateLimitMaxWait` | 20 s | Longest `Retry-After` (or reset time) the command waits for, once per invocation. A longer wait fails with `RateLimited` (25) and prints the reset time |
| `MaxPages` | 10 | Pages read per list call, 100 items each. More → warning `W-PAGING` and the first 1 000 items are used |
| `MaxRuns` | 20 | Pipeline runs read for one head, newest first |
| `MaxAttemptsPerRun` | 10 | Attempts read per run, newest first |
| `MaxArtifactBytes` | 200 MB | One artifact, compressed. Larger → skipped, `W-ARTIFACT-TOO-LARGE` |
| `MaxArtifactTotalBytes` | 500 MB | All artifacts of one invocation |
| `ProviderConcurrency` | 4 | Parallel requests and downloads |

The pipeline is built with `Microsoft.Extensions.Http.Resilience`, as `CloudResilience.Build` does.

**Pagination, rate limits, versions** are given per provider below.

**Status mapping.** Every provider status maps to `PipelineStatus` and `PipelineResult` (§3.3). A value
that is not listed maps to `Completed` + `Other` when the item has an end time, else `Running`.

**Failures** throw `ProviderException` with a `ProviderFailureKind`. `TriageCommand` maps them:
`Unauthorized`, `Forbidden`, `MissingScope` → 24; `NotFound`, `Ambiguous` → 23; the rest → 25.

**Fork PRs.** The PR is read from the base repository. `ChangeRequest.IsFork` is set and
`HeadRepository` names the fork. Pipelines are read where the provider runs them (per provider
below). Two consequences are stated in the output as warning `W-FORK`: a fork build usually has no
`XPING_APIKEY`, so Cloud has no session for it; and `--rerun` fetches the head through a PR ref of the
base remote (`ChangeRequest.FetchRefs`), never by adding a remote.

### 6.1 Resolving the PR, the head, the base and the merge-base

`ChangeRequestResolver`:

1. `<pr>` given → `IChangeRequestHost.GetAsync(number)`.
2. Else, branch known → `FindForBranchAsync(branch, headSha)`. None → failure `change-request-not-found`
   (23): "No open pull request for branch `<b>` in `<repo>`. Push the branch and open one, or pass a
   number: `xping triage <number>`." More than one → `Ambiguous` (23), listing the numbers.
3. Else (detached HEAD and no `<pr>`) → failure `change-request-not-found` (23) asking for a number.

A closed or merged PR is triaged as it is; the envelope carries `changeRequest.state`.

The local branch may be behind or ahead of the PR head. Triage always uses the head the provider
reports. When the local `HEAD` differs, warning `W-HEAD-DIFFERS` says which commit is triaged.

**Merge-base**, first that succeeds:

1. `ChangeRequest.MergeBaseSha` from the provider.
2. `git merge-base <BaseSha> <HeadSha>` when both commits exist locally.
3. After `FetchCommitAsync` for the missing commit(s): step 2 again.
4. Unknown. The diff source and the merge-base rows of §7.4 are then unavailable; warning
   `W-NO-MERGE-BASE`.

Triage runs `git fetch` only in step 3 and in `--rerun`. A fetch adds objects and writes
`FETCH_HEAD`. It changes no branch, no index and no working file. Fetch is skipped, with step 4 as the
result, when `--rerun` is absent and the environment variable `XPING_TRIAGE_NO_FETCH=1` is set.

### 6.2 GitHub

API base `https://api.github.com` (GHES: `https://{host}/api/v3`). Headers `Accept:
application/vnd.github+json`, `X-GitHub-Api-Version: 2022-11-28`. Pagination: `per_page=100` and the
`Link` header `rel="next"`. Rate limit: HTTP 403 or 429 with `x-ratelimit-remaining: 0` (wait until
`x-ratelimit-reset`) or `retry-after` (secondary limit).

| Step | Request | Members read |
|---|---|---|
| PR by number | `GET /repos/{o}/{r}/pulls/{n}` | `number`, `html_url`, `state`, `merged`, `head.sha`, `head.ref`, `head.repo.full_name`, `base.ref`, `base.sha`, `base.repo.full_name` |
| PR by branch | `GET /repos/{o}/{r}/pulls?state=open&head={headOwner}:{branch}` then, when empty, `GET /repos/{o}/{r}/commits/{headSha}/pulls` | same; `headOwner` is `{o}`. The second call finds a PR opened from a fork |
| Merge-base | `GET /repos/{o}/{r}/compare/{base.sha}...{head.sha}?per_page=1` | `merge_base_commit.sha` |
| Runs for the head | `GET /repos/{o}/{r}/actions/runs?head_sha={head}&per_page=100` | `id`, `name`, `run_number`, `run_attempt`, `event`, `status`, `conclusion`, `head_sha`, `html_url`, `run_started_at` |
| One earlier attempt | `GET /repos/{o}/{r}/actions/runs/{id}/attempts/{k}` for `k` in `1 … run_attempt-1` | `status`, `conclusion`, `run_started_at`, `html_url` |
| Jobs of an attempt | `GET /repos/{o}/{r}/actions/runs/{id}/attempts/{k}/jobs?per_page=100` | `id`, `name`, `status`, `conclusion`, `html_url`, `run_attempt` |
| Artifacts of a run | `GET /repos/{o}/{r}/actions/runs/{id}/artifacts?per_page=100` | `id`, `name`, `size_in_bytes`, `expired`, `expires_at` |
| Download | `GET /repos/{o}/{r}/actions/artifacts/{artifactId}/zip` → `302` | the `Location` header, then §6.5 |

- `FetchRefs`: `refs/pull/{n}/head`.
- Fork PRs: `pull_request` runs belong to the base repository and are found by `head_sha` there.
- `TestedSha` is not in the run payload for `pull_request` events. It comes from the sessions
  (`CI.CommitSha`).
- Mapping: `status` `queued`/`requested`/`waiting`/`pending` → `Queued`; `in_progress` → `Running`;
  `completed` → `Completed`. `conclusion` `success` → `Succeeded`; `failure`, `timed_out`,
  `startup_failure` → `Failed`; `cancelled` → `Cancelled`; `skipped`, `neutral` → `Skipped`.
- **Attempts**: `run_attempt` on the run is the latest attempt number. Attempts are per run; a
  re-run of failed jobs creates a new attempt in which the jobs that passed keep their earlier result.
- **Artifact to attempt**: by name (§6.5). Whether the artifacts list of a run returns the artifacts
  of earlier attempts is checked in OQ-1; the naming convention makes the design independent of the
  answer as long as they are returned at all.
- **Re-run instruction**: command `gh run rerun {runId} --failed` (with `--repo {o}/{r}`, and
  `--hostname` is not an option of that command, so on GHES the command is prefixed
  `GH_HOST={host} ` in posix and `$env:GH_HOST='{host}'; ` in PowerShell); URL `html_url` of the run.

### 6.3 GitLab

API base `{server}/api/v4`. The project id is the URL-encoded full path (`group%2Fsub%2Fproject`).
Pagination: `per_page=100` and the `Link` header `rel="next"` (or `x-next-page`). Rate limit: HTTP 429
with `Retry-After`. Supported server version: 15.0 or later (the members below exist since then);
an older server that answers without them fails with `InvalidResponse`.

| Step | Request | Members read |
|---|---|---|
| MR by number | `GET /projects/{id}/merge_requests/{iid}` | `iid`, `web_url`, `state`, `sha`, `source_branch`, `target_branch`, `source_project_id`, `target_project_id`, `diff_refs.base_sha`, `diff_refs.start_sha`, `diff_refs.head_sha` |
| MR by branch | `GET /projects/{id}/merge_requests?state=opened&source_branch={branch}` | same |
| Pipelines of the MR | `GET /projects/{id}/merge_requests/{iid}/pipelines?per_page=100` | `id`, `project_id`, `sha`, `ref`, `status`, `source`, `web_url`, `created_at` |
| Jobs with retries | `GET /projects/{pipelineProjectId}/pipelines/{pipelineId}/jobs?include_retried=true&per_page=100` | `id`, `name`, `stage`, `status`, `allow_failure`, `created_at`, `finished_at`, `web_url`, `artifacts_file.filename`, `artifacts_file.size`, `artifacts_expire_at` |
| Child pipelines | `GET /projects/{pipelineProjectId}/pipelines/{pipelineId}/bridges?per_page=100` | `downstream_pipeline.id`, `downstream_pipeline.project_id`, `downstream_pipeline.status`, `downstream_pipeline.web_url`; one level, same project only |
| Download | `GET /projects/{pipelineProjectId}/jobs/{jobId}/artifacts` | the archive; may answer `302`, then §6.5 |

- Head: `sha`. `BaseSha`: `diff_refs.start_sha`. **Merge-base: `diff_refs.base_sha`**, reported by
  GitLab.
- `FetchRefs`: `refs/merge-requests/{iid}/head`.
- Pipelines are kept when their `sha` equals the head, or when `ref` is
  `refs/merge-requests/{iid}/merge` or `…/train` and their jobs' sessions name the head (§4.3). A
  merged-results pipeline has the merge commit as `sha`; it is on the head side.
- Fork MRs: the pipeline's `project_id` is the fork unless a member ran it in the parent. All later
  calls use the pipeline's `project_id`. A token without access to the fork gives `NotFound` for
  those calls; the pipeline is then listed with `store: "unreadable"` and warning `W-FORK`.
- Mapping: `created`, `waiting_for_resource`, `preparing`, `pending`, `scheduled`, `manual` →
  `Queued`; `running` → `Running`; `success` → `Succeeded`; `failed` → `Failed`; `canceled`,
  `canceling` → `Cancelled`; `skipped` → `Skipped`. A failed job with `allow_failure: true` is kept
  as failed: its tests failed.
- **Attempts.** GitLab has two forms and both are read:
  - *Retried jobs*: several jobs with one `name` in one pipeline. Ordered by `id` ascending, they are
    job attempts 1…k. `PipelineJob.Attempt` is that index.
  - *Re-run pipelines*: several pipelines for one head. Each is a `PipelineRun` with one
    `PipelineAttempt` (number 1) whose jobs are the newest job of each name; earlier jobs of a name
    appear as jobs with a lower `Attempt` in the same `PipelineAttempt`.
  The "latest attempt" of D4 is, per job name, the job with the highest `id` in the newest pipeline
  that contains that name.
- **Artifact**: one archive per job. A job is a store candidate when `artifacts_file.filename`
  starts with `xping-store` (§6.5). `CiArtifact.Id` is the job id; `JobAttempt` is the job's index.
- **Re-run instruction**: one failed job → `glab ci retry {jobId}` (with `-R {fullPath}`); several →
  no command, URL of the pipeline; `Description` says "Retry the failed jobs on the pipeline page."
  The `glab` syntax is verified in OQ-2.

### 6.4 Azure DevOps

API base `https://dev.azure.com/{org}` (Server: `https://{host}/{collection}`). Every request carries
`api-version=7.1`. Pagination: `$top=100` and the response header `x-ms-continuationtoken`, sent back
as `continuationToken`. Rate limit: HTTP 429 with `Retry-After`. Azure DevOps Server 2022 or later.

| Step | Request | Members read |
|---|---|---|
| Repository | `GET /{project}/_apis/git/repositories/{repo}` | `id` |
| PR by number | `GET /{project}/_apis/git/repositories/{repoId}/pullrequests/{n}` | `pullRequestId`, `status`, `sourceRefName`, `targetRefName`, `lastMergeSourceCommit.commitId`, `lastMergeTargetCommit.commitId`, `lastMergeCommit.commitId`, `forkSource.repository.id` |
| PR by branch | `GET /{project}/_apis/git/repositories/{repoId}/pullrequests?searchCriteria.status=active&searchCriteria.sourceRefName=refs/heads/{branch}` | same |
| Merge-base | `GET /{project}/_apis/git/repositories/{repoId}/commits/{head}/mergebases?otherCommitId={baseTip}` | `value[0].commitId` |
| Builds of the PR | `GET /{project}/_apis/build/builds?repositoryId={repoId}&repositoryType=TfsGit&branchName=refs/pull/{n}/merge&reasonFilter=pullRequest&queryOrder=queueTimeDescending&$top=100` | `id`, `buildNumber`, `status`, `result`, `sourceVersion`, `triggerInfo["pr.sourceSha"]`, `definition.name`, `queueTime`, `_links.web.href` |
| Timeline (latest) | `GET /{project}/_apis/build/builds/{buildId}/timeline` | `records[]`: `id`, `parentId`, `type` (`Stage`, `Phase`, `Job`), `name`, `identifier`, `state`, `result`, `attempt`, `previousAttempts[].attempt`, `previousAttempts[].timelineId` |
| Timeline (earlier attempt) | `GET /{project}/_apis/build/builds/{buildId}/timeline/{timelineId}` | same |
| Artifacts | `GET /{project}/_apis/build/builds/{buildId}/artifacts` | `id`, `name`, `resource.type`, `resource.downloadUrl`, `resource.properties.artifactsize` |
| Download | `GET {resource.downloadUrl}` (with `format=zip`) | the archive; may answer `302`, then §6.5 |

- Head: `lastMergeSourceCommit.commitId`. `BaseSha`: `lastMergeTargetCommit.commitId`.
- A build is kept when `triggerInfo["pr.sourceSha"]` equals the head. `TestedSha` is `sourceVersion`
  (the merge commit).
- `FetchRefs`: `refs/heads/{sourceBranch}` (same repository), else `refs/pull/{n}/merge` is **not**
  the head and is not used; a fork PR head that is not on the base remote cannot be fetched, and
  `--rerun` then fails for the head with evidence `rerun.fetchFailed`.
- Mapping: `status` `notStarted`, `postponed` → `Queued`; `inProgress`, `cancelling` → `Running`;
  `completed` → `Completed`. `result` `succeeded` → `Succeeded`; `partiallySucceeded`, `failed` →
  `Failed`; `canceled` → `Cancelled`. Timeline `state`/`result` map the same way; `skipped` →
  `Skipped`.
- **Attempts.** Two forms, both read:
  - *Stage and job re-runs*: a timeline record has `attempt` and `previousAttempts`. A
    `PipelineAttempt` number `k` of a build is the set of `Job` records whose stage attempt is `k`.
    `PipelineJob.Attempt` is the job record's own `attempt`.
  - *Re-queued builds*: several builds with the same `pr.sourceSha`. Each is a `PipelineRun`.
- **Artifact to attempt**: by name (§6.5). Only `resource.type` `PipelineArtifact`
  and `Container` are read.
- **Re-run instruction**: there is no `az` command that re-runs failed jobs of a build. `Command` is
  null, `Url` is the build page, `Description` is "Open the build and choose *Rerun failed jobs*."
- Scope: repositories hosted in Azure Repos (`repositoryType=TfsGit`). A GitHub repository built by
  Azure Pipelines is mixed hosting (OQ-9).

### 6.5 The artifact convention, download and cache

**What is uploaded**: the directory `.xping/sessions/` of the job, with an "always" condition, once
per job attempt (§12.2).

**Name.** `ArtifactNaming` defines and parses it:

```
xping-store-<job>-attempt-<run attempt>[.<job attempt>]
```

- `<job>`: free text that makes the name unique in the run. Allowed: letters, digits, `-`, `_`, `.`.
- `<run attempt>`: GitHub `github.run_attempt`; Azure DevOps `System.StageAttempt`. `<job attempt>`:
  Azure DevOps `System.JobAttempt`.
- GitLab does not use the suffix: an archive belongs to one job, and the job is the attempt. The
  name is `xping-store-<job>` and only the prefix is checked.

Parsing is `^xping-store(?:-(?<job>.+?))?(?:-attempt-(?<run>\d+)(?:\.(?<job_attempt>\d+))?)?$`,
ordinal, case-sensitive. An artifact whose name starts with `xping-store` and has no attempt suffix is
still read; its sessions say their attempt (§12.1) or are used without one.

**Which artifacts are downloaded.** Only those needed, newest first, within the byte limits:

1. Every `xping-store` artifact of the **latest attempt** of each run that has a failed job.
2. Every `xping-store` artifact of **earlier attempts** of those runs, and of other runs for the same
   head, because they hold the passes and failures that decide R1 (§7.4).

An artifact with `Expired` is not requested; its job gets `store: "expired"` and `W-ARTIFACT-EXPIRED`.

**Redirects.** The provider answers the download with `302` to a storage URL. The client follows at
most 3 redirects by hand, **without** the provider credential headers, and only to `https`.

**Reading.** `ArtifactStoreReader`:

1. Streams the archive to a temporary file under the cache directory. The compressed size is checked
   against `MaxArtifactBytes` from the list response before the request and from the stream during it.
2. Opens it with `System.IO.Compression.ZipArchive`. Takes only entries whose **file name** matches
   `session-*.json.gz` (`SessionFileName.SearchPattern`), at any depth. Each is written to
   `<cache>/<key>/sessions/<file name>`: the entry's directory is ignored, so no entry can write
   outside the target. Limits: 2 000 entries, 50 MB per entry uncompressed, 1 GB per archive
   uncompressed; over a limit the artifact is skipped with `W-ARTIFACT-TOO-LARGE`.
3. Reads the sessions with `LocalSessionStore.Create(storePath: <cache>/<key>)` and `ReadEach(null)`.
   An unreadable file is counted in `sources.ciArtifacts.unreadable`.

**Cache.** `ArtifactCache` keeps extracted stores in `XpingHome` under
`cache/triage/<provider>/<repo hash>/<artifact id>/`. An artifact id is immutable on all three
providers, so a cached entry is never refreshed. Entries not used for 7 days are deleted at the start
of an invocation, and the oldest are deleted until the cache is under 500 MB. Files are created
owner-only through `PrivateFiles`. Session files hold error messages and stack traces of the user's
code and nothing else is stored there: no token, no URL with a signature. Writes go to a temporary
directory and are moved into place, so two invocations can run at the same time.

### 6.6 Behaviour in special states

| State | Behaviour | Exit |
|---|---|---|
| No pipeline run for the head | Error `no-run`: "No pipeline run found for `<sha7>` on PR #n." With the provider's page URL | 23 |
| Runs exist, all latest attempts succeeded | Envelope with zero failures. `resolvedTests` lists tests that failed in an earlier attempt | 0 |
| Latest attempt failed, no test failed in any session (a build error, a lint job) | Envelope with zero failures and `run.jobs` listing the failed jobs, each with `store` and URL. Warning `W-NO-TEST-FAILURES` | 0 |
| A run is in progress; no completed failed job with a store | Error `run-in-progress`: "The pipeline for `<sha7>` is still running. Run `xping triage` again when it finishes." | 22 |
| A run is in progress; some jobs already failed and have a store | Those failures are triaged. Warning `W-RUN-IN-PROGRESS`. `run.complete` is `false` | 20 / 21 |
| Failed jobs exist, none has a store, no artifact with the prefix exists anywhere in the run | Error `store-not-uploaded`, with the snippet for the provider (§12.2) and the docs URL | 23 |
| Failed jobs exist, their stores are expired | Error `store-expired`: names the expiry date and says to re-run the pipeline, or to use `--rerun` when the tests are known (`--test`) | 23 |
| Some failed jobs have a store and some do not | The ones with a store are triaged. Each other job is in `run.jobs` with `store: "missing"`. Warning `W-STORE-MISSING` | 20 / 21 |
| Token lacks a scope | Error `provider-scope`, names the scope (§5.2) | 24 |
| PR/MR not found | Error `change-request-not-found` | 23 |
| PR from a fork | Triage proceeds as §6.0. Warning `W-FORK` | — |
| Cancelled run | Treated as completed without failures from that run. Warning `W-RUN-CANCELLED` | — |

---

## 7. Evidence and classification

### 7.1 Sources

| # | Source | What it gives | Strict |
|---|---|---|---|
| S1 | CI provider API | The PR, head, base, merge-base; runs, attempts, jobs and their results | — (no test results) |
| S2 | CI artifact | The sessions of each job attempt: the exact failure record and every pass or fail of the same test in other attempts | yes |
| S3 | Developer's local store | Sessions at the head, the merge-base or on the base branch (`Git.SHA`, `Git.Branch`) | no |
| S4 | `git diff <merge-base> <head>` | Changed files and how each relates to a failed test | — (hint) |
| S5 | Xping Cloud | Digest (Cloud's verdicts), run rows at the head side and the merge-base, the newest pass, signature match | run rows: per D5 |
| S6 | `--rerun` | Runs at the head and the merge-base in clean worktrees | yes |

### 7.2 Observations

One **observation** is one session's result for one test: the deciding attempt's outcome (`Passed`;
`Failed` or `Timeout` → `Failed`), plus the outcomes of every attempt in that session
(`InRunAttempts`). A session in which the test's deciding attempt is `Skipped`, `Inconclusive` or
`NotExecuted` is not an observation.

`SessionCommit.Of(TestSession)` is the only place that reads a session's commit:

1. `CustomProperties["CI.CommitSha"]` when `EnvironmentInfo.IsCIEnvironment` → `CodeId(sha, HeadOnly: false)`.
2. Else `PullRequestContext.CommitSha` when CI → `CodeId(sha, HeadOnly: true)`.
3. Else `CustomProperties["Git.SHA"]` → `CodeId(sha, false)`, and the observation is not strict.
4. Else none: the session is not an observation.

A rerun session takes its code id from the store directory it was read from (§9.4), not from its
properties.

Local store reading (`LocalHistoryReader`): `ISessionSource.ReadEach(assembly)` for each assembly of
the subjects, one pass, one session held at a time, keeping only observations of subject fingerprints
whose `Git.SHA` is the head, a head-side commit or the merge-base, or whose `Git.Branch` is the PR's
base branch. Sessions stamped more than 5 minutes ahead of the local clock are used but reported as
`W-CLOCK` (the count), because "newest" among local sessions then cannot be trusted.

### 7.3 Merge, precedence and conflicts

Observations of one test are merged on `(Assembly, Fingerprint)`.

1. **Duplicates.** The same session can arrive twice (a CI session also uploaded to Cloud). Two
   observations with the same `SessionId` are one; the one from the source higher in this order is
   kept: `CiArtifact`, `Rerun`, `LocalStore`, `Cloud`. A Cloud row whose `SessionKey` equals a local
   `SessionId` is that session.
2. **The failure block** (message, stack, location) comes from the subject's S2 session (D4). No
   other source overwrites it. When the subject comes from Cloud only (P6b), the block comes from
   Cloud and `failure.source` says so.
3. **Runner, framework, project path** for commands come from the subject's session; when it lacks
   them, from the newest session of the same assembly in S2, then S3. Never from Cloud.
4. **Conflicting outcomes for one session id** (the artifact says failed, Cloud says passed): the
   higher source wins and warning `W-SOURCE-CONFLICT` names the session. This is a defect in one
   source and is not evidence about the test.
5. **Different fingerprints, same name.** Two fingerprints with one fully qualified name in one
   assembly (a pinned fingerprint changed) are two tests. Triage does not join them. The evidence
   line `identity.renamed` is added to each as a hint.
6. **The same fingerprint in two assemblies** is two tests (the `TestResolver` rule).
7. **Strict beats non-strict only in §7.4.** Non-strict observations are never dropped; they are
   shown as hints.

### 7.4 The decision table

Input: the observations of one subject test `T`, the head, the head side `H`, the merge-base `B`, and
the rerun facts when `--rerun` ran. "Strict" is D5. Constants: `MinHeadFailures = 2`,
`MinBaseObservations = 1`.

Definitions over **strict** observations only:

- `flip(c)`: at code id `c` (identical code, D5) there is at least one `Passed` and one `Failed`.
- `inRunFlip`: a session whose `InRunAttempts` holds both `Passed` and `Failed`.
- `headFails`: number of `Failed` observations at commits in `H`. `headPasses`: number of `Passed`.
- `baseFails`, `basePasses`: the same at `B`. Both are 0 when `B` is unknown.
- `absentAtBase`: `--rerun` built the test project at `B`, ran the filter `N` times, and no execution
  of `T` was recorded (`RerunFacts.AbsentAtBase`).

Rows are evaluated in order. The first that matches decides.

| Rule | Condition | Next step | `observation` | `causedByChange` | Decisive evidence |
|---|---|---|---|---|---|
| **R1** | `flip(c)` for some `c ∈ H`, or `inRunFlip` in a session at a commit in `H` | `RE-RUN` | `flippedOnIdenticalCode` | `null` | the passes and fails at `c` |
| **R2** | `headPasses = 0` and `baseFails ≥ MinBaseObservations` and `basePasses = 0` | `INVESTIGATE` | `failsAtMergeBase` | `false` | the failures at `B` |
| **R3** | `headPasses = 0` and `headFails ≥ MinHeadFailures` and `basePasses ≥ MinBaseObservations` and `baseFails = 0` | `INVESTIGATE` | `failsOnlyWithChange` | `true` | the failures in `H`, the passes at `B` |
| **R4** | `headPasses = 0` and `headFails ≥ MinHeadFailures` and `absentAtBase` | `INVESTIGATE` | `newTestFails` | `true` | the failures in `H`, the absence at `B` |
| **R5** | anything else | `INCONCLUSIVE` | `undetermined` | `null` | none |

Notes that are part of the rules:

- **R1 needs identical code, not the same head.** A fail at merge commit `m1` and a pass at merge
  commit `m2` of the same head do not match R1. They produce the hint `ci.sameHeadDifferentMerge`.
- **R1 and reruns.** `--rerun` runs at the head commit itself. Mixed results there are `flip(head)`
  and match R1. `N` passes there while CI failed at a merge commit do **not** match R1: the code
  differs. That case falls to R5 with the hint `rerun.passesAtHeadOnly`, whose sentence says the
  failure may come from the merge with the base branch and to update the branch.
- **R1 before R2.** A flip at the head side decides `RE-RUN` even when the test also fails at the
  merge-base.
- **R2 with one base failure.** One strict failure at the merge-base with no pass there is enough:
  the change cannot have caused a failure that exists without it. If the test is unstable at the
  merge-base, the next rerun or attempt shows a pass and R2 no longer matches.
- **`flip(B)`** (passes and fails at the merge-base, none of R1): falls to R5 with the hint
  `base.flipped`. The test is unstable without the change, and that does not say what happened at the
  head.
- **R3 needs two head failures** from separate sessions. One CI failure and a pass at the merge-base
  is what an unstable test looks like too, so it is `INCONCLUSIVE` and the sentence asks for
  `--rerun`. In-run retries are attempts of one session and count once.
- **Timeouts** are failures. A test that timed out and a test that failed an assertion are the same
  outcome for §7.4; `failure.kind` and the signature lines show the difference.
- **Signature.** §7.4 compares outcomes, not signatures. When the failures compared by R2 or R3 have
  different local signature hashes, the hint `failure.signatureDiffers` is added. It does not change
  the rule.

Reachability without `--rerun` and without Cloud: R1 (two CI attempts on one tested commit, or an
in-run flip) and R5. R2–R4 need strict observations at the merge-base, which come from `--rerun` or
from Cloud CI rows on the base branch.

### 7.5 Next-step sentences, commands and the "pre-existing" case

`nextStep.sentence` is chosen by rule and by the hints present. Texts are binding.

| Case | Sentence |
|---|---|
| R1 | `Passed and failed on the same commit. Re-run the failed jobs.` |
| R2 | `Fails at the merge-base too. This change did not cause it. Fix it on <base branch>, or update your branch if <base branch> already has a fix.` |
| R2 and Cloud `canonical.latestOutcome = passed` | `Fails at the merge-base too, and passes on the canonical branch in Xping Cloud. Update your branch from <base branch>.` |
| R3 | `Fails <f> of <f> times at the head and passes at the merge-base. This change is the likely cause.` |
| R4 | `New in this change, and fails <f> of <f> times at the head.` |
| R5, no `--rerun` | `Not enough evidence to decide. Run: xping triage <n> --rerun 5 --test <short fingerprint>` |
| R5, Cloud says flaky | `Not enough evidence to decide. Xping Cloud classifies this test as flaky. Re-run the failed jobs, or run: xping triage <n> --rerun 5 --test <short fingerprint>` |
| R5 after `--rerun`, `rerun.passesAtHeadOnly` | `Passes <p> of <p> times at the head, but CI tested the merge with <base branch>. Update your branch from <base branch> and run CI again.` |
| R5 after `--rerun`, other | `Still not decided after <N> runs. See the evidence.` |

For the pre-existing case (R2) the next step is `INVESTIGATE` with `causedByChange: false`. It is not
a fourth next step: the test fails every time, so re-running is wrong and someone must look. What
differs is where: on the base branch, not in this change. An agent reads `causedByChange` and does not
search the PR diff for the cause.

Commands on every failure (§10.3): `rerunCommand` (CI re-run, from `RerunInstruction`),
`reproCommand` (local repro, §13 change to `ReproCommand`), and `triageCommand` (the `--rerun` call)
on `INCONCLUSIVE` only.

### 7.6 Evidence lines

Every fact shown about a test is an `EvidenceLine`. `code` values are part of the contract
(`EvidenceCodes`). `decisive` is true only for the lines named in the rule that decided.

| Code | Source | Text pattern | Can be decisive |
|---|---|---|---|
| `ci.attempts` | ci | `CI: failed in attempts <list>, passed in attempt <list> of <run name> (same commit <sha7>)` / `CI: failed in <k> of <k> attempts of <run name>` | yes |
| `ci.inRunRetries` | ci | `CI: failed <f> and passed <p> of <n> in-run attempts` | yes (when mixed) |
| `ci.otherJobs` | ci | `CI: also failed in <job>` / `CI: passed in <job> on the same commit` | yes |
| `ci.sameHeadDifferentMerge` | ci | `CI: passed in another run of the same head, on a different merge result` | no |
| `rerun.head` | rerun | `Rerun: failed <f> of <n> at head <sha7>` | yes |
| `rerun.mergeBase` | rerun | `Rerun: passed <p> of <n> at merge-base <sha7>` / `failed <f> of <n> …` / `the test does not exist at merge-base <sha7>` | yes |
| `rerun.passesAtHeadOnly` | rerun | `Rerun: passes at head <sha7>; CI tested merge commit <sha7>` | no |
| `rerun.buildFailed` | rerun | `Rerun: the build failed at <head|merge-base> <sha7>` (+ `data.logTail`) | no |
| `rerun.notRunnable` | rerun | `Rerun: no runner or project was recorded for this test` | no |
| `rerun.timedOut`, `rerun.fetchFailed` | rerun | one sentence each | no |
| `cloud.runsAtHead` | cloud | `Xping Cloud: <f> failed, <p> passed in CI at <sha7>` | yes, when strict (D5) |
| `cloud.runsAtMergeBase` | cloud | `Xping Cloud: <p> passed, <f> failed in CI at merge-base <sha7>` | yes, when strict |
| `cloud.flaky` | cloud | `Xping Cloud: flaky (<severity>), confidence <score> (<evidence level>)` | **never** (D7) |
| `cloud.verdict` | cloud | `Xping Cloud: <verdict> on the canonical branch` | never |
| `cloud.signature` | cloud | `Xping Cloud: this failure was seen in <runs> runs on <machines> machines since <date>` / `…has not been seen before` | never |
| `local.history` | local | `Local store: <p> passed, <f> failed at <sha7> (working tree state unknown)` | never (X2) |
| `local.baseBranch` | local | `Local store: <p> passed, <f> failed on <base branch>` | never |
| `diff.testSource` | git | `This change modifies the test's source file` (or `adds`) | never |
| `diff.stackFrame` | git | `This change modifies <path>, which is in the failure's stack` | never |
| `diff.testProject` | git | `This change modifies <n> files in the test project` | never |
| `diff.referencedProject` | git | `This change modifies <n> files in <project>, which the test project references` | never |
| `diff.none` | git | `This change touches no file related to the test` | never |
| `failure.kind` | ci | `Failure kind: <kind>` | never |
| `failure.signatureDiffers` | ci | `The failure at the merge-base has a different signature` | never |
| `base.flipped` | rerun/cloud | `Passed and failed at merge-base <sha7>` | never |
| `identity.renamed` | ci | `Another test with this name has a different fingerprint` | never |

Order of the lines of one test: decisive first, then by the order of this table, then by `text`
ordinal.

**Changed files** (`DiffReader`, `changedFiles` of an `INVESTIGATE` or `INCONCLUSIVE` failure):
`git diff --name-status -M <merge-base> <head>`, at most 5 000 paths (more → `W-DIFF-TRUNCATED`).
A changed file is related to a test by the first matching relation:

| Relation | Rule |
|---|---|
| `stackFrame` | The path is the mapped path of a stack line (D9) |
| `testSource` | The path is the mapped `Identity.SourceFile` |
| `testProject` | The path is under the directory of `Xping.ProjectPath` |
| `referencedProject` | The path is under the directory of a project that the test project references. References are read from `<ProjectReference Include="…">` elements of the project file at the head (`git show <head>:<path>`), followed to depth 5, at most 200 projects, as XML only: no MSBuild evaluation, no conditions, no imports |

At most 20 files per test, ordered by relation in the order above, then path ordinal. `reason` is the
relation's sentence. Unrelated files are not listed; `summary.changedFiles` has the total count.

**Last pass** (`lastPassed`): the newest `Passed` observation of the test from any source, with its
commit, time, source and `strict` flag. Null when there is none.

**Assertion formats** (`AssertionParser`), matched on the raw message, first match:

| Framework | Pattern | Example |
|---|---|---|
| xUnit | lines `Expected:` and `Actual:` (each may continue on indented lines) | `Assert.Equal() Failure…\nExpected: 5\nActual:   4` |
| NUnit | lines `Expected:` and `But was:` | `  Expected: 5\n  But was:  4` |
| MSTest | `Expected:<…>. Actual:<…>.` on one line | `Assert.AreEqual failed. Expected:<5>. Actual:<4>.` |

The fixtures are the real messages in `tests/Xping.Sdk.Core.Tests/Diagnostics/RealFailureSamples.cs`.
A message that matches none gives `assertion: null`.

---

## 8. Xping Cloud mode

### 8.1 When Cloud is read

`TriageCloudReader` runs when all hold: `--no-cloud` is absent; `CredentialResolver.ResolveAsync`
returns a credential; there is at least one subject. Otherwise `sources.cloud.status` is
`not-attempted` with the reason (`disabled`, `no-credential`, `no-failures`).

`--format summary` reads Cloud like the other formats: the counts in the summary depend on the next
steps, and Cloud observations can decide them.

### 8.2 What is read, with the existing API

1. **Project per assembly**: `ProjectResolver.ResolveAsync(client, assembly, lookup)` with the
   `--project` flag, then `XPING_PROJECTID`, then the session pin of the subject's CI session
   (`LocalSessionProperties.ProjectId`), then the cache, then the name match. Exactly the order
   `report` uses.
2. **Digest**: `GetDigestAsync(projectKey, DigestRequest)` with one `DigestRequestTest(fingerprint,
   [ErrorMessageHash])` per subject, in batches of `CloudEnricher.MaxDigestBatchSize`, at most
   `TargetPlanner.MaxEnrichedTests` subjects in D4 order. `SessionKeys` is empty.
   Converted with `EnvelopeStitcher.ToDto`. `signatureAlgorithm` other than
   `CloudEnricher.SignatureAlgorithmId` makes every signature match `unknown`, as in `report`.
3. **Signature match**: `SignatureMatcher.Match(errorMessageHash, digest, algorithmMismatch)`.
4. **Runs**: `GetRunsAsync(projectKey, fingerprint, CloudEnricher.RunsLimit)` for at most
   `TriageConstants.MaxCloudRunLookups = 25` subjects, in D4 order, skipping tests whose digest state
   is `unknown`. From the rows:
   - rows with `IsCi == true` and `CommitSha` in the head side or equal to the merge-base become
     observations (D5);
   - rows with `IsCi != true` at those commits become non-strict observations;
   - the newest row with `Outcome == "passed"` is a candidate for `lastPassed`.
   When `HasMore` is true and no row reaches the merge-base commit, the line `cloud.runsAtMergeBase`
   is omitted: absence of a row is not evidence.

Concurrency: `CloudEnricher.MaxConcurrency`.

### 8.3 Budget and fail-open

- The budget is a `CloudBudget` with a total of `TriageConstants.CloudTotal = 10 s` and the existing
  1 s credential slice. `CloudBudget` gains a constructor parameter for the total (§13); `report`
  keeps 4 s.
- Retry, attempt timeout and the 429 rule are `CloudResilience` and `CloudHttp`, unchanged.
- Failure handling reuses the classification that `CloudEnricher` holds today, moved to an internal
  static class `CloudFailures` (§13): `CredentialFailure`, `IsReadFailure`, `Classify`, and the hint
  texts. `sources.cloud` reports `status` and `reason` in the `CloudContextDto` vocabulary (§1.4).
- **Any Cloud failure leaves the triage as it is without Cloud.** One line goes to stderr, with the
  same texts as `report` ("Cloud data unavailable: …", "Cloud data partial: …"), and
  `sources.cloud.status` says what happened. The exit code never changes because of Cloud.
- A subject whose runs were not read (budget, cap) has `cloud.runs: null`, and the envelope's
  `sources.cloud.runsRead` and `runsRequested` say how many were.

### 8.4 Attribution

- Everything from Cloud is under a member named `cloud` (per failure and in `sources`), or is an
  evidence line with `source: "cloud"` whose text starts with `Xping Cloud:`.
- `cloud.digest` is the `CloudDigestDto` of the report contract, unchanged
  (`Report/Contract/CloudDtos.cs`): verdict, confidence, flaky, canonical, signatures.
- Triage publishes no member named `flaky`, `score` or `confidence` outside `cloud`. The text
  renderer prints Cloud's verdict on a line that starts with `Xping Cloud:`.
- `observation` values describe what was observed (`flippedOnIdenticalCode`). None is "flaky".
- Cloud's evidence level words (`sparse`, `adequate`, `robust`) are printed only next to Cloud's
  confidence.

### 8.5 What Cloud adds today, and what it cannot

| Capability | Today |
|---|---|
| Cloud's flaky classification, verdict, confidence and evidence level per failed test | yes (digest) |
| Failure signature match across runs and machines | yes (digest + `SignatureMatcher`) |
| Strict passes and fails at the merge-base from CI on the base branch → R2, R3 without `--rerun` | yes (runs), when the merge-base was built by CI and is within the newest 50 rows |
| Passes and fails at the head from other CI runs → hint, or R1 for non-PR builds | partly: PR rows are `headOnly` (D5) |
| The newest commit where the test passed | yes (runs), within 50 rows |
| The failed tests of the PR head without the CI artifact | **no** (X1) |
| Error message and stack of a Cloud-only failure | **no**: rows have hashes only |

### 8.6 Required Cloud changes (`xping-dev/dashboard`) — tracked item C1, not part of this repository

Proposed additions to the DataGateway, all under `{gw}/v1`, same authentication, problem format and
camelCase JSON as the existing calls. They are a proposal for the Cloud contract owner; the CLI part
(P6b) starts only when the contract is approved and deployed.

**C1 — sessions of a commit**

```
GET /v1/projects/{projectKey}/sessions?commitSha={sha}[&pullRequestNumber={n}][&limit={1..50}]
```

`commitSha` matches the head commit of a PR session or the tested commit of any session.

```json
{
  "retentionDays": 30,
  "hasMore": false,
  "sessions": [
    {
      "sessionKey": "0b0e2a52-6a2d-4c58-9d2b-1f0c4a3e7a11",
      "startedAt": "2026-11-20T10:12:03Z",
      "state": "complete",
      "isCi": true,
      "ciProvider": "github",
      "ciRunId": "11873355021",
      "ciRunAttempt": 2,
      "ciJobId": "test",
      "ciJobAttempt": null,
      "commitSha": "4f2a9c1…",
      "testedCommitSha": "9d81b77…",
      "branch": "feature/checkout",
      "pullRequestNumber": 482,
      "tests": 1840, "failed": 3
    }
  ]
}
```

**C2 — failures of a session**

```
GET /v1/projects/{projectKey}/sessions/{sessionKey}/failures[?limit={1..200}][&cursor=…]
```

```json
{
  "next": null,
  "failures": [
    {
      "fingerprint": "a3f5…",
      "testName": "Shop.Tests.CheckoutTests.Total_IncludesTax",
      "assembly": "Shop.Tests",
      "outcome": "failed",
      "attempts": 1,
      "attemptOutcomes": ["failed"],
      "durationMs": 412,
      "exceptionType": "Xunit.Sdk.EqualException",
      "errorMessage": "Assert.Equal() Failure…",
      "errorMessageHash": "…64 hex…",
      "stackTraceHash": "…64 hex…",
      "stackTrace": null,
      "stackTraceOmitted": true,
      "runner": "VSTest",
      "testFramework": "xUnit",
      "projectPath": "tests/Shop.Tests/Shop.Tests.csproj",
      "sourceFile": "tests/Shop.Tests/CheckoutTests.cs",
      "sourceLine": 88
    }
  ]
}
```

`stackTrace` is present only when the project uploads traces (`UploadStackTraces`). Whether Cloud
stores `errorMessage` text at all is a question for the contract owner (OQ-5).

**C3 — more members on the runs row** (`GET …/tests/{fingerprint}/runs`): `testedCommitSha`,
`ciRunId`, `ciRunAttempt`. With `testedCommitSha`, PR rows stop being `headOnly` and can decide R1.

**C4 — optional filter on runs**: `&commitSha={sha}` (repeatable, at most 3), so the merge-base rows
are found even when they are older than the newest 50.

SDK side: the session already uploads `EnvironmentInfo.CustomProperties`, so the properties of §12.1
reach Cloud without a wire change.

With C1 and C2, phase P6b makes Cloud a second source for the subject set: when a failed job has no
artifact store and Cloud has a session for the same run and attempt, the failures come from Cloud,
with `failure.source: "cloud"` and no stack unless uploaded. The artifact stays the first choice
because it always has the stack.

---

## 9. `--rerun N`

### 9.1 What it does

For the subjects (all of them, or the one named by `--test`), triage runs the tests `N` times at the
head commit. For the tests that failed every time there, it runs them `N` times at the merge-base.
Both happen in temporary `git worktree`s. The working tree, the index, the current branch and the
developer's `sessions/` directory are never touched.

`--rerun` runs after sources S1–S5 are read. A test already decided by R1–R4 without rerun is not
rerun, except when `--test` names it.

### 9.2 Preconditions

| Check | Failure |
|---|---|
| `dotnet` on PATH | Error `dotnet-missing` (25) |
| `git worktree` available (`git --version` ≥ 2.17) | Error `git-too-old` (25) |
| The subject has `Xping.Runner` and `Xping.ProjectPath` (§7.3 rule 3) | That test is not rerun: evidence `rerun.notRunnable`; it stays as classified |
| The head commit exists locally, or `FetchCommitAsync(root, remote, head, FetchRefs)` succeeds | No head runs: evidence `rerun.fetchFailed` on every subject; exit code from the classification |
| The merge-base is known and exists locally or can be fetched | No base runs: R2–R4 cannot match from rerun |

Uncommitted local changes are never part of a rerun: a worktree checks out a commit. When
`RepositoryContext.IsDirty` is true, or the local `HEAD` is not the PR head, warning `W-RERUN-COMMIT`
says: "Rerun tests commit `<sha7>` (the PR head), not your working tree."

### 9.3 Worktree lifecycle

- Root: `<temp>/xping-triage/<repo key>/<invocation id>/`, where `<temp>` is `Path.GetTempPath()`,
  `<repo key>` is the first 8 hex of SHA-256 of the repository root (as `LocalStorePathResolver`
  hashes), and `<invocation id>` is 8 random hex. Inside: `head/`, `base/` and `owner.json`
  (`{ "pid": …, "startedAt": … }`). Short names keep Windows paths short.
- Create: `git worktree add --detach <path> <sha>`. When the worktree has a `.gitmodules` file:
  `git -C <path> submodule update --init --recursive`.
- Remove: `git worktree remove --force <path>`, then delete the invocation directory, then
  `git worktree prune`. Removal runs in a `finally` with its own 60 s limit and
  `CancellationToken.None`, so Ctrl+C still cleans up.
- **Ctrl+C** (first press): the token is cancelled; the running child process tree is killed
  (`Process.Kill(entireProcessTree: true)`); worktrees are removed; exit 130. A second press ends
  the process (`CancelKeyHandler`), and worktrees may stay.
- **Stale sweep**, at the start of every `--rerun`: each invocation directory under
  `<temp>/xping-triage/<repo key>/` whose `owner.json` names a process that no longer runs, or that
  is older than 24 hours, is removed the same way.
- Disk: one worktree holds a checkout plus `bin/` and `obj/` of the built projects. Both worktrees
  exist at the same time only while the merge-base runs. Before creating a worktree, triage checks
  free space on the temp volume against twice the size of the tracked files at that commit
  (`git ls-tree -r -l` sum); less → error evidence `rerun.noDiskSpace` and no runs.

### 9.4 Build, run, record

Tests are grouped by `(ProjectPath, Runner)`. For each group, in each worktree:

1. **Build once**: `dotnet build <ProjectPath> -c Debug --nologo -v:q`, working directory the
   worktree, limit `--rerun-timeout`. A non-zero exit → evidence `rerun.buildFailed` with the last 40
   lines of output (`data.logTail`, scrubbed), on every test of the group. No runs for that group in
   that worktree.
2. **Run `N` times**, one after another:
   - VSTest: `dotnet test <ProjectPath> -c Debug --no-build --filter <filter>`
   - MTP: `dotnet run --project <ProjectPath> -c Debug --no-build -- --filter <filter>`
   `<filter>` is `FullyQualifiedName=<escaped>` joined by `|`, built by the `ReproCommand` filter
   builder (§13), so D3 of `cli-repro-spec.md` and its amendments apply unchanged, for NUnit, MSTest,
   xUnit v2 and xUnit v3 on both runners. `--no-build` is safe here because triage chose the
   configuration in step 1 (the reason Amendment 4D dropped it does not apply). Names are chunked at
   40 per invocation; several chunks are several processes per run.
   The exit code of the test process is not read as a result: a failed test gives a non-zero exit.
   A timeout kills the process tree and gives evidence `rerun.timedOut`; runs completed before it
   count.
3. **Environment of the child process**: inherited, with every `XPING_*`, `MSBUILD*`, `VSTEST_*` and
   `TESTINGPLATFORM_*` variable removed (as `SampleRunner.RunProcessAsync` does), then:
   `XPING_ENABLED=true`, `XPING_MODE=LocalOnly`, `XPING_LOCAL_STORE=<rerun store>`. `LocalOnly` means
   a rerun never uploads, whatever key the shell holds. `XPING_*` variables win over file
   configuration on every path (`AGENTS.md`, Architecture rules).
4. **Rerun store** (`RerunStore`): `<store root>/triage/reruns/<full sha>/`, where `<store root>` is
   the developer's store root (`ILocalSessionStore.StorePath`). It is a sibling of `sessions/`, so no
   report window ever reads it, and the store's own `.gitignore` hides it. The SDK in the child
   process writes `sessions/` inside it and applies its normal retention there.
   **The directory is the tag**: a session read from `…/reruns/<sha>/` is a strict observation at
   `<sha>`, whatever its own properties say.
5. **Read**: after each run, `LocalSessionStore.Create(storePath: <rerun store>)` and `ReadEach`;
   sessions whose id was not present before the run are that run's sessions. A subject with no
   execution in them was not run (wrong filter, test removed): at the head that is evidence
   `rerun.notRun`; at the merge-base, after `N` runs with none, it is `absentAtBase`.

Rerun sessions from earlier invocations for the same commit are read too and count as strict
observations at that commit. `rerun.head` then reports the total and `data.fromEarlierRuns`.

### 9.5 Order, early stop, parallelism, time

- Order: head worktree first; merge-base only for subjects with `headPasses = 0` and at least one
  head failure after the head runs.
- Early stop at the head: when every subject of a group has both a pass and a fail (R1 is decided
  for each), the remaining runs of that group are skipped.
- No early stop at the merge-base: R2 and R3 need the full count to exclude a flip there.
- Parallelism: none. One process at a time. Parallel builds or runs change timing, and timing is
  what an unstable test depends on.
- Time: at most `groups × (1 build + N runs) × 2` invocations, each limited by `--rerun-timeout`.
  Progress goes to stderr, one line per build and per run:
  `rerun head 2/5 tests/Shop.Tests/Shop.Tests.csproj … 1 failed, 0 passed (14 s)`.
- In JSON mode nothing but the envelope reaches stdout; child process output is captured and
  discarded, except the build log tail.

### 9.6 What `--rerun` adds to the envelope

`sources.rerun` (`requested`, `head` and `mergeBase` blocks with `sha`, `runs`, `groups`,
`buildFailed`), the evidence lines `rerun.*`, and `RerunFacts` for the classifier. The exit code
follows the classification after rerun.

---

## 10. Output

The JSON contract is designed first; the text and summary forms are views of the same
`TriageEnvelope`. As in `report`, every sentence and every count is resolved by
`TriageEnvelopeBuilder`; renderers choose layout only.

### 10.1 Rules for all formats

- Deterministic: the same inputs give the same bytes, except `generatedAt` and Cloud's `fetchedAt`.
- stdout holds the result only. Everything else goes to stderr (D15).
- Recorded strings (test names, messages, paths) are printed in text mode through
  `TerminalText.Printable` with the resolved charset. JSON carries them as recorded, escaped by
  `ReportJsonOptions`.
- Order of failures: next step (`investigate` with `causedByChange: true`, `investigate` with
  `false`, `inconclusive`, `rerun`), then assembly (ordinal), then fully qualified name (ordinal),
  then fingerprint.
- Shell quoting of every printed command: `ShellQuote.Of` with `ShellDialects.Resolve`, as `report`.

### 10.2 Versioning of the JSON contract

`schemaVersion` is `"1.0"`, held in `TriageEnvelope.CurrentSchemaVersion`. It is independent of the
report envelope's version.

- The **minor** number increases when a member, an enum value, an evidence code or a warning code is
  added. Consumers ignore unknown members, unknown evidence codes and unknown warning codes.
- The **major** number increases when a member is removed or renamed, a type changes, or the meaning
  of `nextStep`, `observation`, `causedByChange`, `status` or an exit code changes.
- Nulls are written, never omitted, so "no value" differs from "member removed".
- Property order is declaration order and is part of the contract, as in `ReportEnvelope`.
- Enum values are camelCase strings: `nextStep.value` is `rerun`, `investigate` or `inconclusive`.
- The envelope's second member is `kind: "triage"`, so a script can tell it from a report envelope
  (`scope`) before reading further.

A golden file per example of §10.4 and a schema-shape test guard the contract (§14).

### 10.3 Schema

```jsonc
{
  "schemaVersion": "1.0",
  "kind": "triage",
  "status": "triaged",              // triaged | noFailures | noTestFailures | notReady | nothingToTriage | providerAuth | providerFailure
  "exitCode": 21,                   // the code the process returns
  "generatedAt": "2026-11-22T09:14:07Z",
  "error": null,                    // ErrorDto when status is not triaged / noFailures / noTestFailures (§10.6)

  "changeRequest": {                // null only when it could not be resolved
    "provider": "github",           // github | gitlab | azureDevOps
    "serverUrl": "https://github.com",
    "repository": "acme/shop",
    "number": 482,
    "url": "https://github.com/acme/shop/pull/482",
    "state": "open",                // open | closed | merged
    "headSha": "4f2a9c1e…40 hex",
    "headRef": "feature/checkout",
    "baseRef": "main",
    "baseSha": "b7710aa…",
    "mergeBaseSha": "71c0d3e…",     // null when unknown
    "isFork": false
  },

  "run": {                          // null when no run was read
    "complete": true,               // false while any run for the head is queued or running
    "runs": [
      {
        "id": "11873355021",
        "name": "Test Pipeline",
        "url": "https://github.com/acme/shop/actions/runs/11873355021",
        "testedSha": "9d81b77…",    // null when no session of the run names it
        "attempts": [
          { "number": 1, "status": "completed", "result": "failed", "url": "…/attempts/1" },
          { "number": 2, "status": "completed", "result": "failed", "url": "…/attempts/2" },
          { "number": 3, "status": "completed", "result": "failed", "url": "…/attempts/3" }
        ]
      }
    ],
    "jobs": [                       // failed jobs of the latest attempt of each run
      {
        "runId": "11873355021", "attempt": 3, "jobAttempt": null,
        "name": "test", "result": "failed", "url": "…/job/3310",
        "store": "read"             // read | missing | expired | tooLarge | unreadable
      }
    ],
    "rerun": {                      // how to re-run the failed jobs in CI
      "command": "gh run rerun 11873355021 --failed --repo acme/shop",   // null when the provider has no command
      "url": "https://github.com/acme/shop/actions/runs/11873355021",
      "description": "Re-run the failed jobs of Test Pipeline."
    }
  },

  "sources": {
    "ci":          { "status": "ok", "provider": "github", "tokenSource": "gh" },
    "ciArtifacts": { "status": "ok", "read": 3, "fromCache": 1, "sessions": 3, "unreadable": 0, "missing": 0, "expired": 0 },
    "localStore":  { "status": "ok", "path": "/src/shop/.xping", "sessions": 12 },   // ok | unavailable | empty
    "diff":        { "status": "ok", "files": 14 },                                  // ok | unavailable
    "cloud":       { "status": "ok", "reason": "none", "credential": "login", "workspace": "Acme",
                     "cloudUrl": "https://app.xping.io", "elapsedMs": 812,
                     "digestsRequested": 3, "digestsRead": 3, "runsRequested": 3, "runsRead": 3 },
                   // status: ok | partial | unavailable | login-required | forbidden | no-project | not-attempted
    "rerun":       { "status": "not-requested", "requested": null, "head": null, "mergeBase": null }
                   // status: not-requested | completed | partial | failed
                   // head / mergeBase: { "sha": "…", "runs": 5, "groups": 1, "buildFailed": false }
  },

  "summary": {
    "failed": 3, "rerun": 1, "investigate": 1, "inconclusive": 1,
    "causedByChange": 1, "notCausedByChange": 0,
    "passedOnLaterAttempt": 0,
    "changedFiles": 14
  },

  "failures": [ /* FailureDto, see below */ ],

  "resolvedTests": [                // failed in an earlier attempt, passed in the latest; at most 25
    { "fullyQualifiedName": "…", "fingerprint": "…", "assembly": "…" }
  ],

  "truncated": { "shown": 3, "total": 3, "command": null },   // command: the call that shows all

  "verify": {
    "commands": [ "dotnet test tests/Shop.Tests/Shop.Tests.csproj -e XPING_ENABLED=false --filter \"FullyQualifiedName=Shop.Tests.CheckoutTests.Total_IncludesTax\"" ],
    "expectation": "Every command exits 0."
  },

  "warnings": [ { "code": "W-FORK", "message": "…" } ]
}
```

`FailureDto`:

```jsonc
{
  "test": {
    "fullyQualifiedName": "Shop.Tests.CheckoutTests.Total_IncludesTax",
    "displayName": "Total_IncludesTax(country: \"DE\")",
    "fingerprint": "a3f5e9c2…64 hex",
    "shortFingerprint": "a3f5e9c2",       // TestResolver.ShortFingerprintOf over the subjects
    "assembly": "Shop.Tests",
    "framework": "xUnit",                 // Xping.TestFramework, or null
    "runner": "VSTest",                   // Xping.Runner, or null
    "projectPath": "tests/Shop.Tests/Shop.Tests.csproj"   // or null
  },
  "failure": {
    "source": "ciArtifact",               // ciArtifact | cloud
    "outcome": "failed",                  // failed | timeout
    "kind": "assertion",                  // D8
    "site": "testBody",                   // FailureSite, or null
    "exceptionType": "Xunit.Sdk.EqualException",
    "message": "Assert.Equal() Failure: Values differ\nExpected: 119.00\nActual:   100.00",
    "messageTruncated": false,
    "assertion": { "expected": "119.00", "actual": "100.00" },    // or null
    "stack": "   at Shop.Tests.CheckoutTests.Total_IncludesTax(String country) in /home/runner/work/shop/shop/tests/Shop.Tests/CheckoutTests.cs:line 88",
    "stackTruncated": false,
    "location": {                         // or null
      "file": "tests/Shop.Tests/CheckoutTests.cs", "line": 88,
      "from": "failureFrame"              // failureFrame | testDefinition
    },
    "durationMs": 412,
    "timeoutBudgetMs": null,
    "retries": { "attempts": 1, "maxRetries": 0, "outcomes": ["failed"] },
    "signature": "5d0c1a77e2b9f014",      // FailureSignature.Hash
    "errorMessageHash": "…64 hex or null",
    "job": { "runId": "11873355021", "attempt": 3, "name": "test", "url": "…" },
    "sessionId": "0b0e2a52-6a2d-4c58-9d2b-1f0c4a3e7a11",
    "testedSha": "9d81b77…"
  },
  "nextStep": {
    "value": "investigate",               // rerun | investigate | inconclusive
    "sentence": "Fails 8 of 8 times at the head and passes at the merge-base. This change is the likely cause.",
    "rule": "R3",
    "observation": "failsOnlyWithChange", // flippedOnIdenticalCode | failsOnlyWithChange | newTestFails | failsAtMergeBase | undetermined
    "causedByChange": true                // true | false | null
  },
  "evidence": [
    { "code": "rerun.head", "source": "rerun", "decisive": true,
      "text": "Rerun: failed 5 of 5 at head 4f2a9c1",
      "data": { "sha": "4f2a9c1e…", "runs": 5, "failed": 5, "passed": 0, "fromEarlierRuns": 0 } }
  ],
  "rerunCommand": "gh run rerun 11873355021 --failed --repo acme/shop",   // or null
  "reproCommand": "dotnet test tests/Shop.Tests/Shop.Tests.csproj -e XPING_ENABLED=false --filter \"FullyQualifiedName=Shop.Tests.CheckoutTests.Total_IncludesTax\"",   // or null
  "triageCommand": null,                  // on inconclusive: "xping triage 482 --rerun 5 --test a3f5e9c2"
  "changedFiles": [                       // investigate and inconclusive only; else []
    { "path": "src/Shop/Pricing/TaxCalculator.cs", "status": "modified",   // added | modified | deleted | renamed
      "relation": "referencedProject",    // stackFrame | testSource | testProject | referencedProject
      "reason": "In src/Shop/Shop.csproj, which the test project references." }
  ],
  "changedFilesTotal": 1,
  "lastPassed": {                         // or null
    "sha": "71c0d3e…", "at": "2026-11-19T16:02:11Z",
    "source": "rerun",                    // ciArtifact | rerun | cloud | localStore
    "strict": true
  },
  "cloud": {                              // null when Cloud was not read for this test
    "digest": { /* CloudDigestDto of the report contract */ },
    "match": "matched",                   // matched | unseen | unsignable | unknown
    "signature": { /* CloudSignatureDto, or null */ },
    "runs": { "atHead": { "passed": 0, "failed": 2 }, "atMergeBase": { "passed": 4, "failed": 0 }, "hasMore": false }   // or null
  }
}
```

### 10.4 Examples

One per next step, as they appear in `failures`. Members equal to the schema sample above are
shortened with `…`; the goldens hold them in full.

**`rerun`** — three CI attempts on one tested commit:

```json
{
  "test": { "fullyQualifiedName": "Shop.Tests.CartTests.AddItem_UpdatesTotal", "displayName": "AddItem_UpdatesTotal",
            "fingerprint": "c41d…", "shortFingerprint": "c41d0e7a", "assembly": "Shop.Tests",
            "framework": "xUnit", "runner": "VSTest", "projectPath": "tests/Shop.Tests/Shop.Tests.csproj" },
  "failure": { "source": "ciArtifact", "outcome": "timeout", "kind": "timeout", "site": "testBody",
               "exceptionType": null, "message": "Test execution timed out after 5000 milliseconds",
               "messageTruncated": false, "assertion": null, "stack": null, "stackTruncated": false,
               "location": { "file": "tests/Shop.Tests/CartTests.cs", "line": 41, "from": "testDefinition" },
               "durationMs": 5003, "timeoutBudgetMs": 5000,
               "retries": { "attempts": 1, "maxRetries": 0, "outcomes": ["timeout"] },
               "signature": "91aa03f2c7d05b6e", "errorMessageHash": "…",
               "job": { "runId": "11873355021", "attempt": 3, "name": "test", "url": "…" },
               "sessionId": "…", "testedSha": "9d81b77…" },
  "nextStep": { "value": "rerun", "sentence": "Passed and failed on the same commit. Re-run the failed jobs.",
                "rule": "R1", "observation": "flippedOnIdenticalCode", "causedByChange": null },
  "evidence": [
    { "code": "ci.attempts", "source": "ci", "decisive": true,
      "text": "CI: failed in attempts 1 and 3, passed in attempt 2 of Test Pipeline (same commit 9d81b77)",
      "data": { "runId": "11873355021", "testedSha": "9d81b77…", "attempts": [ { "number": 1, "outcome": "failed" }, { "number": 2, "outcome": "passed" }, { "number": 3, "outcome": "failed" } ] } },
    { "code": "failure.kind", "source": "ci", "decisive": false, "text": "Failure kind: timeout", "data": { "kind": "timeout" } },
    { "code": "cloud.flaky", "source": "cloud", "decisive": false,
      "text": "Xping Cloud: flaky (major), confidence 0.62 (adequate)",
      "data": { "state": "flaky", "severity": "major", "score": 0.62, "evidenceLevel": "adequate" } }
  ],
  "rerunCommand": "gh run rerun 11873355021 --failed --repo acme/shop",
  "reproCommand": "dotnet test tests/Shop.Tests/Shop.Tests.csproj -e XPING_ENABLED=false --filter \"FullyQualifiedName=Shop.Tests.CartTests.AddItem_UpdatesTotal\"",
  "triageCommand": null, "changedFiles": [], "changedFilesTotal": 0,
  "lastPassed": { "sha": "9d81b77…", "at": "2026-11-20T10:31:40Z", "source": "ciArtifact", "strict": true },
  "cloud": { "digest": { "…": "…" }, "match": "matched", "signature": { "…": "…" }, "runs": null }
}
```

The test is a subject because the latest attempt (3) failed (D4). Attempt 2 passed on the same
tested commit, which is the flip.

**`investigate`** — the `FailureDto` shown in §10.3 (R3, after `--rerun 5`), with evidence:

```json
[
  { "code": "rerun.head", "source": "rerun", "decisive": true, "text": "Rerun: failed 5 of 5 at head 4f2a9c1", "data": { "…": "…" } },
  { "code": "rerun.mergeBase", "source": "rerun", "decisive": true, "text": "Rerun: passed 5 of 5 at merge-base 71c0d3e", "data": { "…": "…" } },
  { "code": "ci.attempts", "source": "ci", "decisive": true, "text": "CI: failed in 3 of 3 attempts of Test Pipeline", "data": { "…": "…" } },
  { "code": "diff.referencedProject", "source": "git", "decisive": false, "text": "This change modifies 1 files in src/Shop/Shop.csproj, which the test project references", "data": { "project": "src/Shop/Shop.csproj", "files": 1 } },
  { "code": "failure.kind", "source": "ci", "decisive": false, "text": "Failure kind: assertion", "data": { "kind": "assertion" } }
]
```

**`investigate`, not caused by the change** (R2):

```json
"nextStep": { "value": "investigate",
              "sentence": "Fails at the merge-base too. This change did not cause it. Fix it on main, or update your branch if main already has a fix.",
              "rule": "R2", "observation": "failsAtMergeBase", "causedByChange": false }
```

**`inconclusive`** — one CI failure, no other strict observation:

```json
{
  "test": { "fullyQualifiedName": "Shop.Tests.InventoryTests.Reserve_DecrementsStock", "…": "…", "shortFingerprint": "7be20c11" },
  "failure": { "source": "ciArtifact", "outcome": "failed", "kind": "network", "…": "…" },
  "nextStep": { "value": "inconclusive",
                "sentence": "Not enough evidence to decide. Run: xping triage 482 --rerun 5 --test 7be20c11",
                "rule": "R5", "observation": "undetermined", "causedByChange": null },
  "evidence": [
    { "code": "ci.attempts", "source": "ci", "decisive": false, "text": "CI: failed in 3 of 3 attempts of Test Pipeline", "data": { "…": "…" } },
    { "code": "local.history", "source": "local", "decisive": false, "text": "Local store: 3 passed, 0 failed at 4f2a9c1 (working tree state unknown)", "data": { "sha": "4f2a9c1e…", "passed": 3, "failed": 0 } },
    { "code": "diff.none", "source": "git", "decisive": false, "text": "This change touches no file related to the test", "data": {} },
    { "code": "failure.kind", "source": "ci", "decisive": false, "text": "Failure kind: network", "data": { "kind": "network" } }
  ],
  "rerunCommand": "gh run rerun 11873355021 --failed --repo acme/shop",
  "reproCommand": "dotnet test tests/Shop.Tests/Shop.Tests.csproj -e XPING_ENABLED=false --filter \"FullyQualifiedName=Shop.Tests.InventoryTests.Reserve_DecrementsStock\"",
  "triageCommand": "xping triage 482 --rerun 5 --test 7be20c11",
  "changedFiles": [], "changedFilesTotal": 0, "lastPassed": { "sha": "4f2a9c1e…", "at": "…", "source": "localStore", "strict": false },
  "cloud": null
}
```

### 10.5 Bounds and truncation

All limits are constants in `TriageConstants`. Truncation is always visible.

| What | Limit | Marker |
|---|---|---|
| Failures written in full | `--top`, default 25; `--all` removes it | `truncated.shown < truncated.total`; `truncated.command` is the same call with `--all`. Counts in `summary` always cover every failure. The exit code is computed over every failure, not the shown ones |
| `failure.message` | 2 000 characters | text ends with `...[truncated <n> chars]`; `messageTruncated: true` |
| `failure.stack` | 40 lines and 6 000 characters, counted from the top | last line `...[truncated <n> lines]`; `stackTruncated: true` |
| `retries.outcomes` | 10 | `attempts` holds the real count |
| `evidence` per failure | 20 lines | the last line has code `evidence.truncated` and `data.omitted` |
| `changedFiles` per failure | 20 | `changedFilesTotal` holds the real count |
| `resolvedTests` | 25 | `summary.passedOnLaterAttempt` holds the real count |
| `data.logTail` of `rerun.buildFailed` | 40 lines, 4 000 characters | same marker as the stack |
| `warnings` | 50 | the last has code `W-MORE` |

Truncation cuts on a character boundary, never inside a surrogate pair. With the defaults, an
envelope of 25 failures stays under about 400 KB.

### 10.6 Errors in each format

A failure of steps 1–8 (§3.1) is a `TriageFailure(code, exitCode, message, nextStep)`.

- **Text and summary**: nothing on stdout. stderr gets two lines: `xping triage: <message>` and the
  next step. Example:
  ```
  xping triage: No Xping store was uploaded by the failed jobs of run 11873355021.
  Add the upload step to the workflow: https://docs.xping.io/guides/pull-requests/triage-ci-setup.html#github-actions
  ```
- **JSON**: stdout gets a full envelope with `status` set, `failures: []`, every part that was read
  (`changeRequest`, `run`, `sources`), and

  ```json
  "error": { "code": "store-not-uploaded", "message": "No Xping store was uploaded by the failed jobs of run 11873355021.",
             "nextStep": "Add the upload step to the workflow.", "docsUrl": "https://docs.xping.io/guides/pull-requests/triage-ci-setup.html#github-actions" }
  ```
  stderr gets the same two lines.

Error codes (`error.code`): `not-a-repository`, `git-missing`, `no-remote`, `provider-not-detected`,
`change-request-not-found`, `change-request-ambiguous`, `no-run`, `run-in-progress`,
`store-not-uploaded`, `store-expired`, `provider-token-missing`, `provider-unauthorized`,
`provider-scope`, `provider-rate-limited`, `provider-unreachable`, `provider-invalid-response`,
`test-not-found`, `test-ambiguous`.

Usage errors (2), crashes (70) and cancellation (130) print no envelope, as in every command.

Warnings are one line each on stderr, `warning: <message>`, and are also in `warnings[]`. Codes:
`W-FORK`, `W-HEAD-DIFFERS`, `W-NO-MERGE-BASE`, `W-RUN-IN-PROGRESS`, `W-RUN-CANCELLED`,
`W-NO-TEST-FAILURES`, `W-STORE-MISSING`, `W-ARTIFACT-EXPIRED`, `W-ARTIFACT-TOO-LARGE`,
`W-ARTIFACT-MISMATCH`, `W-PAGING`, `W-SOURCE-CONFLICT`, `W-CLOCK`, `W-DIFF-TRUNCATED`,
`W-RERUN-COMMIT`, `W-LOCAL-STORE-UNREADABLE`, `W-MORE`. Cloud problems are hints with the texts of
`report` and appear in `sources.cloud`, not in `warnings`.

### 10.7 Text

Width, charset and colour follow `cli-report-format-spec.md` D7 and D10. Glyphs come from
`ReportGlyphs`. The mockup below is the Unicode form at 100 columns; the goldens are the authority
for spacing.

```
PR #482  feature/checkout → main   head 4f2a9c1   merge-base 71c0d3e
Sources: CI (GitHub, 1 run, 3 attempts) · 3 CI stores · local store · git diff · Xping Cloud

3 failed tests   1 investigate   1 inconclusive   1 re-run

INVESTIGATE  Shop.Tests.CheckoutTests.Total_IncludesTax(country: "DE")
  Shop.Tests · tests/Shop.Tests/CheckoutTests.cs:88 · 412 ms · a3f5e9c2
  Xunit.Sdk.EqualException: Assert.Equal() Failure: Values differ
    Expected: 119.00
    Actual:   100.00
  Evidence
    ✓ Rerun: failed 5 of 5 at head 4f2a9c1
    ✓ Rerun: passed 5 of 5 at merge-base 71c0d3e
    ✓ CI: failed in 3 of 3 attempts of Test Pipeline
    · This change modifies 1 files in src/Shop/Shop.csproj, which the test project references
    · Failure kind: assertion
  Changed files
    M src/Shop/Pricing/TaxCalculator.cs   (in src/Shop/Shop.csproj, which the test project references)
  Last passed: 71c0d3e (rerun)
  Next: Fails 8 of 8 times at the head and passes at the merge-base. This change is the likely cause.
    Repro:  dotnet test tests/Shop.Tests/Shop.Tests.csproj -e XPING_ENABLED=false --filter "FullyQualifiedName=Shop.Tests.CheckoutTests.Total_IncludesTax"

INCONCLUSIVE  Shop.Tests.InventoryTests.Reserve_DecrementsStock
  Shop.Tests · tests/Shop.Tests/InventoryTests.cs:23 · 1.2 s · 7be20c11
  System.Net.Http.HttpRequestException: Connection refused (localhost:5432)
  Evidence
    · CI: failed in 3 of 3 attempts of Test Pipeline
    · Local store: 3 passed, 0 failed at 4f2a9c1 (working tree state unknown)
    · This change touches no file related to the test
    · Failure kind: network
  Next: Not enough evidence to decide. Run: xping triage 482 --rerun 5 --test 7be20c11

RE-RUN  Shop.Tests.CartTests.AddItem_UpdatesTotal
  Shop.Tests · tests/Shop.Tests/CartTests.cs:41 · 5.0 s · c41d0e7a
  Test execution timed out after 5000 milliseconds
  Evidence
    ✓ CI: failed in attempts 1 and 3, passed in attempt 2 of Test Pipeline (same commit 9d81b77)
    · Failure kind: timeout
    · Xping Cloud: flaky (major), confidence 0.62 (adequate)
  Next: Passed and failed on the same commit. Re-run the failed jobs.
    Re-run: gh run rerun 11873355021 --failed --repo acme/shop

Verify after a fix:
  dotnet test tests/Shop.Tests/Shop.Tests.csproj -e XPING_ENABLED=false --filter "FullyQualifiedName=Shop.Tests.CheckoutTests.Total_IncludesTax"
```

Rules:

- `✓` marks a decisive line, `·` a hint. ASCII: `*` and `-`.
- The message shows at most 6 lines in text mode; the stack is not printed in text mode (the JSON has
  it). `--verbose` does not change stdout.
- The `Sources:` line names only sources that were read. A source that failed is named on stderr.
- No failed tests: one line, `PR #482  head 4f2a9c1: no failed tests.`, and, when
  `passedOnLaterAttempt > 0`, a second: `2 tests failed in an earlier attempt and passed in the latest.`
- With `--top` cutting the list: a last line `Showing 25 of 61. Run: xping triage 482 --all`.

### 10.8 Summary

One line on stdout:

```
PR #482 @ 4f2a9c1: 3 failed tests: 1 investigate, 1 inconclusive, 1 re-run
PR #482 @ 4f2a9c1: no failed tests
```

Zero counts are left out. No colour, no glyph outside ASCII.

---

## 11. Skill contract

What the future `xping-triage` skill (in `xping-dev/skills`) may rely on. Everything here is covered
by tests named in §14, so a change here is a contract change (§10.2).

**Call**

```bash
xping triage [<pr>] --json            # never prompts, never opens a browser, never writes to the PR
```

Optional: `--rerun <N>`, `--test <name|fingerprint>`, `--no-cloud`, `--provider`, `--repo`, `--all`.

**Inputs the skill must provide**: a working directory inside the repository; a provider token in the
environment or a signed-in provider CLI (§5); for `--rerun`, `git` and `dotnet` on PATH.

**Outputs**

- stdout: exactly one JSON document (`kind: "triage"`), or nothing for exit 2, 70 and 130.
- stderr: human text only. The skill does not parse it.
- Exit code: D14. The skill branches on the exit code first and reads `status` and `error.code` for
  detail.

**Guarantees**

1. Every failed test of the latest attempts is counted in `summary`, and is in `failures` unless
   `truncated.shown < truncated.total`.
2. `nextStep.value` is one of three values. `rerun` is given only on a pass and a fail on identical
   code. `investigate` is given only on repeated strict failures and strict merge-base results.
3. `causedByChange` is `true`, `false` or `null`, and is `null` exactly when the evidence does not
   say.
4. Every `evidence[]` entry with `decisive: true` is one the rule in `nextStep.rule` used.
5. Nothing outside a `cloud` member, and no evidence line with a source other than `cloud`, comes
   from Xping Cloud. Triage output contains no local score and no local flaky verdict.
6. `reproCommand`, `rerunCommand`, `triageCommand` and `verify.commands[]` are complete shell
   commands for the shell `XPING_SHELL` names (default by OS), runnable from the repository root.
   A null means "no command exists", never "unknown".
7. The same inputs give the same output, apart from timestamps.

**The loop**

```
1. xping triage --json
     exit 0   → done: no failed tests.
     exit 22  → wait, then step 1.
     exit 23, 24, 25 → report error.message and error.nextStep to the user. Stop.
     exit 20  → every failure is rerun: run run.rerun.command (or ask the user to open run.rerun.url). Stop or wait and step 1.
     exit 21  → step 2.
2. For each failure:
     rerun        → covered by run.rerun.
     inconclusive → run triageCommand once (it contains --rerun). Read the new envelope. If still inconclusive, report the evidence.
     investigate, causedByChange = false → do not change this PR's code for it. Report: fails on the base branch.
     investigate, causedByChange = true  → step 3.
3. Fix: read failure.location, failure.message, failure.assertion, failure.stack, changedFiles (in order), lastPassed.sha
   (`git diff <lastPassed.sha> <headSha> -- <path>` is the change to read).
4. Repro: run reproCommand before the fix (expect non-zero) and after it (expect 0).
5. Verify: run every verify.commands[] entry. All exit 0 → commit and push.
6. After CI finishes: step 1.
```

The skill must not: compute a flaky verdict or a score from this output; treat `cloud.digest`
`confidence.score` as a decision (see `cloud-data-in-cli-cli-spec.md` §13); re-run CI on
`investigate`.

---

## 12. Required SDK and CI changes

### 12.1 SDK: record the CI attempt and job (`src/Xping.Sdk.Core`)

In `EnvironmentDetector`, the `switch` on `CIPlatform` gains these properties. They are additive
custom properties; no model type and no configuration option changes.

| Property | GitHub Actions | GitLab CI | Azure Pipelines |
|---|---|---|---|
| `CI.RunAttempt` | `GITHUB_RUN_ATTEMPT` | — | `SYSTEM_STAGEATTEMPT` |
| `CI.JobAttempt` | — | — | `SYSTEM_JOBATTEMPT` |
| `CI.JobName` | `GITHUB_JOB` | `CI_JOB_NAME` | `SYSTEM_JOBNAME` |
| `CI.StageName` | — | `CI_JOB_STAGE` | `SYSTEM_STAGENAME` |
| `CI.JobId` | — | (exists) | `SYSTEM_JOBID` |

Run identity used by triage, per provider, from what the SDK records: GitHub `CI.RunId` +
`CI.RunAttempt`; GitLab `CI.PipelineId` + `CI.JobId`; Azure DevOps `CI.BuildId` + `CI.RunAttempt` +
`CI.JobAttempt`. `SessionCommit` and a sibling `SessionRun.Of(session)` in `Triage/Evidence/` are the
only readers.

Also in the same PR: the "Captured Metadata" tables of `docs/getting-started/ci-cd-setup.md`, and
tests in `tests/Xping.Sdk.Core.Tests` beside the existing `EnvironmentDetector` tests.

Sessions recorded by an SDK without these properties still work: the artifact name supplies the
attempt (D10).

**The SDK already writes the store in CI** (§1.3). Nothing changes there.

### 12.2 CI pipeline changes, per provider

Each snippet uploads `.xping/sessions/` once per job attempt, also when the job failed. These are the
ready-to-copy snippets for the docs (§17.3).

**GitHub Actions**

```yaml
    - name: Test
      run: dotnet test --configuration Release

    - name: Upload Xping store
      if: always()
      uses: actions/upload-artifact@v4
      with:
        name: xping-store-${{ github.job }}-attempt-${{ github.run_attempt }}
        path: .xping/sessions/
        include-hidden-files: true
        if-no-files-found: ignore
        retention-days: 14
```

In a matrix job the name needs the matrix index:
`xping-store-${{ github.job }}-${{ strategy.job-index }}-attempt-${{ github.run_attempt }}`.

**GitLab CI**

```yaml
test:
  script:
    - dotnet test --configuration Release
  artifacts:
    name: "xping-store-$CI_JOB_NAME_SLUG"
    when: always
    paths:
      - .xping/sessions/
    expire_in: 2 weeks
```

A job has one archive. A job that already publishes artifacts adds `.xping/sessions/` to its `paths`
and uses the `xping-store-` name.

**Azure Pipelines**

```yaml
- script: dotnet test --configuration Release
  displayName: Test

- task: PublishPipelineArtifact@1
  displayName: Upload Xping store
  condition: always()
  continueOnError: true
  inputs:
    targetPath: $(Build.SourcesDirectory)/.xping/sessions
    artifact: xping-store-$(System.PhaseName)-$(System.JobPositionInPhase)-attempt-$(System.StageAttempt).$(System.JobAttempt)
```

The attempt numbers in the name are required on Azure DevOps: an artifact name must be unique in a
build, and a re-run of a stage would otherwise fail to publish.

Notes for all three: the store path is `.xping/sessions/` under the checkout root unless
`XPING_LOCAL_STORE` is set, in which case the path is `$XPING_LOCAL_STORE/sessions/`; the test step
must not set `XPING_MODE=Disabled`; no API key is needed.

---

## 13. Changes to existing internal APIs

No compatibility is kept (`AGENTS.md`). Every change to code that exists today:

| # | Change | Why | Phase |
|---|---|---|---|
| I1 | `ReportFormat` → `OutputFormat`, moved to `Commands/OutputFormat.cs`. `ReportOptions.Format` and `Program.BuildReportCommand` follow | Shared by `report` and `triage` (X9) | P1 |
| I2 | `Program`: the format options and their validators move to a private helper `FormatOptions` used by both commands; new `BuildTriageCommand`; `triage` added to `BuildRootCommand` | One definition of `--format`, `--json`, `--summary` | P1 (helper), P3 (command) |
| I3 | `ReproCommand`: `Command(names, runner, projectPath, shell)` and `Escape` become one public method `ReproCommand.ForTests(IReadOnlyList<string> fullyQualifiedNames, ReproTarget target, ShellDialect shell, bool record = false)` returning the command or null; plus `ReproCommand.FilterFor(names, runner)` for §9.4. `For(Finding, …)` calls them. `record: true` omits `-e XPING_ENABLED=false` | Triage builds repro and verify commands from names, and rerun needs the filter (X8) | P3 |
| I4 | `CloudBudget`: constructor gains `TimeSpan? total = null`; `Total` stays the default. `CloudApiClientFactory` sets `HttpClient.Timeout` from the budget instance instead of `CloudHttp.TotalTimeout` | Triage budget of 10 s (§8.3) | P6 |
| I5 | `CloudEnricher`: `CredentialFailure`, `IsReadFailure`, `Classify`, `Unavailable` and the reason texts move to `internal static class CloudFailures` (`Cloud/CloudFailures.cs`). `CloudEnricher` calls it. No behaviour change | Shared fail-open rules (§8.3) | P6 |
| I6 | `Redaction`: `HeaderValue` also matches `PRIVATE-TOKEN` and `JOB-TOKEN`; a new pattern scrubs provider token shapes: `gh[pousr]_[A-Za-z0-9]{20,}`, `github_pat_[A-Za-z0-9_]{20,}`, `glpat-[A-Za-z0-9_-]{20,}` | Provider tokens in error text (§18) | P1 |
| I7 | `XpingHome`: new `TriageCacheDirectory` (`cache/triage`) | Artifact cache (§6.5) | P2 |
| I8 | `TestResolver.ShortFingerprintOf`: usable over a list of `(assembly, fingerprint)` pairs, not only a store read | `shortFingerprint` of subjects | P3 |
| I9 | `AGENTS.md`: `xping triage` in "What this is" and "Layout"; the `JsonDocument` exception for provider responses (D15); the triage JSON options beside `ReportJsonOptions` | Agent instructions stay true | P2, P3 |
| I10 | `EnvironmentDetector`: §12.1 | Attempt identity | P1 |
| I11 | `docs/cli/command-reference.md` exit-code table and `nuspec/README.Cli.md` | New command | P9 |

`TriageJsonRenderer` uses `ReportJsonOptions.Default`. No second options object is created.

---

## 14. Testing

Conventions of the repository apply: xUnit, hand-written fakes in the CLI tests, one test class per
production class, mirrored folders, `TimeProvider` and `IEnvironmentVariableProvider` for time and
environment, no network, no real environment variables. Test names are behaviour sentences or
`Method_Scenario_Expected`.

### 14.1 Unit tests

| Area | Class | What is asserted |
|---|---|---|
| Decision table | `ClassifierTests` | One `[Theory]` row per rule R1–R5 and per note of §7.4: flip on one tested commit; in-run flip; same head with different merge commits is not R1; rerun passes at head with CI failure at a merge commit is R5 with `rerun.passesAtHeadOnly`; R1 wins over R2; one base failure is R2; `flip(B)` is R5; one head failure with base passes is R5; two are R3; absent at base is R4; non-strict observations never decide; a Cloud flaky digest never decides; timeouts count as failures. Each row asserts `NextStep`, `Observation`, `CausedByChange`, `Rule` and the decisive codes |
| Identical code | `SessionCommitTests`, `CodeIdTests` | The four cases of §7.2; `headOnly` rules of D5 |
| Merge | `EvidenceMergerTests` | Each of the seven rules of §7.3, including source precedence for one session id and `W-SOURCE-CONFLICT` |
| Subjects | `SubjectSelectorTests` | Latest attempt only; passed on a later attempt goes to `resolvedTests`; one subject for two failing jobs; skipped tests are never subjects; order of the chosen failure block |
| Remote URLs | `RemoteUrlParserTests` | One row per form of §4.2, with and without `.git`, with user info, with ports and path prefixes, mixed case hosts; every `<pr>` URL form; every `--repo` form; the detection order; rejects |
| Artifact names | `ArtifactNamingTests` | Build and parse, both directions, all three providers; names without a suffix; names that only look similar |
| Zip reading | `ArtifactStoreReaderTests` | Entries outside `sessions/`; entries with `../`; entry and archive limits; unreadable sessions counted; nested directories |
| Cache | `ArtifactCacheTests` | Hit, miss, 7-day prune and size prune on a fake clock; two writers |
| Tokens | `ProviderTokenResolverTests` | The order of §5.2 per provider with a fake `IProcessRunner`; CLI missing; CLI exits non-zero; token registered with `Redaction`; no token |
| Failure kind | `FailureKindsTests` | Every row of D8 and the order of the four steps |
| Assertions | `AssertionParserTests` | Every sample of `RealFailureSamples` gives the expected pair or null; multi-line values |
| Locations | `StackLocationTests` | Windows and POSIX CI paths mapped by suffix; two candidates give null; no stack |
| Diff relations | `DiffReaderTests`, `ProjectReferenceReaderTests` | Each relation; precedence; rename status; depth and count limits; a cycle of references |
| Envelope | `TriageEnvelopeBuilderTests` | Order of failures and of evidence; every bound of §10.5 with its marker; sentences of §7.5; commands quoted for both shells |
| Exit codes | `TriageExitCodesTests` | Every row of D14; the code is computed over all failures when `--top` hides some |
| Redaction | `RedactionTests` (extended) | The new header names and token shapes; existing cases unchanged |
| Git | `GitClientTests` | Against real fixture repositories built with `git`, as `GitRepositoryReaderTests` does: locate, remote choice, merge-base, diff with renames, worktree add and remove, fetch of a commit from a second local repository |
| SDK | `EnvironmentDetectorTests` (extended) | §12.1 per provider |

### 14.2 Fakes

- `FakeCiProvider : ICiProvider` and `FakeCiProviderFactory`, scripted in code: change requests, runs,
  attempts, jobs, artifacts (built from `TestSessionFactory` sessions and zipped in memory), failures
  by `ProviderFailureKind`, and a record of every call.
- `FakeProcessRunner : IProcessRunner`: scripted results per executable and argument prefix; records
  requests with their environment.
- `FakeGitClient : IGitClient` for command tests that do not need a repository.
- `TriageHost`: the `Program.Run` harness for triage, in the style of `CliFlowHost`: scratch
  `XpingHome`, dictionary environment, fake clock, the fakes above, an optional `FakeCloud`.

### 14.3 Recorded provider responses (new pattern)

- Files: `tests/Xping.Cli.Tests/Triage/Providers/Recorded/<provider>/<scenario>/NN-<name>.json`, each
  holding `{ "request": { "method", "path", "query" }, "response": { "status", "headers", "body" } }`.
  They are read from the source tree, found the way `GoldenReportTests.GoldenPath` finds goldens.
- `RecordedCiServer`: an in-process Kestrel app on `127.0.0.1:0` (the `FakeCloud` approach) that
  answers each request with the recorded response for its method, path and query, records what was
  asked, serves artifact downloads through a redirect to a second path on itself, and fails the test
  on a request that has no recording. It asserts on dispose that no request to the redirect path
  carried a credential header.
- Recordings are made once from real projects, then cleaned: tokens, cookies, signed URLs, user
  names and e-mail addresses are replaced. A test (`RecordingsHoldNoSecrets`) scans every file with
  `Redaction.Scrub` and fails when the text changes.
- Scenarios per provider (each a test in `<Provider>ProviderRecordedTests`): PR by number; PR by
  branch; fork PR; one run, two attempts, second passed; re-queued run on the same head; run in
  progress; no run; expired artifact; missing artifact; paging over two pages; 401; 403 without
  scope; 404; 429 with `Retry-After` under and over the limit; 5xx then success.
- End-to-end per provider (`TriageRecordedFlowTests`): `Program.Run(["triage", "--json", …])` against
  `RecordedCiServer` for three scenarios (flip across attempts → exit 20; single failure → exit 21;
  no failures → exit 0) with the output compared to a golden.

OQ-10 decides where the GitLab and Azure DevOps recordings come from.

### 14.4 Command, output and contract tests

- `TriageCommandTests`: every row of §6.6 and every error code of §10.6 through `TriageHost`, in
  text and JSON, asserting exit code, stdout and stderr; `--test` found, not found (3), ambiguous
  (4); `--top` and `--all`; `--no-cloud`.
- `TriageSurfaceTests`: every validator of D1 returns 2 with its message; `xping triage --help`
  lists every option; nothing is read from stdin.
- Goldens: `tests/Xping.Cli.Tests/Triage/Goldens/<scenario>.{unicode,ascii}.txt`, `.summary.txt` and
  `.json`, with the `XPING_UPDATE_GOLDENS` switch. Scenarios: the four examples of §10.4, no
  failures, truncated list, fork warning, rerun build failure.
- `TriageContractTests`: property names and order of every DTO against a checked-in list;
  `schemaVersion`; every enum value; the JSON document has no ANSI escape and stdout holds exactly
  one JSON value; determinism (two runs, equal bytes with a fixed clock).
- `TriageStreamsTests`: in JSON mode, progress, warnings and hints are on stderr only.
- `DocumentedSampleTests`: extended to the samples of the new docs pages (§17).

### 14.5 Cloud tests (P6)

- `TriageCloudReaderTests` with a scripted `ICloudApiClient`: digest batches; runs for at most
  `MaxCloudRunLookups` subjects in order; rows split into head, merge-base and last pass; `headOnly`
  rows; `HasMore` without a merge-base row omits the line.
- `TriageCloudFailOpenTests` through `TriageHost` and `FakeCloud`: for each of no credential, expired
  sign-in, 403, 429, 5xx, timeout, budget exhausted, unreadable body, no project: the envelope
  without `cloud` members equals the `--no-cloud` envelope, the exit code is equal, one hint line is
  on stderr, and `sources.cloud.status` and `reason` are the expected pair.
- `TriageCloudAttributionTests`: no member outside `cloud` and no evidence line with a non-cloud
  source changes when Cloud answers; every Cloud line starts with `Xping Cloud:`.
- `CloudBudgetTests`, `CloudEnricherTests`, `ExitCodeIndependenceTests` (existing): still pass after
  I4 and I5, unchanged.

### 14.6 Rerun tests (P7)

- `WorktreeManagerTests` on real fixture repositories: add and remove at two commits; the working
  tree, index, branch and `git status` of the repository are byte-equal before and after; removal
  after cancellation; stale sweep by dead pid and by age; a path with spaces.
- `RerunCoordinatorTests` with `FakeProcessRunner`: build once per group and worktree; `N` runs;
  early stop at the head; no early stop at the merge-base; base runs only for tests that failed
  every time at the head; timeout; build failure; child environment (no inherited `XPING_*`,
  `XPING_MODE=LocalOnly`, the rerun store); chunking at 40 names; absent at base.
- `RerunStoreTests`: sessions under `triage/reruns/<sha>/` are strict observations at `<sha>`;
  `xping report` over the same store root does not see them.
- Integration (`tests/Xping.Sdk.Integration.Tests`, `[Collection(SampleRunner.Collection)]`):
  `TriageRerunTests`: a temporary git repository holding a copy of one sample per framework, with a
  commit that breaks a test; `RerunCoordinator` at the head and merge-base, for NUnit, xUnit v2,
  xUnit v3 and MSTest on VSTest and MTP; asserts the observations and that no session was uploaded
  or written to the developer store. These run where the integration project runs (not on PRs).

### 14.7 Budget and performance guards

In the style of the existing guards: fake clock and call counts, no wall time.

| Guard | Assertion |
|---|---|
| Provider calls | For one run with two attempts and three artifacts, GitHub: at most 10 API requests; GitLab: at most 6; Azure DevOps: at most 9. Counted by `RecordedCiServer` |
| Provider budget | Advancing the fake clock past `ProviderTotal` ends reads with `ProviderFailure`, never a hang; `RateLimitMaxWait` is honoured once |
| Concurrency | At most `ProviderConcurrency` requests in flight |
| Downloads | An artifact over `MaxArtifactBytes` is not requested; the total stops at `MaxArtifactTotalBytes` |
| Local store | `LocalHistoryReader` makes one `ReadEach` scan per assembly and no bounded read (`FakeSessionSource.Scans`, `Reads`) |
| Cloud | At most `MaxCloudRunLookups` runs calls; at most `CloudEnricher.MaxConcurrency` in flight; the 10 s budget on the fake clock |
| Output size | 200 failures with 100 KB messages: default output under 400 KB; `--all` output is bounded per failure |
| Cache | A second invocation for the same artifacts makes no download request |

---

## 15. Implementation sequence

Branching follows the repository rule for phased specs: a feature branch `feat/triage`; one branch
and one PR per phase into it; the implementer stops after each PR for review; an amendment to this
document is committed before the code that depends on it. Commits are conventional
(`feat(cli): …`, `feat(sdk): …`) and reference the issue.

Before calling a phase done: `dotnet build -c Release` is clean, `dotnet format --verify-no-changes`
passes, and the affected test projects pass.

Each phase starts with a **P0 re-read**: the implementer re-reads the §1 rows the phase depends on
against the source, and the provider facts of §5–§6 against the provider's current documentation,
and files an amendment for every difference before writing code.

| Phase | Content | Depends on OQ |
|---|---|---|
| **P1** Foundations | I1, I2 (format helper only), I6, I10 (§12.1). `Triage/Processes` (`IProcessRunner`), `Triage/Git` (`IGitClient`, `RepositoryLocator`, `RemoteUrlParser`). No command yet | — |
| **P2** Provider layer + GitHub | `Triage/Providers` models and interfaces, `ProviderHttp`, `ProviderTokenResolver`, `GitHubProvider`, `Triage/Artifacts` (naming, cache, reader), I7, `RecordedCiServer` and the GitHub recordings | OQ-1, OQ-3 |
| **P3** Triage on GitHub, no Cloud, no rerun | `Triage/Evidence`, `Triage/Classification`, `Triage/Contract`, `Triage/Rendering`, `TriageCommand`, `TriageExitCodes`, I2 (command), I3, I8, I9. Sources S1–S4 | OQ-6, OQ-7, OQ-8, OQ-11, OQ-13, OQ-15 |
| **P4** GitLab | `GitLabProvider`, token sources, recordings, re-run instruction | OQ-2, OQ-10 |
| **P5** Azure DevOps | `AzureDevOpsProvider`, token sources, recordings, re-run instruction | OQ-10 |
| **P6** Cloud mode | `TriageCloudReader`, I4, I5, §8.1–§8.5 | OQ-4 |
| **C1** (dashboard, tracked) | §8.6 C1–C4 in `xping-dev/dashboard` | OQ-5 |
| **P6b** Cloud as failure record | Subjects from C1/C2 when a store is missing; C3 removes `headOnly` | C1 deployed |
| **P7** `--rerun` | `Triage/Rerun`, §9, integration tests | — |
| **P8** User documentation | §17 | — |

P4 and P5 are independent of each other and of P6 and P7. P8 can start after P3 for the GitHub parts
and is finished last.

### Acceptance criteria and test lists

**P1**
- `xping report` behaves and prints exactly as before (all existing tests and goldens pass unchanged
  apart from the rename).
- A session recorded on each provider carries the properties of §12.1.
- `GitClient` and `RemoteUrlParser` cover §4.
- Tests: `RemoteUrlParserTests`, `GitClientTests`, `ProcessRunnerTests` (real `true`/`false`-style
  processes as `LauncherProcessTests`, timeout kills the tree, output cap), `RedactionTests`,
  `EnvironmentDetectorTests`.

**P2**
- Given a token and a repository, `GitHubProvider` returns the PR, every run with every attempt and
  job, and the artifacts; downloads through the redirect without sending the token to the second
  host.
- Every failure of §5.1 and §6.0 maps to its `ProviderFailureKind`.
- An artifact archive becomes sessions through `ArtifactStoreReader`, within the limits, and a second
  read comes from the cache.
- Tests: `GitHubProviderRecordedTests`, `ProviderTokenResolverTests`, `ProviderHttpTests` (retry,
  timeout, rate limit, paging on the fake clock), `ArtifactNamingTests`, `ArtifactStoreReaderTests`,
  `ArtifactCacheTests`, `RecordingsHoldNoSecrets`, the provider guards of §14.7.

**P3**
- `xping triage`, `xping triage <n>`, `xping triage <url>`, `--test`, `--top`, `--all`, all three
  formats, on GitHub, with sources S1–S4.
- Every row of §6.6, every exit code of D14 and every error code of §10.6 is reachable and tested.
- The envelope matches §10.3 and the goldens of §10.4; text matches §10.7.
- No output member, sentence or line names a test flaky or carries a score.
- Tests: `ClassifierTests`, `SessionCommitTests`, `EvidenceMergerTests`, `SubjectSelectorTests`,
  `LocalHistoryReaderTests`, `DiffReaderTests`, `ProjectReferenceReaderTests`, `FailureKindsTests`,
  `AssertionParserTests`, `StackLocationTests`, `TriageEnvelopeBuilderTests`, `TriageExitCodesTests`,
  `TriageCommandTests`, `TriageSurfaceTests`, `TriageContractTests`, `TriageStreamsTests`, goldens,
  `TriageRecordedFlowTests` (GitHub), `ReproCommandTests` (extended for I3), the local store and
  output guards of §14.7.

**P4 / P5**
- The same command works on GitLab (including a self-hosted URL, retried jobs, re-run pipelines,
  merged-results pipelines, fork MRs) / Azure DevOps (both URL forms, stage and job re-runs,
  re-queued builds).
- The P3 command tests pass unchanged with the provider swapped.
- Tests: `GitLabProviderRecordedTests` / `AzureDevOpsProviderRecordedTests`, token resolver rows,
  remote URL rows, `TriageRecordedFlowTests` for the provider, provider guards.

**P6**
- With a credential, the envelope gains `cloud` members and Cloud evidence lines; strict Cloud rows
  can decide R1–R3; a Cloud flaky classification never changes a next step.
- Every Cloud failure leaves the non-Cloud output and the exit code unchanged.
- `report` behaves as before after I4 and I5.
- Tests: §14.5.

**P7**
- `--rerun N` produces `rerun.*` evidence and can decide R1–R4.
- The repository's working tree, index, branch and `sessions/` directory are unchanged after a run, a
  failure and a cancellation.
- No rerun uploads to Cloud.
- Tests: §14.6. The integration tests are run before merging (the `AGENTS.md` rule for changes that
  depend on a test runner).

**P8**
- The pages of §17 exist, are in the tables of contents, and `docfx ./docs/docfx.json` builds
  without a new warning.
- Every sample is generated by the renderer and guarded by `DocumentedSampleTests`.
- Tests: `DocumentedSampleTests`.

---

## 16. Open questions — gates

Each has ranked options; the first is the recommendation. The owner decides OQ-3 to OQ-15 before P1
starts, or before the phase named in §15.

### OQ-1 — GitHub: artifacts across attempts, and hidden files. Closed by the P0 of P2

Verify on a real repository: (a) `GET …/actions/runs/{id}/artifacts` returns artifacts uploaded by
earlier attempts of the run; (b) `actions/upload-artifact@v4` needs `include-hidden-files: true` for
`.xping/sessions/`; (c) two attempts can upload the same artifact name or not.
If (a) is false, the earlier attempts' stores are not reachable through that call; the fallback is
`GET /repos/{o}/{r}/actions/artifacts?name=…` filtered by `workflow_run.id`. Amend §6.2.

### OQ-2 — GitLab CLI commands. Closed by the P0 of P4

Verify `glab config get token --host <host>` prints the token for both a personal access token and an
OAuth sign-in, and the syntax of `glab ci retry`. If no command prints the token, GitLab has
environment variables only, and §5.2 and the docs say so.

### OQ-3 — Fallback when no store was uploaded

1. **None. Fail with setup instructions (D10).** One exact record, one code path, full commands.
2. Provider test reports as a degraded mode, where they exist: names and messages only, every test
   `INCONCLUSIVE`, no commands. Three implementations for output that decides nothing.
3. Parse TRX from a conventionally named artifact. Needs its own CI setup step, so it saves the user
   nothing over uploading the store.

### OQ-4 — May a Cloud flaky classification give `RE-RUN` alone?

1. **No (D7).** It is shown, and it changes the sentence only.
2. Yes, when Cloud says flaky **and** the failure's signature is `matched` with `ciRuns ≥ 1` on the
   canonical branch: "Cloud has seen this exact failure on code without your change". This is closer
   to the question, but it turns a history verdict into a next step, and a signature match on the
   message hash is not a pass on identical code.
3. Yes, always. Hides regressions in tests already known as flaky.

### OQ-5 — Cloud contract (§8.6)

For the dashboard owner: accept C1–C4 as proposed or amend them; say whether Cloud stores error
message text; say whether an upload-only API key may call C1/C2 or only a sign-in can. Until this
closes, P6b does not start. P6 does not depend on it.

### OQ-6 — `MinHeadFailures`

1. **2.** One CI failure plus merge-base passes looks the same for a broken and an unstable test.
2. `N` of `--rerun N` only (ignore CI). Stricter, and it discards a strict observation.
3. 1. Fewer `INCONCLUSIVE` results, and wrong `INVESTIGATE` results for unstable tests.

### OQ-7 — Assertion expected/actual

1. **Parse the message for three known formats (D9).** No SDK change; null when unsure.
2. Record them in the SDK (each adapter reads the framework's exception). Exact, but a model and wire
   change in four adapters, and only future sessions have it.
3. Leave them out. The message already holds them.

### OQ-8 — Local store history

1. **Hint only (X2, D5).**
2. Add a `Git.TreeState` property to the SDK (`clean`, `dirty`), computed by running
   `git status --porcelain` once per session, and treat clean local sessions as strict. More
   decisions without `--rerun`, at the cost of a process start in every local test run.
3. Treat local sessions as strict today. Wrong whenever a developer ran with uncommitted edits.

### OQ-9 — Mixed hosting (code on one provider, CI on another)

1. **Out of scope now; the interfaces are split (D3) so it can be added.** A later option
   `--ci-provider` and `--ci-repo` pairs two providers.
2. In scope for Azure Pipelines building a GitHub repository, in P5. A common .NET setup, but it
   doubles the Azure DevOps test matrix.

### OQ-10 — Where the GitLab and Azure DevOps recordings come from

Recorded responses need real projects with a failing pipeline, a retried job and an uploaded store.

1. **The owner provides one sandbox project per provider** (a GitLab.com project and an Azure DevOps
   project) and a read token for the implementer's session. P4 and P5 start when they exist.
2. The implementer writes the responses by hand from the provider documentation. No access needed,
   and no proof that the provider answers that way.
3. Self-hosted GitLab in a container in CI. Covers one provider, slowly.

### OQ-11 — The pre-existing case

1. **Three next steps; R2 is `INVESTIGATE` with `causedByChange: false` (§7.5).**
2. A fourth next step, `NOT-CAUSED-BY-CHANGE`. Clearer for a person at a glance; it changes the
   agreed three-step contract and the exit-code classes.

### OQ-12 — Where this document lives

1. **`specs/cli-triage-spec.md` until approved** (as instructed), then moved to
   `docs/internals/implementation-specs/` with the other CLI specs, in the P1 PR.
2. Stay in `specs/`.

### OQ-13 — `verifyCommand` is a list

The agreed design names one `verifyCommand`. Tests in two projects need two commands; one shell line
joined with `&&` is not portable between posix and PowerShell 5.

1. **`verify.commands[]`, one per test project, with `verify.expectation`.**
2. One string joined with `&&` (posix and PowerShell 7 only).

### OQ-14 — `git fetch` without `--rerun`

To compute the merge-base and the diff when the head or base commit is not local.

1. **Fetch the missing commits (§6.1).** Adds objects and `FETCH_HEAD`; changes no ref and no file.
   `XPING_TRIAGE_NO_FETCH=1` turns it off.
2. Never fetch without `--rerun`; use the provider's merge-base and its changed-files API. Three more
   API shapes, and no file content for project references.

### OQ-15 — CI failed, but no test failed

A build error or a lint job: the stores exist and hold no failure.

1. **Exit 0 with `status: "noTestFailures"`, `run.jobs` listing the failed jobs, and a warning.**
   The code means "no failed tests", which is true.
2. Exit 23 (`NothingToTriage`). A red pipeline never returns 0, but the code then mixes "setup is
   wrong" with "the failure is not a test".
3. A new code, 26. Exact, and one more branch for every consumer.

---

## 17. User documentation plan (P8)

The implementer writes the pages. Language rules: plain English for readers who are not native
speakers; short sentences; no idioms; one term for one thing (always "merge-base", "head", "next
step", "strict" is not used in user docs: say "on a clean checkout"); "Xping Cloud", never
"Dashboard". Samples are generated by the renderer and marked for `DocumentedSampleTests`.
`specs/docs-overhaul.md` is in progress; pages are added in its structure if that structure has
merged by P8, and the paths below are amended first.

### 17.1 `docs/cli/command-reference.md`

- New section `## \`xping triage\``, placed after `xping report` and before `xping where`, in the
  page's template: one sentence; `bash` synopsis; options table `| Option | Default | Description |`
  from D1; a text sample (the §10.7 fixture); subsections `### Next steps` (the table of §0),
  `### Evidence` (decisive `✓` and hint `·`, one paragraph, link to the guide), `### --rerun`,
  `### --format json` (the top-level members and one failure, link to the guide for the full
  contract), `### Provider access` (the three token tables of §5.2); then the line
  `**Exit codes:** \`0\` no failed tests · \`20\` … · \`130\` cancelled.`
- `## Exit codes` table: rows 20–25, and `triage` added to the Commands column of rows 2, 3, 4, 70,
  130. The sentence under the table gains: "Xping Cloud never changes the exit code of `triage`."
- `## Environment variables`: `GH_TOKEN`, `GITHUB_TOKEN`, `GITLAB_TOKEN`, `CI_JOB_TOKEN`,
  `AZURE_DEVOPS_EXT_PAT`, `SYSTEM_ACCESSTOKEN`, `XPING_TRIAGE_NO_FETCH`.
- `### Files under ~/.xping`: `cache/triage/`. And one sentence on `.xping/triage/reruns/` in the
  repository.

### 17.2 Guide: `docs/guides/pull-requests/triaging-a-failed-pull-request.md`

Title "Triaging a Failed Pull Request". Outline:

1. **What `xping triage` does.** The question it answers; one sample; what it never does (no
   comment on the PR, no change to tests, no score).
2. **Before you start.** The CI upload step (link to 17.3); a provider token (link to the command
   reference); nothing else.
3. **Two modes.** A table: without `xping login` (CI, CI stores, local store, git) and with it (adds
   Cloud's classification, the failure seen on other machines, results at the merge-base from CI on
   the base branch). State that the next steps follow the same rules in both modes and that Cloud is
   never required.
4. **Reading the three next steps.** One subsection each, with the sample block, what decided it and
   what to do. Include "fails at the merge-base too" under `INVESTIGATE`.
5. **Reading the evidence lines.** Decisive and hint; the list of lines by source; why a line from
   the local store is always a hint; why "Xping Cloud: flaky" does not make a test `RE-RUN`.
6. **`--rerun`.** What it runs and where; that the working tree is not touched; time and disk;
   uncommitted changes are not tested; how to stop it.
7. **JSON output.** The envelope members; the full `failures[]` member list; bounds and truncation
   markers; the versioning rule.
8. **Exit codes in scripts.** The D14 table and a shell example that branches on 0, 20, 21, 22.
9. **Using it from a coding agent.** The loop of §11 in prose and as a numbered list; which members
   to read for a fix; what an agent must not do.

`docs/guides/toc.yml`, under "Pull Requests", after "Reading the Xping Comment":

```yaml
    - name: Triaging a Failed Pull Request
      href: pull-requests/triaging-a-failed-pull-request.md
    - name: CI Setup for Triage
      href: pull-requests/triage-ci-setup.md
    - name: Triage Troubleshooting
      href: pull-requests/triage-troubleshooting.md
```

### 17.3 CI setup: `docs/guides/pull-requests/triage-ci-setup.md`

Title "CI Setup for Triage". Outline:

1. Why an upload is needed: a CI machine is deleted after the job, and the store goes with it. One
   sentence: users who are signed in may not need it once Xping Cloud can supply the failures (only
   stated when P6b has shipped; before that the page says the upload is required).
2. The store path (`.xping/sessions/` at the checkout root; `XPING_LOCAL_STORE` changes it) and the
   naming convention (`xping-store-<job>-attempt-<n>`), each in one short table.
3. `## GitHub Actions`, `## GitLab CI`, `## Azure Pipelines`: the snippet of §12.2, then three
   bullets: it runs when the job failed; the name carries the attempt; how long it is kept. The
   matrix variant for GitHub. Anchors `#github-actions`, `#gitlab-ci`, `#azure-pipelines` are used by
   error messages (§10.6) and are pinned by a test, as `ShareableOutputTests` pins the population
   marker anchor.
4. How to check it works: run the pipeline, then `xping triage`.

Also: `docs/getting-started/ci-cd-setup.md` gains one paragraph per platform linking here;
`docs/configuration/local-store.md` gains `triage/` in the layout listing.

### 17.4 Troubleshooting: `docs/guides/pull-requests/triage-troubleshooting.md`

One `##` per problem, each with: the message as printed, the cause, the fix.

| Section | Covers |
|---|---|
| No Xping store was uploaded | `store-not-uploaded`; hidden files on GitHub; a GitLab job whose archive has another name |
| The store has expired | `store-expired`; retention settings per provider; re-run the pipeline |
| The token is missing or lacks a scope | One subsection per provider with the exact scopes and the variable names |
| The pull request was not found | Wrong remote; no PR for the branch; `--repo`, `--remote`, a number or URL |
| Pull requests from forks | Where pipelines run; no Cloud data; `--rerun` and the PR ref |
| Self-hosted GitLab, GitHub Enterprise, Azure DevOps Server | `--provider`, the host variables, API paths |
| The pipeline is still running | Exit 22 |
| Clock warnings | `W-CLOCK`: what it affects and what it does not |
| Xping Cloud is unreachable or the sign-in expired | The hint line; triage continues; `--no-cloud` |
| `--rerun` could not build or timed out | The build log tail; `--rerun-timeout`; SDKs missing at the merge-base |
| The repro command passes but CI fails | CI tested the merge with the base branch; update the branch |

`docs/troubleshooting/common-issues.md` gains a link to this page.

---

## 18. Security, concurrency, cancellation

**Tokens**
- Resolved per §5. Held in memory only. Registered with `Redaction.AddSecret` before first use.
- Never in: the envelope, stdout, stderr, `--verbose` lines, the artifact cache, the rerun store, a
  command line of a child process, or an environment variable that triage sets.
- Sent only to the repository's API base URL. Redirect targets get no credential header.
- Every message built from a provider response or a child process output passes through
  `Redaction.Scrub` (I6 adds the provider header names and token shapes). The HTTP client has no
  request logging (`RemoveAllLoggers`); `VerboseLoggerProvider` passes `Xping.Cli.*` only.
- A token found in a git remote URL (`https://user:token@host/…`) is registered as a secret, and the
  URL is printed without user info.

**Untrusted input**
- Artifact archives: entry names are reduced to their file name; entry count and sizes are limited
  (§6.5). Session files are parsed by the SDK's own reader, which treats a damaged file as unreadable.
- Provider responses are read member by member; a missing member is `InvalidResponse`, never a crash.
- Test names, messages, paths and branch names are data. In text mode they pass through
  `TerminalText.Printable` (no control sequences reach the terminal). In commands they pass through
  `ShellQuote.Of`. Arguments to child processes go through `ArgumentList`, never a shell string.
- `--rerun` executes the repository's build and tests at the PR head. For a PR from a fork that is
  code from another author. Before the first build, when `ChangeRequest.IsFork` is true, stderr gets:
  `warning: --rerun builds and runs code from the fork <name>.` The command does not prompt (D1); the
  docs say the same (§17.2 item 6).

**Concurrency**
- Two invocations at once are safe: rerun directories are per invocation; cache writes are
  temp-then-move; the SDK writes sessions atomically.
- Provider requests and downloads: at most 4 in flight. Reruns: one process at a time.

**Cancellation**
- Every async call takes the command's token. On cancel: child processes are killed with their
  process tree, partial downloads and temporary cache directories are deleted, worktrees are
  removed (§9.3), and `Program.Run` prints `Cancelled.` and returns 130.

---

## 19. Out of scope

- Writing to the PR/MR, re-running CI from the CLI, quarantine, skip lists.
- Any local score, rate-based verdict or flaky label.
- LLM-generated text.
- Providers other than GitHub, GitLab and Azure DevOps; mixed hosting (OQ-9).
- Triage of a branch without a PR/MR, or of a commit on the default branch.
- Running `triage` inside the failing CI job to read that job's own store directly.
- Non-.NET test projects.
- The `xping-triage` skill itself.
- Changes to `xping report`, its envelope or its exit codes.
