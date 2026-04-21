/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

#pragma warning disable CA1707 // Identifiers should not contain underscores

using Xping.Sdk.MSTest;

namespace SampleApp.MSTest;

/// <summary>
/// Sample test class demonstrating Xping SDK integration with MSTest.
/// Inherits from XpingTestBase for automatic test tracking.
/// </summary>
[TestClass]
public class CalculatorTests : XpingTestBase
{
    [TestMethod]
    [TestCategory("Unit")]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var result = Calculator.Add(2, 3);
        Assert.AreEqual(5, result);
    }

    [TestMethod("Verifies that a test which throws an exception is properly tracked")]
    [TestCategory("Unit")]
    public void ThrowingTestIsTracked()
    {
        throw new InvalidOperationException("This is a test exception for tracking purposes.");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fast")]
    public void Subtract_TwoNumbers_ReturnsDifference()
    {
        var result = Calculator.Subtract(10, 4);
        Assert.AreEqual(6, result);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [Ignore("Demonstration of skipped test")]
    public void SlowIntegrationTest()
    {
        // This test is skipped
        Assert.Fail("Should not run");
    }

    /// <summary>
    /// FLAKY TEST TYPE 1: Race-condition / async-dependency failure.
    /// Simulates a service call that races against an internal watchdog timer.
    /// Fails intermittently (~25–35 % of runs) to mimic real-world flakiness caused
    /// by network jitter, momentarily-saturated endpoints, or CPU scheduling variance.
    /// The nondeterminism is subtle: the test logic looks structurally sound at a glance,
    /// reproducing the "heisenbug" pattern where failures resist consistent reproduction.
    /// </summary>
    [TestMethod]
    [TestCategory("Flaky")]
    [TestCategory("RaceCondition")]
    public async Task FlakyTest_RaceCondition_FailsIntermittently()
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

        Assert.IsTrue(
            winner == serviceCall,
            $"Watchdog ({watchdogMs} ms) fired before the simulated service responded " +
            $"({simulatedLatencyMs} ms). " +
            "This reproduces flakiness caused by network timeouts, service-side " +
            "back-pressure, or CPU contention that shifts task-scheduling order.");
    }

    [DataTestMethod]
    [DataRow(2, 3, 5)]
    [DataRow(10, 5, 15)]
    [DataRow(-1, 1, 0)]
    [TestCategory("Unit")]
    public void Add_MultipleInputs_ReturnsExpectedSum(int a, int b, int expected)
    {
        var result = Calculator.Add(a, b);
        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow(10, 2, 5)]
    [DataRow(20, 4, 5)]
    [DataRow(100, 10, 10)]
    [TestCategory("Unit")]
    public void Divide_ValidInputs_ReturnsQuotient(int a, int b, int expected)
    {
        var result = Calculator.Divide(a, b);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [ExpectedException(typeof(DivideByZeroException))]
    public void Divide_ByZero_ThrowsException()
    {
        Calculator.Divide(10, 0);
    }
}

/// <summary>
/// Sample test class demonstrating property-based test configuration.
/// </summary>
[TestClass]
public class StringTests : XpingTestBase
{
    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Team", "TeamA")]
    [TestProperty("Importance", "High")]
    public void String_Concatenation_ReturnsExpected()
    {
        var result = string.Concat("Hello", " ", "World");
        Assert.AreEqual("Hello World", result);
    }

    [DataTestMethod]
    [DataRow("hello", 5)]
    [DataRow("world", 5)]
    [DataRow("", 0)]
    [TestCategory("Unit")]
    public void String_Length_ReturnsExpected(string input, int expectedLength)
    {
        Assert.AreEqual(expectedLength, input.Length);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void String_IsNullOrEmpty_EmptyString_ReturnsTrue()
    {
        Assert.IsTrue(string.IsNullOrEmpty(string.Empty));
    }
}

/// <summary>
/// Sample calculator class for demonstration.
/// </summary>
public static class Calculator
{
    public static int Add(int a, int b) => a + b;
    public static int Subtract(int a, int b) => a - b;
    public static int Divide(int a, int b) => a / b;
}
