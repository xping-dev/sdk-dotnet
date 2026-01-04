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
    /// FLAKY TEST TYPE 3: Environment/State-based failure.
    /// This test fails intermittently based on system state (file system, process count, etc.).
    /// Simulates tests that depend on machine state, available resources, or global state.
    /// </summary>
    [Fact]
    [Trait("Category", "Flaky")]
    [Trait("Category", "StateDependency")]
    public void FlakyTest_EnvironmentState_FailsBasedOnSystemState()
    {
        // Simulate environment-dependent behavior using process count as proxy
        // In real scenarios, this could be file locks, port availability, memory pressure, etc.
        var processCount = System.Diagnostics.Process.GetProcesses().Length;
        var isEvenSecond = DateTime.Now.Second % 2 == 0;

        // Fails when both conditions are met (approximately 25% of the time)
        // Simulates tests that depend on external system state
        var shouldPass = processCount % 3 != 0 || !isEvenSecond;

        Assert.True(shouldPass,
            $"Environment state conflict detected (processes: {processCount}, second: {DateTime.Now.Second}). " +
            "This simulates a test that fails due to system state like file locks, port conflicts, or resource contention.");
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
