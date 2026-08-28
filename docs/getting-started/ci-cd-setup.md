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
          XPING_AUTODETECTCIENVIRONMENT: true
        run: dotnet test --no-build --configuration Release --logger "console;verbosity=detailed"
```

### Captured Metadata

Xping automatically captures:
- `GITHUB_ACTIONS` - CI environment indicator
- `GITHUB_RUN_ID` - Unique workflow run ID
- `GITHUB_RUN_NUMBER` - Sequential run number
- `GITHUB_SHA` - Commit SHA
- `GITHUB_REF` - Branch or tag ref
- `GITHUB_HEAD_REF` / `GITHUB_REF_NAME` - Normalized into `CI.Branch`
- `GITHUB_REPOSITORY` - Repository name
- `GITHUB_ACTOR` - User who triggered the workflow

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
    XPING_AUTODETECTCIENVIRONMENT: true
```

### Captured Metadata

Xping automatically captures:
- `TF_BUILD` - CI environment indicator
- `BUILD_BUILDID` - Unique build ID
- `BUILD_BUILDNUMBER` - Build number
- `BUILD_SOURCEVERSION` - Commit SHA
- `BUILD_SOURCEBRANCH` - Branch name
- `BUILD_REPOSITORY_NAME` - Repository name
- `BUILD_REQUESTEDFOR` - User who triggered the build

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
  XPING_AUTODETECTCIENVIRONMENT: "true"

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
- `CI_COMMIT_SHA` - Commit SHA
- `CI_COMMIT_BRANCH` / `CI_COMMIT_REF_NAME` - Normalized into `CI.Branch`
- `CI_PROJECT_PATH` - Repository path
- `GITLAB_USER_LOGIN` - User who triggered the pipeline

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
        XPING_AUTODETECTCIENVIRONMENT = 'true'
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

### Captured Metadata

Xping automatically captures:
- `JENKINS_URL` - CI environment indicator
- `BUILD_ID` - Unique build ID
- `BUILD_NUMBER` - Build number
- `GIT_COMMIT` - Commit SHA (if using Git)
- `GIT_BRANCH` - Branch name
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
      XPING_AUTODETECTCIENVIRONMENT: "true"
    
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
- `CIRCLE_SHA1` - Commit SHA
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

### 2. Enable Auto-Detection

Set `XPING_AUTODETECTCIENVIRONMENT: true` to automatically capture CI/CD metadata:

```yaml
env:
  XPING_AUTODETECTCIENVIRONMENT: true
```

### 3. Use Descriptive Environment Names

Override the auto-detected environment with a descriptive name:

```yaml
env:
  XPING_ENVIRONMENT: "Production-CI"
  # or
  XPING_ENVIRONMENT: "PR-${{ github.event.pull_request.number }}"
```

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
    "AutoDetectCIEnvironment": true,
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

### Custom CI Environment Label

If you want auto-detected CI runs grouped under a label other than the default `CI`, set `XPING_CIENVIRONMENTNAME`:

```yaml
env:
  XPING_CIENVIRONMENTNAME: "BuildPipeline"
```

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
