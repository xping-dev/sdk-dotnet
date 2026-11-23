---
uid: cloud-read-api
title: Read API
---

# Read API

The read API returns your Xping Cloud data as JSON: projects, tests, sessions, confidence scores and
flaky verdicts. Use it to build your own reports, or to bring Xping Cloud data into other tools.

The API only reads. It cannot change data. The SDK uploads through a separate endpoint.

The base URL is:

```text
https://api.xping.io
```

Every path on this page starts with `/v1`.

---

## Authentication

Send one credential with each request:

| Credential | Header | Plans |
|---|---|---|
| An API key with the `Read` scope | `X-API-Key: <key>` | Plans with API access. See [Plans and retention](plans-and-retention.md#features-that-need-a-plan). |
| A bearer token from `xping login` | `Authorization: Bearer <token>` | Every plan |

Create the API key in **Settings → API keys**. See [API keys](connect.md#api-keys). A key with only
the `Upload` scope cannot read.

A bearer token reads the workspace you chose when you signed in with `xping login`. It reads what you
can see in Xping Cloud.

A request with both headers is refused with `400`. A request with neither is refused with `401`:

```json
{
  "type": "https://httpstatuses.com/401",
  "title": "Error.Authentication.MissingCredentials",
  "status": 401,
  "detail": "The request carries no credentials. Send an X-API-Key header or an Authorization: Bearer header."
}
```

| Status | Title | Meaning |
|---|---|---|
| `400` | `Error.Authentication.AmbiguousCredentials` | The request has both an `X-API-Key` header and a bearer token. |
| `401` | `Error.Authentication.MissingCredentials` | The request has no credential. |
| `401` | | The API key or the token is not valid, or it expired. |
| `403` | | The API key has no `Read` scope, or the workspace plan has no API access. |

The data is always the data of the credential's workspace. No path names a workspace.

---

## Requests and responses

### Errors

An error response has the type `application/problem+json`. `title` names the error and `detail`
explains it:

```json
{
  "type": "https://httpstatuses.com/404",
  "title": "Error.Project.ProjectNotFoundByProjectKey",
  "status": 404,
  "detail": "Project with specified identifier='no-such-project' not found."
}
```

| Status | Meaning |
|---|---|
| `400` | A parameter or the body is not valid. `title` names the check that failed, for example `Validation.PageSize`. |
| `401` | No credential, or a credential that is not valid. See [Authentication](#authentication). |
| `403` | The credential cannot read this. |
| `404` | The project, test, session or score does not exist. |
| `409` | The project is turned off. Only `GET /v1/projects/{projectId}/tests` returns it. |
| `429` | The rate limit is reached. See [Rate limits](#rate-limits). |

### Rate limits

Xping Cloud limits the number of requests per credential: per minute, per hour, per day, and a
short burst limit over 10 seconds. A request must pass all four. The limits depend on your plan.

Each answered request carries these headers:

| Header | Value |
|---|---|
| `X-RateLimit-Limit` | The requests allowed per minute. |
| `X-RateLimit-Remaining` | The requests left in the current minute. |
| `X-RateLimit-Reset` | When the minute ends, in Unix seconds. |

A refused request gets `429`, the same headers with `X-RateLimit-Remaining: 0`, and a `Retry-After`
header with the number of seconds to wait. Wait that long before you send the next request.

A request without a valid credential counts against a smaller limit for its IP address.
`GET /v1/status` has no limit.

### Paging

Lists use one of three kinds of paging:

| Kind | Endpoints | How to get the next page |
|---|---|---|
| Page number | projects, tests of a project, sessions of a test, scores of a test, tests of a session | Send `pageNumber` + 1 while `hasNextPage` is `true`. |
| Cursor | test health | Send the `next` value as `cursor` until `next` is `null`. |
| Time | runs of a test | Send the `startedAt` of the last run as `before` while `hasMore` is `true`. |

A page-number list has this shape around its `items`:

| Field | Meaning |
|---|---|
| `items` | The page. |
| `totalCount` | The number of matches. |
| `isTotalCountExact` | `false` when there are more than 100,000 matches. `totalCount` is then the number you can page through. |
| `pageNumber`, `pageSize` | The page you got. |
| `totalPages`, `hasPreviousPage`, `hasNextPage` | Where the page is in the list. |

`pageSize` is 1 to 100, default 50. A value outside that range is moved into it. Paging stops after
100,000 rows: a `pageNumber` past that point returns `400`
`Error.Pagination.PageOutsidePagingRange`. Use the filters to narrow the list instead.

### Values

- Times are UTC, in ISO 8601: `2026-11-23T22:12:10.760496Z`. A time you send must carry its zone,
  for example a trailing `Z`.
- Durations are `hh:mm:ss.fffffff` strings, for example `00:00:00.0940750`. In runs of a test, the
  duration is `durationMs`, a number of milliseconds.
- Scores and rates are numbers from 0 to 1.
- A field without a value is `null`. The digest and test health endpoints leave out a block that
  has nothing to say. Read a missing block as `null`.

### Two vocabularies

The endpoints use two spellings for the same values:

| Value | Projects, tests, sessions and scores | Digest, test health and runs |
|---|---|---|
| Score category | `Highly Reliable`, `Reliable`, `Moderately Reliable`, `Unreliable`, `Highly Unreliable`, `Insufficient Data` | `highly-reliable`, `reliable`, `moderately-reliable`, `unreliable`, `highly-unreliable`, `insufficient-data` |
| Evidence level | `Sparse`, `Adequate`, `Robust`, or `No Data` without a score | `sparse`, `adequate`, `robust` |
| Score trend | `Significant Improvement`, `Minor Improvement`, `Stable`, `Minor Degradation`, `Significant Degradation` | `significant-improvement`, `minor-improvement`, `stable`, `minor-degradation`, `significant-degradation` |
| Outcome | `Passed`, `Failed`, `Timeout`, `Skipped`, `Inconclusive`, `NotExecuted` | `passed`, `failed`, `timeout`, `skipped`, `inconclusive`, `not-executed` |

The session's `suiteConfidence.category` uses the enum name without spaces, for example
`InsufficientData` or `HighlyReliable`.

The digest and test health endpoints also use these values:

| Field | Values |
|---|---|
| `verdict` | `known-flaky`, `broken`, `unstable`, `passing`, `no-history` |
| `flaky.state` | `flaky`, `not-flaky`, `inconclusive`, `not-examined` |
| `flaky.category` | `timing`, `resource-contention`, `network`, `environment-specific`, `order-dependency`, `concurrency`, `external-dependency`, `state-management`, `isolation`, `unknown` |
| `flaky.severity` | `minor`, `moderate`, `major`, `critical` |
| `confidence.state` | `scored`, `insufficient-evidence`, `not-scored` |
| `freshness` | `current`, `pending`, `none` |
| `tier` | `canonical` (CI on the default branch), `speculative` (a pull request or another branch), `untracked` (outside CI, or no commit) |
| `lastTransition.type` | `flakiness-introduced`, `flakiness-recovered`, `flakiness-worsened`, `flakiness-improved`, `reliability-degraded`, `reliability-recovered` |
| session `state` | `unknown`, `pending`, `processing`, `complete`, `abandoned`, `failed` |

`not-examined` and `inconclusive` do not mean "not flaky". See
[Score and flaky are independent](confidence-scores.md#score-and-flaky-are-independent).

### Fixed-window values

A confidence score, a factor value, an effective sample size and an evidence level each describe a
fixed window of recent runs. The digest and test health endpoints send that window with each value:

| `window.kind` | Fields | Meaning |
|---|---|---|
| `days` | `length`, `end` | The `length` days up to `end`. |
| `runs` | `length`, `oldestAt` | The newest `length` runs. The oldest of them started at `oldestAt`. |

Do not add or average these values over a date range of your own. The mean of a test's daily scores
is not a score for that range. See [Fixed-window values](confidence-scores.md#fixed-window-values).

### Identifiers

| Identifier | Where it comes from |
|---|---|
| `projectId` | The project's ID: the `ProjectId` you set in the SDK, or the one Xping Cloud made from the test assembly name, for example `billing-tests`. See [`ProjectId`](../configuration/configuration-reference.md#projectid). The session endpoints call it `projectSlug`. It has the same value. |
| `testFingerprint` | The SDK's 64-character SHA-256 fingerprint of the test. The `xping` CLI prints it in `--format json` output. |
| `sessionKey` | The SDK's GUID for one test run. |
| `scoreId` | The `id` of a score in the scores of a test. |

---

## Endpoints

| Method and path | Returns |
|---|---|
| [`GET /v1/status`](#get-status) | Whether the API is up |
| [`GET /v1/projects`](#list-projects) | The projects of the workspace |
| [`GET /v1/projects/{projectId}`](#get-a-project) | One project |
| [`GET /v1/projects/{projectId}/tests`](#list-the-tests-of-a-project) | The tests of a project |
| [`GET /v1/projects/{projectId}/tests/{testFingerprint}`](#get-a-test) | One test |
| [`GET /v1/projects/{projectId}/tests/{testFingerprint}/scores`](#list-the-scores-of-a-test) | The confidence score history of a test |
| [`GET /v1/scores/{scoreId}`](#get-a-score) | One confidence score |
| [`GET /v1/scores/{scoreId}/factors`](#get-the-factors-of-a-score) | The six factors behind a score |
| [`GET /v1/projects/{projectId}/tests/{testFingerprint}/runs`](#list-the-runs-of-a-test) | The newest runs of a test |
| [`GET /v1/projects/{projectId}/tests/{testFingerprint}/sessions`](#list-the-sessions-of-a-test) | The sessions that ran a test |
| [`GET /v1/projects/{projectSlug}/sessions/{sessionKey}`](#get-a-session) | One session |
| [`GET /v1/projects/{projectSlug}/sessions/{sessionKey}/tests`](#list-the-tests-of-a-session) | The tests of a session |
| [`GET /v1/projects/{projectSlug}/sessions/{sessionKey}/tests/{testFingerprint}`](#get-the-attempts-of-a-test-in-a-session) | Each attempt of a test in a session |
| [`POST /v1/projects/{projectId}/tests/digest`](#describe-a-batch-of-tests) | What Cloud knows about up to 100 tests |
| [`GET /v1/projects/{projectId}/tests/health`](#list-the-unhealthy-tests-of-a-project) | Every unhealthy test of a project |

The samples below come from a test project. Long lists are cut to one item.

---

### Get status

```http
GET /v1/status
```

Needs no credential and has no rate limit.

```json
{
  "service": "xping-datagateway",
  "version": "API V1 (1.0.0)",
  "status": "Healthy",
  "timestamp": "2026-11-23T22:07:56.556549Z"
}
```

---

### List projects

```http
GET /v1/projects
```

| Parameter | Meaning |
|---|---|
| `searchTerm` | Only projects whose name or description contains this text. |
| `lastSessionAfter` | Only projects with a session after this time. |
| `minFlakyTestCount`, `maxFlakyTestCount` | Only projects with this many flaky tests. |
| `sortBy` | `Name` (default), `LastSessionDate`, `MostProblems`, `FlakyTestCount` or `CreatedDate`. |
| `sortDescending` | `true` to sort in descending order. Default `false`. |
| `pageNumber`, `pageSize` | See [Paging](#paging). |

```json
{
  "items": [
    {
      "id": "sampleapp-xunit",
      "displayName": "SampleApp.XUnit",
      "slug": "sampleapp-xunit",
      "description": "Auto-provisioned from SDK upload",
      "totalTestCount": 16,
      "totalSessionCount": 16,
      "highlyReliableTestCount": 3,
      "reliableTestCount": 6,
      "moderatelyReliableTestCount": 0,
      "unreliableTestCount": 1,
      "highlyUnreliableTestCount": 5,
      "insufficientDataTestCount": 1,
      "flakyTestCount": 1,
      "needsAttentionTestCount": 6,
      "lastSessionAtUtc": "2026-11-23T22:12:10.613787Z",
      "createdAtUtc": "2026-11-11T15:30:41.920441Z"
    }
  ],
  "totalCount": 1,
  "pageNumber": 1,
  "pageSize": 50,
  "isTotalCountExact": true,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

The `…TestCount` fields count the project's tests by [score category](confidence-scores.md#score-categories).
`flakyTestCount` counts the tests flagged flaky. `needsAttentionTestCount` counts the tests that are
flaky, or that score Moderately Reliable or lower.

---

### Get a project

```http
GET /v1/projects/{projectId}
```

Returns one project, with the same fields as an item of [List projects](#list-projects).

---

### List the tests of a project

```http
GET /v1/projects/{projectId}/tests
```

| Parameter | Meaning |
|---|---|
| `isFlaky` | `true` for flaky tests only, `false` for tests not flagged flaky. Leave it out for all tests. |
| `scoreCategory` | `HighlyReliable`, `Reliable`, `ModeratelyReliable`, `Unreliable` or `HighlyUnreliable`. |
| `lastExecutedAfter` | Only tests that ran after this time. |
| `sortBy` | `TestName` (default), `LastExecutedAt`, `SuccessRate`, `OverallConfidenceScore`, `ScoreCategory`, `AverageDuration`, `TotalExecutions` or `CreatedDate`. |
| `sortDescending` | `true` to sort in descending order. Default `false`. |
| `pageNumber`, `pageSize` | See [Paging](#paging). |

The response holds the project and a page of its tests:

```json
{
  "project": {
    "id": "sampleapp-xunit",
    "isActive": true
  },
  "tests": {
    "items": [
      {
        "testFingerprint": "0c3a69872ca8dacd2ed19880f365e08fa9fa1b58b3d30c6a97febcbada726e11",
        "testName": "AnotherPassingTest",
        "className": "SampleTests",
        "namespace": "SampleApp.XUnit",
        "averageDuration": "00:00:00.0000130",
        "lastExecutedAtUtc": "2026-11-23T22:12:10.759715Z",
        "totalExecutions": 16,
        "successRate": 1,
        "confidenceScore": 0.9061125214468193,
        "scoreCategory": "Highly Reliable",
        "scoreDelta": 0.004903783273306113,
        "evidenceLevel": "Sparse",
        "scoreTrend": "Significant Improvement",
        "recommendation": "Limited data — collect more test executions to increase statistical confidence in the score.",
        "isEnabled": true,
        "isFlaky": null,
        "disableReason": "",
        "createdAtUtc": "2026-11-11T15:30:45.2431Z"
      }
    ],
    "totalCount": 16,
    "pageNumber": 1,
    "pageSize": 100,
    "isTotalCountExact": true,
    "totalPages": 1,
    "hasPreviousPage": false,
    "hasNextPage": false
  }
}
```

| Field | Meaning |
|---|---|
| `confidenceScore`, `scoreCategory`, `evidenceLevel` | The test's current [confidence score](confidence-scores.md). `null` while the test has Insufficient Data. |
| `scoreDelta` | The change from the previous score. |
| `scoreTrend` | The [trend](confidence-scores.md#trends), or `null` before the test has enough scores. |
| `isFlaky` | `true` when Xping Cloud flags the test flaky. `null` when flaky detection has not examined it. |
| `successRate` | The share of executions that passed. |
| `recommendation` | A short text about what to do next. |
| `isEnabled`, `disableReason` | Always `true` and empty. Turning a test off is not available. |

---

### Get a test

```http
GET /v1/projects/{projectId}/tests/{testFingerprint}
```

Returns one test, with the same fields as an item of
[List the tests of a project](#list-the-tests-of-a-project).

```json
{
  "testFingerprint": "d15b809bcb63e81076b075917b64faa6cbfcbd775731505f9c0d330c3c3648ba",
  "testName": "FlakyTest_EnvironmentState_FailsBasedOnSystemState",
  "className": "SampleTests",
  "namespace": "SampleApp.XUnit",
  "averageDuration": "00:00:00.0751230",
  "lastExecutedAtUtc": "2026-11-23T22:12:10.771436Z",
  "totalExecutions": 16,
  "successRate": 0.75,
  "confidenceScore": 0.590499339099653,
  "scoreCategory": "Unreliable",
  "scoreDelta": 0.014493065847895784,
  "evidenceLevel": "Sparse",
  "scoreTrend": "Significant Improvement",
  "recommendation": "Limited data — collect more test executions to increase statistical confidence in the score.",
  "isEnabled": true,
  "isFlaky": true,
  "disableReason": "",
  "createdAtUtc": "2026-11-11T15:30:45.428944Z"
}
```

---

### List the scores of a test

```http
GET /v1/projects/{projectId}/tests/{testFingerprint}/scores
```

Returns the test's confidence scores, newest first.

| Parameter | Meaning |
|---|---|
| `environmentName` | The scores of one environment. Leave it out for the overall scores. The match is exact and case-sensitive. |
| `pageNumber`, `pageSize` | See [Paging](#paging). |

```json
{
  "items": [
    {
      "id": "01M857F87JQ1QP3DGQMPNHG1F4",
      "reliability": {
        "score": 0.590499339099653,
        "category": "Unreliable",
        "evidenceLevel": "Sparse",
        "credibleLowerBound": 0.4220874837008285,
        "credibleUpperBound": 0.7369607600227017,
        "evidenceFloor": 0.3936187297021672,
        "credibleIntervalMass": 0.9
      },
      "trend": {
        "direction": "Significant Improvement",
        "delta": 0.014493065847895784
      },
      "context": {
        "environmentName": null,
        "sampleSize": 16,
        "calculatedAtUtc": "2026-11-23T22:12:21.87454Z"
      }
    }
  ],
  "totalCount": 11,
  "pageNumber": 1,
  "pageSize": 2,
  "isTotalCountExact": true,
  "totalPages": 6,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

| Field | Meaning |
|---|---|
| `id` | The score's ID. Use it with [Get a score](#get-a-score) and [Get the factors of a score](#get-the-factors-of-a-score). |
| `reliability.score`, `category`, `evidenceLevel` | The score. See [Confidence scores](confidence-scores.md). |
| `reliability.credibleLowerBound`, `credibleUpperBound` | The range the score is likely to be in. `credibleIntervalMass` is the share of likely values the range holds. |
| `reliability.evidenceFloor` | The lowest the score could be, allowing for the factors that could not be measured. |
| `trend.direction`, `trend.delta` | The trend, and the change from the previous score. |
| `context.environmentName` | The environment, or `null` for the overall score. |
| `context.sampleSize` | The runs the score read. |
| `context.calculatedAtUtc` | When Xping Cloud calculated the score. |

---

### Get a score

```http
GET /v1/scores/{scoreId}
```

Returns one score, with the same fields as an item of
[List the scores of a test](#list-the-scores-of-a-test).

---

### Get the factors of a score

```http
GET /v1/scores/{scoreId}/factors
```

Returns the six [factors](confidence-scores.md#the-six-factors) behind a score.

```json
{
  "factors": [
    {
      "name": "Pass Rate",
      "result": {
        "score": 0.6814229107625303,
        "sampleSize": 16,
        "unavailability": null,
        "withheldAllowance": null
      },
      "explanation": "Passed 12/16 times (68.1%, 48.7%-85.0% credible)",
      "warnings": [],
      "metadata": {
        "PassCount": "12",
        "FailCount": "4",
        "OverallPassRate": "0.75"
      }
    }
  ],
  "globalWarnings": [
    "Test runs in only 1 environment(s) - need at least 2 with 5+ effective runs each to assess consistency"
  ],
  "breakdownUnavailable": false
}
```

| Field | Meaning |
|---|---|
| `name` | The factor: `Pass Rate`, `Execution Stability`, `Retry Behavior`, `Environment Consistency`, `Failure Pattern` or `Concurrency Sensitivity`. |
| `result.score` | The factor's value from 0 to 1, or `null` when it could not be measured. |
| `result.unavailability` | Why the factor has no value: `InsufficientEvidence` (too few runs), `PowerGate` (runs, but not spread so the factor can compare them), `NotApplicable` (the factor does not apply, for example no retries) or `Failed`. `null` when the factor has a value. |
| `result.withheldAllowance` | How much the factor could have lowered the score if it had been measured. It sets `evidenceFloor`. |
| `result.sampleSize` | The runs the factor read. |
| `explanation` | What the factor found, in words. |
| `warnings` | Notes about the factor's data. |
| `metadata` | The values behind the factor, as strings. The keys can change. Do not depend on them. |
| `globalWarnings` | Notes about the score as a whole. |
| `breakdownUnavailable` | `true` when Xping Cloud cannot show the factors of this score. |

---

### List the runs of a test

```http
GET /v1/projects/{projectId}/tests/{testFingerprint}/runs
```

Returns the test's newest runs, newest first, from every branch and machine, within your plan's
retention. A run is one session's result for the test, read by its last attempt.

| Parameter | Meaning |
|---|---|
| `limit` | Runs wanted, 1 to 200. Default 50. |
| `before` | Only runs that started before this time. See [Paging](#paging). |

A page never ends between two runs that started at the same time, so it can hold more runs than
`limit`.

```json
{
  "retentionDays": 365,
  "hasMore": true,
  "runs": [
    {
      "sessionKey": "8d5c0309-86c5-4ec8-99ff-fd8c41c91e84",
      "startedAt": "2026-11-23T22:12:10.760496Z",
      "outcome": "passed",
      "attempts": 1,
      "firstAttemptOutcome": "passed",
      "durationMs": 94,
      "tier": "canonical",
      "isCi": true,
      "machineName": "ci-runner-07",
      "branch": "main",
      "commitSha": "4b825dc642cb6eb9a060e54bf8d69288fbee4904",
      "pullRequestNumber": null,
      "exceptionType": null,
      "errorMessageHash": null
    }
  ]
}
```

| Field | Meaning |
|---|---|
| `retentionDays` | How far back runs go on your plan. |
| `hasMore` | `true` when older runs exist. |
| `outcome`, `firstAttemptOutcome` | The result of the last attempt and of the first attempt. A run that failed first and passed on a retry shows `failed` and then `passed`. |
| `attempts` | How many attempts the run took. |
| `tier` | `canonical`, `speculative` or `untracked`. See [Two vocabularies](#two-vocabularies). |
| `errorMessageHash` | The SDK's hash of the error message. Runs with the same hash failed with the same error. |

---

### List the sessions of a test

```http
GET /v1/projects/{projectId}/tests/{testFingerprint}/sessions
```

Returns the sessions that ran the test, newest first.

| Parameter | Meaning |
|---|---|
| `status` | `Pending`, `Processing`, `Complete`, `Abandoned` or `Failed`. |
| `from`, `to` | Only sessions that started in this time range. |
| `branch` | Only sessions on this branch. |
| `pageNumber`, `pageSize` | See [Paging](#paging). |

```json
{
  "items": [
    {
      "sessionCorrelationKey": "8d5c0309-86c5-4ec8-99ff-fd8c41c91e84",
      "status": "Complete",
      "startedAtUtc": "2026-11-23T22:12:10.613787Z",
      "completedAtUtc": "2026-11-23T22:12:24.041141Z",
      "duration": "00:00:00.5950000",
      "branch": "main",
      "commitSha": "4b825dc642cb6eb9a060e54bf8d69288fbee4904",
      "pullRequestNumber": null,
      "platform": null,
      "environmentName": "CI",
      "framework": "xUnit"
    }
  ],
  "totalCount": 14,
  "pageNumber": 1,
  "pageSize": 1,
  "isTotalCountExact": true,
  "totalPages": 14,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

---

### Get a session

```http
GET /v1/projects/{projectSlug}/sessions/{sessionKey}
```

A run of several test projects shares one `sessionKey`. The project in the path names the one you
want.

```json
{
  "sessionCorrelationKey": "8d5c0309-86c5-4ec8-99ff-fd8c41c91e84",
  "status": "Complete",
  "startedAtUtc": "2026-11-23T22:12:10.613787Z",
  "completedAtUtc": "2026-11-23T22:12:24.041141Z",
  "duration": "00:00:00.5950000",
  "metrics": {
    "totalTests": 16,
    "totalPassed": 10,
    "totalFailed": 4,
    "totalTimeout": 1,
    "totalSkipped": 1,
    "successRate": 0.625,
    "totalAttempts": 16,
    "retryCount": 0,
    "uploadBatchCount": 2
  },
  "environment": {
    "framework": "xUnit",
    "name": "CI",
    "isCIEnvironment": true,
    "machineName": "ci-runner-07",
    "operatingSystem": "Ubuntu 24.04.1 LTS",
    "runtimeVersion": ".NET 10.0.14",
    "utcOffset": "+00:00",
    "timeZoneId": "Etc/UTC"
  },
  "vcsContext": {
    "platform": null,
    "serverUrl": null,
    "repositoryOwner": null,
    "repositoryName": null,
    "commitSha": "4b825dc642cb6eb9a060e54bf8d69288fbee4904",
    "branch": "main",
    "baseBranch": null,
    "pullRequestNumber": null,
    "author": "octocat"
  },
  "suiteConfidence": {
    "score": 0.417378365161034,
    "category": "Unreliable",
    "evidenceLevel": "Sparse",
    "coverage": 0.9375,
    "distribution": {
      "highlyReliable": 3,
      "reliable": 6,
      "moderatelyReliable": 0,
      "unreliable": 1,
      "highlyUnreliable": 5,
      "insufficientData": 1
    }
  }
}
```

| Field | Meaning |
|---|---|
| `metrics` | The counts of the session. `successRate` is passed tests divided by all tests. |
| `environment` | Where the session ran, as the SDK recorded it. See [Data Xping collects](../configuration/collected-data.md). |
| `vcsContext` | The commit, branch and pull request, as the SDK recorded them. |
| `suiteConfidence.score` | The [session reliability score](session-reliability-score.md). |
| `suiteConfidence.coverage` | The share of the session's tests that have a confidence score. |
| `suiteConfidence.distribution` | The session's tests by score category. |

---

### List the tests of a session

```http
GET /v1/projects/{projectSlug}/sessions/{sessionKey}/tests
```

Returns the tests that ran in the session, by test name. For a test with retries, only the last
attempt is included. Each item has the fields of
[List the tests of a project](#list-the-tests-of-a-project), without `isEnabled` and
`disableReason`.

| Parameter | Meaning |
|---|---|
| `pageNumber`, `pageSize` | See [Paging](#paging). |

---

### Get the attempts of a test in a session

```http
GET /v1/projects/{projectSlug}/sessions/{sessionKey}/tests/{testFingerprint}
```

Returns each attempt of the test in the session, first attempt first. It returns an empty list when
the test did not run in the session.

```json
[
  {
    "outcome": "Passed",
    "duration": "00:00:00.0940750",
    "timeoutBudget": null,
    "timeoutBudgetSource": null,
    "startTimeUtc": "2026-11-23T22:12:10.760496Z",
    "endTimeUtc": "2026-11-23T22:12:10.771436Z",
    "attemptNumber": 1,
    "failure": null,
    "retry": {
      "maxRetries": 0,
      "passedOnRetry": false,
      "retryReason": null,
      "retryAttributeName": "",
      "delayBetweenRetries": "00:00:00"
    },
    "context": {
      "environmentName": "CI",
      "wasParallelized": false,
      "concurrentTestCount": 1,
      "threadId": "17",
      "workerId": "Test collection for SampleApp.XUnit.SampleTests",
      "collectionName": "Test collection for SampleApp.XUnit.SampleTests",
      "positionInSuite": 8,
      "previousTestName": "SampleApp.XUnit.SampleTests.AnotherPassingTest",
      "previousTestOutcome": "Passed",
      "suiteElapsedTime": "00:00:00.0497790"
    },
    "classification": {
      "categories": [
        "Flaky"
      ],
      "tags": [
        "framework:xUnit"
      ],
      "customAttributes": {}
    }
  }
]
```

| Field | Meaning |
|---|---|
| `failure` | The exception type, where the test failed (`site`, for example `FixtureSetup`), and whether the SDK saw a transient or network error. `null` for a pass. |
| `retry` | The retry settings and result of this attempt. |
| `context` | How the attempt ran: parallel or not, the other tests at the same time, its position in the suite, and the test before it. |
| `classification` | The test's categories, tags and custom attributes, as the SDK recorded them. |

Xping Cloud stores `classification` and returns it here. It does not use it to filter, score or
detect flaky tests. It reads one tag itself: `framework:<name>`.

---

### Describe a batch of tests

```http
POST /v1/projects/{projectId}/tests/digest
```

Returns what Xping Cloud knows about up to 100 tests and 5 sessions in one request. The `xping` CLI
uses it for the [Cloud data in its report](../cli/cloud-data.md). It counts as one request against
the rate limit, whatever the number of tests.

The body:

```json
{
  "tests": [
    {
      "fingerprint": "d15b809bcb63e81076b075917b64faa6cbfcbd775731505f9c0d330c3c3648ba"
    },
    {
      "fingerprint": "0000"
    }
  ],
  "sessionKeys": [
    "8d5c0309-86c5-4ec8-99ff-fd8c41c91e84"
  ]
}
```

| Field | Rule |
|---|---|
| `tests` | 1 to 100 tests, each fingerprint once. |
| `tests[].fingerprint` | 1 to 128 characters. |
| `tests[].errorMessageHashes` | Optional. Up to 3 error message hashes you saw for the test, each 64 lowercase hexadecimal characters. Xping Cloud answers each one in the test's `signatures`. |
| `sessionKeys` | Optional. Up to 5 session keys, each once. |

The body can be at most 64 KB. A body that breaks a rule returns `400`, and `title` names the field.

The response answers the tests and the sessions in the order you sent them. A fingerprint the project
does not know is answered with `"state": "unknown"` and no other field:

```json
{
  "computedAt": "2026-11-23T22:12:38.349335Z",
  "signatureAlgorithm": "emh-1",
  "retentionDays": 365,
  "tests": [
    {
      "fingerprint": "d15b809bcb63e81076b075917b64faa6cbfcbd775731505f9c0d330c3c3648ba",
      "state": "ok",
      "testName": "SampleApp.XUnit.SampleTests.FlakyTest_EnvironmentState_FailsBasedOnSystemState",
      "lastExecutedAt": "2026-11-23T22:12:10.771436Z",
      "totalRuns": 16,
      "verdict": "known-flaky",
      "confidence": {
        "state": "scored",
        "score": 0.590499339099653,
        "lowerBound": 0.4220874837008285,
        "upperBound": 0.7369607600227017,
        "category": "unreliable",
        "trend": "significant-improvement",
        "delta": 0.014493065847895784,
        "effectiveSampleSize": 14.5,
        "evidenceLevel": "sparse",
        "canonicalRuns": 13,
        "speculativeRuns": 0,
        "untrackedRuns": 3,
        "window": {
          "kind": "days",
          "length": 35,
          "end": "2026-11-23T22:12:21.87454Z"
        },
        "evidenceWindow": {
          "kind": "days",
          "length": 14,
          "end": "2026-11-23T22:12:21.87454Z"
        },
        "computedAt": "2026-11-23T22:12:21.87454Z",
        "freshness": "current"
      },
      "flaky": {
        "state": "flaky",
        "category": "unknown",
        "severity": "moderate",
        "since": null,
        "lastDetectionAt": "2026-11-23T22:12:24.013434Z",
        "window": {
          "kind": "days",
          "length": 14,
          "end": "2026-11-23T22:12:24.013434Z"
        },
        "computedAt": "2026-11-23T22:12:24.013434Z",
        "freshness": "current"
      },
      "canonical": {
        "runs": 10,
        "failed": 3,
        "consecutiveFailures": 0,
        "failingSince": null,
        "latestOutcome": "passed",
        "latestAt": "2026-11-23T22:12:10.760496Z",
        "window": {
          "kind": "runs",
          "length": 10,
          "oldestAt": "2026-11-23T22:11:57.864206Z"
        }
      },
      "signatures": [
        {
          "errorMessageHash": "39526af4c9d88d3703be69368f6ea6762f134ad01f191b908ce083044d1a356b",
          "exceptionType": "Xunit.Sdk.TrueException",
          "requested": false,
          "runs": 4,
          "canonicalRuns": 4,
          "ciRuns": 4,
          "localRuns": 0,
          "machines": 1,
          "firstSeenAt": "2026-11-23T22:11:56.426928Z",
          "lastSeenAt": "2026-11-23T22:12:09.381707Z",
          "stackTraceHashes": [
            "ffa6618d1dfb7dd7ce485b41559bb73c0660f982b062e8c44508064ba2d38bbe"
          ],
          "window": {
            "kind": "days",
            "length": 35,
            "end": "2026-11-23T22:12:38.349335Z"
          }
        }
      ]
    },
    {
      "fingerprint": "0000",
      "state": "unknown"
    }
  ],
  "sessions": [
    {
      "sessionKey": "8d5c0309-86c5-4ec8-99ff-fd8c41c91e84",
      "state": "complete",
      "scored": true,
      "flakyExamined": true
    }
  ]
}
```

| Field | Meaning |
|---|---|
| `verdict` | The test on your default branch: `known-flaky`, `broken` (the newest run failed), `unstable` (a recent run failed), `passing` or `no-history`. |
| `confidence` | The newest overall score, with its range, its evidence and its windows. |
| `flaky` | The flaky state, category and severity, and the newest detection. |
| `canonical` | The newest runs on your default branch. Left out when the verdict is `no-history`. |
| `lastTransition` | The newest change of flaky state or score category. Left out when the test never changed. |
| `signatures` | The hashes you asked about, then the most frequent other errors of the test, with where they were seen. |
| `sessions[].scored`, `sessions[].flakyExamined` | `true` when Xping Cloud has scored the session, or run flaky detection on it. |
| `freshness` | `pending` when Xping Cloud has a newer upload that it has not processed yet. |
| `signatureAlgorithm` | The hash algorithm of the error message hashes. |

---

### List the unhealthy tests of a project

```http
GET /v1/projects/{projectId}/tests/health
```

Returns every test that Xping Cloud considers unhealthy: known flaky, broken or unstable. Each test
has the fields of [Describe a batch of tests](#describe-a-batch-of-tests). A healthy project returns
`"tests": []`.

| Parameter | Meaning |
|---|---|
| `cursor` | The `next` of the previous page. Leave it out for the first page. |
| `pageSize` | Tests per page, 1 to 500. Default 500. |
| `sessionKeys` | Up to 5 session keys, as in the digest. Repeat the parameter for each key. |

A page can hold fewer tests than `pageSize`, or none, when tests became healthy during the request.
Keep asking until `next` is `null`. `totalTests` counts the tests that ran within your plan's
retention.

```json
{
  "computedAt": "2026-11-23T22:12:38.249177Z",
  "signatureAlgorithm": "emh-1",
  "retentionDays": 365,
  "totalTests": 16,
  "tests": [
    {
      "fingerprint": "2227629e4a5f10740f535989431db21b5cef10401cd11684b7cf264a60b40e78",
      "state": "ok",
      "testName": "SampleApp.XUnit.SampleTests.TimeoutTest_UnresponsiveDependency_AwaitsForever",
      "lastExecutedAt": "2026-11-23T22:12:11.280994Z",
      "totalRuns": 16,
      "verdict": "broken",
      "confidence": {
        "state": "scored",
        "score": 0.143362092365893,
        "lowerBound": 0.03854837154283044,
        "upperBound": 0.29044294023392897,
        "category": "highly-unreliable",
        "trend": "significant-degradation",
        "delta": -0.007433382793669608,
        "effectiveSampleSize": 14.5,
        "evidenceLevel": "sparse",
        "canonicalRuns": 13,
        "speculativeRuns": 0,
        "untrackedRuns": 3,
        "window": {
          "kind": "days",
          "length": 35,
          "end": "2026-11-23T22:12:22.363346Z"
        },
        "evidenceWindow": {
          "kind": "days",
          "length": 14,
          "end": "2026-11-23T22:12:22.363346Z"
        },
        "computedAt": "2026-11-23T22:12:22.363346Z",
        "freshness": "current"
      },
      "flaky": {
        "state": "not-flaky",
        "category": null,
        "severity": null,
        "since": null,
        "lastDetectionAt": "2026-11-23T22:12:24.013602Z",
        "window": {
          "kind": "days",
          "length": 14,
          "end": "2026-11-23T22:12:24.013602Z"
        },
        "computedAt": "2026-11-23T22:12:24.013602Z",
        "freshness": "current"
      },
      "canonical": {
        "runs": 10,
        "failed": 10,
        "consecutiveFailures": 13,
        "failingSince": "2026-11-11T15:30:42.8471Z",
        "latestOutcome": "timeout",
        "latestAt": "2026-11-23T22:12:10.782998Z",
        "window": {
          "kind": "runs",
          "length": 10,
          "oldestAt": "2026-11-23T22:11:57.872457Z"
        }
      },
      "signatures": [
        {
          "errorMessageHash": "7e043604fd36937d5adde32ee5ca0a3b8a0a77e74c5bd00541fc233e07f22e60",
          "exceptionType": "Xunit.Sdk.TestTimeoutException",
          "requested": false,
          "runs": 16,
          "canonicalRuns": 13,
          "ciRuns": 15,
          "localRuns": 1,
          "machines": 2,
          "firstSeenAt": "2026-11-11T15:30:40.299258Z",
          "lastSeenAt": "2026-11-23T22:12:10.782998Z",
          "stackTraceHashes": [],
          "window": {
            "kind": "days",
            "length": 35,
            "end": "2026-11-23T22:12:38.249177Z"
          }
        }
      ]
    }
  ],
  "sessions": [
    {
      "sessionKey": "8d5c0309-86c5-4ec8-99ff-fd8c41c91e84",
      "state": "complete",
      "scored": true,
      "flakyExamined": true
    }
  ],
  "next": "MjIyNzYyOWU0YTVmMTA3NDBmNTM1OTg5NDMxZGIyMWI1Y2VmMTA0MDFjZDExNjg0YjdjZjI2NGE2MGI0MGU3OA"
}
```

---

## See also

- [Connect to Xping Cloud](connect.md): create the API key.
- [Confidence scores](confidence-scores.md): what the score values mean.
- [Xping Cloud data in the CLI](../cli/cloud-data.md): the same data in `xping report`.
