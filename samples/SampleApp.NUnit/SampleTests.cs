/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace SampleApp.NUnit;

using global::NUnit.Framework;
using Xping.Sdk.NUnit;

#pragma warning disable CA1707 // Identifiers should not contain underscores

/// <summary>
/// Sample tests demonstrating NUnit adapter usage with Xping SDK.
/// </summary>
[TestFixture]
[XpingTrack] // Apply to the entire fixture-tracks all tests in this class
public class SampleTests
{
    [SetUp]
    public void Setup()
    {
        // Test setup code
    }

    [Test]
    [Category("Integration")]
    [Description("Verifies that a passing test is properly tracked")]
    public void PassingTestIsTracked()
    {
        // Arrange
        var value = 42;

        // Act
        var result = value * 2;

        // Assert
        Assert.That(result, Is.EqualTo(84));
    }

    /// <summary>
    /// TIMEOUT TEST: awaiting a dependency that never answers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A correctly written async client passes its cancellation token down to the service call — and
    /// still hangs, because the service accepted the request and stopped responding. The token is
    /// what eventually ends the wait; nothing here reaches an assertion.
    /// </para>
    /// <para>
    /// Xping records this as <c>Outcome = Timeout</c>, not <c>Failed</c>, and stores the 500 ms
    /// budget declared below alongside the measured duration.
    /// </para>
    /// <para>
    /// Uses <c>[CancelAfter]</c> rather than the older <c>[Timeout]</c> deliberately. A blocking
    /// <c>[Timeout]</c> abandons the test thread without ever invoking <c>ITestAction.AfterTest</c>,
    /// which is the hook Xping tracks from — so such a test is not recorded at all. This is a real
    /// NUnit constraint, not an Xping one; see docs/known-limitations.md.
    /// </para>
    /// </remarks>
    [Test]
    [Category("Unit")]
    [Category("Timeout")]
    [CancelAfter(500)]
    [Description("Verifies that a test killed for overrunning its budget is tracked as a timeout")]
    public async Task TimeoutTest_UnresponsiveDependency_AwaitsForever(CancellationToken cancellationToken)
    {
        // Never completes on its own. Only the token NUnit cancels at 500 ms ends the wait.
        var response = await UnresponsiveService.FetchAsync(cancellationToken);

        Assert.That(response, Is.Not.Null);
    }

    [Test]
    [Category("Unit")]
    [Description("Verifies that a test which throws an exception is properly tracked")]
    public void ThrowingTestIsTracked()
    {
        throw new InvalidOperationException("This is a test exception for tracking purposes.");
    }

    /// <summary>
    /// FLAKY TEST TYPE 2: Race-condition / async-dependency failure.
    /// Simulates a service call that races against an internal watchdog timer.
    /// Fails intermittently (~25–35 % of runs) to mimic real-world flakiness caused
    /// by network jitter, momentarily-saturated endpoints, or CPU scheduling variance.
    /// The nondeterminism is subtle: the test logic looks structurally sound at a glance,
    /// reproducing the "heisenbug" pattern where failures resist consistent reproduction.
    /// </summary>
    [Test]
    [Category("Flaky")]
    [Category("Random")]
    [Description("Demonstrates a flaky test that fails randomly due to probabilistic behavior")]
    public async Task FlakyTest_RandomFailure_FailsProbabilistically()
    {
        // Random.Shared is cryptographically seeded by the runtime — no TickCount bias.
        var rng = Random.Shared;
        const int nominalTimeoutMs = 120;

        // ~30 % of runs take the "slow path", simulating network jitter or a briefly
        // saturated downstream service that overruns the caller's internal deadline.
        var simulatedLatencyMs = rng.NextDouble() < 0.30
            ? nominalTimeoutMs + rng.Next(30, 90)   // slow path: 150–210 ms  → fails
            : rng.Next(10, nominalTimeoutMs - 20);  // fast path:  10–100 ms  → passes

        // The watchdog carries ±15 ms of jitter, so the race outcome is non-trivial
        // near the boundary — reproducing genuine heisenbug behaviour under load or on
        // slower CI runners where task-scheduling order is unpredictable.
        var watchdogMs = nominalTimeoutMs + rng.Next(-15, 15);

        var serviceCall = Task.Delay(simulatedLatencyMs);
        var watchdog    = Task.Delay(watchdogMs);

        var winner = await Task.WhenAny(serviceCall, watchdog);

        Assert.That(
            winner == serviceCall,
            $"Watchdog ({watchdogMs} ms) fired before the simulated service responded " +
            $"({simulatedLatencyMs} ms). " +
            "This reproduces flakiness caused by network timeouts, service-side " +
            "back-pressure, or CPU contention that shifts task-scheduling order.");
    }

    [Test]
    [Category("Unit")]
    [Category("Fast")]
    public void AnotherPassingTest()
    {
        // Arrange
        var expected = true;

        // Act & Assert
        Assert.That(expected, Is.True);
    }

    [Test]
    [Category("Integration")]
    [Ignore("Demonstrating skipped test tracking")]
    public void SkippedTestIsTracked()
    {
        Assert.Fail("This test is skipped");
    }

    [Test]
    [Category("Unit")]
    [Description("Test with parameterized input")]
    [TestCase(1, 2, 3)]
    [TestCase(5, 5, 10)]
    [TestCase(0, 0, 0)]
    public void ParameterizedTestIsTracked(int a, int b, int expected)
    {
        var result = a + b;
        Assert.That(result, Is.EqualTo(expected));
    }
}

/// <summary>
/// Sample fixture without class-level XpingTrack - demonstrates method-level tracking.
/// </summary>
[TestFixture]
public class MethodLevelTracking
{
    [Test]
    [XpingTrack] // Apply to a specific method only
    [Description("Only this test will be tracked")]
    public void TrackedTest()
    {
        Assert.Pass("This test is tracked");
    }

    [Test]
    public void UntrackedTest()
    {
        // This test won't be tracked (no XpingTrack attribute)
        Assert.Pass("This test is NOT tracked");
    }
}

/// <summary>
/// A downstream dependency that accepts a request and then never answers it.
/// </summary>
/// <remarks>
/// Backed by a <see cref="TaskCompletionSource{TResult}"/> that nothing ever completes, which is
/// what a hung service looks like to its caller: no exception, no data, no end. Modelled this way
/// rather than with a long sleep because a sleep eventually returns, and the failure being
/// reproduced is precisely the one that does not.
/// </remarks>
internal static class UnresponsiveService
{
    /// <summary>
    /// Issues a request that never completes unless <paramref name="cancellationToken"/> fires.
    /// </summary>
    /// <param name="cancellationToken">Token that abandons the wait, when the caller supplies one.</param>
    /// <returns>A task that never completes successfully.</returns>
    public static async Task<string> FetchAsync(CancellationToken cancellationToken = default)
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using (cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken)))
        {
            return await pending.Task.ConfigureAwait(false);
        }
    }
}
