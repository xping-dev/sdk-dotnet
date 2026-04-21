/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace SampleApp.XUnit;

using Xunit;

#pragma warning disable CA1707 // Identifiers should not contain underscores

/// <summary>
/// Sample tests demonstrating xUnit adapter usage with Xping SDK.
/// The Xping custom test framework automatically tracks and flushes test results.
/// </summary>
public class SampleTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void PassingTestIsTracked()
    {
        // Arrange
        var value = 42;

        // Act
        var result = value * 2;

        // Assert
        Assert.Equal(84, result);
    }

    [Fact(DisplayName = "Verifies that a test which throws an exception is properly tracked")]
    [Trait("Category", "Unit")]
    public void ThrowingTestIsTracked()
    {
        throw new InvalidOperationException("This is a test exception for tracking purposes.");
    }

    /// <summary>
    /// FLAKY TEST TYPE 3: Race-condition / async-dependency failure.
    /// Simulates a service call that races against an internal watchdog timer.
    /// Fails intermittently (~25–35 % of runs) to mimic real-world flakiness caused
    /// by network jitter, momentarily-saturated endpoints, or CPU scheduling variance.
    /// The nondeterminism is subtle: the test logic looks structurally sound at a glance,
    /// reproducing the "heisenbug" pattern where failures resist consistent reproduction.
    /// </summary>
    [Fact]
    [Trait("Category", "Flaky")]
    [Trait("Category", "StateDependency")]
    public async Task FlakyTest_EnvironmentState_FailsBasedOnSystemState()
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

        Assert.True(
            winner == serviceCall,
            $"Watchdog ({watchdogMs} ms) fired before the simulated service responded " +
            $"({simulatedLatencyMs} ms). " +
            "This reproduces flakiness caused by network timeouts, service-side " +
            "back-pressure, or CPU contention that shifts task-scheduling order.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Fast")]
    public void AnotherPassingTest()
    {
        // Arrange
        var expected = true;

        // Act & Assert
        Assert.True(expected);
    }

    [Fact(Skip = "Demonstrating skipped test tracking")]
    [Trait("Category", "Integration")]
    public void SkippedTestIsTracked()
    {
        Assert.Fail("This test is skipped");
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(5, 5, 10)]
    [InlineData(0, 0, 0)]
    [Trait("Category", "Unit")]
    public void ParameterizedTestIsTracked(int a, int b, int expected)
    {
        var result = a + b;
        Assert.Equal(expected, result);
    }

    [Theory]
    [MemberData(nameof(GetTestData))]
    [Trait("Category", "Unit")]
    public void MemberDataTestIsTracked(string input, int expectedLength)
    {
        Assert.Equal(expectedLength, input.Length);
    }

    public static TheoryData<string, int> GetTestData()
    {
        return new TheoryData<string, int> { { "hello", 5 }, { "world", 5 }, { "xunit", 5 }, };
    }
}

// <summary>
// Sample tests demonstrating test collections and fixtures.
// </summary>
[Collection("Sample Collection")]
public class CollectionTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void TestInCollection()
    {
        Assert.True(true);
    }
}

/// <summary>
/// Collection definition for grouping tests.
/// </summary>
[CollectionDefinition("Sample Collection")]
public class SampleCollection
{
}
