---
uid: flaky-tests-patterns-and-fixes
title: Flaky patterns and fixes
---

# Flaky patterns and fixes

This page lists the common causes of flaky tests. For each cause, it gives the symptom, what
`xping report` shows on your machine, what Xping Cloud reports, and a fix.

To find the flaky tests first, see [Finding flaky tests](finding-flaky-tests.md).

---

## Reproduce the failure

Open the finding with `xping report --id <id>`. Its `repro` line says how many runs you need to see
a failure 95% of the time. The `Repro:` line below the block is the command that reruns only that
test. Run it that many times, from the repository root. When the command names no project, run it
from the project folder. `-e XPING_ENABLED=false` keeps the reruns out of your history.

`xping report --test <name>` prints the same `repro` line under each finding about the test, and the
command as `Repro of row N:`. See
[Reproducing a finding](../cli/reading-the-report.md#reproducing-a-finding) for the output, and for
which findings get a command.

When the report prints no `Repro:` line, for example under the xUnit console runner, rerun the test
in a loop yourself. Use `=` in the filter, not `~`: `~Calc.Add` also runs `Calc.AddRange`.

```bash
for i in {1..10}; do dotnet test -e XPING_ENABLED=false --filter "FullyQualifiedName=MyApp.Tests.CalcTests.Add"; done
```

- When a retry hides the failure, remove the retry attribute first. Otherwise every run passes.
- When the test never fails alone, run the full suite. A test that fails only next to other tests
  points at shared state or at parallel runs.

---

## The patterns at a glance

| Pattern | `xping report` on your machine | Xping Cloud category |
|---|---|---|
| [Race conditions](#race-conditions) | `flaky`, `concurrency` | Concurrency, Timing |
| [External services](#external-services) | `flaky`, `shared failure`, `timing out` | Network, External dependency |
| [Shared state and test order](#shared-state-and-test-order) | `flaky`, `concurrency`, `broken fixture` | Order dependency, Concurrency |
| [Environment differences](#environment-differences) | nothing on one machine | Environment specific |
| [Time-based failures](#time-based-failures) | `time sensitive` | Unknown |
| [Random data](#random-data) | `flaky` | Unknown |
| [Retries that hide a regression](#retries-that-hide-a-regression) | `masked by retry`, `deeper retries`, `out of retries` | any category |

Xping Cloud gives a flaky test one category and a severity. The severity comes from how often the
test fails, not from the pattern. See [Finding flaky tests](finding-flaky-tests.md#categories).

---

## Race conditions

**Symptom.** The test fails now and then with no code change. It fails more when tests run in
parallel. It passes in the debugger.

**On your machine.** `xping report` shows `flaky`. When the failures follow the number of tests
running at the same time, it also shows `concurrency`.

**In Xping Cloud.** The category is Concurrency when the failures follow how many tests ran at the
same time. It is Timing when the duration varies a lot and runs time out.

**Fix.** Await the work that the test checks.

```csharp
[Test]
public async Task ProcessOrder_CompletesTheOrder()
{
    var order = Fixtures.CreateTestOrder();
    var processor = new OrderProcessor();

    // Without await, the assertion can run before ProcessAsync finishes.
    await processor.ProcessAsync(order);

    Assert.That(order.Status, Is.EqualTo(OrderStatus.Completed));
}
```

When the test cannot await the work, wait for a condition with a time limit. Do not wait a fixed
time with `Task.Delay`.

```csharp
public static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
{
    var stopwatch = Stopwatch.StartNew();
    while (!condition())
    {
        if (stopwatch.Elapsed > timeout)
        {
            throw new TimeoutException($"The condition was not met within {timeout}.");
        }

        await Task.Delay(TimeSpan.FromMilliseconds(50));
    }
}
```

---

## External services

**Symptom.** The test fails when a service is slow or down. It passes on your machine and fails in
CI. Several tests that call the same service fail together.

**On your machine.** `xping report` shows `flaky`. When three or more tests fail with the same error
in one run, it shows them as one `shared failure`. A test that mostly hangs until its timeout shows
`timing out`.

**In Xping Cloud.** The category is Network when the failures are network exceptions, such as
`SocketException` or `HttpRequestException`. It is External dependency when they are database or
file-system exceptions, such as `DbException` or `IOException`.

**Fix.** Replace the real service with a stub in unit tests. With `HttpClient`, give it a handler
that returns a fixed response:

```csharp
public sealed class StubHandler(HttpStatusCode status, string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
}

[Test]
public async Task GetUser_ReadsTheName()
{
    var handler = new StubHandler(HttpStatusCode.OK, """{"id":123,"name":"John Doe"}""");
    var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") };
    var client = new UserClient(http);

    var user = await client.GetUserAsync(123);

    Assert.That(user?.Name, Is.EqualTo("John Doe"));
}
```

Keep a few tests against the real service, and run them in a separate CI job. A failure there then
tells you about the service, not about your code.

---

## Shared state and test order

**Symptom.** The test passes alone and fails in the full suite. The result depends on which test ran
before it. The errors say "already exists" or "in use".

**On your machine.** `xping report` shows `flaky`, and `concurrency` when parallel runs make it
worse. When several tests fail in the same setup method, it shows one `broken fixture` and names
that method.

**In Xping Cloud.** The category is Order dependency when the result follows the test that ran just
before it. It is Concurrency when they follow how many tests ran at the same time.

**Fix.** Give each test its own state. Create it in the setup method, not in a static field:

```csharp
public class UserServiceTests
{
    // A static field is shared by every test in the class, and by parallel runs.
    private Database _database = null!;

    [SetUp]
    public void SetUp() => _database = new Database();

    [TearDown]
    public void TearDown() => _database.Dispose();

    [Test]
    public void Insert_AddsOneUser()
    {
        _database.Insert(new User(1, "Alice"));

        Assert.That(_database.Count(), Is.EqualTo(1));
    }
}
```

Give each test its own files and other named resources:

```csharp
[Test]
public void WriteFile_CreatesTheFile()
{
    // A fixed name collides when two tests run at the same time.
    var path = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.txt");

    try
    {
        File.WriteAllText(path, "content");

        Assert.That(File.Exists(path), Is.True);
    }
    finally
    {
        File.Delete(path);
    }
}
```

---

## Environment differences

**Symptom.** The test passes on your machine and fails in CI, or passes on Windows and fails on
Linux.

**On your machine.** `xping report` reads the runs of one machine, so it cannot compare two
environments.

**In Xping Cloud.** The category is Environment specific when the test fails in one environment much
more than in another. Xping Cloud compares the environment names that the SDK records, so set the
[`Environment` option](../configuration/configuration-reference.md#environment) in each place you
run the tests.

**Fix.** Look for what differs between the two environments:

- environment variables that CI sets and your machine does not, or the other way round;
- file paths: `\` on Windows and `/` elsewhere, and file names that differ only in case;
- the culture and the time zone of the machine;
- network access, and the time a call takes;
- memory, CPU and file-handle limits. CI machines are often slower.

Build file paths with `Path.Combine`, and set the culture in tests that format numbers or dates.

---

## Time-based failures

**Symptom.** The test fails at one time of day, at weekends, or near a daylight saving change. It
compares dates or times.

**On your machine.** `xping report` shows `time sensitive` when the failures gather at one time. It
compares three splits of the runs:

- each six-hour quarter of the local day against the rest of the day;
- weekend runs against weekday runs;
- when the runs have two UTC offsets for one time zone, as at a daylight saving change, the runs on
  each side of the change.

The finding needs at least five runs on each side, and failures on at least three different local
days. A single weekday, such as "only fails on Mondays", cannot be detected in the default window,
because the window holds at most two of each weekday. See
[`time sensitive`](../cli/reading-the-report.md#time-sensitive).

A test that reads the clock:

```csharp
[Test]
public void ScheduleEvent_CreatesEventForToday()
{
    var scheduler = new EventScheduler(TimeProvider.System);

    var evt = scheduler.CreateEvent("Meeting");

    // Fails when the test runs across midnight: the date changes between the two calls.
    Assert.That(evt.CreatedAt.Date, Is.EqualTo(DateTime.Now.Date));
}
```

The report shows:

<!-- xping:sample docs-time-sensitive rows -->
```
1.  MED   time sensitive
    SchedulerTests.ScheduleEvent_CreatesEventForToday
    failed 100% in 00:00-06:00 local against 0% in the rest of the day,
    gap 100 pts across 4 days
    evidence adequate | -env | f_3b0e91c4 | .../SchedulerTests.cs:18
```

The finding says when the test fails, not why. Its severity is never above medium, because the time
of day tells you where to look and not what to fix.

**In Xping Cloud.** Xping Cloud does not compare the time of day. A time-based test can still be
flaky there, with the category Unknown.

**Fix.** Pass the clock to the code as a `TimeProvider`, and use `FakeTimeProvider` in the test. It
is in the `Microsoft.Extensions.TimeProvider.Testing` package.

```csharp
public sealed class EventScheduler(TimeProvider clock)
{
    public Event CreateEvent(string name) => new() { Name = name, CreatedAt = clock.GetUtcNow() };
}

[Test]
public void CreateEvent_UsesTheCurrentTime()
{
    var now = new DateTimeOffset(2026, 1, 15, 23, 59, 59, TimeSpan.Zero);
    var clock = new FakeTimeProvider(now);
    var scheduler = new EventScheduler(clock);

    var evt = scheduler.CreateEvent("Meeting");

    Assert.That(evt.CreatedAt, Is.EqualTo(now));
}
```

When the code must read the real clock, compare with a tolerance:

```csharp
Assert.That(evt.CreatedAt, Is.EqualTo(DateTimeOffset.UtcNow).Within(TimeSpan.FromSeconds(5)));
```

---

## Random data

**Symptom.** The test fails now and then with an unexpected value, with no link to the time, the
environment or parallel runs.

**On your machine.** `xping report` shows `flaky`. A test that fails in different ways shows more
than one failure mode.

**In Xping Cloud.** The category is usually Unknown, because no other signal matches.

**Fix.** Give random data a fixed seed, and do not depend on an order that the code does not
promise.

```csharp
[Test]
public void Sort_PutsTheSmallestFirst()
{
    // A fixed seed gives the same numbers on every run.
    var random = new Random(12345);
    var numbers = Enumerable.Range(0, 100).Select(_ => random.Next()).ToList();

    numbers.Sort();

    Assert.That(numbers[0], Is.EqualTo(numbers.Min()));
}

[Test]
public void GetUsers_ReturnsAlice()
{
    var users = UserService.GetUsers();

    // GetUsers promises no order, so check for the item, not for its position.
    Assert.That(users.Select(u => u.Name), Does.Contain("Alice"));
}
```

---

## Retries that hide a regression

**Symptom.** The build is green, but the suite gets slower. A test with a retry attribute has "always
been a bit flaky". Or a test with a retry attribute fails the build anyway.

**On your machine.** `xping report` reads the attempts that the adapter recorded, never the limit in
the retry attribute. It shows one of three kinds:

- **`masked by retry`**: the test failed and then passed on a retry. The build stayed green.
- **`deeper retries`**: the test still passes, but its recent runs need more attempts than earlier
  runs. This kind appears while the build is still green. Its severity is never above medium,
  because no build has failed yet.
- **`out of retries`**: the retries ran out, and the test failed the run.

A test gets at most one of the three: `out of retries` first, then `deeper retries`, then
`masked by retry`. See [Retry kinds](../cli/reading-the-report.md#retry-kinds).

A test whose service got slower:

```csharp
[RetryFact(3)]
public async Task Checkout_CompletesWithinTheServiceBudget()
{
    var checkout = new CheckoutService();

    // The retry still saves the build. This test used to pass on the first attempt.
    // Now it needs the third.
    var result = await checkout.CompleteAsync();

    Assert.True(result.Succeeded);
}
```

The report shows:

<!-- xping:sample docs-retry-deepening rows -->
```
1.  MED   deeper retries
    CheckoutTests.Checkout_CompletesWithinTheServiceBudget
    attempts to pass 1 -> 3 (+2) over 3 runs against 14 before, 2.4s
    spent retrying
    evidence adequate | -env | f_5da7c018 | .../CheckoutTests.cs:27
```

And when the retries no longer save the build:

<!-- xping:sample docs-retry-exhausted rows -->
```
1.  HIGH  out of retries
    CheckoutTests.Checkout_CompletesWithinTheServiceBudget
    gave up after 3 attempts in 7 of 8 retried runs (87.5%), 41s spent
    retrying
    evidence robust | -env | f_9c14ab63 | .../CheckoutTests.cs:27
```

The second block is the reason to remove the retry attribute, not to raise its limit. The retries
cost time and no longer keep the build green.

**In Xping Cloud.** The confidence score reads each run by its last attempt, so a retried test can
score well. Flaky detection reads the first attempt of each run. A test that recovered on a retry is
always examined, and it can be flaky with any category. See
[Score and flaky are independent](../cloud/confidence-scores.md#score-and-flaky-are-independent).

**Fix.** Fix the cause, then remove the retry. A retry is reasonable for a short time while you
investigate, and for a test of a real external service that you do not control. It is not a fix for
a race condition or for shared state.

How well the SDK sees each attempt depends on the framework. See
[Known limitations](../troubleshooting/known-limitations.md). Where an adapter cannot record each
attempt, the report cannot tell a retried test from one that passed the first time.

---

## Skip a test while you fix it

To stop a flaky test from failing builds while you fix it, skip it with your framework's attribute.
Xping does not skip or quarantine tests. Base the decision on the flaky verdict and its severity, not
on the confidence score. A `critical` flaky test fails more than about half of its runs.

| Framework | Attribute |
|---|---|
| NUnit | `[Ignore("Flaky: race in OrderProcessor, see issue 123")]` |
| xUnit | `[Fact(Skip = "Flaky: race in OrderProcessor, see issue 123")]` |
| MSTest | `[Ignore("Flaky: race in OrderProcessor, see issue 123")]` |

Put the reason and an issue link in the message, so the test is not forgotten. The SDK does not
record ignored tests on NUnit and MSTest. See
[Known limitations](../troubleshooting/known-limitations.md#ignored-tests-are-not-tracked).

---

## After a fix

- **On your machine.** The finding goes away when the failing runs leave the report's window. The
  default window is the last 20 runs from the last 14 days.
- **In Xping Cloud.** The flaky verdict clears when two detections in a row find the test not flaky.
  Detection runs when an upload contains the test, and reads the last 14 days of runs. The old
  failures count until they are older than that.

---

## See also

- [Finding flaky tests](finding-flaky-tests.md)
- [Reading the report](../cli/reading-the-report.md)
- [Confidence scores](../cloud/confidence-scores.md)
