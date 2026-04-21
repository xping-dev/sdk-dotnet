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
