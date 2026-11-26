---
uid: cloud-connect
title: Connect to Xping Cloud
---

# Connect to Xping Cloud

This page covers the three connections between your work and Xping Cloud:

- an **API key**, so the SDK can upload your test runs;
- **roles**, which control what each person in your workspace can do;
- a **repository connection**, so Xping Cloud can comment on your pull requests.

To read Xping Cloud data from the `xping` CLI, sign in with `xping login`. See
[Xping Cloud data in the CLI](../cli/cloud-data.md#what-you-need).

---

## API keys

An API key lets a program act for your workspace. The SDK uses one to upload test runs. The
[read API](read-api.md) accepts one to read data.

### Create a key

Only an Owner or an Admin can create a key. Open **Settings → API keys** and create a key with:

- **Name**: how you recognize the key in the list.
- **Description**: optional.
- **Permissions**: the scopes of the key. See [Scopes](#scopes).
- **Expiration**: never, 30 days, 60 days, 90 days, or a date you choose.
- **Allowed addresses**: optional. One IP address or CIDR range per line. The key then works only
  from these addresses.

Xping Cloud shows the key once, when you create it. Copy it then. You cannot see it again.

### Scopes

| Scope | What the key can do | Plans |
|---|---|---|
| `Upload` | Upload test runs from the SDK. | Every plan |
| `Read` | Read projects, tests, runs and scores through the [read API](read-api.md). | Plans with API access. See [Plans and retention](plans-and-retention.md#features-that-need-a-plan). |

Create the SDK's key with only the Upload scope. A CI log or a test machine can leak the key. A key
with only the Upload scope cannot read your data.

### Use the key in the SDK

Set the key as the `XPING_APIKEY` environment variable, or as a secret in your CI:

```bash
export XPING_APIKEY="your-api-key"
```

With a key, the SDK uploads to Xping Cloud. Without one, it keeps results on your machine. See
[`ApiKey`](../configuration/configuration-reference.md#apikey) and
[Set up CI](../getting-started/ci-cd-setup.md).

### Revoke a key

An Owner or an Admin can revoke or delete a key in **Settings → API keys**. A program that uses a
revoked key is refused.

A key belongs to the workspace, not to the person who created it. It keeps working when that person
leaves the workspace. The list marks such a key "no longer a member", so you can decide whether to
revoke it.

---

## Roles

Each member of a workspace has one role.

| Role | What it is for |
|---|---|
| **Owner** | Full control, including the plan, the seats, and who else is an Owner. A workspace always has at least one Owner. |
| **Admin** | Manages projects, API keys, integrations and the team. Cannot change the plan or the Owners. |
| **Member** | Sees every project, run, test and report in the workspace. Manages only their own account and CLI sessions. |

### What each role can do

| Action | Owner | Admin | Member |
|---|---|---|---|
| View projects, runs, tests and reports | ✓ | ✓ | ✓ |
| View the member list | ✓ | ✓ | ✓ |
| Edit, turn off or delete a project; change its branch and PR comment settings | ✓ | ✓ | – |
| Create, revoke and delete API keys | ✓ | ✓ | – |
| Connect repositories, pause PR comments, share error text with AI analysis | ✓ | ✓ | – |
| Invite people | ✓ | ✓ (as Member or Admin) | – |
| Change a member's role | ✓ | ✓ (between Member and Admin) | – |
| Remove a member | ✓ | ✓ (not an Owner) | – |
| Make someone an Owner, or transfer ownership | ✓ | – | – |
| View the plan, seats, committers and usage | ✓ | ✓ | – |
| Link a committer to a member, or unlink one | ✓ | ✓ | – |
| Ask for a plan or seat change | ✓ | – | – |
| Leave the workspace | ✓ (not the last Owner) | ✓ | ✓ |
| Manage their own profile, security settings and CLI sessions | ✓ | ✓ | ✓ |

Xping Cloud checks every request against the person's current role.

### Change the team

Owners and Admins manage the team in **Settings → Team**.

- **Invite** a person by email. The invitation gives them a role.
- **Change a role.** The change applies on the person's next page load. Their CLI sessions stay
  signed in.
- **Remove** a person. They lose access to the workspace at once, and the `xping` CLI is signed out
  of the workspace for them.
- **Transfer ownership.** An Owner gives the Owner role to another member and becomes an Admin.

Members do not use seats. Seats count committers. See
[How committers are counted](plans-and-retention.md#how-committers-are-counted).

### Your own security settings

Each person manages their own sign-in in **Settings → Security**: the password, two-factor
authentication, sign-in providers, the login history and the CLI sessions. Revoke a CLI session there to sign the `xping` CLI out on that
machine.

---

## Connect a repository for PR comments

Xping Cloud posts a comment on each pull request that your CI tests. See
[Reading the PR comment](reading-the-pr-comment.md). For this, Xping Cloud needs access to your
repository, and your workspace needs a plan with PR comments.

An Owner or an Admin connects the repository in **Settings → Integrations**:

| Provider | What to connect |
|---|---|
| GitHub | Install the Xping GitHub App. |
| GitLab | A project access token or a group access token, with the `api` scope and the Reporter role or above. A group token covers every project in the group. Enter the group path (`group` or `group/subgroup`) or the project path (`group/project`). |
| Azure DevOps | A personal access token with the **Code (Read & write)** scope. Enter the organization (`fabrikam`) to cover all its projects, or one project (`fabrikam/Shop`). |

For a self-managed GitLab, or for Azure DevOps Server, also enter the server's address. On Azure DevOps
Server, enter the collection in place of the organization.

A token can have an expiry date. Xping Cloud warns you two weeks before it expires. Replace the token
in **Settings → Integrations**.

The SDK must also record the pull request of each CI run. It does this by default on GitHub Actions,
GitLab CI/CD, Azure Pipelines and Jenkins multibranch builds. See
[`EnablePullRequestDetection`](../configuration/configuration-reference.md#enablepullrequestdetection).

### Turn PR comments off

- For the whole workspace: **Pause PR comments** in **Settings → Integrations**.
- For one project: turn off **Pull-request comments** in the project's settings.

PR comments are on for every project until you turn them off.

---

## See also

- [Plans and retention](plans-and-retention.md)
- [Read API](read-api.md)
- [Configuration reference](../configuration/configuration-reference.md)
