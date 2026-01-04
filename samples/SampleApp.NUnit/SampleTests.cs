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
    /// FLAKY TEST TYPE 2: Random/Probabilistic failure.
    /// This test fails randomly ~30% of the time, simulating tests that depend
    /// on non-deterministic behavior like network calls, external APIs, or random data.
    /// </summary>
    [Test]
    [Category("Flaky")]
    [Category("Random")]
    [Description("Demonstrates a flaky test that fails randomly due to probabilistic behavior")]
    public void FlakyTest_RandomFailure_FailsProbabilistically()
    {
        // Use a pseudo-random seed based on time to get different results per run
        var seed = DateTime.Now.Millisecond + DateTime.Now.Second * 1000;
        var random = new Random(seed);
        // CA5394 suppressed: Random generator used intentionally to simulate non-deterministic test behavior
        // for observability testing
#pragma warning disable CA5394 // Do not use insecure randomness
        var randomValue = random.Next(0, 100);
#pragma warning restore CA5394 // Do not use insecure randomness

        // Fails approximately 30% of the time
        // Simulates unreliable external dependencies or non-deterministic operations
        Assert.That(randomValue, Is.GreaterThan(30),
            $"Random failure occurred (value: {randomValue}). " +
            "This simulates a test with non-deterministic behavior like flaky network calls or database connections.");
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
