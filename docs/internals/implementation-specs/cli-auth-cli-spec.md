# `xping login` — CLI implementation specification

**Status:** Draft for review, written 2026-09-29 from the CLI P0 inventory of the same day.
**Applies to:** `xping-dev/sdk-dotnet`, `src/Xping.Cli/**`, plus one small change in `src/Xping.Sdk.Core` (§11.2) and the CLI test projects.
**Implements:** `xping-dev/dashboard:docs/implementation-plans/cli-auth-contract-spec.md`, read at dashboard commit `b2e930eb1595991f02b65b2c5f6aff8099d660c7` (2026-09-29) and re-read at `60a64fc4329b1d5f00766ba7bd53436f32a55dbe` (2026-10-03) for contract OQ-17 to OQ-20 (§20). Section references written as "contract §n" point at that document. Bare "§n" points at this one.
**Reference only:** `xping-dev/dashboard:docs/implementation-plans/cli-auth-cloud-spec.md`, same commit. Written as "Cloud spec §n". It is not authoritative for the CLI; where it and the contract disagree, the contract wins.
**Depends on:** `cli-report-format-spec.md` §8, `cli-latest-run-spec.md` §8, `cli-finding-detail-spec.md` §8 (the reserved Cloud slots). §11 amends them.
**Schema:** report envelope `1.21` → `1.22` (§11.4).

The key words MUST, MUST NOT, SHOULD, SHOULD NOT and MAY are to be read as in RFC 2119.

---

## Amendment protocol

This document says HOW the CLI implements the contract. It does not change WHAT crosses the wire.
If a statement here contradicts the contract, the contract wins and this document is amended. If a
statement here contradicts the codebase or cannot be implemented as written: **stop and report.**
Do not adapt in code. §20 lists the open questions; an implementation session that needs one of them
answered stops and asks.

Decisions confirmed by the owner on 2026-09-29, before this draft:

| # | Decision |
|---|---|
| A-1 | No `--output rich\|plain\|json` and no Spectre.Console. Auth commands take `--json`, like `report --json`. Human text follows the existing `OutputCapabilities` rules (TTY decoration on, redirected decoration off). |
| A-2 | The API key environment variable is `XPING_APIKEY`, the name the SDK already uses. |
| A-3 | Project binding follows §11.2. The DataGateway MAY be extended additively (`displayName`, `slug` on `ProjectResponse`) to make name matching possible. |
| A-4 | There is no staging environment. Manual verification runs against production with a test account and a test workspace (Cloud spec §14.4). |
| A-5 | `xping auth status` is the CLI name for what the contract calls `xping whoami` (contract §1.3, §6.1, §9.1). Same output, different name. |
| A-6 | Contract §3.1 step 8 and §10.2 ("exactly one request", "one callback and then close") mean **one successful callback**. Requests that fail the `state` check or are not `GET /callback` are answered and the listener keeps waiting (contract §3.1 failure table). |
| A-7 | Cross-platform credential-store tests do not run on every PR. They run in a separate workflow, weekly on `main` only (§17.2), to protect the free CI minutes. |
| A-8 | Credential precedence (revised 2026-09-30, replaces the brief's order): `--api-key` flag, then the stored login, then `XPING_APIKEY` / `Xping:ApiKey`. The explicit flag wins because it was typed for this command; the ambient key comes last because on a developer machine it is almost always the SDK's upload key, and trying it first would waste a request per command. CI has no stored login, so it still uses the key. Fallback rules in §8.1. |

---

## 1. Purpose, scope, traceability

### 1.1 Purpose

`xping login` lets a developer read their own workspace's Cloud data from the `xping` CLI without
creating an API key. This document names every CLI component, its file, its behaviour per output
mode, the credential store per operating system, the HTTP pipeline, and the order of work.

### 1.2 In scope

- Commands `xping login` (with `--device`, `--no-browser`, `--json`), `xping logout` (`--json`),
  `xping auth status` (`--json`).
- The loopback PKCE flow (primary) and the device flow (fallback), as contract §3.1 and §3.2.
- Refresh, rotation, reuse detection, revocation and logout, as contract §3.3 and §3.4.
- Credential storage per operating system with a file fallback, as contract §10.4.
- Credential precedence: `--api-key`, then the stored login, then `XPING_APIKEY` (A-8).
- The authenticated HTTP pipeline and the Cloud API client.
- Enrichment of `xping report` with Cloud data, additive and never required (§11).
- `--cloud-url` and its configuration equivalents.
- Exit codes, redaction, agent usage, the cross-platform test matrix, tests, and phases.

### 1.3 Out of scope

- Everything in contract §1.3: workspace switching, any scope other than `user:read`, uploading
  with a user token, OIDC, introspection.
- The Cloud side. Cloud spec.
- The MCP server and the Xping skill themselves. §16 says how they must use the CLI.
- `xping report --source local|cloud` as a mode switch. This MVP enriches the local report; a
  Cloud-only report is later work.
- Changes to `report`'s exit codes, `--fail-on`, ranking, or text layout beyond the three reserved
  slots (§11.3).
- Trimming or AOT of the CLI. The tool is framework-dependent today and stays so.

### 1.4 Traceability

| Contract | Requirement | CLI component (this document) |
|---|---|---|
| §2 | Access token only to the DataGateway base URL | §9.1 host guard, §10.1 |
| §3.1 | Loopback flow, steps 1–10 and failure table | §4, §5 |
| §3.2 | Device flow and failure table | §6 |
| §3.3 | Refresh trigger, atomic replace, `invalid_grant` deletes tokens, concurrency | §9.2–§9.6 |
| §3.4 | Revoke then delete; offline logout warns | §12 |
| §3.5 | Bearer on every request, proactive refresh, one retry on 401 | §9.1, §9.7 |
| §4.1 | Discovery fields, `issuer` check, custom fields, 24 h cache | §2.3, §14.4 |
| §4.2 | Authorization request parameters | §4.2 |
| §4.3 | Token endpoint, three grants, ignore extra members | §2.4, §4.6, §6.3, §9.3 |
| §4.4 | Revocation request | §12.1 |
| §4.5 | Device authorization request and response | §6.1 |
| §5 | `client_id=xping-cli`, both scopes together, PKCE S256 | §4.2 |
| §6.1 | Decode claims for display and expiry only | §7.2, §8.2 |
| §6.2 | Refresh token opaque, up to 2048 bytes | §7.2, §7.5 |
| §6.4 | Lifetimes and margins | §4.7, §6.3, §9.2 |
| §6.6 | 30 s reuse leeway; reuse revokes the session | §9.5, §9.6 |
| §7.1 | Exactly one credential header | §8.1, §9.1 |
| §7.3, §8.2 | 401/403/404/429/5xx mapping by status then `title` | §9.7, §10.3 |
| §7.7 | Read endpoints and `User-Agent` | §10.1 |
| §8.1 | OAuth error catalog | §2.4 (`OAuthError`), §4.8, §6.3 |
| §10.1, §10.2 | Security requirements on the CLI | §4, §15 |
| §10.4 | Token storage | §7 |
| §11 | Versioning: contract version, min CLI version, 426 | §2.3, §14.4 |

---

## 2. Component architecture

### 2.1 Overview

```
 Program.cs ─ global options (--cloud-url, --api-key, --verbose) ─ commands
   │
   ├─ login ──▶ LoginCommand ──▶ LoopbackFlow ─┬─ Pkce, LoopbackListener, BrowserLauncher
   │                            DeviceFlow ────┘  OAuthClient, DiscoveryClient
   │                                              │
   ├─ logout ─▶ LogoutCommand ──▶ OAuthClient.RevokeAsync ──▶ ICredentialStore.Delete
   ├─ auth status ─▶ AuthStatusCommand ──▶ CredentialResolver (no network)
   │
   └─ report ─▶ ReportCommand ─▶ CloudEnricher ─▶ ICloudApiClient
                                                   │  HttpClient "xping-cloud"
                                                   │   resilience ─▶ BearerTokenHandler ─▶ primary
                                                   │                  │ (or ApiKeyHandler)
                                                   │                  └─ TokenRefresher ─▶ OAuthClient
                                                   └─ ProjectResolver (+ cache)
 CredentialResolver ─▶ ICredentialStore ─▶ WindowsCredentialStore | MacOsKeychainStore
                                            | LibSecretStore | FileCredentialStore
```

All new code lives under `src/Xping.Cli/Auth/` and `src/Xping.Cli/Cloud/`. Nothing is added to
`Xping.Sdk.Core` except §11.2 (the pin stamp), because Core targets netstandard2.0 and the keychain
code needs `LibraryImport`, `UnixFileMode` and `TimeProvider`.

### 2.2 Components and dependencies

| Component | File | Depends on | Role |
|---|---|---|---|
| `LoginCommand`, `LogoutCommand`, `AuthStatusCommand` | `Commands/Auth/*.cs` | flows, store, resolver, `ConsoleIO` | Map options to flows; own the text and JSON output (§3) |
| `CliConfiguration` | `Configuration/CliConfiguration.cs` | env, `appsettings*.json` | Resolves `CloudUrl`, `ApiKey`, `ProjectId` with the precedence of §14 |
| `DiscoveryClient`, `DiscoveryDocument`, `DiscoveryCache` | `Auth/Discovery/*.cs` | `HttpClient "xping-oauth"` | Fetch, validate and cache the discovery document (§2.3) |
| `OAuthClient` | `Auth/OAuthClient.cs` | discovery, `HttpClient "xping-oauth"`, `IXpingSerializer` | Token, device authorization and revocation requests; error mapping (§2.4) |
| `Pkce` | `Auth/Loopback/Pkce.cs` | `RandomNumberGenerator`, `SHA256` | Verifier, challenge, state (§4.1) |
| `LoopbackListener` | `Auth/Loopback/LoopbackListener.cs` | `HttpListener`, `TcpListener` | Bind, accept one successful callback, respond (§4.3–§4.5) |
| `BrowserLauncher`, `HeadlessDetector` | `Auth/Browser/*.cs` | `Process`, env | Open the URL per OS; decide whether to try (§5) |
| `LoopbackFlow`, `DeviceFlow` | `Auth/Flows/*.cs` | above | Orchestrate one login end to end (§4, §6) |
| `CredentialRecord` | `Auth/Store/CredentialRecord.cs` | — | The stored shape (§7.2) |
| `ICredentialStore` + four backends + `CredentialStoreSelector` | `Auth/Store/*.cs` | P/Invoke, file system | Keychain per OS, file fallback (§7) |
| `CredentialResolver`, `CredentialSource` | `Auth/CredentialResolver.cs` | `CliConfiguration`, store | Precedence and "which source is active" (§8) |
| `TokenRefresher`, `CrossProcessLock` | `Auth/Http/*.cs` | `OAuthClient`, store, `TimeProvider` | Proactive, single-flight, cross-process safe refresh (§9.2–§9.6) |
| `BearerTokenHandler`, `ApiKeyHandler` | `Auth/Http/*.cs` | `TokenRefresher` | `DelegatingHandler`s (§9.1) |
| `ICloudApiClient`, `CloudApiClient`, DTOs, `CloudApiException` | `Cloud/*.cs` | `HttpClient "xping-cloud"` | Typed reads (§10) |
| `ProjectResolver`, `ProjectCache` | `Cloud/Projects/*.cs` | `ICloudApiClient`, `CliConfiguration` | Assembly → project key (§11.2) |
| `CloudEnricher` | `Cloud/CloudEnricher.cs` | `ICloudApiClient`, `ProjectResolver` | Adds Cloud data to the envelope, degrades silently (§11) |
| `AuthExitCodes` | `Auth/AuthExitCodes.cs` | — | The new codes (§13) |
| `Redaction` | `Auth/Redaction.cs` | — | One place that scrubs secrets from text (§15.2) |

Registration: `AddXpingCliAuth(IServiceCollection)` in `Hosting/ServiceCollectionExtensions.cs`,
filling the comment-only extension point that exists today at lines 36–41. Two named clients:

- `"xping-oauth"`: back channel to the Portal. `AllowAutoRedirect = false` (contract §10.2),
  `Timeout = 15 s`, no resilience handler; retries are the explicit ones of contract §8.1 (§2.4).
- `"xping-cloud"`: DataGateway. `AllowAutoRedirect = false`, `Timeout = 30 s` total, resilience
  handler of §10.3, then `BearerTokenHandler` or `ApiKeyHandler` (never both), then the primary
  handler. The credential is known only once a command resolves it (§8.1), and a handler instance
  holds that credential's state, which pooled factory handlers must not. So the named client
  registers only the primary handler; `CloudApiClientFactory` (`Cloud/CloudApiClientFactory.cs`)
  builds resilience → credential handler → the factory's primary handler for each credential. A
  fallback (§8.1) therefore always gets a new pipeline.

Both clients set `User-Agent: xping-cli/{XpingVersion.Current} ({RuntimeInformation.OSDescription
trimmed to the OS family: windows, macos, linux})` (contract §7.7) and `Accept: application/json`.

### 2.3 Discovery

`DiscoveryClient.GetAsync(cloudUrl)`:

1. Read `DiscoveryCache` (§14.4). A cached document younger than 24 hours for the same normalized
   Cloud URL is used without a request (contract §4.1).
2. Otherwise `GET {cloudUrl}/.well-known/openid-configuration` through `"xping-oauth"`.
3. Validate, in this order, and fail closed on the first failure with `AuthExitCodes`:
   - `issuer` equals the normalized Cloud URL followed by exactly one `/`, ordinal comparison
     (contract §3.1 step 1, §4.1, OQ-17). OpenIddict writes the issuer as `Uri.AbsoluteUri`, so
     `https://app.xping.io` publishes `https://app.xping.io/`; any other value fails closed.
   - `xping_data_gateway_uri` present, absolute, `https` (or `http` on `localhost`/`127.0.0.1`), no
     user info, query or fragment, no trailing slash after normalization.
   - `xping_contract_version` equals `1`. Otherwise: "This CLI implements Cloud contract version 1;
     the server reports {n}. {Upgrade the CLI | The server is older than this CLI}." Exit code
     `CloudVersionMismatch` (contract §11).
   - `xping_cli_min_version` parsed as SemVer 2 and compared to `XpingVersion.Current` with SemVer
     precedence rules (prerelease lower than release). A lower CLI stops: "Please upgrade the xping
     CLI: this server requires {min} or newer, you have {current}." Same exit code.
   - `authorization_endpoint`, `token_endpoint`, `revocation_endpoint`,
     `device_authorization_endpoint` present, absolute, `https` (or `http` on
     `localhost`/`127.0.0.1`), no user info or fragment: codes and refresh tokens are sent there,
     so they meet the same rule as the Cloud URL (§14.1). They are used as given, never rebuilt
     from the paths in contract §4.
4. Unknown members are ignored (contract §11). Deserialization goes through `IXpingSerializer`
   with a DTO that lists only the fields above.
5. Write the cache.

The cache is only consulted for `report` enrichment, `logout` and refresh. `login` always fetches a
fresh document, so a stale `xping_cli_min_version` cannot let an outdated CLI start a flow.

### 2.4 `OAuthClient`

One class, three operations, all `POST` with `application/x-www-form-urlencoded` bodies,
`client_id=xping-cli` always present, no `client_secret`:

| Method | Grant / endpoint | Contract |
|---|---|---|
| `ExchangeCodeAsync(code, redirectUri, codeVerifier)` | `authorization_code` at `token_endpoint` | §4.3 |
| `RefreshAsync(refreshToken)` | `refresh_token`, no `scope` parameter | §4.3 |
| `PollDeviceAsync(deviceCode)` | `urn:ietf:params:oauth:grant-type:device_code` | §4.3 |
| `StartDeviceAsync(workspaceId?)` | `device_authorization_endpoint`, `scope=user:read offline_access` | §4.5 |
| `RevokeAsync(refreshToken)` | `revocation_endpoint`, `token_type_hint=refresh_token`; any `200` succeeds and the body (empty or `{}`) is ignored | §4.4, OQ-19 |

Token responses map to `TokenResponse(AccessToken, ExpiresIn, RefreshToken, Scope)`; extra members
are ignored. Error responses map to `OAuthError(Error, ErrorDescription, StatusCode)` and are
thrown as `OAuthException`. The retry rules of contract §8.1 are implemented here, once, with
delays from an injected `TimeProvider` so tests do not wait:

- `server_error` (500): one retry after 2 s.
- `temporarily_unavailable` (503): retries after 2 s, 4 s, 8 s.
- Any network failure (`HttpRequestException`, timeout): treated like `server_error`, except in
  `ExchangeCodeAsync`. A code works once, and when no answer arrived the server may already have
  redeemed it; a retry would answer `invalid_grant` and hide the network failure. A 5xx answer
  means the code was not redeemed, so that is still retried.
- `PollDeviceAsync` makes none of these retries: a network failure, `server_error`,
  `temporarily_unavailable`, another 5xx, or a `429` without an OAuth body (a rate limiter in
  front of the Portal) comes back as a transient result, and the device flow polls again at the
  next interval (contract §3.2, §6.3). An immediate retry would poll faster
  than `interval`.
- When the retries are spent, the failure is `AuthFailureException` with `CloudUnreachable`
  (§9.6).
- Everything else is returned to the caller on the first response. A redirect or an error status
  without an OAuth body is never a success; it is `CloudUnreachable` with the status.

`OAuthClient` never logs a request body or a response body (§15).

---

## 3. Command UX

### 3.1 Conventions shared by the three commands

- Options: `--json` (result on stdout as one JSON document, nothing else on stdout),
  `--cloud-url <url>` (global, §14), `--verbose` (global, diagnostics on stderr, §8.3).
- Human text goes to stderr in every mode, including success messages. Stdout carries only the
  `--json` document. This follows the finding-detail spec D8 rule that stdout is data.
- Decoration follows `OutputCapabilities.Resolve(ascii: false, noColor: false, redirected:
  !io.IsErrorTerminal, env)`: colour and Unicode glyphs when stderr is a terminal, plain ASCII when it is
  redirected, `NO_COLOR` honoured. There is no `rich`/`plain` switch (A-1). `ConsoleIO` carries one
  terminal flag per stream (`IsInputTerminal`, `IsTerminal` for stdout, `IsErrorTerminal`); the
  auth commands write their text to stderr, so stderr's flag decides.
- Every JSON document has `"schemaVersion": "1"` (the auth JSON schema, independent of the report
  envelope) and `"cloudUrl"`. Field names are camelCase. Enum values are lowercase strings.
- JSON output MUST NOT contain a token, code, verifier or `state` (§15). It MAY contain `sid`,
  `sub`, `email`, `workspaceId`, `expiresAt`.
- Ctrl+C: `Program.Run` links `Console.CancelKeyPress` to one `CancellationTokenSource` passed to
  every command. On cancellation a command stops what it is doing (listener closed, polling
  stopped, HTTP request aborted), prints "Cancelled." to stderr, and returns exit code 130
  (§13). Nothing is stored on cancellation; a login that has already stored its tokens is complete
  and ignores a later Ctrl+C.

### 3.2 `xping login`

Options: `--device` (use the device flow), `--no-browser` (loopback flow, do not try to open a
browser), `--workspace <ulid>` (sent as `xping_workspace_id`, contract §4.2; preselects only),
`--json`. `--workspace` must be a ULID: 26 Crockford base32 characters, case-insensitive. Anything
else is a parse error, exit 2, before any request; a valid value is sent as given.

**Preconditions, checked in this order, before any network call:**

1. Interactive environment. `login` refuses when stdin is redirected, or stderr is redirected, or
   the `CI` environment variable is set to a truthy value (`true`, `1`, `yes`, case-insensitive).
   Message: "xping login needs an interactive terminal. Run it in your own shell. Coding agents and
   CI must use an API key or a login stored earlier; see `xping auth status`." Exit code
   `InteractiveRequired` (§13). `--json` does not lift this rule; the JSON error document is still
   written.
2. A credential store is available or the file fallback can be created (§7.4). Otherwise exit code
   `CredentialStoreError`.
3. `XPING_APIKEY` or `Xping:ApiKey` present: no warning; the stored login takes precedence over
   the ambient key (A-8). Under `--verbose` one line notes that a key is also set and will be
   used only when no login is available.

Then discovery (§2.3), then the flow (§4 or §6), then storage, then the result.

**Text output, loopback flow (stderr):**

```
Opening your browser to sign in to Xping Cloud (https://app.xping.io).
If it does not open, use this link:

  https://app.xping.io/connect/authorize?response_type=code&client_id=xping-cli&...

Waiting for you to finish in the browser (up to 5 minutes)...
✓ Signed in as jane@example.com
  Workspace  01J8K2V6XN7Y0Q4R5S6T7U8V9X
  Session    ...A7F2Q9   (shown on Settings → Security → CLI sessions)
  Stored in  macOS Keychain
```

The link line prints the full authorization URL exactly once, always, even when the browser
opened (contract §3.1 step 4). When `HeadlessDetector` reports headless (§5.2) the first line is
replaced by "No browser was found on this machine. Open this link in a browser **on this
machine**:" followed by the hint "If your browser is on another machine, press Ctrl+C and run
`xping login --device`." Under `--no-browser` the first line is "Open this link in a browser **on
this machine**:" (nothing was looked for, so "No browser was found" would be false), followed by
the same hint.

**Text output, device flow:**

```
To sign in, open  https://app.xping.io/device
and enter the code  ABCD-EFGH

(or open https://app.xping.io/device?user_code=ABCD-EFGH)

Waiting for you to approve in the browser (up to 10 minutes)...
```

followed by the same success block. The code is printed in bold on a terminal, plain otherwise. The
waiting line names `expires_in` in whole minutes, or in seconds when it is not a whole number of
minutes, so a short lifetime never reads "up to 0 minutes".

**JSON result** (`--json`, stdout, exit 0):

```json
{
  "schemaVersion": "1",
  "result": "signed-in",
  "cloudUrl": "https://app.xping.io",
  "flow": "loopback",
  "email": "jane@example.com",
  "sub": "01J8K2V6XN7Y0Q4R5S6T7U8V9W",
  "workspaceId": "01J8K2V6XN7Y0Q4R5S6T7U8V9X",
  "sessionId": "01J8K2V6XN7Y0Q4R5S6T7U8VA7F2Q9",
  "store": "keychain",
  "accessTokenExpiresAt": "2026-09-29T10:15:00Z"
}
```

On failure (`--json`, stdout, non-zero exit):

```json
{ "schemaVersion": "1", "result": "failed", "cloudUrl": "https://app.xping.io",
  "error": "access_denied", "message": "You declined the sign-in request. Nothing was stored." }
```

`error` is one of: `access_denied`, `timeout`, `interactive_required`,
`cloud_unreachable`, `version_mismatch`, `credential_store`, `oauth_error` (with the server's
`error` code in `oauthError`), `cancelled`, `configuration` (an invalid `XPING_CLOUDURL` or
settings file; exit 2), `local_listener` (no loopback port could be opened, or the listener
stopped; exit 14). There is no `state_mismatch`: a callback with the wrong `state` is answered
400 and the CLI keeps waiting (A-6), so it ends as `timeout` or succeeds. The failure document is the same for all three commands, and every failure
under `--json` writes one, including `configuration`. Its `message` is scrubbed (§15.2) like the
stderr text.

The workspace **name** is not in any token claim and not returned by the token endpoint. The
success block shows the workspace id only (it is in the token and costs nothing); there is no
name lookup and no extra request during `login` (Q-2, answered).

### 3.3 `xping logout`

Options: `--json`. No prompt. Behaviour in §12. Text:

```
✓ Signed out of https://app.xping.io (jane@example.com).
```

or, offline:

```
⚠ Could not reach https://app.xping.io to revoke the session (connection failed).
  Your local credentials were removed. The session may still exist on the server; revoke it
  under Settings → Security → CLI sessions.
✓ Signed out locally.
```

The glyphs are `✓` and `⚠` when stderr is a terminal, `+` and `!` when it is redirected.

Not logged in: "You are not signed in to https://app.xping.io." exit 0 (idempotent).

JSON: `{ "schemaVersion": "1", "result": "signed-out" | "signed-out-locally" | "not-signed-in",
"cloudUrl": "...", "revoked": true|false, "warning": "..." | null }`.

### 3.4 `xping auth status`

Options: `--json`. No network call, ever (contract §6.1 allows answering from stored claims). Text:

```
Cloud URL     https://app.xping.io
Credential    stored login (macOS Keychain)
Signed in     jane@example.com
Workspace     01J8K2V6XN7Y0Q4R5S6T7U8V9X
Session       ...A7F2Q9
Access token  expires in 12 minutes (refreshed automatically)
```

Labels share one width. `--verbose` adds `Home`, `Credentials`, `Discovery` and `Projects` rows
with the §14.4 paths in use.

With a stored login and an ambient key both present: the `Credential` line shows the login and a
second line reads "API key (XPING_APIKEY) also set; used only when no login is available."
With `--api-key` given: `Credential  API key (--api-key)` and, when a login exists, "A stored
login also exists and is not used while --api-key is given." With only a key:
`Credential  API key (XPING_APIKEY)`. Not logged in and no key: `Credential  none` and "Run
`xping login` to sign in." Exit code `AuthRequired` when no credential of any kind is available
(`CredentialStoreError` when, in addition, a credential store could not be read, §8.1);
`0` otherwise, including when only an API key is set (the CLI cannot verify a key without a
network call, and `status` makes none).

JSON:

```json
{
  "schemaVersion": "1",
  "cloudUrl": "https://app.xping.io",
  "credential": "stored-login" | "api-key" | "none",
  "credentialSource": "keychain" | "file" | "flag" | "env" | "config" | null,
  "loggedIn": true,
  "email": "jane@example.com",
  "sub": "...",
  "workspaceId": "...",
  "sessionId": "...",
  "accessTokenExpiresAt": "2026-09-29T10:15:00Z" | null,
  "storedAt": "2026-09-01T08:00:00Z",
  "fallbackApiKey": "env" | "config" | null,
  "shadowedLogin": false,
  "warnings": []
}
```

`fallbackApiKey` names an ambient key that would be used if the login became invalid;
`shadowedLogin` is `true` only when `--api-key` was given and a login exists. `warnings` carries
the file-fallback notice ("No OS credential store is available ({reason}); sign-ins are kept in
~/.xping/credentials.json."), the refused-file notice (§7.5) and a corrupt-entry notice (§7.7) as
full sentences so an agent can relay them as they are.

### 3.5 Prompts

None. `login` never asks a question in the terminal; every choice happens in the browser
(contract §2). `logout` does not confirm. This keeps the commands safe under `--json` and under
agents that wrongly reach them.

---

## 4. Loopback PKCE flow

### 4.1 PKCE and `state`

`Pkce.Create()`:

- `code_verifier`: 32 bytes from `RandomNumberGenerator.Fill`, Base64URL without padding, giving
  43 characters (contract §3.1 step 3, RFC 7636 §4.1).
- `code_challenge`: `Base64Url(SHA256(ASCII(code_verifier)))`, method `S256`.
- `state`: 32 bytes from the same source, Base64URL, 43 characters (≥ 128 bits, contract §4.2).
- Both are held in memory only, in a `sealed` class that is disposed after the token exchange; the
  verifier byte array is cleared with `CryptographicOperations.ZeroMemory`. They are never written
  to disk (contract §10.4) and never logged (§15).
- `state` comparison uses `CryptographicOperations.FixedTimeEquals` on the UTF-8 bytes after a
  length check (contract §10.1).

### 4.2 Authorization URL

Built with `UriBuilder` and proper form encoding of every value:

| Parameter | Value |
|---|---|
| `response_type` | `code` |
| `client_id` | `xping-cli` |
| `redirect_uri` | `http://127.0.0.1:{port}/callback` or `http://[::1]:{port}/callback`, matching the bound host |
| `scope` | `user:read offline_access` (always both, contract §5) |
| `state` | as §4.1 |
| `code_challenge`, `code_challenge_method` | as §4.1, `S256` |
| `xping_workspace_id` | only when `--workspace` was given |

The base is `authorization_endpoint` from discovery.

### 4.3 Listener choice

Three options were weighed:

| Option | For | Against |
|---|---|---|
| `System.Net.HttpListener` | In the BCL, no dependency. Parses HTTP for us. On Windows it uses `http.sys`, which lets a non-elevated process listen on loopback prefixes without a URL reservation. On Unix it is a managed implementation. Used by Google's OAuth .NET client for exactly this purpose. | Cannot bind port `0`; a free port must be found first. Windows `http.sys` may answer with its own error pages for malformed requests. |
| Minimal Kestrel | Full HTTP server, port `0` supported. | Needs `Microsoft.AspNetCore.App`, which a `dotnet tool` user may not have installed with a runtime-only install; large startup cost for one request. |
| Hand-written `TcpListener` responder | Port `0`, full control, no dependency. | An HTTP/1.1 request parser is new code with its own bugs; keep-alive, chunking and slow clients must be handled or rejected. |

**Recommendation: `HttpListener`**, with a `TcpListener` probe to choose the port. The probe binds
`127.0.0.1:0`, reads the assigned port, closes, and `HttpListener` binds `http://127.0.0.1:{port}/`.
The window between the probe and the bind is the reason contract §3.1 allows three attempts. If a
future platform removes `HttpListener` support (`HttpListener.IsSupported == false`), the
`TcpListener` responder is the replacement; the `LoopbackListener` interface (`Start`,
`WaitForCallbackAsync`, `Stop`) hides the choice.

Binding rules (contract §10.2):

1. Try `127.0.0.1` up to three times with fresh probed ports.
2. Then `[::1]` once, prefix `http://[::1]:{port}/`.
3. Then fail: "Could not open a local port for the browser to return to. Run
   `xping login --device` instead." Exit code `LoginFailed` (§13); this is a local problem, not a
   Cloud one.

The prefix is never `localhost`, `+`, `*`, `0.0.0.0` or `::`.

Platform note (found in phase 3): the managed `HttpListener` of macOS and Linux rejects a bracketed
IPv6 prefix (`HttpListenerException`: "Invalid port in prefix"), so step 2 binds only on Windows,
where `http.sys` accepts it. Elsewhere step 2 fails and step 3 applies. The attempt is kept so the
order is the same on every OS; the `TcpListener` responder would lift the limit, and is not worth
its parser while `127.0.0.1` is always present on a loopback interface.

### 4.4 Callback validation and responses

The listener loop runs until it has one successful callback, the timeout (§4.7) fires, or the
command is cancelled. For each request:

| Request | Response | Then |
|---|---|---|
| Method not `GET`, or path not exactly `/callback` | `404`, empty body, `Connection: close` | keep waiting (A-6) |
| `GET /callback` with `state` missing or not equal to the expected value | `400`, error page (§4.5) | keep waiting; log at verbose "callback with wrong state ignored" |
| `GET /callback`, `state` matches, `error` present | `200`, error page showing `error` only | stop; command fails per contract §3.1 (`access_denied` → declined; other → print `error` and `error_description`) |
| `GET /callback`, `state` matches, `code` present | `200`, success page | stop; exchange the code |
| `GET /callback`, `state` matches, neither `code` nor `error` | `400`, error page | keep waiting |

`state` is checked before anything else is read from the query (contract §3.1 step 8). After the
successful callback the `state` value is discarded, so a second identical redirect cannot match.

Every response carries `Content-Type: text/html; charset=utf-8`, `Cache-Control: no-store`,
`Referrer-Policy: no-referrer`, `X-Content-Type-Options: nosniff`, and no cookies. The listener never
serves files and never reads request bodies.

### 4.5 Browser pages

Two static HTML strings embedded in `LoopbackPages.cs`, plain HTML without external resources, no
script, no inline data from the request. They MUST NOT contain `code`, `state`, any token, the
redirect URI, or the Cloud URL (contract §3.1 step 8, §10.1).

- Success: title "Signed in to Xping", body "You can close this tab and return to your terminal."
- Error: title "Sign-in could not be completed", body "Return to your terminal. If the problem
  continues, run `xping login` again." When the callback carried `error`, the page shows only the
  `error` code (an identifier from the contract's catalog), never `error_description`, so a
  malicious redirect cannot render arbitrary text.

### 4.6 Token exchange

`OAuthClient.ExchangeCodeAsync` with the same `redirect_uri` string that was used in §4.2 and the
verifier from §4.1. On success the record of §7.2 is built and stored (§4.9). On `invalid_grant`:
"The sign-in code was rejected (expired or already used). Run `xping login` again." Exit code
`LoginFailed`.

### 4.7 Timeout

5 minutes from the moment the authorization URL is printed (contract §3.1). On timeout the listener
is stopped, the verifier is destroyed, and the command fails: "No sign-in arrived within 5 minutes.
Run `xping login` again, or `xping login --device` if your browser is on another machine." Exit
code `LoginTimedOut`. A code issued later expires on its own (contract §6.4).

### 4.8 Failure summary

| Situation | Exit code (§13) | Message |
|---|---|---|
| User denied | `LoginDeclined` | "You declined the sign-in request. Nothing was stored." |
| Timeout | `LoginTimedOut` | §4.7 |
| Cannot bind | `LoginFailed` | §4.3 |
| Callback `error` other than `access_denied` | `LoginFailed` | "Xping Cloud returned {error}: {error_description}" |
| `invalid_grant` on exchange | `LoginFailed` | §4.6 |
| `invalid_client` | `CloudUnreachable` | "Xping Cloud did not recognise this CLI. Check `--cloud-url` ({url})." |
| `invalid_request`, `unauthorized_client`, `unsupported_grant_type`, `invalid_scope` | `LoginFailed` | "Xping Cloud rejected the request ({error}): {error_description}. This is a CLI or server bug; please report it." |
| Discovery failure, TLS error, network error | `CloudUnreachable` | "Could not reach Xping Cloud at {url}: {reason}." The reason names the exception category only (connection failed, name not resolved, TLS error, timeout). "Connection failed" and "TLS error" stay broad on purpose: refused, reset and unreachable share one category in `HttpRequestError`, and so do certificate and handshake failures. |
| Version checks | `CloudVersionMismatch` | §2.3 |
| Ctrl+C | 130 | "Cancelled." |

### 4.9 Single-use listener shutdown

`LoopbackListener` implements `IAsyncDisposable`. `Stop` is called exactly once, from a `finally`,
on success, failure, timeout and cancellation. `HttpListener.GetContextAsync` has no cancellation
parameter, so cancellation is implemented by `cancellationToken.Register(listener.Stop)` and by
catching the resulting `HttpListenerException`/`ObjectDisposedException`. The response to the
successful callback is flushed and closed before the token exchange starts, so the browser does not
hang while the CLI talks to the Portal.

---

## 5. Browser launch and headless detection

### 5.1 Launch per OS

`BrowserLauncher.TryOpen(Uri)` returns `bool`; it never throws and never blocks longer than
2 seconds waiting for the launcher process to start. The URL is passed as a single argument; it is
never inserted into a shell command line.

| OS | Command |
|---|---|
| Windows | `Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })` |
| macOS | `open <url>` |
| Linux | `xdg-open <url>`; if it is not on `PATH`, try `gio open <url>`, then `sensible-browser <url>`; then give up |
| WSL (§5.2) | `wslview <url>` when present (wslu); otherwise `/mnt/c/Windows/System32/rundll32.exe url.dll,FileProtocolHandler <url>` |

`stdin`, `stdout` and `stderr` of the launcher are `/dev/null` on Unix, set by running it through
the constant script `sh -c 'exec "$0" "$@" </dev/null >/dev/null 2>&1' <launcher> <args…>`, so the
URL is an argument and never script text. Pipes would close when the CLI exits and fail the
browser's next write (the launcher often execs the browser, which inherits them), and the CLI's own
streams would let the browser write into a `--json` document on stdout. A non-zero exit of `open`/`xdg-open` within the
2 seconds counts as "not opened", and the URL hint is shown (it is shown anyway, §3.2).

WSL note: with the default WSL 2 configuration Windows forwards `127.0.0.1:{port}` to the WSL
instance, so the loopback flow works from a Windows browser. If it does not on a given machine,
the 5-minute timeout message names `--device` (§4.7).

### 5.2 Headless detection

`HeadlessDetector.Detect(env, fileSystem)` returns `Interactive`, `Headless(reason)` or `Wsl`:

| Signal | Result |
|---|---|
| `WSL_DISTRO_NAME` or `WSL_INTEROP` set | `Wsl` (browser launch via §5.1 WSL row) |
| `SSH_CLIENT`, `SSH_TTY` or `SSH_CONNECTION` set | `Headless("SSH session")` |
| Linux, not WSL, neither `DISPLAY` nor `WAYLAND_DISPLAY` set | `Headless("no display")` |
| `/.dockerenv` or `/run/.containerenv` exists, or `REMOTE_CONTAINERS`, `CODESPACES` or `DEVCONTAINER` set (any OS) | `Headless("container")` |
| otherwise | `Interactive` |

When headless, `login` does not try to open a browser, prints the URL with the "on this machine"
wording of §3.2, and adds the `--device` suggestion. It keeps waiting (contract §3.1: not a
failure). `--no-browser` forces the same behaviour without the detection, with the first line of
§3.2 for `--no-browser`.

The detector only reads environment variables and two file paths, all through injectable
providers, so tests cover every row (§18.1).

---

## 6. Device flow

### 6.1 Start

After discovery, `OAuthClient.StartDeviceAsync(workspaceId)` (contract §4.5). The response DTO:
`DeviceAuthorization(DeviceCode, UserCode, VerificationUri, VerificationUriComplete, ExpiresIn,
Interval)`. `Interval` defaults to 5 when absent; `ExpiresIn` is required. A `user_code` with a
control character is an incomplete response (`CloudUnreachable`): it is printed as sent, and
nobody could type it. Errors are mapped as §4.8.

### 6.2 Prompt

§3.2 text. `user_code` is shown exactly as the server sent it (`XXXX-XXXX`). `login --device`
MAY try `BrowserLauncher.TryOpen(VerificationUriComplete)` when the environment is `Interactive`
and `--no-browser` is absent; failure to open changes nothing (contract §3.2 step 3).

### 6.3 Polling

```
interval = response.interval
deadline = now + response.expires_in
loop:
  wait min(interval, deadline - now) (TimeProvider-based delay; cancellable)
  if now >= deadline: fail LoginTimedOut ("The sign-in code expired. Run xping login --device again.")
  r = PollDeviceAsync(device_code)
  success            → store (§7), print success block, exit 0
  authorization_pending → continue
  slow_down          → interval += 5 s; continue
  access_denied      → LoginDeclined
  expired_token      → LoginTimedOut, same message as the deadline
  invalid_grant      → LoginFailed ("The device code was rejected. Run xping login --device again.")
  network error / 5xx / 429 → continue at the next interval (never faster; contract §3.2);
                       the first one prints a warning on stderr
  any other error    → as §4.8 (invalid_client → CloudUnreachable; invalid_request and the
                       other client-bug codes → LoginFailed "please report it"); otherwise
                       LoginFailed with error and error_description, control characters dropped
```

The warning reads "! Could not reach Xping Cloud ({reason}). Still waiting; an approval made in
the meantime is not lost." Without it a user whose network dropped waits out the code's lifetime
with no sign of trouble.

The CLI MUST NOT poll faster than `interval`, including after a network error; the delay is
always waited before the next request. `device_code` is held in memory only and cleared after
the loop.

---

## 7. Credential store

### 7.1 Options evaluated

| Option | Windows | macOS | Linux | Verdict |
|---|---|---|---|---|
| `Microsoft.Identity.Client.Extensions.Msal` (`Storage` class) | DPAPI-encrypted **file**, not Credential Manager | Keychain via P/Invoke | libsecret via P/Invoke, optional plain-file fallback | Proven code, MIT, maintained. But Windows storage is a DPAPI file, which does not match the fixed decision "Windows Credential Manager", and the package brings MSAL itself. Not chosen; its P/Invoke signatures are the reference for §7.3. |
| `Meziantou.Framework.Win32.CredentialManager` | Credential Manager | — | — | Good, but Windows only; two more libraries would still be needed. |
| `Tmds.DBus.Protocol` (Secret Service over D-Bus) | — | — | Managed, no native library | Works without libsecret installed, but adds a package and ~300 lines of protocol code (sessions, unlock prompts, collections). More surface than P/Invoke to libsecret. |
| `git credential` helpers, `security`/`secret-tool` CLIs | via process | via process | via process | Depends on tools being installed; secrets pass through argv or stdin of a child process. Rejected. |
| **Thin native wrappers in the CLI** | `advapi32` `CredRead/CredWrite/CredDelete` | `Security.framework` `SecItem*` | `libsecret-1` loaded with `NativeLibrary.TryLoad` | **Chosen.** No package dependency, exact semantics of the fixed decision, ~150 lines per backend, each behind `ICredentialStore` and unit-testable through the file backend. Signatures follow MSAL's, which are known to work. |

### 7.2 Stored data shape

One `CredentialRecord` per Cloud URL, serialized with `IXpingSerializer` (camelCase):

```json
{
  "schemaVersion": 1,
  "cloudUrl": "https://app.xping.io",
  "refreshToken": "<opaque, ≤ 2048 bytes>",
  "accessToken": "<jwt or null>",
  "accessTokenExpiresAt": "2026-09-29T10:15:00Z",
  "workspaceId": "01J8...",
  "sid": "01J8...",
  "sub": "01J8...",
  "email": "jane@example.com",
  "dataGatewayUri": "https://api.xping.io",
  "storedAt": "2026-09-29T10:00:00Z"
}
```

`accessTokenExpiresAt` is `now + expires_in` computed by the CLI from the token response, not
read from `exp`, so clock skew between machine and server is applied consistently. `workspaceId`,
`sid`, `sub` and `email` are copied from the decoded (unverified) access token payload at store
time, for display only (contract §6.1). `code_verifier`, `state`, the authorization code and the
device code are never part of the record (contract §10.4).

`schemaVersion` is `1`. A record with another version is treated as corrupt (§7.7).

### 7.3 Backends

`ICredentialStore`:

```csharp
internal interface ICredentialStore
{
    CredentialStoreKind Kind { get; }                        // Keychain, File
    string DisplayName { get; }                              // "macOS Keychain", "Windows Credential Manager", "Secret Service", "~/.xping/credentials.json"
    Task<CredentialReadResult> ReadAsync(string cloudUrl, CancellationToken ct);
    Task WriteAsync(CredentialRecord record, CancellationToken ct);   // create or replace, atomic per Cloud URL
    Task<bool> DeleteAsync(string cloudUrl, CancellationToken ct);
}

internal sealed record CredentialReadResult(CredentialRecord? Record, string? Warning);
```

A corrupt entry (§7.7) and a refused file (§7.5) both read as "no record" with one `Warning` for
the user; `auth status` and `report` need that text, so it travels with the result instead of
being logged. A failure to reach the backend at all (I/O error, access denied) throws
`CredentialStoreException`; §7.4 says what the callers of a backend do with it.

Entry naming (contract §10.4): service/label `xping-cli`, account/target = the normalized Cloud
URL. Concretely:

| Backend | API | Entry |
|---|---|---|
| `WindowsCredentialStore` | `CredWriteW`, `CredReadW`, `CredDeleteW`, `CredFree`; `CRED_TYPE_GENERIC`, `CRED_PERSIST_LOCAL_MACHINE` | `TargetName = "xping-cli:{cloudUrl}"`, `UserName = "xping-cli"`, `CredentialBlob` = UTF-8 JSON of the record |
| `MacOsKeychainStore` | `SecItemAdd`, `SecItemCopyMatching`, `SecItemUpdate`, `SecItemDelete` with `kSecClassGenericPassword`; CoreFoundation dictionaries built and released in the wrapper | `kSecAttrService = "xping-cli"`, `kSecAttrAccount = cloudUrl`, `kSecValueData` = JSON. `kSecAttrAccessible` is not set (login keychain default). |
| `LibSecretStore` | `secret_password_storev_sync`, `secret_password_lookupv_sync`, `secret_password_clearv_sync` and `secret_password_free` from `libsecret-1.so.0`, with a `SecretSchema` named `io.xping.cli` and two string attributes `service` = `xping-cli`, `cloud` = cloudUrl passed as a `GHashTable` (`g_hash_table_new`, `g_hash_table_insert`, `g_hash_table_unref`, `g_str_hash`, `g_str_equal`); `GError` read and freed with `g_error_free` from `libglib-2.0.so.0`. The `v` forms are used because the variadic ones cannot be called portably from .NET (variadic arguments follow a different calling convention on arm64). | label `xping-cli ({cloudUrl})`, secret = JSON, collection `SECRET_COLLECTION_DEFAULT` |
| `FileCredentialStore` | §7.5 | one file for all Cloud URLs |

P/Invoke uses `LibraryImport` with `StringMarshalling.Utf16` (Windows) or `Utf8` (Unix) and
`SetLastError` where the API defines it. libsecret is optional on Linux, so `LibSecretStore` binds
its functions at run time (`NativeLibrary.TryLoad`, `GetExport`, function pointers) instead of with
`LibraryImport`; a missing library is a fallback reason, not a crash.

The OS backends implement `IKeychainCredentialStore`, which adds what the selector and the size
rule need:

```csharp
internal interface IKeychainCredentialStore : ICredentialStore
{
    int? MaxSecretBytes { get; }                             // 2560 on Windows, null elsewhere
    KeychainProbe Probe(string cloudUrl);                    // §7.4
}

internal sealed record KeychainProbe(bool Available, string? Reason);
```

The service name (`xping-cli`) is a constructor argument, so the `Category=CredentialStore` tests
use `xping-cli-tests` (§17.2) and never touch a developer's real entry. `AnalysisMode=All` will require `[SupportedOSPlatform]`
on each backend and justified suppressions for the few interop analyzers that fire on CoreFoundation
signatures; the suppressions carry the reason as the code style requires.

**Size limits.** Windows generic credentials hold at most 2560 bytes (`CRED_MAX_CREDENTIAL_BLOB_SIZE`).
A record with a maximum-length refresh token (2048 bytes, contract §6.2) plus an access token does
not fit. Rule for every backend: `WriteAsync` first serializes the full record; if the backend
declares `MaxSecretBytes` and the JSON exceeds it, the store drops `accessToken` (which contract
§10.4 marks MAY) and serializes again; if it still exceeds, `WriteAsync` throws
`CredentialStoreException` and the command fails with `CredentialStoreError`. `TokenRefresher`
treats a missing `accessToken` as expired and refreshes on first use (§9.2). Only the Windows
backend declares a limit today.

**macOS prompts.** The `xping` tool shim is not code-signed by Apple, so the keychain identifies it
by hash; after every tool upgrade macOS shows one "xping wants to use your confidential
information" dialog for the existing item, and "Always Allow" silences it until the next upgrade.
This is documented in `docs/cli/command-reference.md` (§19 phase 8). It cannot be avoided without
signing the tool, which is out of scope.

### 7.4 Selection and the file fallback

`CredentialStoreSelector.Select(cloudUrl)` probes once per process (later calls return the same
result):

| OS | First choice | Available when | Otherwise |
|---|---|---|---|
| Windows | `WindowsCredentialStore` | always (`CredRead` of a probe name returns `ERROR_NOT_FOUND` or success) | file |
| macOS | `MacOsKeychainStore` | `SecItemCopyMatching` for a probe returns `errSecItemNotFound` or success | file, when the result is `errSecInteractionNotAllowed` (-25308) or `errSecAuthFailed` (-25293) — typical over SSH with a locked keychain, reason "keychain locked" — or `errSecNotAvailable` (-25291) or any other error, reason "keychain unavailable" |
| Linux | `LibSecretStore` | `libsecret-1.so.0` loads **and** `DBUS_SESSION_BUS_ADDRESS` is set or `$XDG_RUNTIME_DIR/bus` exists **and** a probe lookup returns without a `GError` | file |

The probe uses the real entry name for the current Cloud URL, so "available" also means "readable
for this user right now". The selector's decision is exposed on `ICredentialStore.DisplayName` and
shown by `auth status`.

**Read order is always keychain, then file**, regardless of which one the selector chose for
writes. A developer who logged in over SSH (file) and later runs in a GUI session (keychain
available) must still be logged in. A keychain whose probe failed is left out of the read order of
that process: reading it would fail the same way, and every `auth status` on a machine without a
keyring would then report a store failure (exit 17) instead of "not signed in" (exit 10). The
reason is already shown as `FallbackReason`. The SSH-then-GUI case is two processes with two
probes, so it is unaffected. Such a keychain is kept as `CredentialStores.Unreachable` unless the
probe says it cannot hold anything (libsecret not installed): `logout` still tries to delete from
it, and when that fails it warns that a sign-in made in a desktop session may remain there, without
failing the sign-out.

The probe reads the secret (a locked macOS keychain lists items but refuses their content), so the
backend keeps that result and hands it to the first `ReadAsync` of the same entry; a write or delete
drops it. One command therefore reads the keychain once: one access dialog after an upgrade, one
D-Bus round trip. `logout` deletes from both. `login` writes to the selected
store and, when that is the keychain, also deletes any file entry for the same Cloud URL so the
two never disagree.

These rules live in one place: `Select()` returns `CredentialStores`, which holds the selected
store (`Selected`, with its `DisplayName` and the selector's `FallbackReason`, `null` when the
keychain was chosen) and the read order. `CredentialStores.ReadAsync` returns the first valid
record with the store it came from, plus the warnings of every store it visited and the
`Failures` of every store that threw `CredentialStoreException`. A failing store does not stop
the lookup: a locked keychain must not hide a file login. `WriteAsync` writes to `Selected` and
clears the file entry when `Selected` is a keychain; a failure to write `Selected` is thrown, a
failure to clear the file entry is returned as a warning (the sign-in is stored, and the keychain is
read first, so the old entry is never used). `DeleteAllAsync` tries
every store, even after one fails, so `logout` removes whatever it can, and then throws one
`CredentialStoreException` naming each store that failed. Commands and the resolver use `CredentialStores`, never
a backend directly.

### 7.5 The file backend

Path: `~/.xping/credentials.json`, where `~` is `Environment.GetFolderPath(UserProfile)`. The
directory `~/.xping` is created with mode `0700` and the file with `0600` (contract §10.4):

- Unix: each missing directory level is created with `Directory.CreateDirectory(dir,
  UserReadWriteExecute)` (the overload applies the mode to the last level only). An existing
  `~/.xping` with group or other bits is tightened to `0700` before anything is written: the SDK
  puts its local store at `<repo>/.xping`, so a home directory that is a repository (a dotfiles
  repo) already has a `0755` `~/.xping`, and the store works the same at `0700`. The file is created with `new FileStream(tmp, new FileStreamOptions {
  Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, UnixCreateMode =
  UnixFileMode.UserRead | UnixFileMode.UserWrite })`, so the mode is `0600` from the first byte
  and no separate chmod step exists.
- Windows: after creation, `FileSecurity` with inheritance disabled and one ACE granting the
  current user `FullControl` (`FileSystemAclExtensions.SetAccessControl`). On Windows the file
  backend is only reached when Credential Manager fails, so this path is rare.

Writes are atomic: serialize to `credentials.json.tmp-{pid}-{random}` in the same directory
(unique per write, so two writers in one process never touch each other's file), set its mode,
then `File.Move(tmp, path, overwrite: true)`. The file holds every Cloud URL:

```json
{ "schemaVersion": 1, "credentials": { "https://app.xping.io": { ...record... } } }
```

Each entry is parsed on its own when it is looked up. A write or delete for one Cloud URL carries
every other entry over verbatim, a corrupt one included (§7.7: nothing is deleted
automatically). Read-modify-write is serialized within the process; across processes the last
move wins, which only matters for two concurrent logins to different Cloud URLs (refresh is
serialized by §9.4).

**Refusing unsafe files (contract §10.4).** Before reading on Unix, `File.GetUnixFileMode(path)`
is checked; if any group or other bit is set, the store does not read the file and the command
prints: "Refusing to read ~/.xping/credentials.json because other users can read it. Run
`chmod 600 ~/.xping/credentials.json` (and `chmod 700 ~/.xping`) and try again." `auth status`
reports `credential: none` with that warning; `report` degrades to local with the same one-line
hint; `login` overwrites the file with a correct mode and does not need the check. On Windows the
mode check is skipped; the ACL is set on write only.

An unsafe file's content is never carried into a safe one: a write on a refused file starts from
an empty file, so entries for other Cloud URLs are dropped, and a delete removes the file and
reports that it deleted something (nothing in it is trusted for any Cloud URL). Carrying them over would
give a `0600` file, and the next read would trust entries that another user could have written.

### 7.6 Warning the user about the fallback

- `login` prints, to stderr, after it stores to the file: "Stored credentials in
  ~/.xping/credentials.json because no OS credential store is available ({reason}). The file is
  readable only by you." `{reason}` is the selector's reason: "Secret Service not running",
  "libsecret not installed", "keychain locked", "keychain unavailable", "Credential Manager error".
- A refresh that rotates the token into the file writes the same notice to the `--verbose` log
  only, so a pipeline of `xping report --json` is not polluted on every refresh. It is still a
  line on stderr, never on stdout.
- `auth status` always shows the store in the `Credential` line and lists the reason under
  `warnings` in JSON.

### 7.7 Migration and corruption

- There is nothing to migrate: no credentials exist before this feature.
- A keychain entry or file record that fails to deserialize, has a `schemaVersion` other than `1`,
  lacks `refreshToken` or `dataGatewayUri`, or whose `cloudUrl` is missing or differs from the
  Cloud URL it is stored under, is **corrupt**. A credentials file that does not parse, or whose
  top-level `schemaVersion` is not `1`, makes every lookup in it corrupt; `login` replaces it, and
  a delete leaves it untouched and reports nothing deleted (it may hold another Cloud URL's login
  written by a newer CLI). The store returns `null` and reports a warning "Stored
  credentials for {url} are unreadable and will be replaced at the next `xping login`."
  Nothing is deleted automatically, so a bug in a new CLI version cannot wipe a login; `login`
  overwrites and `logout` deletes.
- A future schema change bumps `schemaVersion`; the reader of version *n* MUST accept version *n*
  only. Records written by a newer CLI are corrupt to an older one, by design (no compatibility
  contract).
- A file with a bad mode is not corrupt; it is refused (§7.5).

---

## 8. Credential resolver

### 8.1 Precedence

`CredentialResolver.ResolveAsync(configuration)` takes the `CliConfiguration` (§14), because it
needs the API key and where it came from as well as the Cloud URL, and returns one
`ResolvedCredential`. It makes no request and writes nothing. A corrupt or refused stored login
counts as "no stored login" and its warning is carried in `ResolvedCredential.Warnings`. A store
that cannot be read also counts as "no stored login", so `report` still degrades to the next row
or to local; its message is carried in `ResolvedCredential.StoreFailures`, and `auth status`
exits `CredentialStoreError` instead of `AuthRequired` when no credential was found and a store
failed (§3.4).

| Order | Source | `CredentialSource` | Header used |
|---|---|---|---|
| 1 | `--api-key <key>` | `ApiKeyFlag` | `X-API-Key` |
| 2 | stored login for this Cloud URL (§7.4 read order) | `StoredLogin` with `Store = Keychain \| File` | `Authorization: Bearer` |
| 3 | `XPING_APIKEY` (env), then `Xping:ApiKey` in `appsettings*.json` of the working directory (§14) | `ApiKeyEnv` / `ApiKeyConfig` | `X-API-Key` |
| 4 | none | `None` | no request is made |

Why this order (A-8): the flag is explicit input for this one command and must not be overridden
by stored state. The ambient key is almost always the SDK's upload key on a developer machine;
trying it first would cost one request per command to learn it has no read scope. The login costs
at most one refresh every 15 minutes. CI has no stored login and therefore still uses the key.

**Fallback rules.** The resolver picks one credential per command; the handler for `"xping-cloud"`
is chosen from it, and the two handlers are never both in the pipeline. Exactly one header is ever
set on any request (contract §7.1). The resolver moves to the next row only on a **definitive**
authentication outcome, never on a network error, timeout or 5xx (Cloud down for one credential
is Cloud down for all, and a second try only doubles the wait):

| Outcome with the current credential | Next step |
|---|---|
| No stored login | row 3 |
| Stored login: refresh answers `invalid_grant`, or a second 401 after refresh (§9.6) | tokens deleted per contract §3.3; the `login-required` hint is printed (§11.6); then row 3 if a key exists |
| API key: 403 `Error.ApiKey.InsufficientScope` (upload-only key) or `Error.ApiKey.FeatureNotAvailable` (plan without `ApiAccess`, contract §7.4) | nothing left to try; hint "This API key cannot read Cloud data ({scope or plan}). Sign in with `xping login` instead." |
| API key: any other 401/403 | nothing left; hint with `title` |
| Network error, timeout, 5xx, version mismatch | stop; `Cloud data unavailable` hint; no fallback |

The resolver owns *where* to fall back: `ResolvedCredential.FallbackToApiKey()` returns the row-3
credential (or `None`) for a stored login. The caller owns *when*: only the authenticated pipeline
(§9) sees the outcome, so the "definitive outcome only" rule is enforced and tested there.

The fallback happens at most once per command, and a rebuilt `HttpClient` pipeline is used for
the second credential so no header from the first can leak into it. An API key is used as-is; the
CLI does not validate its scope or plan before sending.

CI behaviour is unchanged: CI does not run the CLI with Cloud today, and when it does with an API
key, no login exists, so the key is used directly and no refresh happens.

### 8.2 Reporting the active source

- `auth status` (§3.4) is the canonical report.
- `--verbose` on any command prints one line to stderr at startup:
  `credential: stored login (macOS Keychain), workspace 01J8…, expires in 12 min` or
  `credential: API key (--api-key)` or `credential: API key (XPING_APIKEY)` or
  `credential: none (local only)`. When a fallback happened, a second line says
  `credential: fell back to API key (XPING_APIKEY) because the stored login is no longer valid`.
  Never the key or token (§15).
- `report --json` records the source in `context.cloud.credential` (§11.4), because an agent that
  reads the envelope needs to know whether Cloud data was even attempted.

### 8.3 `--verbose`

New global option, `bool`. It re-enables `Microsoft.Extensions.Logging` with a console provider
writing to stderr at `Information`, categories `Xping.Cli.*` only, and the `Redaction` filter of
§15.2 on every message. `System.Net.Http` logging stays off. This is the "future `--verbose`" the
comment in `Program.BuildHost` anticipates.

---

## 9. Authenticated HTTP pipeline

### 9.1 `BearerTokenHandler`

A `DelegatingHandler` that `CloudApiClientFactory` places in the `"xping-cloud"` pipeline (§2.2)
when the resolved credential is a stored login:

1. **Host guard.** If `request.RequestUri` is not under `record.dataGatewayUri` (scheme, host,
   port, and path prefix), throw `InvalidOperationException` before sending. The token is sent only
   to the DataGateway (contract §2, §10.2).
2. `token = await refresher.GetAccessTokenAsync(ct)` (§9.2). Set
   `Authorization: Bearer {token}`; remove any `X-API-Key` header defensively.
3. Send. If the response is `401` and `WWW-Authenticate` contains `error="invalid_token"`
   (contract §7.3), call `refresher.ForceRefreshAsync(ct)` once, rebuild the request (a new
   `HttpRequestMessage` with the same method, URI and headers; bodies are never present on
   reads), and send once more. The retried request carries the request option
   `BearerTokenHandler.RetriedAfterRefresh`. A second `401` is returned to the caller unchanged;
   `CloudApiClient` sees the option, maps it (§9.7) and deletes the stored tokens (contract §8.2).
4. `403` is never retried. `WWW-Authenticate` values are never logged verbatim; only the `error`
   parameter name.

`ApiKeyHandler` sets `X-API-Key` and nothing else; no refresh, no retry on 401.

### 9.2 Proactive refresh

`TokenRefresher.GetAccessTokenAsync`:

```
record = cached in-memory record (loaded once per process from the store)
if record.accessToken != null and record.accessTokenExpiresAt - now > 60 s: return it
return await RefreshAsync(force: false)
```

The 60-second margin is contract §3.3. `now` comes from `TimeProvider`.

### 9.3 Single-flight within one process

`TokenRefresher` holds a `SemaphoreSlim(1, 1)`. Concurrent callers (the enricher issues up to
four parallel requests, §11.5) wait on it; the first one refreshes, and every waiter re-checks the
in-memory record after acquiring the semaphore and returns without a second refresh when the token
is now fresh. `ForceRefreshAsync` follows the same path but skips the freshness check only for the
caller that observed the 401; a waiter that arrives after the refresh completed still gets the new
token without another round trip (it compares the token it sent with the current one).

### 9.4 Cross-process safety

Two `xping` processes for the same user and Cloud URL are a normal case (parallel test runs, an
agent and a human). Contract §6.6 gives a 30-second reuse leeway; the CLI MUST keep its use of an
old refresh token inside that leeway. Design:

1. `CrossProcessLock`: a lock file `~/.xping/locks/{sha256(cloudUrl) first 16 hex}.lock` opened
   with `FileShare.None`. Acquire with retry every 100 ms for up to 5 s; on timeout proceed
   without the lock (the leeway still protects) and log at verbose. The wait runs inside the
   10-second per-attempt timeout (§10.2), so it must be shorter than that, or the attempt would
   time out before the "proceed without the lock" rule could apply.
2. Under the lock, **re-read the record from the store**. If the stored `accessToken` is fresh
   (another process refreshed meanwhile), adopt it, release, return.
3. Otherwise `POST refresh_token` with the stored (re-read) refresh token, write the new pair
   with the store's atomic `WriteAsync`, update the in-memory record, release. The pair is written
   to the store the record was read from, not to the store `login` would select today: a refresh
   does not move a file sign-in into a keychain that has since become available.

Once the lock is held, the refresh request and the write of the new pair run to the end with no
cancellation token, even if the caller cancels or its attempt times out: after the server has
rotated the token, the new pair must reach the store, or the next process presents the old token
outside the leeway and the session is revoked. The caller stops waiting; the next caller waits for
that refresh. A stored record older than the one in memory (a refresh whose write failed, §9.5)
is not adopted; records are compared by `storedAt`.

Because the refresh token is re-read under the lock immediately before use, the time between
"read" and "present" is milliseconds, far inside the leeway. A long-running process never presents
a token it loaded minutes ago.

### 9.5 Atomic persistence of rotated tokens

`WriteAsync` replaces the whole record in one operation per backend (§7.3, §7.5). The new pair is
written before the old one is discarded from memory (contract §3.3 step 4). If `WriteAsync` fails
after a successful refresh, the process keeps using the new tokens in memory, prints the
`CredentialStoreError` warning, and the next process will present the old refresh token: inside
the leeway that works; outside it the session is revoked by reuse detection and the user is asked
to log in again. This is the accepted cost of a store failure; it is logged so support can see it.

### 9.6 Reuse detection and the re-login hint

`invalid_grant` on refresh:

1. Re-read the record from the store (without the lock if it is already held). If the stored
   refresh token differs from the one just presented, another process rotated it: retry the refresh
   **once** with the stored token.
2. Otherwise the session is gone (revoked, expired, reuse detected, membership removed; contract
   §3.3, §9). Delete the record for this Cloud URL from both stores, clear the in-memory record,
   and throw `LoginRequiredException`. The command that observes it prints "Your Xping Cloud
   sign-in is no longer valid. Run `xping login` to sign in again." `report` degrades to local
   output with that line as the hint (§11.6). `auth status`, on its next run, shows `none`.

No other error deletes tokens (contract §3.3): network errors, 5xx, `temporarily_unavailable` and
`server_error` are retried as §2.4 and then surfaced as `CloudUnreachable`.

### 9.7 401/403 mapping

Handled in `CloudApiClient` after the handler's single retry:

| Response | Action |
|---|---|
| 401 `Error.AccessToken.Invalid`, `Error.AccessToken.Expired` (second time) | delete tokens (§9.6 step 2) unless the store now holds a newer sign-in (a login in another terminal), `LoginRequiredException` |
| any other bearer 401 that was not retried after a refresh (no `invalid_token` challenge: a proxy page, a stripped header) | `CloudApiException` with `title`; tokens kept, no fallback |
| 401 `Error.Authentication.MissingCredentials` | `LoginRequiredException` without deleting (nothing was sent; CLI bug or no credential) |
| 400 `Error.Authentication.AmbiguousCredentials` | `CloudApiException("CLI bug: two credentials sent")`, surfaced as a bug message |
| 403 `Error.AccessToken.InsufficientScope` | `CloudApiException`, never retried, message from `detail` |
| 403 `Error.ApiKey.*` | same, with the plan hint of §8.1 for `FeatureNotAvailable` |
| 404 | `null` result for single-resource reads (the CLI treats "not in Cloud" as "no enrichment") |
| 426 | `CloudVersionMismatch` message (contract §11) |
| 429 | handled by the resilience pipeline (§10.3) once, then `CloudApiException` |
| 5xx | resilience pipeline, then `CloudUnreachable` |
| other 4xx | `CloudApiException` with `title` and `detail` |

Status is decided first, `title` second (contract §8.2).

---

## 10. Cloud API client

### 10.1 Endpoints and DTOs

`ICloudApiClient`, all `GET` under `{dataGatewayUri}/v1/` (contract §7.7):

| Method | Route | Response DTO (CLI-side, only the fields the CLI reads) |
|---|---|---|
| `ListProjectsAsync(pageNumber, pageSize)` | `/v1/projects` | `PagedResult<ProjectSummary(Id, DisplayName?, Slug?)>` — `DisplayName` and `Slug` are read when present (A-3, §11.2) |
| `GetProjectAsync(projectKey)` | `/v1/projects/{externalProjectId}` | `ProjectSummary` or `null` on 404 |
| `GetTestAsync(projectKey, fingerprint)` | `/v1/projects/{externalProjectId}/tests/{testFingerprint}` | `CloudTest(TestFingerprint, ConfidenceScore?, ScoreCategory?, EvidenceLevel, TotalExecutions, IsFlaky?, ScoreTrend?, ScoreDelta?, LastExecutedAtUtc?)` or `null` on 404 |

The MVP uses only these three. The sessions and score-factor routes are not called; adding one is
an additive change to this table. DTOs are `sealed record`s deserialized through `IXpingSerializer`
with unknown members ignored (contract §11). Route values are escaped with `Uri.EscapeDataString`.

### 10.2 Timeouts

| Client | Per attempt | Total |
|---|---|---|
| `"xping-oauth"` | 15 s (`HttpClient.Timeout`) | one request at a time; the §2.4 retries are explicit |
| `"xping-cloud"` | 10 s (resilience `AddTimeout`) | 30 s (`HttpClient.Timeout`) |

`CloudEnricher` additionally caps the whole enrichment of one `report` at **10 seconds** wall
clock (§11.5) so a slow Cloud cannot make a local report slow.

### 10.3 Retries

A `ResilienceHandler` that `CloudApiClientFactory` places first in each `"xping-cloud"` pipeline
(§2.2), built from `HttpRetryStrategyOptions` and `HttpTimeoutStrategyOptions` with
`HttpClientResiliencePredicates.IsTransient`, mirroring `AddXpingUploader` but without a circuit
breaker (the process is short-lived):

- Retry: `MaxRetryAttempts = 3`, exponential from 2 s with jitter, on what `IsTransient` accepts
  (`5xx`, `408`, `HttpRequestException`, `TimeoutRejectedException`); on `429` a **single** retry
  after `Retry-After` (contract §8.2) when it is 10 s or less, else give up. `Retry-After` is not
  honoured on other statuses (`ShouldRetryAfterHeader = false`), so a 5xx cannot stall the command.
- Never on `401`, `403`, `404`, `400`.
- Timeout per attempt as §10.2.

The bearer handler sits **inside** the resilience handler, so each attempt attaches a current
token, and the 401-refresh-retry of §9.1 is not counted as a resilience retry.

### 10.4 Graceful degradation

Any failure of the Cloud client during `report` (no credential, login required, unreachable,
version mismatch, timeout, store refused, project not resolved) results in:

- the local report exactly as today: same text, same JSON except the additive `cloud` fields
  (§11.4), same exit code;
- **one** line on stderr, chosen from §11.6. The `login-required` line is always printed, even
  when stderr is redirected, because a human reading a CI or agent log needs to know that a
  sign-in is the fix (Q-9, answered). Every other hint is printed only when stderr is a terminal
  or `--verbose` is set.

`report --json` in a pipe therefore stays silent on stderr in every case except an expired or
revoked login.

---

## 11. Integration into existing commands

### 11.1 Which commands

Only `xping report` (including `--id`). `where` and `clear` are local by definition. `report` is
enriched when all of these hold: a credential resolved (§8), the local report was produced (exit
paths 2 and the `--id` path 3 are decided before enrichment and unchanged), and enrichment finished
inside the budget.

### 11.2 Project binding

The DataGateway keys reads by the project key (`externalProjectId`), which the Cloud derives
server-side from the test assembly name (`ProjectKey.FromAssemblyName`) or takes from the SDK's
`XPING_PROJECTID` pin. Local sessions record the assembly name and the test fingerprint, not the
key. `ProjectResolver.ResolveAsync(assembly)` therefore tries, in order:

| # | Source | Cost | Cached? |
|---|---|---|---|
| 1 | `report --project <key>` (new option; applies to the whole report) | none | no |
| 2 | `XPING_PROJECTID` env var, then `Xping:ProjectId` from `appsettings*.json` in the report's directory (`report --directory`, else the working directory; §14.2), the same values the SDK reads | none | no |
| 3 | The pin stamped in the session: new custom property `Xping.ProjectId` written by the SDK's orchestrator next to `Xping.Mode` (`LocalSessionProperties`) when `ProjectPin` is set. **SDK change**, `src/Xping.Sdk.Core`, one constant and one line in the environment-info build. Sessions written before this carry no key and fall through. | none | no |
| 4 | Name match: `ListProjectsAsync` pages of 50 until a project whose `displayName` equals the assembly simple name (ordinal) is found. Needs the additive DataGateway change of A-3: `displayName` and `slug` on `ProjectResponse` (the collector sets `DisplayName` to the assembly name for derived projects). Until that field exists the response has no `displayName` and this step finds nothing. | one to a few requests per workspace | yes |
| 5 | Nothing: no enrichment for that assembly; hint line §11.6 | — | — |

The CLI MUST NOT reproduce `FromAssemblyName` locally. The Cloud owns that rule and may change it.

`ProjectCache`: `~/.xping/cache/projects/{sha256(cloudUrl) first 16 hex}/{workspaceId}.json`,
`{ "schemaVersion": 1, "entries": { "<assembly>": { "projectKey": "...", "slug": "...",
"source": "name-match", "resolvedAt": "..." } } }`, mode `0600`, TTL 24 h from `resolvedAt`.
Only step 4 results are cached, and only for a stored login: an API key carries no workspace id
to key the file by, so an API-key run repeats step 4. When no reported test is found under a key,
the key is checked: a name match that came from the cache is evicted and step 4 runs once more in
the same run (a match listed in this run is known to exist and is not listed twice); a key from
steps 1–3 is checked with one `GetProjectAsync`, and a 404 there is the "names no project" hint of
§11.6 rather than silent success. Step 3 uses only the newest session of each assembly: a pin
removed since must not be revived from an older run. `logout` deletes the directory for that Cloud
URL. The cache holds no secrets, but it does reveal project keys, so it lives under
`~/.xping` with the same directory mode as the credentials.

### 11.3 What is enriched and how it is labelled

Per flagged test in the envelope (`findings[].subject` with a fingerprint), `GetTestAsync` returns
the Cloud view. The three specs reserve the slots; this document fills them, and phase 7 (§19)
amends each spec with an "Amendment — Cloud data" section that quotes this section:

| Slot | Spec | Content |
|---|---|---|
| Header line 1 | report-format §8 | `· cloud` appended when at least one finding was enriched; `· cloud unavailable` is **not** shown (the hint line covers it) |
| Row trailer | report-format §8 | `confidence 0.62 · moderately reliable` from `confidenceScore` (two decimals) and `scoreCategory` split into lowercase words (`HighlyReliable` → `highly reliable`, `reliable`, `moderately reliable`, `unreliable`, `highly unreliable`; `insufficient data` or no score shows no trailer). It is its own trailer segment directly after `evidence {level}`, before the population token and the id; the `·` is the glyph set's separator, except in ASCII, where it is `-`: ASCII's separator is `|`, which also joins the trailer's segments and would make the category read as a field of its own. When the trailer without its path would pass the 72-column fence, the category is dropped (`confidence 0.62`): the trailer is cut from the left, which would otherwise remove the local evidence level, and the detail view always shows the category. The Cloud `evidenceLevel` is not on the row: the row already carries the local evidence level, and two evidence words would collide (Q-3, answered) |
| Latest-run contrast | latest-run §8 | `confidence 0.94 over 812 runs` from `confidenceScore` and `totalExecutions`, replacing the local contrast; the "failed on this branch" clause is **not** available from `TestResponse` and is left out (Q-4, answered) |
| Detail metrics block | finding-detail §8 | labelled pairs appended after the local metrics: `cloud confidence  0.62 (moderately reliable)`, `cloud evidence  robust, 812 runs`, `cloud trend  stable` when present |

Every Cloud value is labelled "cloud" or "confidence" in text, and lives under a `cloud` object in
JSON, so it can never be mistaken for a local statistic. **The text slots are therefore rendered
from those `cloud` objects by `TextReportRenderer`, not resolved into strings by the builder.**
The reserving specs put the contrast sentence and the detail pairs in the builder, which would put
Cloud values into `latestRun.failures[].contrast` and `findings[].metrics`; in JSON those two stay
purely local, and the three report specs carry an amendment saying so. Local analysis computes no
confidence score (fixed decision; `ImpactScorer` stays as it is). Layout is identical with and without Cloud: the
slots are empty strings when there is no data, which is what the specs already require.

### 11.4 JSON envelope changes (additive, `1.21` → `1.22`)

- `context.cloud` (nullable object): `{ "cloudUrl", "credential": "stored-login" | "api-key",
  "workspaceId": "..." | null, "status": "ok" | "partial" | "unavailable" | "login-required" |
  "not-attempted", "reason": "..." | null, "project": { "assembly": "...", "projectKey": "...",
  "source": "flag" | "env" | "config" | "session" | "name-match" } | null }`
  (`env` is `XPING_PROJECTID` or `Xping__ProjectId`, `config` is `Xping:ProjectId` in `appsettings*.json`). `null` when no credential
  resolved, so today's consumers see a new nullable field and nothing else.
- `context.cloud.reason` is the hint line of §11.6 without its `Cloud data unavailable: ` prefix
  (`null` when `status` is `ok` or `not-attempted`). `not-attempted` means a credential resolved
  but the envelope held no test with a fingerprint, so no request was made.
- `findings[].cloud` (nullable object): `{ "confidence": 0.62, "category": "moderately-reliable",
  "evidenceLevel": "robust", "runs": 812, "trend": "rising" | null, "delta": -0.03 | null,
  "flaky": true | null, "lastExecutedAt": "..." | null, "fetchedAt": "..." }`. `category`,
  `evidenceLevel` and `trend` are the Cloud's values in kebab case (`ModeratelyReliable` →
  `moderately-reliable`); `confidence` and `category` are `null` when the Cloud has no score.
  Group findings have no fingerprint and get no `cloud` object.
- `latestRun.failures[].cloud` (nullable): same shape.

Nothing existing is renamed, removed or re-typed. `schemaVersion` becomes `"1.22"`; the literal is
pinned in `ReportEnvelope.CurrentSchemaVersion`, `ReportEnvelopeTests`, `CliSurfaceTests` and
`docs/cli/command-reference.md` (finding-detail D9 lists the four places).

### 11.5 Budget and concurrency

`CloudEnricher` runs after `EnvelopeBuilder.Build` and before rendering. It resolves the project
once per assembly in the envelope, then fetches tests with at most **4** concurrent requests,
under one `CancellationTokenSource` with a 10-second budget. Findings that were fetched before the
budget ran out are enriched; `context.cloud.status` is `partial` when at least one was not. The
default report has at most 10 findings (`--top`), so the usual cost is one round of parallel
requests.

### 11.6 Hint lines (stderr, one line)

Every line is printed when stderr is a terminal or under `--verbose`; the login-required line is
printed always.

| Condition | Line |
|---|---|
| no credential | none (local mode is the default; the existing cloud invitation already covers discovery) |
| login required (§9.6) | `Cloud data unavailable: your sign-in is no longer valid. Run xping login.` — always printed (§10.4) |
| unreachable, TLS, timeout, 5xx | `Cloud data unavailable: could not reach https://api.xping.io ({category}). Showing local results only.` |
| version mismatch | `Cloud data unavailable: {§2.3 message}` |
| project not resolved for some assembly | `Cloud data unavailable: no matching Cloud project for {assembly}. Use --project <key>.` |
| a key from steps 1–3 names no project (nothing found under it, and `GetProjectAsync` answers 404) | `Cloud data unavailable: no Cloud project '{key}' (from {origin}). Check the key, or pass --project <key>.` |
| file refused | `Cloud data unavailable: {§7.5 message}` |
| plan (API key, 403 `ApiKeyFeatureNotAvailable`) | `Cloud data unavailable: This API key cannot read Cloud data (the workspace's plan does not include API access). Sign in with `xping login` instead.` |
| scope (API key, 403 `ApiKeyInsufficientScope`) | `Cloud data unavailable: This API key cannot read Cloud data (its scope does not include read). Sign in with `xping login` instead.` |
| 429 | `Cloud data unavailable: Xping Cloud is rate limiting requests; try again in a minute.` |
| other refusal | `Cloud data unavailable: Xping Cloud refused the request ({title}): {detail}` |
| defect in the enricher | `Cloud data unavailable: an unexpected error occurred. Showing local results only.` (the exception goes to the `--verbose` log) |

### 11.7 Existing behaviour that must not change

- Exit codes of `report` (§13 keeps 0–3 as they are).
- Text output when no credential is resolved: byte-identical to today; the golden tests
  (`GoldenReportTests`) prove it.
- `--json` output when no credential is resolved: identical except `schemaVersion` and the new
  `null` fields.
- `where`, `clear`, `--help` output, parse errors.

---

## 12. `logout`

### 12.1 Sequence

1. Read the record (§7.4 read order). None → "not signed in", exit 0.
2. Discovery from cache or network (§2.3). If discovery fails, skip to step 4 with the warning.
3. `OAuthClient.RevokeAsync(refreshToken)` with a 15-second timeout. `200` → revoked. Any error
   (network, 5xx after the §2.4 retries, `invalid_client`, unexpected status) → not revoked;
   remember the category for the warning. `invalid_request`/`invalid_grant` do not occur for
   revocation (contract §4.4 answers 200 for unknown tokens); if they do, treat as not revoked.
4. Delete the record from the keychain and from the file (§7.4), whether or not step 3 succeeded
   (contract §3.4). Delete the project cache directory for this Cloud URL (§11.2). Delete the
   discovery cache entry.
5. Print the result (§3.3). Exit 0 in every case except a store deletion failure, which is
   `CredentialStoreError` with "Could not remove stored credentials: {reason}. Remove them by hand:
   {entry name or file path}."

Logout never touches a browser session (contract §3.4) and never uses an API key: with only an
API key set, `logout` says "not signed in" and suggests unsetting `XPING_APIKEY`.

---

## 13. Exit codes

Today's codes 0–3 keep their meaning per command. New codes start at 10 so they never collide with
a `report` meaning and are easy to recognise in scripts:

| Code | Name | When | Commands |
|---|---|---|---|
| 0 | `Success` | as today; also `logout` in every completed case, `auth status` with a credential | all |
| 1 | — | as today (`report` threshold, `where`/`clear` failures, root without args) | existing |
| 2 | — | parse or validation error, including an invalid `--cloud-url`, `--project` or `--workspace`; an invalid `XPING_CLOUDURL` or settings file (`"error": "configuration"`); `report` unavailable | all |
| 3 | — | `report --id` not reported | existing |
| 10 | `AuthRequired` | `auth status` with no credential of any kind | `auth status` |
| 11 | `LoginDeclined` | consent denied in the browser | `login` |
| 12 | `LoginTimedOut` | 5-minute loopback wait or device code expiry | `login` |
| 13 | `InteractiveRequired` | `login` without a TTY or with `CI` set | `login` |
| 14 | `LoginFailed` | any other flow failure: bind or listener (`local_listener`), `invalid_grant`, other OAuth error | `login` |
| 15 | `CloudUnreachable` | discovery, network, TLS, `invalid_client` on an auth command | `login`, (never `report`) |
| 16 | `CloudVersionMismatch` | contract version or min CLI version, 426 | `login`, (never `report`) |
| 17 | `CredentialStoreError` | store unavailable at login, write failure, deletion failure; `auth status` with no credential when a store could not be read | `login`, `logout`, `auth status` |
| 130 | `Cancelled` | Ctrl+C | `login`, `logout`, `auth status` |

`report` never returns a code above 3; Cloud problems are hints (§10.4). The table is repeated in
`docs/cli/command-reference.md` (phase 8).

---

## 14. Configuration

### 14.1 Cloud URL

| Source | Form | Precedence |
|---|---|---|
| `--cloud-url <url>` | global option on every command | 1 |
| `XPING_CLOUDURL` | environment variable, `XPING_` + property name like every SDK variable | 2 |
| `Xping:CloudUrl` | `appsettings.json` / `appsettings.{env}.json` in the working directory (or `--directory`), read by `CliConfiguration` with the same file and environment rules as `BuildXpingConfiguration` in Core | 3 |
| default | `https://app.xping.io` (contract OQ-12) | 4 |

Validation (parse-time, exit 2): absolute URI; scheme `https`, or `http` only when the host is
`localhost` or `127.0.0.1` (contract §10.1; `[::1]` is not in the contract's list and is refused,
Q-5, answered); no user info, query or fragment; a path is allowed only as `/` (the issuer must equal the URL
followed by one `/`, contract OQ-17, so the Portal issuer has no other path). Normalization: lowercase scheme and host, default port
removed, trailing slash removed. The normalized string is the key for the credential store, the
discovery cache and the project cache. `https://App.Xping.io/` and `https://app.xping.io` are the
same login.

### 14.2 API key and project id

`--api-key <key>` (global) and `--project <key>` (`report` only) with the env and appsettings
equivalents `XPING_APIKEY` / `Xping:ApiKey` and `XPING_PROJECTID` / `Xping:ProjectId`. Same
precedence order as §14.1. `--api-key` is listed in `--help` with the note "prefer `XPING_APIKEY`
so the key does not land in shell history" (Q-8).

### 14.3 Documentation rows

`docs/configuration/configuration-reference.md` gains three rows in the quick reference table with
a "CLI only" marker in the description, and one section each in the same format as `LocalStorePath`:

| Setting | Type | Default | Environment Variable | Description |
|---|---|---|---|---|
| `CloudUrl` | string | `https://app.xping.io` | `XPING_CLOUDURL` | CLI only. Xping Cloud (Portal) URL used by `xping login` and Cloud-enriched reports |
| `ApiKey` | (existing row) | | `XPING_APIKEY` | add: "The CLI uses it for Cloud reads when no stored login is available. `--api-key` on the command line takes precedence over a stored login; the environment variable does not." |
| `ProjectId` | (existing row) | | `XPING_PROJECTID` | add: "The CLI uses it to bind local runs to a Cloud project." |

`CloudUrl` is **not** added to `XpingConfiguration` in Core: the SDK never needs it, and an unused
option with a validator would be dead weight. `CliConfiguration` is the CLI's own reader.

### 14.4 Caches under `~/.xping`

| Path | Content | Mode | TTL |
|---|---|---|---|
| `~/.xping/credentials.json` | §7.5, only when the file backend is used | 0600 | — |
| `~/.xping/cache/discovery/{hash}.json` | discovery document + `fetchedAt` | 0600 | 24 h |
| `~/.xping/cache/projects/{hash}/{workspaceId}.json` | §11.2 | 0600 | 24 h |
| `~/.xping/locks/{hash}.lock` | empty lock files | 0600 | — |

`xping where` is not changed to show these; `auth status --verbose` prints the paths in use.
The existing local store (`<repo>/.xping/`) is unrelated and unchanged.

---

## 15. Security

### 15.1 Rules

- Tokens, codes, verifiers, `state`, `device_code` and API keys MUST NOT appear in: logs at any
  level, `--verbose` output, exception messages, JSON output, browser pages, the process command
  line of any child process, or crash output. Contract §10.1.
- Every exception that escapes a command is caught in `Program.Run`; the message shown is the
  exception's message after `Redaction.Scrub`, and the stack trace is shown only under
  `--verbose`, also scrubbed.
- No token in a URL: all token-carrying requests are `POST` bodies or the `Authorization` header.
  The only values in URLs are `code` and `state` on the incoming loopback redirect, which the CLI
  reads and never re-emits, and `state`/`code_challenge` in the authorization URL that is printed
  for the user, which the protocol requires and which are not secrets.
- The authorization URL is the only long string printed; it is printed to stderr, never stdout.
- The CLI never follows redirects on the back channel (`AllowAutoRedirect = false` on both
  clients) and never accepts a redirect response as success.
- TLS errors fail closed. No option disables certificate validation. `http://` is accepted only
  per §14.1.
- `code_verifier` and the in-memory refresh token are cleared when no longer needed
  (`ZeroMemory` on byte arrays; strings are unavoidable for HTTP form encoding and are kept as
  short-lived as the API allows).

### 15.2 Redaction

`Redaction.Scrub(string)` is applied to every log message, every error message, and every
`--verbose` line. It replaces:

- The value of `Authorization` and `X-API-Key` headers when they appear as `Name: value`.
- Form fields `code=`, `refresh_token=`, `access_token=`, `id_token=`, `device_code=`,
  `code_verifier=`, `client_secret=`, `token=`, `state=` up to the next `&` or whitespace.
- The same names, and their camelCase forms (`refreshToken`, `accessToken`, …, as in the stored
  record of §7.2), as JSON members: the string value of `"name": "value"`. A token response body
  is the likeliest place for an opaque, non-JWT token to reach a message.
- Any string that looks like a JWT (`eyJ[A-Za-z0-9_-]*\.eyJ[A-Za-z0-9_-]*\.[A-Za-z0-9_-]+`).
  Both the header and the payload are Base64URL JSON objects, so both start `eyJ`. A looser
  pattern (three dot-separated runs of 10+ characters) also matches .NET type names such as
  `Microsoft.Extensions.Configuration.FileConfigurationProvider` and would mangle the stack traces
  `--verbose` prints.
- The current refresh token, access token and API key values themselves, when known to the
  process (exact substring match).

Replacement text is `[redacted]`. `System.Net.Http` diagnostics and `HttpClient` logging stay
disabled even under `--verbose`, because their messages include full URLs and headers.

### 15.3 File permissions

§7.5 and §14.4. Every file the CLI creates under `~/.xping` is `0600` in a `0700` directory on
Unix, with an owner-only ACL on Windows, created with the mode before content is written, never
chmod-ed afterwards. The only chmod is the tightening of an existing `~/.xping` itself (§7.5).

### 15.4 Loopback listener

§4.3–§4.5: `127.0.0.1` or `[::1]` only, one successful callback, `GET /callback` only, no files,
no request bodies, no reflected input except the `error` code.

---

## 16. Agent usage

The Xping skill and the MCP server run the CLI as a child process. Rules:

- They MUST call `xping auth status --json` (no network, §3.4) to learn whether Cloud data is
  possible, and MUST treat exit code `10` or `"credential": "none"` as "local only". They MUST
  relay `warnings` and the `login-required` status to the human when relevant.
- They MUST NOT run `xping login` in any form. If they do, §3.2 precondition 1 refuses with exit
  `13` and a JSON error document (`"error": "interactive_required"`) when `--json` was passed,
  because a child process of an agent has no TTY on stdin. The message tells the human to run
  `xping login` in their own shell.
- `xping report --json` is safe to run at any time: it never prompts, never opens a browser, and
  never returns a Cloud exit code. `context.cloud.status` says what happened; `login-required` is
  the machine-readable "the human must sign in".
- A stored login MAY be used by agents. Refresh happens silently under §9. Reuse detection cannot
  be triggered by an agent and a human in parallel because of §9.4.
- Agents SHOULD set `XPING_NO_BANNER=1` as today to keep stderr quiet, and MAY pass `--cloud-url`
  when a human configured one.

---

## 17. Cross-platform test matrix

### 17.1 Matrix

| Environment | Credential store | Browser launch | Loopback | Expected `auth status` store |
|---|---|---|---|---|
| Windows 11, terminal | Credential Manager | `UseShellExecute` | 127.0.0.1 | keychain (`Windows Credential Manager`) |
| macOS 14+, Terminal/iTerm | Keychain | `open` | 127.0.0.1 | keychain (`macOS Keychain`) |
| macOS over SSH, keychain locked | file | none (headless) | 127.0.0.1, URL printed | file, with warning |
| Ubuntu desktop with GNOME Keyring | libsecret | `xdg-open` | 127.0.0.1 | keychain (`Secret Service`) |
| Ubuntu server, no D-Bus session | file | headless | URL printed | file, with warning |
| Ubuntu with libsecret missing | file | as above | as above | file, reason "libsecret not installed" |
| WSL 2 (Ubuntu) | file unless a keyring runs; usually file | `wslview` or `rundll32` | 127.0.0.1 via Windows localhost forwarding | file |
| VS Code devcontainer | file | headless → `--device` hint | listener works but the host browser cannot reach it unless the port is forwarded; `--device` is the documented path | file |
| SSH to Linux | file | headless | URL printed; `--device` hint | file |

### 17.2 What is automated where

- Every PR on `ubuntu-latest` (existing `ci.yml`): unit tests, the fake-server flow tests
  (§18.2), the file backend, `LibSecretStore` with the library **absent** (fallback path), and
  the regression suites. No keyring is installed on the PR runner, and the CLI test step runs with
  `--filter "Category!=CredentialStore"` so a runner image that ships one never turns them on.
- Weekly on `main` only (A-7, Q-7), new workflow `cli-credential-stores.yml`, `schedule: cron
  '0 3 * * 1'` plus `workflow_dispatch`, matrix `windows-latest`, `macos-latest`,
  `ubuntu-latest`. It runs **only** `tests/Xping.Cli.Tests` filtered to
  `Category=CredentialStore`, about ten tests, on each runner:
  - Windows: `WindowsCredentialStore` round trip, blob-limit rule, delete, corrupt entry.
  - macOS: `MacOsKeychainStore` round trip, update, delete, corrupt entry (the runner's login
    keychain is unlocked).
  - Ubuntu: installs `gnome-keyring` and `libsecret-1-0`, starts `dbus-run-session` with an
    unlocked keyring, runs the `LibSecretStore` round trip; then a second step, outside that
    session, runs the fallback tests (library missing, no session bus), whose environment is
    injected.
  The job has `timeout-minutes: 15` and `concurrency` cancel-in-progress. Expected cost:
  under 5 runner minutes on Linux, ~3 on Windows (×2 billing), ~3 on macOS (×10 billing), so
  roughly 40 billed minutes per run, about 1200 per month if it ran daily. **Decision (Q-7,
  answered): it runs weekly**, about 160 billed minutes per month, plus
  `workflow_dispatch` for a manual run after a backend change. Developers run the same filter
  locally before touching a backend.
- Code coverage comes from the PR job only, which cannot run the native backends: the Windows and
  macOS stores never execute on Linux, and `LibSecretStore` needs a keyring the PR runner does not
  install. The three backend classes, and the OS switch in `ServiceCollectionExtensions` that
  constructs them, are `[ExcludeFromCodeCoverage]` with that reason; their tests are the weekly run
  above. Everything they share (`CredentialRecordCodec`, `ProbedSecret`, the selector and
  `CredentialStores`) stays measured, so keep logic out of the backends.
- Tests use the service/target name `xping-cli-tests` and a Cloud URL of
  `https://tests.invalid`, and delete what they create in `finally`, so a developer's real login
  is never touched.

### 17.3 Manual checks

Once per release candidate, recorded in the PR: the §18.4 checklist on the developer's own
machine (macOS) and on a Windows VM, plus one WSL 2 run. The remaining rows of §17.1 are covered
by the fake-server tests with the environment providers stubbed.

---

## 18. Testing strategy

### 18.1 Unit tests (`tests/Xping.Cli.Tests`, xUnit, no Moq; hand-written fakes as today)

| Class | Covers |
|---|---|
| `PkceTests` | verifier length and alphabet, challenge vector from RFC 7636 appendix B, state length, constant-time compare, zeroing |
| `CloudUrlTests` | normalization and validation table of §14.1 |
| `DiscoveryClientTests` | every validation row of §2.3, cache hit/expiry, unknown fields ignored |
| `OAuthClientTests` | form encoding, `client_id` on every grant, no `scope` on refresh, error mapping and retry delays with `FakeTimeProvider` |
| `LoopbackListenerTests` | real `HttpListener` on an ephemeral port: 404 for other paths, 400 for wrong state, keeps waiting, success page has no code/state, stops after success, cancellation, bind retry (port occupied by a `TcpListener` the test holds) |
| `HeadlessDetectorTests`, `BrowserLauncherTests` | every row of §5.2; launcher argument construction per OS with a fake process starter |
| `DeviceFlowTests` | polling state machine against `FakeCloud` (§18.2) with the real `OAuthClient`, as the loopback flow is tested, and `FakeTimeProvider`: pending, slow_down accumulation, denied, expired, network error keeps interval |
| `CredentialRecordTests`, `FileCredentialStoreTests` | round trip, atomic write, mode 0600/0700 (the mode assertions are skipped on Windows through a trait), refusal of group/world-readable file, corrupt record, multi-cloud file |
| `CredentialStoreSelectorTests` | selection with fake availability probes; read order keychain → file |
| `WindowsCredentialStoreTests`, `MacOsKeychainStoreTests`, `LibSecretStoreTests` | `Category=CredentialStore` (§17.2); on other OSes they are skipped, not failed |
| `CredentialResolverTests` | precedence table of §8.1, both fallback rows (no login → key; login invalid → key through `FallbackToApiKey`), corrupt or refused login → next row with its warning, shadowed-login and fallback-key reporting. "No fallback on network error" is the pipeline's decision and is tested by `Report_NoFallback_OnNetworkError` (phase 6) |
| `TokenRefresherTests` | proactive margin, single-flight (N concurrent callers, one refresh), re-read under lock adopts a fresher record, `invalid_grant` with a rotated stored token retries once, `invalid_grant` otherwise deletes and throws, store write failure keeps in-memory tokens |
| `BearerTokenHandlerTests` | host guard, header set, 401 `invalid_token` → one refresh and retry, second 401 passes through, 403 not retried, no `X-API-Key` |
| `CloudApiClientTests` | DTO mapping, 404 → null, status-then-title mapping table of §9.7, 429 `Retry-After` |
| `ProjectResolverTests`, `ProjectCacheTests` | resolution order, cache TTL, eviction on 404 |
| `CloudEnricherTests` | budget, partial status, four-way concurrency, label placement in the three slots, envelope additive fields |
| `RedactionTests` | every rule of §15.2 with positive and negative samples |
| `AuthExitCodesTests`, `AuthJsonTests` | codes table; JSON documents contain no token-shaped string |

### 18.2 The in-process fake Cloud

`tests/Xping.Cli.Tests/Cloud/FakeCloud.cs`, an `HttpListener` on an ephemeral `127.0.0.1` port
(the pattern of `MockApiServer` in the integration tests), implementing the contract:

- `GET /.well-known/openid-configuration` with configurable `issuer`, `xping_data_gateway_uri`
  (the same fake's `/gw` prefix), `xping_contract_version`, `xping_cli_min_version`.
- `GET /connect/authorize`: validates the contract §4.2 parameters (PKCE S256, exact scope set,
  loopback redirect URI, state present), then, per test script, redirects to the redirect URI with
  a code and the same state, or with `error=access_denied`, or with a **wrong** state, or does not
  redirect at all (timeout tests). The test plays the browser: it issues the `GET` with
  `HttpClient` (`AllowAutoRedirect = false`) and then follows the `Location` to the CLI's listener.
- `POST /connect/token`: all three grants; PKCE verification; single-use codes with a 60 s
  lifetime on a `FakeTimeProvider`; refresh rotation with the 30 s leeway and whole-session
  revocation on reuse after it; `authorization_pending` / `slow_down` / `expired_token` /
  `access_denied` scripts for device polling; `server_error` and `temporarily_unavailable` fault
  injection with counters.
- `POST /connect/device`, `POST /connect/revoke` (always 200, records the call).
- `/gw/v1/projects`, `/gw/v1/projects/{key}`, `/gw/v1/projects/{key}/tests/{fp}` returning
  scripted bodies; validates exactly one credential header (400 on both, 401 with
  `WWW-Authenticate` on none); accepts tokens it issued and answers 401 `invalid_token` for
  expired or revoked ones; scripted 403/404/429/5xx.
- Records every request (method, path, headers, form) so tests assert, for example, that the
  access token was never sent to the Portal and the refresh token was never in a URL.

Flow tests run `Program.Run` in-process with `isTerminal: true` (one flag for stdin, stdout and
stderr), and a test-only hook `Program.Run(..., configureServices)` that replaces `XpingHome` with a
scratch directory so `~/.xping` is isolated, `IBrowserLauncher` with a capturing fake,
`IHeadlessDetector` with a fixed answer, `TimeProvider` with `FakeTimeProvider`, and the
environment provider with a dictionary. The environment is always injected: CI runners set
`CI=true`, which `login` refuses (§3.2). The host registers a `CredentialStoreSelector` without a
keychain backend, so flow tests never read or write the developer's real keychain. Scenarios:

| Test | Scenario |
|---|---|
| `Login_Loopback_Success` | full flow, record stored with the §7.2 fields, success text, JSON document |
| `Login_Loopback_Denied` | `access_denied` → exit 11, nothing stored |
| `Login_Loopback_Timeout` | no redirect → `FakeTimeProvider` advances 5 min → exit 12, listener closed |
| `Login_Loopback_StateMismatch_ThenGenuine` | wrong state first (400, keeps waiting), then genuine → success |
| `Login_Loopback_StateMismatch_Only` | wrong state, then timeout → exit 12 |
| `Login_Discovery_IssuerMismatch`, `_ContractVersion`, `_MinCliVersion` | exit 16/15 before any authorize request (asserted by the request log) |
| `Login_Device_Success`, `_SlowDown`, `_Denied`, `_Expired` | polling scripts |
| `Login_NoTty` | `isTerminal: false` → exit 13, no request made |
| `Login_WithApiKeySet_Proceeds` | no warning, login proceeds, `auth status` then reports the login as active and the key as fallback |
| `Report_Enriched` | stored login → envelope has `cloud` fields, header `· cloud`, trailer text |
| `Report_Refresh_Rotation` | access token expired → refresh → new pair stored → report enriched |
| `Report_ReuseDetected` | fake revokes on reuse → `invalid_grant` → record deleted → local report, hint line, exit code unchanged |
| `Report_TwoProcesses_Refresh` | two `TokenRefresher` instances over the same file store refresh concurrently → both succeed, one refresh request or two inside leeway, store holds a valid pair |
| `Report_CloudDown` | 5xx/timeouts → local report, one hint, same exit code and stdout as without credential (except schema fields) |
| `Report_ApiKey_Precedence` | flag + env + stored login → `X-API-Key` (flag value) only, `credential: api-key`; env + stored login → `Bearer` only, `credential: stored-login`; env only → `X-API-Key` |
| `Report_Fallback_LoginInvalid_ThenKey` | stored login revoked on the fake → `invalid_grant` → tokens deleted, hint printed, then the env key is used and the report is enriched; the request log shows no request with both headers |
| `Report_Fallback_UploadOnlyKey` | env key answers 403 `InsufficientScope` → local report, scope hint, exit code unchanged |
| `Report_NoFallback_OnNetworkError` | stored login refresh times out → local report, one hint, no key request made |
| `Report_BothHeadersNever` | request log shows never both headers in any test (asserted in the fake's dispose) |
| `Logout_Revokes_ThenDeletes`, `Logout_Offline_DeletesAndWarns`, `Logout_NotSignedIn` | §12 |
| `AuthStatus_*` | each credential state, no request made (asserted) |

The four tests named in phase 6's exit criterion (`Report_Refresh_Rotation`, `Report_ReuseDetected`,
`Report_TwoProcesses_Refresh`, `Report_BothHeadersNever`) run in phase 6 at the pipeline level,
because `report` does not call Cloud before phase 7: they drive `CloudApiClientFactory` →
`CloudApiClient` → `BearerTokenHandler` → `TokenRefresher` against `FakeCloud` and assert the
store and the request log. Phase 7 extends them to run `xping report` and adds the report-level
assertions (hint line, exit code, envelope). The fallback decision of §8.1 belongs to the caller,
`CloudEnricher`, so `Report_Fallback_*` and `Report_NoFallback_OnNetworkError` are phase 7 tests.

### 18.3 Regression suites

- `X-API-Key`: `tests/Xping.Sdk.Integration.Tests/ApiCommunicationTests` and
  `XpingUploaderTests` unchanged and green; a new CLI test asserts the SDK's `AddXpingUploader`
  registration is untouched by `AddXpingCliAuth` (no handler added to the uploader client).
- Local-only: `GoldenReportTests`, `ShareableOutputTests`, `DocumentedSampleTests`,
  `CliSurfaceTests` unchanged except the schema literal; they run with no credential resolved and
  prove byte-identical text output. A new `CliSurfaceTests.Report_NoCredential_NoNetwork` wraps
  the run with a fake `HttpMessageHandler` that fails the test on any request.
- Exit codes: `ExitCodeTests` extended with the §13 table and a test that `report` never returns
  ≥ 10 for any Cloud failure script.

### 18.4 Manual verification against production (A-4)

Run once in phase 8 with the test account and workspace the owner creates, recorded in the PR
without any token or code:

1. `xping login` on macOS: browser opens, consent shows the workspace, success block, `auth
   status` shows Keychain.
2. `xping report` in a repo that uploaded runs from that workspace: rows show `confidence …`,
   header shows `· cloud`; `xping report --json | jq .context.cloud`.
3. Wait 16 minutes, `xping report` again: refresh happened silently (`--verbose` shows it).
4. Revoke the session on the Portal sessions page; `xping report --verbose` shows the hint and
   `auth status` shows `none`.
5. `xping login --device` from an SSH session to a Linux box without a keyring: file fallback
   warning, `auth status` shows file; `chmod 644` the file and confirm the refusal message.
6. `xping logout` online, then again: "not signed in".
7. `XPING_APIKEY=<team key> xping report`: `credential: api-key` in `--verbose`, same
   enrichment; with a Trial key: plan hint.
8. Windows VM: steps 1, 2 and 6 with Credential Manager (check the entry in the Credential
   Manager UI, then that it is gone after logout).

---

## 19. Implementation phases

Integration branch: `feat/cli-auth`, created from `main` in phase 0. One sub-branch per phase,
merged back by PR when its exit criterion holds. Sub-branches are named `feat/cli-auth-NN-<name>`
(for example `feat/cli-auth-00-scaffold`): git cannot hold `feat/cli-auth` and
`feat/cli-auth/…` at the same time, because a ref cannot be both a branch and a directory.
Every merge leaves `feat/cli-auth` building, green, and behaving exactly as `main` for anyone
without a credential.

| # | Branch | Work | Exit criterion |
|---|---|---|---|
| 0 | `feat/cli-auth-00-scaffold` | Global options `--cloud-url`, `--api-key`, `--verbose`; `CliConfiguration` (§14) with env and appsettings reading; `AuthExitCodes`; `Redaction`; `Program.Run` test hook and Ctrl+C token; `auth`, `login`, `logout` command shells that print "not implemented" and exit 14; docs rows of §14.3. | Build green; all existing tests green unchanged; `xping --help` lists the new commands; `CloudUrlTests`, `RedactionTests` green. |
| 1 | `feat/cli-auth-01-discovery-oauth` | `DiscoveryClient` + cache, `OAuthClient` with the §2.4 retries, DTOs, `"xping-oauth"` client; `FakeCloud` with discovery, token, device, revoke. | `DiscoveryClientTests`, `OAuthClientTests` green against `FakeCloud`; every §2.3 failure row has a test. |
| 2 | `feat/cli-auth-02-file-store` | `CredentialRecord`, `ICredentialStore`, `FileCredentialStore` with modes and atomic write, `CredentialStoreSelector` and `CredentialStores` (read order) with the file backend only, `CredentialResolver`. | `FileCredentialStoreTests`, `CredentialResolverTests` green on Linux and macOS locally; the refusal message verified by a test that chmods the file. |
| 3 | `feat/cli-auth-03-loopback-login` | `Pkce`, `LoopbackListener`, pages, `BrowserLauncher`, `HeadlessDetector`, `LoopbackFlow`, `LoginCommand` (loopback only), `AuthStatusCommand`, `LogoutCommand` (§12). | Flow tests `Login_Loopback_*`, `Logout_*`, `AuthStatus_*` green; manual login against production succeeds on the developer machine with the file store. |
| 4 | `feat/cli-auth-04-device-login` | `DeviceFlow`, `--device`, `--no-browser`, `--workspace`. | `Login_Device_*` green; manual device login over SSH succeeds. |
| 5 | `feat/cli-auth-05-keychains` | `WindowsCredentialStore`, `MacOsKeychainStore`, `LibSecretStore`, selector probes, size-limit rule, read-order rule, `cli-credential-stores.yml` (weekly, A-7). | `Category=CredentialStore` tests green on all three runners in one manual `workflow_dispatch` run; PR CI unchanged in duration (±1 min). |
| 6 | `feat/cli-auth-06-authenticated-pipeline` | `TokenRefresher`, `CrossProcessLock`, `BearerTokenHandler`, `ApiKeyHandler`, `"xping-cloud"` client with resilience, `CloudApiClient`, error mapping, `FakeCloud` gateway routes. | `TokenRefresherTests`, `BearerTokenHandlerTests`, `CloudApiClientTests`, `Report_Refresh_Rotation`, `Report_ReuseDetected`, `Report_TwoProcesses_Refresh`, `Report_BothHeadersNever` green at the pipeline level (§18.2; phase 7 runs them through `report`). |
| 7 | `feat/cli-auth-07-report-enrichment` | SDK pin stamp (§11.2 step 3), `ProjectResolver` + cache, `--project`, `CloudEnricher`, envelope `1.22`, the three renderer slots, amendments to the three report specs, `context.cloud`. Depends on the DataGateway `displayName`/`slug` change for step 4; steps 1–3 work without it. | Goldens unchanged; `Report_Enriched`, `Report_CloudDown`, `Report_ApiKey_Precedence`, `Report_NoCredential_NoNetwork` green; `ReportEnvelopeTests` updated for `1.22`. |
| 8 | `feat/cli-auth-08-docs-verification` | `docs/cli/command-reference.md` (commands, exit codes, macOS prompt note, WSL and devcontainer notes), `README.md` roadmap lines, adapter READMEs where they mention `xping login`, `nuspec/README.Cli.md`; §18.4 manual verification recorded in the PR; remove `feat/cli-auth` from the `pull_request` branches in `.github/workflows/ci.yml` (added in phase 0 so the phase PRs run CI). | Docs build (`docfx`) green; verification record attached; `feat/cli-auth` ready for one PR to `main`. |

Each PR names the contract sections it implements and the tests that prove them. A phase that
finds a contract or spec conflict stops and reports.

---

## 20. Open questions

| # | Question | Needed by |
|---|---|---|
| Q-1 | *Answered 2026-09-29.* Filed as [xping-dev/dashboard#333](https://github.com/xping-dev/dashboard/issues/333): `displayName` and `slug` on `ProjectResponse`. Phase 7 step 4 waits for it; steps 1–3 do not. | — |
| Q-2 | *Answered 2026-09-29.* The id is enough for the MVP. `login` shows the workspace id and makes no name lookup (§3.2). | — |
| Q-3 | *Answered 2026-09-30.* Option A: the row trailer uses `scoreCategory`; `evidenceLevel` appears only in the detail block and in JSON (§11.3). | — |
| Q-4 | *Answered 2026-10-07.* Leave the clause out: the latest-run contrast is `confidence 0.94 over 812 runs` and no sessions request is made (§11.3). | — |
| Q-5 | *Answered 2026-09-29.* Yes: `http://[::1]` Cloud URLs are refused (§14.1). | — |
| Q-6 | *Answered 2026-09-30.* Superseded by A-8: the stored login now takes precedence over the ambient key, so `login` proceeds without a warning (§3.2) and `auth status` reports the key as a fallback (§3.4). | — |
| Q-7 | *Answered 2026-09-29.* Weekly (§17.2). The owner may raise the frequency later. | — |
| Q-8 | *Answered 2026-09-30.* Keep the flag, with the `--help` hint to prefer `XPING_APIKEY` (§14.2). | — |
| Q-9 | *Answered 2026-09-29.* Yes: the `login-required` hint is always printed; other hints stay terminal-or-verbose only (§10.4, §11.6). | — |

### Contract observations recorded while writing (no conflict found)

- Contract amendments of 2026-10-02/03, applied in phase 1: OQ-17 (`issuer` is `{Cloud URL}/`,
  §2.3), OQ-19 (revocation body may be `{}`, §2.4). OQ-18 (authorize request errors render a page
  and never redirect) needs no CLI change: the loopback flow meets them as its timeout (§4.7).
  OQ-20 (key withdrawal ends sessions with `invalid_grant`) is already §9.6.

- Contract §3.1 step 8 versus the failure table: resolved as A-6 ("one successful callback").
- Contract §3.1 step 2 says "port 0, then read the assigned port"; with `HttpListener` this is a
  probe-then-bind sequence (§4.3). Behaviour is the same; the three retries the contract allows
  cover the probe race.
- Contract §10.4 lists what is stored and does not forbid dropping the optional `access_token`,
  which §7.3 does only on Windows when the record exceeds 2560 bytes.
- Contract §1.3 names `xping whoami`; the CLI command is `xping auth status` (A-5). If the Portal
  sessions page copy says "match with `xping whoami`", the Cloud spec's phase 7 copy should say
  `xping auth status` instead; this is a note for the Cloud side, not a contract change.
- Cloud spec §9.6 (no-credential 401 title changes to `Error.Authentication.MissingCredentials`)
  is already what §9.7 handles; the SDK uploader is unaffected because the DataCollector does not
  change.
