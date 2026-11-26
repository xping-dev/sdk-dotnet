---
uid: troubleshooting-known-limitations
title: Known limitations
---

# Known limitations

This page lists what the SDK does not record, or records differently from what you might expect,
for each test framework. It is for developers who set up the SDK or read its results.

## Requirements

The MSTest adapter needs MSTest 3.8 or later. For the other minimum versions, see the
[quickstarts](../getting-started/quickstart-nunit.md).

## Framework-specific limitations

### NUnit

#### Ignored tests are not tracked

A test marked `[Ignore]` does not appear in Xping.

`XpingTrackAttribute` is an NUnit `ITestAction`, which runs only for tests that execute. NUnit leaves
out `[Ignore]` tests before execution starts, so `BeforeTest` and `AfterTest` never run for them.
There is no workaround. Xping tracks only the tests that run.

---

### MSTest

#### Ignored tests are not tracked

A test marked `[Ignore]` does not appear in Xping.

`XpingTestBase` records a test from its `[TestInitialize]` and `[TestCleanup]` hooks, which run only
for tests that execute. MSTest skips `[Ignore]` tests before these hooks run. There is no
workaround.

#### Retry attempts are numbered by counting

Two executions of the same test identity in one run are recorded as attempt 1 and attempt 2 of a
retry, whether or not a retry produced them.

MSTest runs the whole per-test lifecycle again for each retried attempt. A retry attribute derived
from `TestMethodAttribute` invokes the test method again, which builds a new test class instance and
runs `[TestInitialize]`, the method and `[TestCleanup]` once more. Xping therefore records **every
attempt** as its own `TestExecution`, with its own outcome, duration and failure text. A test that
fails and then passes has `AttemptNumber = 2` and `PassedOnRetry = true` on its last execution, so
the hidden failure is visible even though the build is green.

```csharp
// Both attempts recorded: attempt 1 Failed with its error, attempt 2 Passed
[Retry(3)]
public void FlakyTest()
{
    // ...
}
```

Nothing in `TestContext` says which attempt is running, so the adapter counts the executions it has
already recorded for the same test fingerprint. A test identity that passed starts a new count,
because a retry only follows an attempt that did not pass. But two `[DataRow]` rows with *identical*
values share a fingerprint, so a failing row followed by a repeat of itself looks like a retry.

A retry helper that runs only the method body again, without going through `ITestMethod.Invoke`,
skips `[TestInitialize]` and `[TestCleanup]`. Xping then sees one execution for the whole retry loop.

`AttemptNumber` comes from a `RetryAttempt` or `RetryCount` test property, or from an attempt marker
in the test name (`(Retry 2)`, `[Attempt 2]`), when a retry helper publishes one. The counted value
is the lowest number used.

---

### xUnit

This section covers xUnit v2. For xUnit v3, see [xUnit v3](#xunit-v3).

Tests with a `Skip` parameter are tracked, with `Outcome = Skipped`:

```csharp
[Fact(Skip = "Temporarily disabled - ticket #123")]
public void MyTest()
{
    // Tracked with Outcome = Skipped
}
```

#### Retry attempts are tracked only for retry libraries with a single-attempt hook

With some retry libraries, a test that fails and then passes on a retry is recorded as one passing
execution. `AttemptNumber` stays `1`, `PassedOnRetry` stays `false`, the hidden failure is not
recorded, and the duration is the total of all attempts.

xUnit has no retry support of its own. A retry library runs each attempt on a message bus of its
own, drops the messages of each attempt it retries, and then sends one result. Xping reads test
results from a message sink outside that bus, so it never sees the dropped attempts.

[xRetry](https://github.com/JoshKeegan/xRetry) makes `RetryTestCaseRunner.RunAsync` public, so other
xUnit extensions can supply the code that runs one attempt. Xping uses it, so it runs *inside* the
retry loop. Every attempt is recorded as its own `TestExecution`, with the right `AttemptNumber`, its
own duration and, for the attempts the retry hid, the failure message, stack trace and exception
type. xRetry still controls the retry count, the delays, the skip-on-exception handling and what the
runner sees, so the tests behave the same.

Libraries that run the retry loop inside their own test case's `RunAsync`, behind a private bus, have
no such hook. The retry sample in xUnit's documentation is one of them. Xping leaves those test cases
alone and records only the attempt the library reports.

```csharp
// Fully tracked: two executions, attempt 1 Failed and attempt 2 Passed
[RetryFact(3)]
public void FlakyTest()
{
    // ...
}
```

`AttemptNumber` is also read from a `RetryAttempt` trait, or from an attempt number in the display
name (`(attempt 2)`, `[Retry 2]`), when a library publishes one.

---

### xUnit v3

Skipped tests are tracked, including a test that skips itself while it runs (`Assert.Skip`,
`Assert.SkipWhen`). An explicit test (`[Fact(Explicit = true)]`) that the run leaves out is not
recorded, because it did not run.

#### xUnit v3 records only the last retry attempt

This applies to xRetry.v3 1.0. A test that xRetry.v3 retries is recorded once, as the attempt it
ended on. `AttemptNumber` and `PassedOnRetry` are right, so a test that passed on its second attempt
shows as a pass on retry. The attempts before it are not recorded: their failure messages and stack
traces are lost, and the recorded duration is the total of all attempts.

xRetry.v3 runs each attempt on a message bus of its own and drops every attempt but the last, as its
v2 version does. Unlike the v2 version, it has no hook to run an attempt through, so Xping cannot sit
inside the loop. It does announce each attempt (`Running test "…" attempt (2/3)`) on the run's own
bus, and the attempt number comes from there.

#### vstest needs `xunit.v3.mtp-off` on the .NET 10 SDK

On the .NET 10 SDK, `dotnet test` without a Microsoft.Testing.Platform `global.json` fails to build an
`xunit.v3` project, with "Testing with VSTest target is no longer supported by
Microsoft.Testing.Platform".

`xunit.v3` makes the test project a Microsoft.Testing.Platform app, and the .NET 10 SDK's vstest
refuses one. This is how xUnit and the .NET SDK behave, with or without Xping. Reference
`xunit.v3.mtp-off` instead of `xunit.v3` to stay on vstest, or move `dotnet test` to
Microsoft.Testing.Platform.

#### A timed-out test can crash the TRX report on Microsoft.Testing.Platform

This applies to xunit.v3 4.0.2 with Microsoft.Testing.Extensions.TrxReport 2.5.1. Sometimes a run with
`--report-trx` exits with code 7 ("test host crashed") after a `[Fact(Timeout = …)]` test times out.
The output shows an extra `[Test Cleanup Failure (…)]` result for that test and an
`InvalidOperationException` from `TrxReportEngine.GetClassAndMethodName`, and the TRX file is empty.
Xping still records the test as timed out. It happens more often on a busy machine, such as a CI
agent.

The test passed `TestContext.Current.CancellationToken` to the call that hung. xUnit cancels that
token at the timeout, so the abandoned task fails afterwards. xUnit v3 sometimes reports that failure
as a separate cleanup failure, which has no class or method name, and the TRX extension crashes on
it. This happens without Xping too: see [xunit/xunit#3669](https://github.com/xunit/xunit/issues/3669).
To avoid it, don't pass the token to a call that the timeout is meant to abandon (and suppress
`xUnit1069` there), or run without `--report-trx`.

#### A theory row is identified by the arguments in its display name

Two rows of one theory whose arguments display the same way, such as two different objects with the
same `ToString()`, share one identity and one history. A row's identity also changes when its display
text changes.

xUnit v3 passes a theory row that it did not pre-enumerate to Xping without its arguments. Only the
display name carries them. vstest and Microsoft.Testing.Platform pre-enumerate. xUnit's own console
runner, and a runner set to `preEnumerateTheories: false`, do not. Xping always reads the arguments
from the display name, so a row keeps the same identity however it runs.

The exception is a value that xUnit shortens in the display name, which then ends with `···`: a
string over 50 characters, a collection over 5 items, or a deeply nested object. A pre-enumerated row
with such a value is identified by its real arguments, so rows that differ after the cut stay apart.
A row that xUnit did not pre-enumerate still uses the shortened text. So such a row has a different
identity under xUnit's console runner than under vstest or Microsoft.Testing.Platform.

#### The runner is unknown under xUnit's own console runner

A run recorded by `xunit.v3.runner.console`, or by running the test executable directly without
Microsoft.Testing.Platform, has no `Xping.Runner` property. `xping report` then prints no command to
run a test again.

An xUnit v3 test assembly is its own executable on every runner, so the entry assembly does not name
the runner. Xping reads the runner from the runner's code on the call stack and from vstest's
environment, and leaves it out rather than guess.

---

### Timeouts

Xping records a test that the framework stopped for running past its timeout as `Outcome = Timeout`,
not `Failed`, with the time limit the test declared. The cases below are recorded differently, or not
at all.

#### A hang that stops the test host is not recorded

Each adapter records a test from a callback the framework raises after the test ends: MSTest's
`[TestCleanup]`, NUnit's `AfterTest`, xUnit's result message. If a hang stops the whole test host, no
callback runs and the test is not recorded. Each framework's own `[Timeout]` handles the usual case,
and Xping records that.

#### NUnit's blocking `[Timeout]` is not tracked

NUnit's `[Timeout]` on a synchronous test abandons the test thread without calling
`ITestAction.AfterTest`, so Xping never sees the result and records nothing for that test. Use
`[CancelAfter]` instead. It cancels the test cooperatively, and Xping tracks it:

```csharp
[Test, CancelAfter(500)]
public async Task MyTest(CancellationToken cancellationToken)
{
    // Tracked with Outcome = Timeout when it runs too long
    await Task.Delay(5000, cancellationToken);
}
```

#### MSTest's cooperative `[Timeout]` is recorded as failed or passed

With `[Timeout(..., CooperativeCancellation = true)]`, or with cooperative cancellation on for every
test (`CooperativeCancellationTimeout` in `.runsettings` or `testconfig.json`), MSTest cancels the
test's token instead of abandoning the test. When `[TestCleanup]` runs, nothing MSTest reports marks
the test as timed out:

- A test that observes the token ends with an `OperationCanceledException`. Xping records it as
  `Failed`.
- A test that ignores the token runs to the end. MSTest passes it, and Xping records `Passed`.

A non-cooperative timeout, which is MSTest's default, is recorded as `Timeout`, for synchronous and
async tests.

#### MSTest's run-wide timeout is recorded without a time limit

A test stopped by a timeout for the whole run (`TestTimeout` in `.runsettings` or `testconfig.json`)
is recorded as `Timeout`. Xping reads the time limit only from the test's own `[Timeout]`, so that
execution has none.

#### xUnit v2 applies a timeout only to async tests

xUnit v2 rejects `[Fact(Timeout = ...)]` on a synchronous test. It fails the test with "Tests marked
with Timeout are only supported for async tests". Xping records that as `Failed`, not `Timeout`,
because the test is set up wrongly rather than hanging.

## Fixture failures

Xping records **where** a failing test failed in `TestExecution.Site`: the test body, the per-test
setup or teardown, or a one-time fixture. It names the member in `FailureSiteMember`. When every
failure in a group agrees on one member, `xping report` reports a **broken fixture** that names it,
instead of N separate failing tests.

What Xping can see differs by framework. No framework reports a failure site of its own. The table
below records what running each framework showed, not what its documentation says.

### What is recorded

| Lifecycle member | NUnit | MSTest | xUnit v2 | xUnit v3 |
|---|---|---|---|---|
| Test body | `TestBody` | `TestBody` | `TestBody` | `TestBody` |
| Per-test setup | `TestSetup` (`[SetUp]`) | `TestSetup` (`[TestInitialize]`) | `TestSetup` (constructor, `InitializeAsync`) | `TestSetup` (constructor, `InitializeAsync`) |
| Per-test teardown | `TestTeardown` (`[TearDown]`) | **not recorded** | `TestTeardown` (`Dispose`, `DisposeAsync`) | `TestTeardown` (`Dispose`, `DisposeAsync`) |
| One-time fixture setup | **not recorded** | **not recorded** | `FixtureSetup` (`IClassFixture<T>`, `ICollectionFixture<T>`) | `FixtureSetup` (class, collection and assembly fixtures) |
| One-time fixture teardown | **not recorded** | **not recorded** | **not recorded** | **not recorded** |
| Assembly setup and teardown | **not recorded** | **not recorded** | **not recorded** | Setup: `FixtureSetup` (assembly fixtures). Teardown: **not recorded** |

A failure that Xping cannot place is recorded as `Unknown`. It is not assumed to be in the test body.
A group that contains one is reported as a plain shared failure. The report never names a member it
did not observe.

### NUnit: `[OneTimeSetUp]` and `[OneTimeTearDown]` are not observable

A broken `[OneTimeSetUp]` produces **no Xping records at all**, not failing ones.

`XpingTrackAttribute` is an `ITestAction`, and its hooks run only for tests that execute. When
`[OneTimeSetUp]` throws, NUnit marks the fixture's tests failed without running them, so neither
`BeforeTest` nor `AfterTest` runs, for the tests or for the fixture. The runner reports N failing
tests, and Xping sees none of them.

`[OneTimeTearDown]` runs after the last test is reported. So when it throws, Xping records that test
as **passed**, while the runner fails it.

To work around this, move the work into `[SetUp]`. It runs for each test instead of once, which
costs time, but Xping records it. A `[SetUp]` that breaks for every test in a fixture gives the same
broken fixture finding that a `[OneTimeSetUp]` would.

### NUnit: the framework reports no failure site

`ResultState.Site` is `Test` for every test-level result, including one whose `[SetUp]` threw. The
states that carry a site (`SetUpFailure`, `SetUpError`, `TearDownError`) are recorded on the
enclosing *suite*, which the adapter never sees. So Xping matches the stack trace against the
fixture's own lifecycle methods. A failure with no stack trace cannot be placed and stays `Unknown`.
`UploadStackTraces` does not change the site: Xping reads the site from the trace before the upload
leaves the trace out.

### MSTest: a throwing `[TestCleanup]` discards the whole record

The test produces **no `TestExecution` at all**.

MSTest runs `[TestCleanup]` methods from the derived class first and stops at the first one that
throws. `XpingTestBase.XpingTestCleanup` is declared on the base class. So when your own
`[TestCleanup]` throws, Xping's cleanup does not run, and the record is never built. The runner
reports the test as failed, and Xping reports nothing.

To work around this, wrap the body of your `[TestCleanup]` in a `try`/`catch`, and assert the failure
inside the test instead.

### MSTest: `[ClassInitialize]` and `[ClassCleanup]` are not observable

A broken `[ClassInitialize]` produces **no Xping records**. A broken `[ClassCleanup]` leaves its
tests recorded as **passed**.

When `[ClassInitialize]` fails, MSTest stops the class before any `[TestInitialize]` runs.
`[ClassCleanup]` runs after every test is recorded. The adapter has no hook at either point.
`[AssemblyInitialize]` and `[AssemblyCleanup]` are not observable for the same reason.

To work around this, move the work into `[TestInitialize]`, which Xping records.

### MSTest: a run finalized by the process-exit safety net loses its upload on vstest

When no `[AssemblyCleanup]` finalizes the run, the SDK finalizes it from a safety net that runs
as the process exits. It writes the local history, so `xping report` sees the run. But on vstest the
**Xping Cloud upload is usually lost**. On Microsoft.Testing.Platform the upload completes.

The safety net runs in `AppDomain.ProcessExit`, and vstest stops the test host 100 ms after the run
ends. Microsoft.Testing.Platform runs in the test process and lets the handler finish. A local file
write fits in that time, and a network round trip does not. The safety net runs when the test
assembly declares no `[AssemblyCleanup]`, because MSTest runs only the assembly hooks declared in the
test assembly itself. It also runs when your `[AssemblyCleanup]` throws before it finalizes Xping.

The SDK logs a warning when this happens. It starts with `[Xping] Process exiting with session …
still active`.

To fix this, declare an `[AssemblyCleanup]` in the test assembly that calls
`XpingContext.FinalizeAndShutdownAsync()`. If the safety net still runs on vstest, set
`VSTEST_TESTHOST_SHUTDOWN_TIMEOUT=5000` in the environment that runs `dotnet test`, so the upload has
time to finish.

### xUnit: fixture disposal is not attributed to a test

A class or collection fixture whose `Dispose` throws leaves its tests recorded as **passed**.

xUnit reports it as `ITestClassCleanupFailure`, `ITestCollectionCleanupFailure` or
`ITestAssemblyCleanupFailure`. These messages name no test, and they arrive after every test in the
class is reported. `XpingMessageSink` passes them to the runner without recording them, because there
is no test execution to attach them to.

Fixture *construction* is recorded. xUnit v2 wraps a failing `IClassFixture<T>` constructor in
`Xunit.Sdk.TestClassException` and names the fixture type in the message. This is the only failure
site that a framework reports directly. xUnit v2 does **not** wrap a failing `ICollectionFixture<T>`
constructor: the exception arrives as it is, and Xping recognizes it from its constructor frame.
xUnit v3 wraps a failing fixture of every scope, assembly, collection and class, in
`Xunit.Sdk.TestPipelineException`, and names its type.

---

## Local analysis

The limits of the CLI's local analysis are on
[Reading the report](../cli/reading-the-report.md#limits-of-local-analysis).

---

## General limitations

### Printed commands are quoted for one shell, chosen by `XPING_SHELL` or the OS

This applies to the drill-down, `History:` and repro commands that `xping report` prints, and to their
`--format json` fields.

The CLI can't see which shell started it. It quotes for the shell that `XPING_SHELL` names (`posix` or
`powershell`). Without it, it quotes by OS: PowerShell 7.3+ on Windows, POSIX everywhere else. The two
styles differ for a value that holds an apostrophe or a curly single quote (`‘ ’ ‚ ‛`), which
PowerShell doubles, and for a `\`, which PowerShell leaves bare and POSIX quotes.

- **Git Bash on Windows**, where coding agents usually run commands, gets PowerShell quoting unless
  you set `XPING_SHELL=posix`.
- **cmd.exe** isn't supported. It has no single quotes, so a quoted value is split at `|`, `&` or a
  space, and keeps its quotes.
- **Windows PowerShell 5.1** isn't supported. It passes a `"` inside an argument to `dotnet` without
  escaping it. So a repro filter that names an NUnit test with a string argument, like `Echo("x")`,
  breaks. Use PowerShell 7.3 or later.

### The repro command needs the project path from the build

To print a command that runs a test again, the CLI needs the test project's path. The SDK's build
targets write it into the test assembly as `[assembly: AssemblyMetadata("Xping.ProjectPath", ...)]`,
relative to the repository root.

The path is not written when the project is built outside a git repository, or with
`GenerateAssemblyInfo=false`. The printed command then has no project path, and you run it from the
project folder. See [The command](../cli/reading-the-report.md#the-command).

### Runs recorded in CI have no commit

The SDK records the commit only outside CI. So `xping report` shows no commit for a run recorded in
CI, and `xping report --since <sha>` matches only the runs recorded on a developer machine. To scope
CI runs, use `--since <date>` or `--runs`. See
[Command reference](../cli/command-reference.md#xping-report).

### Source location comes from the PDB, and points at the start of the body

A finding's last line ends with the file and line where the test is declared:

<!-- xping:sample docs-location rows -->
```
1.  HIGH  flaky
    SampleTests.FlakyTest_PassesOnRetry
    failed 5 of 10 executions (50%) in 5 of 5 runs, 1 failure mode
    evidence sparse | -env-cluster | f_8f042eab | .../SampleTests.cs:135
```

No framework reports this, so the SDK reads it from the assembly's Portable PDB, by the test method's
metadata token. Test Explorer finds it the same way. This has four limits.

**The line is where the body starts, not the attribute.** A PDB records where *code* is, and an
attribute is not code. For

```csharp
[Test]                       // line 40
public void Checkout()       // line 41
{                            // line 42  <- Debug build
    Assert.That(...);        // line 43  <- Release build
}
```

which line you get depends on the build configuration. A Debug build gives the opening brace its own
sequence point, so line 42 is reported. A Release build keeps no point for a brace that generates no
code, so the first *executable* statement, line 43, is the first line the PDB can name. The gap is not
always one line: when a body starts with a comment or a blank line, Debug reports the brace and Release
reports the first statement below them.

Both lines are inside the method, which is what the location is for. Neither points at the attribute
or the signature.

**No PDB, no location.** A build with `DebugType=none` has no symbols, and the location is left out.
Everything else about the test is still recorded. The default portable PDB (a `.pdb` file beside the
assembly) and `DebugType=embedded` both work. A test assembly copied to another machine without its
`.pdb` has no location either.

**Paths are made relative to the repository when possible.** A PDB stores the absolute path on the
machine that compiled the assembly. The SDK cuts it at the nearest enclosing `.git` or solution file,
and removes the `/_/` root that a deterministic CI build (`ContinuousIntegrationBuild=true`) writes.
When neither applies, such as an assembly built somewhere other than where it runs without a
deterministic build, the absolute path is recorded as it is.

**MSTest cannot tell overloads apart.** The MSTest adapter finds a test's `MethodInfo` from
`TestContext.FullyQualifiedTestClassName` and `TestContext.TestName`, which name a method but not its
parameters. When a test class has two overloads of the same test method name, the adapter takes the
one the runtime lists first, so the reported line may belong to the other one. The same limit applies
to the pinned fingerprint and the timeout. NUnit and xUnit pass the method itself and are not
affected.

---

## Reporting issues

If you find a limitation that is not on this page:

1. **Search the existing issues**: [GitHub Issues](https://github.com/xping-dev/sdk-dotnet/issues)
2. **Report a new one**: [Create an issue](https://github.com/xping-dev/sdk-dotnet/issues/new)

Include:

- the test framework and its version;
- the Xping SDK version;
- a minimal example that reproduces it;
- what you expected, and what happened.
