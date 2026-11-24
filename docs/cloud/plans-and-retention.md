---
uid: cloud-plans-and-retention
title: Plans and retention
---

# Plans and retention

Xping Cloud has five plans: **Trial**, **Basic**, **Pro**, **Team** and **Enterprise**. Each plan
includes every feature of the plans before it.

This page describes what the plans change in Xping Cloud's behaviour. For prices, limits and the full
feature list of each plan, see [xping.io/pricing](https://xping.io/pricing).

---

## Features that need a plan

Uploads, confidence scores and flaky detection work on every plan. These features need a plan:

| Feature | Plans | Without it |
|---|---|---|
| [PR comments](reading-the-pr-comment.md) | Team and Enterprise | Xping Cloud posts no comment, and you cannot connect a repository. |
| An API key with the `Read` scope, for the [read API](read-api.md) | Team and Enterprise | The `Read` scope is greyed out when you create a key. A `Read` key in a workspace without the feature is refused with `403`. |
| AI analysis of a confidence score, and AI summaries of flaky tests | Pro, Team and Enterprise | The test page shows the rule-based text. |

The `xping` CLI reads Xping Cloud data on every plan after `xping login`. See
[Xping Cloud data in the CLI](../cli/cloud-data.md).

AI session summaries work on every plan.

---

## Monthly limits

Each plan has monthly limits, which [xping.io/pricing](https://xping.io/pricing) lists. When a
workspace reaches one:

- **Test results.** Xping Cloud refuses uploads until the first day of the next month. The SDK logs
  the refusal. Your test run itself does not fail, unless
  [`StrictMode`](../configuration/configuration-reference.md#strictmode) is on.
- **AI analyses and summaries.** Xping Cloud shows the rule-based text, and PR comments show no ✨
  line, until the next month.
- **Projects.** Xping Cloud refuses an upload that would create a new project. Set
  [`ProjectId`](../configuration/configuration-reference.md#projectid) to send several test
  assemblies to one project.

The limits on Pro and Team grow with the number of seats.

---

## Retention

Each plan keeps your data for a fixed number of days. Trial and Basic keep it the shortest time,
Pro keeps it longer, and Team and Enterprise keep it the longest. See
[xping.io/pricing](https://xping.io/pricing) for the days.

When a test session is older than your plan's retention, Xping Cloud deletes it, with its test
executions and its uploads. It also deletes PR comment data whose newest run is that old. A test's
confidence scores and flaky state stay.

Confidence scores read about the last five weeks of runs. Every plan keeps runs for at least that
long. See [Confidence scores](confidence-scores.md#when-a-test-gets-a-score).

The [read API](read-api.md) returns your plan's retention as `retentionDays`.

---

## Team members

Trial and Basic have one member. Pro, Team and Enterprise allow any number of members, at no cost.
Members do not use seats. See [Roles](connect.md#roles).

---

## How committers are counted

On Pro and Team, you pay per seat. A seat covers one **active committer**: a person whose CI runs
uploaded results to the workspace in the last 30 days.

- **Who counts.** For a CI run, the committer is the pull request's author. Without a pull request,
  it is the CI user who started the run.
- **One committer per CI platform and login.** Xping Cloud compares logins without case, so
  `Octocat` and `octocat` are one committer. The same person on two CI platforms counts twice.
- **Local runs never count.** Only runs that the SDK recorded in CI count.
- **Bots never count.** For example GitHub Apps (`…[bot]`), Dependabot, Renovate, Azure DevOps build
  service accounts and GitLab access token users.
- **The email does not change the count.** The SDK can send the CI user's email
  ([`CollectCiActorEmail`](../configuration/configuration-reference.md#collectciactoremail)). Xping
  Cloud uses it only to link the committer to a member. Linking a committer to a member does not
  change the count either.
- **Xping Cloud keeps a committer for 90 days** after their last upload, and then deletes the record.

More active committers than seats never refuses an upload. Owners and Admins see the count in
**Settings → Subscription**.

---

## See also

- [Connect to Xping Cloud](connect.md)
- [xping.io/pricing](https://xping.io/pricing)
