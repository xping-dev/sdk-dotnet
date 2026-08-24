/*
 * © 2026 Xping.io. All Rights Reserved.
 * License: [MIT]
 */

using Xping.Sdk.Core.Models.Executions;

namespace Xping.Sdk.Core.Tests.Models;

public class TestOutcomeExtensionsTests
{
    [Theory]
    [InlineData(TestOutcome.Failed)]
    [InlineData(TestOutcome.Timeout)]
    public void IsFailure_FailingOutcome_ReturnsTrue(TestOutcome outcome)
    {
        Assert.True(outcome.IsFailure());
    }

    [Theory]
    [InlineData(TestOutcome.Passed)]
    [InlineData(TestOutcome.Skipped)]
    [InlineData(TestOutcome.Inconclusive)]
    [InlineData(TestOutcome.NotExecuted)]
    public void IsFailure_NonFailingOutcome_ReturnsFalse(TestOutcome outcome)
    {
        Assert.False(outcome.IsFailure());
    }

    /// <summary>
    /// Pins the classification of every declared member, so that adding one forces a decision about
    /// whether it turns a run red instead of defaulting to green by omission. A green default is
    /// exactly how a timed-out test used to pass unnoticed.
    /// </summary>
    [Fact]
    public void IsFailure_EveryDeclaredOutcome_IsClassifiedDeliberately()
    {
        var expected = new Dictionary<TestOutcome, bool>
        {
            [TestOutcome.Passed] = false,
            [TestOutcome.Failed] = true,
            [TestOutcome.Skipped] = false,
            [TestOutcome.Inconclusive] = false,
            [TestOutcome.NotExecuted] = false,
            [TestOutcome.Timeout] = true,
        };

        var declared = Enum.GetValues<TestOutcome>();

        Assert.Equal(expected.Count, declared.Length);

        foreach (TestOutcome outcome in declared)
        {
            Assert.True(expected.ContainsKey(outcome), $"TestOutcome.{outcome} has no expected classification.");
            Assert.Equal(expected[outcome], outcome.IsFailure());
        }
    }
}
