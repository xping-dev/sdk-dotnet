/*
 * © 2025 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

namespace Xping.Sdk.Core.Tests.Models;

using Xping.Sdk.Core.Models;

public sealed class TestOutcomeTests
{
    [Fact]
    public void TestOutcomeShouldHaveCorrectValues()
    {
        // Assert
        Assert.Equal(0, (int)TestOutcome.Passed);
        Assert.Equal(1, (int)TestOutcome.Failed);
        Assert.Equal(2, (int)TestOutcome.Skipped);
        Assert.Equal(3, (int)TestOutcome.Inconclusive);
        Assert.Equal(4, (int)TestOutcome.NotExecuted);
    }

    [Theory]
    [InlineData(TestOutcome.Passed, "Passed")]
    [InlineData(TestOutcome.Failed, "Failed")]
    [InlineData(TestOutcome.Skipped, "Skipped")]
    [InlineData(TestOutcome.Inconclusive, "Inconclusive")]
    [InlineData(TestOutcome.NotExecuted, "NotExecuted")]
    public void TestOutcomeShouldConvertToString(TestOutcome outcome, string expected)
    {
        // Act
        var result = outcome.ToString();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TestOutcomeShouldHaveFiveValues()
    {
        // Arrange & Act
        var values = Enum.GetValues<TestOutcome>();

        // Assert
        Assert.Equal(5, values.Length);
    }
}
