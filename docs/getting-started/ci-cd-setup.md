---
uid: getting-started-ci-cd-setup
title: Set up CI
---

# Set up CI

This page shows how to upload test results to Xping Cloud from a CI pipeline, and how to fail a
build on what `xping report` finds. It is for developers who already have the SDK in a test
project. If you do not, start with a quick start for
[NUnit](quickstart-nunit.md), [xUnit](quickstart-xunit.md) or [MSTest](quickstart-mstest.md).

---

## What every pipeline needs

1. In Xping Cloud, open **Settings → API keys** and create a key. Create it with only the Upload
   scope. See [API keys](../cloud/connect.md#api-keys).
2. Store the key as a secret in your CI platform. Do not store it as a plain variable, and do not
   put it in `appsettings.json`.
3. Pass the secret to the test step as the `XPING_APIKEY` environment variable.
4. Run `dotnet test` as usual.

You do not name a project. Xping Cloud creates one project for each test assembly, on the first
upload. To send several test assemblies to one project, set `XPING_PROJECTID`. See
[ProjectId](../configuration/configuration-reference.md#projectid).

The SDK detects the CI platform and records the build, the branch, the commit and the person behind
the run. You do not configure this. [Data Xping collects](../configuration/collected-data.md#ci-metadata)
lists each field and the variable it comes from.

**A failed upload does not fail the build.** The SDK logs the error, and your tests pass or fail as
usual. To fail the build when the upload fails, turn on
[strict mode](../configuration/configuration-reference.md#strictmode).

**To see the SDK's lines in a CI log**, run `dotnet test` with
`--logger "console;verbosity=normal"`. On xUnit v3, run the test project with `dotnet run` instead.

---

## Supported platforms

| Platform | Detected | PR context | Setup |
|---|---|---|---|
| GitHub Actions | Yes | Yes | [GitHub Actions](#github-actions) |
| Azure Pipelines | Yes | Yes, for Azure Repos and GitHub repositories | [Azure Pipelines](#azure-pipelines) |
| GitLab CI/CD | Yes | Yes, in merge request pipelines | [GitLab CI/CD](#gitlab-cicd) |
| Jenkins | Yes | Yes, for multibranch PR builds | [Jenkins](#jenkins) |
| CircleCI | Yes | No | [CircleCI](#circleci) |
| Travis CI, TeamCity, Bitbucket Pipelines, AppVeyor | Yes | No | [Other platforms](#other-platforms) |
| Any CI that sets `CI=true` | Yes | No | [Other platforms](#other-platforms) |

**PR context** is the pull request a build belongs to: its number, branches and head commit. With
it, Xping Cloud files the run under the PR, and can post a
[pull request comment](../cloud/reading-the-pr-comment.md). Travis CI, TeamCity, Bitbucket
Pipelines, AppVeyor and a generic CI upload without PR context. They are not targeted for it.

---

## GitHub Actions

Add the key under **Settings → Secrets and variables → Actions** in your repository, as a secret
named `XPING_APIKEY`.

```yaml
name: Test

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Run tests
        env:
          XPING_APIKEY: ${{ secrets.XPING_APIKEY }}
        run: dotnet test --configuration Release --logger "console;verbosity=normal"
```

**Tests in a container.** When the tests run in `docker run`, pass the `GITHUB_*` variables,
including `GITHUB_SERVER_URL` and `GITHUB_EVENT_NAME`. Also mount the runner's temp folder, so that
the file at `GITHUB_EVENT_PATH` is readable. Without them, the SDK still records the run, but it
skips the PR context and logs a warning. Without `GITHUB_EVENT_NAME`, Xping Cloud also files the
run under no commit.

---

## Azure Pipelines

Create a variable group named `Xping` under **Pipelines → Library**, add a variable `XpingApiKey`
and mark it as secret. Azure Pipelines does not pass a secret to a step by itself, so map it in the
step's `env:`.

```yaml
trigger:
  branches:
    include:
      - main

pool:
  vmImage: 'ubuntu-latest'

variables:
  - group: Xping

steps:
  - task: UseDotNet@2
    inputs:
      version: '10.0.x'

  - script: dotnet test --configuration Release --logger "console;verbosity=normal"
    displayName: 'Run tests'
    env:
      XPING_APIKEY: $(XpingApiKey)
```

On a pull request build, the SDK records the PR context for repositories in Azure Repos and on
GitHub. Repositories on other providers get no PR context.

---

## GitLab CI/CD

Add the key under **Settings → CI/CD → Variables**, as a masked variable named `XPING_APIKEY`.
GitLab passes it to every job as an environment variable.

```yaml
image: mcr.microsoft.com/dotnet/sdk:10.0

test:
  stage: test
  script:
    - dotnet test --configuration Release --logger "console;verbosity=normal"
```

In a merge request pipeline, the SDK records the MR context, on gitlab.com and on a self-managed
instance. A branch pipeline gets no MR context.

---

## Jenkins

Add the key under **Manage Jenkins → Credentials**, as a **Secret text** credential with the ID
`xping-api-key`.

```groovy
pipeline {
    agent any

    environment {
        XPING_APIKEY = credentials('xping-api-key')
    }

    stages {
        stage('Test') {
            steps {
                sh 'dotnet test --configuration Release --logger "console;verbosity=normal"'
            }
        }
    }
}
```

**Pull request builds.** On a multibranch pipeline, the SDK records the PR context for GitHub,
GitLab and Azure DevOps repositories, with the merge strategy and with the head strategy. Jenkins has
no variable for the PR head commit when it merges the PR, so the SDK reads it from the `.git`
folder of the workspace:

- Run the tests in the checked-out workspace. With `skipDefaultCheckout`, run `checkout scm` before
  the test stage.
- Keep `git` on the agent's `PATH`. The SDK runs `git rev-parse` when `git gc` has packed the
  commit.
- Shallow clones work.

GitHub Pull Request Builder (GHPRB) jobs are marked as PR builds, but get no PR context.

---

## CircleCI

Add the key under **Project Settings → Environment Variables**, as `XPING_APIKEY`. CircleCI passes
it to every job.

```yaml
version: 2.1

jobs:
  test:
    docker:
      - image: mcr.microsoft.com/dotnet/sdk:10.0
    steps:
      - checkout
      - run:
          name: Run tests
          command: dotnet test --configuration Release --logger "console;verbosity=normal"

workflows:
  test:
    jobs:
      - test
```

---

## Other platforms

On these platforms, store the key as a secret environment variable named `XPING_APIKEY`, then run
`dotnet test`. The SDK detects the platform and records its build metadata.

| Platform | Where to store the key |
|---|---|
| Travis CI | Repository **Settings → Environment Variables**, with **Display value in build log** off |
| TeamCity | A build parameter `env.XPING_APIKEY` of type **Password** |
| Bitbucket Pipelines | **Repository settings → Repository variables**, with **Secured** on |
| AppVeyor | A secure variable in `appveyor.yml` (`XPING_APIKEY: { secure: … }`), or in the project's **Environment** settings |
| Any other CI | Any secret that the CI passes to the test step as an environment variable. The SDK detects the CI when `CI=true` is set. |

On TeamCity and a generic CI, the SDK cannot tell a pull request build from a push. Xping Cloud
then files no run from them under a commit. See
[Which commit is recorded](../configuration/collected-data.md#which-commit-is-recorded).

---

## Fail the build on findings

`xping report --fail-on <severity>` exits with code `1` when a finding of that severity or higher
appears in any test assembly. Add it as a step after the tests. It reads the local store that the
SDK wrote in the same job, so it works with or without an API key.

```yaml
- name: Install the Xping CLI
  run: dotnet tool install -g Xping.Cli --prerelease

- name: Run tests
  env:
    XPING_APIKEY: ${{ secrets.XPING_APIKEY }}
  run: dotnet test --configuration Release

- name: Check findings
  if: always()
  run: xping report --fail-on high
```

- `--fail-on` takes `high`, `medium` or `low`.
- The CLI needs the .NET 10 SDK or runtime on the agent. The SDK packages do not.
- The step reads only the runs in the local store on that agent. A new CI agent starts with an
  empty store. `xping report` shows findings only after 5 runs, so on an agent that keeps no store
  between jobs, `--fail-on` does not fail.
- When the store has no runs at all, for example because the SDK did not record, `xping report`
  exits with code `2`.

[Exit codes](../cli/command-reference.md#exit-codes) lists every code. These options help in a CI
log:

```bash
xping report --summary            # one line for each assembly
xping report --format json        # the findings as JSON, for a script or a coding agent
xping report --no-color --ascii   # for log viewers that do not show color codes
```

### Add Xping Cloud data to the report

`xping report` adds Xping Cloud's confidence for each test when its key can read Xping Cloud data.
A key with only the Upload scope cannot. In a pipeline, create a second key with the Read scope, and
pass it only to the report step. Reading also needs a plan that includes API access. See
[Credentials](../cli/cloud-data.md#what-you-need).


```yaml
- name: Check findings
  if: always()
  env:
    XPING_APIKEY: ${{ secrets.XPING_READ_APIKEY }}
  run: xping report --fail-on high
```

`xping login` does not work in a pipeline: it needs a terminal, and it does not run when `CI` is
set. When the key cannot read, the report says so on stderr and shows only local data. Xping Cloud
data never changes the exit code. See [Credentials](../cli/cloud-data.md#what-you-need).

---

## The default branch and canonical branches

Xping Cloud counts a CI push to the default branch as a Canonical run. Canonical runs are the
strongest evidence behind a confidence score. A push to one of the project's canonical branches,
such as `release/*`, counts too. You set the canonical branch patterns in the project's settings in
Xping Cloud. A pull request build never counts as Canonical, whatever its branch.

GitHub Actions and GitLab CI/CD report the default branch. On other platforms, Xping Cloud uses the
project's default branch setting, then `main` or `master`. To name the default branch from the
pipeline, set `XPING_DEFAULTBRANCH`:

```yaml
- script: dotnet test --configuration Release
  env:
    XPING_APIKEY: $(XpingApiKey)
    XPING_DEFAULTBRANCH: develop
```

The value replaces what the platform reports, and wins over the project's setting. See
[DefaultBranch](../configuration/configuration-reference.md#defaultbranch).

---

## Environments and projects

**Name environments after deployments.** Set `XPING_ENVIRONMENT` only when a pipeline tests a
different deployment, such as `Staging`. Xping Cloud scores each test for each environment. A name
that changes from run to run, such as `PR-123` or `CI`, splits each test's history. The SDK already
records whether a run is on CI and which commit and branch it tested. When you test one deployment,
leave `XPING_ENVIRONMENT` unset: CI runs and local runs then share the `Default` environment.

The SDK does not read `ASPNETCORE_ENVIRONMENT`. To reuse it, pass it in the shell of the test step:

```yaml
- name: Run tests
  shell: bash
  run: XPING_ENVIRONMENT="$ASPNETCORE_ENVIRONMENT" dotnet test
```

On GitHub Actions, `${{ env.ASPNETCORE_ENVIRONMENT }}` does not work for this. The `env` context
holds only the variables that the workflow declares, not the ones the runner image or a container
sets.

**Use one project for one product.** With `XPING_PROJECTID`, every execution in the session goes to
that project, whatever its assembly. Do not split branches or environments into projects: the SDK
records both, and separate projects split the history that the confidence score needs.

---

## Troubleshooting

**No results in Xping Cloud:**

1. Check that the test step gets the key:

   ```bash
   echo "API key set: $([ -n "$XPING_APIKEY" ] && echo yes || echo no)"
   ```

2. Run `dotnet test` with `--logger "console;verbosity=normal"`. On xUnit v3, run the test project
   with `dotnet run` instead. The SDK's start line says
   `Initialized in local-only mode` when it has no key. With a key, it prints one line for each
   upload that Xping Cloud accepts, or an error that says why the upload failed.
3. Check that the agent can reach the upload endpoint:

   ```bash
   curl -I https://upload.xping.io/v1
   ```

**Some results are missing:**

- Check that the session ends. On NUnit, the `[OneTimeTearDown]` method must call
  `FinalizeAndShutdownAsync`. On MSTest, the `[AssemblyCleanup]` method must call it. See the
  quick starts.
- On MSTest with vstest, a session that no `[AssemblyCleanup]` method ends often loses its upload.
  See [Known limitations](../troubleshooting/known-limitations.md#mstest-a-session-finalized-by-the-process-exit-safety-net-loses-its-upload-on-vstest).
- To fail the build when an upload fails, set `XPING_STRICTMODE: "true"`.

[Common issues](../troubleshooting/common-issues.md) covers more problems.

---

## See also

- [Data Xping collects](../configuration/collected-data.md)
- [Configuration reference](../configuration/configuration-reference.md)
- [CLI reference](../cli/command-reference.md)
- [Reading the PR comment](../cloud/reading-the-pr-comment.md)
