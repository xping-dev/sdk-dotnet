# CI/CD Integration Guide

Learn how to integrate Xping SDK into your CI/CD pipelines for continuous test reliability monitoring. This guide covers the most popular CI/CD platforms and best practices.

---

## Overview

Xping SDK automatically detects CI/CD environments and captures relevant metadata like build numbers, commit SHAs, and branch names. This enables you to:

- **Track test reliability across builds**
- **Detect flaky tests in your pipeline**
- **Correlate test failures with specific commits**
- **Monitor test performance trends over time**

---

## Quick Setup (All Platforms)

The basic setup is the same across all CI/CD platforms:

1. **Get your API key** from [Xping Dashboard](https://app.xping.io): **Account** → **Settings** → **API & Integration** → **Create API Key**
2. **Choose a Project ID** - any meaningful identifier for your project (e.g., `"my-app"`, `"payment-service"`)
3. **Store credentials as secrets/variables** in your CI/CD platform
4. **Set environment variables in your pipeline**
5. **Run your tests normally** - Xping SDK handles the rest

> **Note:** The Project ID is user-defined and doesn't need to exist beforehand. Xping automatically creates the project in your workspace when tests first run, requiring only that the name is unique within your workspace.

---

## GitHub Actions

### Configuration

Store your Xping credentials as GitHub Secrets:

1. Go to **Repository Settings** → **Secrets and variables** → **Actions**
2. Add the following secrets:
   - `XPING_API_KEY`: Your Xping API key (from Account → Settings → API & Integration)
   - `XPING_PROJECT_ID`: Your chosen project identifier (e.g., `"my-app"`)

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
          dotnet-version: '8.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore --configuration Release
      
      - name: Run tests with Xping
        env:
          XPING__APIKEY: ${{ secrets.XPING_API_KEY }}
          XPING__PROJECTID: ${{ secrets.XPING_PROJECT_ID }}
          XPING__ENABLED: true
          XPING__AUTODETECTCIENVIRONMENT: true
        run: dotnet test --no-build --configuration Release --logger "console;verbosity=detailed"
```

### Captured Metadata

Xping automatically captures:
- `GITHUB_ACTIONS` - CI environment indicator
- `GITHUB_RUN_ID` - Unique workflow run ID
- `GITHUB_RUN_NUMBER` - Sequential run number
- `GITHUB_SHA` - Commit SHA
- `GITHUB_REF` - Branch or tag ref
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
   - `XPING.ProjectId`: Your chosen project identifier (e.g., `"my-app"`)

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
    version: '8.0.x'

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
    XPING__APIKEY: $(XPING.ApiKey)
    XPING__PROJECTID: $(XPING.ProjectId)
    XPING__ENABLED: true
    XPING__AUTODETECTCIENVIRONMENT: true
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
   - `XPING_API_KEY`: Your Xping API key (mark as masked)
   - `XPING_PROJECT_ID`: Your chosen project identifier (e.g., `"my-app"`)

### Pipeline Example (.gitlab-ci.yml)

```yaml
image: mcr.microsoft.com/dotnet/sdk:8.0

stages:
  - build
  - test

variables:
  XPING__ENABLED: "true"
  XPING__AUTODETECTCIENVIRONMENT: "true"

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
    XPING__APIKEY: $XPING_API_KEY
    XPING__PROJECTID: $XPING_PROJECT_ID
```

### Captured Metadata

Xping automatically captures:
- `GITLAB_CI` - CI environment indicator
- `CI_PIPELINE_ID` - Unique pipeline ID
- `CI_JOB_ID` - Job ID
- `CI_COMMIT_SHA` - Commit SHA
- `CI_COMMIT_REF_NAME` - Branch or tag name
- `CI_PROJECT_PATH` - Repository path
- `GITLAB_USER_LOGIN` - User who triggered the pipeline

---

## Jenkins

### Configuration

Store your Xping credentials using Jenkins Credentials:

1. Go to **Manage Jenkins** → **Credentials**
2. Add **Secret text** credentials:
   - ID: `xping-api-key`, Secret: Your Xping API key
   - ID: `xping-project-id`, Secret: Your chosen project identifier (e.g., `"my-app"`)

### Pipeline Example (Jenkinsfile)

```groovy
pipeline {
    agent any
    
    environment {
        XPING__APIKEY = credentials('xping-api-key')
        XPING__PROJECTID = credentials('xping-project-id')
        XPING__ENABLED = 'true'
        XPING__AUTODETECTCIENVIRONMENT = 'true'
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
   - `XPING_API_KEY`: Your Xping API key
   - `XPING_PROJECT_ID`: Your chosen project identifier (e.g., `"my-app"`)

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
      XPING__APIKEY: $XPING_API_KEY
      XPING__PROJECTID: $XPING_PROJECT_ID
      XPING__ENABLED: "true"
      XPING__AUTODETECTCIENVIRONMENT: "true"
    
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

## Best Practices

### 1. Always Use Secrets for Credentials

**✅ Do:**
```yaml
env:
  XPING__APIKEY: ${{ secrets.XPING_API_KEY }}
```

**❌ Don't:**
```yaml
env:
  XPING__APIKEY: "pk_live_1234567890abcdef"  # Never hardcode!
```

### 2. Enable Auto-Detection

Set `XPING__AUTODETECTCIENVIRONMENT: true` to automatically capture CI/CD metadata:

```yaml
env:
  XPING__AUTODETECTCIENVIRONMENT: true
```

### 3. Use Descriptive Environment Names

Override the auto-detected environment with a descriptive name:

```yaml
env:
  XPING__ENVIRONMENT: "Production-CI"
  # or
  XPING__ENVIRONMENT: "PR-${{ github.event.pull_request.number }}"
```

### 4. Conditional Execution for PRs

Only track tests for main branches and pull requests:

```yaml
- name: Run tests with Xping
  if: github.ref == 'refs/heads/main' || github.event_name == 'pull_request'
  env:
    XPING__ENABLED: true
  run: dotnet test
```

### 5. Handle Network Failures Gracefully

Xping SDK includes retry logic with exponential backoff, but you can add explicit handling:

```yaml
- name: Run tests with Xping
  env:
    XPING__ENABLED: true
    XPING__MAXRETRIES: 5
    XPING__RETRYDELAY: "00:00:03"
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

### Tests not appearing in dashboard

**Check these common issues:**

1. **Credentials not set**: Verify environment variables are accessible
   ```bash
   echo "API Key set: $([[ -n "$XPING__APIKEY" ]] && echo "Yes" || echo "No")"
   ```

2. **Network restrictions**: Ensure your CI environment can reach `api.xping.io`
   ```bash
   curl -I https://api.xping.io/health
   ```

3. **Insufficient permissions**: Some CI systems restrict outbound network calls

4. **Build timeout**: Test process may be killed before flush completes

### Partial test data

If some tests are tracked but not all:

1. **Check assembly cleanup**: Ensure cleanup hooks run
2. **Increase flush interval**: Give more time for batch uploads
   ```yaml
   env:
     XPING__FLUSHINTERVAL: "00:02:00"
   ```

3. **Review logs**: Enable detailed logging
   ```yaml
   env:
     XPING__LOGLEVEL: "Debug"
   ```

### Performance degradation

If tests run slower in CI:

1. **Check network latency**: Uploads may be slower in CI
2. **Increase batch size**: Reduce number of API calls
   ```yaml
   env:
     XPING__BATCHSIZE: 500
   ```

3. **Use async mode**: Ensure async operations aren't blocking

---

## Advanced Configuration

### Custom Metadata

Add custom properties in CI environment:

```yaml
env:
  XPING__CUSTOMENVIRONMENT: |
    {
      "BuildAgent": "${{ runner.name }}",
      "Executor": "${{ github.actor }}",
      "PullRequest": "${{ github.event.pull_request.number }}"
    }
```

### Sampling for Large Test Suites

For very large test suites (>10,000 tests), use sampling:

```yaml
env:
  XPING__SAMPLINGRATE: 0.1  # Track 10% of tests
```

### Separate Projects per Branch

Track different branches in different Xping projects:

```yaml
- name: Set Xping Project
  run: |
    if [ "${{ github.ref }}" == "refs/heads/main" ]; then
      echo "XPING__PROJECTID=${{ secrets.XPING_PROJECT_MAIN }}" >> $GITHUB_ENV
    else
      echo "XPING__PROJECTID=${{ secrets.XPING_PROJECT_DEV }}" >> $GITHUB_ENV
    fi

- name: Run tests
  env:
    XPING__APIKEY: ${{ secrets.XPING_API_KEY }}
  run: dotnet test
```

---

## Verification Checklist

After setting up CI/CD integration:

- [ ] Secrets/variables configured correctly
- [ ] Environment variables set in pipeline
- [ ] Test job runs successfully
- [ ] Tests appear in Xping dashboard
- [ ] CI metadata captured correctly (build number, commit SHA, etc.)
- [ ] Test execution time overhead is acceptable (<5%)
- [ ] Failed tests are tracked properly
- [ ] Retry logic works for transient failures

---

## Next Steps

- **[Configuration Reference](../configuration/configuration-reference.md)** - All configuration options
- **[Flaky Test Detection](../guides/flaky-test-detection.md)** - Understanding CI test reliability
- **[Performance Tuning](../guides/performance-tuning.md)** - Optimize for large builds
- **[Troubleshooting](../troubleshooting/common-issues.md)** - Common CI/CD issues

---

## Need Help?

- 📚 [Documentation](https://docs.xping.io)
- 💬 [Community Discussions](https://github.com/xping-dev/sdk-dotnet/discussions)
- 🐛 [Report an Issue](https://github.com/xping-dev/sdk-dotnet/issues)
- 📧 [Email Support](mailto:support@xping.io)

---

**Build with Confidence!** 🚀
