# CI/CD Integration Guide

Learn how to integrate Xping SDK into your CI/CD pipelines for continuous test reliability monitoring. This guide covers the most popular CI/CD platforms and best practices.

---

## Overview

Xping SDK automatically detects CI/CD environments and captures relevant metadata like build numbers, commit SHAs, and branch names. It also marks non-CI executions as local developer-machine runs. This enables you to:

- **Track test reliability across builds**
- **Detect flaky tests in your pipeline**
- **Correlate test failures with specific commits**
- **Monitor test performance trends over time**

---

## Quick Setup (All Platforms)

The basic setup is the same across all CI/CD platforms:

1. **Get your API key** from [Xping Cloud](https://app.xping.io): **Account** → **Settings** → **API & Integration** → **Create API Key**
2. **Store it as a secret** in your CI/CD platform — never as a plain variable, and never in `appsettings.json`
3. **Expose it to the test step** as `XPING_APIKEY`
4. **Run your tests normally** - Xping SDK handles the rest

That is the whole setup. One secret, one environment variable.

> **You do not choose a project name.** Xping derives the project from the test assembly each
> execution belongs to, and creates it on the first upload. A solution with several test projects
> gets one Xping project each.
>
> `XPING_PROJECTID` is **optional** and exists only to override that — set it when several test
> assemblies should report into a single project. See
> [ProjectId](../configuration/configuration-reference.md#projectid).

> **`XPING_APIKEY` is upload-only.** It can write test runs and nothing else, so the key sitting in
> your CI secrets is not a way into your data. Reading history back is a person's action,
> authenticated in [Xping Cloud](https://app.xping.io).

---

## Which Commit Is Recorded

A pull request build can carry two different commits, and Xping records both on purpose:

- **`CI.CommitSha`** is the commit the build checked out and tested. On a push build that is the
  pushed commit. On a pull request build it is often a merge commit the platform made for the run,
  which exists nowhere else.
- **The PR context's commit** is the head commit the author pushed. It stays the same when the build
  is re-run, and it is the commit your code host shows on the PR.

| Build | PR context commit | `CI.CommitSha` |
|---|---|---|
| GitHub `push`, `schedule`, `workflow_dispatch`, `merge_group`, `release`, `create`, `deployment`, `deployment_status` | — | `GITHUB_SHA` (the commit the run checked out) |
| GitHub `pull_request` | `pull_request.head.sha` | `GITHUB_SHA` (merge commit) |
| GitHub `pull_request_target` | `pull_request.head.sha` | not sent (`GITHUB_SHA` is the base branch's tip) |
| GitHub, any other event (`issue_comment`, `workflow_run`, `repository_dispatch`, …) | — | not sent (`GITHUB_SHA` is the default branch's tip, not what the workflow checked out) |
| Azure Pipelines PR | `SYSTEM_PULLREQUEST_SOURCECOMMITID` | `BUILD_SOURCEVERSION` (merge commit) |
| GitLab merged-results / merge train | `CI_MERGE_REQUEST_SOURCE_BRANCH_SHA` | `CI_COMMIT_SHA` (merge result) |
| Jenkins Branch Source PR | `refs/remotes/origin/$BRANCH_NAME`, checked against `GIT_COMMIT` | `GIT_COMMIT` (merge commit under the merge strategy) |

Xping Cloud files a run under the PR context's commit when there is one. It uses `CI.CommitSha`
only when `CI.IsPullRequest` is `false`. A pull request build without a PR context (for example a
Jenkins GHPRB job) is filed under no commit, so it is never mistaken for a run on your main branch.
A GitHub run of any other event is filed under no commit too: it sends neither `CI.IsPullRequest`
nor `CI.CommitSha`, because a "/retest" comment or a `workflow_run` follow-up usually tests PR code.

PR context and `CI.ServerUrl` both name the server the repository lives on (`https://github.com`,
`https://ghes.example.com`, `https://gitlab.example.com:8443`, …), so GitHub Enterprise, GHE.com,
self-managed GitLab and Azure DevOps Server are supported like their cloud counterparts. Xping
Cloud posts PR comments only for github.com repositories.

---

## GitHub Actions

### Configuration

Store your Xping credentials as GitHub Secrets:

1. Go to **Repository Settings** → **Secrets and variables** → **Actions**
2. Add the following secrets:
   - `XPING_APIKEY`: Your Xping API key (from Account → Settings → API & Integration)
   - `XPING_PROJECTID` *(optional)*: Pins every test assembly to one project. Omit it and each test assembly gets its own project, named after the assembly.

### Workflow Example

```yaml
name: Test with Xping

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore --configuration Release
      
      - name: Run tests with Xping
        env:
          XPING_APIKEY: ${{ secrets.XPING_APIKEY }}
          XPING_ENABLED: true
        run: dotnet test --no-build --configuration Release --logger "console;verbosity=detailed"
```

### Captured Metadata

Xping automatically captures:
- `GITHUB_ACTIONS` - CI environment indicator
- `GITHUB_RUN_ID` - Unique workflow run ID
- `GITHUB_RUN_NUMBER` - Sequential run number
- `GITHUB_SHA` - Normalized into `CI.CommitSha`, omitted on `pull_request_target` and on events outside the list below (see [Which Commit Is Recorded](#which-commit-is-recorded))
- `GITHUB_EVENT_PATH` - Event payload; on a pull request, `pull_request.head.sha` becomes the PR context's commit; `repository.default_branch` becomes `CI.DefaultBranch`
- `GITHUB_EVENT_NAME` / `GITHUB_REF` - Normalized into `CI.IsPullRequest`: `true` for `pull_request`, `pull_request_target`, or any run on `refs/pull/*`; `false` for `push`, `schedule`, `workflow_dispatch`, `merge_group`, `release`, `create`, `deployment` and `deployment_status`; not sent for any other event
- `GITHUB_REF` - Branch or tag ref
- `GITHUB_SERVER_URL` - Normalized into `CI.ServerUrl` and the PR context's server (github.com, GHE.com or GitHub Enterprise Server)
- `GITHUB_HEAD_REF` / `GITHUB_REF_NAME` - Normalized into `CI.Branch`
- `GITHUB_REPOSITORY` - Repository name
- `GITHUB_ACTOR` - User who triggered the workflow

If tests run inside a container (`docker run`), pass the `GITHUB_*` variables, including
`GITHUB_SERVER_URL`, **and** mount the runner's temp directory so the file at `GITHUB_EVENT_PATH`
is readable. Without them, the run is still recorded, but PR context (PR number, branches, PR
comment) is skipped and a warning is logged. Without `GITHUB_EVENT_NAME`, a warning is logged, PR
context is skipped, and unless `GITHUB_REF` is `refs/pull/*` the run is filed under no commit.

---

## Azure DevOps

### Configuration

Store your Xping credentials as Pipeline Variables:

1. Go to **Pipelines** → **Library** → **Variable groups**
2. Create a variable group named `Xping`
3. Add the following variables:
   - `XPING.ApiKey`: Your Xping API key (mark as secret)
   - `XPING.ProjectId` *(optional)*: Pins every test assembly to one project. Omit it and each test assembly gets its own project, named after the assembly.

### Pipeline Example (YAML)

```yaml
trigger:
  branches:
    include:
      - main
      - develop

pool:
  vmImage: 'ubuntu-latest'

variables:
  - group: Xping

steps:
- task: UseDotNet@2
  displayName: 'Setup .NET'
  inputs:
    version: '10.0.x'

- task: DotNetCoreCLI@2
  displayName: 'Restore dependencies'
  inputs:
    command: 'restore'

- task: DotNetCoreCLI@2
  displayName: 'Build'
  inputs:
    command: 'build'
    arguments: '--no-restore --configuration Release'

- task: DotNetCoreCLI@2
  displayName: 'Run tests with Xping'
  inputs:
    command: 'test'
    arguments: '--no-build --configuration Release'
  env:
    XPING_APIKEY: $(XPING.ApiKey)
    XPING_ENABLED: true
```

### Captured Metadata

Xping automatically captures:
- `TF_BUILD` - CI environment indicator
- `BUILD_BUILDID` - Unique build ID
- `BUILD_BUILDNUMBER` - Build number
- `BUILD_SOURCEVERSION` - Normalized into `CI.CommitSha` (see [Which Commit Is Recorded](#which-commit-is-recorded))
- `BUILD_REASON` - Normalized into `CI.IsPullRequest` (`true` when `PullRequest`)
- `BUILD_SOURCEBRANCH` / `SYSTEM_PULLREQUEST_SOURCEBRANCH` - Normalized into `CI.Branch`, the full branch name
  without `refs/heads/` (`feature/main`, not `main`); on a PR build, the PR's source branch; on a tag
  build, the tag name. TFVC builds use `BUILD_SOURCEBRANCHNAME`
- `BUILD_REPOSITORY_NAME` - Repository name
- `SYSTEM_COLLECTIONURI` (Azure Repos) / `BUILD_REPOSITORY_URI` (other providers) - Normalized into
  `CI.ServerUrl`, the same server the PR context records
- `BUILD_REQUESTEDFOR` - User who triggered the build

On a pull request build, Xping also records the PR context (number, branches, head commit) for
repositories in Azure Repos or on GitHub:
- `BUILD_REPOSITORY_PROVIDER` - `TfsGit` (Azure Repos), `GitHub` or `GitHubEnterprise`; other providers get no PR context
- `BUILD_REPOSITORY_URI` - For GitHub, the server (github.com, GHE.com or GitHub Enterprise Server)
- `SYSTEM_PULLREQUEST_PULLREQUESTID` (Azure Repos) / `SYSTEM_PULLREQUEST_PULLREQUESTNUMBER` (GitHub) - PR number
- `SYSTEM_PULLREQUEST_SOURCEBRANCH` / `SYSTEM_PULLREQUEST_TARGETBRANCH` - Head and base branch
- `SYSTEM_PULLREQUEST_SOURCECOMMITID` - PR head commit (`BUILD_SOURCEVERSION` is the merge commit)
- `SYSTEM_COLLECTIONURI` / `SYSTEM_TEAMPROJECT` - Azure Repos server and owner. On Azure DevOps
  Services the owner is `{organization}/{project}` and the server `https://dev.azure.com`, whichever
  URL form the organization uses. On Azure DevOps Server the owner is `{collection}/{project}` and
  the server is the collection URL without the collection (`https://tfs.example.com/tfs`).

Xping Cloud doesn't post PR comments on Azure Repos or GitHub Enterprise yet; the PR context still
drives PR insights.

---

## GitLab CI/CD

### Configuration

Store your Xping credentials as CI/CD Variables:

1. Go to **Settings** → **CI/CD** → **Variables**
2. Add the following variables:
   - `XPING_APIKEY`: Your Xping API key (mark as masked)
   - `XPING_PROJECTID` *(optional)*: Pins every test assembly to one project. Omit it and each test assembly gets its own project, named after the assembly.

### Pipeline Example (.gitlab-ci.yml)

```yaml
image: mcr.microsoft.com/dotnet/sdk:8.0

stages:
  - build
  - test

variables:
  XPING_ENABLED: "true"

before_script:
  - dotnet --version

build:
  stage: build
  script:
    - dotnet restore
    - dotnet build --no-restore --configuration Release
  artifacts:
    paths:
      - ./**/bin/Release/
    expire_in: 1 hour

test:
  stage: test
  dependencies:
    - build
  script:
    - dotnet test --no-build --configuration Release --logger "console;verbosity=detailed"
  variables:
    XPING_APIKEY: $XPING_APIKEY
```

### Captured Metadata

Xping automatically captures:
- `GITLAB_CI` - CI environment indicator
- `CI_PIPELINE_ID` - Unique pipeline ID
- `CI_JOB_ID` - Job ID
- `CI_COMMIT_SHA` - Normalized into `CI.CommitSha` (see [Which Commit Is Recorded](#which-commit-is-recorded))
- `CI_MERGE_REQUEST_IID` - Normalized into `CI.IsPullRequest` (`true` in merge request pipelines)
- `CI_COMMIT_BRANCH` / `CI_COMMIT_REF_NAME` - Normalized into `CI.Branch`
- `CI_DEFAULT_BRANCH` - Normalized into `CI.DefaultBranch`
- `CI_PROJECT_PATH` - Repository path
- `CI_SERVER_URL` - Normalized into `CI.ServerUrl`
- `GITLAB_USER_LOGIN` - User who triggered the pipeline

In a merge request pipeline, on gitlab.com or a self-managed instance, Xping also records the MR
context (number, branches, head commit). The MR context comes from:
- `CI_SERVER_URL` - Server, including the port and any relative URL root
- `CI_MERGE_REQUEST_IID` - MR number
- `CI_MERGE_REQUEST_PROJECT_PATH` - Owner and project (nested groups stay in the owner: `group/sub`)
- `CI_MERGE_REQUEST_SOURCE_BRANCH_NAME` / `CI_MERGE_REQUEST_TARGET_BRANCH_NAME` - Head and base branch
- `CI_MERGE_REQUEST_SOURCE_BRANCH_SHA` - MR head commit in merged-results pipelines, where
  `CI_COMMIT_SHA` is the merge result; otherwise `CI_COMMIT_SHA`

Xping Cloud doesn't post MR comments on GitLab yet; the MR context still drives PR insights.

---

## Jenkins

### Configuration

Store your Xping credentials using Jenkins Credentials:

1. Go to **Manage Jenkins** → **Credentials**
2. Add **Secret text** credentials:
   - ID: `xping-api-key`, Secret: Your Xping API key

   That is the only credential needed. `XPING_PROJECTID` is optional and only pins several test
   assemblies into a single project — omit it and each assembly gets its own.

### Pipeline Example (Jenkinsfile)

```groovy
pipeline {
    agent any
    
    environment {
        XPING_APIKEY = credentials('xping-api-key')
        XPING_ENABLED = 'true'
    }
    
    stages {
        stage('Restore') {
            steps {
                sh 'dotnet restore'
            }
        }
        
        stage('Build') {
            steps {
                sh 'dotnet build --no-restore --configuration Release'
            }
        }
        
        stage('Test') {
            steps {
                sh 'dotnet test --no-build --configuration Release'
            }
        }
    }
    
    post {
        always {
            // Archive test results if needed
            archiveArtifacts artifacts: '**/TestResults/*.trx', allowEmptyArchive: true
        }
    }
}
```

### Pull Request Builds

On a multibranch pipeline, Xping attaches the PR context to PR builds with both the merge strategy
(the default) and the head strategy. No Jenkins variable holds the PR head commit when Jenkins merges
the PR into its target, so Xping reads it from the workspace's `.git` directory: the
`refs/remotes/origin/$BRANCH_NAME` ref, used only when it is `GIT_COMMIT` or `GIT_COMMIT`'s first
parent. The tests must therefore run in the checked-out workspace (`WORKSPACE`). If you use
`skipDefaultCheckout`, check out with `checkout scm` before the test stage. Shallow clones work.
If `git gc` has packed the merge commit (common on long-lived workspaces), Xping runs
`git rev-parse` to read it, so keep `git` on the agent's `PATH`.

GitHub Pull Request Builder (GHPRB) jobs are marked as PR builds but get no PR context.

### Captured Metadata

Xping automatically captures:
- `JENKINS_URL` - CI environment indicator
- `BUILD_ID` - Unique build ID
- `BUILD_NUMBER` - Build number
- `GIT_COMMIT` - Normalized into `CI.CommitSha` (if using Git)
- `CHANGE_ID` / `ghprbPullId` - Normalized into `CI.IsPullRequest` (`true` when either is set)
- `CHANGE_ID`, `CHANGE_URL`, `CHANGE_BRANCH`, `CHANGE_TARGET`, `CHANGE_AUTHOR` - PR context on
  multibranch (Branch Source) builds of GitHub, GitLab and Azure DevOps repositories on any server.
  The platform is read from the shape of `CHANGE_URL`. For a GitLab instance under a relative URL
  root, the root ends up in the owner rather than the server.
- `CHANGE_BRANCH` / `ghprbSourceBranch` / `BRANCH_NAME` / `GIT_BRANCH` - Normalized into `CI.Branch`:
  the PR's source branch on a Branch Source (`CHANGE_BRANCH`) or GHPRB (`ghprbSourceBranch`) PR build,
  else `BRANCH_NAME`, else `GIT_BRANCH` without a leading `origin/` or `refs/remotes/origin/` (other
  remotes are kept as is)
- `JOB_NAME` - Job name
- `BUILD_USER` - User who triggered the build (if available)

---

## CircleCI

### Configuration

Store your Xping credentials as Environment Variables:

1. Go to **Project Settings** → **Environment Variables**
2. Add the following variables:
   - `XPING_APIKEY`: Your Xping API key
   - `XPING_PROJECTID` *(optional)*: Pins every test assembly to one project. Omit it and each test assembly gets its own project, named after the assembly.

### Pipeline Example (.circleci/config.yml)

```yaml
version: 2.1

orbs:
  dotnet: circleci/dotnet@1.0.0

jobs:
  build-and-test:
    docker:
      - image: mcr.microsoft.com/dotnet/sdk:8.0
    
    environment:
      XPING_APIKEY: $XPING_APIKEY
      XPING_ENABLED: "true"
    
    steps:
      - checkout
      
      - run:
          name: Restore dependencies
          command: dotnet restore
      
      - run:
          name: Build
          command: dotnet build --no-restore --configuration Release
      
      - run:
          name: Run tests with Xping
          command: dotnet test --no-build --configuration Release

workflows:
  build-test:
    jobs:
      - build-and-test
```

### Captured Metadata

Xping automatically captures:
- `CIRCLECI` - CI environment indicator
- `CIRCLE_BUILD_NUM` - Build number
- `CIRCLE_SHA1` - Normalized into `CI.CommitSha`
- `CIRCLE_PULL_REQUEST` - Normalized into `CI.IsPullRequest` (`true` whenever the branch has an open PR)
- `CIRCLE_BRANCH` - Branch name
- `CIRCLE_PROJECT_REPONAME` - Repository name
- `CIRCLE_USERNAME` - User who triggered the build

---

## Gating the Build on Findings

The SDK records; the CLI reads. If you want CI to *act* on what was recorded — not just ship it to
Xping Cloud — add the `xping` tool to the job. It reads the same `.xping/` store the SDK just
wrote, so this works with or without an API key:

```yaml
- name: Install Xping CLI
  run: dotnet tool install -g Xping.Cli

- name: Run tests
  env:
    XPING_APIKEY: ${{ secrets.XPING_APIKEY }}
  run: dotnet test --no-build --configuration Release

- name: Check reliability findings
  if: always()
  run: xping report --fail-on high
```

`--fail-on high` exits non-zero when a high-severity finding appears. Useful variants:

```bash
xping report --summary            # one line, good as a CI step title
xping report --format json        # versioned envelope, for a script or an agent
xping report --no-color --ascii   # for log collectors that mangle ANSI
```

> The CLI targets `net10.0`. If your build agent is on an older SDK, either add a .NET 10 setup
> step or run the check in a separate job — the SDK packages themselves target `netstandard2.0` and
> are unaffected.
>
> A fresh CI runner starts with an empty store, so its report covers only that job's runs. The
> cross-run history that makes findings meaningful accumulates in Xping Cloud, or on developer
> machines that keep their `.xping/` between runs.

To have the CI report add Xping Cloud's confidence for each test, give the report step a key that
can read Cloud data. `xping login` needs an interactive terminal and refuses to run when `CI` is set, so in a pipeline a
key is the only way:

```yaml
- name: Check reliability findings
  if: always()
  env:
    XPING_APIKEY: ${{ secrets.XPING_READ_APIKEY }}
  run: xping report --fail-on high
```

An upload-only key cannot read; the report then says so on stderr and stays local. Cloud data never
changes the exit code. See [Credentials](../cli/command-reference.md#credentials).

Full flag list: [CLI Command Reference](../cli/command-reference.md).

---

## Best Practices

### 1. Always Use Secrets for Credentials

**✅ Do:**
```yaml
env:
  XPING_APIKEY: ${{ secrets.XPING_APIKEY }}
```

**❌ Don't:**
```yaml
env:
  XPING_APIKEY: "pk_live_1234567890abcdef"  # Never hardcode!
```

### 2. CI Metadata Is Captured Automatically

Nothing needs enabling. Every run detects its CI platform and records the branch, commit SHA, run
ID, actor and `IsCIEnvironment` flag on its own.

Whatever the platform, the commit lands in `CI.CommitSha` and the pull request flag in
`CI.IsPullRequest` (`"true"` or `"false"`). The flag is left out where the platform gives no
reliable pull request marker (TeamCity, or a generic `CI=true` runner), and Xping Cloud then treats
the run as possibly a pull request. On GitHub Actions, a run of an event that doesn't say which commit
was built (`issue_comment`, `workflow_run`, …) sends neither the flag nor `CI.CommitSha` (see
[Which Commit Is Recorded](#which-commit-is-recorded)).

### 3. Name Environments After Deployments, Not After Runs

`XPING_ENVIRONMENT` names the **deployed environment your tests targeted**. Set it only when a
pipeline points at a genuinely different deployment:

**✅ Do:**
```yaml
env:
  XPING_ENVIRONMENT: "Staging"
```

**❌ Don't:**
```yaml
env:
  XPING_ENVIRONMENT: "PR-${{ github.event.pull_request.number }}"   # a new environment per PR
  # or
  XPING_ENVIRONMENT: "Production-CI"                                # CI is not an environment
```

Confidence scores are computed per (test, environment). A name that changes run to run splits each
test's history permanently, and the distinction is already recorded: a run's commit SHA and
pull-request flag tell Xping Cloud how far to trust that revision, and `IsCIEnvironment` marks the
build agent. If your suite only ever tests one deployment, leave `XPING_ENVIRONMENT` unset - runs
land in `Default` and the laptop and the pipeline share one bucket, which is the point.

To reuse the .NET hosting variable, pass it through explicitly - it is never read on its own. Do it
in the shell, so the value is read from the process the tests run in:

```yaml
- name: Run tests
  run: XPING_ENVIRONMENT="$ASPNETCORE_ENVIRONMENT" dotnet test
```

`${{ env.ASPNETCORE_ENVIRONMENT }}` will **not** do this. The `env` context only contains variables
declared in a workflow, job or step `env:` block - not ones set by the runner image, a container
`ENV`, or an earlier step - so it would expand to nothing in exactly the case you wanted it for.

### 4. Conditional Execution for PRs

Only track tests for main branches and pull requests:

```yaml
- name: Run tests with Xping
  if: github.ref == 'refs/heads/main' || github.event_name == 'pull_request'
  env:
    XPING_ENABLED: true
  run: dotnet test
```

### 5. Handle Network Failures Gracefully

Xping SDK includes retry logic with exponential backoff, but you can add explicit handling:

```yaml
- name: Run tests with Xping
  env:
    XPING_ENABLED: true
    XPING_MAXRETRIES: 5
    XPING_RETRYDELAY: "00:00:03"
  run: dotnet test
  continue-on-error: false  # Don't fail build if Xping upload fails
```

### 6. Use Configuration Profiles

Create environment-specific configurations:

**appsettings.CI.json:**
```json
{
  "Xping": {
    "Enabled": true,
    "BatchSize": 500,
    "FlushInterval": "00:01:00",
    "CaptureStackTraces": true
  }
}
```

Load in pipeline:
```yaml
env:
  DOTNET_ENVIRONMENT: CI
```

### 7. Monitor Build Time Impact

Track test execution time to ensure Xping overhead is minimal:

```yaml
- name: Run tests with Xping
  run: |
    START_TIME=$(date +%s)
    dotnet test
    END_TIME=$(date +%s)
    echo "Test duration: $((END_TIME - START_TIME)) seconds"
```

---

## Troubleshooting CI/CD Issues

### Tests not appearing in Xping Cloud

**Check these common issues:**

1. **Credentials not set**: Verify environment variables are accessible
   ```bash
   echo "API Key set: $([[ -n "$XPING_APIKEY" ]] && echo "Yes" || echo "No")"
   ```

2. **Network restrictions**: Ensure your CI environment can reach the upload endpoint
   ```bash
   curl -I https://upload.xping.io/v1
   ```

3. **Insufficient permissions**: Some CI systems restrict outbound network calls

4. **Build timeout**: Test process may be killed before flush completes

### Partial test data

If some tests are tracked but not all:

1. **Check assembly cleanup**: Ensure cleanup hooks run
2. **Increase flush interval**: Give more time for batch uploads
   ```yaml
   env:
     XPING_FLUSHINTERVAL: "00:02:00"
   ```

3. **Fail loudly instead of silently**: by default the SDK degrades quietly when it cannot upload.
   Set `XPING_STRICTMODE` to turn configuration and delivery problems into an error rather than a
   silent no-op:
   ```yaml
   env:
     XPING_STRICTMODE: "true"
   ```

### Performance degradation

If tests run slower in CI:

1. **Check network latency**: Uploads may be slower in CI
2. **Increase batch size**: Reduce number of API calls
   ```yaml
   env:
     XPING_BATCHSIZE: 500
   ```

3. **Use async mode**: Ensure async operations aren't blocking

---

## Advanced Configuration

### Merging Several Test Assemblies into One Project

By default each test assembly reports into its own Xping project. Set `XPING_PROJECTID` to collapse
them — useful in a monorepo where a dozen test projects are really one product:

```yaml
- name: Run tests
  env:
    XPING_APIKEY: ${{ secrets.XPING_APIKEY }}
    XPING_PROJECTID: payment-platform
  run: dotnet test
```

It is a hard pin: every execution in the session lands in that project regardless of which assembly
it came from. Leave it unset unless you specifically want that.

> Branch is captured automatically from CI metadata and does not need its own project. Splitting
> branches across projects fragments the history the confidence score depends on.

> The same applies to the environment name. Labelling pipeline runs `CI`, `BuildPipeline` or
> `PullRequestValidation` separates them from the identical runs on a developer's machine, which
> halves the evidence behind every score on both sides. `XPING_ENVIRONMENT` is for naming
> deployments - `Staging`, `Production` - and nothing else.

---

## Verification Checklist

After setting up CI/CD integration:

- [ ] Secrets/variables configured correctly
- [ ] Environment variables set in pipeline
- [ ] Test job runs successfully
- [ ] Tests appear in Xping Cloud
- [ ] CI metadata captured correctly (build number, commit SHA, etc.)
- [ ] Test execution time overhead is acceptable (<5%)
- [ ] Failed tests are tracked properly
- [ ] Retry logic works for transient failures

---

## Next Steps

- **[Configuration Reference](../configuration/configuration-reference.md)** - All configuration options
- **[Identifying Flaky Tests](../guides/working-with-tests/identifying-flaky-tests.md)** - Understanding CI test reliability
- **[Performance Overview](../guides/optimization/performance-overview.md)** - Understanding performance, optimization, and tuning settings
- **[Troubleshooting](../troubleshooting/common-issues.md)** - Common CI/CD issues

---

## Need Help?

- 📚 [Documentation](https://docs.xping.io)
- 💬 [Community Discussions](https://github.com/xping-dev/sdk-dotnet/discussions)
- 🐛 [Report an Issue](https://github.com/xping-dev/sdk-dotnet/issues)
- 📧 [Email Support](mailto:support@xping.io)

---

**Build with Confidence!** 🚀
