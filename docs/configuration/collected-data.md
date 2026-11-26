---
uid: configuration-collected-data
title: Data Xping collects
---

# Data Xping collects

This page lists every field the Xping SDK records, where it goes, and the switch that controls it.
It is for developers and for anyone who reviews what a test run sends to Xping Cloud.

---

## Where the data goes

- A **run** is one `dotnet test` invocation that the SDK records, for one test assembly or more.
  The SDK's API (`TestSession`, `SessionId`) and the [read API](../cloud/read-api.md) call it a
  session. A run holds one execution for
  each test that ran.
- An **execution** is one test's result within a run. A retried test has one execution for
  each attempt that the framework reports.

| [Mode](configuration-reference.md#mode) | Local store | Xping Cloud |
|---|---|---|
| `LocalOnly` (the `Auto` mode without an API key) | The full run | Nothing. The SDK makes no network calls. |
| `Cloud` (the `Auto` mode with an API key) | The full run | The run, without the stack trace text unless you turn on [UploadStackTraces](configuration-reference.md#uploadstacktraces) |
| `Disabled` | Nothing | Nothing |

The SDK writes the local store before it uploads. The [local store](local-store.md) page says where
the store is and how to delete it.

---

## Personal data and other sensitive fields

| Field | When the SDK records it | Switch | Default |
|---|---|---|---|
| Machine name (`MachineName`) | Always | None | On |
| CI login or name of the person behind the run (`CI.Actor`, `CI.ActorId`) | On CI, when the platform gives one. See [Who is behind the run](#who-is-behind-the-run). | None | On |
| CI email of that person (`CI.ActorEmail`) | On Azure Pipelines, GitLab, Jenkins and AppVeyor | [CollectCiActorEmail](configuration-reference.md#collectciactoremail) | Off |
| GitLab commit author (`CI.CommitAuthor`) | On GitLab. GitLab gives it as `Name <email>`. | None | On |
| PR author (`PullRequestContext.Author`) | On a PR build with PR context | [EnablePullRequestDetection](configuration-reference.md#enablepullrequestdetection) | On |
| Local git author name (`Git.Actor`) | Outside CI, from `user.name` in the repository's git config, or else in your global git config (`~/.gitconfig` or `$XDG_CONFIG_HOME/git/config`) | [CollectLocalGitAuthor](configuration-reference.md#collectlocalgitauthor) | Off |
| Error message (`ErrorMessage`) | When a test fails | None | On |
| Stack trace text (`StackTrace`) | When a test fails. The local store always keeps it. | [UploadStackTraces](configuration-reference.md#uploadstacktraces), for the upload only | Not uploaded |
| NUnit author tag (`Tags`, `author:<name>`) | For each `[Author]` attribute. `[Author(name, email)]` gives `author:Name <email>`. | None | On |
| Test arguments (`CustomAttributes["Arguments"]`) | For each data row of a parameterized test | None | On |
| xUnit test output (`CustomAttributes["Output"]`) | When an xUnit test writes to `ITestOutputHelper` | None | On |
| MSTest `TestContext` properties | Always on MSTest. Some of them are folder paths on the machine, which can contain the user name. | None | On |
| Source file path (`SourceFile`) | When the test assembly has a Portable PDB. Outside a git repository, the path is absolute and can contain the user name. | None | On |

**The SDK stores and uploads the error message word for word.** It contains whatever your test or
your code put in it, for example an assertion value or a connection string. The SDK does not redact
it. It also sends a hash of the message, with numbers, GUIDs, paths and times replaced, so that
Xping Cloud can group repeats of the same failure.

**Without the stack trace text**, the upload still carries a hash of the method names in the
trace, without file paths or line numbers. Xping Cloud groups failures by it.

The SDK does not read or send your source code.

To record nothing at all, set [Enabled](configuration-reference.md#enabled) to `false`
(`XPING_ENABLED=false`).

---

## Each run

| Field | What it holds |
|---|---|
| `SessionId` | A new GUID for each run |
| `StartedAt`, `EndedAt` | UTC times of the first and last record |
| `Assemblies` | The names of the test assemblies in the run |
| `SdkVersion` | The version of the Xping SDK |
| `SessionState` | Which upload this is: `Initial` (the first batch), `Partial` (a later batch) or `Finalized` (the last one, at the end of the run) |
| `EnvironmentInfo` | The machine and the CI build. See [The environment](#the-environment). |
| `PullRequestContext` | The pull request, on a PR build. See [Pull request context](#pull-request-context). |
| `QuickStatistics`, `StatisticsByAssembly` | Counts of executions by outcome, for the whole run and for each assembly |

---

## Each execution

### Identity

| Field | What it holds |
|---|---|
| `TestFingerprint` | The test's stable id: a SHA-256 hash of the fully qualified name and the parameter hash, or the value of `[XpingFingerprint]` |
| `FullyQualifiedName`, `Assembly`, `Namespace`, `ClassName`, `MethodName` | Where the test is declared |
| `DisplayName` | The name the framework shows |
| `ParameterHash` | A hash of the arguments, for a data row of a parameterized test |
| `SourceFile`, `SourceLineNumber` | The file and the line of the test method, read from the Portable PDB. The path is relative to the repository root when the SDK finds one, and absolute otherwise. Both are empty when the assembly has no PDB. |

### Result

| Field | What it holds |
|---|---|
| `ExecutionId` | A new GUID for each execution |
| `TestName` | The test's name |
| `Outcome` | `Passed`, `Failed`, `Timeout`, `Skipped`, `Inconclusive` or `NotExecuted` |
| `Duration`, `StartTimeUtc`, `EndTimeUtc` | How long the test ran, and when |
| `ExceptionType` | The full type name of the exception that failed the test |
| `ErrorMessage` | The exception message, word for word |
| `ErrorMessageHash` | A SHA-256 hash of the message, with run-specific detail replaced |
| `StackTrace` | The stack trace text. Left out of the upload unless `UploadStackTraces` is on. |
| `StackTraceHash` | A SHA-256 hash of the method names in the trace |
| `StackTraceOmitted` | `true` on the uploaded copy when the SDK left out the trace text |
| `TimeoutBudget`, `TimeoutBudgetSource` | The timeout the test declared. `TimeoutBudgetSource` is `Declared` for a finite timeout and `Infinite` when the test declared no limit. Both are empty when the test declared nothing. |
| `Site` | Where a failing test failed: `TestBody`, `TestSetup`, `TestTeardown`, `FixtureSetup`, `FixtureTeardown`, `AssemblySetup`, `AssemblyTeardown` or `Unknown`. Empty when the test did not fail. |
| `FailureSiteMember` | The setup or teardown member that failed, such as `OrdersFixture.OneTimeSetUp`, when the framework names one |

### Categories, tags and attributes

| Field | NUnit | xUnit v2 and v3 | MSTest |
|---|---|---|---|
| `Categories` | `[Category]` | `[Trait("Category", …)]` | `TestCategory` from `TestContext.Properties` |
| `Description` | `[Description]` | The first `[Trait("Description", …)]` | `Description` from `TestContext.Properties` |
| `Tags` | `framework:NUnit`, `type:test` or `type:suite`, and `author:<name>` for `[Author]` | `framework:xUnit`, `type:fact` or `type:theory`, and `key:value` for every other trait | `framework:MSTest`, `type:test` or `type:datatest` |
| `CustomAttributes` | `FixtureType`, and `Arguments` for a `[TestCase]` | `Arguments` for a theory, and `Output` for the test output | Every other `TestContext.Properties` entry, `Arguments` for a `[DataRow]`, and `ResultsDirectory` |

Xping Cloud stores these fields with each execution and returns them from its API. Scoring and
flaky-test detection do not use them, and the test list does not filter by them. Xping Cloud reads
only one tag itself: `framework:<name>`.

### Test order and concurrency

| Field | What it holds |
|---|---|
| `PositionInSuite` | The test's position on its worker |
| `GlobalPosition` | The test's position across all workers |
| `PreviousTestId`, `PreviousTestName`, `PreviousTestOutcome` | The test that ran just before this one on the same worker |
| `WasParallelized`, `ConcurrentTestCount` | Whether other tests ran at the same time, and how many |
| `ThreadId`, `WorkerId` | The thread and the worker that ran the test |
| `SuiteElapsedTime` | The time from the start of the run to the start of the test |
| `CollectionName` | The xUnit test collection, the NUnit fixture or the MSTest test class |

### Retries

The SDK records retry fields when a test has a retry attribute:

| Field | What it holds |
|---|---|
| `AttemptNumber` | `1` for the first attempt, `2` and up for retries |
| `MaxRetries` | The count the attribute declares |
| `PassedOnRetry` | `true` when an attempt after the first passed |
| `DelayBetweenRetries` | The delay the attribute declares |
| `RetryReason` | The reason the attribute declares |
| `RetryAttributeName` | The name of the retry attribute |
| `AdditionalMetadata` | The attribute's other properties |

The SDK recognizes these attributes, by name:

| Framework | Attributes |
|---|---|
| NUnit | `Retry` |
| xUnit | `RetryFact`, `RetryTheory`, `Retry`, `XunitRetry` |
| MSTest | `TestRetry`, `Retry`, `DataTestMethodWithRetry` |

To add your own attribute, call `RetryAttributeRegistry.RegisterCustomRetryAttribute` before the
run starts. The framework name is `xunit`, `nunit` or `mstest`:

```csharp
using Xping.Sdk.Core.Services.Retry;

RetryAttributeRegistry.RegisterCustomRetryAttribute("nunit", "RetryOnDeadlock");
```

On xUnit v3, xRetry.v3 reports only the last attempt. See
[Known limitations](../troubleshooting/known-limitations.md#xunit-v3-records-only-the-last-retry-attempt).

---

## The environment

`EnvironmentInfo` describes the machine, once for each run.

| Field | What it holds |
|---|---|
| `MachineName` | The machine name |
| `OperatingSystem` | The OS name and version |
| `RuntimeVersion`, `Framework` | The .NET runtime |
| `EnvironmentName` | The value of [Environment](configuration-reference.md#environment), or `Default` |
| `IsCIEnvironment` | `true` when the SDK detects a CI platform |
| `TimeZoneId`, `UtcOffset` | The machine's time zone and its UTC offset at the start of the run |
| `CustomProperties` | The properties below, and the CI properties in [CI metadata](#ci-metadata) |

| Custom property | When it is set |
|---|---|
| `ExecutionContext` | Always: `CI` or `Local` |
| `IsDeveloperMachine` | `true` outside CI and outside a container |
| `IsContainer` | `true` when `/.dockerenv` exists, `KUBERNETES_SERVICE_HOST` is set, or on Linux `/proc/1/cgroup` names `docker` or `kubepods` |
| `IsInsideGitRepository` | Always: `true` or `false` |
| `Platform` | `Windows`, `Linux`, `macOS`, `Android` or `iOS` |
| `IsMobile`, `DeviceType` | On Android and iOS |
| `ProcessorArchitecture`, `ProcessorCount` | Always |
| `Git.Branch`, `Git.SHA` | Outside CI, from the local git repository |
| `IsDetachedHead`, `HasStagedChanges` | Outside CI, from the local git repository |
| `Git.Actor` | Outside CI, only with [CollectLocalGitAuthor](configuration-reference.md#collectlocalgitauthor) |

---

## CI metadata

### Detected CI platforms

The SDK checks for each platform in this order and uses the first match. It sets `CIPlatform` to
the name in the first column.

| `CIPlatform` | Detected by | PR context |
|---|---|---|
| `GitHubActions` | `GITHUB_ACTIONS=true` | Yes |
| `AzureDevOps` | `TF_BUILD` | Yes, for Azure Repos and GitHub repositories |
| `Jenkins` | `JENKINS_URL` | Yes, for multibranch (Branch Source) PR builds of GitHub, GitLab and Azure DevOps repositories |
| `GitLabCI` | `GITLAB_CI=true` | Yes, in merge request pipelines |
| `CircleCI` | `CIRCLECI=true` | No |
| `TravisCI` | `TRAVIS=true` | No |
| `TeamCity` | `TEAMCITY_VERSION` | No |
| `BitbucketPipelines` | `BITBUCKET_PIPELINE_UUID` | No |
| `AppVeyor` | `APPVEYOR=True` | No |
| `Generic` | `CI=true` or `CI=True` | No |

CircleCI, Travis CI, TeamCity, Bitbucket Pipelines, AppVeyor and a generic CI upload without PR
context. They are not targeted for it. Most of them still send the PR flag (`CI.IsPullRequest`).

### Properties for each platform

| Property | GitHub Actions | Azure Pipelines | Jenkins | GitLab | CircleCI | Travis CI | TeamCity | Bitbucket | AppVeyor |
|---|---|---|---|---|---|---|---|---|---|
| `CI.CommitSha` | `GITHUB_SHA` (see [Which commit is recorded](#which-commit-is-recorded)) | `BUILD_SOURCEVERSION` | `GIT_COMMIT` | `CI_COMMIT_SHA` | `CIRCLE_SHA1` | `TRAVIS_COMMIT` | `BUILD_VCS_NUMBER` | `BITBUCKET_COMMIT` | `APPVEYOR_REPO_COMMIT` |
| `CI.IsPullRequest` | `GITHUB_EVENT_NAME`, `GITHUB_REF` | `BUILD_REASON` | `CHANGE_ID` or `ghprbPullId` | `CI_MERGE_REQUEST_IID` | `CIRCLE_PULL_REQUEST` | `TRAVIS_PULL_REQUEST` | Not sent | `BITBUCKET_PR_ID` | `APPVEYOR_PULL_REQUEST_NUMBER` |
| `CI.Branch` | `GITHUB_HEAD_REF`, else `GITHUB_REF_NAME`, else the branch in `GITHUB_REF` | `SYSTEM_PULLREQUEST_SOURCEBRANCH` on a PR, else `BUILD_SOURCEBRANCH` without `refs/heads/` | `CHANGE_BRANCH` or `ghprbSourceBranch` on a PR, else `BRANCH_NAME`, else `GIT_BRANCH` without `origin/` | `CI_COMMIT_BRANCH`, else `CI_COMMIT_REF_NAME` | `CIRCLE_BRANCH` | `TRAVIS_BRANCH` | — | `BITBUCKET_BRANCH` | `APPVEYOR_REPO_BRANCH` |
| `CI.DefaultBranch` | `repository.default_branch` from the event file | — | — | `CI_DEFAULT_BRANCH` | — | — | — | — | — |
| `CI.ServerUrl` | `GITHUB_SERVER_URL` | `SYSTEM_COLLECTIONURI` (Azure Repos) or `BUILD_REPOSITORY_URI` | — | `CI_SERVER_URL` | — | — | — | — | — |
| `CI.Repository` | `GITHUB_REPOSITORY` | `BUILD_REPOSITORY_NAME` | — | `CI_PROJECT_PATH` (as `CI.ProjectPath`) | `CIRCLE_PROJECT_REPONAME` (as `CI.ProjectName`) | `TRAVIS_REPO_SLUG` | `TEAMCITY_PROJECT_NAME` (as `CI.ProjectName`) | `BITBUCKET_REPO_FULL_NAME` | `APPVEYOR_REPO_NAME` |
| Build ids | `CI.RunId` (`GITHUB_RUN_ID`), `CI.RunNumber` (`GITHUB_RUN_NUMBER`) | `CI.BuildId` (`BUILD_BUILDID`), `CI.BuildNumber` (`BUILD_BUILDNUMBER`) | `CI.BuildNumber` (`BUILD_NUMBER`), `CI.JobName` (`JOB_NAME`), `CI.BuildUrl` (`BUILD_URL`) | `CI.JobId` (`CI_JOB_ID`), `CI.PipelineId` (`CI_PIPELINE_ID`) | `CI.BuildNumber` (`CIRCLE_BUILD_NUM`), `CI.WorkflowId` (`CIRCLE_WORKFLOW_ID`) | `CI.BuildNumber` (`TRAVIS_BUILD_NUMBER`), `CI.JobNumber` (`TRAVIS_JOB_NUMBER`) | `CI.BuildId` (`TEAMCITY_BUILD_ID`), `CI.Version` (`TEAMCITY_VERSION`) | `CI.BuildNumber` (`BITBUCKET_BUILD_NUMBER`) | `CI.BuildNumber` (`APPVEYOR_BUILD_NUMBER`), `CI.BuildVersion` (`APPVEYOR_BUILD_VERSION`) |
| Other | `CI.Ref` (`GITHUB_REF`), `CI.HeadBranch` (`GITHUB_HEAD_REF`), `CI.BaseBranch` (`GITHUB_BASE_REF`), `CI.Workflow` (`GITHUB_WORKFLOW`) | `CI.SourceBranch` (`BUILD_SOURCEBRANCH`) | `CI.GitBranch` (`GIT_BRANCH`) | `CI.CommitBranch` (`CI_COMMIT_BRANCH`), `CI.CommitAuthor` (`CI_COMMIT_AUTHOR`) | — | — | — | — | — |

A generic CI (`CI=true` or `CI=True`) sends only `CIPlatform`, and `CI.DefaultBranch` when you set DefaultBranch. The actor properties are in
[Who is behind the run](#who-is-behind-the-run).

[DefaultBranch](configuration-reference.md#defaultbranch) (`XPING_DEFAULTBRANCH`) replaces
`CI.DefaultBranch` on every platform. When it differs from what the platform reports, the SDK logs
a warning.

### Which commit is recorded

A pull request build can carry two commits, and the SDK records both:

- **`CI.CommitSha`** is the commit the build checked out and tested. On a pull request build, it is
  often a merge commit that the platform made for the run.
- **The PR context's commit** is the head commit the author pushed. It stays the same when the
  build runs again, and it is the commit your code host shows on the PR.

| Build | PR context commit | `CI.CommitSha` |
|---|---|---|
| GitHub `push`, `schedule`, `workflow_dispatch`, `merge_group`, `release`, `create`, `deployment`, `deployment_status` | — | `GITHUB_SHA` |
| GitHub `pull_request` | `pull_request.head.sha` | `GITHUB_SHA` (the merge commit) |
| GitHub `pull_request_target` | `pull_request.head.sha` | Not sent: `GITHUB_SHA` is the tip of the base branch |
| GitHub, any other event (`issue_comment`, `workflow_run`, `repository_dispatch`, …) | — | Not sent: `GITHUB_SHA` is the tip of the default branch, not what the workflow checked out |
| Azure Pipelines PR | `SYSTEM_PULLREQUEST_SOURCECOMMITID` | `BUILD_SOURCEVERSION` (the merge commit) |
| GitLab merged-results pipeline or merge train | `CI_MERGE_REQUEST_SOURCE_BRANCH_SHA` | `CI_COMMIT_SHA` (the merge result) |
| Jenkins Branch Source PR | `refs/remotes/origin/$BRANCH_NAME`, checked against `GIT_COMMIT` | `GIT_COMMIT` (the merge commit, with the merge strategy) |

On GitHub Actions, the SDK sets `CI.IsPullRequest` to `true` for `pull_request`,
`pull_request_target` and any run on `refs/pull/*`. It sets it to `false` for the push-like events
in the first row. For any other event, it sends neither `CI.IsPullRequest` nor `CI.CommitSha`,
because a `/retest` comment or a `workflow_run` usually tests PR code.

Xping Cloud files a run under the PR context's commit when there is one. It uses `CI.CommitSha` only
when `CI.IsPullRequest` is `false`. Xping Cloud does not file these runs under a commit, so it never
takes them for a run on your main branch:

- A run without the flag (TeamCity, a generic CI, or the GitHub events above). It may be a pull
  request build.
- A pull request build without PR context, such as a Jenkins GHPRB job.

### Who is behind the run

Xping Cloud counts a workspace's active committers from each uploaded CI run. It counts the PR
author when there is PR context, and `CI.Actor` otherwise. `CI.Actor` is the person the platform
names for the run. On most platforms, that is the person who started it. On Jenkins, it is the PR's
author. On AppVeyor, it is the author of the commit.

| Platform | `CI.Actor` | `CI.ActorId` | `CI.ActorEmail` (only with `CollectCiActorEmail`) |
|---|---|---|---|
| GitHub Actions | `GITHUB_ACTOR` | `GITHUB_ACTOR_ID` | — |
| Azure Pipelines | `BUILD_REQUESTEDFOR` (a display name). Not sent on scheduled builds or for a service identity. | — | `BUILD_REQUESTEDFOREMAIL` |
| GitLab | `GITLAB_USER_LOGIN` | — | `GITLAB_USER_EMAIL` |
| Jenkins | `CHANGE_AUTHOR` (Branch Source PR builds), `ghprbPullAuthorLogin` (GHPRB PR builds). Not sent on push builds. | — | `CHANGE_AUTHOR_EMAIL`, `ghprbPullAuthorEmail` |
| CircleCI | `CIRCLE_USERNAME` | — | — |
| Bitbucket Pipelines | `BITBUCKET_STEP_TRIGGERER_UUID` (an account UUID) | — | — |
| AppVeyor | `APPVEYOR_REPO_COMMIT_AUTHOR` (the commit author's git name) | — | `APPVEYOR_REPO_COMMIT_AUTHOR_EMAIL` |
| TeamCity, Travis CI, generic CI | — | — | — |

A run with neither a PR author nor `CI.Actor` adds no committer. Local runs never add one: they send
no `CI.Actor`, and Xping Cloud does not count `Git.Actor`.

`CollectCiActorEmail` is off by default, because an email address is personal data. It does not
change how Xping Cloud counts committers: Xping Cloud counts by platform and login only. The email
lets Xping Cloud link a committer to a workspace member automatically on Azure Pipelines, GitLab,
Jenkins and AppVeyor. Without it, an Owner or Admin links them by hand. GitHub Actions links by the
actor id and login, so the switch does not affect it.

### Pull request context

On a PR build, the SDK records the pull request once for each run. Set
[EnablePullRequestDetection](configuration-reference.md#enablepullrequestdetection) to `false` to
turn it off.

| Field | What it holds |
|---|---|
| `Platform` | `GitHub`, `GitLab` or `AzureDevOps`: the code host, not the CI |
| `ServerUrl` | The code host's server, such as `https://github.com` or `https://gitlab.example.com:8443` |
| `RepositoryOwner`, `RepositoryName` | The repository |
| `PullRequestNumber` | The PR or merge request number |
| `CommitSha` | The PR head commit. See [Which commit is recorded](#which-commit-is-recorded). |
| `BaseBranch`, `HeadBranch` | The target branch and the source branch |
| `Author` | `GITHUB_ACTOR` (GitHub Actions), `BUILD_REQUESTEDFOR` (Azure Pipelines), `GITLAB_USER_LOGIN` (GitLab) or `CHANGE_AUTHOR` (Jenkins) |

GitHub Enterprise Server, GHE.com, self-managed GitLab and Azure DevOps Server get PR context in the
same way as the cloud services. [Set up CI](../getting-started/ci-cd-setup.md) lists what each
platform needs.

---

## See also

- [Configuration reference](configuration-reference.md)
- [Local store](local-store.md)
- [Set up CI](../getting-started/ci-cd-setup.md)
