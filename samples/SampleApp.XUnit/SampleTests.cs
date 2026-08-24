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
    /// <summary>
    /// TIMEOUT TEST: awaiting a dependency that never answers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The service accepted the request and stopped responding, so the await never resumes and the
    /// test never reaches its assertion. xUnit's own timeout is what ends it.
    /// </para>
    /// <para>
    /// Xping records this as <c>Outcome = Timeout</c>, not <c>Failed</c>, and stores the 500 ms
    /// budget declared below alongside the measured duration.
    /// </para>
    /// <para>
    /// The test is async deliberately: xUnit applies a timeout only to async tests and fails a
    /// synchronous one outright with "Tests marked with Timeout are only supported for async tests".
    /// Xping records that case as <c>Failed</c>, because it is a misconfigured test rather than a
    /// hanging one.
    /// </para>
    /// </remarks>
    [Fact(Timeout = 500)]
    [Trait("Category", "Unit")]
    [Trait("Category", "Timeout")]
    public async Task TimeoutTest_UnresponsiveDependency_AwaitsForever()
    {
        // Never completes. xUnit races this await against its own 500 ms budget and kills the test.
        var response = await UnresponsiveService.FetchAsync();

        Assert.NotNull(response);
    }

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

/// <summary>
/// Shared state every test in <see cref="BrokenFixtureTests"/> depends on, which fails to build.
/// </summary>
public sealed class UnprovisionedDatabase
{
    public UnprovisionedDatabase() =>
        throw new InvalidOperationException("The shared test database was never provisioned.");
}

/// <summary>
/// BROKEN FIXTURE: a class fixture that throws, taking every test in the class down with it.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is wrong with the three tests below. The defect is in
/// <see cref="UnprovisionedDatabase"/>'s constructor, and it is reported once per test that tried to
/// use it — which is why one broken fixture arrives looking like a class full of broken tests.
/// </para>
/// <para>
/// Xping records <c>Site = FixtureSetup</c> and names the fixture, so the report emits one
/// <c>broken fixture</c> finding rather than three <c>always failing</c> ones.
/// </para>
/// <para>
/// This is the one case a framework marks for itself: xUnit wraps the failure in
/// <c>Xunit.Sdk.TestClassException</c> and names the fixture type in the message. A
/// <c>ICollectionFixture&lt;T&gt;</c> that throws is the same event but arrives unwrapped, and is
/// recognised from its constructor frame instead.
/// </para>
/// </remarks>
public class BrokenFixtureTests(UnprovisionedDatabase database) : IClassFixture<UnprovisionedDatabase>
{
    private readonly UnprovisionedDatabase _database = database;

    [Fact]
    public void FirstTestNeedingTheDatabase() => Assert.NotNull(_database);

    [Fact]
    public void SecondTestNeedingTheDatabase() => Assert.NotNull(_database);

    [Fact]
    public void ThirdTestNeedingTheDatabase() => Assert.NotNull(_database);
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
