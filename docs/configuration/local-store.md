---
uid: configuration-local-store
title: Local store
---

# Local store

This page says where the Xping SDK keeps the test history on your machine, what it keeps, and how
long. It is for developers who use the `xping` CLI, and for anyone who reviews what stays on disk.

The SDK writes every session to the local store, in `LocalOnly` and in `Cloud` mode. The
[CLI](../cli/command-reference.md) reads it, so `xping report` works with or without an account.
In `Disabled` mode, the SDK writes nothing.

---

## Location

The SDK uses the first of these locations:

1. The path in the `XPING_LOCAL_STORE` environment variable, or in the
   [LocalStorePath](configuration-reference.md#localstorepath) setting. The SDK does not check that
   it can write there. A relative path is read from the repository root, else from the test
   project's folder.
2. A `.xping` folder at the **repository root**: the nearest folder above the test assembly that
   holds `.git`, a `*.sln` file or a `*.slnx` file.
3. A folder under your user profile, `Xping/stores/<key>`, when there is no repository root or the
   SDK cannot write to it. The profile folder is `%LOCALAPPDATA%` on Windows,
   `~/Library/Application Support` on macOS, and `$XDG_DATA_HOME` or else `~/.local/share` on Linux.

The `<key>` of the profile folder comes from the repository root when there is one. Otherwise it
comes from the nearest folder above that holds a `.csproj`, `.fsproj` or `.vbproj` file. So
`xping report` and `xping where` find the folder the tests wrote to when you run them inside the
repository or, without one, in the test project's folder.

To see where the store is:

```bash
xping where
```

The SDK starts its search at the test assembly, not at the current directory, because the current
directory of `dotnet test` differs between the CLI, IDEs and CI agents.

---

## Layout

```
<repository root>/.xping/
├── .gitignore
├── sessions/
│   ├── session-0639309348082804300-00b189fd-47e6952c.json.gz
│   └── …
└── state.json
```

- Each session is one gzipped JSON file in `sessions/`. The name holds the start time, the first 8
  characters of the session id, and a short hash of each test assembly the session covers (up to
  8).
- Test projects that run at the same time write separate files, so they never wait for a lock.
- The SDK writes each file under a `.tmp` name first and renames it when it is complete. A killed
  test run can leave a `session-*.json.gz.tmp` file. The SDK deletes such a file once it is ten
  minutes old, and it counts toward the size limit until then.
- `state.json` is the CLI's own state, such as when it last showed the Xping Cloud invitation.

---

## Git

The store keeps itself out of git. `.xping/.gitignore` holds a comment line and `*`, so git ignores
the whole folder. The SDK does not change your repository's own `.gitignore`. You do not need to
add `.xping/` to it, but an entry there does no harm.

---

## What is stored

The store keeps the **full session**: every field on
[Data Xping collects](collected-data.md), for every execution. That includes:

- the error message and the full stack trace of each failure, whatever
  [UploadStackTraces](configuration-reference.md#uploadstacktraces) says. That setting decides
  only what the SDK uploads. `xping report` reads the stack traces to group failures.
- the machine name, the source file paths and the CI metadata, including `CI.ActorEmail` when
  [CollectCiActorEmail](configuration-reference.md#collectciactoremail) is on.

These switches limit what the SDK records, and so what the store keeps:

| Switch | What it removes |
|---|---|
| [CollectLocalGitAuthor](configuration-reference.md#collectlocalgitauthor) off (the default) | The git author name of local runs |
| [CollectCiActorEmail](configuration-reference.md#collectciactoremail) off (the default) | The email of the person behind a CI run |
| [EnablePullRequestDetection](configuration-reference.md#enablepullrequestdetection) off | The pull request context, including its author |
| [Enabled](configuration-reference.md#enabled) off, or `Mode` set to `Disabled` | Everything: the SDK writes no session |

The store never leaves your machine, and the SDK never uploads a session from it again. It is not
a backup of what Xping Cloud holds. To delete it, run [`xping clear`](../cli/command-reference.md#xping-clear).

### How the session was recorded

Each session also records how it was recorded, as properties of its environment. The CLI reads
them. The SDK writes them to the local store only, never to the upload.

| Property | Value | Missing when |
|---|---|---|
| `Xping.Mode` | `LocalOnly` or `Cloud` | Never |
| `Xping.ProjectId` | The [ProjectId](configuration-reference.md#projectid) of the session | No project is set |
| `Xping.TestFramework` | `NUnit`, `MSTest`, `xUnit` (v2) or `xUnit.v3` | Never |
| `Xping.Runner` | `VSTest` or `MTP` (Microsoft.Testing.Platform) | The tests ran in another host, such as the xUnit console runner, or an xUnit v3 test executable without Microsoft.Testing.Platform |
| `Xping.ProjectPath` | The test project's path from the repository root, such as `tests/Shop.Tests/Shop.Tests.csproj` | The project was built outside a git repository or with `GenerateAssemblyInfo=false`, or the session covered test assemblies of more than one project |

The project path comes from an MSBuild file that the Xping packages add to your test project's
build. It stamps the path into the test assembly as
`[assembly: AssemblyMetadata("Xping.ProjectPath", "...")]`. In a git submodule, the path is
relative to the submodule.

---

## Retention

After each write, the SDK deletes old sessions until these limits hold. The limits are fixed: no
setting changes them.

| Limit | Value | Applies to |
|---|---|---|
| Maximum age | 30 days, by the file's last write time | The whole store |
| Maximum sessions | 50 | Each test assembly |
| Maximum size | 100 MB | The whole store |

The SDK applies them in this order:

1. It deletes sessions older than 30 days.
2. It keeps the newest 50 sessions of each test assembly, so a busy project never pushes another
   project's sessions out. A session that covers several test assemblies counts for each of them.
   The SDK keeps it while any of them still needs it.
3. If the store is still over 100 MB, it deletes the oldest sessions, first those outside the newest
   20 of every test assembly. It deletes from those 20 only if the store is still too big.

The session the SDK just wrote is never deleted.

The size of a session grows with the number of tests, and with the length of the error messages
and stack traces of failed tests.

`xping report` reads the newest 20 sessions from the last 14 days by default. The store keeps more,
so that `--runs`, `--since` and `--test` can look further back.

---

## When the store is written

The SDK writes the session once, when it ends, after the last test. During the run, it only keeps
each execution in memory. The CLI reads and analyses the history, so your test run does not pay for
it.

---

## Failures

**The local store never fails your test run.** When the SDK cannot write the session, for example
because the disk is full or because `XPING_LOCAL_STORE` names a folder it cannot write to, the run
has no local history. A repository root that is not writable is not such a case: the SDK then uses
the profile folder, so run `xping where` to find it. The SDK
logs the reason at the `Debug` level. To see it, set
[`XPING_LOGLEVEL=Debug`](configuration-reference.md#loglevel).

When the CLI reads the store, it skips a session file that it cannot read or that does not parse,
and reads the rest.

---

## Environment variables

| Variable | Read by | Effect |
|---|---|---|
| `XPING_LOCAL_STORE` | SDK and CLI | Moves the store. See [LocalStorePath](configuration-reference.md#localstorepath). |
| `XPING_NO_BANNER` | SDK and CLI | Any non-empty value hides the SDK's retry hint after a test run and the CLI's Xping Cloud invitation |

If you set `XPING_LOCAL_STORE` for the test run, set it for `xping report` too, to the same value.
Otherwise the CLI looks in another place and finds nothing. A relative value names the same folder
for both, as long as you run `xping report` inside the repository.

```bash
export XPING_LOCAL_STORE=/var/tmp/xping-store
```

---

## See also

- [Run without an account](../getting-started/local-first.md)
- [Data Xping collects](collected-data.md)
- [Configuration reference](configuration-reference.md)
- [CLI reference](../cli/command-reference.md)
